using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using New_Tradegy.Library.Models;

namespace New_Tradegy.Library.UI
{
    public class IndexHudLabels
    {
        private readonly Control _parent;
        private readonly Label _lblKospi;
        private readonly Label _lblKosdaq;

        private double _kospiDisplayScore = 0.0;
        private double _kosdaqDisplayScore = 0.0;



        public IndexHudLabels(Control parent)
        {
            _parent = parent ?? throw new ArgumentNullException(nameof(parent));

            _lblKospi = CreateLabel();
            _lblKosdaq = CreateLabel();

            _parent.Controls.Add(_lblKospi);
            _parent.Controls.Add(_lblKosdaq);
        }

        private static Label CreateLabel()
        {
            return new Label
            {
                BackColor = Color.White,
                ForeColor = Color.Black,
                Font = new Font("Consolas", 12, FontStyle.Bold),
                TextAlign = ContentAlignment.TopLeft,
                BorderStyle = BorderStyle.FixedSingle,
                AutoSize = false
            };
        }

        public void Relocate()
        {


           float  W = (float)g.ChartManager.Chart1.Width;
            float H = (float)g.ChartManager.Chart1.Height;
            float bookH = (float) g.cellHeight * 7;
            float hudH = (float)(H / 3 - bookH) ;


            float yKospi = bookH;
            float yKosdaq = H / 3 * 2 + bookH;

            float x = W / 10 * 2;

            _lblKospi.SetBounds((int)x, (int)yKospi, (int)W /10, (int)hudH);
            _lblKosdaq.SetBounds((int)x, (int)yKosdaq, (int)W / 10, (int)hudH);

            _lblKospi.BringToFront();
            _lblKosdaq.BringToFront();
        }







        public void Update(StockData kospiData, StockData kosdaqData)
        {
            if (_lblKospi != null && _lblKospi.InvokeRequired)
            {
                _lblKospi.BeginInvoke(new Action(() => Update(kospiData, kosdaqData)));
                return;
            }

            if (kospiData != null)
            {
                _lblKospi.Text = BuildIndexLabelText(kospiData, true, out double rawScoreKospi);
                _lblKospi.BackColor = ScoreToColor(ref _kospiDisplayScore, rawScoreKospi);
            }
             
            if (kosdaqData != null)
            {
                _lblKosdaq.Text = BuildIndexLabelText(kosdaqData, false, out double rawScoreKosdaq);
                _lblKosdaq.BackColor = ScoreToColor(ref _kosdaqDisplayScore, rawScoreKosdaq);
            }
        }

        public static string BuildIndexLabelText(StockData data, bool isKospi, out double rawScore)
        {
            rawScore = 0.0;
            if (data == null || data.Post == null || data.Api == null)
                return string.Empty;

            var p = data.Post;
            string sp = "\u2007"; // figure space

            // --------------------------------------------------
            // 1) NQ : 현재값만 표시
            // --------------------------------------------------
            double nq = MajorIndex.Instance.NasdaqIndex;

            string l1 = string.Format(
                CultureInfo.InvariantCulture,
                "NQ {0:F3}",
                nq);

            // --------------------------------------------------
            // 2) ETF : 현재 지수값만 표시
            // --------------------------------------------------
            double etfNow = isKospi
                ? MajorIndex.Instance.KospiIndex
                : MajorIndex.Instance.KosdaqIndex;

            string l2 = string.Format(
                CultureInfo.InvariantCulture,
                "ETF {0:F2}",
                etfNow);

            // --------------------------------------------------
            // 3) MUL : 배수차 / 배수합
            // --------------------------------------------------
            string l3 = string.Format(
                CultureInfo.InvariantCulture,
                "{0:0}/{1:0}{4}{2:0}/{3:0}{4}{5:0}/{6:0}",
                p.분10배수차, p.분10배수합,
                p.분20배수차, p.분20배수합,
                sp,
                p.분30배수차, p.분30배수합);

            // --------------------------------------------------
            // 4) FLOW : PRO / FOR / INST / RETAIL
            // --------------------------------------------------
            var (dInst, dRetail) = DeltaPair(data.Api.분기관천, data.Api.분개인천);

            

            string l4 = string.Format(
                CultureInfo.InvariantCulture,
                "{0:0}/{1:0}{6}{2:+0;-0;0}{6}{3:+0;-0;0}{6}{4:+0;-0;0}{6}{5:+0;-0;0}",
                p.분30배수차, p.분30배수합,
                p.분30프로천,
                p.분30외인천,
                dInst * 60 / 80,
                dRetail * 60 / 80,
                sp);

            // --------------------------------------------------
            // 5) ETF score : 화면 색상용
            //    숫자는 안 보여도 rawScore 계산은 유지
            // --------------------------------------------------
            double sigma = isKospi ? 6.4 : 6.5;

            double etf0 = p.분10가격차;
            double etf1 = p.분20가격차 - p.분10가격차;
            double etf2 = p.분30가격차 - p.분20가격차;

            double z0 = etf0 / sigma;
            double z1 = etf1 / sigma;
            double z2 = etf2 / sigma;

            rawScore = 0.5 * z0 + 0.3 * z1 + 0.2 * z2;

            return $"{l1}\n{l2}\n{l3}\n{l4}";
        }
        private static (double dInst, double dRetail) DeltaPair(double[] instArr, double[] retailArr)
        {
            double dInst = 0.0;
            double dRetail = 0.0;

            if (instArr != null && instArr.Length >= 2)
                dInst = instArr[instArr.Length - 1] - instArr[instArr.Length - 2];

            if (retailArr != null && retailArr.Length >= 2)
                dRetail = retailArr[retailArr.Length - 1] - retailArr[retailArr.Length - 2];

            return (dInst, dRetail);
        }

        private static Color ScoreToColor(ref double displayScore, double rawScore)
        {
            // 천천히 변화
            displayScore = 0.85 * displayScore + 0.15 * rawScore;

            // 압축
            double s = Math.Max(-1.0, Math.Min(1.0, displayScore / 2.0));

            // 끝단 제한 (과도한 색 방지)
            s *= 0.8;   // ⭐ 핵심

            // 더 연한 색으로 변경
            Color blue = Color.FromArgb(100, 140, 255);   // 기존보다 밝게
            Color white = Color.FromArgb(255, 255, 255);
            Color red = Color.FromArgb(255, 120, 120);    // 기존보다 밝게

            return s >= 0
                ? Lerp(white, red, s)
                : Lerp(white, blue, -s);
        }

        private static Color Lerp(Color a, Color b, double t)
        {
            t = Math.Max(0.0, Math.Min(1.0, t));

            int r = (int)(a.R + (b.R - a.R) * t);
            int g = (int)(a.G + (b.G - a.G) * t);
            int bb = (int)(a.B + (b.B - a.B) * t);

            return Color.FromArgb(r, g, bb);
        }
    }
}