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

        private double _displayScoreKospi = 0.0;
        private double _displayScoreKosdaq = 0.0;
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

            _lblKospi.BackColor = ScoreToColor(ref _displayScoreKospi, rawScoreKospi);
            _lblKosdaq.BackColor = ScoreToColor(ref _displayScoreKosdaq, rawScoreKosdaq);

            _lblKospi.ForeColor = Color.Black;
            _lblKosdaq.ForeColor = Color.Black;

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
            string sp = "\u2007"; // figure space

            rawScore = 0.0;
            h1 = default(HeatResult);
            h25 = default(HeatResult);
            h5 = default(HeatResult);

            if (data == null || data.Post == null || data.Api == null)
                return string.Empty;

            var p = data.Post;

            // --------------------------------------------------
            // 1) NQ / ETF
            // --------------------------------------------------
            double nq = MajorIndex.Instance.NasdaqIndex;

            double etfNow = isKospi
                ? MajorIndex.Instance.KospiIndex
                : MajorIndex.Instance.KosdaqIndex;

            string l1 = string.Format(
    CultureInfo.InvariantCulture,
    "{0:+0.000;-0.000;0.000} {1:+0.00;-0.00;0.00}",
    nq,
    etfNow / 100.0);

            // --------------------------------------------------
            // Heat 계산
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

            // --------------------------------------------------
            // 2) Heat
            // --------------------------------------------------
            string l2 = string.Format(
                CultureInfo.InvariantCulture,
                "H {0:+0.0;-0.0;0.0}|{1:+0.0;-0.0;0.0}|{2:+0.0;-0.0;0.0}",
                h1.Heat,
                h25.Heat,
                h5.Heat);

            // --------------------------------------------------
            // 3) Heat Z
            // --------------------------------------------------
            string l3 = string.Format(
                CultureInfo.InvariantCulture,
                "Z {0:+0.0;-0.0;0.0}|{1:+0.0;-0.0;0.0}|{2:+0.0;-0.0;0.0}",
                h1.Z,
                h25.Z,
                h5.Z);

            // --------------------------------------------------
            // 4) NQ Motion : A / R
            // --------------------------------------------------
            var nm = MajorIndex.Instance.NqMotion;
            double e = isKospi ? nm.EKospi : nm.EKosdaq; 
            double deltaE = (e - etfNow) / 100.0; // etfNow와 e는 둘 다 ×100

            string l4 = string.Format(
                CultureInfo.InvariantCulture,
                "A {0:+0.000;-0.000;0.000}  R {1:0.00}",
                nm.A,
                nm.R);

            // --------------------------------------------------
            // 5) NQ Motion : Z / ΔE
            // --------------------------------------------------
            string l5 = string.Format(
                CultureInfo.InvariantCulture,
                "Z {0:+0.0;-0.0;0.0}  ΔE {1:+0.00;-0.00;0.00}",
                nm.Z,
                deltaE);

            // --------------------------------------------------
            // 6) MUL : 10 / 20 / 30
            // --------------------------------------------------
            string l6 = string.Format(
                CultureInfo.InvariantCulture,
                "{0:0}|{1:0} {2:0}|{3:0} {4:0}|{5:0}",
                p.분10배수차, p.분10배수합,
                p.분20배수차, p.분20배수합,
                p.분30배수차, p.분30배수합);

            // --------------------------------------------------
            // 7) FLOW : PRO / FOR / INST / RETAIL
            // --------------------------------------------------
            double[] instArr = GetColumn(data.Api.x, 4, data.Api.nrow);
            double[] retailArr = GetColumn(data.Api.x, 6, data.Api.nrow);

            var (dInst, dRetail) = DeltaPair(instArr, retailArr);

            string l7 = string.Format(
                CultureInfo.InvariantCulture,
                "{0:+0;-0;0}{4}{1:+0;-0;0}{4}{2:+0;-0;0}{4}{3:+0;-0;0}",
                p.분30프로천 / 10,
                p.분30외인천 / 10,
                dInst * 60 / 90 / 10,
                dRetail * 60 / 90 / 10,
                sp);

            //Current Scoring System
            //1.NQ Motion(A)      ±1.0
            //2.NQ Z              ±1.0
            //3.ΔE                ±1.0
            //4.Heat1             ±1.0
            //5.Heat2.5           ±0.5
            //6.HeatZ             ±0.5

            // --------------------------------------------------
            // Score : 색상용
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

        private static Color ScoreToColor(ref double displayScore, double rawScore)
        {
            // 천천히 변화
            displayScore = 0.85 * displayScore + 0.15 * rawScore;

            double s = Math.Max(-3.0, Math.Min(3.0, displayScore));

            // 7단계 (연한 색 위주)
            Color strongBuy = Color.FromArgb(120, 200, 120);
            Color buy = Color.FromArgb(170, 230, 170);
            Color weakBuy = Color.FromArgb(220, 245, 220);

            Color neutral = Color.White;

            Color weakSell = Color.FromArgb(255, 235, 235);
            Color sell = Color.FromArgb(255, 205, 205);
            Color strongSell = Color.FromArgb(255, 170, 170);

            if (s >= 2.0)
                return strongBuy;

            if (s >= 1.2)
                return buy;

            if (s >= 0.3)
                return weakBuy;

            if (s > -0.3)
                return neutral;

            if (s > -1.2)
                return weakSell;

            if (s > -2.0)
                return sell;

            return strongSell;
        }

        private static double[] GetColumn(int[,] x, int col, int nrow)
        {
            if (x == null || nrow <= 0)
                return null;

            int maxRow = x.GetLength(0);
            int count = Math.Min(nrow, maxRow);

            double[] arr = new double[count];

            for (int i = 0; i < count; i++)
            {
                int row = nrow - 1 - i;   // 최신 → 과거
                if (row < 0)
                    break;

                arr[i] = x[row, col];
            }

            return arr;
        }

        private static (double dInst, double dRetail) DeltaPair(double[] instArr, double[] retailArr)
        {
            return (
                DeltaRecent(instArr, 90),
                DeltaRecent(retailArr, 90)
            );
        }

        private static double DeltaRecent(double[] arr, int maxLookBackSeconds)
        {
            if (arr == null || arr.Length < 2)
                return 0.0;

            // arr[0] 이 최신값
            double last = arr[0];

            // 7222 누적 수급은 약 90초마다 갱신된다.
            // 분 배열은 시각 경계가 어긋날 수 있으므로 +1칸 더 본다.
            int maxLookBackBars =
                (int)Math.Ceiling(maxLookBackSeconds / 60.0) + 1;

            int maxIndex = Math.Min(arr.Length - 1, maxLookBackBars);

            for (int i = 1; i <= maxIndex; i++)
            {
                double prev = arr[i];

                if (Math.Abs(last - prev) >= 1.0)
                    return last - prev;
            }

            return 0.0;
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