using New_Tradegy.Library.Listeners;
using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace New_Tradegy
{
    public class ETF_NQ
    {
        private Sec10Engine _sec10Kospi;
        private Sec10Engine _sec10Kosdaq;

        private int _colEtf = 1;
        private int _colNq = 9;

        private Chart _chart;
        private Label _lblKospi;
        private Label _lblKosdaq;

        public void SetEngines(Sec10Engine kospi, Sec10Engine kosdaq)
        {
            _sec10Kospi = kospi;
            _sec10Kosdaq = kosdaq;
        }

        public void Initialize(Chart chart, Label lblKospi = null, Label lblKosdaq = null)
        {
            _chart = chart;
            _lblKospi = lblKospi;
            _lblKosdaq = lblKosdaq;

            if (_chart == null) return;

            if (_chart.InvokeRequired)
            {
                _chart.Invoke(new Action(() => Initialize(chart, lblKospi, lblKosdaq)));
                return;
            }

            EnsureOverlayAreaAndSeries(
                areaName: "ETF_NQ_KOSPI",
                etfSeriesName: "ETF_KP",
                fitSeriesName: "FIT_KP",
                colorEtf: Color.Red,
                colorFit: Color.Blue,
                position: new ElementPosition(0, 0, 20, 20));

            EnsureOverlayAreaAndSeries(
                areaName: "ETF_NQ_KOSDAQ",
                etfSeriesName: "ETF_KQ",
                fitSeriesName: "FIT_KQ",
                colorEtf: Color.Red,
                colorFit: Color.Blue,
                position: new ElementPosition(0, 50, 20, 20));
        }

        private void EnsureOverlayAreaAndSeries(
            string areaName,
            string etfSeriesName,
            string fitSeriesName,
            Color colorEtf,
            Color colorFit,
            ElementPosition position)
        {
            var area = _chart.ChartAreas.FindByName(areaName);
            if (area == null)
            {
                area = new ChartArea(areaName);
                _chart.ChartAreas.Add(area);
            }

            area.Position = position;
            area.InnerPlotPosition = new ElementPosition(0, 0, 100, 100);

            area.BackColor = Color.Transparent;
            area.BorderColor = Color.Transparent;
            area.ShadowColor = Color.Transparent;

            area.AxisX.Enabled = AxisEnabled.False;
            area.AxisY.Enabled = AxisEnabled.False;

            area.AxisX.MajorGrid.Enabled = false;
            area.AxisY.MajorGrid.Enabled = false;
            area.AxisX.MinorGrid.Enabled = false;
            area.AxisY.MinorGrid.Enabled = false;

            area.AxisX.LabelStyle.Enabled = false;
            area.AxisY.LabelStyle.Enabled = false;

            area.AxisX.LineWidth = 0;
            area.AxisY.LineWidth = 0;

            var sEtf = _chart.Series.FindByName(etfSeriesName);
            if (sEtf == null)
                sEtf = _chart.Series.Add(etfSeriesName);

            sEtf.ChartArea = areaName;
            sEtf.ChartType = SeriesChartType.Line;
            sEtf.BorderWidth = 2;
            sEtf.Color = colorEtf;
            sEtf.IsVisibleInLegend = false;

            var sFit = _chart.Series.FindByName(fitSeriesName);
            if (sFit == null)
                sFit = _chart.Series.Add(fitSeriesName);

            sFit.ChartArea = areaName;
            sFit.ChartType = SeriesChartType.Line;
            sFit.BorderWidth = 2;
            sFit.Color = colorFit;
            sFit.IsVisibleInLegend = false;
        }

        public void Refresh(int fitCount, int excludeCount)
        {
            if (_chart == null || _chart.IsDisposed) return;

            if (_chart.InvokeRequired)
            {
                _chart.BeginInvoke(new Action(() => Refresh(fitCount, excludeCount)));
                return;
            }

            PlotKospi(_chart, _sec10Kospi, fitCount, excludeCount);
            PlotKosdaq(_chart, _sec10Kosdaq, fitCount, excludeCount);
        }

        private FitResult PlotKospi(Chart chart, Sec10Engine sec10, int fit, int ex)
        {
            return PlotOne(
                chart,
                _lblKospi,
                sec10,
                "KOSPI",
                fit,
                ex,
                areaName: "ETF_NQ_KOSPI",
                etfSeriesName: "ETF_KP",
                fitSeriesName: "FIT_KP");
        }

        private FitResult PlotKosdaq(Chart chart, Sec10Engine sec10, int fit, int ex)
        {
            return PlotOne(
                chart,
                _lblKosdaq,
                sec10,
                "KOSDAQ",
                fit,
                ex,
                areaName: "ETF_NQ_KOSDAQ",
                etfSeriesName: "ETF_KQ",
                fitSeriesName: "FIT_KQ");
        }

        private FitResult PlotOne(
            Chart chart,
            Label lblAnno,
            Sec10Engine sec10,
            string title,
            int fitCount,
            int excludeCount,
            string areaName,
            string etfSeriesName,
            string fitSeriesName)
        {
            var resInvalid = new FitResult { Valid = false };

            if (chart == null || chart.IsDisposed) return resInvalid;

            var sEtf = chart.Series.FindByName(etfSeriesName);
            var sFit = chart.Series.FindByName(fitSeriesName);
            if (sEtf == null || sFit == null) return resInvalid;

            sEtf.Points.Clear();
            sFit.Points.Clear();
            RemoveOwnedAnnotations(chart, areaName);

            if (sec10 == null || !sec10.TryGetSnapshot(out int[,] a, out int count))
            {
                SetLabel(lblAnno, $"{title}  no data", Color.Gray);
                return resInvalid;
            }

            int rowCount = a.GetLength(0);
            int colCount = a.GetLength(1);

            if (_colEtf < 0 || _colEtf >= colCount || _colNq < 0 || _colNq >= colCount)
            {
                SetLabel(lblAnno, $"{title}  invalid column", Color.Red);
                return resInvalid;
            }

            count = Math.Min(count, rowCount);

            if (count < excludeCount + fitCount)
            {
                SetLabel(lblAnno, $"{title}  no data", Color.Gray);
                return resInvalid;
            }

            var fit = FitResult.FitSec10(
                a,
                count,
                fitCount,
                excludeCount,
                _colEtf,
                _colNq);

            if (!fit.Valid)
            {
                SetLabel(lblAnno, $"{title}  invalid fit", Color.Gray);
                return fit;
            }

            int n = Math.Min(count, 60);

            for (int src = n - 1, x = 0; src >= 0; src--, x++)
            {
                double etf = a[src, _colEtf];
                double nq = a[src, _colNq];
                double fitted = fit.A * nq + fit.B;

                sEtf.Points.AddXY(x, etf);
                sFit.Points.AddXY(x, fitted);
            }



            string fitText = GradeFit(fit.RMSE);
            string zText = fit.Z.ToString("0.00", CultureInfo.InvariantCulture);
            string rText = fit.ResidNow.ToString("0.0", CultureInfo.InvariantCulture);

            SetLabel(
                lblAnno,
                $"{title}   Fit:{fitText}  RMSE:{fit.RMSE:0.0}  R:{rText}  Z:{zText}σ  a:{fit.A:0.000}  b:{fit.B:0.0}",
                Math.Abs(fit.Z) >= 2.0 ? Color.Red : Color.White);

            return fit;
        }

        private void RemoveOwnedAnnotations(Chart chart, string areaName)
        {
            if (chart == null) return;

            var removeList = chart.Annotations
                .OfType<Annotation>()
                .Where(a => a != null && !string.IsNullOrEmpty(a.Name) && a.Name.StartsWith(areaName + "_"))
                .ToList();

            foreach (var anno in removeList)
                chart.Annotations.Remove(anno);
        }

       

        private void SetLabel(Label lbl, string text, Color color)
        {
            if (lbl == null) return;

            if (lbl.InvokeRequired)
            {
                lbl.BeginInvoke(new Action(() => SetLabel(lbl, text, color)));
                return;
            }

            lbl.Text = text;
            lbl.ForeColor = color;
        }

        private string GradeFit(double rmse)
        {
            if (rmse <= 3) return "A+";
            if (rmse <= 5) return "A";
            if (rmse <= 8) return "B";
            if (rmse <= 12) return "C";
            if (rmse <= 20) return "D";
            return "F";
        }







    }

    public class FitResult
    {
        public double A;
        public double B;
        public double RMSE;
        public double Sigma;
        public double ResidNow;
        public bool Valid;

        public double Z => Sigma == 0 ? 0 : ResidNow / Sigma;

        public static FitResult FitSec10(
            int[,] A,
            int count,
            int fitCount,
            int excludeCount,
            int colEtf,
            int colNq)
        {
            var res = new FitResult();

            if (A == null) return res;

            int rowCount = A.GetLength(0);
            int colCount = A.GetLength(1);

            if (rowCount <= 0 || colCount <= 0) return res;
            if (fitCount < 2) return res;
            if (excludeCount < 0) return res;

            if (colEtf < 0 || colEtf >= colCount) return res;
            if (colNq < 0 || colNq >= colCount) return res;

            count = Math.Min(count, rowCount);
            if (count <= 0) return res;

            int start = excludeCount;
            int end = excludeCount + fitCount;

            if (count < end) return res;
            if (start < 0 || start >= rowCount) return res;
            if (end <= start || end > rowCount) return res;

            int n = 0;
            double sumX = 0;
            double sumY = 0;

            for (int i = start; i < end; i++)
            {
                double x = A[i, colNq];
                double y = A[i, colEtf];

                sumX += x;
                sumY += y;
                n++;
            }

            if (n < 2) return res;

            double meanX = sumX / n;
            double meanY = sumY / n;

            double varX = 0;
            double covXY = 0;

            for (int i = start; i < end; i++)
            {
                double x = A[i, colNq] - meanX;
                double y = A[i, colEtf] - meanY;

                varX += x * x;
                covXY += x * y;
            }

            if (Math.Abs(varX) < 1e-12) return res;

            double a = covXY / varX;
            double b = meanY - a * meanX;

            res.A = a;
            res.B = b;

            double sse = 0;
            for (int i = start; i < end; i++)
            {
                double x = A[i, colNq];
                double y = A[i, colEtf];

                double pred = a * x + b;
                double err = y - pred;

                sse += err * err;
            }

            double rmse = Math.Sqrt(sse / n);

            res.RMSE = rmse;
            res.Sigma = rmse == 0 ? 1 : rmse;

            double xNow = A[0, colNq];
            double yNow = A[0, colEtf];
            double predNow = a * xNow + b;

            res.ResidNow = yNow - predNow;
            res.Valid = true;

            return res;
        }
    }
}