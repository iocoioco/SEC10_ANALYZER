using MathNet.Numerics.LinearAlgebra;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace New_Tradegy.Test
{

    /// <summary>
    /// NQ로 설명되지 않는 분간 가격 움직임이
    /// 다음 분에도 지속되는지를 연구하기 위한 Analyzer.
    ///
    /// 1단계:
    ///     KOSPI  : KODEX 레버리지
    ///     KOSDAQ : KODEX 코스닥150레버리지
    ///
    /// 일봉 데이터 로드 확인.
    ///
    /// 다음 단계:
    ///     종목 분 데이터 + NQ 분 데이터 로드
    ///     Residual 계산
    ///     다음 1분 continuation 분석
    /// </summary>

    // ---------------------------------------------------------
    // ETF 일봉 데이터
    // ---------------------------------------------------------
    //private class EtfDay
    //{
    //    public int Date;

    //    public double Open;
    //    public double Close;
    //}
    


    public static class MinuteResidualMomentumAnalyzer
    {
        private const string Root =
            @"C:\BJs\분";

        private const string KospiFile =
            "KODEX 레버리지.txt";

        private const string KosdaqFile =
            "KODEX 코스닥150레버리지.txt";


        private class MinuteRow
        {
            public int Time;
            public double Price;
            // 프로그램 누적
            public double Program;
            public double Institution;
            //public double Foreign;
            public double Individual;
        }

        private class OpenFlowFuture
        {
            public string Market;
            public int Date;

            // 09:03:59 시점 누적 수급
            public double Program;
            public double Institution;

            // Program 자체가 프로 + 외인
            public double Three;

            // 09:03:59 기준 미래 ETF 변화(%)
            public double Etf5;
            public double Etf10;
            public double Etf30;
        }

        private class MotionPair
        {
            public string Market;
            public int Date;

            public int TimeMinus1;
            public int Time0;
            public int Time1;
            public int Time2;

            public double Move0;
            public double Move1;
            public double Move2;

            // 해당 1분의 프로그램 증분
            public double ProgramDelta;

            public double ProgramDelta3;
            public double ProgramDelta5;

            public int ProgramUpCount3;
            public int ProgramUpCount5;

            public double EtfMove3;
        }


        public static void Run()
        {
            var kospi =
                new List<MotionPair>();

            var kosdaq =
                new List<MotionPair>();

            var kospiFlow =
                new List<FlowMinute>();

            var kosdaqFlow =
                new List<FlowMinute>();

            var kospiOpenFlow =
                new List<OpenFlowFuture>();

            var kosdaqOpenFlow =
                new List<OpenFlowFuture>();


            string[] dayDirs =
                Directory.GetDirectories(Root)
                    .OrderBy(x => x)
                    .ToArray();



            foreach (string dir in dayDirs)
            {
                string name =
                    Path.GetFileName(dir);

                if (!int.TryParse(
                        name,
                        out int date))
                {
                    continue;
                }


                string kospiPath =
                    Path.Combine(
                        dir,
                        KospiFile);

                string kosdaqPath =
                    Path.Combine(
                        dir,
                        KosdaqFile);


                if (File.Exists(kospiPath))
                {
                    ProcessFile(
                        "KOSPI",
                        date,
                        kospiPath,
                        kospi);
                }


                if (File.Exists(kosdaqPath))
                {
                    ProcessFile(
                        "KOSDAQ",
                        date,
                        kosdaqPath,
                        kosdaq);
                }

                if (File.Exists(kospiPath))
                {
                    ProcessFile(
                        "KOSPI",
                        date,
                        kospiPath,
                        kospi);

                    ProcessFlowFile(
                        "KOSPI",
                        date,
                        kospiPath,
                        kospiFlow);
                }


                if (File.Exists(kosdaqPath))
                {
                    ProcessFile(
                        "KOSDAQ",
                        date,
                        kosdaqPath,
                        kosdaq);

                    ProcessFlowFile(
                        "KOSDAQ",
                        date,
                        kosdaqPath,
                        kosdaqFlow);
                }

                if (File.Exists(kospiPath))
                {
                    ProcessOpenFlowFuture(
                        "KOSPI",
                        date,
                        kospiPath,
                        kospiOpenFlow);
                }

                if (File.Exists(kosdaqPath))
                {
                    ProcessOpenFlowFuture(
                        "KOSDAQ",
                        date,
                        kosdaqPath,
                        kosdaqOpenFlow);
                }

            }


            Console.WriteLine();
            Console.WriteLine(
                "======================================");

            Console.WriteLine(
                $"KOSPI pairs  : {kospi.Count:N0}");

            Console.WriteLine(
                $"KOSDAQ pairs : {kosdaq.Count:N0}");

            Console.WriteLine(
                "======================================");


            PrintSummary(
                "KOSPI",
                kospi);

            PrintSummary(
                "KOSDAQ",
                kosdaq);
            PrintProgramRelation(
                "KOSPI",
                kospi);

            PrintProgramRelation(
                "KOSDAQ",
                kosdaq);

            PrintProgramPersistence(
                "KOSPI",
                kospi);

            PrintProgramPersistence(
                "KOSDAQ",
                kosdaq);

            // -----------------------------------------------------
            // P3 ProgramDelta × ETF 3분 변화
            // -----------------------------------------------------
            PrintP3ProgramEtfMatrix(
                "KOSPI",
                kospi);

            PrintP3ProgramEtfMatrix(
                "KOSDAQ",
                kosdaq);



            // -------------------------------------------------
            // Program 3분 누적 × ETF 3분 방향 일치/충돌
            // -------------------------------------------------
            PrintP3Alignment(
                "KOSPI",
                kospi);

            PrintP3Alignment(
                "KOSDAQ",
                kosdaq);




            CalcFlowAccumulation(kospiFlow);
            CalcFlowAccumulation(kosdaqFlow);

            PrintFlowAnalysis(
                "KOSPI",
                kospiFlow);

            PrintFlowAnalysis(
                "KOSDAQ",
                kosdaqFlow);

            // -------------------------------------------------
            // 3분 누적
            // -------------------------------------------------
            PrintAccumulatedStrength(
                "KOSPI",
                kospiFlow,
                3);

            PrintAccumulatedStrength(
                "KOSDAQ",
                kosdaqFlow,
                3);


            //PrintFlowNextStrength(
            //    "KOSPI",
            //    kospiFlow,
            //    5);

            //PrintFlowNextStrength(
            //    "KOSDAQ",
            //    kosdaqFlow,
            //    5);

            PrintFlowAnalysis(
               "KOSPI",
               kospiFlow);

            PrintFlowAnalysis(
                "KOSDAQ",
                kosdaqFlow);

            PrintOpenFlowFuture(
                "KOSPI",
                kospiOpenFlow);

            PrintOpenFlowFuture(
                "KOSDAQ",
                kosdaqOpenFlow);

            PrintOpenFlowStrength(
                "KOSPI",
                kospiOpenFlow);

            PrintOpenFlowStrength(
                "KOSDAQ",
                kosdaqOpenFlow);
        }
        private static void PrintOpenFlowFuture(
    string market,
    List<OpenFlowFuture> rows)
        {
            if (rows == null ||
                rows.Count == 0)
                return;

            Console.WriteLine();
            Console.WriteLine(
                $"========== {market} 09:03 FLOW -> FUTURE ==========");

            Console.WriteLine(
                $"N = {rows.Count:N0}");

            Console.WriteLine(
                $"Corr(Three, +5m)  = " +
                $"{CalcCorrelation(rows.Select(x => x.Three), rows.Select(x => x.Etf5)):F4}");

            Console.WriteLine(
                $"Corr(Three, +10m) = " +
                $"{CalcCorrelation(rows.Select(x => x.Three), rows.Select(x => x.Etf10)):F4}");

            Console.WriteLine(
                $"Corr(Three, +30m) = " +
                $"{CalcCorrelation(rows.Select(x => x.Three), rows.Select(x => x.Etf30)):F4}");

            Console.WriteLine();

            PrintOpenFutureDirection(
                "Three BUY",
                rows.Where(x => x.Three > 0));

            PrintOpenFutureDirection(
                "Three SELL",
                rows.Where(x => x.Three < 0));
        }
        private static void PrintOpenFutureDirection(
    string label,
    IEnumerable<OpenFlowFuture> source)
        {
            var rows =
                source.ToList();

            if (rows.Count == 0)
                return;

            bool positive =
                rows.Average(x => x.Three) > 0;


            PrintFutureHorizon(
                label,
                "5m",
                rows,
                x => x.Etf5,
                positive);

            PrintFutureHorizon(
                label,
                "10m",
                rows,
                x => x.Etf10,
                positive);

            PrintFutureHorizon(
                label,
                "30m",
                rows,
                x => x.Etf30,
                positive);
        }

        private static void PrintFutureHorizon(
    string label,
    string horizon,
    List<OpenFlowFuture> rows,
    Func<OpenFlowFuture, double> selector,
    bool positive)
        {
            int same = 0;
            int opposite = 0;
            int zero = 0;

            foreach (var row in rows)
            {
                double v =
                    selector(row);

                if (v == 0)
                {
                    zero++;
                }
                else if (
                    (positive && v > 0) ||
                    (!positive && v < 0))
                {
                    same++;
                }
                else
                {
                    opposite++;
                }
            }

            int directional =
                same + opposite;

            double rate =
                directional > 0
                ? same * 100.0 / directional
                : 0.0;

            double mean =
                rows.Average(selector);

            Console.WriteLine(
                $"{label,-12}" +
                $" {horizon,4}" +
                $" N={rows.Count,5:N0}" +
                $" Same={same,5:N0}" +
                $" Opp={opposite,5:N0}" +
                $" Zero={zero,4:N0}" +
                $" Rate={rate,6:F2}%" +
                $" Mean={mean,8:F4}%");
        }

        private static void PrintOpenFlowStrength(
    string market,
    List<OpenFlowFuture> rows)
        {
            if (rows == null || rows.Count < 10)
                return;

            double[] values =
                rows
                .Select(x => x.Three)
                .OrderBy(x => x)
                .ToArray();

            double p20 = GetPercentile(values, 20.0);
            double p40 = GetPercentile(values, 40.0);
            double p60 = GetPercentile(values, 60.0);
            double p80 = GetPercentile(values, 80.0);

            Console.WriteLine();
            Console.WriteLine(
                $"========== {market} 09:03 FLOW STRENGTH ==========");

            Console.WriteLine(
                $"P20={p20:F1}  P40={p40:F1}  " +
                $"P60={p60:F1}  P80={p80:F1}");

            PrintOpenFlowBin(
                "<= P20",
                rows.Where(x => x.Three <= p20));

            PrintOpenFlowBin(
                "P20 ~ P40",
                rows.Where(x =>
                    x.Three > p20 &&
                    x.Three <= p40));

            PrintOpenFlowBin(
                "P40 ~ P60",
                rows.Where(x =>
                    x.Three > p40 &&
                    x.Three <= p60));

            PrintOpenFlowBin(
                "P60 ~ P80",
                rows.Where(x =>
                    x.Three > p60 &&
                    x.Three <= p80));

            PrintOpenFlowBin(
                ">= P80",
                rows.Where(x => x.Three > p80));
        }
        private static void PrintOpenFlowBin(
    string label,
    IEnumerable<OpenFlowFuture> source)
        {
            var r = source.ToList();

            if (r.Count == 0)
                return;

            double threeMean =
                r.Average(x => x.Three);

            double mean5 =
                r.Average(x => x.Etf5);

            double mean10 =
                r.Average(x => x.Etf10);

            double mean30 =
                r.Average(x => x.Etf30);

            double up5 =
                r.Count(x => x.Etf5 > 0) * 100.0 /
                r.Count(x => x.Etf5 != 0);

            double up10 =
                r.Count(x => x.Etf10 > 0) * 100.0 /
                r.Count(x => x.Etf10 != 0);

            double up30 =
                r.Count(x => x.Etf30 > 0) * 100.0 /
                r.Count(x => x.Etf30 != 0);

            Console.WriteLine(
                $"{label,-10}" +
                $" N={r.Count,4}" +
                $" ThreeMean={threeMean,9:F1}" +

                $" | 5m={mean5,8:F4}%" +
                $" Up={up5,6:F2}%" +

                $" | 10m={mean10,8:F4}%" +
                $" Up={up10,6:F2}%" +

                $" | 30m={mean30,8:F4}%" +
                $" Up={up30,6:F2}%");
        }
        private static void ProcessOpenFlowFuture(
    string market,
    int date,
    string file,
    List<OpenFlowFuture> output)
        {
            List<MinuteRow> rows =
                LoadMinuteFile(file);

            if (rows == null ||
                rows.Count == 0)
                return;

            rows =
                rows
                .OrderBy(x => x.Time)
                .ToList();


            // -------------------------------------------------
            // 기준 시점 : 09:03:59
            // -------------------------------------------------
            MinuteRow baseRow =
                rows.FirstOrDefault(
                    x => x.Time == 90359);

            if (baseRow == null)
                return;


            // -------------------------------------------------
            // 미래 시점
            // -------------------------------------------------
            MinuteRow row5 =
                rows.FirstOrDefault(
                    x => x.Time == 90859);

            MinuteRow row10 =
                rows.FirstOrDefault(
                    x => x.Time == 91359);

            MinuteRow row30 =
                rows.FirstOrDefault(
                    x => x.Time == 93359);


            // 세 시점이 모두 있어야 우선 사용
            if (row5 == null ||
                row10 == null ||
                row30 == null)
                return;


            // Price는 시초 대비 % × 100
            // 따라서 차이 / 100.0
            double etf5 =
                (row5.Price -
                 baseRow.Price) / 100.0;

            double etf10 =
                (row10.Price -
                 baseRow.Price) / 100.0;

            double etf30 =
                (row30.Price -
                 baseRow.Price) / 100.0;


            // Program = 프로 + 외인
            double three =
                baseRow.Program +
                baseRow.Institution;


            output.Add(
                new OpenFlowFuture
                {
                    Market = market,
                    Date = date,

                    Program = baseRow.Program,
                    Institution = baseRow.Institution,
                    Three = three,

                    Etf5 = etf5,
                    Etf10 = etf10,
                    Etf30 = etf30
                });
        }

        private static void PrintFlowNextStrength(
    string market,
    List<FlowMinute> data,
    int minutes)
        {
            var rows = new List<Tuple<double, double>>();

            foreach (var dayGroup in data.GroupBy(x => x.Date))
            {
                var day =
                    dayGroup
                    .OrderBy(x => x.Time)
                    .ToList();

                for (int i = 0; i < day.Count - 1; i++)
                {
                    // 다음 행이 실제 다음 1분인지 확인
                    if (!IsContinuousMinutes(day, i, i + 1))
                        continue;

                    double flow;

                    if (minutes == 3)
                        flow = day[i].Three3;
                    else if (minutes == 5)
                        flow = day[i].Three5;
                    else
                        continue;

                    // 누적값이 만들어지지 않은 행 제외
                    if (flow == 0)
                        continue;

                    double nextEtf =
                        day[i + 1].EtfDelta;

                    rows.Add(
                        Tuple.Create(
                            flow,
                            nextEtf));
                }
            }

            if (rows.Count == 0)
                return;

            // ---------------------------------------------
            // 극단값 0.5% 제거
            // ---------------------------------------------
            double[] flows =
                rows
                .Select(x => x.Item1)
                .OrderBy(x => x)
                .ToArray();

            double lo =
                GetPercentile(
                    flows,
                    0.5);

            double hi =
                GetPercentile(
                    flows,
                    99.5);

            rows =
                rows
                .Where(x =>
                    x.Item1 >= lo &&
                    x.Item1 <= hi)
                .ToList();

            Console.WriteLine();
            Console.WriteLine(
                $"========== {market} {minutes}MIN FLOW -> NEXT 1MIN ==========");

            Console.WriteLine(
                $"{minutes}MIN Three 0.5% ~ 99.5% : " +
                $"{lo:F1} ~ {hi:F1}");

            // 시장별 구간
            if (market == "KOSPI")
            {
                if (minutes == 5)
                {
                    PrintNextStrengthRow(
                        "<= -2500", rows,
                        x => x <= -2500);

                    PrintNextStrengthRow(
                        "-2500 ~ -1250", rows,
                        x => x > -2500 && x <= -1250);

                    PrintNextStrengthRow(
                        "-1250 ~ -500", rows,
                        x => x > -1250 && x <= -500);

                    PrintNextStrengthRow(
                        "-500 ~ 0", rows,
                        x => x > -500 && x < 0);

                    PrintNextStrengthRow(
                        "0 ~ +500", rows,
                        x => x >= 0 && x < 500);

                    PrintNextStrengthRow(
                        "+500 ~ +1250", rows,
                        x => x >= 500 && x < 1250);

                    PrintNextStrengthRow(
                        "+1250 ~ +2500", rows,
                        x => x >= 1250 && x < 2500);

                    PrintNextStrengthRow(
                        ">= +2500", rows,
                        x => x >= 2500);
                }
            }
            else // KOSDAQ
            {
                if (minutes == 5)
                {
                    PrintNextStrengthRow(
                        "<= -250", rows,
                        x => x <= -250);

                    PrintNextStrengthRow(
                        "-250 ~ -150", rows,
                        x => x > -250 && x <= -150);

                    PrintNextStrengthRow(
                        "-150 ~ -50", rows,
                        x => x > -150 && x <= -50);

                    PrintNextStrengthRow(
                        "-50 ~ 0", rows,
                        x => x > -50 && x < 0);

                    PrintNextStrengthRow(
                        "0 ~ +50", rows,
                        x => x >= 0 && x < 50);

                    PrintNextStrengthRow(
                        "+50 ~ +150", rows,
                        x => x >= 50 && x < 150);

                    PrintNextStrengthRow(
                        "+150 ~ +250", rows,
                        x => x >= 150 && x < 250);

                    PrintNextStrengthRow(
                        ">= +250", rows,
                        x => x >= 250);
                }
            }
        }

        private static void PrintNextStrengthRow(
    string label,
    List<Tuple<double, double>> source,
    Func<double, bool> condition)
        {
            var rows =
                source
                .Where(x => condition(x.Item1))
                .ToList();

            int n = rows.Count;

            if (n == 0)
                return;

            int same = 0;
            int opposite = 0;
            int zero = 0;

            foreach (var x in rows)
            {
                double flow = x.Item1;
                double etf = x.Item2;

                if (etf == 0)
                {
                    zero++;
                }
                else if (
                    (flow > 0 && etf > 0) ||
                    (flow < 0 && etf < 0))
                {
                    same++;
                }
                else
                {
                    opposite++;
                }
            }

            int directional =
                same + opposite;

            double rate =
                directional > 0
                ? same * 100.0 / directional
                : 0;

            double nextMean =
                rows.Average(x => x.Item2);

            double flowMean =
                rows.Average(x => x.Item1);

            Console.WriteLine(
                $"{label,-16}" +
                $" N={n,6:N0}" +
                $" Same={same,6:N0}" +
                $" Opp={opposite,6:N0}" +
                $" Zero={zero,5:N0}" +
                $" Rate={rate,6:F2}%" +
                $" NextMean={nextMean,8:F4}%" +
                $" FlowMean={flowMean,9:F1}");
        }
        private static void CalcFlowAccumulation(
    List<FlowMinute> data)
        {
            if (data == null || data.Count == 0)
                return;

            foreach (var dayGroup in
                data.GroupBy(x => x.Date))
            {
                var rows =
                    dayGroup
                    .OrderBy(x => x.Time)
                    .ToList();

                for (int i = 0; i < rows.Count; i++)
                {
                    // -----------------------------------------
                    // 3분
                    // -----------------------------------------
                    if (i >= 2 &&
                        IsContinuousMinutes(rows, i - 2, i))
                    {
                        rows[i].Three3 =
                            rows[i].ThreeDelta +
                            rows[i - 1].ThreeDelta +
                            rows[i - 2].ThreeDelta;

                        rows[i].Etf3 =
                            rows[i].EtfDelta +
                            rows[i - 1].EtfDelta +
                            rows[i - 2].EtfDelta;

                        rows[i].Valid3 = true;
                    }

                    // -----------------------------------------
                    // 5분
                    // -----------------------------------------
                    if (i >= 4 &&
                        IsContinuousMinutes(rows, i - 4, i))
                    {
                        rows[i].Three5 =
                            rows[i].ThreeDelta +
                            rows[i - 1].ThreeDelta +
                            rows[i - 2].ThreeDelta +
                            rows[i - 3].ThreeDelta +
                            rows[i - 4].ThreeDelta;

                        rows[i].Etf5 =
                            rows[i].EtfDelta +
                            rows[i - 1].EtfDelta +
                            rows[i - 2].EtfDelta +
                            rows[i - 3].EtfDelta +
                            rows[i - 4].EtfDelta;

                        rows[i].Valid5 = true;
                    }
                }
            }
        }
        private static bool IsContinuousMinutes(
    List<FlowMinute> rows,
    int start,
    int end)
        {
            if (start < 0 ||
                end >= rows.Count ||
                start >= end)
                return false;

            for (int i = start + 1; i <= end; i++)
            {
                if (rows[i].Date != rows[i - 1].Date)
                    return false;

                int prev =
                    TimeToMinutes(rows[i - 1].Time);

                int curr =
                    TimeToMinutes(rows[i].Time);

                if (curr - prev != 1)
                    return false;
            }

            return true;
        }

        private static void PrintAccumulatedStrength(
     string market,
     List<FlowMinute> data,
     int minutes)
        {
            if (data == null ||
                data.Count == 0)
                return;


            // =====================================================
            // 1. 정상적으로 누적 계산된 행만
            // =====================================================
            List<FlowMinute> validRows;

            if (minutes == 3)
            {
                validRows =
                    data
                    .Where(x => x.Valid3)
                    .ToList();
            }
            else if (minutes == 5)
            {
                validRows =
                    data
                    .Where(x => x.Valid5)
                    .ToList();
            }
            else
            {
                return;
            }


            if (validRows.Count == 0)
                return;


            // =====================================================
            // 2. 누적 ThreeDelta 분포
            // =====================================================
            double[] values =
                validRows
                .Select(x =>
                    GetThreeAccum(x, minutes))
                .OrderBy(x => x)
                .ToArray();


            double low =
                GetPercentile(
                    values,
                    0.5);

            double high =
                GetPercentile(
                    values,
                    99.5);


            // =====================================================
            // 3. 누적 ThreeDelta 자체를 0.5% trim
            // =====================================================
            List<FlowMinute> rows =
                validRows
                .Where(x =>
                {
                    double v =
                        GetThreeAccum(
                            x,
                            minutes);

                    return
                        v >= low &&
                        v <= high;
                })
                .ToList();


            Console.WriteLine();
            Console.WriteLine(
                $"========== {market} {minutes}MIN " +
                $"ThreeDelta STRENGTH ==========");

            Console.WriteLine(
                $"{minutes}MIN Three " +
                $"0.5% ~ 99.5% : " +
                $"{low:0.0} ~ {high:0.0}");

            Console.WriteLine(
                $"N ALL={validRows.Count:N0}  " +
                $"N TRIM={rows.Count:N0}");

            Console.WriteLine();


            // =====================================================
            // 이후 기존 구간별 출력 코드
            // =====================================================

            if (market == "KOSPI")
            {
                if (minutes == 3)
                {
                    PrintAccumGroup(
                        "<= -1500",
                        rows,
                        x => x.Three3 <= -1500,
                        minutes);

                    PrintAccumGroup(
                        "-1500 ~ -750",
                        rows,
                        x =>
                            x.Three3 > -1500 &&
                            x.Three3 <= -750,
                        minutes);

                    PrintAccumGroup(
                        "-750 ~ -300",
                        rows,
                        x =>
                            x.Three3 > -750 &&
                            x.Three3 <= -300,
                        minutes);

                    PrintAccumGroup(
                        "-300 ~ 0",
                        rows,
                        x =>
                            x.Three3 > -300 &&
                            x.Three3 < 0,
                        minutes);

                    PrintAccumGroup(
                        "0 ~ +300",
                        rows,
                        x =>
                            x.Three3 >= 0 &&
                            x.Three3 < 300,
                        minutes);

                    PrintAccumGroup(
                        "+300 ~ +750",
                        rows,
                        x =>
                            x.Three3 >= 300 &&
                            x.Three3 < 750,
                        minutes);

                    PrintAccumGroup(
                        "+750 ~ +1500",
                        rows,
                        x =>
                            x.Three3 >= 750 &&
                            x.Three3 < 1500,
                        minutes);

                    PrintAccumGroup(
                        ">= +1500",
                        rows,
                        x => x.Three3 >= 1500,
                        minutes);
                }
                else
                {
                    PrintAccumGroup(
                        "<= -2500",
                        rows,
                        x => x.Three5 <= -2500,
                        minutes);

                    PrintAccumGroup(
                        "-2500 ~ -1250",
                        rows,
                        x =>
                            x.Three5 > -2500 &&
                            x.Three5 <= -1250,
                        minutes);

                    PrintAccumGroup(
                        "-1250 ~ -500",
                        rows,
                        x =>
                            x.Three5 > -1250 &&
                            x.Three5 <= -500,
                        minutes);

                    PrintAccumGroup(
                        "-500 ~ 0",
                        rows,
                        x =>
                            x.Three5 > -500 &&
                            x.Three5 < 0,
                        minutes);

                    PrintAccumGroup(
                        "0 ~ +500",
                        rows,
                        x =>
                            x.Three5 >= 0 &&
                            x.Three5 < 500,
                        minutes);

                    PrintAccumGroup(
                        "+500 ~ +1250",
                        rows,
                        x =>
                            x.Three5 >= 500 &&
                            x.Three5 < 1250,
                        minutes);

                    PrintAccumGroup(
                        "+1250 ~ +2500",
                        rows,
                        x =>
                            x.Three5 >= 1250 &&
                            x.Three5 < 2500,
                        minutes);

                    PrintAccumGroup(
                        ">= +2500",
                        rows,
                        x => x.Three5 >= 2500,
                        minutes);
                }
            }
            else
            {
                double scale =
                    minutes == 3
                    ? 150.0
                    : 250.0;

                PrintAccumGroup(
                    $"<= -{scale:N0}",
                    rows,
                    x =>
                        GetThreeAccum(x, minutes) <=
                        -scale,
                    minutes);

                PrintAccumGroup(
                    $"-{scale:N0} ~ 0",
                    rows,
                    x =>
                        GetThreeAccum(x, minutes) >
                        -scale &&
                        GetThreeAccum(x, minutes) < 0,
                    minutes);

                PrintAccumGroup(
                    $"0 ~ +{scale:N0}",
                    rows,
                    x =>
                        GetThreeAccum(x, minutes) >= 0 &&
                        GetThreeAccum(x, minutes) < scale,
                    minutes);

                PrintAccumGroup(
                    $">= +{scale:N0}",
                    rows,
                    x =>
                        GetThreeAccum(x, minutes) >= scale,
                    minutes);
            }


            Console.WriteLine();
        }

        private static void PrintAccumGroup(
    string label,
    List<FlowMinute> source,
    Func<FlowMinute, bool> condition,
    int minutes)
        {
            var rows =
                source
                .Where(condition)
                .ToList();

            int n = rows.Count;

            if (n == 0)
                return;

            int same = 0;
            int opposite = 0;
            int zero = 0;

            foreach (var row in rows)
            {
                double three =
                    GetThreeAccum(row, minutes);

                double etf =
                    GetEtfAccum(row, minutes);

                if (etf == 0)
                {
                    zero++;
                }
                else if (
                    (three > 0 && etf > 0) ||
                    (three < 0 && etf < 0))
                {
                    same++;
                }
                else
                {
                    opposite++;
                }
            }

            int directional =
                same + opposite;

            double rate =
                directional > 0
                ? same * 100.0 / directional
                : 0;

            double etfMean =
                rows.Average(
                    x => GetEtfAccum(x, minutes));

            double threeMean =
                rows.Average(
                    x => GetThreeAccum(x, minutes));

            Console.WriteLine(
                $"{label,-16}" +
                $" N={n,7:N0}" +
                $" Same={same,7:N0}" +
                $" Opp={opposite,7:N0}" +
                $" Zero={zero,6:N0}" +
                $" Rate={rate,6:F2}%" +
                $" ETFMean={etfMean,8:F4}%" +
                $" ThreeMean={threeMean,9:F1}");
        }
        private static double GetThreeAccum(
    FlowMinute row,
    int minutes)
        {
            return minutes == 3
                ? row.Three3
                : row.Three5;
        }

        private static double GetEtfAccum(
            FlowMinute row,
            int minutes)
        {
            return minutes == 3
                ? row.Etf3
                : row.Etf5;
        }
        private static int TimeToMinutes(int time)
        {
            // 90059, 90159 등의 형식
            int hhmm = time / 100;

            int hour = hhmm / 100;
            int minute = hhmm % 100;

            return hour * 60 + minute;
        }

        // =========================================================
        // 하루 파일 처리
        // =========================================================
        private static void ProcessFile(
            string market,
            int date,
            string file,
            List<MotionPair> output)
        {
            List<MinuteRow> rows =
                LoadMinuteFile(file);

            if (rows == null ||
                rows.Count < 4)
            {
                return;
            }


            for (int i = 5;i < rows.Count - 1;i++)
            {
                MinuteRow p5 = rows[i - 5];
                MinuteRow p4 = rows[i - 4];
                MinuteRow p3 = rows[i - 3];
                MinuteRow p2 = rows[i - 2];
                MinuteRow p1 = rows[i - 1];
                MinuteRow cur = rows[i];
                MinuteRow next = rows[i + 1];


                // -------------------------------------------------
                // 6개 시점이 전부 1분 연속인지 확인
                // -------------------------------------------------
                if (!IsNextMinute(p5.Time, p4.Time))
                    continue;

                if (!IsNextMinute(p4.Time, p3.Time))
                    continue;

                if (!IsNextMinute(p3.Time, p2.Time))
                    continue;

                if (!IsNextMinute(p2.Time, p1.Time))
                    continue;

                if (!IsNextMinute(p1.Time, cur.Time))
                    continue;

                if (!IsNextMinute(cur.Time, next.Time))
                    continue;


                // -------------------------------------------------
                // 가격 변화
                // -------------------------------------------------
                double move0 =
                    (p1.Price - p2.Price) / 100.0;

                double move1 =
                    (cur.Price - p1.Price) / 100.0;

                double move2 =
                    (next.Price - cur.Price) / 100.0;


                // -------------------------------------------------
                // 1분 프로그램 증분
                // -------------------------------------------------
                double programDelta =
                    cur.Program -
                    p1.Program;


                // -------------------------------------------------
                // 3분 / 5분 프로그램 누적 증분
                // -------------------------------------------------
                double programDelta3 =
                    cur.Program -
                    p3.Program;

                double etfMove3 =
                    (cur.Price -
                     p3.Price) / 100.0;

                double programDelta5 =
                    cur.Program -
                    p5.Program;


                // -------------------------------------------------
                // 각 1분 프로그램 증분
                // -------------------------------------------------
                double d1 =
                    p4.Program - p5.Program;

                double d2 =
                    p3.Program - p4.Program;

                double d3 =
                    p2.Program - p3.Program;

                double d4 =
                    p1.Program - p2.Program;

                double d5 =
                    cur.Program - p1.Program;


                // -------------------------------------------------
                // 최근 3분 중 프로그램 매수인 분의 수
                //
                // p3 -> p2
                // p2 -> p1
                // p1 -> cur
                // -------------------------------------------------
                int upCount3 = 0;

                if (d3 > 0) upCount3++;
                if (d4 > 0) upCount3++;
                if (d5 > 0) upCount3++;


                // -------------------------------------------------
                // 최근 5분 중 프로그램 매수인 분의 수
                // -------------------------------------------------
                int upCount5 = 0;

                if (d1 > 0) upCount5++;
                if (d2 > 0) upCount5++;
                if (d3 > 0) upCount5++;
                if (d4 > 0) upCount5++;
                if (d5 > 0) upCount5++;


                output.Add(
                    new MotionPair
                    {
                        Market = market,
                        Date = date,

                        TimeMinus1 = p2.Time,
                        Time0 = p1.Time,
                        Time1 = cur.Time,
                        Time2 = next.Time,

                        Move0 = move0,
                        Move1 = move1,
                        Move2 = move2,

                        ProgramDelta =
                            programDelta,

                        ProgramDelta3 =
                            programDelta3,

                        ProgramDelta5 =
                            programDelta5,

                        ProgramUpCount3 =
                            upCount3,

                        ProgramUpCount5 =
                            upCount5,

                        EtfMove3 = etfMove3
                    });
            }
        }

        private static void PrintP3Alignment(
    string market,
    List<MotionPair> data)
        {
            Console.WriteLine();
            Console.WriteLine(
                $"========== {market} P3 ALIGNMENT ==========");

            var alignBuy =
                new List<MotionPair>();

            var conflictBuy =
                new List<MotionPair>();

            var alignSell =
                new List<MotionPair>();

            var conflictSell =
                new List<MotionPair>();


            foreach (MotionPair x in data)
            {
                double p3 =
                    x.ProgramDelta3;

                double etf3 =
                    x.EtfMove3;


                // -----------------------------------------
                // 잡음 제거
                // -----------------------------------------
                if (Math.Abs(p3) < 25.0)
                    continue;

                if (Math.Abs(etf3) < 0.25)
                    continue;


                // -----------------------------------------
                // 4가지 상태
                // -----------------------------------------
                if (p3 > 0 &&
                    etf3 > 0)
                {
                    alignBuy.Add(x);
                }
                else if (p3 > 0 &&
                         etf3 < 0)
                {
                    conflictBuy.Add(x);
                }
                else if (p3 < 0 &&
                         etf3 < 0)
                {
                    alignSell.Add(x);
                }
                else if (p3 < 0 &&
                         etf3 > 0)
                {
                    conflictSell.Add(x);
                }
            }


            PrintAlignmentGroup(
                "ALIGN BUY     P+ ETF+",
                alignBuy,
                true);

            PrintAlignmentGroup(
                "CONFLICT BUY  P+ ETF-",
                conflictBuy,
                true);

            PrintAlignmentGroup(
                "ALIGN SELL    P- ETF-",
                alignSell,
                false);

            PrintAlignmentGroup(
                "CONFLICT SELL P- ETF+",
                conflictSell,
                false);
        }

        private static void PrintAlignmentGroup(
     string label,
     List<MotionPair> rows,
     bool programBuy)
        {
            if (rows == null ||
                rows.Count == 0)
            {
                Console.WriteLine(
                    $"{label,-24} N=0");

                return;
            }


            int same = 0;
            int opposite = 0;
            int zero = 0;

            double nextSum = 0;
            double p3Sum = 0;
            double etf3Sum = 0;


            foreach (MotionPair x in rows)
            {
                double next =
                    x.Move2;

                nextSum += next;

                p3Sum +=
                    x.ProgramDelta3;

                etf3Sum +=
                    x.EtfMove3;


                if (next == 0)
                {
                    zero++;
                    continue;
                }


                // 프로그램 방향 기준으로
                // 다음 분도 같은 방향인가?
                if (programBuy)
                {
                    if (next > 0)
                        same++;
                    else
                        opposite++;
                }
                else
                {
                    if (next < 0)
                        same++;
                    else
                        opposite++;
                }
            }


            int directional =
                same + opposite;


            double rate =
                directional > 0
                    ? same * 100.0 / directional
                    : 0;


            // 프로그램 방향으로 부호 정렬
            double directionalNextMean =
                programBuy
                    ? nextSum / rows.Count
                    : -nextSum / rows.Count;


            double p3Mean =
                p3Sum / rows.Count;

            double etf3Mean =
                etf3Sum / rows.Count;


            Console.WriteLine(
                $"{label,-24}" +
                $" N={rows.Count,7:N0}" +
                $" Same={same,7:N0}" +
                $" Opp={opposite,7:N0}" +
                $" Zero={zero,6:N0}" +
                $" Rate={rate,6:0.00}%" +
                $" NextMean={directionalNextMean,8:0.0000}%" +
                $" P3Mean={p3Mean,8:0.0}" +
                $" ETF3Mean={etf3Mean,8:0.000}%");
        }
        private static void PrintP3ProgramEtfMatrix(
    string market,
    List<MotionPair> data)
        {
            Console.WriteLine();
            Console.WriteLine(
                $"========== {market} P3 PROGRAM x ETF ==========");

            // Program 3분 누적 구간
            double[] progCuts =
            {
        -100,
        -50,
        -25,
        -10,
        0,
        10,
        25,
        50,
        100
    };

            // ETF 3분 누적 변화(%)
            double[] etfCuts =
            {
        -1.00,
        -0.50,
        -0.25,
        0,
        0.25,
        0.50,
        1.00
    };

            for (int p = 0;
                 p <= progCuts.Length;
                 p++)
            {
                var progRows =
                    data.Where(x =>
                        InRange(
                            x.ProgramDelta3,
                            progCuts,
                            p))
                        .ToList();

                if (progRows.Count == 0)
                    continue;

                Console.WriteLine();
                Console.WriteLine(
                    $"--- P3 {RangeText(progCuts, p),15} " +
                    $"N={progRows.Count:N0} ---");

                for (int e = 0;
                     e <= etfCuts.Length;
                     e++)
                {
                    var rows =
                        progRows.Where(x =>
                            InRange(
                                x.EtfMove3,
                                etfCuts,
                                e))
                            .ToList();

                    if (rows.Count == 0)
                        continue;

                    PrintMatrixRow(
                        RangeText(etfCuts, e),
                        rows);
                }
            }
        }

        private static bool InRange(
            double value,
            double[] cuts,
            int index)
        {
            if (index == 0)
                return value < cuts[0];

            if (index == cuts.Length)
                return value >= cuts[cuts.Length - 1];

            return
                value >= cuts[index - 1] &&
                value < cuts[index];
        }

        private static string RangeText(
            double[] cuts,
            int index)
        {
            if (index == 0)
                return $"< {cuts[0]:0.##}";

            if (index == cuts.Length)
                return $">= {cuts[cuts.Length - 1]:0.##}";

            return
                $"{cuts[index - 1]:0.##} ~ " +
                $"{cuts[index]:0.##}";
        }

        private static void PrintMatrixRow(
    string etfRange,
    List<MotionPair> rows)
        {
            int up = 0;
            int down = 0;
            int zero = 0;

            double nextSum = 0;
            double progSum = 0;
            double etf3Sum = 0;

            foreach (MotionPair x in rows)
            {
                if (x.Move2 > 0)
                    up++;
                else if (x.Move2 < 0)
                    down++;
                else
                    zero++;

                nextSum += x.Move2;

                progSum +=
                    x.ProgramDelta3;

                etf3Sum +=
                    x.EtfMove3;
            }

            int directional =
                up + down;

            double rate =
                directional > 0
                    ? up * 100.0 / directional
                    : 0;

            double nextMean =
                nextSum / rows.Count;

            double progMean =
                progSum / rows.Count;

            double etf3Mean =
                etf3Sum / rows.Count;

            Console.WriteLine(
                $"ETF3 {etfRange,12}  " +
                $"N={rows.Count,6:N0}  " +
                $"Up={up,6:N0}  " +
                $"Dn={down,6:N0}  " +
                $"Zero={zero,5:N0}  " +
                $"NextUp={rate,6:0.00}%  " +
                $"Next={nextMean,8:0.0000}%  " +
                $"P3Mean={progMean,8:0.0}  " +
                $"ETF3Mean={etf3Mean,7:0.000}%");
        }
        private static void PrintProgramPersistence(
    string market,
    List<MotionPair> data)
        {
            Console.WriteLine();
            Console.WriteLine(
                $"========== {market} PROGRAM PERSISTENCE ==========");


            var p3 =
                data
                    .Where(x =>
                        x.ProgramDelta3 > 0 &&
                        x.ProgramUpCount3 == 3)
                    .ToList();

            PrintPersistenceGroup(
                "P3 BUY 3/3",
                p3);


            var p5_4 =
                data
                    .Where(x =>
                        x.ProgramDelta5 > 0 &&
                        x.ProgramUpCount5 >= 4)
                    .ToList();

            PrintPersistenceGroup(
                "P5 BUY >=4/5",
                p5_4);


            var p5_5 =
                data
                    .Where(x =>
                        x.ProgramDelta5 > 0 &&
                        x.ProgramUpCount5 == 5)
                    .ToList();

            PrintPersistenceGroup(
                "P5 BUY 5/5",
                p5_5);
        }

        private static void PrintPersistenceGroup(
    string label,
    List<MotionPair> rows)
        {
            if (rows == null ||
                rows.Count == 0)
                return;


            int up = 0;
            int down = 0;
            int zero = 0;

            double nextSum = 0;


            foreach (MotionPair x in rows)
            {
                nextSum +=
                    x.Move2;

                if (x.Move2 > 0)
                    up++;
                else if (x.Move2 < 0)
                    down++;
                else
                    zero++;
            }


            int directional =
                up + down;

            double rate =
                directional > 0
                    ? up * 100.0 / directional
                    : 0;

            double nextMean =
                nextSum / rows.Count;


            Console.WriteLine(
                $"{label,-15}" +
                $"N={rows.Count,7:N0}  " +
                $"Up={up,7:N0}  " +
                $"Down={down,7:N0}  " +
                $"Zero={zero,6:N0}  " +
                $"NextUp={rate,6:0.00}%  " +
                $"NextMean={nextMean,8:0.0000}%");
        }
        // =========================================================
        // 분 파일 Loading
        //
        // 첫 컬럼 : 시간
        // 두번째 : 가격
        // =========================================================
        private static List<MinuteRow> LoadMinuteFile(string file)
        {
            var result =
                new List<MinuteRow>();

            foreach (string line in File.ReadLines(file))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                string[] sp =
                    line.Split(
                        new[] { ' ', '\t', ',' },
                        StringSplitOptions.RemoveEmptyEntries);

                // 0 = 시간
                // 1 = 가격
                // 2 = 수요
                // 3 = 프로그램
                // 4 = 기관
                // 5 = 외인
                // 6 = 개인
                if (sp.Length < 7)
                    continue;

                if (!int.TryParse(
                        sp[0],
                        out int time))
                    continue;

                if (!double.TryParse(
                        sp[1],
                        out double price))
                    continue;

                if (!double.TryParse(
                        sp[3],
                        out double program))
                    continue;

                if (!double.TryParse(
                        sp[4],
                        out double institution))
                    continue;

                //if (!double.TryParse(
                //        sp[5],
                //        out double foreign))
                //    continue;

                if (!double.TryParse(
                        sp[6],
                        out double individual))
                    continue;

                if (!IsValidTime(time))
                    continue;

                result.Add(
                    new MinuteRow
                    {
                        Time = time,
                        Price = price,

                        Program = program,
                        Institution = institution,
                        //Foreign = foreign,
                        Individual = individual
                    });
            }

            return result
                .OrderBy(x => TimeToMinute(x.Time))
                .ToList();
        }

        // =========================================================
        // 정확한 다음 1분인가?
        // =========================================================
        private static bool IsNextMinute(
            int time1,
            int time2)
        {
            return
                TimeToMinute(time2) -
                TimeToMinute(time1) == 1;
        }


        // =========================================================
        // HHMMSS -> 하루 중 minute
        //
        // 085959 -> 539
        // 090059 -> 540
        // =========================================================
        private static int TimeToMinute(
            int time)
        {
            int hh =
                time / 10000;

            int mm =
                (time / 100) % 100;

            return
                hh * 60 + mm;
        }


        private static bool IsValidTime(
            int time)
        {
            int hh =
                time / 10000;

            int mm =
                (time / 100) % 100;

            int ss =
                time % 100;


            return
                hh >= 0 &&
                hh <= 23 &&
                mm >= 0 &&
                mm <= 59 &&
                ss >= 0 &&
                ss <= 59;
        }


        // =========================================================
        // 기본 결과
        // =========================================================
        private static void PrintSummary(
            string market,
            List<MotionPair> data)
        {
            Console.WriteLine();
            Console.WriteLine(
                $"========== {market} ==========");

            // -----------------------------------------------------
            // 하락
            // -----------------------------------------------------
            PrintRange(data, -999.0, -2.00, "<= -2.00");

            PrintRange(data, -2.00, -1.75, "-2.00~-1.75");
            PrintRange(data, -1.75, -1.50, "-1.75~-1.50");
            PrintRange(data, -1.50, -1.25, "-1.50~-1.25");
            PrintRange(data, -1.25, -1.00, "-1.25~-1.00");

            PrintRange(data, -1.00, -0.75, "-1.00~-0.75");
            PrintRange(data, -0.75, -0.50, "-0.75~-0.50");
            PrintRange(data, -0.50, -0.25, "-0.50~-0.25");
            PrintRange(data, -0.25, 0.00, "-0.25~ 0.00");


            // -----------------------------------------------------
            // 상승
            // -----------------------------------------------------
            PrintRange(data, 0.00, 0.25, " 0.00~+0.25");

            PrintRange(data, 0.25, 0.50, "+0.25~+0.50");
            PrintRange(data, 0.50, 0.75, "+0.50~+0.75");
            PrintRange(data, 0.75, 1.00, "+0.75~+1.00");

            PrintRange(data, 1.00, 1.25, "+1.00~+1.25");
            PrintRange(data, 1.25, 1.50, "+1.25~+1.50");
            PrintRange(data, 1.50, 1.75, "+1.50~+1.75");
            PrintRange(data, 1.75, 2.00, "+1.75~+2.00");

            PrintRange(data, 2.00, 999.0, ">= +2.00");
        }

        private static void PrintRange(
    List<MotionPair> data,
    double min,
    double max,
    string label)
        {
            var rows =
                data
                    .Where(x =>
                        x.Move1 >= min &&
                        x.Move1 < max)
                    .ToList();

            if (rows.Count == 0)
                return;


            int same = 0;
            int opposite = 0;
            int zero = 0;

            double directionalSum = 0;


            foreach (MotionPair x in rows)
            {
                // 앞 분의 방향을 기준으로
                // 다음 분 움직임을 방향 정렬
                double directional =
                    Math.Sign(x.Move1) *
                    x.Move2;

                directionalSum += directional;

                if (directional > 0)
                    same++;
                else if (directional < 0)
                    opposite++;
                else
                    zero++;
            }


            double rate =
                same * 100.0 / rows.Count;

            double mean =
                directionalSum / rows.Count;


            Console.WriteLine(
                $"{label,-13} " +
                $"N={rows.Count,7:N0}  " +
                $"Same={same,7:N0}  " +
                $"Opp={opposite,7:N0}  " +
                $"Zero={zero,5:N0}  " +
                $"Rate={rate,6:0.00}%  " +
                $"NextMean={mean,8:0.0000}%");
        }
        private static void PrintProgramRelation(
    string market,
    List<MotionPair> data)
        {
            var rows =
                data
                    .Where(x => x.ProgramDelta != 0)
                    .ToList();

            if (rows.Count == 0)
                return;


            // =====================================================
            // 1. Pearson correlation
            //    ProgramDelta <-> 같은 1분 ETF Move1
            // =====================================================
            double progMean =
                rows.Average(x => x.ProgramDelta);

            double etfMean =
                rows.Average(x => x.Move1);


            double cov = 0;
            double progVar = 0;
            double etfVar = 0;


            foreach (MotionPair x in rows)
            {
                double dp =
                    x.ProgramDelta - progMean;

                double de =
                    x.Move1 - etfMean;

                cov += dp * de;
                progVar += dp * dp;
                etfVar += de * de;
            }


            double corr = 0;

            if (progVar > 0 &&
                etfVar > 0)
            {
                corr =
                    cov /
                    Math.Sqrt(
                        progVar * etfVar);
            }


            Console.WriteLine();
            Console.WriteLine(
                $"========== {market} PROGRAM ==========");

            Console.WriteLine(
                $"N = {rows.Count:N0}");

            Console.WriteLine(
                $"Corr(ProgDelta, ETF Move) = {corr:0.0000}");

            Console.WriteLine();


            // =====================================================
            // 2. 프로그램 매수
            // =====================================================
            var buy =
                rows
                    .Where(x => x.ProgramDelta > 0)
                    .ToList();

            PrintProgramGroup(
                "PROG BUY ",
                buy,
                true);


            // =====================================================
            // 3. 프로그램 매도
            // =====================================================
            var sell =
                rows
                    .Where(x => x.ProgramDelta < 0)
                    .ToList();

            PrintProgramGroup(
                "PROG SELL",
                sell,
                false);

            // =====================================================
            // 4. ProgramDelta 분포
            // =====================================================
            PrintProgramDistribution(
                market,
                rows);

            PrintProgramStrength(
                market,
                rows);



        }
        private static void PrintProgramGroup(
             string label,
             List<MotionPair> rows,
             bool isBuy)
        {
            if (rows.Count == 0)
                return;


            int same = 0;
            int opposite = 0;
            int zero = 0;


            foreach (MotionPair x in rows)
            {
                if (x.Move1 == 0)
                {
                    zero++;
                    continue;
                }


                if (isBuy)
                {
                    if (x.Move1 > 0)
                        same++;
                    else
                        opposite++;
                }
                else
                {
                    if (x.Move1 < 0)
                        same++;
                    else
                        opposite++;
                }
            }


            int directional =
                same + opposite;


            double sameRate =
                directional > 0
                    ? same * 100.0 / directional
                    : 0;


            double etfMean =
                rows.Average(x => x.Move1);


            double progMean =
                rows.Average(x => x.ProgramDelta);


            Console.WriteLine(
                $"{label}  " +
                $"N={rows.Count,7:N0}  " +
                $"Same={same,7:N0}  " +
                $"Opp={opposite,7:N0}  " +
                $"Zero={zero,6:N0}  " +
                $"Rate={sameRate,6:0.00}%  " +
                $"ETFMean={etfMean,8:0.0000}%  " +
                $"ProgMean={progMean,9:0.0}");
        }
        private static void PrintProgramDistribution(
    string market,
    List<MotionPair> rows)
        {
            if (rows == null ||
                rows.Count == 0)
                return;

            double[] values =
                rows
                    .Select(x => x.ProgramDelta)
                    .OrderBy(x => x)
                    .ToArray();

            Console.WriteLine();
            Console.WriteLine(
                $"----- {market} ProgramDelta Distribution -----");

            PrintPercentile(values, 1);
            PrintPercentile(values, 5);
            PrintPercentile(values, 10);
            PrintPercentile(values, 25);
            PrintPercentile(values, 50);
            PrintPercentile(values, 75);
            PrintPercentile(values, 90);
            PrintPercentile(values, 95);
            PrintPercentile(values, 99);

            Console.WriteLine();
        }


        private static void PrintPercentile(
            double[] values,
            double percentile)
        {
            if (values == null ||
                values.Length == 0)
                return;

            double pos =
                (values.Length - 1) *
                percentile / 100.0;

            int lo =
                (int)Math.Floor(pos);

            int hi =
                (int)Math.Ceiling(pos);

            double value;

            if (lo == hi)
            {
                value = values[lo];
            }
            else
            {
                double w =
                    pos - lo;

                value =
                    values[lo] * (1.0 - w) +
                    values[hi] * w;
            }

            Console.WriteLine(
                $"P{percentile,2:0} = {value,10:0.0}");
        }

        private static void PrintProgramStrength(
    string market,
    List<MotionPair> rows)
        {
            if (rows == null ||
                rows.Count == 0)
                return;

            Console.WriteLine();
            Console.WriteLine(
                $"----- {market} ProgramDelta Strength -----");


            // -----------------------------------------------------
            // 시장별 프로그램 규모가 크게 다르므로
            // 서로 다른 경계 사용
            // -----------------------------------------------------
            double[] bounds;

            if (market == "KOSPI")
            {
                bounds = new double[]
                {
            45,
            100,
            200,
            400
                };
            }
            else
            {
                bounds = new double[]
                {
            5,
            15,
            25,
            55
                };
            }


            // =====================================================
            // SELL SIDE
            // =====================================================

            PrintProgramStrengthGroup(
                $"<= -{bounds[3]:0}",
                rows.Where(x =>
                    x.ProgramDelta <= -bounds[3]),
                false);

            PrintProgramStrengthGroup(
                $"-{bounds[3]:0} ~ -{bounds[2]:0}",
                rows.Where(x =>
                    x.ProgramDelta > -bounds[3] &&
                    x.ProgramDelta <= -bounds[2]),
                false);

            PrintProgramStrengthGroup(
                $"-{bounds[2]:0} ~ -{bounds[1]:0}",
                rows.Where(x =>
                    x.ProgramDelta > -bounds[2] &&
                    x.ProgramDelta <= -bounds[1]),
                false);

            PrintProgramStrengthGroup(
                $"-{bounds[1]:0} ~ -{bounds[0]:0}",
                rows.Where(x =>
                    x.ProgramDelta > -bounds[1] &&
                    x.ProgramDelta <= -bounds[0]),
                false);

            PrintProgramStrengthGroup(
                $"-{bounds[0]:0} ~ 0",
                rows.Where(x =>
                    x.ProgramDelta > -bounds[0] &&
                    x.ProgramDelta < 0),
                false);


            // =====================================================
            // BUY SIDE
            // =====================================================

            PrintProgramStrengthGroup(
                $"0 ~ +{bounds[0]:0}",
                rows.Where(x =>
                    x.ProgramDelta > 0 &&
                    x.ProgramDelta < bounds[0]),
                true);

            PrintProgramStrengthGroup(
                $"+{bounds[0]:0} ~ +{bounds[1]:0}",
                rows.Where(x =>
                    x.ProgramDelta >= bounds[0] &&
                    x.ProgramDelta < bounds[1]),
                true);

            PrintProgramStrengthGroup(
                $"+{bounds[1]:0} ~ +{bounds[2]:0}",
                rows.Where(x =>
                    x.ProgramDelta >= bounds[1] &&
                    x.ProgramDelta < bounds[2]),
                true);

            PrintProgramStrengthGroup(
                $"+{bounds[2]:0} ~ +{bounds[3]:0}",
                rows.Where(x =>
                    x.ProgramDelta >= bounds[2] &&
                    x.ProgramDelta < bounds[3]),
                true);

            PrintProgramStrengthGroup(
                $">= +{bounds[3]:0}",
                rows.Where(x =>
                    x.ProgramDelta >= bounds[3]),
                true);

            Console.WriteLine();
        }


        private static void PrintProgramStrengthGroup(
            string label,
            IEnumerable<MotionPair> source,
            bool positive)
        {
            var rows =
                source.ToList();

            if (rows.Count == 0)
                return;


            int same = 0;
            int opp = 0;
            int zero = 0;

            double etfSum = 0;
            double progSum = 0;


            foreach (MotionPair x in rows)
            {
                double move =
                    x.Move1;

                etfSum += move;
                progSum += x.ProgramDelta;


                if (move == 0)
                {
                    zero++;
                    continue;
                }


                if (positive)
                {
                    if (move > 0)
                        same++;
                    else
                        opp++;
                }
                else
                {
                    if (move < 0)
                        same++;
                    else
                        opp++;
                }
            }


            int directional =
                same + opp;

            double rate =
                directional > 0
                    ? same * 100.0 / directional
                    : 0;

            double etfMean =
                etfSum / rows.Count;

            double progMean =
                progSum / rows.Count;


            Console.WriteLine(
                $"{label,-14}" +
                $"N={rows.Count,7:N0}  " +
                $"Same={same,7:N0}  " +
                $"Opp={opp,7:N0}  " +
                $"Zero={zero,6:N0}  " +
                $"Rate={rate,6:0.00}%  " +
                $"ETFMean={etfMean,8:0.0000}%  " +
                $"ProgMean={progMean,9:0.0}");
        }
        private static void PrintBucket(
            List<MotionPair> data,
            double min,
            double max)
        {
            var rows =
                data
                    .Where(
                        x =>
                            Math.Abs(x.Move1) >= min &&
                            Math.Abs(x.Move1) < max)
                    .ToList();


            if (rows.Count == 0)
                return;


            int same = 0;
            int opposite = 0;
            int zero = 0;


            double directionalSum = 0;


            foreach (MotionPair x in rows)
            {
                // Move1 방향으로 Move2를 변환
                double directional =
                    Math.Sign(x.Move1) *
                    x.Move2;


                directionalSum +=
                    directional;


                if (directional > 0)
                    same++;
                else if (directional < 0)
                    opposite++;
                else
                    zero++;
            }


            double sameRate =
                rows.Count > 0
                    ? same * 100.0 / rows.Count
                    : 0;


            double mean =
                rows.Count > 0
                    ? directionalSum / rows.Count
                    : 0;


            string upper =
                max >= 999
                    ? "+"
                    : max.ToString("0.00");


            Console.WriteLine(
                $"{min,4:0.00}-{upper,-4}  " +
                $"N={rows.Count,7:N0}  " +
                $"Same={same,7:N0}  " +
                $"Opp={opposite,7:N0}  " +
                $"Zero={zero,5:N0}  " +
                $"Rate={sameRate,6:0.00}%  " +
                $"NextMean={mean,8:0.0000}%");
        }

        private static void ProcessFlowFile(
   string market,
   int date,
   string file,
   List<FlowMinute> output)
        {
            List<MinuteRow> rows =
                LoadMinuteFile(file);

            if (rows == null ||
                rows.Count < 2)
                return;

            for (int i = 1;
                 i < rows.Count;
                 i++)
            {
                MinuteRow prev =
                    rows[i - 1];

                MinuteRow cur =
                    rows[i];

                // ---------------------------------------------
                // 반드시 정확한 다음 1분이어야 함
                // ---------------------------------------------
                if (!IsNextMinute(
                        prev.Time,
                        cur.Time))
                {
                    continue;
                }


                // ---------------------------------------------
                // 기관/개인 최초 발표 이후만
                //
                // 파일 시간이 xx:xx:59 형태이므로
                // 우선 09:03:59부터 사용
                // ---------------------------------------------
                if (cur.Time < 90359)
                    continue;


                // ---------------------------------------------
                // ETF 같은 1분 변화
                // ---------------------------------------------
                double etfDelta =
                    (cur.Price -
                     prev.Price) / 100.0;


                // ---------------------------------------------
                // 각 주체 1분 증분
                // ---------------------------------------------
                double dPro =
                    cur.Program -
                    prev.Program;

                //double dFor =
                //    cur.Foreign -
                //    prev.Foreign;

                double dInst =
                    cur.Institution -
                    prev.Institution;

                double dIndi =
                    cur.Individual -
                    prev.Individual;


                // ---------------------------------------------
                // 프로 + 외인 + 기관
                // ---------------------------------------------
                double three =
                    dPro +
                    //dFor +
                    dInst;


                output.Add(
                    new FlowMinute
                    {
                        Market = market,
                        Date = date,
                        Time = cur.Time,

                        EtfDelta = etfDelta,

                        ProDelta = dPro,
                        //ForeignDelta = dFor,
                        InstitutionDelta = dInst,
                        IndividualDelta = dIndi,

                        ThreeDelta = three
                    });
            }
        }

        private static double GetPercentile(
    double[] sorted,
    double percentile)
        {
            if (sorted == null ||
                sorted.Length == 0)
                return 0;

            double pos =
                (sorted.Length - 1) *
                percentile / 100.0;

            int lo =
                (int)Math.Floor(pos);

            int hi =
                (int)Math.Ceiling(pos);

            if (lo == hi)
                return sorted[lo];

            double w =
                pos - lo;

            return
                sorted[lo] * (1.0 - w) +
                sorted[hi] * w;
        }
        private static void PrintThreeStrength(
     string market,
     List<FlowMinute> rows)
        {
            Console.WriteLine();
            Console.WriteLine(
                $"========== {market} ThreeDelta STRENGTH ==========");


            double[] cuts;
            string[] labels;


            // =====================================================
            // KOSPI
            // 거래규모가 크므로 큰 구간
            // =====================================================
            if (market == "KOSPI")
            {
                cuts = new double[]
                {
            double.NegativeInfinity,

            -750,
            -500,
            -250,
            -100,
            0,
            100,
            250,
            500,
            750,

            double.PositiveInfinity
                };

                labels = new string[]
                {
            "<= -750",
            "-750 ~ -500",
            "-500 ~ -250",
            "-250 ~ -100",
            "-100 ~ 0",

            "0 ~ +100",
            "+100 ~ +250",
            "+250 ~ +500",
            "+500 ~ +750",
            ">= +750"
                };
            }

            // =====================================================
            // KOSDAQ
            // 기존 구간 유지
            // =====================================================
            else
            {
                cuts = new double[]
                {
            double.NegativeInfinity,

            -100,
            -50,
            -25,
            -10,
            0,
            10,
            25,
            50,
            100,

            double.PositiveInfinity
                };

                labels = new string[]
                {
            "<= -100",
            "-100 ~ -50",
            "-50 ~ -25",
            "-25 ~ -10",
            "-10 ~ 0",

            "0 ~ +10",
            "+10 ~ +25",
            "+25 ~ +50",
            "+50 ~ +100",
            ">= +100"
                };
            }


            // =====================================================
            // 구간별 통계
            // cuts는 항상 labels보다 1개 많음
            // =====================================================
            for (int k = 0;
                 k < labels.Length;
                 k++)
            {
                double lo =
                    cuts[k];

                double hi =
                    cuts[k + 1];


                List<FlowMinute> group;


                // 첫 구간
                if (k == 0)
                {
                    group =
                        rows
                            .Where(x =>
                                x.ThreeDelta <= hi)
                            .ToList();
                }

                // 마지막 구간
                else if (k == labels.Length - 1)
                {
                    group =
                        rows
                            .Where(x =>
                                x.ThreeDelta >= lo)
                            .ToList();
                }

                // 중간 구간
                else
                {
                    group =
                        rows
                            .Where(x =>
                                x.ThreeDelta > lo &&
                                x.ThreeDelta <= hi)
                            .ToList();
                }


                PrintThreeStrengthGroup(
                    labels[k],
                    group);
            }


            Console.WriteLine();
        }

        private static void PrintThreeStrengthGroup(
    string label,
    List<FlowMinute> rows)
        {
            int n = rows.Count;

            if (n == 0)
                return;

            int same = 0;
            int opposite = 0;
            int zero = 0;

            double etfSum = 0.0;
            double flowSum = 0.0;

            foreach (var x in rows)
            {
                etfSum += x.EtfDelta;
                flowSum += x.ThreeDelta;

                if (x.EtfDelta == 0 ||
                    x.ThreeDelta == 0)
                {
                    zero++;
                }
                else if (
                    Math.Sign(x.EtfDelta) ==
                    Math.Sign(x.ThreeDelta))
                {
                    same++;
                }
                else
                {
                    opposite++;
                }
            }

            int directional =
                same + opposite;

            double rate =
                directional > 0
                ? same * 100.0 / directional
                : 0.0;

            double etfMean =
                etfSum / n;

            double flowMean =
                flowSum / n;


            Console.WriteLine(
                $"{label,-12}" +
                $" N={n,7:N0}" +
                $" Same={same,7:N0}" +
                $" Opp={opposite,7:N0}" +
                $" Zero={zero,6:N0}" +
                $" Rate={rate,6:F2}%" +
                $" ETFMean={etfMean,8:F4}%" +
                $" ThreeMean={flowMean,8:F1}");
        }

        private static void PrintFlowAnalysis(
    string market,
    List<FlowMinute> data)
        {
            if (data == null ||
                data.Count == 0)
                return;


            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine(
                $"========== {market} 3:1 FLOW ==========");


            // =====================================================
            // 1. 전체 데이터
            // =====================================================
            PrintFlowStats(
                "ALL",
                data);


            // =====================================================
            // 2. ThreeDelta 자체 분포
            // =====================================================
            double[] threeValues =
                data
                    .Select(x => x.ThreeDelta)
                    .OrderBy(x => x)
                    .ToArray();

            double threeLow =
                GetPercentile(
                    threeValues,
                    0.5);

            double threeHigh =
                GetPercentile(
                    threeValues,
                    99.5);


            // =====================================================
            // 3. Individual 자체 분포
            // =====================================================
            double[] indiValues =
                data
                    .Select(x => x.IndividualDelta)
                    .OrderBy(x => x)
                    .ToArray();

            double indiLow =
                GetPercentile(
                    indiValues,
                    0.5);

            double indiHigh =
                GetPercentile(
                    indiValues,
                    99.5);


            Console.WriteLine();
            Console.WriteLine(
                $"Three  0.5% ~ 99.5% : " +
                $"{threeLow:0.0} ~ {threeHigh:0.0}");

            Console.WriteLine(
                $"Indi   0.5% ~ 99.5% : " +
                $"{indiLow:0.0} ~ {indiHigh:0.0}");


            // =====================================================
            // 4. 양쪽 모두 정상범위에 있는 샘플
            // =====================================================
            var trimmed =
                data
                    .Where(x =>
                        x.ThreeDelta >= threeLow &&
                        x.ThreeDelta <= threeHigh &&
                        x.IndividualDelta >= indiLow &&
                        x.IndividualDelta <= indiHigh)
                    .ToList();


            PrintFlowStats(
                "TRIM 0.5%",
                trimmed);

            PrintThreeStrength(
                  market,
                  trimmed);
        }

        private static void PrintFlowStats(
    string label,
    List<FlowMinute> rows)
        {
            if (rows == null ||
                rows.Count == 0)
                return;


            double corrThree =
                CalcCorrelation(
                    rows.Select(x => x.ThreeDelta),
                    rows.Select(x => x.EtfDelta));

            double corrIndi =
                CalcCorrelation(
                    rows.Select(x => x.IndividualDelta),
                    rows.Select(x => x.EtfDelta));


            Console.WriteLine();
            Console.WriteLine(
                $"----- {label} -----");

            Console.WriteLine(
                $"N = {rows.Count:N0}");

            Console.WriteLine(
                $"Corr(3, ETF)    = {corrThree:0.0000}");

            Console.WriteLine(
                $"Corr(Indi, ETF) = {corrIndi:0.0000}");


            PrintFlowDirection(
                "3 BUY ",
                rows.Where(x => x.ThreeDelta > 0),
                true);

            PrintFlowDirection(
                "3 SELL",
                rows.Where(x => x.ThreeDelta < 0),
                false);

            PrintFlowDirection(
                "I BUY ",
                rows.Where(x => x.IndividualDelta > 0),
                true);

            PrintFlowDirection(
                "I SELL",
                rows.Where(x => x.IndividualDelta < 0),
                false);
        }
        private static double CalcCorrelation(
    IEnumerable<double> xs,
    IEnumerable<double> ys)
        {
            double[] x =
                xs.ToArray();

            double[] y =
                ys.ToArray();

            if (x.Length == 0 ||
                x.Length != y.Length)
                return 0;


            double mx =
                x.Average();

            double my =
                y.Average();

            double cov = 0;
            double vx = 0;
            double vy = 0;


            for (int i = 0;
                 i < x.Length;
                 i++)
            {
                double dx =
                    x[i] - mx;

                double dy =
                    y[i] - my;

                cov += dx * dy;
                vx += dx * dx;
                vy += dy * dy;
            }


            if (vx <= 0 ||
                vy <= 0)
                return 0;


            return
                cov /
                Math.Sqrt(vx * vy);
        }
        private static void PrintFlowDirection(
    string label,
    IEnumerable<FlowMinute> source,
    bool positive)
        {
            List<FlowMinute> rows =
                source.ToList();

            if (rows.Count == 0)
                return;


            int same = 0;
            int opposite = 0;
            int zero = 0;

            double etfSum = 0;
            double flowSum = 0;


            foreach (FlowMinute x in rows)
            {
                etfSum +=
                    x.EtfDelta;

                double flow =
                    label.StartsWith("3")
                        ? x.ThreeDelta
                        : x.IndividualDelta;

                flowSum += flow;


                if (x.EtfDelta == 0)
                {
                    zero++;
                    continue;
                }


                if (positive)
                {
                    if (x.EtfDelta > 0)
                        same++;
                    else
                        opposite++;
                }
                else
                {
                    if (x.EtfDelta < 0)
                        same++;
                    else
                        opposite++;
                }
            }


            int directional =
                same + opposite;

            double rate =
                directional > 0
                    ? same * 100.0 / directional
                    : 0;

            double etfMean =
                etfSum / rows.Count;

            double flowMean =
                flowSum / rows.Count;


            Console.WriteLine(
                $"{label,-7}" +
                $" N={rows.Count,7:N0}" +
                $" Same={same,7:N0}" +
                $" Opp={opposite,7:N0}" +
                $" Zero={zero,6:N0}" +
                $" Rate={rate,6:0.00}%" +
                $" ETFMean={etfMean,8:0.0000}%" +
                $" FlowMean={flowMean,9:0.0}");
        }
    }

    public class FlowMinute
    {
        public string Market;
        public int Date;
        public int Time;

        // 같은 1분 ETF 변화(%)
        public double EtfDelta;

        // 각 주체 1분 증분
        public double ProDelta;
        public double ForeignDelta;
        public double InstitutionDelta;
        public double IndividualDelta;

        // 3주체 1분 합
        public double ThreeDelta;


        // ---------------------------------------------
        // 추가 : 3분 / 5분 누적
        // ---------------------------------------------
        public double Three3;
        public double Three5;

        public double Etf3;
        public double Etf5;

        public bool Valid3;
        public bool Valid5;
    }
}

