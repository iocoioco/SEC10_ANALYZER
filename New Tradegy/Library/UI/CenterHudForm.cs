// CenterHudForm.cs
using System;
using System.Drawing;
using System.Windows.Forms;

namespace New_Tradegy.Library.UI
{
    /// <summary>
    /// Center HUD (중요 공지 전용)
    /// 정책:
    /// - owner: 항상 Form1 (외부에서 변경 불가)
    /// - font family/style 고정 ("맑은 고딕", Bold)
    /// - fontSize만 외부에서 조정 가능 (default: 43)
    /// - foreColor: Brown
    /// - bgAlpha: 0 (완전 투명, 글자만)
    /// - padding / cornerRadius: 고정
    /// - duration 기본 5초
    /// </summary>
    /// 

    //usage
    //CenterHudForm.Show("섹터 강세");
    //CenterHudForm.Show("나스닥 급변\n외인 순매수 확대", 10000);
    //CenterHudForm.Show("중요 공지", 5000, 45);
    public sealed class CenterHudForm : HudBaseForm
    {
        private static CenterHudForm _inst;

        private CenterHudForm() : base() { }

        private static CenterHudForm Inst
        {
            get
            {
                if (_inst == null || _inst.IsDisposed)
                    _inst = new CenterHudForm();
                return _inst;
            }
        }

        /// <summary>
        /// Center HUD 표시 (최종 API)
        /// </summary>
        public static void Show(
            string text,
            int durationMs = 5000,
            float fontSize = 43f)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            // owner는 정책적으로 항상 Form1
            var owner = Application.OpenForms["Form1"] as Form;
            if (owner == null || owner.IsDisposed)
                return;

            var inst = Inst;

            using (var font = new Font("맑은 고딕", fontSize, FontStyle.Bold))
            {
                inst.ShowOnFormCenterInternal(
                    owner,
                    text,
                    durationMs,
                    font
                );
            }
        }

        /// <summary>
        /// 실제 중앙 계산 + 표시 (내부 전용)
        /// </summary>
        private void ShowOnFormCenterInternal(
            Form owner,
            string text,
            int durationMs,
            Font font)
        {
            // HUD 크기 계산
            var size = MeasureHudSize(
                text,
                font,
                padding: 20,
                maxSize: new Size(1200, 800)
            );

            // owner 전체 영역 기준 중앙 (테두리/타이틀 포함)
            var ownerRect = owner.Bounds;

            // 작업표시줄 등 고려해서 WorkingArea 안으로 클램프
            var wa = Screen.FromControl(owner).WorkingArea;

            int x = ownerRect.Left + (ownerRect.Width - size.Width) / 2;
            int y = ownerRect.Top + (ownerRect.Height - size.Height) / 2;

            x = Math.Max(wa.Left, Math.Min(wa.Right - size.Width, x));
            y = Math.Max(wa.Top, Math.Min(wa.Bottom - size.Height, y));

            ShowHudAt(
                screenLocation: new Point(x, y),
                text: text,
                durationMs: durationMs,

                // --- Center HUD 고정 정책 ---
                font: font,
                foreColor: Color.Blue,
                bgAlpha: 0,
                bgColor: null,
                padding: 20,
                cornerRadius: 18
            );
        }

        public static void HideHud()
        {
            try
            {
                if (_inst == null || _inst.IsDisposed)
                    return;

                if (_inst.InvokeRequired)
                {
                    _inst.BeginInvoke(new Action(() =>
                    {
                        if (_inst != null && !_inst.IsDisposed)
                        {
                            _inst._hideTimer.Stop();
                            _inst.Close();
                            _inst = null;
                        }
                    }));
                }
                else
                {
                    _inst._hideTimer.Stop();
                    _inst.Close();
                    _inst = null;
                }
            }
            catch
            {
            }
        }
    }
}
