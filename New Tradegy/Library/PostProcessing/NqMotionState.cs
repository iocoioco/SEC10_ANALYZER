using New_Tradegy.Library.Models;
using New_Tradegy.Library.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace New_Tradegy.Library.PostProcessing
{
    public class NqMotionState
    {
        // 현재값
        public double A;      // slope * (n - 1)
        public double R;      // R²
        public double Z;      // Z-score

        // 당일 누적 통계
        public long Count;
        public double MeanA;
        public double M2A;

        // 계산용
        public double StdA =>
            Count > 1
                ? Math.Sqrt(M2A / (Count - 1))
                : 0.0;

        public void Reset()
        {
            A = 0;
            R = 0;
            Z = 0;

            Count = 0;
            MeanA = 0;
            M2A = 0;
        }

        public static void UpdateNqMotion()
        {
            const string ETF_KOSPI = "KODEX 레버리지";
            const int N = 60;
            const double MaxSpanSeconds = 50.0;
            const double MinStd = 1e-9;

            var data = g.StockRepo.TryGetDataOrNull(ETF_KOSPI);
            var api = data?.Api;
            if (api == null)
                return;

            if (api.틱의시간 == null || api.틱나스닥 == null)
                return;

            if (api.틱의시간.Length < N || api.틱나스닥.Length < N)
                return;

            // 틱[0] = 현재, 틱[N-1] = 가장 오래된 값 가정
            double spanMs = TimeUtils.ElapsedMillisecondsDouble(
                api.틱의시간[N - 1],
                api.틱의시간[0]);

            if (spanMs <= 0)
                return;

            double spanSec = spanMs / 1000.0;
            if (spanSec > MaxSpanSeconds)
                return;

            // ─────────────────────────────
            // Linear Regression
            // x = 0 ... N-1
            // y = 오래된 NQ → 현재 NQ
            // ─────────────────────────────
            double sumX = 0.0;
            double sumY = 0.0;
            double sumX2 = 0.0;
            double sumXY = 0.0;

            for (int i = 0; i < N; i++)
            {
                double x = i;
                double y = api.틱나스닥[N - 1 - i];

                if (double.IsNaN(y) || double.IsInfinity(y))
                    return;

                sumX += x;
                sumY += y;
                sumX2 += x * x;
                sumXY += x * y;
            }

            double denom = N * sumX2 - sumX * sumX;
            if (Math.Abs(denom) < 1e-12)
                return;

            double slope = (N * sumXY - sumX * sumY) / denom;
            double intercept = (sumY - slope * sumX) / N;

            // A = 회귀선 기준 최근 60틱 전체 변화량
            double a = slope * (N - 1);

            // ─────────────────────────────
            // R² 계산
            // ─────────────────────────────
            double meanY = sumY / N;
            double ssTot = 0.0;
            double ssRes = 0.0;

            for (int i = 0; i < N; i++)
            {
                double x = i;
                double y = api.틱나스닥[N - 1 - i];
                double fit = slope * x + intercept;

                double dy = y - meanY;
                double err = y - fit;

                ssTot += dy * dy;
                ssRes += err * err;
            }

            double r2 = ssTot > 1e-12
                ? 1.0 - ssRes / ssTot
                : 0.0;

            if (r2 < 0) r2 = 0;
            if (r2 > 1) r2 = 1;

            // ─────────────────────────────
            // Welford 방식: 과거 통계 기준으로 Z 계산 후 현재 A 반영
            // ─────────────────────────────
            var nq = MajorIndex.Instance.NqMotion;

            double stdBefore = nq.Count > 1
                ? Math.Sqrt(nq.M2A / (nq.Count - 1))
                : 0.0;

            double z = stdBefore > MinStd
                ? (a - nq.MeanA) / stdBefore
                : 0.0;

            nq.A = a;
            nq.R = r2;
            nq.Z = z;

            nq.Count++;

            double delta = a - nq.MeanA;
            nq.MeanA += delta / nq.Count;

            double delta2 = a - nq.MeanA;
            nq.M2A += delta * delta2;
        }

    } 
}
    
