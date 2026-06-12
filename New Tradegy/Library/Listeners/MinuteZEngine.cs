
using System;
using System.Collections.Generic;

namespace New_Tradegy.Library.Listeners
{
    public class MinuteZEngine
    {
        private readonly Dictionary<int, int> _minuteToPrice = new Dictionary<int, int>();

        public double Z1 { get; private set; }
        public double Z3 { get; private set; }
        public double Z6 { get; private set; }
        public double Z10 { get; private set; }

        public int R1 { get; private set; }
        public int R3 { get; private set; }
        public int R6 { get; private set; }
        public int R10 { get; private set; }

        public int LastMinuteKey { get; private set; } = -1;

        private readonly double _sigma1;
        private readonly double _sigma3;
        private readonly double _sigma6;
        private readonly double _sigma10;

        public MinuteZEngine(double sigma1, double sigma3, double sigma6, double sigma10)
        {
            _sigma1 = sigma1;
            _sigma3 = sigma3;
            _sigma6 = sigma6;
            _sigma10 = sigma10;
        }

        public void Reset()
        {
            _minuteToPrice.Clear();

            Z1 = Z3 = Z6 = Z10 = 0.0;
            R1 = R3 = R6 = R10 = 0;

            LastMinuteKey = -1;
        }

        public void Update(int hhmmss, int price)
        {
            int minuteKey = ToMinuteKey(hhmmss);

            // 09:00 ~ 15:09만 사용
            if (minuteKey < 9 * 60 || minuteKey > 15 * 60 + 9)
                return;

            // 같은 분이면 마지막 값으로 덮어씀
            _minuteToPrice[minuteKey] = price;
            LastMinuteKey = minuteKey;

            Calc(minuteKey, price);
        }

        private void Calc(int t, int p)
        {
            R1 = R3 = R6 = R10 = 0;
            Z1 = Z3 = Z6 = Z10 = 0.0;

            if (_minuteToPrice.TryGetValue(t - 1, out int p1))
            {
                R1 = p - p1;
                Z1 = SafeDiv(R1, _sigma1);
            }

            if (_minuteToPrice.TryGetValue(t - 3, out int p3))
            {
                R3 = p - p3;
                Z3 = SafeDiv(R3, _sigma3);
            }

            if (_minuteToPrice.TryGetValue(t - 6, out int p6))
            {
                R6 = p - p6;
                Z6 = SafeDiv(R6, _sigma6);
            }

            if (_minuteToPrice.TryGetValue(t - 10, out int p10))
            {
                R10 = p - p10;
                Z10 = SafeDiv(R10, _sigma10);
            }
        }

        private static int ToMinuteKey(int hhmmss)
        {
            int hh = hhmmss / 10000;
            int mm = (hhmmss / 100) % 100;
            return hh * 60 + mm;
        }

        private static double SafeDiv(double x, double sigma)
        {
            if (sigma <= 0.0000001) return 0.0;
            return x / sigma;
        }
    }
}

