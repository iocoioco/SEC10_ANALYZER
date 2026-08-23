using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace New_Tradegy.Test
{
    public static class NqMotionIntervalAnalyzer
    {
        private const string Root = @"C:\BJS\Study\지수10초";

        private static readonly string[] MotionNames =
        {
            "A20", "A30", "A40",
            "A60", "A90", "A120", "A180"
        };

        private static readonly int[] FutureSeconds =
        {
            10, 20, 30
        };

        // 절대 Motion 구간
        private static readonly double[] MotionBounds =
        {
            0.00,
            0.01,
            0.02,
            0.05,
            0.10,
            0.20,
            0.50,
            double.MaxValue
        };

        private sealed class Row
        {
            public int Time;
            public double Etf;
            public Dictionary<string, double> Motion =
                new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        }

        private sealed class CorrelationResult
        {
            public int Count;
            public double R;
            public double R2;
            public double DirectionRate;
        }

        private sealed class RangeResult
        {
            public int Count;

            // 실제 ETF 미래 변화
            public double SumFuture;
            public double SumAbsFuture;

            // Motion 방향 기준 수익:
            // 양수 Motion이면 미래 ETF 상승,
            // 음수 Motion이면 미래 ETF 하락일 때 양수
            public double SumAlignedFuture;

            public int DirectionMatch;
            public int FutureUp;
            public int FutureDown;
            public int FutureZero;
        }
        public static void Run()
        {
            RunAllDates(
                "KOSPI_ANALYZED_V1.txt",
                "NqMotionIntervalSummary_KOSPI.txt",
                "NqMotionRangeSummary_KOSPI.txt");

            RunAllDates(
                "KOSDAQ_ANALYZED_V1.txt",
                "NqMotionIntervalSummary_KOSDAQ.txt",
                "NqMotionRangeSummary_KOSDAQ.txt");
        }
        private static void RunAllDates(
            string analyzedFileName,
            string correlationOutputName,
            string rangeOutputName)
        {
            var allRows = new List<Row>();

            if (!Directory.Exists(Root))
                return;

            foreach (string dir in Directory.GetDirectories(Root))
            {
                string path = FindAnalyzedFile(dir, analyzedFileName);

                if (path == null)
                    continue;

                LoadRows(path, allRows);
            }

            if (allRows.Count < 10)
                return;

            // 날짜별 파일을 합쳤으므로 시간순 정렬은 하지 않는다.
            // 각 파일 안에서만 미래행을 연결해야 하므로 LoadRows에서
            // 날짜 경계 구분용 Time=-1 행을 넣는다.

            WriteCorrelationSummary(
                allRows,
                Path.Combine(Root, correlationOutputName));

            WriteRangeSummary(
                allRows,
                Path.Combine(Root, rangeOutputName));
        }
        private static string FindAnalyzedFile(
            string directory,
            string analyzedFileName)
        {
            string exactPath = Path.Combine(directory, analyzedFileName);

            if (File.Exists(exactPath))
                return exactPath;

            string withoutExtension =
                Path.GetFileNameWithoutExtension(analyzedFileName);

            string pathWithoutExtension =
                Path.Combine(directory, withoutExtension);

            if (File.Exists(pathWithoutExtension))
                return pathWithoutExtension;

            return null;
        }
        private static void LoadRows(
    string path,
    List<Row> destination)
        {
            string[] lines;

            try
            {
                lines = File.ReadAllLines(path, Encoding.UTF8);
            }
            catch
            {
                return;
            }

            if (lines.Length < 2)
                return;

            string[] header = SplitCsv(lines[0]);

            int timeIndex = FindColumn(header, "time");
            int etfIndex = FindColumn(header, "etf");

            if (timeIndex < 0 || etfIndex < 0)
                return;

            var motionIndexes =
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (string motionName in MotionNames)
                motionIndexes[motionName] = FindColumn(header, motionName);

            int beforeCount = destination.Count;
            int? previousTimeMs = null;

            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                    continue;

                string[] values = SplitCsv(lines[i]);

                if (!TryGetInt(values, timeIndex, out int time))
                    continue;

                if (!TryGetDouble(values, etfIndex, out double etf))
                    continue;

                if (!TryConvertTimeToMilliseconds(time, out int currentTimeMs))
                    continue;

                if (previousTimeMs.HasValue)
                {
                    int elapsedMs = currentTimeMs - previousTimeMs.Value;

                    // 정상 저장 간격은 약 8~12초.
                    // 15초 초과, 중복 또는 시간 역행이면 새로운 구간으로 처리
                    if (elapsedMs <= 0 || elapsedMs > 15000)
                    {
                        if (destination.Count > 0 &&
                            destination[destination.Count - 1].Time >= 0)
                        {
                            destination.Add(new Row
                            {
                                Time = -1,
                                Etf = 0.0
                            });
                        }
                    }
                }

                var row = new Row
                {
                    Time = time,
                    Etf = etf
                };

                foreach (string motionName in MotionNames)
                {
                    int index = motionIndexes[motionName];

                    if (TryGetDouble(values, index, out double motion))
                        row.Motion[motionName] = motion;
                    else
                        row.Motion[motionName] = 0.0;
                }

                destination.Add(row);
                previousTimeMs = currentTimeMs;
            }

            // 다음 날짜 파일과 연결되지 않도록 날짜 경계 추가
            if (destination.Count > beforeCount &&
                destination[destination.Count - 1].Time >= 0)
            {
                destination.Add(new Row
                {
                    Time = -1,
                    Etf = 0.0
                });
            }
        }
        private static bool TryConvertTimeToMilliseconds(
            int time,
            out int milliseconds)
        {
            milliseconds = 0;

            int hour;
            int minute;
            int second;
            int ms;

            // hhmmssfff 형식
            if (time > 235959)
            {
                hour = time / 10000000;
                minute = (time / 100000) % 100;
                second = (time / 1000) % 100;
                ms = time % 1000;
            }
            // hhmmss 형식도 허용
            else
            {
                hour = time / 10000;
                minute = (time / 100) % 100;
                second = time % 100;
                ms = 0;
            }

            if (hour < 0 || hour > 23 ||
                minute < 0 || minute > 59 ||
                second < 0 || second > 59)
            {
                return false;
            }

            milliseconds =
                hour * 3600000 +
                minute * 60000 +
                second * 1000 +
                ms;

            return true;
        }
        private static void WriteCorrelationSummary(
            List<Row> rows,
            string outputPath)
        {
            var sb = new StringBuilder();

            sb.AppendLine(
                "Motion,FutureSec,Count,R,R2,DirectionRate");

            foreach (string motionName in MotionNames)
            {
                foreach (int futureSec in FutureSeconds)
                {
                    int futureRows = futureSec / 10;

                    CorrelationResult result =
                        AnalyzeCorrelation(
                            rows,
                            motionName,
                            futureRows);

                    sb.AppendLine(string.Format(
                        CultureInfo.InvariantCulture,
                        "{0},{1},{2},{3:F6},{4:F6},{5:F4}",
                        motionName,
                        futureSec,
                        result.Count,
                        result.R,
                        result.R2,
                        result.DirectionRate));
                }
            }

            File.WriteAllText(
                outputPath,
                sb.ToString(),
                Encoding.UTF8);
        }
        private static CorrelationResult AnalyzeCorrelation(
            List<Row> rows,
            string motionName,
            int futureRows)
        {
            int count = 0;
            int directionMatch = 0;
            int directionCount = 0;

            double sumX = 0.0;
            double sumY = 0.0;
            double sumXX = 0.0;
            double sumYY = 0.0;
            double sumXY = 0.0;

            for (int i = 0; i + futureRows < rows.Count; i++)
            {
                Row now = rows[i];
                Row future = rows[i + futureRows];

                if (!IsSameTradingBlock(rows, i, futureRows))
                    continue;

                if (!now.Motion.TryGetValue(
                    motionName,
                    out double motion))
                    continue;

                double futureChange = future.Etf - now.Etf;

                if (!IsFinite(motion) ||
                    !IsFinite(futureChange))
                    continue;

                count++;

                sumX += motion;
                sumY += futureChange;
                sumXX += motion * motion;
                sumYY += futureChange * futureChange;
                sumXY += motion * futureChange;

                if (motion != 0.0 && futureChange != 0.0)
                {
                    directionCount++;

                    if (Math.Sign(motion) ==
                        Math.Sign(futureChange))
                    {
                        directionMatch++;
                    }
                }
            }

            double numerator =
                count * sumXY - sumX * sumY;

            double denominatorX =
                count * sumXX - sumX * sumX;

            double denominatorY =
                count * sumYY - sumY * sumY;

            double denominator =
                Math.Sqrt(
                    Math.Max(0.0, denominatorX) *
                    Math.Max(0.0, denominatorY));

            double r = denominator > 0.0
                ? numerator / denominator
                : 0.0;

            return new CorrelationResult
            {
                Count = count,
                R = r,
                R2 = r * r,
                DirectionRate = directionCount > 0
                    ? (double)directionMatch / directionCount
                    : 0.0
            };
        }
        private static void WriteRangeSummary(
            List<Row> rows,
            string outputPath)
        {
            var sb = new StringBuilder();

            sb.AppendLine(
                "Motion,FutureSec,Direction,RangeMin,RangeMax," +
                "Count,AvgFuture,AvgAbsFuture,AvgAlignedFuture," +
                "DirectionRate,UpRate,DownRate,ZeroRate");

            foreach (string motionName in MotionNames)
            {
                foreach (int futureSec in FutureSeconds)
                {
                    int futureRows = futureSec / 10;

                    // 양수와 음수를 분리
                    WriteDirectionRanges(
                        sb,
                        rows,
                        motionName,
                        futureSec,
                        futureRows,
                        +1);

                    WriteDirectionRanges(
                        sb,
                        rows,
                        motionName,
                        futureSec,
                        futureRows,
                        -1);
                }
            }

            File.WriteAllText(
                outputPath,
                sb.ToString(),
                Encoding.UTF8);
        }
        private static void WriteDirectionRanges(
            StringBuilder sb,
            List<Row> rows,
            string motionName,
            int futureSec,
            int futureRows,
            int motionDirection)
        {
            for (int rangeIndex = 0;
                 rangeIndex < MotionBounds.Length - 1;
                 rangeIndex++)
            {
                double min = MotionBounds[rangeIndex];
                double max = MotionBounds[rangeIndex + 1];

                RangeResult result =
                    AnalyzeRange(
                        rows,
                        motionName,
                        futureRows,
                        motionDirection,
                        min,
                        max);

                double avgFuture = result.Count > 0
                    ? result.SumFuture / result.Count
                    : 0.0;

                double avgAbsFuture = result.Count > 0
                    ? result.SumAbsFuture / result.Count
                    : 0.0;

                double avgAlignedFuture = result.Count > 0
                    ? result.SumAlignedFuture / result.Count
                    : 0.0;

                double directionRate = result.Count > 0
                    ? (double)result.DirectionMatch / result.Count
                    : 0.0;

                double upRate = result.Count > 0
                    ? (double)result.FutureUp / result.Count
                    : 0.0;

                double downRate = result.Count > 0
                    ? (double)result.FutureDown / result.Count
                    : 0.0;

                double zeroRate = result.Count > 0
                    ? (double)result.FutureZero / result.Count
                    : 0.0;

                string directionText =
                    motionDirection > 0 ? "UP" : "DOWN";

                string maxText =
                    max == double.MaxValue
                        ? "INF"
                        : max.ToString(
                            "0.000",
                            CultureInfo.InvariantCulture);

                sb.AppendLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0},{1},{2},{3:0.000},{4},{5}," +
                    "{6:F6},{7:F6},{8:F6}," +
                    "{9:F4},{10:F4},{11:F4},{12:F4}",
                    motionName,
                    futureSec,
                    directionText,
                    min,
                    maxText,
                    result.Count,
                    avgFuture,
                    avgAbsFuture,
                    avgAlignedFuture,
                    directionRate,
                    upRate,
                    downRate,
                    zeroRate));
            }
        }
        private static RangeResult AnalyzeRange(
            List<Row> rows,
            string motionName,
            int futureRows,
            int motionDirection,
            double minAbsMotion,
            double maxAbsMotion)
        {
            var result = new RangeResult();

            for (int i = 0; i + futureRows < rows.Count; i++)
            {
                if (!IsSameTradingBlock(rows, i, futureRows))
                    continue;

                Row now = rows[i];
                Row future = rows[i + futureRows];

                if (!now.Motion.TryGetValue(
                    motionName,
                    out double motion))
                    continue;

                if (motionDirection > 0 && motion <= 0.0)
                    continue;

                if (motionDirection < 0 && motion >= 0.0)
                    continue;

                double absMotion = Math.Abs(motion);

                if (absMotion < minAbsMotion ||
                    absMotion >= maxAbsMotion)
                    continue;

                double futureChange =
                    future.Etf - now.Etf;

                if (!IsFinite(futureChange))
                    continue;

                double alignedFuture =
                    futureChange * motionDirection;

                result.Count++;
                result.SumFuture += futureChange;
                result.SumAbsFuture += Math.Abs(futureChange);
                result.SumAlignedFuture += alignedFuture;

                if (alignedFuture > 0.0)
                    result.DirectionMatch++;

                if (futureChange > 0.0)
                    result.FutureUp++;
                else if (futureChange < 0.0)
                    result.FutureDown++;
                else
                    result.FutureZero++;
            }

            return result;
        }
        private static bool IsSameTradingBlock(
            List<Row> rows,
            int startIndex,
            int futureRows)
        {
            int endIndex = startIndex + futureRows;

            if (endIndex >= rows.Count)
                return false;

            for (int i = startIndex; i <= endIndex; i++)
            {
                if (rows[i].Time < 0)
                    return false;
            }

            return true;
        }
        private static int FindColumn(
            string[] header,
            string columnName)
        {
            for (int i = 0; i < header.Length; i++)
            {
                if (string.Equals(
                    header[i].Trim(),
                    columnName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }
        private static string[] SplitCsv(string line)
        {
            return line.Split(',');
        }
        private static bool TryGetInt(
            string[] values,
            int index,
            out int value)
        {
            value = 0;

            if (index < 0 || index >= values.Length)
                return false;

            return int.TryParse(
                values[index].Trim(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out value);
        }
        private static bool TryGetDouble(
            string[] values,
            int index,
            out double value)
        {
            value = 0.0;

            if (index < 0 || index >= values.Length)
                return false;

            return double.TryParse(
                values[index].Trim(),
                NumberStyles.Float |
                NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture,
                out value);
        }
        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) &&
                   !double.IsInfinity(value);
        }
    }
}

