using New_Tradegy.Library.IO;
using New_Tradegy.Library.PostProcessing;
using New_Tradegy.Library.Utils;
using New_Tradegy.Library;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace New_Tradegy.Test
{
    internal class NqAnalyzer
    {

        public static void NQLeadLagAnalysis()
        {
            int[] mins = { 0, 5, 10, 20, 30, 40, 50, 75, 100 };
            int[] maxs = { 5, 10, 20, 30, 40, 50, 75, 100, int.MaxValue };

            string root = @"C:\BJS\Study\지수10초";
            string outputFile = Path.Combine(root, "NQ_THRESHOLD_RESULT.txt");

            if (!Directory.Exists(root))
            {
                MessageBox.Show("지수10초 디렉토리가 없습니다.");
                return;
            }

            var sb = new StringBuilder();

            sb.AppendLine("NQ Threshold Analysis");
            sb.AppendLine("X = 현재 10초 NQ 변화");
            sb.AppendLine("Y = 같은 10초 ETF 변화");
            sb.AppendLine();

            for (int side = 0; side < 2; side++)
            {
                bool positiveSide = (side == 0);

                sb.AppendLine(
                    positiveSide
                    ? "===== NQ Positive ====="
                    : "===== NQ Negative =====");
                sb.AppendLine();



                for (int k = 0; k < mins.Length; k++)
                {
                    int min = mins[k];
                    int max = maxs[k];
                    var debug = new StringBuilder();

                    var kpX = new List<double>();
                    var kpY = new List<double>();

                    var kqX = new List<double>();
                    var kqY = new List<double>();

                    string[] directories = Directory.GetDirectories(root)
                        .OrderBy(x => x)
                        .ToArray();

                    foreach (string directory in directories)
                    {
                        string name = Path.GetFileName(directory);

                        int date;
                        if (!int.TryParse(name, out date))
                            continue;

                        g.date = date;

                        Sec10Store.KospiRow = 0;
                        Sec10Store.KosdaqRow = 0;

                        FileLoader.LoadIndex10SecData();

                        AddLeadLagData(
                            "KOSPI",
                            Sec10Store.Kospi,
                            Sec10Store.KospiRow,
                            positiveSide,
                            min,
                            max,
                            kpX,
                            kpY,
                            debug);

                        AddLeadLagData(
                            "KOSDAQ",
                            Sec10Store.Kosdaq,
                            Sec10Store.KosdaqRow,
                            positiveSide,
                            min, max,
                            kqX, kqY,
                            debug);
                    }

                    var kp = RegressionMethod.FitLinear1D(kpX, kpY);
                    var kq = RegressionMethod.FitLinear1D(kqX, kqY);

                    string title =
                     max == int.MaxValue
                     ? $"{min}+"
                     : $"{min}~{max}";

                    sb.AppendLine($"Range {title}");

                    sb.AppendLine($"KOSPI  Count={kp.Count,7}  A={kp.A,8:F4}  B={kp.B,8:F4}  R2={kp.R2:F4}");
                    sb.AppendLine($"KOSDAQ Count={kq.Count,7}  A={kq.A,8:F4}  B={kq.B,8:F4}  R2={kq.R2:F4}");
                    sb.AppendLine();
                }

                File.WriteAllText(outputFile, sb.ToString());

                MessageBox.Show(
                    $"완료\n{outputFile}",
                    "NQ Threshold");
            }
        }

        private static void AddLeadLagData(
            string marketName,
            int[,] data,
            int rowCount,
            bool positiveSide,
            int minThreshold,
            int maxThreshold,
            List<double> xs,
            List<double> ys,
            StringBuilder debug)
        {
            int warmupRemaining = 3;

            if (data == null || rowCount < 2)
                return;

            for (int i = 1; i < rowCount; i++)
            {
                double dt = TimeUtils.ElapsedMillisecondsDouble(
                    data[i - 1, (int)Sec10Col.Time],
                    data[i, (int)Sec10Col.Time]);

                if (dt > 12_500)
                {
                    warmupRemaining = 3;
                    continue;
                }

                // 정상 10초 범위
                if (dt < 8_000)
                    continue;

                // 재시작 후 첫 3개의 정상 데이터 제외
                if (warmupRemaining > 0)
                {
                    warmupRemaining--;
                    continue;
                }

                double nqPrev2 = i >= 2
                ? data[i - 2, (int)Sec10Col.Nq]
                : double.NaN;

                double nqPrev3 = i >= 3
                    ? data[i - 3, (int)Sec10Col.Nq]
                    : double.NaN;

                double nqPrev = data[i - 1, (int)Sec10Col.Nq];
                double nqNow = data[i, (int)Sec10Col.Nq];

                // NQ가 3개 이상 같은 값으로 멈췄다가 다시 움직인 첫 점프 제외
                bool receiverRestartJump =
                    i >= 3 &&
                    nqPrev3 == nqPrev2 &&
                    nqPrev2 == nqPrev &&
                    nqNow != nqPrev;

                if (receiverRestartJump)
                    continue;

                double etfPrev = data[i - 1, (int)Sec10Col.Etf];
                double etfNow = data[i, (int)Sec10Col.Etf];

                double x = nqNow - nqPrev;

                if (positiveSide)
                {
                    if (x <= 0)
                        continue;
                }
                else
                {
                    if (x >= 0)
                        continue;
                }

                if (Math.Abs(x) > 1000)
                    continue;

                double ax = Math.Abs(x);

                if (ax < minThreshold || ax >= maxThreshold)
                    continue;

                double y = etfNow - etfPrev;

                xs.Add(x);
                ys.Add(y);
            }
        }

    }
}
