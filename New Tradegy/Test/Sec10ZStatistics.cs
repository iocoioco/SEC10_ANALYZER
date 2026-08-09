using New_Tradegy.Library.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace New_Tradegy.Library.PostProcessing
{
    public static class Sec10ZStatistics
    {
        private const int StartTime = 90330000;

        // 상/하 각각 1%
        private const double TrimRatio = 0.01;

        private class MarketSamples
        {
            public List<double> Nq = new List<double>();
            public List<double> Etf = new List<double>();

            public List<double> Pro = new List<double>();
            public List<double> Foreign = new List<double>();

            public List<double> Inst = new List<double>();
            public List<double> Retail = new List<double>();

            public List<double> M10Diff = new List<double>();
            public List<double> M10Sum = new List<double>();
        }

        private class StatResult
        {
            public string Name;

            public int RawCount;
            public int TrimmedCount;

            public double Mean;
            public double Std;

            public double Min;
            public double Max;
        }

        private static MarketSamples _kospi;
        private static MarketSamples _kosdaq;

        // =========================================================
        // 1. 전체 통계 시작 전에 한 번 호출
        // =========================================================
        public static void Reset()
        {
            _kospi = new MarketSamples();
            _kosdaq = new MarketSamples();
        }



        public static void Collect(
int[,] data,
int count,
bool isKospi)
        {
            // --------------------------------------------------
            // 기본 검사
            // --------------------------------------------------
            if (data == null || count <= 1)
                return;

            MarketSamples market =
                isKospi
                    ? _kospi
                    : _kosdaq;

            if (market == null)
                return;

            // 실제 배열 행 수보다 count가 클 경우 방어
            int rowCount =
                Math.Min(
                    count,
                    data.GetLength(0));

            if (rowCount <= 1)
                return;

            int columnCount =
                data.GetLength(1);

            // --------------------------------------------------
            // int[,] → List<int[]>
            //
            // Sec10Store 원본은 절대로 변경하지 않는다.
            // 복사한 rows에서만 수급 후처리를 수행한다.
            // --------------------------------------------------
            var rows =
                new List<int[]>(rowCount);

            for (int i = 0;
                 i < rowCount;
                 i++)
            {
                int[] row =
                    new int[columnCount];

                for (int c = 0;
                     c < columnCount;
                     c++)
                {
                    row[c] =
                        data[i, c];
                }

                rows.Add(row);
            }

            // --------------------------------------------------
            // 실전 Sec10과 동일한 수급 후처리
            //
            // Pro
            //     누계 → 10초 증감
            //
            // Foreign
            //     누계 → 10초 증감
            //
            // Institution
            //     누계 → 10초 증감
            //          → 지연 발표값 분배
            //
            // Retail
            //     누계 → 10초 증감
            //          → 지연 발표값 분배
            //
            // 중요:
            // 원본 data가 아니라 복사본 rows를 변경한다.
            // --------------------------------------------------
            Sec10FlowProcessor.PostProcessAccumulatedFlows(
                rows);

            // --------------------------------------------------
            // Z 통계용 Sample 수집
            //
            // CollectNormal10Sec() 내부에서:
            //
            // 1. StartTime(09:04:00) 이전 제외
            //
            // 2. 직전 row와 현재 row의 시간 간격 검사
            //       8초 <= gap <= 12초
            //
            // 3. ETF / NQ
            //       절대값이므로 현재 - 직전
            //
            // 4. Pro / Foreign / Inst / Retail
            //       위 Processor에서 이미
            //       10초 증감/분배 완료되었으므로
            //       현재 row 값을 그대로 사용
            //
            // 5. Diff / Sum
            //       원래 10초 구간값이므로 그대로 사용
            //
            // --------------------------------------------------
            CollectNormal10Sec(
                rows,
                market);
        }
        private static void CollectNormal10Sec(
     List<int[]> rows,
     MarketSamples market)
        {
            if (rows == null || rows.Count <= 1)
                return;

            for (int i = 1; i < rows.Count; i++)
            {
                int[] row = rows[i];
                int[] prevRow = rows[i - 1];

                if (row == null || prevRow == null)
                    continue;

                int time =
                    row[(int)Sec10Col.Time];

                // 09:04:00 이후만
                if (time < 90400000)
                    continue;

                int prevTime =
                    prevRow[(int)Sec10Col.Time];

                double gapMs =
                    TimeUtils.ElapsedMillisecondsDouble(
                        prevTime,
                        time);

                // 정상적인 Sec10 간격만
                if (gapMs < 8000.0 ||
                    gapMs > 12000.0)
                {
                    continue;
                }

                // NQ / ETF는 절대값 → 10초 증감
                market.Nq.Add(
                    row[(int)Sec10Col.Nq] -
                    prevRow[(int)Sec10Col.Nq]);

                market.Etf.Add(
                    row[(int)Sec10Col.Etf] -
                    prevRow[(int)Sec10Col.Etf]);

                // 수급은 Processor에서 이미
                // delta / distribution 완료
                market.Pro.Add(
                    row[(int)Sec10Col.ProAcc]);

                market.Foreign.Add(
                    row[(int)Sec10Col.ForAcc]);

                market.Inst.Add(
                    row[(int)Sec10Col.InstAcc]);

                market.Retail.Add(
                    row[(int)Sec10Col.IndiAcc]);

                // Mul은 이미 10초 값
                market.M10Diff.Add(
                    row[(int)Sec10Col.Diff]);

                market.M10Sum.Add(
                    row[(int)Sec10Col.Sum]);
            }
        }
        // =========================================================
        // NQ / ETF / Pro / Foreign / Mul
        // 정상 10초 간격일 때만 사용
        // =========================================================


        // =========================================================
        // 기관 / 개인
        //
        // 발표 시점 사이 누계 변화량을
        // elapsed 10초 구간 수로 균등 분배
        //
        // 예)
        // 1000 → 1090
        // elapsed = 90 sec
        //
        // +90 / 9 = +10 을 9개 sample로 추가
        // =========================================================
        private static void CollectDistributedSupply(
            int[,] data,
            int count,
            int column,
            List<double> output)
        {
            int firstIndex = -1;

            // 09:03:30 이후 첫 행
            for (int i = 0; i < count; i++)
            {
                if (data[i, 0] >= StartTime)
                {
                    firstIndex = i;
                    break;
                }
            }

            if (firstIndex < 0 ||
                firstIndex >= count - 1)
            {
                return;
            }

            int lastValue =
                data[firstIndex, column];

            int lastTime =
                data[firstIndex, 0];

            // 첫 값은 기준값으로만 사용.
            // 그 자체를 증감으로 사용하지 않는다.
            for (int i = firstIndex + 1;
                 i < count;
                 i++)
            {
                int currentValue =
                    data[i, column];

                // 발표값 변화 없음
                if (currentValue == lastValue)
                    continue;

                int currentTime =
                    data[i, 0];

                double elapsedMs =
                    TimeUtils.ElapsedMillisecondsDouble(
                        lastTime,
                        currentTime);

                if (elapsedMs <= 0)
                {
                    lastValue = currentValue;
                    lastTime = currentTime;
                    continue;
                }

                // elapsed 시간을 10초 bucket 수로 환산
                int bucketCount =
                    (int)Math.Round(
                        elapsedMs / 10000.0);

                // 비정상적인 경우 제외
                if (bucketCount != 8 && bucketCount != 9)
                {
                    lastValue = currentValue;
                    lastTime = currentTime;
                    continue;
                }

                double delta =
                    currentValue - lastValue;

                double perBucket =
                    delta / bucketCount;

                for (int k = 0;
                     k < bucketCount;
                     k++)
                {
                    output.Add(perBucket);
                }

                lastValue = currentValue;
                lastTime = currentTime;
            }
        }

        // =========================================================
        // 3. 모든 날짜 Collect 완료 후 호출
        // =========================================================
        public static void CalculateAndSave()
        {
            string dir =
                @"C:\BJS\Study\지수10초\Statistics";

            Directory.CreateDirectory(dir);

            SaveMarket(
                Path.Combine(
                    dir,
                    "KOSPI_Z_STATS.txt"),
                _kospi);

            SaveMarket(
                Path.Combine(
                    dir,
                    "KOSDAQ_Z_STATS.txt"),
                _kosdaq);
        }

        private static void SaveMarket(
            string path,
            MarketSamples market)
        {
            var results =
                new List<StatResult>();

            results.Add(
                Calculate("NQ_D10", market.Nq));

            results.Add(
                Calculate("ETF_D10", market.Etf));

            results.Add(
                Calculate("PRO_D10", market.Pro));

            results.Add(
                Calculate("FOR_D10", market.Foreign));

            results.Add(
                Calculate("INST_D10", market.Inst));

            results.Add(
                Calculate("RETAIL_D10", market.Retail));

            results.Add(
                Calculate("M10_DIFF", market.M10Diff));

            results.Add(
                Calculate("M10_SUM", market.M10Sum));

            var sb = new StringBuilder();

            sb.AppendLine(
                "item," +
                "rawCount," +
                "trimmedCount," +
                "mean," +
                "std," +
                "min," +
                "max");

            foreach (var r in results)
            {
                sb.AppendLine(string.Join(",",
                    r.Name,
                    r.RawCount,
                    r.TrimmedCount,
                    ToText(r.Mean),
                    ToText(r.Std),
                    ToText(r.Min),
                    ToText(r.Max)));
            }

            File.WriteAllText(
                path,
                sb.ToString(),
                Encoding.UTF8);
        }

        // =========================================================
        // 상하 1% 제거 후 mean / std
        // =========================================================
        private static StatResult Calculate(
            string name,
            List<double> source)
        {
            var result =
                new StatResult
                {
                    Name = name
                };

            if (source == null ||
                source.Count == 0)
            {
                return result;
            }

            result.RawCount =
                source.Count;

            List<double> sorted =
                source
                .OrderBy(x => x)
                .ToList();

            int cut =
                (int)(
                    sorted.Count *
                    TrimRatio);

            if (cut * 2 >= sorted.Count)
                cut = 0;

            List<double> values =
                sorted
                .Skip(cut)
                .Take(
                    sorted.Count -
                    cut * 2)
                .ToList();

            if (values.Count == 0)
                return result;

            result.TrimmedCount =
                values.Count;

            double mean =
                values.Average();

            double sumSq = 0.0;

            foreach (double value in values)
            {
                double d =
                    value - mean;

                sumSq +=
                    d * d;
            }

            // population std
            double std =
                Math.Sqrt(
                    sumSq /
                    values.Count);

            result.Mean = mean;
            result.Std = std;

            result.Min =
                values.First();

            result.Max =
                values.Last();

            return result;
        }

        private static string ToText(
            double value)
        {
            return value.ToString(
                "0.########",
                CultureInfo.InvariantCulture);
        }
    }
}