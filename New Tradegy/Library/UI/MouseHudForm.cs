// MouseHudForm.cs
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace New_Tradegy.Library.UI
{
    /// <summary>
    /// Mouse HUD (표시 전용)
    /// - Base: 1~3줄(지수/점수) + Extra: 0~10줄(가변)
    /// - Overlay: 임시 메시지(가변) / Overlay 중엔 Flash 금지
    /// - Flash: Base 1줄(첫 줄) 글씨색만 검정<->빨강 2번 토글
    /// - 큰 검정판넬 금지: AutoSize + 내용만큼 폼 크기
    /// </summary>
    public sealed class MouseHudForm : Form
    {
        private static MouseHudForm _instance;
        private static readonly object _lock = new object();

        // ===== UI =====
        private readonly FlowLayoutPanel _baseStack;   // Base(3) + Extra(가변)
        private readonly FlowLayoutPanel _line1Row;
        private readonly Label _l1a;
        private readonly Label _l1b;
        private readonly Label _l1c;

        private readonly Label _l2;
        private readonly Label _l3;
        private readonly List<Label> _extraLines = new List<Label>();

        // Overlay (panel+label) : 반투명 느낌용(WinForms Label 알파 한계 회피)
        private readonly AlphaPanel _overlayPanel;
        private readonly Label _overlayLabel;

        // ===== 상태 =====
        private bool _baseVisible = true;

        private string _overlayText = null;
        private DateTime _overlayUntil = DateTime.MinValue;

        private string _extraText = null;
        private DateTime _extraUntil = DateTime.MinValue;

        private bool _isFlashing;

        // ===== Timers =====
        private readonly Timer _timer;           // overlay/extra expiry
        private readonly Timer _followMouseTimer;

        // ===== Style =====
        private readonly Font _font = new Font("맑은 고딕", 20f, FontStyle.Bold);
        private readonly Color _fgNormal = Color.Black;
        private readonly Color _fgFlash = Color.Red;

        // 마우스에서 너무 붙지 않게 오프셋
        private const int OffsetX = 100;
        private const int OffsetY = 80;

        private const int MaxExtraLines = 10;
        //protected override bool ShowWithoutActivation => true;


        // 클래스 필드
        private bool _inRelayout;

        private MouseHudForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            //ShowWithoutActivation = true; 
            ///Enabled = false;

            Opacity = 1.0; // 글자 선명 유지

            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);

            // ===== Base stack =====
            _baseStack = new FlowLayoutPanel
            {
                AutoSize = true,
                WrapContents = false,
                FlowDirection = FlowDirection.TopDown,
                BackColor = Color.White,
                Padding = new Padding(0),
                Margin = new Padding(0),
                Location = new Point(0, 0)
            };




            // 1줄: 가로 3라벨 (구간별 색)
            _line1Row = new FlowLayoutPanel
            {
                AutoSize = true,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = Color.White,
                Margin = new Padding(0),
                Padding = new Padding(0),
            };

            _l1a = MakeLineLabel(); _l1a.Margin = new Padding(0);
            _l1b = MakeLineLabel(); _l1b.Margin = new Padding(0);
            _l1c = MakeLineLabel(); _l1c.Margin = new Padding(0);

            // 간격은 텍스트 앞 공백으로 관리(레이아웃 단순)
            _line1Row.Controls.Add(_l1a);
            _line1Row.Controls.Add(_l1b);
            _line1Row.Controls.Add(_l1c);

            _l2 = MakeLineLabel();
            _l3 = MakeLineLabel();

            // base stack: 1줄(row) + (필요시 2,3줄)
            _baseStack.Controls.Add(_line1Row);
            _baseStack.Controls.Add(_l2);
            _baseStack.Controls.Add(_l3);





            Controls.Add(_baseStack);

            // ===== Overlay =====
            _overlayLabel = new Label
            {
                AutoSize = true,
                Font = _font,
                ForeColor = Color.Black,
                BackColor = Color.Transparent,
                Padding = new Padding(6, 3, 6, 3),
                TextAlign = ContentAlignment.MiddleLeft,
                Text = ""
            };

            _overlayPanel = new AlphaPanel
            {
                Visible = false,
                Padding = new Padding(0),
                Margin = new Padding(0),
                BackFill = Color.FromArgb(180, 240, 240, 240) // “반투명 판넬 느낌”
            };
            _overlayPanel.Controls.Add(_overlayLabel);
            Controls.Add(_overlayPanel);
            _overlayPanel.BringToFront();

            // ===== Timers =====
            _timer = new Timer { Interval = 50 };
            _timer.Tick += (s, e) =>
            {
                CheckOverlayExpiry();
                CheckExtraExpiry();
            };

            _followMouseTimer = new Timer { Interval = 50 };
            _followMouseTimer.Tick += (s, e) => UpdatePositionNearMouse();









            _baseStack.AutoSize = true;
            _baseStack.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            _baseStack.WrapContents = false;
            _baseStack.FlowDirection = FlowDirection.TopDown;

            _overlayPanel.AutoSize = true;
            _overlayPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;

          
            Layout += (s, e) => Relayout();
        }

        private Label MakeLineLabel()
        {
            return new Label
            {
                AutoSize = true,
                Font = _font,
                ForeColor = _fgNormal,
                BackColor = Color.White,      // 밝은 판넬(글자색 변화 잘 보임)
                Padding = new Padding(6, 3, 6, 3),
                TextAlign = ContentAlignment.MiddleLeft,
                Text = ""
            };
        }

        // =========================================================
        // Singleton
        // =========================================================
        private static void EnsureInstance()
        {
            if (_instance != null) return;
            lock (_lock)
            {
                if (_instance == null)
                    _instance = new MouseHudForm();
            }
        }

        // =========================================================
        // Public API (최신 호출부 호환)
        // =========================================================
        public static void ShowSticky()
        {
            EnsureInstance();
            if (_instance.InvokeRequired)
            {
                _instance.BeginInvoke(new Action(ShowSticky));
                return;
            }

            if (!_instance.Visible)
                _instance.Show();

            _instance._timer.Start();
            _instance._followMouseTimer.Start();
            _instance.UpdatePositionNearMouse();
        }

        public static void HideSticky()
        {
            if (_instance == null) return;
            if (_instance.InvokeRequired)
            {
                _instance.BeginInvoke(new Action(HideSticky));
                return;
            }

            _instance._timer.Stop();
            _instance._followMouseTimer.Stop();
            _instance.Hide();
        }

        public static void SetBaseVisible(bool visible)
        {
            EnsureInstance();
            if (_instance.InvokeRequired)
            {
                _instance.BeginInvoke(new Action<bool>(SetBaseVisible), visible);
                return;
            }

            _instance._baseVisible = visible;
            _instance._baseStack.Visible = visible && (_instance._overlayPanel.Visible == false);
            _instance.Relayout();
        }

        /// <summary>
        /// 최신 Tick()에서 쓰는 형태: 3줄 + 3색
        /// </summary>
        /// 

        public static void UpdateBase(string text, Color color)
        {
            EnsureInstance();

            if (_instance.InvokeRequired)
            {
                _instance.BeginInvoke(new Action<string, Color>(UpdateBase), text, color);
                return;
            }

            string s1 = text ?? "";

            _instance._l1a.Text = s1;
            _instance._l1a.ForeColor = color;

            _instance._l1b.Text = "";
            _instance._l1c.Text = "";

            _instance._l1b.Visible = false;
            _instance._l1c.Visible = false;

            _instance._l1a.Visible = true;
            _instance._l1a.AutoSize = true;
            _instance._l1a.Padding = new Padding(0);

            _instance._l2.Text = "";
            _instance._l3.Text = "";
            _instance._l2.Visible = false;
            _instance._l3.Visible = false;

            _instance._baseStack.Visible =
                _instance._baseVisible &&
                (_instance._overlayPanel.Visible == false);

            _instance.Relayout();
        }
        public static void UpdateBase(string g1, string g2, string g3, Color c1, Color c2, Color c3)
        {
            EnsureInstance();
            if (_instance.InvokeRequired)
            {
                _instance.BeginInvoke(new Action<string, string, string, Color, Color, Color>(UpdateBase), g1, g2, g3, c1, c2, c3);
                return;
            }


            // 1줄: g1/g2/g3를 한 줄에 두되, 구간별 색 적용
            string s1 = g1 ?? "";
            string s2 = g2 ?? "";
            string s3 = g3 ?? "";

            _instance._l1a.Text = s1;
            _instance._l1b.Text = string.IsNullOrEmpty(s2) ? "" : s2;
            _instance._l1c.Text = string.IsNullOrEmpty(s3) ? "" : s3;

            _instance._l1a.ForeColor = c1;
            _instance._l1b.ForeColor = c2;
            _instance._l1c.ForeColor = c3;

            _instance._l1a.AutoSize = true;
            _instance._l1b.AutoSize = true;
            _instance._l1c.AutoSize = true;

            _instance._l1a.Padding = new Padding(0);
            _instance._l1b.Padding = new Padding(0);
            _instance._l1c.Padding = new Padding(0);

            // 2~3줄은 기본적으로 안 쓰면 숨김(원하면 나중에 다시 켜면 됨)
            _instance._l2.Text = "";
            _instance._l3.Text = "";
            _instance._l2.Visible = false;
            _instance._l3.Visible = false;


            // Base가 비가시면 숨김
            _instance._baseStack.Visible = _instance._baseVisible && (_instance._overlayPanel.Visible == false);

            _instance.Relayout();
        }

        /// <summary>
        /// Extra: Base 아래에 붙는 가변 라인(최대 10줄)
        /// </summary>
        public static void ShowExtra(string text, int durationMs = 1200)
        {
            EnsureInstance();
            if (_instance.InvokeRequired)
            {
                _instance.BeginInvoke(new Action<string, int>(ShowExtra), text, durationMs);
                return;
            }

            _instance._extraText = NormalizeLines(text, MaxExtraLines);
            _instance._extraUntil = DateTime.UtcNow.AddMilliseconds(Math.Max(200, durationMs));

            _instance.ApplyExtraLines();
            _instance.Relayout();
        }

        public static void ClearExtra()
        {
            EnsureInstance();
            if (_instance.InvokeRequired)
            {
                _instance.BeginInvoke(new Action(ClearExtra));
                return;
            }

            _instance._extraText = null;
            _instance.HideExtraLines();
            _instance.Relayout();
        }

        /// <summary>
        /// Overlay: 임시 메시지(가변). 표시 중엔 Flash 금지.
        /// </summary>
        public static void ShowOverlay(string text, int durationMs)
        {
            EnsureInstance();
            if (_instance.InvokeRequired)
            {
                _instance.BeginInvoke(new Action<string, int>(ShowOverlay), text, durationMs);
                return;
            }

            _instance._overlayText = NormalizeLines(text, 30); // overlay는 넉넉히
            _instance._overlayUntil = DateTime.UtcNow.AddMilliseconds(Math.Max(200, durationMs));

            _instance._overlayLabel.Text = _instance._overlayText ?? "";
            _instance._overlayPanel.Visible = true;
            _instance._overlayPanel.BringToFront();

            // overlay 뜨면 base 숨김
            _instance._baseStack.Visible = false;

            _instance.Relayout();
        }

        /// <summary>
        /// Flash: (요구대로) 첫째줄만 토글. Overlay 중이면 무시.
        /// </summary>
        public static void Flash()
        {
            EnsureInstance();
            if (_instance.InvokeRequired)
            {
                _instance.BeginInvoke(new Action(Flash));
                return;
            }

            if (_instance._overlayPanel.Visible) return;
            if (_instance._isFlashing) return;

            _instance.BeginFlashLine1();
        }

        // =========================================================
        // Internal
        // =========================================================
        private void CheckOverlayExpiry()
        {
            if (_overlayPanel.Visible == false) return;
            if (DateTime.UtcNow < _overlayUntil) return;

            _overlayPanel.Visible = false;
            _overlayText = null;

            // overlay 끝나면 base 복귀
            _baseStack.Visible = _baseVisible;

            Relayout();
        }

        private void CheckExtraExpiry()
        {
            if (_extraText == null) return;
            if (DateTime.UtcNow < _extraUntil) return;

            _extraText = null;
            HideExtraLines();
            Relayout();
        }

        private void ApplyExtraLines()
        {
            HideExtraLines();

            if (string.IsNullOrWhiteSpace(_extraText))
                return;

            var lines = _extraText.Replace("\r\n", "\n").Split('\n').Take(MaxExtraLines).ToArray();
            EnsureExtraLabelPool(lines.Length);

            for (int i = 0; i < lines.Length; i++)
            {
                var lb = _extraLines[i];
                lb.Text = lines[i];
                lb.ForeColor = _fgNormal;   // “나머지 줄은 내 방식대로” -> 기본 검정
                lb.Visible = true;
            }
        }

        private void HideExtraLines()
        {
            foreach (var lb in _extraLines)
                lb.Visible = false;
        }

        private void EnsureExtraLabelPool(int needed)
        {
            while (_extraLines.Count < needed)
            {
                var lb = new Label
                {
                    AutoSize = true,
                    Font = _font,
                    ForeColor = _fgNormal,
                    BackColor = Color.White,
                    Padding = new Padding(6, 1, 6, 3),
                    TextAlign = ContentAlignment.MiddleLeft,
                    Text = "",
                    Visible = false
                };

                _extraLines.Add(lb);
                _baseStack.Controls.Add(lb);
            }
        }

      
        private void Relayout()
        {
            if (_inRelayout) return;
            _inRelayout = true;

            try
            {
                int y = 0;
                int w = 0;

                // 1) base stack
                if (_baseStack != null && _baseStack.Visible)
                {
                    _baseStack.Location = new Point(0, y);
                    _baseStack.PerformLayout();

                    y += _baseStack.Height;
                    w = Math.Max(w, _baseStack.Width);
                }

                // 2) overlay panel
                if (_overlayPanel != null && _overlayPanel.Visible)
                {
                    _overlayPanel.Location = new Point(0, y);
                    _overlayPanel.PerformLayout();

                    y += _overlayPanel.Height;
                    w = Math.Max(w, _overlayPanel.Width);
                }

                if (w <= 0) w = 1;
                if (y <= 0) y = 1;

                ClientSize = new Size(w, y);
                UpdatePositionNearMouse();
            }
            finally
            {
                _inRelayout = false;
            }
        }
        private void BeginFlashLine1()
        {
            _isFlashing = true;
            int step = 0;

            // 원래색 저장(Delta 구간 = _l1a)
            Color orig = _l1a.ForeColor;

            var t = new Timer { Interval = 150 };
            t.Tick += (s, e) =>
            {
                step++;
                bool on = (step % 2 == 1);

                // 첫째줄(Delta 구간)만 토글
                _l1a.ForeColor = on ? _fgFlash : orig;

                if (step >= 4)
                {
                    t.Stop();
                    t.Dispose();

                    _l1a.ForeColor = orig;
                    _isFlashing = false;
                }
            };

            t.Start();
        }

        private static string NormalizeLines(string text, int maxLines)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";

            var lines = text.Replace("\r\n", "\n").Split('\n').Take(maxLines);
            return string.Join("\n", lines);
        }

        private void UpdatePositionNearMouse()
        {
            Point p = Cursor.Position;

            int x = p.X + OffsetX;
            int y = p.Y + OffsetY;

            Rectangle wa = Screen.FromPoint(p).WorkingArea;

            if (x + Width > wa.Right) x = wa.Right - Width - 6;
            if (y + Height > wa.Bottom) y = wa.Bottom - Height - 6;
            if (x < wa.Left) x = wa.Left + 6;
            if (y < wa.Top) y = wa.Top + 6;

            Location = new Point(x, y);
        }

        //protected override void OnFormClosing(FormClosingEventArgs e)
        //{
        //    e.Cancel = true;
        //    Hide();
        //}

        /// <summary>
        /// 알파 배경을 진짜로 칠하기 위한 패널(WinForms Label 알파 한계 회피)
        /// </summary>
        private sealed class AlphaPanel : Panel
        {
            public Color BackFill { get; set; } = Color.FromArgb(180, 240, 240, 240);

            public AlphaPanel()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.UserPaint |
                         ControlStyles.OptimizedDoubleBuffer, true);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                using (var br = new SolidBrush(BackFill))
                {
                    e.Graphics.FillRectangle(br, ClientRectangle);
                }
                base.OnPaint(e);
            }
        }
    }
}