using System;
using System.Drawing;
using System.Windows.Forms;
using StarMap2010.Models;

namespace StarMap2010
{
    //MainForm.Input
    public partial class MainForm
    {

                
        // ============================================================
        // Star context actions (Measure / Swap / Route)
        // ============================================================

        private string _swapAId;
        private string _routeAId;

        private ContextMenuStrip _starCtx;
        private StarSystemInfo _ctxHitSystem;

// Measure mode (distance between two star systems)
        private bool _measureMode;
        private StarSystemInfo _measureA;
        private string _measureAId;

private void CenterViewportOn(int cx, int cy)
        {
            int targetX = Math.Max(0, cx - viewport.ClientSize.Width / 2);
            int targetY = Math.Max(0, cy - viewport.ClientSize.Height / 2);

            viewport.AutoScrollPosition = new Point(targetX, targetY);
        }

        private void SyncDesiredScrollFromBars()
        {
            desiredScrollX = viewport.HorizontalScroll.Value;
            desiredScrollY = viewport.VerticalScroll.Value;
            desiredScrollInit = true;
        }

        private void Any_MouseWheelZoom(object sender, MouseEventArgs e)
        {
            HandledMouseEventArgs he = e as HandledMouseEventArgs;
            if (he != null) he.Handled = true;

            if (canvas == null || viewport == null) return;

            Point mouseCanvasPt;
            Control src = sender as Control;
            if (src != null)
            {
                Point screenPt = src.PointToScreen(e.Location);
                mouseCanvasPt = canvas.PointToClient(screenPt);
            }
            else
            {
                mouseCanvasPt = e.Location;
            }

            float oldZoom = zoomFactor;

            PointF worldUnderMouse = canvas.ScreenToWorld(mouseCanvasPt);

            if (e.Delta > 0) zoomFactor += ZOOM_STEP;
            else if (e.Delta < 0) zoomFactor -= ZOOM_STEP;

            if (zoomFactor < ZOOM_MIN) zoomFactor = ZOOM_MIN;
            if (zoomFactor > ZOOM_MAX) zoomFactor = ZOOM_MAX;

            if (Math.Abs(zoomFactor - oldZoom) < 0.0001f)
                return;

            if (!desiredScrollInit)
                SyncDesiredScrollFromBars();

            SetRedraw(viewport, false);

            try
            {
                canvas.ZoomFactor = zoomFactor;

                PointF newMouseCanvasPtF = canvas.WorldToScreen(worldUnderMouse);

                float dxF = newMouseCanvasPtF.X - mouseCanvasPt.X;
                float dyF = newMouseCanvasPtF.Y - mouseCanvasPt.Y;

                desiredScrollX += dxF;
                desiredScrollY += dyF;

                float maxXf = Math.Max(0, canvas.Width - viewport.ClientSize.Width);
                float maxYf = Math.Max(0, canvas.Height - viewport.ClientSize.Height);

                if (desiredScrollX < 0f) desiredScrollX = 0f;
                if (desiredScrollY < 0f) desiredScrollY = 0f;
                if (desiredScrollX > maxXf) desiredScrollX = maxXf;
                if (desiredScrollY > maxYf) desiredScrollY = maxYf;

                int targetX = (int)Math.Round(desiredScrollX);
                int targetY = (int)Math.Round(desiredScrollY);

                int maxX = (int)maxXf;
                int maxY = (int)maxYf;

                if (targetX < 0) targetX = 0;
                if (targetY < 0) targetY = 0;
                if (targetX > maxX) targetX = maxX;
                if (targetY > maxY) targetY = maxY;

                viewport.HorizontalScroll.Value = targetX;
                viewport.VerticalScroll.Value = targetY;
            }
            finally
            {
                SetRedraw(viewport, true);
                viewport.Invalidate(true);
                viewport.Update();
            }
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!isPanning) return;

            int t = Environment.TickCount;
            if (t - lastPanTick < 10) return;
            lastPanTick = t;

            Point now = viewport.PointToClient(canvas.PointToScreen(e.Location));
            int dx = now.X - panMouseDown.X;
            int dy = now.Y - panMouseDown.Y;

            int targetX = panScrollDown.X - dx;
            int targetY = panScrollDown.Y - dy;

            int maxX = Math.Max(0, canvas.Width - viewport.ClientSize.Width);
            int maxY = Math.Max(0, canvas.Height - viewport.ClientSize.Height);

            if (targetX < 0) targetX = 0;
            if (targetY < 0) targetY = 0;
            if (targetX > maxX) targetX = maxX;
            if (targetY > maxY) targetY = maxY;

            viewport.HorizontalScroll.Value = targetX;
            viewport.VerticalScroll.Value = targetY;

            desiredScrollX = targetX;
            desiredScrollY = targetY;
            desiredScrollInit = true;
        }

        private void Canvas_MouseDown(object sender, MouseEventArgs e)
        {
            mouseDownCanvas = e.Location;
            mouseDownWasSpace = spaceDown;

            if (e.Button == MouseButtons.Left && spaceDown)
            {
                isPanning = true;

                panMouseDown = viewport.PointToClient(canvas.PointToScreen(e.Location));
                panScrollDown = new Point(viewport.HorizontalScroll.Value, viewport.VerticalScroll.Value);

                canvas.Capture = true;
            }
        }

        private void Canvas_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                ShowStarContextMenu(e.Location);
                return;
            }

            if (e.Button != MouseButtons.Left) return;

            if (isPanning)
            {
                isPanning = false;
                canvas.Capture = false;
                ClampScrollToWorld();
                return;
            }

            if (mouseDownWasSpace) return;

            int dx = e.Location.X - mouseDownCanvas.X;
            int dy = e.Location.Y - mouseDownCanvas.Y;
            if ((dx * dx + dy * dy) > (CLICK_SLOP_PX * CLICK_SLOP_PX))
                return;

            StarSystemInfo hit = canvas.HitTest(e.Location);
            if (hit == null) return;


            // Context modes (right-click -> choose action, then left-click a target)
            if (_measureMode)
            {
                HandleMeasureClick(hit);
                return;
            }
            if (!string.IsNullOrEmpty(_swapAId))
            {
                HandleSwapClick(hit);
                return;
            }
            if (!string.IsNullOrEmpty(_routeAId))
            {
                HandleRouteClick(hit);
                return;
            }

            bool shift = (Control.ModifierKeys & Keys.Shift) == Keys.Shift;
            if (!shift)
            {
                selectedA = hit;
                selectedB = null;
                selectedIdA = selectedA != null ? selectedA.SystemId : null;
                selectedIdB = null;
            canvas.SetSelected(selectedA);
            return;
            }

            if (string.IsNullOrEmpty(selectedIdA))
            {
                selectedA = hit;
                selectedB = null;

                selectedIdA = selectedA.SystemId;
                selectedIdB = null;
            canvas.SetSelected(selectedA);
            return;
            }

            if (!string.IsNullOrEmpty(selectedIdA) && hit.SystemId == selectedIdA)
                return;

            selectedB = hit;
            selectedIdB = selectedB.SystemId;
            canvas.SetSelected(selectedB);
            SetSelectedSystem(selectedB);
            using (var dlg = new CompareSwapForm(_dbPath, selectedIdA, selectedIdB))
            {
                var result = dlg.ShowDialog(this);
                if (result == DialogResult.OK)
                {
                    ReloadAndRefresh();
                    ResetCompareSelection();
                }
                else
                {
                    selectedB = null;
                    selectedIdB = null;

                    selectedA = FindSystemById(selectedIdA);
            canvas.SetSelected(selectedA);
            }
            }
        }

        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space)
            {
                spaceDown = true;
                canvas.Cursor = Cursors.Hand;
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.Escape)
            {
                ResetCompareSelection();
                e.Handled = true;
                return;
            }
        }

        private void MainForm_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space)
            {
                spaceDown = false;

                isPanning = false;
                canvas.Capture = false;

                canvas.Cursor = Cursors.Default;
                e.Handled = true;
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Space && (spaceDown || isPanning))
                return true;
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void ClampScrollToWorld()
        {
            int sx = viewport.HorizontalScroll.Value;
            int sy = viewport.VerticalScroll.Value;

            int maxX = Math.Max(0, canvas.Width - viewport.ClientSize.Width);
            int maxY = Math.Max(0, canvas.Height - viewport.ClientSize.Height);

            if (sx < 0) sx = 0;
            if (sy < 0) sy = 0;
            if (sx > maxX) sx = maxX;
            if (sy > maxY) sy = maxY;

            viewport.HorizontalScroll.Value = sx;
            viewport.VerticalScroll.Value = sy;

            desiredScrollX = sx;
            desiredScrollY = sy;
            desiredScrollInit = true;
        }


        private void SetMeasureMode(bool enabled)
        {
            _measureMode = enabled;
            _measureA = null;
            _measureAId = null;

            // Don't accidentally keep swap selections around in measure mode
            selectedA = null; selectedB = null;
            selectedIdA = null; selectedIdB = null;

            if (_lblMeasure != null)
                _lblMeasure.Text = enabled ? "Click a system…" : "";
        }



        private void SetStatus(string text)
        {
            if (_lblMeasure == null) return;
            _lblMeasure.Text = text ?? "";
        }

        private void ClearStatus()
        {
            SetStatus("");
        }

        private static double ComputeDistanceLy(StarSystemInfo a, StarSystemInfo b)
        {
            if (a == null || b == null) return 0.0;

            double dx = a.XReal - b.XReal;
            double dy = a.YReal - b.YReal;
            double dz = a.ZReal - b.ZReal;

            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        private void EnsureStarContextMenu()
        {
            if (_starCtx != null) return;

            _starCtx = new ContextMenuStrip();

            var miMeasure = new ToolStripMenuItem("Measure from here", null, (s, e) => BeginMeasureFromContext());
            var miSwap = new ToolStripMenuItem("Swap from here", null, (s, e) => BeginSwapFromContext());
            var miRoute = new ToolStripMenuItem("Route from here", null, (s, e) => BeginRouteFromContext());
            var miCancel = new ToolStripMenuItem("Cancel mode", null, (s, e) => CancelContextModes());

            _starCtx.Items.Add(miMeasure);
            _starCtx.Items.Add(miSwap);
            _starCtx.Items.Add(miRoute);
            _starCtx.Items.Add(new ToolStripSeparator());
            _starCtx.Items.Add(miCancel);

            _starCtx.Opening += (s, e) =>
            {
                bool hasHit = (_ctxHitSystem != null);
                miMeasure.Enabled = hasHit;
                miSwap.Enabled = hasHit;
                miRoute.Enabled = hasHit;
                miCancel.Enabled = true;
            };
        }

        private void ShowStarContextMenu(Point clientPoint)
        {
            EnsureStarContextMenu();

            if (canvas == null) return;

            StarSystemInfo hit = canvas.HitTest(clientPoint);
            if (hit == null) return;

            _ctxHitSystem = hit;

            // Ensure selection + details panel update on right-click
            try
            {
                canvas.SetSelected(hit);
                SetSelectedSystem(hit);
            }
            catch { }

            _starCtx.Show(canvas, clientPoint);
        }

        private void BeginMeasureFromContext()
        {
            if (_ctxHitSystem == null) return;

            _measureMode = true;
            _measureA = _ctxHitSystem;
            _measureAId = _ctxHitSystem.SystemId;

            _swapAId = null;
            _routeAId = null;

            SetStatus("Measure: From " + (_ctxHitSystem.SystemName ?? "(unnamed)") + "\r\nLeft-click another system…");
        }

        private void BeginSwapFromContext()
        {
            if (_ctxHitSystem == null) return;

            _measureMode = false;
            _measureA = null;
            _measureAId = null;

            _swapAId = _ctxHitSystem.SystemId;
            _routeAId = null;

            SetStatus("Swap: Start " + (_ctxHitSystem.SystemName ?? "(unnamed)") + "\r\nLeft-click a second system…");
        }

        private void BeginRouteFromContext()
        {
            if (_ctxHitSystem == null) return;

            _measureMode = false;
            _measureA = null;
            _measureAId = null;

            _swapAId = null;
            _routeAId = _ctxHitSystem.SystemId;

            SetStatus("Route: Start " + (_ctxHitSystem.SystemName ?? "(unnamed)") + "\r\nLeft-click a destination…");
        }

        private void CancelContextModes()
        {
            _measureMode = false;
            _measureA = null;
            _measureAId = null;
            _swapAId = null;
            _routeAId = null;
            ClearStatus();
        }

        private void HandleMeasureClick(StarSystemInfo hit)
        {
            if (hit == null) return;

            canvas.SetSelected(hit);
            SetSelectedSystem(hit);

            if (_measureA == null || string.IsNullOrEmpty(_measureAId))
            {
                _measureA = hit;
                _measureAId = hit.SystemId;
                SetStatus("Measure: From " + (hit.SystemName ?? "(unnamed)") + "\r\nLeft-click another system…");
                return;
            }

            if (string.Equals(_measureAId, hit.SystemId, StringComparison.OrdinalIgnoreCase))
            {
                _measureA = hit;
                _measureAId = hit.SystemId;
                SetStatus("Measure: From " + (hit.SystemName ?? "(unnamed)") + "\r\nLeft-click another system…");
                return;
            }

            double d = ComputeDistanceLy(_measureA, hit);
            SetStatus(string.Format("Measure:\r\n{0} → {1}: {2:0.00} ly",
                (_measureA.SystemName ?? "(unnamed)"),
                (hit.SystemName ?? "(unnamed)"),
                d));

            // Chain: keep this as the new start
            _measureA = hit;
            _measureAId = hit.SystemId;
        }

        private void HandleSwapClick(StarSystemInfo hit)
        {
            if (hit == null) return;
            if (string.IsNullOrEmpty(_swapAId)) return;

            if (string.Equals(_swapAId, hit.SystemId, StringComparison.OrdinalIgnoreCase))
                return;

            // Ensure selection follows click
            canvas.SetSelected(hit);
            SetSelectedSystem(hit);

            using (var dlg = new CompareSwapForm(_dbPath, _swapAId, hit.SystemId))
            {
                var result = dlg.ShowDialog(this);
                if (result == DialogResult.OK)
                {
                    ReloadAndRefresh();
                }
            }

            _swapAId = null;
            ClearStatus();
        }

        private void HandleRouteClick(StarSystemInfo hit)
        {
            if (hit == null) return;
            if (string.IsNullOrEmpty(_routeAId)) return;

            var start = FindSystemById(_routeAId);
            if (start == null)
            {
                SetStatus("Route: start system not found.");
                _routeAId = null;
                return;
            }

            // TEMP (until RoutePlanner is integrated): show direct distance.
            double d = ComputeDistanceLy(start, hit);
            SetStatus(string.Format("Route (direct):\r\n{0} → {1}: {2:0.00} ly",
                (start.SystemName ?? "(unnamed)"),
                (hit.SystemName ?? "(unnamed)"),
                d));

            _routeAId = null;
        }

    }
}
