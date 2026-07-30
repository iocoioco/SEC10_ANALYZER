using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace New_Tradegy.Test
{


   
    public static class NqEtfNoiseAnalyzer
    {
        private const string RootPath =
            @"C:\BJS\Study\지수10초";

        private const string OutputPath =
            @"C:\BJS\Study\지수10초\NQ_ETF_NOISE_RAW_KOSPI.txt";

        private const string TargetFileName =
            "KODEX 레버리지.txt";


        private sealed class Sec10Row
        {
            public int Time;
            public int Etf;
            public int Nq;
        }
        private sealed class RawRow
        {
            public string Date;
            public int Time;

            public int DEtf;
            public int DNq;
            public int AbsDNq;

            public int Direction;
            public int Match;
        }

        private sealed class BucketStat
        {
            public string Name;
            public int MinInclusive;
            public int MaxExclusive;

            public readonly List<double> DirectionalEtfValues =
                new List<double>();

            public readonly List<double> ResponseValues =
                new List<double>();

            public int Count;
            public int MatchCount;
            public int OppositeCount;
            public int ZeroCount;

        }
            public static void Run()
        {
            const string kospiRawPath =
                @"C:\BJS\Study\지수10초\NQ_ETF_NOISE_RAW_KOSPI.txt";

            const string kospiStatPath =
                @"C:\BJS\Study\지수10초\NQ_ETF_NOISE_STAT_KOSPI.txt";

            const string kosdaqRawPath =
                @"C:\BJS\Study\지수10초\NQ_ETF_NOISE_RAW_KOSDAQ.txt";

            const string kosdaqStatPath =
                @"C:\BJS\Study\지수10초\NQ_ETF_NOISE_STAT_KOSDAQ.txt";

            RunOne(
                "KODEX 레버리지.txt",
                kospiRawPath);

            AnalyzeRaw(
                kospiRawPath,
                kospiStatPath);

            RunOne(
                "KODEX 코스닥150레버리지.txt",
                kosdaqRawPath);

            AnalyzeRaw(
                kosdaqRawPath,
                kosdaqStatPath);
        }
        private static void AnalyzeRaw(
    string rawPath,
    string outputPath)
        {
            if (!File.Exists(rawPath))
                return;

            List<RawRow> rows = LoadRawRows(rawPath);

            if (rows.Count == 0)
                return;

            List<BucketStat> buckets =
                CreateBuckets();

            foreach (RawRow row in rows)
            {
                BucketStat bucket =
                    FindBucket(buckets, row.AbsDNq);

                if (bucket == null)
                    continue;

                bucket.Count++;

                if (row.Match > 0)
                    bucket.MatchCount++;
                else if (row.Match < 0)
                    bucket.OppositeCount++;
                else
                    bucket.ZeroCount++;

                double directionalEtf =
                    row.Direction * row.DEtf;

                bucket.DirectionalEtfValues.Add(
                    directionalEtf);

                /*
                 * dNq가 0이면 Response 계산 불가.
                 *
                 * 현재 저장 단위:
                 * ETF = 실제 % × 100
                 * NQ  = 실제 % × 1000
                 *
                 * 실제 반응배수:
                 *
                 * (dEtf / 100.0) / (dNq / 1000.0)
                 * = dEtf * 10.0 / dNq
                 */
                if (row.DNq != 0)
                {
                    double response =
                        row.DEtf * 10.0 / row.DNq;

                    bucket.ResponseValues.Add(
                        response);
                }
            }

            using (var writer = new StreamWriter(
                outputPath,
                false,
                new UTF8Encoding(true)))
            {
                writer.WriteLine(
                    "nqRange,count,match,opposite,zero," +
                    "matchRate,directionalEtfMean," +
                    "directionalEtfStd,directionalEtfMedian," +
                    "responseMean,responseStd,responseMedian");

                foreach (BucketStat bucket in buckets)
                {
                    double matchRate =
                        bucket.Count > 0
                            ? bucket.MatchCount * 100.0 /
                              bucket.Count
                            : 0.0;

                    double directionalMean =
                        Mean(bucket.DirectionalEtfValues);

                    double directionalStd =
                        Std(bucket.DirectionalEtfValues);

                    double directionalMedian =
                        Median(bucket.DirectionalEtfValues);

                    double responseMean =
                        Mean(bucket.ResponseValues);

                    double responseStd =
                        Std(bucket.ResponseValues);

                    double responseMedian =
                        Median(bucket.ResponseValues);

                    writer.WriteLine(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "{0},{1},{2},{3},{4}," +
                            "{5:F2},{6:F4},{7:F4},{8:F4}," +
                            "{9:F4},{10:F4},{11:F4}",
                            bucket.Name,
                            bucket.Count,
                            bucket.MatchCount,
                            bucket.OppositeCount,
                            bucket.ZeroCount,
                            matchRate,
                            directionalMean,
                            directionalStd,
                            directionalMedian,
                            responseMean,
                            responseStd,
                            responseMedian));
                }
            }
        }
        private static void RunOne(
     string fileName,
     string outputPath)
        {
            string root =
                @"C:\BJS\Study\지수10초";

            List<string> dateFolders =
                Directory.GetDirectories(root)
                    .OrderBy(x => x)
                    .ToList();

            int rowCount = 0;
            int skippedGapCount = 0;

            using (var writer = new StreamWriter(
                outputPath,
                false,
                new UTF8Encoding(true)))
            {
                writer.WriteLine(
                    "date,time,etf,nq,dEtf,dNq,absDNq,direction,match");

                foreach (string folder in dateFolders)
                {
                    string date =
                        Path.GetFileName(folder);

                    string path =
                        Path.Combine(folder, fileName);

                    if (!File.Exists(path))
                        continue;

                    List<Sec10Row> rows =
                        LoadRows(path);

                    if (rows.Count < 2)
                        continue;

                    for (int i = 1; i < rows.Count; i++)
                    {
                        Sec10Row prev = rows[i - 1];
                        Sec10Row curr = rows[i];

                        //--------------------------------------------------
                        // 시간 검사
                        //--------------------------------------------------
                        int prevSeconds =
                            ToSeconds(prev.Time);

                        int currSeconds =
                            ToSeconds(curr.Time);

                        if (prevSeconds < 0 ||
                            currSeconds < 0)
                        {
                            skippedGapCount++;
                            continue;
                        }

                        //--------------------------------------------------
                        // 시초 1분 제외
                        //--------------------------------------------------
                        if (currSeconds < 9 * 3600 + 60)
                            continue;

                        //--------------------------------------------------
                        // 10초 연속 데이터만 사용
                        //--------------------------------------------------
                        int gapSeconds =
                            currSeconds - prevSeconds;

                        if (gapSeconds < 8 ||
                            gapSeconds > 12)
                        {
                            skippedGapCount++;
                            continue;
                        }

                        //--------------------------------------------------
                        // 변화량 계산
                        //--------------------------------------------------
                        int dEtf =
                            curr.Etf - prev.Etf;

                        int dNq =
                            curr.Nq - prev.Nq;

                        //--------------------------------------------------
                        // NQ 변화 없으면 제외
                        //--------------------------------------------------
                        if (dNq == 0)
                            continue;

                        int absDNq =
                            Math.Abs(dNq);

                        int direction =
                            Math.Sign(dNq);

                        int match;

                        if (dEtf == 0)
                        {
                            match = 0;
                        }
                        else
                        {
                            match =
                                Math.Sign(dEtf) == direction
                                    ? 1
                                    : -1;
                        }

                        writer.WriteLine(
                            string.Format(
                                CultureInfo.InvariantCulture,
                                "{0},{1},{2},{3},{4},{5},{6},{7},{8}",
                                date,
                                curr.Time,
                                curr.Etf,
                                curr.Nq,
                                dEtf,
                                dNq,
                                absDNq,
                                direction,
                                match));

                        rowCount++;
                    }
                }
            }

            Console.WriteLine(
                $"{fileName}");

            Console.WriteLine(
                $"Rows : {rowCount:N0}");

            Console.WriteLine(
                $"Skipped Gap : {skippedGapCount:N0}");
        }

        private static bool IsDateDirectory(string directoryPath)
        {
            string name = Path.GetFileName(directoryPath);

            return
                name.Length == 8 &&
                name.All(char.IsDigit);
        }

        private static List<Sec10Row> LoadRows(string filePath)
        {
            var rows = new List<Sec10Row>();

            foreach (string rawLine in File.ReadLines(filePath))
            {
                if (string.IsNullOrWhiteSpace(rawLine))
                    continue;

                string line = rawLine.Trim();

                // 헤더 제외
                if (line.StartsWith(
                    "time",
                    StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string[] parts = line.Split(',');

                // time,etf,nq,pro,for,inst,indi,diff,sum
                if (parts.Length < 3)
                    continue;

                int time;
                int etf;
                int nq;

                if (!int.TryParse(
                    parts[0].Trim(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out time))
                {
                    continue;
                }

                if (!int.TryParse(
                    parts[1].Trim(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out etf))
                {
                    continue;
                }

                if (!int.TryParse(
                    parts[2].Trim(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out nq))
                {
                    continue;
                }

                rows.Add(
                    new Sec10Row
                    {
                        Time = time,
                        Etf = etf,
                        Nq = nq
                    });
            }

            return rows
                .OrderBy(x => x.Time)
                .ToList();
        }

        private static double GetElapsedSeconds(
            int previousTime,
            int currentTime)
        {
            TimeSpan previous =
                ConvertTimeToTimeSpan(previousTime);

            TimeSpan current =
                ConvertTimeToTimeSpan(currentTime);

            return (current - previous).TotalSeconds;
        }

        private static TimeSpan ConvertTimeToTimeSpan(int time)
        {
            // 형식:
            // HHmmssfff
            //
            // 예:
            // 90057076
            // 09:00:57.076

            int millisecond = time % 1000;
            int hhmmss = time / 1000;

            int second = hhmmss % 100;
            int minute = (hhmmss / 100) % 100;
            int hour = hhmmss / 10000;

            return new TimeSpan(
                0,
                hour,
                minute,
                second,
                millisecond);
        }






        private static int ToSeconds(int hhmmss)
        {
            int hour = hhmmss / 10000;
            int minute = (hhmmss / 100) % 100;
            int second = hhmmss % 100;

            if (hour < 0 || hour > 23)
                return -1;

            if (minute < 0 || minute > 59)
                return -1;

            if (second < 0 || second > 59)
                return -1;

            return hour * 3600 + minute * 60 + second;
        }








        private static List<RawRow> LoadRawRows(
    string rawPath)
        {
            var rows = new List<RawRow>();

            foreach (string rawLine in
                File.ReadLines(rawPath))
            {
                if (string.IsNullOrWhiteSpace(rawLine))
                    continue;

                string line = rawLine.Trim();

                if (line.StartsWith(
                    "date",
                    StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string[] parts = line.Split(',');

                /*
                 * date,time,etf,nq,
                 * dEtf,dNq,absDNq,direction,match
                 */
                if (parts.Length < 9)
                    continue;

                int time;
                int dEtf;
                int dNq;
                int absDNq;
                int direction;
                int match;

                if (!int.TryParse(
                    parts[1].Trim(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out time))
                {
                    continue;
                }

                if (!int.TryParse(
                    parts[4].Trim(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out dEtf))
                {
                    continue;
                }

                if (!int.TryParse(
                    parts[5].Trim(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out dNq))
                {
                    continue;
                }

                if (!int.TryParse(
                    parts[6].Trim(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out absDNq))
                {
                    continue;
                }

                if (!int.TryParse(
                    parts[7].Trim(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out direction))
                {
                    continue;
                }

                if (!int.TryParse(
                    parts[8].Trim(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out match))
                {
                    continue;
                }

                rows.Add(
                    new RawRow
                    {
                        Date = parts[0].Trim(),
                        Time = time,
                        DEtf = dEtf,
                        DNq = dNq,
                        AbsDNq = absDNq,
                        Direction = direction,
                        Match = match
                    });
            }

            return rows;
        }



        private static List<BucketStat> CreateBuckets()
        {
            return new List<BucketStat>
    {
        CreateBucket("0-4",   0,   5),
        CreateBucket("5-9",   5,  10),
        CreateBucket("10-14", 10, 15),
        CreateBucket("15-19", 15, 20),
        CreateBucket("20-24", 20, 25),
        CreateBucket("25-29", 25, 30),
        CreateBucket("30-39", 30, 40),
        CreateBucket("40-49", 40, 50),
        CreateBucket("50+",   50, int.MaxValue)
    };
        }

        private static BucketStat CreateBucket(
            string name,
            int minInclusive,
            int maxExclusive)
        {
            return new BucketStat
            {
                Name = name,
                MinInclusive = minInclusive,
                MaxExclusive = maxExclusive
            };
        }

        private static BucketStat FindBucket(
            List<BucketStat> buckets,
            int absDNq)
        {
            foreach (BucketStat bucket in buckets)
            {
                if (absDNq >= bucket.MinInclusive &&
                    absDNq < bucket.MaxExclusive)
                {
                    return bucket;
                }
            }

            return null;
        }



        private static double Mean(
    List<double> values)
        {
            if (values == null ||
                values.Count == 0)
            {
                return 0.0;
            }

            return values.Average();
        }

        private static double Std(
            List<double> values)
        {
            if (values == null ||
                values.Count < 2)
            {
                return 0.0;
            }

            double mean = values.Average();

            double variance =
                values.Sum(
                    x => (x - mean) * (x - mean))
                / values.Count;

            return Math.Sqrt(variance);
        }

        private static double Median(
            List<double> values)
        {
            if (values == null ||
                values.Count == 0)
            {
                return 0.0;
            }

            double[] sorted =
                values
                    .OrderBy(x => x)
                    .ToArray();

            int middle = sorted.Length / 2;

            if (sorted.Length % 2 == 1)
                return sorted[middle];

            return
                (sorted[middle - 1] +
                 sorted[middle]) / 2.0;
        }
    }
}