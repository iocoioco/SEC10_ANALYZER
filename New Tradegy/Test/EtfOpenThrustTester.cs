using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

public static class EtfOpenThrustTester
{
    static readonly string RootDir = @"C:\BJS\분";

    static readonly string StartDate = "20240320";
    static readonly string EndDate = "20260522";

    static readonly double ThrustMin = 1.0;
    static readonly double ThrustMax = 10000.0;

    static readonly string[] Etfs =
    {
        "KODEX 레버리지",
        "KODEX 코스닥150레버리지"
    };

    // C:\BJS\data work\ETF_OpenThrustTest.txt

    // 85959 = 09:00:00~09:00:59
    // 90059 = 09:01:00 기준
    static readonly int[] NeedTimes =
    {
        85959, 90059, 90159, 90259, 90359, 90459, 90559
    };

    public static void Run()
    {
        var events = new List<EventResult>();

        foreach (var dir in Directory.GetDirectories(RootDir))
        {
            string date = Path.GetFileName(dir);
            if (date.CompareTo(StartDate) < 0 || date.CompareTo(EndDate) > 0)
                continue;

            foreach (string etf in Etfs)
            {
                string file = Directory.GetFiles(dir)
                    .FirstOrDefault(f => Path.GetFileName(f).Contains(etf));

                if (file == null) continue;

                var rows = LoadPriceRows(file);

                if (!NeedTimes.All(t => rows.ContainsKey(t)))
                    continue;

                double p0 = rows[85959];
                double p1 = rows[90059];

                if (p0 <= 0) continue;

                double thrust = Rate(p1, p0);

                if (thrust < ThrustMin || thrust >= ThrustMax)
                    continue;

                var r = new EventResult
                {
                    Date = date,
                    Etf = etf,
                    Thrust = thrust,
                    Fwds = new double[5]
                };

                for (int i = 0; i < 5; i++)
                {
                    int t = NeedTimes[i + 2]; // 90159 ~ 90559
                    r.Fwds[i] = Rate(rows[t], p1);
                }

                r.Mfe5 = r.Fwds.Max();
                r.Mae5 = r.Fwds.Min();

                events.Add(r);
            }
        }

        Print(events);
    }

    static Dictionary<int, double> LoadPriceRows(string file)
    {
        var dict = new Dictionary<int, double>();

        foreach (var line in File.ReadLines(file, Encoding.Default))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var parts = line.Split(new[] { ',', '\t', ' ' },
                StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 2) continue;

            if (!int.TryParse(parts[0], out int time)) continue;

            if (!double.TryParse(parts[1], NumberStyles.Any,
                CultureInfo.InvariantCulture, out double price))
            {
                double.TryParse(parts[1], NumberStyles.Any,
                    CultureInfo.CurrentCulture, out price);
            }

            if (price <= 0) continue;

            dict[time] = price;
        }

        return dict;
    }

    static double Rate(double now, double basis)
    {
        return (now - basis) / 100.0;
    }

    static void Print(List<EventResult> events)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"ETF OPEN THRUST TEST");
        sb.AppendLine($"Period    : {StartDate} ~ {EndDate}");
        sb.AppendLine($"Condition : {ThrustMin:0.##}% <= 85959→90059 < {ThrustMax:0.##}%");
        sb.AppendLine();

        sb.AppendLine("DATE      ETF                    THRUST    +1M     +2M     +3M     +4M     +5M     MFE5    MAE5");
        sb.AppendLine(new string('-', 105));

        foreach (var e in events.OrderBy(x => x.Date).ThenBy(x => x.Etf))
        {
            sb.AppendLine(
                $"{e.Date}  {ShortName(e.Etf),-20} " +
                $"{e.Thrust,7:0.00} " +
                $"{e.Fwds[0],7:0.00} {e.Fwds[1],7:0.00} {e.Fwds[2],7:0.00} " +
                $"{e.Fwds[3],7:0.00} {e.Fwds[4],7:0.00} " +
                $"{e.Mfe5,7:0.00} {e.Mae5,7:0.00}");
        }

        sb.AppendLine();

        foreach (var g in events.GroupBy(x => x.Etf))
        {
            sb.AppendLine();
            sb.AppendLine($"[{g.Key}]");
            sb.AppendLine($"N = {g.Count()}");
            sb.AppendLine("        AVG      STD      WIN%");
            sb.AppendLine("------------------------------");

            for (int i = 0; i < 5; i++)
            {
                var arr = g.Select(x => x.Fwds[i]).ToList();
                sb.AppendLine($"+{i + 1}M   {Avg(arr),7:0.00}  {Std(arr),7:0.00}  {Win(arr),7:0.0}");
            }

            sb.AppendLine();
            sb.AppendLine($"STOP -1% HIT   : {HitRate(g, x => x.Mae5 <= -1.0):0.0}%");
            sb.AppendLine($"TARGET +2% HIT : {HitRate(g, x => x.Mfe5 >= 2.0):0.0}%");
            sb.AppendLine($"TARGET +3% HIT : {HitRate(g, x => x.Mfe5 >= 3.0):0.0}%");
        }

        string text = sb.ToString();

        Console.WriteLine(text);

        string outFile = @"C:\BJS\data work\ETF_OpenThrustTest.txt";
        Directory.CreateDirectory(Path.GetDirectoryName(outFile));
        File.WriteAllText(outFile, text, Encoding.UTF8);
    }

    static string ShortName(string etf)
    {
        if (etf.Contains("코스닥")) return "KQLEV";
        return "KSLEV";
    }

    static double Avg(List<double> xs)
    {
        return xs.Count == 0 ? 0 : xs.Average();
    }

    static double Std(List<double> xs)
    {
        if (xs.Count <= 1) return 0;
        double avg = xs.Average();
        return Math.Sqrt(xs.Sum(x => (x - avg) * (x - avg)) / (xs.Count - 1));
    }

    static double Win(List<double> xs)
    {
        return xs.Count == 0 ? 0 : xs.Count(x => x > 0) * 100.0 / xs.Count;
    }

    static double HitRate(IEnumerable<EventResult> xs, Func<EventResult, bool> pred)
    {
        var list = xs.ToList();
        if (list.Count == 0) return 0;
        return list.Count(pred) * 100.0 / list.Count;
    }

    class EventResult
    {
        public string Date;
        public string Etf;
        public double Thrust;
        public double[] Fwds;
        public double Mfe5;
        public double Mae5;
    }
}