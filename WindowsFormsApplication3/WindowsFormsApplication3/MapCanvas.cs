using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace StarMap2010
{
    public class MapCanvas : Control
    {
        private List<StarInfo> data;
        private float centerX, centerY;
        private float baseScale;

        public float ZoomFactor { get; set; }

        // Grid spacing (ly) – must match MainForm
        private const float GRID_SPACING_LY = 10f;

        public MapCanvas()
        {
            // Proper flicker-free drawing for WinForms
            this.SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer,
                true);

            this.UpdateStyles();
            this.BackColor = Color.Black;
            this.ZoomFactor = 1f;
        }

        // =========================
        // Data + world setup
        // =========================
        public void SetData(List<StarInfo> stars)
        {
            data = stars;
        }

        public void SetWorld(float cx, float cy, float scale)
        {
            centerX = cx;
            centerY = cy;
            baseScale = scale;
        }

        // =========================
        // Drawing
        // =========================
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Black);

            DrawGrid(g);

            if (data == null) return;

            foreach (StarInfo s in data)
            {
                Point p = Project(s);
                DrawStarSun(g, p.X, p.Y, s.ColorName, s.Government);
            }
        }

        // =========================
        // Projection
        // =========================
        public Point Project(StarInfo s)
        {
            float scaleNow = baseScale * ZoomFactor;

            // Same mapping as VB6:
            // X right, Y up
            int sx = (int)Math.Round(centerX + s.XReal * scaleNow);
            int sy = (int)Math.Round(centerY - s.YReal * scaleNow);

            return new Point(sx, sy);
        }

        // =========================
        // Hit testing
        // =========================
        public StarInfo HitTest(Point canvasPoint, int radiusPx)
        {
            if (data == null) return null;

            int r2 = radiusPx * radiusPx;

            for (int i = 0; i < data.Count; i++)
            {
                Point p = Project(data[i]);
                int dx = canvasPoint.X - p.X;
                int dy = canvasPoint.Y - p.Y;

                if (dx * dx + dy * dy <= r2)
                    return data[i];
            }

            return null;
        }

        // =========================
        // Grid
        // =========================
        private void DrawGrid(Graphics g)
        {
            float spacingPx = GRID_SPACING_LY * baseScale * ZoomFactor;
            if (spacingPx < 15f) return;

            using (Pen pen = new Pen(Color.FromArgb(40, 40, 40), 1f))
            {
                // Vertical right
                for (float x = centerX; x <= Width; x += spacingPx)
                    g.DrawLine(pen, x, 0, x, Height);

                // Vertical left
                for (float x = centerX - spacingPx; x >= 0; x -= spacingPx)
                    g.DrawLine(pen, x, 0, x, Height);

                // Horizontal down
                for (float y = centerY; y <= Height; y += spacingPx)
                    g.DrawLine(pen, 0, y, Width, y);

                // Horizontal up
                for (float y = centerY - spacingPx; y >= 0; y -= spacingPx)
                    g.DrawLine(pen, 0, y, Width, y);
            }
        }

        // =========================
        // Star drawing
        // =========================
        private static void DrawStarSun(Graphics g, int sx, int sy, string colorName, string government)
        {
            const float PI = 3.1415927f;
            float outerR = 10f;
            float innerR = 5f;
            int rays = 12;

            // Government glow (dim / transparent)
            Color glow = GlowColorFromGovernment(government);
            using (SolidBrush glowBrush = new SolidBrush(Color.FromArgb(60, glow.R, glow.G, glow.B)))
            {
                g.FillEllipse(glowBrush, sx + 10 - 5, sy - 10 - 5, 10, 10);
            }

            Color starColor = ColorFromName(colorName);

            // Rays
            using (Pen pen = new Pen(starColor, 1f))
            {
                for (int i = 0; i < rays; i++)
                {
                    float a = (2f * PI) * (i / (float)rays);
                    float x2 = sx + outerR * (float)Math.Cos(a);
                    float y2 = sy + outerR * (float)Math.Sin(a);
                    g.DrawLine(pen, sx, sy, x2, y2);
                }
            }

            // Core disk
            using (SolidBrush b = new SolidBrush(starColor))
            {
                g.FillEllipse(b, sx - innerR, sy - innerR, innerR * 2, innerR * 2);
            }
        }

        // =========================
        // Color helpers
        // =========================
        private static Color ColorFromName(string name)
        {
            string n = (name ?? "").Trim().ToLowerInvariant();

            switch (n)
            {
                case "red": return Color.Red;
                case "orange": return Color.FromArgb(255, 165, 0);
                case "yellow": return Color.Yellow;
                case "white": return Color.White;

                case "bluewhite":
                case "blue-white":
                case "blue":
                    return Color.FromArgb(180, 180, 255);

                case "brown dwarf":
                case "brown":
                case "magenta":
                    return Color.FromArgb(200, 100, 80);

                default:
                    return Color.White;
            }
        }

        private static Color GlowColorFromGovernment(string gov)
        {
            string g = (gov ?? "").Trim().ToLowerInvariant();

            if (g == "auric concord") return Color.FromArgb(255, 210, 80);
            if (g == "thalean compact") return Color.FromArgb(120, 180, 255);
            if (g == "khar'vess dominion" || g == "kharvess dominion") return Color.FromArgb(190, 80, 220);
            if (g == "lumenary synod") return Color.FromArgb(220, 230, 255);
            if (g == "virel free marches") return Color.FromArgb(120, 255, 160);
            if (g == "sable cartel enclaves" || g == "sable cartel") return Color.FromArgb(255, 140, 180);
            if (g == "caer-astryn remnant" || g == "caer astryn remnant") return Color.FromArgb(200, 200, 255);

            return Color.FromArgb(160, 200, 255);
        }
    }

    // =========================
    // Shared StarInfo model
    // =========================
    public class StarInfo
    {
        public string Name;
        public float XReal, YReal, ZReal;
        public string ColorName;
        public string StarType;
        public string Government;
    }
}
