using System;
using System.Drawing;
using System.Windows.Forms;
using StarMap2010.Models;

namespace StarMap2010
{
    public partial class MainForm
    {

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

            bool shift = (Control.ModifierKeys & Keys.Shift) == Keys.Shift;

            if (!shift)
            {
                selectedA = hit;
                selectedB = null;
                selectedIdA = selectedA != null ? selectedA.SystemId : null;
                selectedIdB = null;

                SetSelectedSystem(selectedA);
                canvas.SetSelected(selectedA);
                return;
            }

            if (string.IsNullOrEmpty(selectedIdA))
            {
                selectedA = hit;
                selectedB = null;

                selectedIdA = selectedA.SystemId;
                selectedIdB = null;

                SetSelectedSystem(selectedA);
                canvas.SetSelected(selectedA);
                return;
            }

            if (!string.IsNullOrEmpty(selectedIdA) && hit.SystemId == selectedIdA)
                return;

            selectedB = hit;
            selectedIdB = selectedB.SystemId;

            SetSelectedSystem(selectedB);
            canvas.SetSelected(selectedB);

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
                    SetSelectedSystem(selectedA);
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

    }
}
