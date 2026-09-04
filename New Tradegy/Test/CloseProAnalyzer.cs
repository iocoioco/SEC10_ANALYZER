using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace New_Tradegy.Test
{
    internal class CloseProAnalyzer
    {
        // ---------------------------------------------------------
        // 우리가 사용할 5개 시점
        // 초는 무시하고 HHMM 기준
        // ---------------------------------------------------------
        private static readonly int[] TargetMinutes =
        {
            1459,
            1504,
            1509,
            1514,
            1519
        };

        private class Point
        {
            public int Time;
            public int Etf;
            public int Pro;
            public int Nq;
        }
        private class AnalysisRow
        {
            public string Date;
            public string Market;
            public string Period;

            public int DEtf;
            public int DPro;
        }

        // =========================================================
        // 폴더 전체 분석
        // =========================================================
        public static void Run()
        {
            string sourceFolder = @"C:\BJS\분";
            string outputFile =
                @"C:\BJS\CloseProAnalyzerResult.txt";

            if (!Directory.Exists(sourceFolder))
                throw new DirectoryNotFoundException(sourceFolder);

            var sb = new StringBuilder();

            sb.AppendLine(
                "date,market,period," +
                "etf0,etf1,dETF," +
                "pro0,pro1,dPro," +
                "nq0,nq1,dNQ");

            // ---------------------------------------------------------
            // 분석할 ETF
            // ---------------------------------------------------------
            string[] targetStocks =
            {
        "KODEX 레버리지",
        "KODEX 코스닥150레버리지"
    };

            // ---------------------------------------------------------
            // 날짜 폴더
            // 예: C:\BJS\분\20260824
            // ---------------------------------------------------------
            string[] dateFolders =
                Directory.GetDirectories(sourceFolder)
                    .OrderBy(f => f)
                    .ToArray();

            int usedFiles = 0;
            int skippedFiles = 0;

            foreach (string dateFolder in dateFolders)
            {
                string date =
                    Path.GetFileName(dateFolder);

                // 날짜 폴더가 아니면 제외
                if (date.Length != 8 ||
                    !date.All(char.IsDigit))
                {
                    continue;
                }

                foreach (string stock in targetStocks)
                {
                    // -------------------------------------------------
                    // 확장자 없는 것처럼 보여도 실제 파일은 .txt
                    // -------------------------------------------------
                    string file =
                        Path.Combine(
                            dateFolder,
                            stock + ".txt");

                    if (!File.Exists(file))
                    {
                        skippedFiles++;
                        continue;
                    }

                    Dictionary<int, Point> points =
                        LoadTargetPoints(file);

                    // -------------------------------------------------
                    // 1459 / 1504 / 1509 / 1514 / 1519
                    // 모두 있어야 사용
                    // -------------------------------------------------
                    bool complete =
                        TargetMinutes.All(
                            m => points.ContainsKey(m));

                    if (!complete)
                    {
                        skippedFiles++;
                        continue;
                    }

                    usedFiles++;

                    string market =
                        stock.Contains("코스닥")
                            ? "KOSDAQ"
                            : "KOSPI";

                    // -------------------------------------------------
                    // 네 개 5분 구간
                    // -------------------------------------------------
                    for (int i = 1;
                         i < TargetMinutes.Length;
                         i++)
                    {
                        int m0 =
                            TargetMinutes[i - 1];

                        int m1 =
                            TargetMinutes[i];

                        Point p0 = points[m0];
                        Point p1 = points[m1];

                        int dEtf =
                            p1.Etf - p0.Etf;

                        int dPro =
                            p1.Pro - p0.Pro;

                        int dNq =
                            p1.Nq - p0.Nq;

                        string period =
                            $"{m0:D4}-{m1:D4}";

                        sb.AppendLine(
                            $"{date}," +
                            $"{market}," +
                            $"{period}," +
                            $"{p0.Etf}," +
                            $"{p1.Etf}," +
                            $"{dEtf}," +
                            $"{p0.Pro}," +
                            $"{p1.Pro}," +
                            $"{dPro}," +
                            $"{p0.Nq}," +
                            $"{p1.Nq}," +
                            $"{dNq}");
                    }
                }
            }

            File.WriteAllText(
                outputFile,
                sb.ToString(),
                Encoding.UTF8);

            Console.WriteLine("CloseProAnalyzer 완료");
            Console.WriteLine($"사용 파일 : {usedFiles:N0}");
            Console.WriteLine($"제외 파일 : {skippedFiles:N0}");
            Console.WriteLine($"결과 : {outputFile}");


            CloseProAnalyzer.AnalyzeResult(@"C:\BJS\CloseProAnalyzerResult.txt");
        }


        // =========================================================
        // 한 파일에서 5개 시점 추출
        // =========================================================
        private static Dictionary<int, Point> LoadTargetPoints(
            string file)
        {
            var result =
                new Dictionary<int, Point>();

            string[] lines =
                File.ReadAllLines(file);

            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                string[] sp =
                    line.Split(
                        new[]
                        {
                            ',',
                            '\t',
                            ' '
                        },
                        StringSplitOptions.RemoveEmptyEntries);

                // 최소한 NQ까지 존재해야 함
                if (sp.Length < 7)
                    continue;

                int time;

                if (!int.TryParse(
                        sp[0],
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out time))
                {
                    // header 등
                    continue;
                }

                // ---------------------------------------------
                // 초 제거
                //
                // 145959 / 100 = 1459
                // 145958 / 100 = 1459
                // ---------------------------------------------
                int hhmm = time / 100;

                if (!TargetMinutes.Contains(hhmm))
                    continue;

                int etf;
                int proFor;
                int foreign;
                int nq;

                if (!TryParseInt(sp[1], out etf))
                    continue;

                // Pro + Foreign
                if (!TryParseInt(sp[3], out proFor))
                    continue;

                // Foreign
                if (!TryParseInt(sp[5], out foreign))
                    continue;

                // NQ = 마지막에서 한 칸 앞
                if (!TryParseInt(
                        sp[sp.Length - 2],
                        out nq))
                {
                    continue;
                }

                int pro =
                    proFor - foreign;

                var point =
                    new Point
                    {
                        Time = time,
                        Etf = etf,
                        Pro = pro,
                        Nq = nq
                    };

                // ---------------------------------------------
                // 같은 분에 여러 데이터가 있으면
                // 가장 늦은 초 데이터 사용
                // ---------------------------------------------
                Point old;

                if (!result.TryGetValue(
                        hhmm,
                        out old) ||
                    time > old.Time)
                {
                    result[hhmm] = point;
                }
            }

            return result;
        }

        // =========================================================
        // 숫자 parsing
        // 1,234 같은 값도 처리
        // =========================================================
        private static bool TryParseInt(
            string s,
            out int value)
        {
            s = s.Trim()
                 .Replace(",", "");

            return int.TryParse(
                s,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out value);
        }

        
        public static void AnalyzeResult(
            string resultFile)
        {
            var rows =
                LoadAnalysisRows(resultFile);

            Console.WriteLine();
            Console.WriteLine(
                "========================================");
            Console.WriteLine(
                " Close Pro Analysis");
            Console.WriteLine(
                "========================================");

            AnalyzeMarket(
                rows,
                "KOSPI");

            AnalyzeMarket(
                rows,
                "KOSDAQ");
        }
        private static List<AnalysisRow> LoadAnalysisRows(
    string file)
        {
            var result =
                new List<AnalysisRow>();

            string[] lines =
                File.ReadAllLines(file);

            foreach (string line in lines.Skip(1))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                string[] sp =
                    line.Split(',');

                if (sp.Length < 12)
                    continue;

                int dEtf;
                int dPro;

                if (!int.TryParse(sp[5], out dEtf))
                    continue;

                if (!int.TryParse(sp[8], out dPro))
                    continue;

                result.Add(
                    new AnalysisRow
                    {
                        Date = sp[0],
                        Market = sp[1],
                        Period = sp[2],

                        DEtf = dEtf,
                        DPro = dPro
                    });
            }

            return result;
        }
        private static void AnalyzeMarket(
    List<AnalysisRow> allRows,
    string market)
        {
            var rows =
                allRows
                    .Where(x => x.Market == market)
                    .ToList();

            Console.WriteLine();
            Console.WriteLine(
                $"========== {market} ==========");

            Console.WriteLine(
                $"5분 표본 : {rows.Count:N0}");

            Analyze20Minute(rows);
            AnalyzePeriods(rows);
            AnalyzeProQuintile(rows);
            AnalyzeLead(rows);
        }
        private static void Analyze20Minute(
    List<AnalysisRow> rows)
        {
            var daily =
                rows
                    .GroupBy(x => x.Date)
                    .Where(g => g.Count() == 4)
                    .Select(g => new
                    {
                        Date = g.Key,

                        Pro =
                            g.Sum(x => x.DPro),

                        Etf =
                            g.Sum(x => x.DEtf)
                    })
                    .ToList();

            int same =
                daily.Count(x =>
                    Math.Sign(x.Pro) ==
                    Math.Sign(x.Etf) &&
                    x.Pro != 0 &&
                    x.Etf != 0);

            int valid =
                daily.Count(x =>
                    x.Pro != 0 &&
                    x.Etf != 0);

            double hit =
                valid > 0
                    ? same * 100.0 / valid
                    : 0;

            double corr =
                Correlation(
                    daily.Select(x => (double)x.Pro).ToList(),
                    daily.Select(x => (double)x.Etf).ToList());

            Console.WriteLine();
            Console.WriteLine("[20분 전체]");

            Console.WriteLine(
                $"날짜수        : {daily.Count}");

            Console.WriteLine(
                $"방향 일치율   : {hit:F1}%");

            Console.WriteLine(
                $"Pro/ETF 상관  : {corr:F3}");
        }
        private static void AnalyzePeriods(
    List<AnalysisRow> rows)
        {
            Console.WriteLine();
            Console.WriteLine("[5분 구간별]");

            string[] periods =
            {
        "1459-1504",
        "1504-1509",
        "1509-1514",
        "1514-1519"
    };

            foreach (string period in periods)
            {
                var r =
                    rows
                        .Where(x =>
                            x.Period == period)
                        .ToList();

                int valid =
                    r.Count(x =>
                        x.DPro != 0 &&
                        x.DEtf != 0);

                int same =
                    r.Count(x =>
                        x.DPro != 0 &&
                        x.DEtf != 0 &&
                        Math.Sign(x.DPro) ==
                        Math.Sign(x.DEtf));

                double hit =
                    valid > 0
                        ? same * 100.0 / valid
                        : 0;

                double corr =
                    Correlation(
                        r.Select(x => (double)x.DPro).ToList(),
                        r.Select(x => (double)x.DEtf).ToList());

                Console.WriteLine(
                    $"{period}  " +
                    $"일치={hit,5:F1}%   " +
                    $"Corr={corr,6:F3}");
            }
        }
        private static void AnalyzeProQuintile(
    List<AnalysisRow> rows)
        {
            Console.WriteLine();
            Console.WriteLine("[dPro 5등분]");

            var sorted =
                rows
                    .OrderBy(x => x.DPro)
                    .ToList();

            int n =
                sorted.Count;

            for (int q = 0;
                 q < 5;
                 q++)
            {
                int start =
                    n * q / 5;

                int end =
                    n * (q + 1) / 5;

                var group =
                    sorted
                        .Skip(start)
                        .Take(end - start)
                        .ToList();

                if (group.Count == 0)
                    continue;

                double avgPro =
                    group.Average(x => x.DPro);

                double avgEtf =
                    group.Average(x => x.DEtf);

                double upPct =
                    group.Count(x => x.DEtf > 0) *
                    100.0 /
                    group.Count;

                Console.WriteLine(
                    $"Q{q + 1}  " +
                    $"Pro={avgPro,8:F1}   " +
                    $"ETF={avgEtf,7:F2}   " +
                    $"ETF↑={upPct,5:F1}%");
            }
        }
        private static void AnalyzeLead(
    List<AnalysisRow> rows)
        {
            Console.WriteLine();
            Console.WriteLine("[Pro → 다음 5분 ETF]");

            string[] periods =
            {
        "1459-1504",
        "1504-1509",
        "1509-1514",
        "1514-1519"
    };

            var byDate =
                rows
                    .GroupBy(x => x.Date);

            for (int p = 0;
                 p < 3;
                 p++)
            {
                var proList =
                    new List<double>();

                var etfNextList =
                    new List<double>();

                int same = 0;
                int valid = 0;

                foreach (var day in byDate)
                {
                    var now =
                        day.FirstOrDefault(
                            x =>
                            x.Period ==
                            periods[p]);

                    var next =
                        day.FirstOrDefault(
                            x =>
                            x.Period ==
                            periods[p + 1]);

                    if (now == null ||
                        next == null)
                    {
                        continue;
                    }

                    proList.Add(
                        now.DPro);

                    etfNextList.Add(
                        next.DEtf);

                    if (now.DPro != 0 &&
                        next.DEtf != 0)
                    {
                        valid++;

                        if (Math.Sign(now.DPro) ==
                            Math.Sign(next.DEtf))
                        {
                            same++;
                        }
                    }
                }

                double hit =
                    valid > 0
                        ? same * 100.0 / valid
                        : 0;

                double corr =
                    Correlation(
                        proList,
                        etfNextList);

                Console.WriteLine(
                    $"{periods[p]} Pro" +
                    $" → {periods[p + 1]} ETF   " +
                    $"일치={hit,5:F1}%   " +
                    $"Corr={corr,6:F3}");
            }
        }
        private static double Correlation(
    List<double> x,
    List<double> y)
        {
            if (x == null ||
                y == null ||
                x.Count != y.Count ||
                x.Count < 2)
            {
                return 0;
            }

            double mx =
                x.Average();

            double my =
                y.Average();

            double sxy = 0;
            double sx2 = 0;
            double sy2 = 0;

            for (int i = 0;
                 i < x.Count;
                 i++)
            {
                double dx =
                    x[i] - mx;

                double dy =
                    y[i] - my;

                sxy +=
                    dx * dy;

                sx2 +=
                    dx * dx;

                sy2 +=
                    dy * dy;
            }

            if (sx2 <= 0 ||
                sy2 <= 0)
            {
                return 0;
            }

            return
                sxy /
                Math.Sqrt(
                    sx2 * sy2);
        }
    }
}