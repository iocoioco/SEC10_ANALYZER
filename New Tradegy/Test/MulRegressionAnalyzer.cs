using New_Tradegy.Library;
using New_Tradegy.Library.IO;
using New_Tradegy.Library.Listeners;
using New_Tradegy.Library.PostProcessing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace New_Tradegy.Test
{
    internal class MulRegressionAnalyzer
    {
        public static void RawDataForAllDates()
        {
            string root = @"C:\BJS\Study\지수10초";

            foreach (string dir in Directory.GetDirectories(root))
            {
                string name = Path.GetFileName(dir);

                int date;
                if (!int.TryParse(name, out date))
                    continue;

                CreateRawFilesForDate(date);
            }

            MessageBox.Show("Raw Data 완료");
        }
        public static void CreateRawFilesForDate(int date)
        {
            CreateRawFileForDate(date, true);   // KOSPI
            CreateRawFileForDate(date, false);  // KOSDAQ
        }

        private static void CreateRawFileForDate(int date, bool isKospi)
        {
            int[,] a = isKospi
                ? Sec10Store.Kospi
                : Sec10Store.Kosdaq;

            int n = isKospi
                ? Sec10Store.KospiRow
                : Sec10Store.KosdaqRow;

            if (a == null || n < 2)
                return;

            string dir = Path.Combine(@"C:\BJS\Study\지수10초", date.ToString());

            string fileName = isKospi
                ? "KOSPI_REG_RAW_V1.txt"
                : "KOSDAQ_REG_RAW_V1.txt";

            string path = Path.Combine(dir, fileName);

            StringBuilder sb = new StringBuilder();

            sb.AppendLine("Index,Time,Price,D,S,DS,PrevPrice,Y");

            // i = 1부터 시작
            // Y = 현재가격 - 이전가격
            for (int i = 1; i < n; i++)
            {
                int time = a[i, 0];

                double price = a[i, 1];
                double prevPrice = a[i - 1, 1];

                double d = a[i, 7];   // diff
                double s = a[i, 8];   // sum
                double ds = d * s;

                double y = price - prevPrice;

                sb.AppendLine(
                    $"{i},{time},{price:F2},{d:F4},{s:F4},{ds:F4},{prevPrice:F2},{y:F2}");
            }

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }




        public static void RunSummary(int method)
        {
            string root = @"C:\BJS\Study\지수10초";
            string outPath = Path.Combine(
                root,
                $"RegressionSummary_V{method}.txt");

            StringBuilder sb = new StringBuilder();

            RunMethodWiseExperiment(sb, true, method);
            RunMethodWiseExperiment(sb, false, method);

            File.WriteAllText(outPath, sb.ToString(), Encoding.UTF8);
        }

        private static void RunMethodWiseExperiment(StringBuilder sb, bool isKospi, int method)
        {
            string root = @"C:\BJS\Study\지수10초";
            string market = isKospi ? "KOSPI" : "KOSDAQ";

            sb.AppendLine("==================================================");
            sb.AppendLine("001 Current10 D+DS");
            sb.AppendLine("Y = Price(i) - Price(i-1)");
            switch (method)
            {
                case 1:
                    sb.AppendLine("Model = A*D + B*DS + C");
                    break;

                case 2:
                    sb.AppendLine("Model = A*D + B*S + C");
                    break;
                case 3:
                    sb.AppendLine("Model = Y = A·X + C \n X = D × (1 + kS)");
                    break;
            }
            sb.AppendLine("Market,Date,Count,A,B,C,R2");

            List<double> allX = new List<double>();
            List<double> allY = new List<double>();
            List<double> allZ = new List<double>();

            foreach (string dir in Directory.GetDirectories(root))
            {
                string name = Path.GetFileName(dir);

                int date;
                if (!int.TryParse(name, out date))
                    continue;

                string fileName = isKospi
                    ? $"KOSPI_REG_RAW_V1.txt"
                    : $"KOSDAQ_REG_RAW_V1.txt";

                string path = Path.Combine(dir, fileName);

                if (!File.Exists(path))
                    continue;

                List<double> x = new List<double>();
                List<double> y = new List<double>();
                List<double> z = new List<double>();

                string[] lines = File.ReadAllLines(path);

                for (int i = 1; i < lines.Length; i++)
                {
                    string[] sp = lines[i].Split(',');
                    if (sp.Length < 8)
                        continue;

                    double d, s, ds, yy;

                    if (!double.TryParse(sp[3], out d)) continue;
                    if (!double.TryParse(sp[4], out s)) continue;
                    if (!double.TryParse(sp[5], out ds)) continue;
                    if (!double.TryParse(sp[7], out yy)) continue;

                    switch (method)
                    {
                        case 1:
                            x.Add(d);
                            y.Add(ds);
                            z.Add(yy);

                            allX.Add(d);
                            allY.Add(ds);
                            allZ.Add(yy);
                            break;
                        case 2:
                            if (Math.Abs(s) < 1e-9)
                                continue;

                            double div = d / s;

                            x.Add(div);   // D / S
                            y.Add(s);     // S
                            z.Add(yy);

                            allX.Add(div);
                            allY.Add(s);
                            allZ.Add(yy);
                            break;

                        case 3:

                            const double k = 0.001;

                            double x1 = d * (1.0 + k * s);

                            x.Add(x1);
                            y.Add(s);      // 0.0 말고 s
                            z.Add(yy);

                            allX.Add(x1);
                            allY.Add(s);   // 0.0 말고 s
                            allZ.Add(yy);

                            break;

                        case 4:

                            x.Add(d);
                            y.Add(s);
                            z.Add(yy);

                            allX.Add(d);
                            allY.Add(s);
                            allZ.Add(yy);

                            break;
                        case 5:

                            double bmul = (s + d) / 2;
                            double smul = (s - d) / 2;
                            x.Add(bmul);
                            y.Add(smul);
                            z.Add(yy);

                            allX.Add(bmul);
                            allY.Add(smul);
                            allZ.Add(yy);

                            break;

                    }

                }



                //var r = RegressionMethod.FitLinear2D(x, y, z);
                var r = RegressionMethod.FitLinear2D_NoIntercept(x, y, z);

                sb.AppendLine(
                    $"{market},{date},{r.Count},{r.A:F8},{r.B:F8},{r.C:F8},{r.R2:F6}");
            }

            if (allX.Count >= 3)
            {
                //var all = RegressionMethod.FitLinear2D(allX, allY, allZ);
                var all = RegressionMethod.FitLinear2D_NoIntercept(allX, allY, allZ);

                sb.AppendLine(
                    $"{market},ALL,{all.Count},{all.A:F8},{all.B:F8},{all.C:F8},{all.R2:F6}");
            }

            sb.AppendLine();
        }
    }
}