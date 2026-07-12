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

        private int _zCount6, _zCount15, _zCount30;
        private double _meanHeat6, _meanHeat15, _meanHeat30;
        private double _m2Heat6, _m2Heat15, _m2Heat30;

        public int GetValue(int row, int col)
        {
            lock (_sync)
            {
                if (row < 0 || row >= _count)
                    return 0;

                return _a[row, col];
            }
        }

        public void AddRow(int[] row)
        {
            lock (_sync)
            {
                for (int i = Math.Min(_count, SIZE - 1); i > 0; i--)
                    for (int j = 0; j < COLS; j++)
                        _a[i, j] = _a[i - 1, j];

                for (int j = 0; j < COLS && j < row.Length; j++)
                    _a[0, j] = row[j];

                if (_count < SIZE)
                    _count++;
            }
        }

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


            // ---------- Threshold ----------
            int minUpCount, minDownCount;
            double minUpNqAbs, minDownNqAbs;

            GetHeatThreshold(
                bars,
                out minUpCount,
                out minDownCount,
                out minUpNqAbs,
                out minDownNqAbs);

            bool validUp =
                r.CountUp >= minUpCount &&
                Math.Abs(r.SumNqUp) >= minUpNqAbs;

            bool validDown =
                r.CountDown >= minDownCount &&
                Math.Abs(r.SumNqDown) >= minDownNqAbs;

            // ---------- BPlus / BMinus / Heat ----------
            if (validUp)
                r.BPlus = r.SumEtfUp / r.SumNqUp;
            else
                r.BPlus = 0.0;

            if (validDown)
                r.BMinus = r.SumEtfDown / r.SumNqDown;
            else
                r.BMinus = 0.0;

            // 상승/하락 양쪽 표본이 모두 충분할 때만 Heat 유효
            if (validUp && validDown)
            {
                r.Heat = r.BPlus - r.BMinus;
                r.Valid = true;
            }
            else
            {
                r.Heat = 0.0;
                r.Valid = false;
            }

            if (r.Valid)
            {
                if (bars == 6)
                {
                    UpdateHeatZ(r, ref _zCount6, ref _meanHeat6, ref _m2Heat6);
                }
                else if (bars == 15)
                {
                    UpdateHeatZ(r, ref _zCount15, ref _meanHeat15, ref _m2Heat15);
                }
                else if (bars == 30)
                {
                    UpdateHeatZ(r, ref _zCount30, ref _meanHeat30, ref _m2Heat30);
                }
                else
                {
                    r.ZCount = 0;
                    r.MeanHeat = 0.0;
                    r.M2Heat = 0.0;
                    r.StdHeat = 0.0;
                    r.Z = 0.0;
                }
            }
            else
            {
                r.ZCount = 0;
                r.MeanHeat = 0.0;
                r.M2Heat = 0.0;
                r.StdHeat = 0.0;
                r.Z = 0.0;
            }

            return r;
        }

        private static bool GetHeatThreshold(
    int bars,
    out int minUpCount,
    out int minDownCount,
    out double minUpNqAbs,
    out double minDownNqAbs)
        {
            if (bars <= 6)          // 1분
            {
                minUpCount = 2;
                minDownCount = 2;
                minUpNqAbs = 0.03;
                minDownNqAbs = 0.03;
                return true;
            }

            if (bars <= 15)         // 2.5분
            {
                minUpCount = 3;
                minDownCount = 3;
                minUpNqAbs = 0.05;
                minDownNqAbs = 0.05;
                return true;
            }

            if (bars <= 30)         // 5분
            {
                minUpCount = 4;
                minDownCount = 4;
                minUpNqAbs = 0.07;
                minDownNqAbs = 0.07;
                return true;
            }

            minUpCount = 3;
            minDownCount = 3;
            minUpNqAbs = 0.05;
            minDownNqAbs = 0.05;
            return true;
        }

        private static void UpdateHeatZ(
            HeatResult r,
            ref int zCount,
            ref double meanHeat,
            ref double m2Heat)
                {
            zCount++;

            double delta = r.Heat - meanHeat;
            meanHeat += delta / zCount;
            double delta2 = r.Heat - meanHeat;
            m2Heat += delta * delta2;

            r.ZCount = zCount;
            r.MeanHeat = meanHeat;
            r.M2Heat = m2Heat;

            if (zCount > 1)
            {
                r.StdHeat = Math.Sqrt(m2Heat / (zCount - 1));

                if (r.StdHeat > 1e-9)
                    r.Z = (r.Heat - meanHeat) / r.StdHeat;
                else
                    r.Z = 0.0;
            }
            else
            {
                r.StdHeat = 0.0;
                r.Z = 0.0;
            }
        }

        public bool TryGetRecentValues(int bars, int col, out double[] values)
        {
            values = null;

            lock (_sync)
            {
                int n = Math.Min(bars, _count);
                if (n < 3)
                    return false;

                values = new double[n];

                // _a[0] 최신, _a[n-1] 오래된 값
                // 반환은 오래된 값 → 현재 값 순서
                for (int i = 0; i < n; i++)
                {
                    int row = n - 1 - i;
                    values[i] = _a[row, col] / 1000.0;
                }

                return true;
            }
        }
    }
}