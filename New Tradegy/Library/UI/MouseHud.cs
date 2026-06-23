// MouseHud.cs
using New_Tradegy.Library.Models;
using New_Tradegy.Library.PostProcessing;
using New_Tradegy.Library.Utils;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace New_Tradegy.Library.UI
{
    /// <summary>
    /// Mouse HUD Controller
    /// Base(1줄): NQ|KOSPI|KOSDAQ (분20NQ|KOSPI|KOSDAQ)  // 라벨 없이 숫자만
    /// Extra(최대 2줄): 유연한 추가 정보
    /// Overlay(최대 3줄): 임시 메시지 (Overlay 뜨면 Extra는 숨김)
    /// </summary>
    public static class MouseHud
    {
        private struct NqOneSecPoint
        {
            public DateTime TimeUtc;
            public double Value;

            public NqOneSecPoint(DateTime timeUtc, double value)
            {
                TimeUtc = timeUtc;
                Value = value;
            }
        }

        private static readonly Queue<NqOneSecPoint> _nq1s =
            new Queue<NqOneSecPoint>();

        private const int NqMotionN = 60;





        public static int UpdateIntervalMs = 250;

        // ✅ Flash는 "급변"에만 (원하면 유지/조정)
        public static double WindowSeconds = 3.0;
        public static double FlashThresholdPct = 0.01;
        public static double FlashCooldownSeconds = 1.0;

        public static int DefaultOverlayDurationMs = 3000;
        public static int Decimals = 2;

        /// <summary>장중 아니면 Base 완전 숨김</summary>
        public static bool HideBaseWhenTest = true;

        /// <summary>색상 레벨 기준(절대값, %)</summary>
        public static double L1Abs = 0.20;
        public static double L2Abs = 0.60;

        // ===== internals =====
        private static Timer _timer;
        private static bool _started;
        static string _lastG1 = "";
        static string _lastG2 = "";
        static string _lastG3 = "";
        static Color _lastC1, _lastC2, _lastC3;


        private static DateTime _lastFlashUtc = DateTime.MinValue;

        private static readonly SeriesBuffer _nq = new SeriesBuffer();
        private static readonly SeriesBuffer _kospi = new SeriesBuffer();
        private static readonly SeriesBuffer _kosdaq = new SeriesBuffer();

        // 분20 고정(요청)
        private const double SecWindowForParen = 20.0;

        // MouseHud.cs (클래스 내부 아무 곳)
        /// <summary>
        /// replaced 03021437
        /// </summary>
        /// <param name="v"></param>
        /// <returns></returns>
        static string FmtPct3(double v) => v.ToString("+0.000;-0.000;0.000");
        static string FmtPct2(double v) => v.ToString("+0.00;-0.00;0.00");
        static string FmtIntSigned(int v) => v.ToString("+0;-0;0");

        // 2줄 기준 중립(회색) 룰
        //static bool IsNeutralNqDelta(double dnq20) => Math.Abs(dnq20) <= 0.05;  // friend 요청
        //static bool IsNeutralScore(int s) => Math.Abs(s) <= 50;                // friend 요청


        private static void AddNqOneSecond(DateTime nowUtc, double nqNow)
        {
            if (double.IsNaN(nqNow) || double.IsInfinity(nqNow))
                return;

            if (Math.Abs(nqNow) < 0.000001)
                return;

            _nq1s.Enqueue(new NqOneSecPoint(nowUtc, nqNow));

            while (_nq1s.Count > NqMotionN)
                _nq1s.Dequeue();
        }
        static Color ColorByDelta(double v)
        {
            double abs = Math.Abs(v);

            if (abs <= 0.015)
                return Color.Gray;

            if (v > 0)
            {
                if (abs < 0.02) return Color.LightCoral;
                if (abs < 0.03) return Color.Red;
                if (abs < 0.04) return Color.DarkRed;
                return Color.Maroon;
            }
            else
            {
                if (abs < 0.02) return Color.LightBlue;
                if (abs < 0.03) return Color.Blue;
                if (abs < 0.04) return Color.DarkBlue;
                return Color.Navy;
            }
        }

        static Color ColorByScore5(int s)
        {
            int abs = Math.Abs(s);

            if (abs < 30)
                return Color.Gray;

            if (s > 0)
            {
                if (abs < 50) return Color.LightCoral;
                if (abs < 70) return Color.Red;
                if (abs < 85) return Color.DarkRed;
                return Color.Maroon;
            }
            else
            {
                if (abs < 50) return Color.LightBlue;
                if (abs < 70) return Color.Blue;
                if (abs < 85) return Color.DarkBlue;
                return Color.Navy;
            }
        }

        // =========================================================
        // Public
        // =========================================================
        public static void Start()
        {
            if (_started) return;
            _started = true;

            MouseHudForm.ShowSticky();

            //_timer = new Timer { Interval = Math.Max(50, UpdateIntervalMs) };
            //_timer.Tick += (s, e) => Tick();
            //_timer.Start();
        }

        public static void Stop()
        {
            _started = false;
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Dispose();
                _timer = null;
            }

            //MouseHudForm.HideSticky(); // 20280328
        }

        /// <summary>임시 메시지(Overlay). 들어오면 overwrite + 타이머 리셋</summary>
        public static void ShowTemporary(string text, int? durationMs = null)
        {
            MouseHudForm.ShowOverlay(text, durationMs ?? DefaultOverlayDurationMs);
        }

        /// <summary>추가 2줄(Extra). duration 후 자동 해제</summary>
        public static void ShowExtra(string text, int durationMs)
        {
            SoundUtils.Sound("", "ding");
            MouseHudForm.ShowExtra(text, durationMs);
        }

        public static void ClearExtra()
        {
            MouseHudForm.ClearExtra();
        }


        // =========================================================
        // Tick
        // =========================================================

        private static DateTime _lastDnqAlertTime = DateTime.MinValue;


        private static void Tick()
        {
            MouseHudForm.SetBaseVisible(true);

            var nowUtc = DateTime.UtcNow;

            double nqNow = MajorIndex.Instance.NasdaqIndex;

            _nq.Add(nowUtc, nqNow);

            double dnq20 = DeltaBySeconds(_nq, nowUtc, 20.0);

            Color c1 = ColorByDelta(dnq20);

            string g1 = $"{FmtPct3(nqNow)}({FmtPct3(dnq20)})";

            bool changed =
                g1 != _lastG1 ||
                c1 != _lastC1;

            if (!changed)
                return;

            MouseHudForm.UpdateBase(g1, c1);

            _lastG1 = g1;
            _lastC1 = c1;
        }

        // display Nq, KospiEtf, KosdaqEtf
        public static void TickDisplayNqKospiKosdaq()
        {


            MouseHudForm.SetBaseVisible(true);

            var nowUtc = DateTime.UtcNow;

            double nqNow = MajorIndex.Instance.NasdaqIndex;
            double kpNow = MajorIndex.Instance.KospiIndex / 100.0;
            double kqNow = MajorIndex.Instance.KosdaqIndex / 100.0;

            AddNqOneSecond(nowUtc, nqNow);


            _nq.Add(nowUtc, nqNow);
            _kospi.Add(nowUtc, kpNow);
            _kosdaq.Add(nowUtc, kqNow);

            double dnq20 = DeltaBySeconds(_nq, nowUtc, 20.0);

            int kpScore = 0;
            int kqScore = 0;

            if (IndexScoreStore.TryGet("Kospi", out var kp))
                kpScore = kp.HudScore;

            if (IndexScoreStore.TryGet("Kosdaq", out var kq))
                kqScore = kq.HudScore;

            Color c1 = ColorByDelta(dnq20);
            Color c2 = ColorByScore5(kpScore);
            Color c3 = ColorByScore5(kqScore);

            var motion = MajorIndex.Instance.NqMotion;

            bool hasMotion = TryCalcNqMotion1s(out double a, out double r, out double z);

            string g1 =
                $"{FmtPct3(nqNow)}({FmtPct3(dnq20)}) " +
                (hasMotion
                    ? $"A{a:+0.00;-0.00} R{r:0.00} Z{z:+0.0;-0.0}"
                    : "A-- R-- Z--");

            string g2 = $"{FmtPct2(kpNow)}({FmtIntSigned(kpScore)})";
            string g3 = $"{FmtPct2(kqNow)}({FmtIntSigned(kqScore)})";

            bool changed =
                g1 != _lastG1 || g2 != _lastG2 || g3 != _lastG3 ||
                c1 != _lastC1 || c2 != _lastC2 || c3 != _lastC3;

            if (!changed) return;

            MouseHudForm.UpdateBase(g1, g2, g3, c1, c2, c3);

            _lastG1 = g1; _lastG2 = g2; _lastG3 = g3;
            _lastC1 = c1; _lastC2 = c2; _lastC3 = c3;
        }

        private static bool TryCalcNqMotion1s(out double a, out double r, out double z)
        {
            a = 0;
            r = 0;
            z = 0;

            if (_nq1s.Count < NqMotionN)
                return false;

            var arr = _nq1s.ToArray();
            int n = arr.Length;

            double sumX = 0, sumY = 0, sumX2 = 0, sumXY = 0;

            for (int i = 0; i < n; i++)
            {
                double x = i;
                double y = arr[i].Value;

                if (double.IsNaN(y) || double.IsInfinity(y) || Math.Abs(y) < 0.000001)
                    return false;

                sumX += x;
                sumY += y;
                sumX2 += x * x;
                sumXY += x * y;
            }

            double denom = n * sumX2 - sumX * sumX;
            if (Math.Abs(denom) < 1e-12)
                return false;

            double slope = (n * sumXY - sumX * sumY) / denom;
            double intercept = (sumY - slope * sumX) / n;

            a = slope * (n - 1);

            double meanY = sumY / n;
            double ssTot = 0;
            double ssRes = 0;

            for (int i = 0; i < n; i++)
            {
                double x = i;
                double y = arr[i].Value;
                double fit = slope * x + intercept;

                double dy = y - meanY;
                double err = y - fit;

                ssTot += dy * dy;
                ssRes += err * err;
            }

            r = ssTot > 1e-12 ? 1.0 - ssRes / ssTot : 0.0;
            if (r < 0) r = 0;
            if (r > 1) r = 1;

            var motion = MajorIndex.Instance.NqMotion;

            double stdBefore = motion.Count > 1
                ? Math.Sqrt(motion.M2A / (motion.Count - 1))
                : 0.0;

            z = stdBefore > 1e-9
                ? (a - motion.MeanA) / stdBefore
                : 0.0;

            motion.A = a;
            motion.R = r;
            motion.Z = z;

            motion.Count++;

            double delta = a - motion.MeanA;
            motion.MeanA += delta / motion.Count;

            double delta2 = a - motion.MeanA;
            motion.M2A += delta * delta2;

            return true;
        }

        private static double DeltaBySeconds(SeriesBuffer buf, DateTime nowUtc, double sec)
        {
            if (!buf.TryGetLatest(out double latest)) return 0;

            var target = nowUtc.AddSeconds(-sec);
            if (!buf.TryGetValueAtOrBefore(target, out double past)) return 0;

            return latest - past;
        }

        // =========================================================
        // Series buffer
        // =========================================================
        private sealed class SeriesBuffer
        {
            private readonly System.Collections.Generic.Queue<(DateTime t, double v)> _q
                = new System.Collections.Generic.Queue<(DateTime, double)>();

            public void Add(DateTime tUtc, double v)
            {
                _q.Enqueue((tUtc, v));

                // 너무 오래된 건 정리(최소 60초면 충분하지만 넉넉히)
                var cutoff = tUtc.AddSeconds(-120);
                while (_q.Count > 0 && _q.Peek().t < cutoff)
                    _q.Dequeue();
            }

            public bool TryGetLatest(out double v)
            {
                if (_q.Count == 0) { v = 0; return false; }
                v = default;

                // Queue라서 마지막을 바로 못 잡으니 순회(갯수 작음)
                foreach (var it in _q) v = it.v;
                return true;
            }

            public bool TryGetValueAtOrBefore(DateTime targetUtc, out double v)
            {
                v = 0;
                bool found = false;

                foreach (var it in _q)
                {
                    if (it.t <= targetUtc)
                    {
                        v = it.v;
                        found = true;
                    }
                    else
                    {
                        // queue는 시간순이므로 여기서 종료
                        break;
                    }
                }

                // target 이전 값이 없다면 가장 오래된 값으로라도(초반 안정)
                if (!found && _q.Count > 0)
                {
                    v = _q.Peek().v;
                    return true;
                }

                return found;
            }
        }
    }
}