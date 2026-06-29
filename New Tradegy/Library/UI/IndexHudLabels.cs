using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;
using New_Tradegy.Library.Listeners;
using New_Tradegy.Library.Models;
using New_Tradegy.Library.PostProcessing;

namespace New_Tradegy.Library.UI
{
    public class IndexHudLabels
    {
        private readonly Control _parent;
        private readonly Label _lblKospi;
        private readonly Label _lblKosdaq;

        private double _kospiDisplayScore = 0.0;
        private double _kosdaqDisplayScore = 0.0;

        private DateTime _lastHeatLogTime = DateTime.MinValue;
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

            HeatResult kospiH1, kospiH25, kospiH5;
            HeatResult kosdaqH1, kosdaqH25, kosdaqH5;

            _lblKospi.Text = BuildIndexLabelText(
                kospiData, true, out double rawScoreKospi,
                out kospiH1, out kospiH25, out kospiH5);

            _lblKosdaq.Text = BuildIndexLabelText(
                kosdaqData, false, out double rawScoreKosdaq,
                out kosdaqH1, out kosdaqH25, out kosdaqH5);

            TrySaveHeatLog(
                MajorIndex.Instance.NqMotion,
                kospiH1, kospiH25, kospiH5,
                kosdaqH1, kosdaqH25, kosdaqH5);
        }

        private void TrySaveHeatLog(
            NqMotionState nm,
            HeatResult kospiH1, HeatResult kospiH25, HeatResult kospiH5,
            HeatResult kosdaqH1, HeatResult kosdaqH25, HeatResult kosdaqH5)
        {
            DateTime now = DateTime.Now;

            if ((now - _lastHeatLogTime).TotalSeconds < 10)
                return;

            _lastHeatLogTime = now;

            string dir = @"C:\BJS\data work\HeatLog";
            Directory.CreateDirectory(dir);

            string path = Path.Combine(dir, now.ToString("yyyyMMdd") + "_HeatLog.csv");

            bool newFile = !File.Exists(path);

            if (newFile)
            {
                File.AppendAllText(
                    path,
                    "Time,NQ,KospiETF,KosdaqETF," +
                    "KpH1,KpH25,KpH5,KpZ1,KpZ25,KpZ5," +
                    "KdH1,KdH25,KdH5,KdZ1,KdZ25,KdZ5," +
                    "NqA,NqR,NqZ,EKospi,EKosdaq,DeltaKospi,DeltaKosdaq" +
                    Environment.NewLine,
                    Encoding.UTF8);
            }

            double nq = MajorIndex.Instance.NasdaqIndex;

            double kospiEtf = MajorIndex.Instance.KospiIndex;
            double kosdaqEtf = MajorIndex.Instance.KosdaqIndex;

            double deltaKospi = (nm.EKospi - kospiEtf) / 100.0;
            double deltaKosdaq = (nm.EKosdaq - kosdaqEtf) / 100.0;

            string line = string.Format(
                CultureInfo.InvariantCulture,
                "{0:HH:mm:ss},{1:+0.000;-0.000;0.000},{2:+0.00;-0.00;0.00},{3:+0.00;-0.00;0.00}," +
                "{4:+0.0;-0.0;0.0},{5:+0.0;-0.0;0.0},{6:+0.0;-0.0;0.0},{7:+0.0;-0.0;0.0},{8:+0.0;-0.0;0.0},{9:+0.0;-0.0;0.0}," +
                "{10:+0.0;-0.0;0.0},{11:+0.0;-0.0;0.0},{12:+0.0;-0.0;0.0},{13:+0.0;-0.0;0.0},{14:+0.0;-0.0;0.0},{15:+0.0;-0.0;0.0}," +
                "{16:+0.000;-0.000;0.000},{17:0.00},{18:+0.0;-0.0;0.0},{19:+0.00;-0.00;0.00},{20:+0.00;-0.00;0.00},{21:+0.00;-0.00;0.00},{22:+0.00;-0.00;0.00}",
                now,
                nq,
                kospiEtf / 100.0,
                kosdaqEtf / 100.0,

                kospiH1.Heat,
                kospiH25.Heat,
                kospiH5.Heat,
                kospiH1.Z,
                kospiH25.Z,
                kospiH5.Z,

                kosdaqH1.Heat,
                kosdaqH25.Heat,
                kosdaqH5.Heat,
                kosdaqH1.Z,
                kosdaqH25.Z,
                kosdaqH5.Z,

                nm.A,
                nm.R,
                nm.Z,
                nm.EKospi / 100.0,
                nm.EKosdaq / 100.0,
                deltaKospi,
                deltaKosdaq);

            File.AppendAllText(path, line + Environment.NewLine, Encoding.UTF8);
        }

        private static string BuildIndexLabelText(
             StockData data,
             bool isKospi,
             out double rawScore,
             out HeatResult h1,
             out HeatResult h25,
             out HeatResult h5)
        {
            rawScore = 0.0;
            h1 = default(HeatResult);
            h25 = default(HeatResult);
            h5 = default(HeatResult);

            if (data == null || data.Post == null || data.Api == null)
                return string.Empty;

            var p = data.Post;
            string sp = "\u2007"; // figure space

            // --------------------------------------------------
            // 1) NQ
            // --------------------------------------------------
            double nq = MajorIndex.Instance.NasdaqIndex;

            // --------------------------------------------------
            // 1) NQ + ETF
            // --------------------------------------------------

            double etfNow = isKospi
                ? MajorIndex.Instance.KospiIndex
                : MajorIndex.Instance.KosdaqIndex;

            string l1 = string.Format(
                CultureInfo.InvariantCulture,
                "NQ {0:+0.000;-0.000;0.000}{2}ETF {1:+0.00;-0.00;0.00}",
                nq,
                etfNow / 100.0,
                sp);

            // --------------------------------------------------
            // 2) MUL : 10 / 20 / 30
            // --------------------------------------------------
            string l2 = string.Format(
                CultureInfo.InvariantCulture,
                "{0:0}/{1:0}{4}{2:0}/{3:0}{4}{5:0}/{6:0}",
                p.분10배수차, p.분10배수합,
                p.분20배수차, p.분20배수합,
                sp,
                p.분30배수차, p.분30배수합);

            // --------------------------------------------------
            // 3) FLOW : PRO / FOR / INST / RETAIL
            // --------------------------------------------------
            var (dInst, dRetail) = DeltaPair(data.Api.분기관천, data.Api.분개인천);

            string l3 = string.Format(
                CultureInfo.InvariantCulture,
                "{0:+0;-0;0}/{1:+0;-0;0}/{2:+0;-0;0}/{3:+0;-0;0}",
                Math.Round(p.분30프로천 / 10.0),
                Math.Round(p.분30외인천 / 10.0),
                Math.Round((dInst * 60 / 80) / 10.0),
                Math.Round((dRetail * 60 / 80) / 10.0));

            // --------------------------------------------------
            // 4) NQ Motion
            // --------------------------------------------------
            var nm = MajorIndex.Instance.NqMotion;

            double e = isKospi ? nm.EKospi : nm.EKosdaq;

            // etfNow와 e는 둘 다 ×100
            double deltaE = (e - etfNow) / 100.0;

            string l4 = string.Format(
                CultureInfo.InvariantCulture,
                "A {0:+0.000;-0.000;0.000}{2}{2}R {1:+0.00;-0.00;0.00}",
                nm.A,
                nm.R,
                sp);

            // --------------------------------------------------
            // 5) Z / ΔE
            // --------------------------------------------------
            string l5 = string.Format(
                CultureInfo.InvariantCulture,
                "Z {0:+0.0;-0.0;0.0}{2}{2}ΔE {1:+0.00;-0.00;0.00}",
                nm.Z,
                deltaE,
                sp);

            // --------------------------------------------------
            // 6) Heat
            // --------------------------------------------------
            int nqCol = 10;
            int etfCol = 1;

            var kospiH1 = g.Sec10Kospi.CalcHeat(6, nqCol, etfCol);
            var kospiH25 = g.Sec10Kospi.CalcHeat(15, nqCol, etfCol);
            var kospiH5 = g.Sec10Kospi.CalcHeat(30, nqCol, etfCol);

            var kosdaqH1 = g.Sec10Kosdaq.CalcHeat(6, nqCol, etfCol);
            var kosdaqH25 = g.Sec10Kosdaq.CalcHeat(15, nqCol, etfCol);
            var kosdaqH5 = g.Sec10Kosdaq.CalcHeat(30, nqCol, etfCol);

             h1 = isKospi ? kospiH1 : kosdaqH1;
             h25 = isKospi ? kospiH25 : kosdaqH25;
             h5 = isKospi ? kospiH5 : kosdaqH5;

            string l6 = string.Format(
                CultureInfo.InvariantCulture,
                "H {0:+0.0;-0.0;0.0}/{1:+0.0;-0.0;0.0}/{2:+0.0;-0.0;0.0}",
                h1.Heat,
                h25.Heat,
                h5.Heat);

            // --------------------------------------------------
            // 7) Heat Z
            // --------------------------------------------------
            string l7 = string.Format(
                CultureInfo.InvariantCulture,
                "HZ {0:+0.0;-0.0;0.0}/{1:+0.0;-0.0;0.0}/{2:+0.0;-0.0;0.0}",
                h1.Z,
                h25.Z,
                h5.Z);


            // --------------------------------------------------
            // 6) Score : 색상용
            // --------------------------------------------------
            double score = 0.0;

            //-------------------------------------------------
            // 1. NQ Motion
            //-------------------------------------------------
            score += Math.Sign(nm.A) * 1.0;

            //-------------------------------------------------
            // 2. NQ Z
            //-------------------------------------------------
            score += Math.Max(-1.0, Math.Min(1.0, nm.Z / 2.0));

            //-------------------------------------------------
            // 3. ETF Residual
            //-------------------------------------------------
            score += Math.Max(-1.0, Math.Min(1.0, -deltaE / 0.30));

            //-------------------------------------------------
            // 4. Heat
            //-------------------------------------------------
            score += Math.Max(-1.0, Math.Min(1.0, h1.Heat / 3.0));
            score += Math.Max(-0.5, Math.Min(0.5, h25.Heat / 5.0));

            //-------------------------------------------------
            // 5. Heat Z
            //-------------------------------------------------
            score += Math.Max(-0.5, Math.Min(0.5, h1.Z / 3.0));

            if (Math.Abs(h1.Heat) > 0.5 &&
                Math.Abs(h1.Z) > 0.5 &&
                Math.Sign(h1.Heat) != Math.Sign(h1.Z))
            {
                score *= 0.8;
            }

            rawScore = score;

            if (nm.R < 0.2)
                rawScore *= 0.5;
            else if (nm.R < 0.4)
                rawScore *= 0.75;

            return $"{l1}\n{l2}\n{l3}\n{l4}\n{l5}\n{l6}\n{l7}";
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