// QuickTradePopup.cs (FINAL)
using New_Tradegy.Library.Utils;
using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;







namespace New_Tradegy.Library.Deals
{
    public sealed class QuickTradePopup : Form
    {
        // ===== 결과 3가지(승인/완전기각/관찰유지) =====
        public enum PopupResultKind
        {
            Confirm,        // 매수 실행 (price, qty)
            CancelRemove,   // 완전 기각: interested 제거 + chart update
            CancelKeep      // 관찰: interested 유지 + chart update 생략
        }

        public readonly struct PopupResult
        {
            public readonly PopupResultKind Kind;
            public readonly int Price;
            public readonly int Qty;

            public PopupResult(PopupResultKind kind, int price = 0, int qty = 0)
            {
                Kind = kind;
                Price = price;
                Qty = qty;
            }
        }

        private int _price;
        private int _qty;
        private readonly int _timeoutMs;
        private readonly Color _sideColor;
        private readonly string _symbol;

        private readonly int _offsetX;
        private readonly int _offsetY;

        //private readonly string _noteTop;   // 종목 옆 (한줄비고) - 선택
        private readonly string _reason;    // 매수이유 1줄 - 선택

        private Label _label;
        private System.Windows.Forms.Timer _timer;
        private Point _anchorScreen;
        private static int _openGate;

        // 클래스 필드로 추가
        private bool _finished;

        private TaskCompletionSource<PopupResult> _tcs;

        private QuickTradePopup(
            int price,
            int qty,
            int timeoutMs,
            Color sideColor,
            string symbol,
            int offsetX,
            int offsetY,
            string reason)
        {
            _price = Math.Max(1, price);
            _qty = Math.Max(1, qty);
            _timeoutMs = Math.Max(500, timeoutMs);
            _sideColor = sideColor;
            _symbol = symbol ?? string.Empty;

            _offsetX = offsetX;
            _offsetY = offsetY;

            _reason = reason ?? string.Empty;

            // ── 기본 폼 설정 ─────────────────────────────────────────────
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            KeyPreview = true;
            Padding = new Padding(18);

            // 투명창(글자만)
            BackColor = Color.Lime;
            TransparencyKey = Color.Lime;

            // ── 표시 라벨 (멀티라인) ─────────────────────────────────────
            _label = new Label
            {
                AutoSize = true,
                Font = new Font(SystemFonts.DefaultFont.FontFamily, 27f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent,
                ForeColor = _sideColor,
            };
            Controls.Add(_label);

            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;

            // ── 타이머 ─────────────────────────────────────────────────
            _timer = new System.Windows.Forms.Timer { Interval = _timeoutMs };

            // ⚠️ timeout 정책: 안전하게 "보류" 추천. (원하면 CancelRemove로 바꿔도 됨)
            //  - CancelRemove: timeout인데 종목을 관심에서 제거해버릴 수 있어 위험
            //  - CancelKeep : 그냥 지나가고 다음 pending으로
            _timer.Tick += (_, __) => CancelKeepAndClose();

            // ── 이벤트 연결 ───────────────────────────────────────────
            Shown += (_, __) =>
            {
                // Show 시점 커서 위치를 anchor로 고정
                _anchorScreen = Cursor.Position;

                // anchor + offset 으로 위치 결정
                Location = new Point(_anchorScreen.X + _offsetX, _anchorScreen.Y + _offsetY);

                SnapPrice();
                UpdateLine();

                // AutoSize로 실제 Width/Height 나온 뒤에 화면 밖 방지
                ClampToWorkingArea();

                // 시작 타임아웃 스타트
                ResetTimeout();

                // ✅ 앞으로/포커스 강제 (안 보임/키 안 먹음 방지)
                TopMost = true;
                BringToFront();
                Activate();
                Focus();
            };

            KeyDown += OnKeyDown;

            // ✅ 핵심 변경: 포커스 잃으면 "완전 기각"이 아니라 "보류"로 닫기
            // - Impulse 중 호가창 클릭 시 Deactivate가 자주 발생함
            // - 여기서 CancelRemove 하면 꼬임/오판이 너무 많음
            Deactivate += (_, __) => CancelKeepAndClose();
        }

        public static Task<PopupResult> ShowAsync(
            string symbol,
            int price,
            int qty,
            int offsetX = 100,
            int offsetY = -50,
            int timeoutMs = 10000,
            Color? sideColor = null,
            string reason = null)
        {
            // ✅ 내부 보험 게이트: 동시에 여러 개 뜨는 것 방지
            if (Interlocked.CompareExchange(ref _openGate, 1, 0) != 0)
            {
                // 이미 하나 떠있으면 "보류" 반환
                return Task.FromResult(new PopupResult(PopupResultKind.CancelKeep));
            }

            var popup = new QuickTradePopup(
                price,
                qty,
                timeoutMs,
                sideColor ?? Color.Black,
                symbol ?? "",
                offsetX,
                offsetY,
                reason
            );

            // ✅ 현재 팝업 전역 등록
            g.PopupCurrent = popup;

            popup._tcs = new TaskCompletionSource<PopupResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            // ✅ 어떤 경로로 닫히든 gate 해제 + Task 완결 + 전역 해제 보장
            popup.FormClosed += (_, __) =>
            {
                Interlocked.Exchange(ref _openGate, 0);

                // 전역 참조 정리 (내가 등록한 팝업일 때만)
                if (ReferenceEquals(g.PopupCurrent, popup))
                    g.PopupCurrent = null;

                if (!popup._finished)
                {
                    popup._finished = true;
                    popup._tcs.TrySetResult(new PopupResult(PopupResultKind.CancelKeep));
                }
            };

            void showOnUi()
            {
                try
                {
                    popup.Show();
                    popup.TopMost = true;
                    popup.BringToFront();
                    popup.Activate();
                }
                catch
                {
                    popup._finished = true;
                    popup._tcs.TrySetResult(new PopupResult(PopupResultKind.CancelKeep));

                    try { popup.Close(); } catch { }

                    Interlocked.Exchange(ref _openGate, 0);

                    if (ReferenceEquals(g.PopupCurrent, popup))
                        g.PopupCurrent = null;
                }
            }

            // Show()는 반드시 UI thread
            if (Application.OpenForms.Count > 0)
            {
                var ui = Application.OpenForms[0];
                if (ui.InvokeRequired)
                    ui.BeginInvoke((Action)showOnUi);
                else
                    showOnUi();
            }
            else
            {
                showOnUi();
            }

            return popup._tcs.Task;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // Tab / Shift+Tab 모두 관찰(keep)로 닫기
            if (keyData == Keys.Tab || keyData == (Keys.Shift | Keys.Tab))
            {
                CancelKeepAndClose();
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        // ── 키 처리 ───────────────────────────────────────────────────
        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            // ✅ 무조건 먼저 먹기 (키 새는 문제 방지)
            EatKey(e);

            switch (e.KeyCode)
            {
                // 실행(확정) : ` (ESC 바로 아래, Oem3)
                case Keys.Oem3:
                    ConfirmAndClose();
                    return;

                // ✅ 완전 기각 : ESC (명시적일 때만)
                case Keys.Space:
                    CancelRemoveAndClose();
                    return;

                // ✅ 완전 기각 : ESC (명시적일 때만)
                case Keys.Escape:
                    CancelRemoveAndClose();
                    return;

                // 물량 증가 : 1  (×1.25)
                case Keys.D1:
                case Keys.NumPad1:
                    AdjustQtyUp(1.25, 1);
                    UpdateLine();
                    ResetTimeout();
                    return;

                // 물량 감소 : 2  (×0.8)
                case Keys.D2: 
                case Keys.NumPad2:
                    AdjustQtyDown(0.80, 1);
                    UpdateLine();
                    ResetTimeout();
                    return;

                // 가격 증가(+틱) : Q
                case Keys.Q:
                    {
                        int tick = DealUtils.GetTick(_symbol, _price);
                        _price += tick;
                        SnapPrice();
                        UpdateLine();
                        ResetTimeout();
                        return;
                    }

                // 가격 감소(-틱) : W
                case Keys.W:
                    {
                        int tick = DealUtils.GetTick(_symbol, _price);
                        _price = Math.Max(tick, _price - tick);
                        SnapPrice();
                        UpdateLine();
                        ResetTimeout();
                        return;
                    }

                default:
                    // 실수 방지: 팝업 떠 있을 땐 다른 키는 모두 먹어버림
                    return;
            }
        }

        // 공통 종료 함수 (새로 추가)
        private void Finish(PopupResult result)
        {
            if (_finished) return;
            _finished = true;

            try
            {
                _timer?.Stop();
                _timer?.Dispose();
                _timer = null;
            }
            catch { /* ignore */ }

            _tcs?.TrySetResult(result);

            // ✅ Close만 호출 (Dispose는 여기서 하지 말자)
            if (!IsDisposed)
                Close();
        }

        private void ConfirmAndClose()
        {
            Finish(new PopupResult(PopupResultKind.Confirm, _price, _qty));
        }

        // 완전 기각: interested 제거 + chart update (호출부가 수행)
        private void CancelRemoveAndClose()
        {
            Finish(new PopupResult(PopupResultKind.CancelRemove));
        }

        private void CancelKeepAndClose()
        {
            Finish(new PopupResult(PopupResultKind.CancelKeep));
        }

        private void UpdateLine()
        {
            long amount = _price * (long)_qty;

            // 1줄: 종목 + (비고)
            string top = _symbol ?? string.Empty;
            //if (!string.IsNullOrWhiteSpace(_noteTop))
            //    top = string.IsNullOrWhiteSpace(top) ? $"({_noteTop})" : $"{top} ({_noteTop})";

            // 2줄: 한 줄로 길게
            string mid = $"{_price:N0} × {_qty:N0}  =  {amount:N0}";

            // 3줄: 매수이유(1줄)
            string reason = _reason ?? string.Empty;

            if (string.IsNullOrWhiteSpace(top))
                _label.Text = string.IsNullOrWhiteSpace(reason) ? mid : $"{mid}\n{reason}";
            else
                _label.Text = string.IsNullOrWhiteSpace(reason) ? $"{top}\n{mid}" : $"{top}\n{mid}\n{reason}";
        }

        private void ResetTimeout()
        {
            if (_timer == null) return;

            int ms = Math.Max(_timeoutMs, 50); // 최소 50ms 같은 가드
            _timer.Stop();
            _timer.Interval = ms;
            _timer.Start();
        }

        private void SnapPrice()
        {
            int tick = DealUtils.GetTick(_symbol, _price);
            if (tick <= 1) return;

            int mod = _price % tick;
            if (mod != 0)
                _price -= mod;

            _price = Math.Max(tick, _price);
        }

        private void ClampToWorkingArea()
        {
            var wa = Screen.FromPoint(_anchorScreen).WorkingArea;

            int x = Location.X;
            int y = Location.Y;

            if (x + Width > wa.Right) x = wa.Right - Width;
            if (y + Height > wa.Bottom) y = wa.Bottom - Height;

            if (x < wa.Left) x = wa.Left;
            if (y < wa.Top) y = wa.Top;

            Location = new Point(x, y);
        }

        private static void EatKey(KeyEventArgs e)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        private void AdjustQtyUp(double factor, int minStep)
        {
            int baseQty = Math.Max(1, _qty);
            int next = (int)Math.Ceiling(baseQty * factor);
            int step = Math.Max(minStep, next - baseQty);
            _qty = baseQty + step;
            UpdateLine();
            ResetTimeout();
        }

        private void AdjustQtyDown(double factor, int minStep)
        {
            int baseQty = Math.Max(1, _qty);
            int next = (int)Math.Floor(baseQty * factor);
            int step = Math.Max(minStep, baseQty - next);
            _qty = Math.Max(1, baseQty - step);
            UpdateLine();
            ResetTimeout();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);

            if (!_finished)
            {
                _tcs?.TrySetResult(new PopupResult(PopupResultKind.CancelKeep));
                _finished = true;
            }
        }

    }
}

