// HudBaseForm.cs
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace New_Tradegy.Library.UI
{
    public partial class HudBaseForm : Form
    {
        private readonly Timer _hideTimer;

        private string _text = "";
        private Font _font = SystemFonts.DefaultFont;
        private Color _foreColor = Color.White;

        private readonly Color _transparentKey = Color.Red;

        private int _bgAlpha = 120; // 20260515
        private Color _bgColor = Color.Black;
        private int _padding = 12;
        private int _cornerRadius = 14;

        private Size _maxSize = new Size(900, 600);

        // --- micro-optimization cache ---
        private string _lastText = null;
        private Font _lastFont = null;
        private int _lastPadding = -1;
        private Size _lastMeasuredSize = Size.Empty;

        // ─────────────────────────────────────────────
        // WinAPI: NO-ACTIVATE show (핵심)
        // ─────────────────────────────────────────────
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy,
            uint uFlags);

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint SWP_NOZORDER = 0x0004;
        private bool _clickThrough = true;
        public void SetClickThrough(bool on)
        {
            _clickThrough = on;
            if (IsHandleCreated)
            {
                // Recreate handle so CreateParams re-applies exstyles
                var wasVisible = Visible;
                RecreateHandle();
                if (wasVisible) Show();
            }
        }
        protected HudBaseForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;

            BackColor = _transparentKey;
            TransparencyKey = _transparentKey;

            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            UpdateStyles();

            _hideTimer = new Timer();
            _hideTimer.Tick += (s, e) =>
            {
                _hideTimer.Stop();
                // 타이머 Tick에서도 "안전하게 UI 스레드"에서 Hide
                if (!IsDisposed)
                {
                    if (InvokeRequired) BeginInvoke(new Action(Hide));
                    else Hide();
                }
            };

            Opacity = 1.0;
            Hide();
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
                if (_clickThrough)
                    cp.ExStyle |= 0x00000020; // WS_EX_TRANSPARENT (click-through)
                return cp;
            }
        }

        protected void ShowHudFixedAt(Point screenLocation, Size fixedSize, int durationMs)
        {
            if (IsDisposed) return;

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => ShowHudFixedAt(screenLocation, fixedSize, durationMs)));
                return;
            }

            if (Size != fixedSize) Size = fixedSize;
            if (Location != screenLocation) Location = screenLocation;

            _hideTimer.Stop();
            Invalidate();

            if (!IsHandleCreated) CreateControl();

            uint flags = SWP_NOACTIVATE | SWP_SHOWWINDOW;
            SetWindowPos(Handle, HWND_TOPMOST, screenLocation.X, screenLocation.Y, fixedSize.Width, fixedSize.Height, flags);

            durationMs = Math.Max(1, durationMs);
            _hideTimer.Interval = durationMs;
            _hideTimer.Start();
        }

        protected void ShowHudAt(
            Point screenLocation,
            string text,
            int durationMs,
            Font font,
            Color? foreColor,
            int bgAlpha,
            Color? bgColor,
            int padding,
            int cornerRadius)
        {
            if (IsDisposed) return;

            // ✅ 여기선 BeginInvoke는 OK. (단, 메시지 큐 누적 방지 위해 Show/BringToFront 제거가 핵심)
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() =>
                    ShowHudAt(screenLocation, text, durationMs, font, foreColor, bgAlpha, bgColor, padding, cornerRadius)));
                return;
            }

            _text = text ?? "";
            _font = font ?? SystemFonts.DefaultFont;
            _foreColor = foreColor ?? Color.White;
            _bgAlpha = Math.Max(0, Math.Min(255, bgAlpha));
            _bgColor = bgColor ?? Color.Black;
            _padding = Math.Max(0, padding);
            _cornerRadius = Math.Max(0, cornerRadius);

            // --- measure size only when needed ---
            bool needMeasure = !string.Equals(_lastText, _text, StringComparison.Ordinal)
                               || !ReferenceEquals(_lastFont, _font)
                               || _lastPadding != _padding
                               || _lastMeasuredSize.IsEmpty;

            Size targetSize;
            if (needMeasure)
            {
                targetSize = MeasureHudSize(_text, _font, _padding, _maxSize);
                _lastText = _text;
                _lastFont = _font;
                _lastPadding = _padding;
                _lastMeasuredSize = targetSize;
            }
            else
            {
                targetSize = _lastMeasuredSize;
            }

            // ✅ Size/Location 먼저 확정
            if (Size != targetSize) Size = targetSize;
            if (Location != screenLocation) Location = screenLocation;

            // ✅ 타이머는 "항상 리셋"
            _hideTimer.Stop();

            // ✅ 동기 Update() 금지 (너가 이미 제거한 판단이 맞음)
            Invalidate();

            // ✅ Handle 보장
            if (!IsHandleCreated) CreateControl();

            // ✅ 핵심: Show()/BringToFront() 대신 NOACTIVATE로 띄우기
            // - 이미 Visible이어도 SetWindowPos로 "표시+TopMost 유지"만 한다.
            // - 포커스/키 입력 라우팅 건드리지 않음.
            uint flags = SWP_NOACTIVATE | SWP_SHOWWINDOW;
            SetWindowPos(
                Handle,
                HWND_TOPMOST,
                screenLocation.X,
                screenLocation.Y,
                targetSize.Width,
                targetSize.Height,
                flags);

            // ✅ duration
            durationMs = Math.Max(1, durationMs);
            _hideTimer.Interval = durationMs;
            _hideTimer.Start();





            // 20260515

            this.TopMost = true;
            this.Show();
            this.BringToFront();
            this.Refresh();
        }

        protected Size MeasureHudSize(string text, Font font, int padding, Size maxSize)
        {
            var flags = TextFormatFlags.NoPadding | TextFormatFlags.LeftAndRightPadding;

            var proposed = new Size(maxSize.Width - padding * 2, maxSize.Height - padding * 2);
            var textSize = TextRenderer.MeasureText(text ?? "", font, proposed,
                flags | TextFormatFlags.WordBreak);

            int w = Math.Min(maxSize.Width, textSize.Width + padding * 2);
            int h = Math.Min(maxSize.Height, textSize.Height + padding * 2);

            w = Math.Max(40, w);
            h = Math.Max(24, h);

            return new Size(w, h);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (string.IsNullOrEmpty(_text))
                return;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new Rectangle(0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
            int r = Math.Min(_cornerRadius, Math.Min(rect.Width, rect.Height) / 2);

            using (var path = RoundedRect(rect, r))
            using (var brush = new SolidBrush(Color.FromArgb(_bgAlpha, _bgColor)))
            {
                e.Graphics.FillPath(brush, path);

                int penAlpha = Math.Min(255, (int)(_bgAlpha * 0.6));
                if (penAlpha >= 10)
                {
                    using (var pen = new Pen(Color.FromArgb(penAlpha, _bgColor)))
                        e.Graphics.DrawPath(pen, path);
                }
            }

            var textRect = new Rectangle(_padding, _padding,
                ClientSize.Width - _padding * 2,
                ClientSize.Height - _padding * 2);

            TextRenderer.DrawText(
                e.Graphics,
                _text,
                _font,
                textRect,
                _foreColor,
                TextFormatFlags.WordBreak |
                TextFormatFlags.Left |
                TextFormatFlags.Top |
                TextFormatFlags.NoPadding);
        }

        private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            if (radius <= 0)
            {
                path.AddRectangle(bounds);
                path.CloseFigure();
                return path;
            }

            int d = radius * 2;
            var arc = new Rectangle(bounds.Location, new Size(d, d));

            path.AddArc(arc, 180, 90);
            arc.X = bounds.Right - d;
            path.AddArc(arc, 270, 90);
            arc.Y = bounds.Bottom - d;
            path.AddArc(arc, 0, 90);
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);

            path.CloseFigure();
            return path;
        }
    }
}
