using New_Tradegy.Library.UI.KeyBindings;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace New_Tradegy.Library.Utils
{
    public static class TimerHelper
    {
        private static DateTime _savedTime; // Stores the start time

        public static void Start()
        {
            _savedTime = DateTime.Now; // Start the timer
        }

        public static double Stop()
        {
            return (DateTime.Now - _savedTime).TotalMilliseconds; // Return elapsed time
        }
    }

    public class TaskTimer
    {
        private readonly string _taskName;
        private readonly TimeSpan _interval;
        private Stopwatch _intervalStopwatch;

        public TaskTimer(string taskName, TimeSpan? interval = null)
        {
            _taskName = taskName;
            _interval = interval ?? TimeSpan.FromMinutes(10);
            _intervalStopwatch = Stopwatch.StartNew();
        }

        public async Task TryMeasureAndLogAsync(Func<Task> action)
        {

            if (_intervalStopwatch.Elapsed < _interval)
            {
                await action();
                return;
            }

            _intervalStopwatch.Restart();

            //Stopwatch sw = Stopwatch.StartNew();
            await action();
            //sw.Stop();

            //TimeUtils.LogTaskTime(_taskName, sw.Elapsed);
        }
    }
    // Don_vare



    public sealed class TimingMeter
    {
        private readonly string _name;
        private readonly int _cap;
        private readonly long[] _buf;
        private int _pos;
        private int _count;
        private long _sum;
        private long _max;

        private readonly Stopwatch _sw = new Stopwatch();
        private readonly object _sync = new object();
        private int _tickCounter; // 몇 회마다 요약 출력할지

        // cap: 유지할 샘플 개수 (예: 600개 = 5분치 @ 500ms)
        // printEvery: 몇 회마다 한 줄 요약 출력할지 (예: 20회)
        public TimingMeter(string name, int cap = 600, int printEvery = 20)
        {
            _name = name;
            _cap = Math.Max(10, cap);
            _buf = new long[_cap];
            _tickCounter = printEvery <= 0 ? 20 : printEvery;
        }

        public void Start() { _sw.Restart(); }

        public void StopAndRecord()
        {
            _sw.Stop();
            long ms = _sw.ElapsedMilliseconds;

            lock (_sync)
            {
                // 버퍼 회전
                if (_count < _cap) _count++;
                else _sum -= _buf[_pos];

                _buf[_pos] = ms;
                _pos = (_pos + 1) % _cap;

                // 집계
                _sum += ms;
                if (ms > _max) _max = ms;

                // 주기적 요약 출력
                if (--_tickCounter <= 0)
                {
                    _tickCounter = 20; // 20회마다 출력
                    PrintSummary();
                }
            }
        }

        public void PrintSummary()
        {
            if (_count == 0) return;

            // 복사 후 정렬해 p95 계산 (O(n log n), 버퍼가 작으니 OK)
            var tmp = new long[_count];
            int idx = 0;
            for (int i = 0; i < _cap && idx < _count; i++)
            {
                // 최근 순서와 무관, 통계만 필요
                tmp[idx++] = _buf[i];
            }
            Array.Sort(tmp);

            long p95 = tmp[(int)Math.Max(0, Math.Min(_count - 1, Math.Round(_count * 0.95) - 1))];
            long avg = _sum / _count;
            long max = _max;

            var sb = new StringBuilder();
            sb.AppendFormat("{0:HH:mm:ss} ▶ {1}  n={2}  avg={3} ms, p95={4} ms, max={5} ms",
                DateTime.Now, _name, _count, avg, p95, max);

            //Debug.WriteLine(sb.ToString());
        }

        public void ResetMax() { lock (_sync) { _max = 0; } }
    }


    internal class TimeUtils
    {

        /// <summary>
        /// Logs the elapsed time of a named task to a file with timestamp.
        /// </summary>
        /// <param name="taskName">Name of the measured task</param>
        /// <param name="elapsed">Elapsed time from Stopwatch</param>
        /// <param name="thresholdMs">Optional threshold to log only slow operations</param>
        /// <param name="asCsv">Whether to log as CSV (default: false)</param>

        private static readonly object LogLock = new object();
        private static readonly string LogPath = @"C:\BJS\Z Data\tasksTimeDuration.txt";
        private static readonly TimeZoneInfo _kst =
        TimeZoneInfo.FindSystemTimeZoneById("Korea Standard Time");

        public static DateTime GetKstNow()
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _kst);
        }
        /// <summary>
        /// Logs the execution time of a task.
        /// </summary>
        /// <param name="taskName">The task name</param>
        /// <param name="elapsed">The elapsed duration</param>
        /// <param name="thresholdMs">Only log if duration ≥ threshold (0 = log all)</param>
        /// <param name="asCsv">Log in CSV format if true</param>
        public static void LogTaskTime(string taskName, TimeSpan elapsed, double thresholdMs = 0, bool asCsv = false)
        {
            try
            {
                double elapsedMs = elapsed.TotalMilliseconds;

                if (thresholdMs > 0 && elapsedMs < thresholdMs)
                    return;

                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                string line = asCsv
                    ? $"{timestamp},{taskName},{elapsedMs:F2}"
                    : $"{timestamp} | {taskName,-20} took {elapsedMs,8:F2} ms";

                lock (LogLock)
                {
                    File.AppendAllText(LogPath, line + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                //Console.WriteLine($"[Warning] Failed to log task '{taskName}': {ex.Message}");
            }
        }


        public static int ElapsedSeconds(int fromTime, int toTime)
        {
            // fromTime, toTime : HHmmss 형식 (예: 93005 = 09:30:05)

            int h1 = fromTime / 10000;
            int m1 = (fromTime / 100) % 100;
            int s1 = fromTime % 100;

            int h2 = toTime / 10000;
            int m2 = (toTime / 100) % 100;
            int s2 = toTime % 100;

            TimeSpan t1 = new TimeSpan(h1, m1, s1);
            TimeSpan t2 = new TimeSpan(h2, m2, s2);

            return (int)(t2 - t1).TotalSeconds;
        }


        public static int ElapsedMillisecondsInteger(long t1, long t2)
        {
            // Parse long time (HHmmssfff) to DateTime
            DateTime Parse(long t)
            {
                int hour = (int)(t / 10000000);
                int minute = (int)((t / 100000) % 100);
                int second = (int)((t / 1000) % 100);
                int millisecond = (int)(t % 1000);
                return new DateTime(1, 1, 1, hour, minute, second, millisecond);
            }

            var dt1 = Parse(t1);
            var dt2 = Parse(t2);

            return (int)Math.Abs((dt2 - dt1).TotalMilliseconds);
        }

        public static double ElapsedMillisecondsDouble(long fromTime, long toTime)
        {
            int h1 = (int)(fromTime / 10000000);
            int m1 = (int)((fromTime / 100000) % 100);
            int s1 = (int)((fromTime / 1000) % 100);
            int f1 = (int)(fromTime % 1000);

            int h2 = (int)(toTime / 10000000);
            int m2 = (int)((toTime / 100000) % 100);
            int s2 = (int)((toTime / 1000) % 100);
            int f2 = (int)(toTime % 1000);

            TimeSpan t1 = new TimeSpan(0, h1, m1, s1, f1);
            TimeSpan t2 = new TimeSpan(0, h2, m2, s2, f2);

            return (t2 - t1).TotalMilliseconds;
        }




        public static int TimeToInt(string value)
        {
            string[] words = value.Split(':');
            return Convert.ToInt32(words[0]) * 10000 +
                Convert.ToInt32(words[1]) * 100 +
                Convert.ToInt32(words[2]);
        }


        public static void MinuteAdvanceRetreat(int advance_lines)
        {
            if (advance_lines == 0)
            {
                g.Npts[1] = g.EndNptsBeforeExtend;
                g.EndNptsBeforeExtend = 0;
                g.EndNptsExtendedOrNot = false;
            }
            else
            {
                g.EndNptsBeforeExtend = g.Npts[1];
                g.Npts[1] += advance_lines; // expedientH
                if (g.Npts[1] > g.TestMaximumRow)
                    g.Npts[1] = g.TestMaximumRow;

                g.EndNptsExtendedOrNot = true;
            }
        }


    }





}
