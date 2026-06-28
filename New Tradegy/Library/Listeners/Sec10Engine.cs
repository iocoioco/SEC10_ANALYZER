using System;

namespace New_Tradegy.Library.Listeners
{
    public class HeatResult
    {
        public int Bars;

        public int CountUp;
        public int CountDown;

        public double SumNqUp;
        public double SumEtfUp;
        public double SumNqDown;
        public double SumEtfDown;

        public double BPlus;
        public double BMinus;
        public double Heat;

        // ---------- Z 누적 ----------
        public int ZCount;
        public double MeanHeat;
        public double M2Heat;
        public double StdHeat;
        public double Z;
        // ----------------------------

        public bool Valid;
    }
    public class Sec10Engine
    {
        private readonly HeatResult _heat1 = new HeatResult();
        private readonly HeatResult _heat25 = new HeatResult();
        private readonly HeatResult _heat5 = new HeatResult();

        private const int SIZE = 60;
        private const int COLS = 12;
        private readonly object _sync = new object();
        private readonly int[,] _a = new int[SIZE, COLS];
        private readonly int[] _current10s = new int[COLS];
        private int _count = 0;
        private int _lastBucket = -1;
        public int Count {get
            {
                lock (_sync)
                    return _count;
            }}

        // 원본 직접 노출 대신 복사본 제공
        public int[,] Snapshot()
        {
            lock (_sync)
            {
                var copy = new int[SIZE, COLS];
                Array.Copy(_a, copy, _a.Length);
                return copy;
            }
        }
        public void TryAppend(int[] t)
        {
            if (t == null || t.Length < COLS)
                return;

            DateTime now = DateTime.Now;
            int bucket = (now.Hour * 3600 + now.Minute * 60 + now.Second) / 10;

            int[] tt = new int[COLS];
            Array.Copy(t, tt, COLS);

            tt[0] =
                  now.Hour * 10000000
                + now.Minute * 100000
                + now.Second * 1000
                + now.Millisecond;

            lock (_sync)
            {
                if (bucket == _lastBucket)
                {
                    Array.Copy(tt, _current10s, COLS);
                    return;
                }

                if (_lastBucket != -1)
                {
                    InsertUnsafe(_current10s);
                }

                _lastBucket = bucket;
                Array.Copy(tt, _current10s, COLS);
            }
        }
        private void InsertUnsafe(int[] t)
        {
            for (int i = SIZE - 1; i >= 1; i--)
            {
                for (int j = 0; j < COLS; j++)
                    _a[i, j] = _a[i - 1, j];
            }

            for (int j = 0; j < COLS; j++)
                _a[0, j] = t[j];

            if (_count < SIZE)
                _count++;
        }
        public bool TryGetSnapshot(out int[,] a, out int count)
        {
            lock (_sync)
            {
                a = new int[SIZE, COLS];
                Array.Copy(_a, a, _a.Length);
                count = _count;
                return count > 0;
            }
        }
        public HeatResult CalcHeat(int bars, int nqCol, int etfCol)
        {
            var r = new HeatResult();
            r.Bars = bars;

            lock (_sync)
            {
                if (_count < 2)
                    return r;

                int n = Math.Min(bars, _count - 1);

                for (int i = 0; i < n; i++)
                {
                    // _a[0]이 최신, _a[1]이 직전
                    double nqNow = _a[i, nqCol];
                    double nqPrev = _a[i + 1, nqCol];

                    double etfNow = _a[i, etfCol];
                    double etfPrev = _a[i + 1, etfCol];

                    double dNq = nqNow - nqPrev;
                    double dEtf = etfNow - etfPrev;

                    if (dNq > 0)
                    {
                        r.SumNqUp += dNq;
                        r.SumEtfUp += dEtf;
                        r.CountUp++;
                    }
                    else if (dNq < 0)
                    {
                        r.SumNqDown += dNq;   // 음수 그대로 유지
                        r.SumEtfDown += dEtf;
                        r.CountDown++;
                    }
                }
            }

            if (Math.Abs(r.SumNqUp) > 1e-9)
                r.BPlus = r.SumEtfUp / r.SumNqUp;

            if (Math.Abs(r.SumNqDown) > 1e-9)
                r.BMinus = r.SumEtfDown / r.SumNqDown;

            r.Heat = r.BPlus - r.BMinus;
            r.Valid = (r.CountUp > 0 || r.CountDown > 0);

           
            // ---------- Welford ----------
            r.ZCount++;

            double delta = r.Heat - r.MeanHeat;
            r.MeanHeat += delta / r.ZCount;
            double delta2 = r.Heat - r.MeanHeat;
            r.M2Heat += delta * delta2;

            if (r.ZCount > 1)
            {
                r.StdHeat = Math.Sqrt(r.M2Heat / (r.ZCount - 1));

                if (r.StdHeat > 1e-9)
                    r.Z = (r.Heat - r.MeanHeat) / r.StdHeat;
                else
                    r.Z = 0;
            }
            else
            {
                r.StdHeat = 0;
                r.Z = 0;
            }

            return r;
        }
    }
}