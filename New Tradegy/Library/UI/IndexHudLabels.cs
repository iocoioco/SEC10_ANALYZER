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
            // 1) NQ
            // --------------------------------------------------
            double nq = MajorIndex.Instance.NasdaqIndex;

            string l1 = string.Format(
                CultureInfo.InvariantCulture,
                "NQ {0:+0.000;-0.000;0.000}",
                nq);

            // --------------------------------------------------
            // 2) ETF
            // --------------------------------------------------
            double etfNow = isKospi
                ? MajorIndex.Instance.KospiIndex
                : MajorIndex.Instance.KosdaqIndex;

            string l2 = string.Format(
        CultureInfo.InvariantCulture,
        "ETF {0:+0.00;-0.00;0.00}",
        etfNow / 100.0);

            // --------------------------------------------------
            // 3) MUL : 10 / 20 / 30
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
                "{0:+0;-0;0}/{1:+0;-0;0}/{2:+0;-0;0}/{3:+0;-0;0}",
                Math.Round(p.분30프로천 / 10.0),
                Math.Round(p.분30외인천 / 10.0),
                Math.Round((dInst * 60 / 80) / 10.0),
                Math.Round((dRetail * 60 / 80) / 10.0));

            // --------------------------------------------------
            // 5) A / R / Z / E
            // --------------------------------------------------
            var nm = MajorIndex.Instance.NqMotion;
            double e = isKospi ? nm.EKospi : nm.EKosdaq; // A * nq + C

            // etfNow와 e는 둘 다 ×100 값
            double deltaE = (etfNow - e) / 100.0;

            string l5 = string.Format(
                CultureInfo.InvariantCulture,
                "A {0:+0.000;-0.000;0.000}{2}{2}R {1:+0.00;-0.00;0.00}",
                nm.A,
                nm.R,
                sp);

            string l6 = string.Format(
                CultureInfo.InvariantCulture,
                "Z {0:+0.0;-0.0;0.0}{2}{2}ΔE {1:+0.00;-0.00;0.00}",
                nm.Z,
                deltaE,
                sp);

            // --------------------------------------------------
            // 6) Score : 색상용
            // --------------------------------------------------
            double score = 0.0;

            if (nm.A > 0) score += 1.0;
            if (nm.A < 0) score -= 1.0;

            if (nm.Z > 1.0) score += 1.0;
            if (nm.Z < -1.0) score -= 1.0;

            if (e < 0) score += 1.0;
            if (e > 0) score -= 1.0;

            // R은 방향이 아니라 신뢰도이므로 최종 점수 보정
            rawScore = score;

            if (nm.R < 0.20)
                rawScore *= 0.70;
            else if (nm.R < 0.40)
                rawScore *= 0.85;


            return $"{l1}\n{l2}\n{l3}\n{l4}\n{l5}\n{l6}";
        }
        private static (double dInst, double dRetail) DeltaPair(double[] instArr, double[] retailArr)
        {
            return (
                DeltaRecent(instArr, 120),
                DeltaRecent(retailArr, 120)
            );
        }

        private static double DeltaRecent(double[] arr, int maxLookBackSeconds)
        {
            if (arr == null || arr.Length < 2)
                return 0.0;

            int lastIndex = arr.Length - 1;
            double last = arr[lastIndex];

            // 분 데이터 배열이라고 가정: 1칸 = 60초
            int maxLookBackBars = maxLookBackSeconds / 60;

            for (int i = lastIndex - 1; i >= 0 && (lastIndex - i) <= maxLookBackBars; i--)
            {
                double prev = arr[i];

                // 천만원 단위 저장이므로 1 이상 차이면 의미 있는 변화
                if (Math.Abs(last - prev) >= 1.0)
                    return last - prev;
            }

            return 0.0;
        }
        private static Color ScoreToColor(ref double displayScore, double rawScore)
        {
            // 천천히 변화
            displayScore = 0.85 * displayScore + 0.15 * rawScore;

            // score 범위 대략 -3 ~ +3 기준 압축
            double s = Math.Max(-1.0, Math.Min(1.0, displayScore / 3.0));

            // 너무 진하지 않게
            s *= 0.85;

            Color white = Color.FromArgb(255, 255, 255);

            // 매수 후보: 녹색 계열
            Color green = Color.FromArgb(120, 220, 140);

            // 매도/위험: 빨강 계열
            Color red = Color.FromArgb(255, 120, 120);

            return s >= 0
                ? Lerp(white, green, s)
                : Lerp(white, red, -s);
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