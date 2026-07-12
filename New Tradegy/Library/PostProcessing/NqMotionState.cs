using New_Tradegy.Library.Listeners;
using New_Tradegy.Library.Models;
using New_Tradegy.Library.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace New_Tradegy.Library.PostProcessing
{
    public class NqMotionResult
    {
        // 현재 계산값
        public double A;   // slope * (n - 1) : 해당 구간 NQ 방향/속도
        public double B;   // intercept       : 회귀식 절편
        public double R;   // R²              : 직선성
        public double Z;   // 당일 A의 Z-score

        // 당일 누적 통계, Z 계산용
        public long Count;
        public double MeanA;
        public double M2A;

        public double StdA
        {
            get
            {
                return Count > 1
                    ? Math.Sqrt(M2A / (Count - 1))
                    : 0.0;
            }
        }

        public void Reset()
        {
            A = 0.0;
            B = 0.0;
            R = 0.0;
            Z = 0.0;

            Count = 0;
            MeanA = 0.0;
            M2A = 0.0;
        }
    }





    public class NqMotionState
    {
        public readonly NqMotionResult M1 = new NqMotionResult(); // 1분, 6 bars
        public readonly NqMotionResult M25 = new NqMotionResult(); // 2.5분, 15 bars
        public readonly NqMotionResult M5 = new NqMotionResult(); // 5분, 30 bars

        public void Reset()
        {
            M1.Reset();
            M25.Reset();
            M5.Reset();
        }

        public static void UpdateNqMotion()
        {
            var nq = MajorIndex.Instance.NqMotion;
            if (nq == null)
                return;

            // NQ는 코스피/코스닥 공통이므로 하나의 Sec10Engine만 사용
            // 기존 Heat 계산에서 nqCol = 10 이었음
            int nqCol = 10;

            nq.UpdateOne(g.Sec10Kospi, 6, nqCol, nq.M1);
            nq.UpdateOne(g.Sec10Kospi, 15, nqCol, nq.M25);
            nq.UpdateOne(g.Sec10Kospi, 30, nqCol, nq.M5);
        }

        public void UpdateOne(
            Sec10Engine sec10,
            int bars,
            int nqCol,
            NqMotionResult r)
        {
            if (sec10 == null || r == null)
                return;

            double a;
            double b;
            double rr;

            if (!TryCalcRegression(sec10, bars, nqCol, out a, out b, out rr))
                return;

            const double MinStd = 1e-9;

            double stdBefore = r.Count > 1
                ? Math.Sqrt(r.M2A / (r.Count - 1))
                : 0.0;

            double z = stdBefore > MinStd
                ? (a - r.MeanA) / stdBefore
                : 0.0;

            r.A = a;
            r.B = b;
            r.R = rr;
            r.Z = z;

            r.Count++;

            double delta = a - r.MeanA;
            r.MeanA += delta / r.Count;

            double delta2 = a - r.MeanA;
            r.M2A += delta * delta2;
        }

        private bool TryCalcRegression(
            Sec10Engine sec10,
            int bars,
            int nqCol,
            out double a,
            out double b,
            out double r2)
        {
            a = 0.0;
            b = 0.0;
            r2 = 0.0;

            double[] yValues;
            if (!sec10.TryGetRecentValues(bars, nqCol, out yValues))
                return false;

            int n = yValues.Length;
            if (n < 3)
                return false;

            double sumX = 0.0;
            double sumY = 0.0;
            double sumX2 = 0.0;
            double sumXY = 0.0;

            // yValues는 오래된 값 → 현재 값 순서
            for (int i = 0; i < n; i++)
            {
                double x = i;
                double y = yValues[i];

                if (double.IsNaN(y) || double.IsInfinity(y))
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

            a = slope * (n - 1); // 구간 전체 NQ 변화량
            b = intercept;

            double meanY = sumY / n;
            double ssTot = 0.0;
            double ssRes = 0.0;

            for (int i = 0; i < n; i++)
            {
                double x = i;
                double y = yValues[i];
                double fit = slope * x + intercept;

                double dy = y - meanY;
                double err = y - fit;

                ssTot += dy * dy;
                ssRes += err * err;
            }

            r2 = ssTot > 1e-12
                ? 1.0 - ssRes / ssTot
                : 0.0;

            if (r2 < 0.0) r2 = 0.0;
            if (r2 > 1.0) r2 = 1.0;

            return true;
        }
    }
}
    
