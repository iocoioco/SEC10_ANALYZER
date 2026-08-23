using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace New_Tradegy.Test
{
    class ResearchRow
    {
        public int Date;

        public double ResetNQ;

        // 한국장 당일 NQ
        public double NQ0900Pct;
        public double NQ1530Pct;

        // 한국장 당일 ETF
        public double ETFOpenPct;
        public double ETFClosePct;

        // 한국장 종료 후 미국장 마감
        public double USNQClosePct;

        // 다음 한국 거래일 시초
        public double NextETFOpenPct;
    }

    class EtfDay
    {
        public int Date;
        public double Open;
        public double Close;
    }

    class BucketResult
    {
        public string Name;

        public int Count;

        public double Avg;
        public double Median;
        public double Std;

        public double UpPct;
    }
    internal class NQ_ETF_DataBento
    {
        public static void update()
        {
            string nqFile =
                @"C:\BJS\DataBento\NQ.txt";

            string resetFile =
                @"C:\BJS\DataBento\NQ_Reset.txt";

            string etfFile =
                @"C:\BJS\data work\일\KODEX 레버리지.txt";

            var nq =
                LoadNQ(nqFile);

            var reset =
                LoadResetNQ(resetFile);

            var etf =
                LoadETF(etfFile);

            var rows =
                BuildResearch(
                    nq,
                    reset,
                    etf);

            string outputFile = @"C:\BJS\DataBento\NQ_ETF_Research.txt";

            SaveResearch(
                rows,
                outputFile);

            PrintUSNQBucketStats(rows); // output widnow Nq 구간별 다음날 etf 시가

            PrintNQ1530CrashDays(rows); // output window Nq 박살난 날 다음 날 etf 시가

            PrintETFCrashDays(rows); // 한국장 박살난 다음 날 etf 시가

            Console.WriteLine();
            Console.WriteLine("Rows = " + rows.Count);

            Console.ReadLine();
        }
        static List<ResearchRow> BuildResearch(
             Dictionary<int, (double nq0900, double nq1530)> nq,
             SortedDictionary<int, double> reset,
             List<EtfDay> etf)
        {
            var result =
                new List<ResearchRow>();

            // -------------------------------------------------
            // 한국 ETF 일봉이 기준이다.
            //
            // i - 1 : 직전 한국 거래일
            // i     : 당일 한국 거래일
            // i + 1 : 다음 한국 거래일
            // -------------------------------------------------
            for (int i = 1;
                 i < etf.Count - 1;
                 i++)
            {
                EtfDay prev =
                    etf[i - 1];

                EtfDay today =
                    etf[i];

                EtfDay next =
                    etf[i + 1];

                int date =
                    today.Date;


                // =============================================
                // 1. ETF
                //
                // 일봉 자체가 한국 거래일 순서이므로
                // 휴일 계산 필요 없음
                // =============================================

                if (prev.Close <= 0 ||
                    today.Open <= 0 ||
                    today.Close <= 0 ||
                    next.Open <= 0)
                {
                    continue;
                }


                double etfOpenPct =
                    (today.Open / prev.Close - 1.0)
                    * 100.0;


                double etfClosePct =
                    (today.Close / prev.Close - 1.0)
                    * 100.0;


                double nextETFOpenPct =
                    (next.Open / today.Close - 1.0)
                    * 100.0;


                // =============================================
                // 2. 당일 NQ 09:00 / 15:30
                // =============================================

                if (!nq.TryGetValue(
                        date,
                        out var nqToday))
                {
                    continue;
                }


                // =============================================
                // 3. Reset NQ
                //
                // 한국 날짜 D부터 과거 방향으로 검색
                //
                // D에 있으면 D
                // 없으면 D 이전의 가장 최근 Reset
                // =============================================

                double resetNQ = 0.0;

                foreach (var r in reset.Reverse())
                {
                    if (r.Key <= date)
                    {
                        resetNQ = r.Value;
                        break;
                    }
                }

                if (resetNQ <= 0)
                    continue;


                // =============================================
                // 4. 한국장 NQ %
                // =============================================

                double nq0900Pct =
                    (nqToday.nq0900 / resetNQ - 1.0)
                    * 100.0;


                double nq1530Pct =
                    (nqToday.nq1530 / resetNQ - 1.0)
                    * 100.0;


                // =============================================
                // 5. 미국장 마감 NQ
                //
                // 현재 Reset 이후에 존재하는
                // 첫 번째 Reset을 사용
                //
                // 미국 휴일 / 주말은 Reset 파일 자체에서
                // 빠져 있으므로 자동 처리
                // =============================================

                //KeyValuePair<int, double> nextReset =
                //    reset.FirstOrDefault(
                //        x => x.Key > date);

                double nextResetNQ = 0.0;

                foreach (var r in reset.Reverse())
                {
                    if (r.Key <= next.Date)
                    {
                        nextResetNQ = r.Value;
                        break;
                    }
                }

                if (nextResetNQ <= 0)
                {
                    continue;
                }

                // 현재 Reset을 0% 기준으로
                // 다음 한국 거래일 직전 미국장 마감까지 변화율
                double usNQClosePct =
                    (nextResetNQ / resetNQ - 1.0)
                    * 100.0;


                // =============================================
                // 6. 결과
                // =============================================

                result.Add(
                    new ResearchRow
                    {
                        Date = date,

                        ResetNQ = resetNQ,

                        NQ0900Pct = nq0900Pct,
                        NQ1530Pct = nq1530Pct,

                        ETFOpenPct = etfOpenPct,
                        ETFClosePct = etfClosePct,

                        USNQClosePct = usNQClosePct,

                        NextETFOpenPct = nextETFOpenPct
                    });
            }

            return result;
        }
        static Dictionary<int, (double nq0900, double nq1530)>
        LoadNQ(string file)
        {
            var result =
                new Dictionary<int, (double nq0900, double nq1530)>();

            var temp =
                new Dictionary<int, double[]>();

            foreach (string line in File.ReadLines(file).Skip(1))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                string[] sp = line.Split(',');

                if (sp.Length < 3)
                    continue;

                if (!int.TryParse(sp[0], out int date))
                    continue;

                if (!int.TryParse(sp[1], out int time))
                    continue;

                if (!double.TryParse(
                        sp[2],
                        NumberStyles.Any,
                        CultureInfo.InvariantCulture,
                        out double nq))
                    continue;

                if (time != 900 &&
                    time != 1530)
                    continue;

                if (!temp.TryGetValue(date, out double[] v))
                {
                    v = new double[2];
                    temp[date] = v;
                }

                if (time == 900)
                    v[0] = nq;

                if (time == 1530)
                    v[1] = nq;
            }

            foreach (var kv in temp)
            {
                if (kv.Value[0] > 0 &&
                    kv.Value[1] > 0)
                {
                    result[kv.Key] =
                        (kv.Value[0], kv.Value[1]);
                }
            }

            return result;
        }

        static SortedDictionary<int, double>

            LoadResetNQ(string file)
        {
            var result =
                new SortedDictionary<int, double>();

            foreach (string line in File.ReadLines(file).Skip(1))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                string[] sp = line.Split(',');

                if (sp.Length < 2)
                    continue;

                if (!int.TryParse(sp[0], out int date))
                    continue;

                if (!double.TryParse(
                        sp[1],
                        NumberStyles.Any,
                        CultureInfo.InvariantCulture,
                        out double nq))
                    continue;

                result[date] = nq;
            }

            return result;
        }
        static Dictionary<int, Dictionary<int, double>>


        LoadNQMinutes(string file)
        {
            var result =
                new Dictionary<int, Dictionary<int, double>>();

            foreach (string line in File.ReadLines(file).Skip(1))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                string[] sp = line.Split(',');

                if (sp.Length < 3)
                    continue;

                if (!int.TryParse(sp[0], out int date))
                    continue;

                if (!int.TryParse(sp[1], out int time))
                    continue;

                if (!double.TryParse(
                        sp[2],
                        NumberStyles.Any,
                        CultureInfo.InvariantCulture,
                        out double nq))
                    continue;

                if (!result.TryGetValue(
                        date,
                        out Dictionary<int, double> day))
                {
                    day = new Dictionary<int, double>();
                    result[date] = day;
                }

                day[time] = nq;
            }

            return result;
        }
        // ---------------------------------------------------------
        // 분 파일 시간 -> DataBento에서 찾아야 할 다음 분
        //
        // 90059  -> 901
        // 90159  -> 902
        // 95959  -> 1000
        // 152959 -> 1530
        // ---------------------------------------------------------
        private static int GetNextMinute(int time)
        {
            int hhmm = time / 100;

            int hour = hhmm / 100;
            int minute = hhmm % 100;

            minute++;

            if (minute >= 60)
            {
                minute = 0;
                hour++;
            }

            return hour * 100 + minute;
        }


        // ---------------------------------------------------------
        // DataBento NQ 전체 로드
        //
        // key   : date + time
        // value : NQ
        // ---------------------------------------------------------
        private static Dictionary<(int date, int time), double>
            LoadNQAll(string file)
        {
            var result =
                new Dictionary<(int date, int time), double>();

            foreach (string line in File.ReadLines(file).Skip(1))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                string[] sp = line.Split(',');

                if (sp.Length < 3)
                    continue;

                if (!int.TryParse(sp[0], out int date))
                    continue;

                if (!int.TryParse(sp[1], out int time))
                    continue;

                if (!double.TryParse(
                        sp[2],
                        NumberStyles.Any,
                        CultureInfo.InvariantCulture,
                        out double nq))
                    continue;

                result[(date, time)] = nq;
            }

            return result;
        }

        // =========================================================
        // 분 데이터의 NQ를 DataBento NQ로 교체
        // =========================================================
        public static void ReplaceMinuteNQ()
        {
            const string nqFile =
                @"C:\BJS\DataBento\NQ.txt";

            const string resetFile =
                @"C:\BJS\DataBento\NQ_Reset.txt";

            const string minuteRoot =
                @"C:\BJS\분";

            string[] stockNames =
            {
        "KODEX 레버리지",
        "KODEX 코스닥150레버리지"
    };

            var nqData =
                LoadNQAll(nqFile);

            var reset =
                LoadResetNQ(resetFile);

            Console.WriteLine(
                $"DataBento NQ : {nqData.Count:N0}개");

            Console.WriteLine(
                $"Reset NQ     : {reset.Count:N0}개");


            string[] directories =
                Directory.GetDirectories(minuteRoot)
                .OrderBy(x => x)
                .ToArray();


            foreach (string directory in directories)
            {
                string folderName =
                    Path.GetFileName(directory);

                if (!int.TryParse(folderName, out int date))
                    continue;


                // =================================================
                // 이 한국 거래일에 사용할 ResetNQ 찾기
                //
                // BuildResearch와 동일:
                // date 이하의 가장 최근 Reset
                // =================================================

                double resetNQ = 0.0;

                foreach (var r in reset.Reverse())
                {
                    if (r.Key <= date)
                    {
                        resetNQ = r.Value;
                        break;
                    }
                }

                if (resetNQ <= 0)
                {
                    Console.WriteLine();
                    Console.WriteLine(
                        $"Reset NQ 없음 : {date}");

                    return;
                }


                foreach (string stockName in stockNames)
                {
                    string file =
                        Path.Combine(
                            directory,
                            stockName + ".txt");

                    if (!File.Exists(file))
                        continue;


                    Console.WriteLine(
                        $"{date}  {stockName}  Reset={resetNQ:F2}");


                    string[] lines =
                        File.ReadAllLines(
                            file,
                            Encoding.UTF8);

                    string[] newLines =
                        new string[lines.Length];


                    for (int i = 0; i < lines.Length; i++)
                    {
                        string line = lines[i];

                        if (string.IsNullOrWhiteSpace(line))
                        {
                            newLines[i] = line;
                            continue;
                        }


                        string[] sp =
                            line.Split('\t');

                        if (sp.Length < 2)
                        {
                            newLines[i] = line;
                            continue;
                        }


                        if (!int.TryParse(
                                sp[0],
                                out int minuteTime))
                        {
                            // header
                            newLines[i] = line;
                            continue;
                        }


                        // =========================================
                        // 분 파일 -> DataBento 시간
                        //
                        // 90059 -> 901
                        // 90159 -> 902
                        // ...
                        // 95959 -> 1000
                        // =========================================

                        //int nqTime =
                        //    GetNextMinute(minuteTime);

                        
                        int nqTime =
                            minuteTime / 100;
                        if (nqTime == 859)
                            continue;

                        // =========================================
                        // DataBento에 정확한 날짜/분이
                        // 반드시 존재해야 한다.
                        // =========================================

                        if (!TryGetNQPrice(
                            nqData,
                            date,
                            nqTime,
                            out double nqPrice))
                        {
                            Console.WriteLine(
                                $"NQ 누락 1분 초과 - 중단 : " +
                                $"{date} {nqTime}");

                            return;
                        }


                        // =========================================
                        // Reset 대비 NQ %
                        //
                        // BuildResearch와 동일한 계산
                        // =========================================

                        double nqPct =
                            (nqPrice / resetNQ - 1.0)
                            * 100.0;


                        // =========================================
                        // 기존 분 파일 형식
                        //
                        // NQ % × 1000
                        // =========================================

                        int nq1000 =
                            (int)Math.Round(
                                nqPct * 1000.0);


                        // NQ = 마지막에서 두 번째 컬럼
                        int nqColumn =
                            sp.Length - 2;

                        sp[nqColumn] =
                            nq1000.ToString(
                                CultureInfo.InvariantCulture);


                        newLines[i] =
                            string.Join("\t", sp);
                    }


                    // =============================================
                    // 파일 전체 성공 후에만 원본 덮어쓰기
                    // =============================================

                    File.WriteAllLines(
                        file,
                        newLines,
                        Encoding.UTF8);
                }
            }


            Console.WriteLine();
            Console.WriteLine(
                "====================================");

            Console.WriteLine(
                "NQ 교체 완료");

            Console.WriteLine(
                "====================================");
        }

        static bool TryGetNQPrice(
            Dictionary<(int date, int time), double> nqData,
            int date,
            int time,
            out double price)
        {
            // 1. 정확한 데이터가 있으면 그대로 사용
            if (nqData.TryGetValue(
                    (date, time),
                    out price))
            {
                return true;
            }

            // 2. 현재 1분만 빠졌는지 검사
            int prevTime =
                GetPreviousMinute(time);

            int nextTime =
                GetNextMinuteHHMM(time);

            // 앞/뒤가 모두 있어야
            // 정확히 현재 1분만 누락된 것으로 인정
            if (!nqData.TryGetValue(
                    (date, prevTime),
                    out double prevPrice))
            {
                price = 0;
                return false;
            }

            if (!nqData.TryGetValue(
                    (date, nextTime),
                    out double nextPrice))
            {
                price = 0;
                return false;
            }

            // 3. 딱 한 분 누락 → 중간값
            price =
                (prevPrice + nextPrice) / 2.0;

            return true;
        }
        private static int GetPreviousMinute(int hhmm)
        {
            int hour = hhmm / 100;
            int minute = hhmm % 100;

            minute--;

            if (minute < 0)
            {
                minute = 59;
                hour--;
            }

            return hour * 100 + minute;
        }

        private static int GetNextMinuteHHMM(int hhmm)
        {
            int hour = hhmm / 100;
            int minute = hhmm % 100;

            minute++;

            if (minute >= 60)
            {
                minute = 0;
                hour++;
            }

            return hour * 100 + minute;
        }
        static List<EtfDay> LoadETF(string file)
        {
            var result = new List<EtfDay>();

            foreach (string line in File.ReadLines(file))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                string[] sp =
                    line.Split(
                        new[] { ' ', '\t' },
                        StringSplitOptions.RemoveEmptyEntries);

                if (sp.Length < 5)
                    continue;

                if (!int.TryParse(sp[0], out int date))
                    continue;

                if (!double.TryParse(sp[1], out double open))
                    continue;

                if (!double.TryParse(sp[4], out double close))
                    continue;

                result.Add(
                    new EtfDay
                    {
                        Date = date,
                        Open = open,
                        Close = close
                    });
            }

            return result
                .OrderBy(x => x.Date)
                .ToList();
        }
        static void SaveResearch(
    List<ResearchRow> rows,
    string file)
        {
            StringBuilder sb =
                new StringBuilder();


            sb.AppendLine(
                "Date," +
                "ResetNQ," +
                "NQ0900Pct," +
                "NQ1530Pct," +
                "ETF_OpenPct," +
                "ETF_ClosePct," +
                "US_NQ_ClosePct," +
                "Next_ETF_OpenPct");


            foreach (ResearchRow r in rows)
            {
                sb.Append(
                    r.Date);

                sb.Append(',');

                sb.Append(
                    r.ResetNQ.ToString(
                        "F2",
                        CultureInfo.InvariantCulture));

                sb.Append(',');

                sb.Append(
                    r.NQ0900Pct.ToString(
                        "F4",
                        CultureInfo.InvariantCulture));

                sb.Append(',');

                sb.Append(
                    r.NQ1530Pct.ToString(
                        "F4",
                        CultureInfo.InvariantCulture));

                sb.Append(',');

                sb.Append(
                    r.ETFOpenPct.ToString(
                        "F4",
                        CultureInfo.InvariantCulture));

                sb.Append(',');

                sb.Append(
                    r.ETFClosePct.ToString(
                        "F4",
                        CultureInfo.InvariantCulture));

                sb.Append(',');

                sb.Append(
                    r.USNQClosePct.ToString(
                        "F4",
                        CultureInfo.InvariantCulture));

                sb.Append(',');

                sb.Append(
                    r.NextETFOpenPct.ToString(
                        "F4",
                        CultureInfo.InvariantCulture));

                sb.AppendLine();
            }


            File.WriteAllText(
                file,
                sb.ToString(),
                Encoding.UTF8);
        }
        static void PrintUSNQBucketStats(
    List<ResearchRow> rows)
        {
            // ---------------------------------------------
            // 구간
            // ---------------------------------------------
            double[] cuts =
            {
        -1.50,
        -1.00,
        -0.75,
        -0.50,
        -0.25,
         0.00,
         0.25,
         0.50,
         0.75,
         1.00,
         1.50
    };


            Console.WriteLine();
            Console.WriteLine(
                "NQ1530Pct -> Next_ETF_OpenPct");

            Console.WriteLine(
                $"{"Range",-20}" +
                $"{"N",7}" +
                $"{"Avg",11}" +
                $"{"Median",11}" +
                $"{"Up%",9}" +
                $"{"Std",11}");

            Console.WriteLine(
                new string('-', 69));


            // ---------------------------------------------
            // 첫 구간 : < -1.50
            // ---------------------------------------------
            PrintBucket(
                rows,
                "< -1.50%",
                double.NegativeInfinity,
                cuts[0]);


            // ---------------------------------------------
            // 중간 구간
            // ---------------------------------------------
            for (int i = 0;
                 i < cuts.Length - 1;
                 i++)
            {
                double low =
                    cuts[i];

                double high =
                    cuts[i + 1];

                string name =
                    $"{low:+0.00;-0.00;0.00}% ~ " +
                    $"{high:+0.00;-0.00;0.00}%";

                PrintBucket(
                    rows,
                    name,
                    low,
                    high);
            }


            // ---------------------------------------------
            // 마지막 구간 : >= +1.50
            // ---------------------------------------------
            PrintBucket(
                rows,
                ">= +1.50%",
                cuts[cuts.Length - 1],
                double.PositiveInfinity);
        }
        static void PrintBucket(
    List<ResearchRow> rows,
    string name,
    double low,
    double high)
        {
            List<double> values =
                rows
                 .Where(r =>
                    r.NQ1530Pct >= low &&
                    r.NQ1530Pct < high)
                    .Select(r =>
                        r.NextETFOpenPct)
                    .OrderBy(x => x)
                    .ToList();


            int n =
                values.Count;


            if (n == 0)
            {
                Console.WriteLine(
                    $"{name,-20}" +
                    $"{0,7}");

                return;
            }


            // ---------------------------------------------
            // 평균
            // ---------------------------------------------
            double avg =
                values.Average();


            // ---------------------------------------------
            // 중앙값
            // ---------------------------------------------
            double median;

            if (n % 2 == 1)
            {
                median =
                    values[n / 2];
            }
            else
            {
                median =
                    (
                        values[n / 2 - 1] +
                        values[n / 2]
                    ) / 2.0;
            }


            // ---------------------------------------------
            // 상승 확률
            // 0% 초과만 상승으로 정의
            // ---------------------------------------------
            int upCount =
                values.Count(x => x > 0);

            double upPct =
                upCount * 100.0 / n;


            // ---------------------------------------------
            // 표준편차
            // Population Std
            // ---------------------------------------------
            double variance =
                values
                    .Select(x =>
                        (x - avg) *
                        (x - avg))
                    .Average();

            double std =
                Math.Sqrt(
                    variance);


            // ---------------------------------------------
            // 출력
            // ---------------------------------------------
            Console.WriteLine(
                $"{name,-20}" +
                $"{n,7}" +
                $"{avg,11:F3}" +
                $"{median,11:F3}" +
                $"{upPct,8:F1}%" +
                $"{std,11:F3}");
        }
        static void PrintNQ1530CrashDays(
    List<ResearchRow> rows)
        {
            Console.WriteLine();
            Console.WriteLine(
                "NQ1530Pct < -1.50% 상세");

            Console.WriteLine(
                $"{"Date",-10}" +
                $"{"NQ1530",10}" +
                $"{"ETF Close",12}" +
                $"{"US Close",12}" +
                $"{"US-NQ1530",12}" +
                $"{"Next Open",12}");

            Console.WriteLine(
                new string('-', 68));

            var selected =
                rows
                .Where(r => r.NQ1530Pct < -1.50)
                .OrderBy(r => r.Date)
                .ToList();

            foreach (var r in selected)
            {
                // 한국장 마감 이후 미국장 마감까지
                // ResetNQ 기준 %의 차이
                double after1530 =
                    r.USNQClosePct -
                    r.NQ1530Pct;

                Console.WriteLine(
                    $"{r.Date,-10}" +
                    $"{r.NQ1530Pct,9:F3}%" +
                    $"{r.ETFClosePct,11:F3}%" +
                    $"{r.USNQClosePct,11:F3}%" +
                    $"{after1530,11:F3}%" +
                    $"{r.NextETFOpenPct,11:F3}%");
            }

            Console.WriteLine();
            Console.WriteLine(
                $"Count = {selected.Count}");
        }
        static void PrintETFCrashDays(
    List<ResearchRow> rows)
        {
            Console.WriteLine();
            Console.WriteLine(
                "ETF_ClosePct <= -10% 다음날");

            Console.WriteLine(
                $"{"Date",-10}" +
                $"{"ETF Close",12}" +
                $"{"NQ1530",11}" +
                $"{"US Close",11}" +
                $"{"Next Open",12}");

            Console.WriteLine(
                new string('-', 56));

            var selected =
                rows
                .Where(r => r.ETFClosePct <= -10.0)
                .OrderBy(r => r.Date)
                .ToList();

            foreach (var r in selected)
            {
                Console.WriteLine(
                    $"{r.Date,-10}" +
                    $"{r.ETFClosePct,11:F3}%" +
                    $"{r.NQ1530Pct,10:F3}%" +
                    $"{r.USNQClosePct,10:F3}%" +
                    $"{r.NextETFOpenPct,11:F3}%");
            }

            Console.WriteLine();
            Console.WriteLine(
                $"Count = {selected.Count}");

            if (selected.Count > 0)
            {
                double avg =
                    selected.Average(
                        r => r.NextETFOpenPct);

                double upPct =
                    selected.Count(
                        r => r.NextETFOpenPct > 0)
                    * 100.0 /
                    selected.Count;

                Console.WriteLine(
                    $"Next Open Avg = {avg:F3}%");

                Console.WriteLine(
                    $"Next Open Up  = {upPct:F1}%");
            }
        }
        public static void AnalyzeNQMinuteZ()
        {
            var file = @"C:\BJS\DataBento\NQ.txt";

            var lines = File.ReadAllLines(file);

            var changes = new List<double>();

            int prevDate = 0;
            int prevTime = 0;
            double prevNQ = 0;

            foreach (string line in lines.Skip(1))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                string[] sp = line.Split(',');

                if (sp.Length < 3)
                    continue;

                if (!int.TryParse(sp[0], out int date))
                    continue;

                if (!int.TryParse(sp[1], out int time))
                    continue;

                if (!double.TryParse(sp[2], out double nq))
                    continue;

                // 한국장 시간만
                if (time < 900 || time > 1530)
                    continue;

                // 같은 날짜의 연속 데이터만 계산
                // 같은 날짜 + 정확히 연속된 1분 데이터만 계산
                if (date == prevDate &&
                    prevNQ > 0 &&
                    IsNextMinute(prevTime, time))
                {
                    double pct =
                        (nq - prevNQ) / prevNQ * 100.0;

                    changes.Add(pct);

                    if (pct < -0.5)
                    {
                        Console.WriteLine(
                            $"BIG DROP : {date} {time:D4}  {pct:F4}%  " +
                            $"{prevNQ:F2} -> {nq:F2}");
                    }
                }

                prevDate = date;
                prevTime = time;
                prevNQ = nq;
            }

            if (changes.Count < 2)
                return;

            double mean = changes.Average();

            double variance =
                changes.Sum(x => (x - mean) * (x - mean))
                / (changes.Count - 1);

            double std = Math.Sqrt(variance);

            Console.WriteLine();
            Console.WriteLine("===== NQ 1 Minute =====");
            Console.WriteLine($"Count : {changes.Count:N0}");
            Console.WriteLine($"Mean  : {mean:F5}%");
            Console.WriteLine($"Std   : {std:F5}%");
            Console.WriteLine($"Min   : {changes.Min():F4}%");
            Console.WriteLine($"Max   : {changes.Max():F4}%");

            Console.WriteLine();

            double[] zLevels =
            {
        1.0,
        1.5,
        2.0,
        2.5,
        3.0,
        4.0,
        5.0
    };

            foreach (double level in zLevels)
            {
                int count =
                    changes.Count(x =>
                        Math.Abs((x - mean) / std) >= level);

                double pct =
                    count * 100.0 / changes.Count;

                Console.WriteLine(
                    $"|Z| >= {level:F1} : " +
                    $"{count,6:N0}  ({pct:F3}%)");
            }
        }
        static bool IsNextMinute(int prev, int current)
        {
            int prevHour = prev / 100;
            int prevMin = prev % 100;

            prevMin++;

            if (prevMin >= 60)
            {
                prevMin = 0;
                prevHour++;
            }

            int next = prevHour * 100 + prevMin;

            return current == next;
        }
    }
}
