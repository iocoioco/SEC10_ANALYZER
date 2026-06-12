using System;

namespace New_Tradegy.Library.Listeners
{
    public class Sec10Engine
    {
        private const int SIZE = 60;
        private const int COLS = 12;

        private readonly object _sync = new object();

        private readonly int[,] _a = new int[SIZE, COLS];
        private readonly int[] _current10s = new int[COLS];

        private int _count = 0;
        private int _lastBucket = -1;

        public int Count
        {
            get
            {
                lock (_sync)
                    return _count;
            }
        }

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
    }
}