﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Windows.Forms;
using StarMap2010.Models;

namespace StarMap2010
{
    public class MapCanvas : Control
    {
        private List<StarSystemInfo> data;
        private StarSystemInfo selected;

        private float centerX, centerY;
        private float baseScale; // px per LY at zoom=1

        public float ZoomFactor { get; set; }

        private const float GRID_SPACING_LY = 10f;

        // Label behavior
        private const float LABEL_SHOW_ZOOM = 1.2f;
        private const float LABEL_FULL_ZOOM = 2.0f;
        private const int LABEL_DX = 10;
        private const int LABEL_DY = -10;

        // Cap how much stars + fonts scale. (Positions keep zooming.)
        private const float SYMBOL_ZOOM_CAP = LABEL_FULL_ZOOM;

        public delegate void SystemSelectedHandler(StarSystemInfo s);
        public event SystemSelectedHandler SystemSelected;

        // Measure mode
        public bool MeasureMode { get; set; }

        private StarSystemInfo _measureA;
        private StarSystemInfo _measureB;

        public event Action<StarSystemInfo, StarSystemInfo, float> MeasureCompleted;


        // ---- Gates overlay ----
        private List<JumpGateRenderableLink> _gateLinks = new List<JumpGateRenderableLink>();
        public bool ShowGates { get; set; }

        // ---- Background tile ----
        private Image _spaceTile; // loaded once by MainForm

        // ---- Background look tuning ----
        // Goal: keep the background quiet so systems/lines are the focus.
        private const float BG_TILE_SATURATION = 0.35f;  // 0..1 (1 = original)
        private const float BG_TILE_BRIGHTNESS = 0.55f;  // 0..1 (1 = original)
        private const int BG_DIM_ALPHA = 85;             // 0..255 black overlay
        private const int BG_VIGNETTE_ALPHA = 55;        // 0..255 edge darken

        public MapCanvas()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer,
                true);

            UpdateStyles();
            BackColor = Color.Black;
            ZoomFactor = 1f;

            ShowGates = false;
        }

        // ---- Background tile API ----
        public void SetSpaceTile(Image img)
        {
            _spaceTile = img;
            Invalidate();
        }

        public void SetGateLinks(List<JumpGateRenderableLink> links)
        {
            _gateLinks = (links != null) ? links : new List<JumpGateRenderableLink>();
            Invalidate();
        }

        public void SetWorld(float cx, float cy, float scale)
        {
            centerX = cx;
            centerY = cy;
            baseScale = scale;
        }

        public void SetData(List<StarSystemInfo> stars)
        {
            data = stars;
            Invalidate();
        }

        public void SetSelected(StarSystemInfo s)
        {
            selected = s;
            Invalidate();
        }

        private float GetSymbolZoom()
        {
            float z = ZoomFactor;
            if (z <= 0.0001f) return 1f;
            return (z < SYMBOL_ZOOM_CAP) ? z : SYMBOL_ZOOM_CAP;
        }

        // Screen-space position of a star under FULL zoom around center
        private PointF ToScreenZoomed(StarSystemInfo s)
        {
            return new PointF(
                centerX + s.ScreenX * ZoomFactor,
                centerY + s.ScreenY * ZoomFactor);
        }

        // ---- Mirror-tiled background (no seams) ----
        private void DrawMirroredTiledBackground(Graphics g)
        {
            if (_spaceTile == null) return;

            // Avoid interpolation “hairline seams”
            g.SmoothingMode = SmoothingMode.None;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;

            int tw = _spaceTile.Width;
            int th = _spaceTile.Height;
            if (tw <= 0 || th <= 0) return;

            // Desaturate + darken the tile so it doesn't compete with symbols.
            // This keeps your existing space art, just makes it "atlas quiet".
            ImageAttributes attrs = null;
            try
            {
                attrs = new ImageAttributes();

                float s = BG_TILE_SATURATION;
                if (s < 0f) s = 0f;
                if (s > 1f) s = 1f;

                float b = BG_TILE_BRIGHTNESS;
                if (b < 0f) b = 0f;
                if (b > 1f) b = 1f;

                // Luminance coefficients (NTSC-ish)
                const float lr = 0.3086f;
                const float lg = 0.6094f;
                const float lb = 0.0820f;

                float a = (1f - s);

                // Saturation matrix, then brightness via RGB scale.
                // Note: We bake brightness into the diagonal terms for speed.
                ColorMatrix cm = new ColorMatrix(new float[][]
                {
                    new float[] { b * (a*lr + s), b * (a*lr),     b * (a*lr),     0f, 0f },
                    new float[] { b * (a*lg),     b * (a*lg + s), b * (a*lg),     0f, 0f },
                    new float[] { b * (a*lb),     b * (a*lb),     b * (a*lb + s), 0f, 0f },
                    new float[] { 0f,             0f,             0f,             1f, 0f },
                    new float[] { 0f,             0f,             0f,             0f, 1f }
                });

                attrs.SetColorMatrix(cm, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
            }
            catch
            {
                // If ImageAttributes fails for any reason, fall back to raw tile.
                if (attrs != null) attrs.Dispose();
                attrs = null;
            }

            // --- Parallax based on scroll position (your pan is scrollbar-driven) ---
            const float PARALLAX = 0.05f; // try 0.02f subtle, 0.05f noticeable, 0.10f strong

            int scrollX = 0;
            int scrollY = 0;

            ScrollableControl sc = this.Parent as ScrollableControl;
            if (sc != null)
            {
                // AutoScrollPosition is NEGATIVE when scrolled
                Point asp = sc.AutoScrollPosition;
                scrollX = -asp.X;
                scrollY = -asp.Y;
            }

            int offsetX = (int)(scrollX * PARALLAX);
            int offsetY = (int)(scrollY * PARALLAX);

            // Seamless wrap offsets
            int ox = offsetX % tw;
            int oy = offsetY % th;
            if (ox < 0) ox += tw;
            if (oy < 0) oy += th;

            // Draw enough tiles to cover the whole control even while offset
            int tilesX = (Width + tw - 1) / tw + 3;
            int tilesY = (Height + th - 1) / th + 3;

            for (int ty = -1; ty < tilesY; ty++)
            {
                for (int tx = -1; tx < tilesX; tx++)
                {
                    int dx = tx * tw - ox;
                    int dy = ty * th - oy;

                    bool flipX = (tx & 1) == 1;
                    bool flipY = (ty & 1) == 1;

                    GraphicsState state = g.Save();

                    g.TranslateTransform(dx + (flipX ? tw : 0),
                                         dy + (flipY ? th : 0));
                    g.ScaleTransform(flipX ? -1f : 1f,
                                     flipY ? -1f : 1f);

                    if (attrs != null)
                    {
                        g.DrawImage(
                            _spaceTile,
                            new Rectangle(0, 0, tw, th),
                            0, 0, tw, th,
                            GraphicsUnit.Pixel,
                            attrs);
                    }
                    else
                    {
                        g.DrawImage(_spaceTile, 0, 0, tw, th);
                    }

                    g.Restore(state);
                }
            }

            if (attrs != null) attrs.Dispose();

            // Darken the whole background so symbols/labels pop.
            using (var dim = new SolidBrush(Color.FromArgb(BG_DIM_ALPHA, 0, 0, 0)))
            {
                g.FillRectangle(dim, 0, 0, Width, Height);
            }

            // Subtle vignette to push attention toward the map content.
            // Keep this very light (it's easy to overdo).
            using (GraphicsPath gp = new GraphicsPath())
            {
                gp.AddRectangle(new Rectangle(0, 0, Width, Height));
                using (PathGradientBrush pgb = new PathGradientBrush(gp))
                {
                    pgb.CenterColor = Color.FromArgb(0, 0, 0, 0);
                    pgb.SurroundColors = new Color[] { Color.FromArgb(BG_VIGNETTE_ALPHA, 0, 0, 0) };
                    g.FillRectangle(pgb, 0, 0, Width, Height);
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g = e.Graphics;

            // Clear and draw background first (screen space)
            g.Clear(Color.Black);
            DrawMirroredTiledBackground(g);

            // PASS 1: world (grid scales with zoom forever)
            g.SmoothingMode = SmoothingMode.None;
            g.PixelOffsetMode = PixelOffsetMode.Half;

            g.TranslateTransform(centerX, centerY);
            g.ScaleTransform(ZoomFactor, ZoomFactor);
            g.TranslateTransform(-centerX, -centerY);

            DrawGrid(g);

            if (data == null)
            {
                g.ResetTransform();
                return;
            }

            // PASS 2: screen space (symbols capped)
            g.ResetTransform();

            g.SmoothingMode = SmoothingMode.None;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            // Draw gates FIRST so stars sit on top
            if (ShowGates)
            {
                DrawGateLinks(g);
            }

            int labelAlpha = GetLabelAlpha(ZoomFactor);
            float symbolZoom = GetSymbolZoom();

            float fontSize = 6f * symbolZoom;
            if (fontSize > 12f) fontSize = 12f;

            using (Font labelFont = new Font("Arial", fontSize))
            {
                for (int i = 0; i < data.Count; i++)
                {
                    StarSystemInfo s = data[i];

                    PointF p = ToScreenZoomed(s);

                    // IMPORTANT: round once for stable pixel alignment
                    int sx = (int)Math.Round(p.X);
                    int sy = (int)Math.Round(p.Y);

                    DrawStarSun(g, sx, sy, s.FactionColor, symbolZoom, s.primaryStarColor);

                    if (selected != null && object.ReferenceEquals(selected, s))
                    {
                        using (Pen pen = new Pen(Color.FromArgb(220, 255, 255, 0), 2f))
                        {
                            float r = 16f * symbolZoom;
                            if (r > 24f) r = 24f;
                            g.DrawEllipse(pen, sx - r, sy - r, r * 2, r * 2);
                        }
                    }

                    if (labelAlpha > 0)
                    {
                        string label = !string.IsNullOrEmpty(s.SystemName) ? s.SystemName : s.RealSystemName;
                        if (!string.IsNullOrEmpty(label))
                        {
                            Color baseCol = ColorUtil.FromSqliteHex(ColorUtil.HexWithTransparency(s.FactionColor, 0));
                            Color brushColor = Color.FromArgb(labelAlpha, baseCol.R, baseCol.G, baseCol.B);

                            using (SolidBrush b = new SolidBrush(brushColor))
                            {
                                g.DrawString(label, labelFont, b, sx + LABEL_DX, sy + LABEL_DY);
                            }
                        }
                    }
                }
            }
        }

        // ---------- Gate coloring helpers ----------
        private static string NormGateType(string t)
        {
            if (string.IsNullOrWhiteSpace(t)) return "standard";
            return t.Trim().ToLowerInvariant();
        }

        private static Color GateTypeToColor(string gateType)
        {
            // Muted palette for black background
            switch (NormGateType(gateType))
            {
                case "legacy": return Color.FromArgb(180, 95, 95, 95);      // dark iron gray
                case "standard": return Color.FromArgb(180, 165, 165, 165); // soft silver
                case "advanced": return Color.FromArgb(180, 60, 120, 150);  // steel blue
                case "military": return Color.FromArgb(180, 140, 60, 60);   // dark red oxide
                default: return Color.FromArgb(180, 165, 165, 165);
            }
        }

        // Decide a single type for the link.
        // Rule: military wins, then advanced, then standard, then legacy.
        private static string EffectiveLinkGateType(JumpGateRenderableLink link)
        {
            string a = NormGateType(link != null ? link.GateTypeA : null);
            string b = NormGateType(link != null ? link.GateTypeB : null);

            if (a == "military" || b == "military") return "military";
            if (a == "advanced" || b == "advanced") return "advanced";
            if (a == "standard" || b == "standard") return "standard";
            return "legacy";
        }

        private static string NormStatus(string st)
        {
            if (string.IsNullOrWhiteSpace(st)) return "open";
            return st.Trim().ToLowerInvariant();
        }

        private static int StatusToAlpha(string status)
        {
            // Keep open brightest; closed dimmest
            switch (NormStatus(status))
            {
                case "restricted": return 170;
                case "interdicted": return 120;
                case "closed": return 70;
                default: return 190; // open
            }
        }

        private static void ApplyStatusStyle(Pen pen, string status)
        {
            status = (status ?? "open").Trim().ToLowerInvariant();
            if (status == "open")
            {
                pen.DashStyle = DashStyle.Solid;
                return;
            }

            pen.DashStyle = DashStyle.Custom;
            pen.DashCap = DashCap.Round;

            switch (status)
            {
                case "restricted": // long dash
                    pen.DashPattern = new float[] { 8f, 4f };
                    break;
                case "interdicted": // dotted
                    pen.DashPattern = new float[] { 1f, 3f };
                    break;
                case "closed": // dash-dot-ish
                    pen.DashPattern = new float[] { 6f, 3f, 1f, 3f };
                    break;
                default:
                    pen.DashPattern = new float[] { 8f, 4f };
                    break;
            }
        }

        // Wider + higher contrast line: shadow stroke + colored stroke
        private void DrawGateLinks(Graphics g)
        {
            if (_gateLinks == null || _gateLinks.Count == 0 || data == null || data.Count == 0) return;

            // Make gate lines look clean
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            float w = 1.8f * GetSymbolZoom();
            if (w < 1.6f) w = 1.6f;
            if (w > 3.0f) w = 3.0f;

            var byId = new Dictionary<string, StarSystemInfo>(StringComparer.Ordinal);
            for (int i = 0; i < data.Count; i++)
            {
                var s = data[i];
                if (s != null && !string.IsNullOrEmpty(s.SystemId)) byId[s.SystemId] = s;
            }

            for (int i = 0; i < _gateLinks.Count; i++)
            {
                var link = _gateLinks[i];
                if (link == null) continue;

                StarSystemInfo a, b;
                if (!byId.TryGetValue(link.SystemAId, out a)) continue;
                if (!byId.TryGetValue(link.SystemBId, out b)) continue;

                PointF pa = ToScreenZoomed(a);
                PointF pb = ToScreenZoomed(b);

                int ax = (int)Math.Round(pa.X);
                int ay = (int)Math.Round(pa.Y);
                int bx = (int)Math.Round(pb.X);
                int by = (int)Math.Round(pb.Y);

                // Color by gate type (endpoint-derived)
                string effectiveType = EffectiveLinkGateType(link);
                Color typeColor = GateTypeToColor(effectiveType);

                // Modify by status
                string st = NormStatus(link.Status);
                int aLine = StatusToAlpha(st);

                Color lineCol = Color.FromArgb(aLine, typeColor.R, typeColor.G, typeColor.B);
                int shadowAlpha = Math.Min(160, aLine);

                using (var penShadow = new Pen(Color.FromArgb(shadowAlpha, 0, 0, 0), w + 1.6f))
                using (var penLine = new Pen(lineCol, w))
                {
                    penShadow.StartCap = LineCap.Round;
                    penShadow.EndCap = LineCap.Round;

                    penLine.StartCap = LineCap.Round;
                    penLine.EndCap = LineCap.Round;

                    ApplyStatusStyle(penLine, st);

                    g.DrawLine(penShadow, ax, ay, bx, by);
                    g.DrawLine(penLine, ax, ay, bx, by);
                }
            }

            // restore crisp defaults for stars
            g.SmoothingMode = SmoothingMode.None;
            g.PixelOffsetMode = PixelOffsetMode.Half;
        }

        private void DrawGrid(Graphics g)
        {
            float spacingWorld = GRID_SPACING_LY * baseScale;
            float spacingScreen = spacingWorld * ZoomFactor;
            if (spacingScreen < 8f) return;

            float invZ = 1f / ZoomFactor;

            float worldLeft = centerX + (0f - centerX) * invZ;
            float worldRight = centerX + (Width - centerX) * invZ;
            float worldTop = centerY + (0f - centerY) * invZ;
            float worldBottom = centerY + (Height - centerY) * invZ;

            using (Pen pen = new Pen(Color.FromArgb(40, 40, 40), 1f))
            using (Pen solPen = new Pen(Color.FromArgb(70, 70, 70), 1.5f))
            {
                const float EPS = 0.01f;

                float startX = centerX + (float)Math.Floor((worldLeft - centerX) / spacingWorld) * spacingWorld;
                for (float x = startX; x <= worldRight; x += spacingWorld)
                {
                    float xr = (float)Math.Round(x);
                    bool isSolAxis = Math.Abs(x - centerX) < EPS;
                    g.DrawLine(isSolAxis ? solPen : pen, xr, worldTop, xr, worldBottom);
                }

                float startY = centerY + (float)Math.Floor((worldTop - centerY) / spacingWorld) * spacingWorld;
                for (float y = startY; y <= worldBottom; y += spacingWorld)
                {
                    float yr = (float)Math.Round(y);
                    bool isSolAxis = Math.Abs(y - centerY) < EPS;
                    g.DrawLine(isSolAxis ? solPen : pen, worldLeft, yr, worldRight, yr);
                }
            }
        }

        public StarSystemInfo HitTest(Point mouseCanvasPt)
        {
            if (data == null || data.Count == 0) return null;

            const int R = 12;
            int r2 = R * R;

            for (int i = 0; i < data.Count; i++)
            {
                StarSystemInfo s = data[i];

                PointF p = ToScreenZoomed(s);
                int sx = (int)Math.Round(p.X);
                int sy = (int)Math.Round(p.Y);

                int dx = mouseCanvasPt.X - sx;
                int dy = mouseCanvasPt.Y - sy;

                if (dx * dx + dy * dy <= r2) return s;
            }

            return null;
        }

        private static int GetLabelAlpha(float zoom)
        {
            if (zoom < LABEL_SHOW_ZOOM) return 0;
            if (zoom >= LABEL_FULL_ZOOM) return 255;

            float t = (zoom - LABEL_SHOW_ZOOM) / (LABEL_FULL_ZOOM - LABEL_SHOW_ZOOM);
            int a = (int)(t * 255f);
            if (a < 0) a = 0;
            if (a > 255) a = 255;
            return a;
        }

        private static void DrawStarSun(Graphics graphic, int sx, int sy, string starColor, float symbolZoom, string primaryStarColor)
        {
            // Visual grammar:
            // - Inner sphere = primary star type (with subtle 3D shading)
            // - Outer ring   = government/faction (replaces the noisy "rays")

            float ringR = 9f * symbolZoom;
            float coreR = 5f * symbolZoom;

            if (ringR > 18f) ringR = 18f;
            if (coreR > 10f) coreR = 10f;

            Color ringColor = ColorUtil.FromSqliteHex(starColor);
            Color coreColor = ColorUtil.FromSqliteHex(primaryStarColor);

            // Outer government ring (crisp + calm)
            float ringW = 1.6f * symbolZoom;
            if (ringW < 1.2f) ringW = 1.2f;
            if (ringW > 2.4f) ringW = 2.4f;

            using (Pen p = new Pen(Color.FromArgb(210, ringColor.R, ringColor.G, ringColor.B), ringW))
            {
                p.Alignment = PenAlignment.Center;
                graphic.DrawEllipse(p, sx - ringR, sy - ringR, ringR * 2f, ringR * 2f);
            }

            // Inner star "sphere" using a path gradient (subtle highlight)
            RectangleF rc = new RectangleF(sx - coreR, sy - coreR, coreR * 2f, coreR * 2f);
            using (GraphicsPath gp = new GraphicsPath())
            {
                gp.AddEllipse(rc);
                using (PathGradientBrush pgb = new PathGradientBrush(gp))
                {
                    // Offset the highlight toward top-left (global light direction)
                    pgb.CenterPoint = new PointF(sx - coreR * 0.35f, sy - coreR * 0.35f);

                    // Center is slightly brighter; edge slightly darker
                    Color center = Mix(coreColor, Color.White, 0.25f);
                    Color edge = Mix(coreColor, Color.Black, 0.25f);

                    pgb.CenterColor = center;
                    pgb.SurroundColors = new Color[] { edge };

                    graphic.FillEllipse(pgb, rc);
                }
            }

            // Thin outline helps tiny stars remain crisp on bright backgrounds
            using (Pen outline = new Pen(Color.FromArgb(110, 0, 0, 0), 1f))
            {
                graphic.DrawEllipse(outline, rc);
            }
        }

        private static Color Mix(Color a, Color b, float t)
        {
            if (t <= 0f) return a;
            if (t >= 1f) return b;

            int r = (int)(a.R + (b.R - a.R) * t);
            int g = (int)(a.G + (b.G - a.G) * t);
            int bl = (int)(a.B + (b.B - a.B) * t);

            if (r < 0) r = 0; if (r > 255) r = 255;
            if (g < 0) g = 0; if (g > 255) g = 255;
            if (bl < 0) bl = 0; if (bl > 255) bl = 255;

            return Color.FromArgb(255, r, g, bl);
        }

        public PointF ScreenToWorld(Point canvasPt)
        {
            float z = (ZoomFactor <= 0.0001f) ? 1f : ZoomFactor;
            float wx = centerX + (canvasPt.X - centerX) / z;
            float wy = centerY + (canvasPt.Y - centerY) / z;
            return new PointF(wx, wy);
        }

        public PointF WorldToScreen(PointF worldPt)
        {
            float z = (ZoomFactor <= 0.0001f) ? 1f : ZoomFactor;
            float sx = centerX + (worldPt.X - centerX) * z;
            float sy = centerY + (worldPt.Y - centerY) * z;
            return new PointF(sx, sy);
        }
        public void ClearMeasure()
        {
            _measureA = null;
            _measureB = null;
            Invalidate();
        }

        private static float DistanceLy(StarSystemInfo a, StarSystemInfo b)
        {
            if (a == null || b == null) return 0f;

            // Adjust these field names to match your model:
            // commonly something like a.XLy / a.YLy, or a.X / a.Y (in LY space)
            float dx = (float)(a.XReal - b.XReal);
            float dy = (float)(a.YReal - b.YReal);
            float dz = (float)(a.ZReal - b.ZReal);
            return (float)Math.Sqrt((dx * dx + dy * dy) + dz * dz);
        }
    }
}
