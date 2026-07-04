using New_Tradegy.Library.Models;
using System;
using System.Drawing;

namespace New_Tradegy.Library
{
    public class ChartIndexHeat
    {
        public double[] FittedNq;

        public double Slope;
        public double Begin;
        public double R;
        public double Residual;

        public bool Valid;

        private int _lastFitMinute = -1;

        private DateTime _lastFitTime = DateTime.MinValue;
        private int _lastFitSlot = -1;
        public ChartIndexHeat()
        {
            FittedNq = new double[382];
            Reset();
        }

        public void Reset()
        {
            Valid = false;

            Slope = 0.0;
            Begin = 0.0;
            R = 0.0;
            Residual = 0.0;

            if (FittedNq != null)
                Array.Clear(FittedNq, 0, FittedNq.Length);
        }


        public void Update(StockData data, int fitMinutes, int openMinutes)
        {
            DateTime now = DateTime.Now;
            int slot = now.Minute * 6 + now.Second / 10;

            if (!g.test && slot == _lastFitSlot)
                return;

            _lastFitSlot = slot;

            Reset();

            if (data == null || data.Api == null || data.Api.x == null)
                return;

            int nrow = data.Api.nrow;
            if (g.test)
                nrow = g.Npts[1];

            if (nrow <= 0)
                return;


            if (FittedNq == null || FittedNq.Length != 382)
                FittedNq = new double[382];

            // 1차 목표:
            // 원본 NQ를 같은 row에 그대로 복사
            for (int i = 0; i < nrow; i++)
            {
                FittedNq[i] = data.Api.x[i, 10];
            }

            // --------------------------------------------------
            // 2차 목표 : fit 구간으로 NQ를 가격에 fitting
            // --------------------------------------------------


            int fitBars;
            int openBars;

            GetFitOpenBars(
                nrow,
                fitMinutes,
                openMinutes,
                out fitBars,
                out openBars);

            if (fitBars == 0)
            {
                Valid = false;
                return;
            }


            int fitEnd = nrow - openBars;        // open 구간 시작
            int fitStart = fitEnd - fitBars;       // fit 구간 시작

            if (fitStart < 0 || fitEnd <= fitStart)
            {
                Valid = false;
                return;     // FittedNq는 이미 원본 복사된 상태
            }

            double sx = 0.0;
            double sy = 0.0;
            double sxx = 0.0;
            double sxy = 0.0;
            double syy = 0.0;
            int n = 0;

            for (int i = fitStart; i < fitEnd; i++)
            {
                if (data.Api.x[i, 0] == 0)
                    continue;

                double nq = data.Api.x[i, 10];  // 원본 NQ
                double pr = data.Api.x[i, 1];   // 가격

                if (nq == 0 || pr == 0)
                    continue;

                sx += nq;
                sy += pr;
                sxx += nq * nq;
                sxy += nq * pr;
                syy += pr * pr;
                n++;
            }

            if (n < 2)
            {
                Valid = false;
                return;
            }

            double den = n * sxx - sx * sx;
            if (Math.Abs(den) < 1e-9)
            {
                Valid = false;
                return;
            }

            double a = (n * sxy - sx * sy) / den;
            double b = (sy - a * sx) / n;

            // R 계산
            double denR = (n * sxx - sx * sx) * (n * syy - sy * sy);
            double corr = 0.0;

            if (denR > 1e-9)
                corr = (n * sxy - sx * sy) / Math.Sqrt(denR);

            double r2 = corr * corr;

            // 전체 row에 fitted NQ 생성
            for (int i = 0; i < nrow; i++)
            {
                if (data.Api.x[i, 0] == 0)
                    continue;

                FittedNq[i] = a * data.Api.x[i, 10] + b;
            }

            R = r2;
            Valid = true;
        }


        private static void GetFitOpenBars(
    int nrow,
    int fitMax,
    int openMax,
    out int fitBars,
    out int openBars)
        {
            fitBars = 0;
            openBars = 0;

            // 0859 데이터 1개는 의미 없으므로 제외
            int usableBars = nrow - 1;

            // 0900, 0901, 0902 최소 3개부터 시작
            if (usableBars < 3)
                return;

            int maxTotal = fitMax + openMax;

            if (usableBars < maxTotal)
            {
                openBars = usableBars / 2;
                fitBars = usableBars - openBars;

                if (openBars < 1 || fitBars < 2)
                {
                    fitBars = 0;
                    openBars = 0;
                }

                return;
            }

            fitBars = fitMax;
            openBars = openMax;
        }
    }
}