using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace New_Tradegy.Test
{
    internal class RegressionMethod
    {
        public class Linear2DResult
        {
            public int Count;
            public double A;
            public double B;
            public double C;
            public double R2;
        }
        public static Linear2DResult FitLinear2D(
           List<double> xs,
           List<double> ys,
           List<double> zs)
        {
            var result = new Linear2DResult();

            if (xs == null || ys == null || zs == null)
                return result;

            int n = Math.Min(xs.Count, Math.Min(ys.Count, zs.Count));
            result.Count = n;

            if (n < 3)
                return result;

            double sx = 0.0, sy = 0.0, sz = 0.0;
            double sxx = 0.0, syy = 0.0, sxy = 0.0;
            double sxz = 0.0, syz = 0.0;

            for (int i = 0; i < n; i++)
            {
                double x = xs[i];
                double y = ys[i];
                double z = zs[i];

                sx += x;
                sy += y;
                sz += z;

                sxx += x * x;
                syy += y * y;
                sxy += x * y;

                sxz += x * z;
                syz += y * z;
            }

            // Normal equation:
            // [sxx sxy sx] [a] = [sxz]
            // [sxy syy sy] [b] = [syz]
            // [sx  sy  n ] [c] = [sz ]

            double[,] m =
            {
        { sxx, sxy, sx  },
        { sxy, syy, sy  },
        { sx,  sy,  n   }
    };

            double[] v = { sxz, syz, sz };

            if (!Solve3x3(m, v, out double a, out double b, out double c))
                return result;

            result.A = a;
            result.B = b;
            result.C = c;

            double meanZ = sz / n;
            double ssTot = 0.0;
            double ssRes = 0.0;

            for (int i = 0; i < n; i++)
            {
                double pred = a * xs[i] + b * ys[i] + c;
                double dz = zs[i] - meanZ;
                double er = zs[i] - pred;

                ssTot += dz * dz;
                ssRes += er * er;
            }

            if (ssTot > 1e-12)
                result.R2 = 1.0 - ssRes / ssTot;
            else
                result.R2 = 0.0;

            return result;
        }


        private static bool Solve3x3(
           double[,] m,
           double[] v,
           out double x0,
           out double x1,
           out double x2)
        {
            x0 = x1 = x2 = 0.0;

            double a00 = m[0, 0], a01 = m[0, 1], a02 = m[0, 2];
            double a10 = m[1, 0], a11 = m[1, 1], a12 = m[1, 2];
            double a20 = m[2, 0], a21 = m[2, 1], a22 = m[2, 2];

            double det =
                a00 * (a11 * a22 - a12 * a21)
              - a01 * (a10 * a22 - a12 * a20)
              + a02 * (a10 * a21 - a11 * a20);

            if (Math.Abs(det) < 1e-12)
                return false;

            double b0 = v[0], b1 = v[1], b2 = v[2];

            double det0 =
                b0 * (a11 * a22 - a12 * a21)
              - a01 * (b1 * a22 - a12 * b2)
              + a02 * (b1 * a21 - a11 * b2);

            double det1 =
                a00 * (b1 * a22 - a12 * b2)
              - b0 * (a10 * a22 - a12 * a20)
              + a02 * (a10 * b2 - b1 * a20);

            double det2 =
                a00 * (a11 * b2 - b1 * a21)
              - a01 * (a10 * b2 - b1 * a20)
              + b0 * (a10 * a21 - a11 * a20);

            x0 = det0 / det;
            x1 = det1 / det;
            x2 = det2 / det;

            return true;
        }
        public static Linear2DResult FitLinear2D_NoIntercept(
    List<double> x,
    List<double> y,
    List<double> z)
        {
            var r = new Linear2DResult();

            int n = Math.Min(x.Count, Math.Min(y.Count, z.Count));
            if (n < 2)
                return r;

            double sxx = 0.0;
            double syy = 0.0;
            double sxy = 0.0;
            double sxz = 0.0;
            double syz = 0.0;

            for (int i = 0; i < n; i++)
            {
                double xx = x[i];
                double yy = y[i];
                double zz = z[i];

                sxx += xx * xx;
                syy += yy * yy;
                sxy += xx * yy;
                sxz += xx * zz;
                syz += yy * zz;
            }

            double det = sxx * syy - sxy * sxy;
            if (Math.Abs(det) < 1e-20)
                return r;

            double a = (sxz * syy - syz * sxy) / det;
            double b = (syz * sxx - sxz * sxy) / det;

            // R² 계산
            double sse = 0.0;
            double mean = 0.0;

            for (int i = 0; i < n; i++)
                mean += z[i];

            mean /= n;

            double sst = 0.0;

            for (int i = 0; i < n; i++)
            {
                double pred = a * x[i] + b * y[i];
                double err = z[i] - pred;

                sse += err * err;

                double d = z[i] - mean;
                sst += d * d;
            }

            r.Count = n;
            r.A = a;
            r.B = b;
            r.C = 0.0;                    // Intercept 강제 0
            r.R2 = (sst > 1e-12)
                ? 1.0 - sse / sst
                : 0.0;

            return r;
        }



        public class Linear1DResult
        {
            public int Count;
            public double A;
            public double B;
            public double R2;
        }

        public static Linear1DResult FitLinear1D(
            List<double> xs,
            List<double> ys)
        {
            var result = new Linear1DResult();

            if (xs == null || ys == null)
                return result;

            int n = Math.Min(xs.Count, ys.Count);
            if (n < 2)
                return result;

            double sumX = 0.0;
            double sumY = 0.0;
            double sumXX = 0.0;
            double sumXY = 0.0;

            for (int i = 0; i < n; i++)
            {
                double x = xs[i];
                double y = ys[i];

                if (double.IsNaN(x) || double.IsInfinity(x) ||
                    double.IsNaN(y) || double.IsInfinity(y))
                    continue;

                sumX += x;
                sumY += y;
                sumXX += x * x;
                sumXY += x * y;
                result.Count++;
            }

            n = result.Count;
            if (n < 2)
                return result;

            double denominator = n * sumXX - sumX * sumX;
            if (Math.Abs(denominator) < 1e-12)
                return result;

            result.A = (n * sumXY - sumX * sumY) / denominator;
            result.B = (sumY - result.A * sumX) / n;

            double meanY = sumY / n;
            double ssTotal = 0.0;
            double ssError = 0.0;

            for (int i = 0; i < Math.Min(xs.Count, ys.Count); i++)
            {
                double x = xs[i];
                double y = ys[i];

                if (double.IsNaN(x) || double.IsInfinity(x) ||
                    double.IsNaN(y) || double.IsInfinity(y))
                    continue;

                double fitted = result.A * x + result.B;

                ssTotal += (y - meanY) * (y - meanY);
                ssError += (y - fitted) * (y - fitted);
            }

            result.R2 = ssTotal > 1e-12
                ? 1.0 - ssError / ssTotal
                : 0.0;

            return result;
        }
    }
}
