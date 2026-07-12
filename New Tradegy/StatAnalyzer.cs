using System;
using System.IO;
using System.Text;
using System.Globalization;
using System.Collections.Generic;

namespace New_Tradegy.Library.Listeners
{

    internal class StatBucket
    {
        public int Count;
        public int Win10;
        public int Win30;
        public int Win60;

        public double Sum10;
        public double Sum30;
        public double Sum60;

        public double SumSq10;
        public double SumSq30;
        public double SumSq60;

        public void Add(double y10, double y30, double y60)
        {
            Count++;

            Sum10 += y10;
            Sum30 += y30;
            Sum60 += y60;

            SumSq10 += y10 * y10;
            SumSq30 += y30 * y30;
            SumSq60 += y60 * y60;

            if (y10 > 0) Win10++;
            if (y30 > 0) Win30++;
            if (y60 > 0) Win60++;
        }
    }

    public static class StatAnalyzer
    {
        private const string Root = @"C:\BJS\Study\지수10초";

        public static void CreateStatFilesAllDates()
        {
            CreateOneMarketStat("KOSPI_ANALYZED_V1.txt", "KOSPI_STAT_V1.txt");
            CreateOneMarketStat("KOSDAQ_ANALYZED_V1.txt", "KOSDAQ_STAT_V1.txt");
        }

        private static void CreateOneMarketStat(string inputFileName, string outputFileName)
        {
            var stats = new Dictionary<string, Dictionary<string, StatBucket>>();

            string[] variables =
            {
                "H1","H25","H5",
                "HZ1","HZ25","HZ5",
                "A1","A25","A5",
                "AZ1","AZ25","AZ5",
                "M10Diff","M10Sum",
                "M20Diff","M20Sum",
                "M30Diff","M30Sum"
            };

            foreach (string dir in Directory.GetDirectories(Root))
            {
                string file = Path.Combine(dir, inputFileName);

                if (!File.Exists(file))
                    continue;

                // 다음 단계: 파일 읽고 통계 누적
                var lines = File.ReadAllLines(file);

                if (lines.Length < 8)
                    continue;

                var rows = new List<double[]>();

                for (int i = 1; i < lines.Length; i++) // header skip
                {
                    string line = lines[i].Trim();
                    if (string.IsNullOrEmpty(line))
                        continue;

                    string[] p = line.Split(',');
                    if (p.Length < 25)
                        continue;

                    double[] v = new double[p.Length];

                    bool ok = true;
                    for (int j = 0; j < p.Length; j++)
                    {
                        if (!double.TryParse(p[j], NumberStyles.Any, CultureInfo.InvariantCulture, out v[j]))
                        {
                            ok = false;
                            break;
                        }
                    }

                    if (ok)
                        rows.Add(v);
                }

                for (int i = 0; i + 6 < rows.Count; i++)
                {
                    double etfNow = rows[i][2];

                    double y10 = rows[i + 1][2] - etfNow;
                    double y30 = rows[i + 3][2] - etfNow;
                    double y60 = rows[i + 6][2] - etfNow;

                    // 다음 단계에서 변수별 bucket에 Add
                    for (int c = 0; c < variables.Length; c++)
                    {
                        string varName = variables[c];

                        int col = GetColumnIndex(varName);
                        if (col < 0 || col >= rows[i].Length)
                            continue;

                        double x = rows[i][col];
                        string bucket = GetBucketName(varName, x);

                        GetBucket(stats, varName, bucket).Add(y10, y30, y60);
                    }

                }
            }

            string outPath = Path.Combine(Root, outputFileName);

            // 다음 단계: 결과 저장
            File.WriteAllText(outPath, "준비됨", Encoding.UTF8);

            var sb = new StringBuilder();

            sb.AppendLine(
                "Variable,Bucket,Count," +
                "Mean10,Std10,Win10," +
                "Mean30,Std30,Win30," +
                "Mean60,Std60,Win60");

            foreach (var v in stats)
            {
                foreach (var b in v.Value)
                {
                    StatBucket s = b.Value;

                    if (s.Count < 5)
                        continue;

                    double mean10 = s.Sum10 / s.Count;
                    double mean30 = s.Sum30 / s.Count;
                    double mean60 = s.Sum60 / s.Count;

                    double std10 = Math.Sqrt(Math.Max(0,
                        s.SumSq10 / s.Count - mean10 * mean10));

                    double std30 = Math.Sqrt(Math.Max(0,
                        s.SumSq30 / s.Count - mean30 * mean30));

                    double std60 = Math.Sqrt(Math.Max(0,
                        s.SumSq60 / s.Count - mean60 * mean60));

                    sb.AppendLine(string.Join(",",
                        v.Key,
                        b.Key,
                        s.Count,

                        mean10,
                        std10,
                        (double)s.Win10 / s.Count,

                        mean30,
                        std30,
                        (double)s.Win30 / s.Count,

                        mean60,
                        std60,
                        (double)s.Win60 / s.Count));
                }
            }

            File.WriteAllText(outPath, sb.ToString(), Encoding.UTF8);
        }
        private static int GetColumnIndex(string name)
        {
            switch (name)
            {
                case "H1": return 3;
                case "H25": return 4;
                case "H5": return 5;
                case "HZ1": return 6;
                case "HZ25": return 7;
                case "HZ5": return 8;
                case "A1": return 9;
                case "A25": return 10;
                case "A5": return 11;
                case "AZ1": return 12;
                case "AZ25": return 13;
                case "AZ5": return 14;
                case "M10Diff": return 15;
                case "M10Sum": return 16;
                case "M20Diff": return 17;
                case "M20Sum": return 18;
                case "M30Diff": return 19;
                case "M30Sum": return 20;
                default: return -1;
            }
        }

        private static StatBucket GetBucket(
    Dictionary<string, Dictionary<string, StatBucket>> stats,
    string variable,
    string bucket)
        {
            if (!stats.ContainsKey(variable))
                stats[variable] = new Dictionary<string, StatBucket>();

            if (!stats[variable].ContainsKey(bucket))
                stats[variable][bucket] = new StatBucket();

            return stats[variable][bucket];
        }


        private static string GetBucketName(string varName, double x)
        {
            if (varName.Contains("Sum"))
                return BucketByStep(x, 0, 100, 10);

            if (varName.Contains("Diff"))
                return BucketByStep(x, -100, 100, 10);

            return BucketByStep(x, -5, 5, 0.5);
        }



        private static string BucketByStep(
        double value,
        double min,
        double max,
        double step)
        {
            if (value < min)
                return $"<{min}";

            if (value >= max)
                return $">={max}";

            double b = Math.Floor((value - min) / step) * step + min;
            double e = b + step;

            return $"{b:0.##}~{e:0.##}";
        }
    }
}



