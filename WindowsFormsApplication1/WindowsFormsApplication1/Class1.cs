using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace StarMap2010
{
    public class MainForm : Form
    {
        // =========================
        // Constants (matches your VB6 intent)
        // =========================
        private const int CANVAS_SIZE = 4000;       // like picView
        private const float SCALEa = 20f;           // px per ly
        private const float GRID_SPACING_LY = 10f;  // grid spacing in ly

        private const float ZOOM_MIN = 0.3f;
        private const float ZOOM_MAX = 4.0f;
        private const float ZOOM_STEP = 0.2f;

        private const int RADIUS = 10; // star radius (px) in VB6 you used 10

        // =========================
        // UI
        // =========================
        private MenuStrip menu;
        private ToolStripMenuItem mnuFile, mnuLoad, mnuClear;

        private ComboBox cboStarList;
        private Panel viewport;          // like picFrame
        private MapCanvas canvas;        // like picView (custom control)
        private OpenFileDialog ofd;

        // =========================
        // State
        // =========================
        private readonly List<StarInfo> stars = new List<StarInfo>();
        private float zoomFactor = 1f;

        // Center of canvas where Sol (0,0) projects to
        private float centerX;
        private float centerY;

        private string currentFile = @"F:\Stellar Framwork information\Stars\Auric_Concord_VB6_Mapped.csv";

        public MainForm()
        {
            Text = "Star Map (C# 2010)";
            StartPosition = FormStartPosition.CenterScreen;
            WindowState = FormWindowState.Maximized;

            BuildUi();

            centerX = CANVAS_SIZE / 2f;
            centerY = CANVAS_SIZE / 2f;

            canvas.SetWorld(centerX, centerY, SCALEa, () => zoomFactor);
            canvas.SetData(stars);

            // Default load if present
            if (File.Exists(currentFile))
            {
                LoadStarsFromFile(currentFile, append: false);
            }

            // Center on Sol (0,0) => center of canvas
            CenterViewportOn((int)centerX, (int)centerY);
        }

        private void BuildUi()
        {
            // Menu
            menu = new MenuStrip();
            mnuFile = new ToolStripMenuItem("&File");
            mnuLoad = new ToolStripMenuItem("&Load Star File...");
            mnuClear = new ToolStripMenuItem("&Clear Stars");

            mnuLoad.Click += (s, e) => DoLoadFileAppend();
            mnuClear.Click += (s, e) => ClearAllStars();

            mnuFile.DropDownItems.Add(mnuLoad);
            mnuFile.DropDownItems.Add(new ToolStripSeparator());
            mnuFile.DropDownItems.Add(mnuClear);
            menu.Items.Add(mnuFile);

            Controls.Add(menu);

            // ComboBox (locked near upper-left)
            cboStarList = new ComboBox();
            cboStarList.DropDownStyle = ComboBoxStyle.DropDownList;
            cboStarList.Width = 360;
            cboStarList.Left = 8;
            cboStarList.Top = menu.Height + 6;
            cboStarList.Anchor = AnchorStyles.Left | AnchorStyles.Top;
            cboStarList.SelectedIndexChanged += (s, e) => CenterOnSelectedStar();

            Controls.Add(cboStarList);

            // Viewport panel (scrollbars)
            viewport = new Panel();
            viewport.Left = 0;
            viewport.Top = cboStarList.Bottom + 6;
            viewport.Width = ClientSize.Width;
            viewport.Height = ClientSize.Height - viewport.Top;
            viewport.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            viewport.AutoScroll = true;
            viewport.BackColor = Color.Black;

            Controls.Add(viewport);

            // Canvas (large drawing area)
            canvas = new MapCanvas();
            canvas.Size = new Size(CANVAS_SIZE, CANVAS_SIZE);
            canvas.Location = new Point(0, 0);
            canvas.BackColor = Color.Black;
            canvas.MouseDown += Canvas_MouseDown;
            canvas.MouseWheel += Canvas_MouseWheel;
            canvas.TabStop = true; // so it can receive wheel after click

            // smoother rendering
            canvas.DoubleBuffered = true;

            viewport.Controls.Add(canvas);

            // File dialog
            ofd = new OpenFileDialog();
            ofd.Title = "Select Star Data File";
            ofd.Filter = "Star Map Files (*.txt;*.tsv;*.csv)|*.txt;*.tsv;*.csv|All Files (*.*)|*.*";

            // Resize handler
            Resize += (s, e) =>
            {
                viewport.Top = cboStarList.Bottom + 6;
                viewport.Height = ClientSize.Height - viewport.Top;
            };
        }

        // =========================
        // File loading (append or replace)
        // =========================
        private void DoLoadFileAppend()
        {
            try
            {
                if (!string.IsNullOrEmpty(currentFile))
                    ofd.FileName = currentFile;

                if (ofd.ShowDialog(this) != DialogResult.OK)
                    return;

                currentFile = ofd.FileName;
                LoadStarsFromFile(currentFile, append: true);
            }
            catch
            {
                // user canceled, ignore
            }
        }

        private void LoadStarsFromFile(string filename, bool append)
        {
            if (!File.Exists(filename))
            {
                MessageBox.Show("Unable to open file:\r\n" + filename, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (!append)
            {
                stars.Clear();
                cboStarList.Items.Clear();
            }

            int added = 0;
            foreach (var line in File.ReadLines(filename))
            {
                StarInfo s;
                if (TryParseStarLine(line, out s))
                {
                    stars.Add(s);
                    added++;
                }
            }

            // rebuild combobox (sorted)
            RebuildStarCombo();

            // redraw
            canvas.Invalidate();

            // keep view centered on Sol (canvas center)
            if (!append)
                CenterViewportOn((int)centerX, (int)centerY);
        }

        private void RebuildStarCombo()
        {
            // Keep selection if possible
            string selected = cboStarList.SelectedItem as string;

            cboStarList.BeginUpdate();
            cboStarList.Items.Clear();

            foreach (var name in stars
                .Select(st => st.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n))
            {
                cboStarList.Items.Add(name);
            }

            cboStarList.EndUpdate();

            if (!string.IsNullOrEmpty(selected))
            {
                int idx = cboStarList.FindStringExact(selected);
                if (idx >= 0) cboStarList.SelectedIndex = idx;
            }
        }

        private void ClearAllStars()
        {
            stars.Clear();
            cboStarList.Items.Clear();
            canvas.Invalidate();
        }

        // =========================
        // Parsing (TAB preferred, else COMMA)
        // Layout:
        // FictionalName RealStarName ScreenX ScreenY Color RealX_ly RealY_ly RealZ_ly Dist_ly RA_deg Dec_deg StarType Government
        // =========================
        private static bool TryParseStarLine(string raw, out StarInfo star)
        {
            star = null;
            if (raw == null) return false;

            string line = raw.Trim();
            if (line.Length == 0) return false;

            char sep = '\0';
            if (line.IndexOf('\t') >= 0) sep = '\t';
            else if (line.IndexOf(',') >= 0) sep = ',';
            else return false;

            string[] parts = line.Split(sep);

            // skip header
            if (parts.Length > 0 && parts[0].Trim().Equals("FictionalName", StringComparison.OrdinalIgnoreCase))
                return false;

            if (parts.Length < 13) return false;

            try
            {
                // Using invariant culture for decimals
                var ci = CultureInfo.InvariantCulture;

                string fictional = parts[0].Trim();
                string color = parts[4].Trim().ToLowerInvariant();

                float x = float.Parse(parts[5].Trim(), ci);
                float y = float.Parse(parts[6].Trim(), ci);
                float z = float.Parse(parts[7].Trim(), ci);

                string starType = parts[11].Trim();
                string gov = parts[12].Trim();

                star = new StarInfo
                {
                    Name = fictional,
                    ColorName = color,
                    XReal = x,
                    YReal = y,
                    ZReal = z,
                    StarType = starType,
                    Government = gov
                };

                return true;
            }
            catch
            {
                return false;
            }
        }

        // =========================
        // Combo → center on star
        // =========================
        private void CenterOnSelectedStar()
        {
            if (cboStarList.SelectedIndex < 0) return;
            string name = cboStarList.SelectedItem as string;
            if (string.IsNullOrEmpty(name)) return;

            // find first matching
            var st = stars.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (st == null) return;

            // project to screen coords (on canvas)
            Point p = canvas.Project(st);
            CenterViewportOn(p.X, p.Y);
        }

        // =========================
        // Mouse
        // =========================
        private void Canvas_MouseDown(object sender, MouseEventArgs e)
        {
            canvas.Focus(); // so wheel works

            // if clicked a star, show info; else center
            var hit = canvas.HitTest(e.Location, RADIUS);
            if (hit != null)
            {
                MessageBox.Show(
                    "Name: " + hit.Name + "\r\n" +
                    "Type: " + hit.StarType + "\r\n" +
                    "Government: " + hit.Government + "\r\n\r\n" +
                    "Real Coordinates (ly):\r\n" +
                    "   X = " + hit.XReal + "\r\n" +
                    "   Y = " + hit.YReal + "\r\n" +
                    "   Z = " + hit.ZReal,
                    "Star Selected",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            else
            {
                CenterViewportOn(e.X, e.Y);
            }
        }

        private void Canvas_MouseWheel(object sender, MouseEventArgs e)
        {
            float old = zoomFactor;

            if (e.Delta > 0) zoomFactor += ZOOM_STEP;
            else if (e.Delta < 0) zoomFactor -= ZOOM_STEP;

            if (zoomFactor < ZOOM_MIN) zoomFactor = ZOOM_MIN;
            if (zoomFactor > ZOOM_MAX) zoomFactor = ZOOM_MAX;

            if (Math.Abs(zoomFactor - old) > 0.0001f)
            {
                canvas.Invalidate();
            }
        }

        // =========================
        // Center viewport on a point in canvas coords
        // =========================
        private void CenterViewportOn(int cx, int cy)
        {
            // AutoScrollPosition uses *negative* coords internally; setting it uses positive.
            int targetX = Math.Max(0, cx - viewport.ClientSize.Width / 2);
            int targetY = Math.Max(0, cy - viewport.ClientSize.Height / 2);

            viewport.AutoScrollPosition = new Point(targetX, targetY);
        }

        // =========================
        // Star record
        // =========================
        private class StarInfo
        {
            public string Name;
            public float XReal, YReal, ZReal;
            public string ColorName;
            public string StarType;
            public string Government;
        }

        // =========================
        // Custom canvas control
        // =========================
        private class MapCanvas : Control
        {
            private List<StarInfo> data;
            private float centerX, centerY;
            private float baseScale;
            private Func<float> getZoom;

            public void SetData(List<StarInfo> stars)
            {
                data = stars;
            }

            public void SetWorld(float cx, float cy, float scale, Func<float> zoomGetter)
            {
                centerX = cx;
                centerY = cy;
                baseScale = scale;
                getZoom = zoomGetter;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);

                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                // background
                g.Clear(Color.Black);

                // grid
                DrawGrid(g);

                // stars
                if (data == null) return;

                foreach (var s in data)
                {
                    Point p = Project(s);
                    DrawStarSun(g, p.X, p.Y, s.ColorName, s.Government);
                }
            }

            public Point Project(StarInfo s)
            {
                float scaleNow = baseScale * (getZoom != null ? getZoom() : 1f);

                // match VB6 mapping:
                // sx = CenterX + XReal*scale
                // sy = CenterY - YReal*scale
                int sx = (int)Math.Round(centerX + s.XReal * scaleNow);
                int sy = (int)Math.Round(centerY - s.YReal * scaleNow);
                return new Point(sx, sy);
            }

            public StarInfo HitTest(Point canvasPoint, int radiusPx)
            {
                if (data == null) return null;
                int r2 = radiusPx * radiusPx;

                // simple linear scan (fine for a few thousand)
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

            private void DrawGrid(Graphics g)
            {
                float zoom = getZoom != null ? getZoom() : 1f;
                float spacingPx = GRID_SPACING_LY * baseScale * zoom;

                if (spacingPx < 15f) return; // avoid mush

                using (var pen = new Pen(Color.FromArgb(40, 40, 40), 1f))
                {
                    // vertical lines right
                    for (float x = centerX; x <= Width; x += spacingPx)
                        g.DrawLine(pen, x, 0, x, Height);

                    // vertical lines left
                    for (float x = centerX - spacingPx; x >= 0; x -= spacingPx)
                        g.DrawLine(pen, x, 0, x, Height);

                    // horizontal lines down
                    for (float y = centerY; y <= Height; y += spacingPx)
                        g.DrawLine(pen, 0, y, Width, y);

                    // horizontal lines up
                    for (float y = centerY - spacingPx; y >= 0; y -= spacingPx)
                        g.DrawLine(pen, 0, y, Width, y);
                }
            }

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
                    case "blue": return Color.FromArgb(180, 180, 255);
                    case "brown dwarf":
                    case "brown":
                    case "magenta": return Color.FromArgb(200, 100, 80);
                    default: return Color.White;
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

            private static void DrawStarSun(Graphics g, int sx, int sy, string colorName, string gov)
            {
                const float PI = 3.1415927f;

                // sizes similar to your VB6 intent
                float outerR = RADIUS;        // ray length
                float innerR = RADIUS * 0.5f; // center disk radius
                int steps = 12;

                // government glow (dim / semi-transparent)
                Color glow = GlowColorFromGovernment(gov);
                using (var glowBrush = new SolidBrush(Color.FromArgb(60, glow.R, glow.G, glow.B)))
                {
                    // your VB6 glow was offset +10,-10 and small; keep it
                    g.FillEllipse(glowBrush, sx + 10 - (RADIUS / 2), sy - 10 - (RADIUS / 2), RADIUS, RADIUS);
                }

                Color starColor = ColorFromName(colorName);

                // rays
                using (var pen = new Pen(starColor, 1f))
                {
                    for (int i = 0; i < steps; i++)
                    {
                        float a = (2f * PI) * (i / (float)steps);
                        float x2 = sx + outerR * (float)Math.Cos(a);
                        float y2 = sy + outerR * (float)Math.Sin(a);
                        g.DrawLine(pen, sx, sy, x2, y2);
                    }
                }

                // center disk (filled)
                using (var b = new SolidBrush(starColor))
                {
                    g.FillEllipse(b, sx - innerR, sy - innerR, innerR * 2, innerR * 2);
                }
            }
        }
    }
}
