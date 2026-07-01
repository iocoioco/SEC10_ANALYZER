using MathNet.Numerics;
using New_Tradegy.Library.Listeners;
using New_Tradegy.Library.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Windows.Forms.DataVisualization.Charting;
using System.Xml.Linq;
using Charting = System.Windows.Forms.DataVisualization.Charting;


using Chart = System.Windows.Forms.DataVisualization.Charting.Chart;
using Series = System.Windows.Forms.DataVisualization.Charting.Series;
using ChartArea = System.Windows.Forms.DataVisualization.Charting.ChartArea;
using RectangleAnnotation = System.Windows.Forms.DataVisualization.Charting.RectangleAnnotation;
using New_Tradegy.Library.Utils;

namespace New_Tradegy.Library.Trackers
{
    public static class ChartIndex
    {
        private static ChartIndexHeat _kospiHeat = new ChartIndexHeat();
        private static ChartIndexHeat _kosdaqHeat = new ChartIndexHeat();




        private static readonly Dictionary<int, Color> colorKODEX =
    new Dictionary<int, Color>()
    {
        { 0, Color.White },
        { 1, Color.Red },
        { 2, Color.White },
        { 3, Color.Black },
        { 4, Color.RoyalBlue },
        { 5, Color.Magenta },
        { 6, Color.Green },
        { 10, Color.IndianRed},
        { 11, Color.Brown },
    };

        public static (ChartArea area, Annotation anno)
        UpdateChartArea(Chart chart, StockData data)
        {
            string areaName = data?.Stock;
            if (string.IsNullOrEmpty(areaName)) return (null, null);

            if (data.Stock == "KODEX 레버리지")
                _kospiHeat.Update(data, g.ChartHeatFitMin, g.ChartHeatOpenMin);
            else if (data.Stock == "KODEX 코스닥150레버리지")
                _kosdaqHeat.Update(data, g.ChartHeatFitMin, g.ChartHeatOpenMin);

            ChartArea area = null;
            Annotation anno = null;

            // Update exiting chartarea
            if (chart.ChartAreas.IndexOf(areaName) >= 0 && !data.Misc.CreateNewChartArea)
            {
                area = chart.ChartAreas[areaName];
                if (data?.Api != null && data.Api.nrow > 0)
                    UpdateSeries(chart, data); // ← 기존 함수 그대로 사용
            }
            // Generate a new chartarea
            else
            {
                ChartBasic.RemoveChartBlock(chart, data.Stock);
                area = CreateChartArea(chart, data);
                if (data.Misc.CreateNewChartArea)
                    data.Misc.CreateNewChartArea = false;
            }

            if (area != null) // anno.Text = annotationText; 존재하면 text만 교체
            {
                //anno = IndexAnnotation(chart, data); // existing annotation will be deleted in GeneralAnnotation
            }





            if (area != null)
                UpdateStopLossOverlay(area, data);

            return (area, anno);
        }

        private static void UpdateStopLossOverlay(ChartArea area, StockData data)
        {
            if (area == null || data?.Deal == null)
                return;

            if (data.Deal.보유량 == 0)
            {
                data.Deal.수익률 = 0;
                area.BackColor = Color.White;
                return;
            }

            double loss = -data.Deal.수익률;


            if (loss <= 0)
                area.BackColor = Color.White;
            else if (loss < 0.1)
                area.BackColor = Color.FromArgb(15, 255, 120, 120);
            else if (loss < 0.2)
                area.BackColor = Color.FromArgb(35, 255, 100, 100);
            else if (loss < 0.3)
                area.BackColor = Color.FromArgb(60, 255, 80, 80);
            else
                area.BackColor = Color.FromArgb(130, 255, 40, 40);
        }



        public static Annotation IndexAnnotation(Chart chart, StockData data)
        {
            if (chart == null || data?.Api == null || string.IsNullOrEmpty(data.Stock))
                return null;

            string areaName = data.Stock;
            string annotationText = IndexAnnotationText(data);

            float width, yLocation;
            if (chart.Name == "chart1")
            {
                width = 100.0f / g.nCol;
                yLocation = 1.0f / 3.0f;
            }
            else
            {
                width = 100.0f / 5.0f;
                yLocation = 1.0f / 2.0f;
            }

            if (chart.Annotations.FindByName(areaName) is TextAnnotation t)
            {
                t.Text = annotationText;
                t.Visible = false;
                return t;
            }

            var anno = ChartGeneral.CreateAnnotationOrOverlay(
                chart, areaName,
                new RectangleF(0f, yLocation, width * 2, 7f), // 20260331
                annotationText, Color.Black, Color.White);

            anno.Visible = false;
            return anno;
        }

        public static ChartArea CreateChartArea(Chart chart, StockData data)
        {
            if (chart == null || data?.Api?.x == null) return null;

            string stock = data.Stock;
            if (string.IsNullOrEmpty(stock)) return null;

            if (!ChartLayoutUtils.TryGetDrawRange(data, out int start, out int endEx))
                return null;

            if (endEx - start < 2)
                return null;

            var area = new ChartArea(stock);
            chart.ChartAreas.Add(area);

            try
            {
                // X축 설정
                int totalPoints = endEx - start;
                area.AxisX.LabelStyle.Enabled = false;
                area.AxisX.MajorGrid.Enabled = false;
                area.AxisX.Interval = Math.Max(1, totalPoints - 1);
                area.AxisX.IntervalOffset = 1; // 필요하면 1 유지

                // Y축 설정
                area.AxisY.LabelStyle.Enabled = false;
                area.AxisY.MajorGrid.Enabled = false;
                area.AxisY.MajorTickMark.Enabled = false;
                area.AxisY.MinorTickMark.Enabled = false;
                area.AxisY.MinorGrid.Enabled = false;

                // 시리즈 추가 (여기서 y_min/y_max도 채워진다고 가정)
                bool success = AddSeriesLines(chart, data, data.Stock, area.Name, start, endEx);
                if (!success)
                {
                    chart.ChartAreas.Remove(area);
                    return null;
                }

                // 축 설정
                if (data.Misc.y_min == int.MaxValue || data.Misc.y_max == int.MinValue || data.Misc.y_min == data.Misc.y_max)
                {
                    area.RecalculateAxesScale();
                }
                else
                {
                    double padding = (data.Misc.y_max - data.Misc.y_min) * 0.1;
                    area.AxisY.Minimum = data.Misc.y_min - 0.2 * padding;
                    area.AxisY.Maximum = data.Misc.y_max + 1.5 * padding;
                }

                // 폰트 및 내부 여백
                area.InnerPlotPosition = new ElementPosition(0, 0, 70, 95);
                area.AxisX.LabelStyle.Font = new Font("Arial Bold", 9);  // 20260331
                area.AxisY.LabelStyle.Font = new Font("Arial Bold", 9);  // 20260331

                return area;
            }
            catch
            {
                // 예외 시 area 제거 (누수 방지)
                chart.ChartAreas.Remove(area);
                return null;
            }
        }

        // ✅ annotation 텍스트 구성 함수
        public static string IndexAnnotationText(StockData data)
        {
            if (data == null || data.Post == null) return string.Empty;
            var p = data.Post;




            double[] instArr = GetColumn(data.Api.x, 4);
            double[] retailArr = GetColumn(data.Api.x, 6);

            var (dInst, dRetail) = DeltaPair(instArr, retailArr);



            string sp = "\u2007"; // figure space



            // 20260331
            double nq = MajorIndex.Instance.NasdaqIndex;

            // 🔥 delta 기준으로 수정
            double a = p.분10나스닥;                    // 0~10초
            double b = p.분20나스닥 - p.분10나스닥;     // 10~20초
            double c = p.분30나스닥 - p.분20나스닥;     // 20~30초

            // 문자열
            string s0 = nq.ToString("F3", CultureInfo.InvariantCulture);
            string s1 = a.ToString("+0.000;-0.000", CultureInfo.InvariantCulture);
            string s2 = b.ToString("+0.000;-0.000", CultureInfo.InvariantCulture);
            string s3 = c.ToString("+0.000;-0.000", CultureInfo.InvariantCulture);

            // 색
            Color c1 = GetColor(a);
            Color c2 = GetColor(b);
            Color c3 = GetColor(c);

            // 👉 여기서 Annotation 4개 만들어서 위치만 옆으로 밀면서 붙이면 됨

            Color GetColor(double v)
            {
                if (v > 0) return Color.Red;
                if (v < 0) return Color.Blue;
                return Color.Gray;
            }

            // 문자열 (sign 없이 기존 스타일)
            string l1 = string.Format(CultureInfo.InvariantCulture,
    "{0:F3}{4}{1:+0.000;-0.000;+0.000}{4}{2:+0.000;-0.000;+0.000}{4}{3:+0.000;-0.000;+0.000}",
    nq, a, b, c, sp);








            string l2 = string.Format(CultureInfo.InvariantCulture,
                "{0:0}/{1:0}{4}{2:0}/{3:0}{4}{5:0}/{6:0}",
                p.분10배수차, p.분10배수합, p.분20배수차, p.분20배수합, sp, p.분30배수차, p.분30배수합);

            string l3 = string.Format(
                CultureInfo.InvariantCulture,
                "{0:+0;-0;0}{1:+0;-0;0}{2:+0;-0;0}{3}&{3}{4:+0;-0;0}",
                p.분30프로천,          // {0}
                p.분30외인천,          // {1}
                dInst * 60 / 90,                      // {2}
                sp,                         // {3} = figure space around '&'
                dRetail * 60 / 90);                   // {4}

            return $"{l1}\n{l2}\n{l3}";
        }
        private static double[] GetColumn(int[,] x, int col)
        {
            if (x == null)
                return null;

            int n = x.GetLength(0);
            double[] arr = new double[n];

            for (int i = 0; i < n; i++)
                arr[i] = x[i, col];

            return arr;
        }
        private static (double dInst, double dRetail) DeltaPair(double[] instArr, double[] retailArr)
        {
            return (
                DeltaRecent(instArr, 90),
                DeltaRecent(retailArr, 90)
            );
        }

        private static double DeltaRecent(double[] arr, int maxLookBackSeconds)
        {
            if (arr == null || arr.Length < 2)
                return 0.0;

            int lastIndex = arr.Length - 1;
            double last = arr[lastIndex];

            // 7222 누적 수급은 약 90초마다 갱신된다.
            // 분 배열은 시각 경계가 어긋날 수 있으므로 +1칸 더 본다.
            int maxLookBackBars =
                (int)Math.Ceiling(maxLookBackSeconds / 60.0) + 1;

            for (int i = lastIndex - 1; i >= 0 && (lastIndex - i) <= maxLookBackBars; i--)
            {
                double prev = arr[i];

                if (Math.Abs(last - prev) >= 1.0)
                    return last - prev;
            }

            return 0.0;
        }



        private static bool AddSeriesLines(
    Chart chart,
    StockData data,
    string stockName,
    string area,
    int start,
    int endEx)
        {
            if (chart == null || data?.Api?.x == null) return false;

            var x = data.Api.x;
            int nrow = data.Api.nrow;

            // endEx 방어
            endEx = Math.Min(endEx, nrow);
            start = Math.Max(0, start);
            if (endEx - start < 2) return false;

            data.Misc.y_min = int.MaxValue;
            data.Misc.y_max = int.MinValue;

            bool anyAdded = false;

            int[] ids = { 1, 3, 4, 5, 6, 10, 11 };

            foreach (int id in ids)
            {
                string seriesName = stockName + " " + id;

                double mag = 1.0;
                Magnifier(stockName, id == 10 ? 1 : id, ref mag);

                var series = new Series(seriesName)
                {
                    ChartArea = area,
                    ChartType = SeriesChartType.Line,
                    XValueType = ChartValueType.String, // ✅ 문자열 축으로 통일
                    IsVisibleInLegend = false,

                    BorderWidth = 2
                };
                if (id == 10)
                    series.BorderWidth = 3;

                // ✅ 컬러 방어
                if (!colorKODEX.TryGetValue(id, out var c))
                    c = Color.Gray;

                series.Color = c;

                chart.Series.Add(series);

                int iLast = -1;

                for (int i = start; i < endEx; i++)
                {
                    if (x[i, 0] == 0) break;

                    int val = (int)(x[i, id] * mag);
                    if(id == 10)
                    {
                        val = (int)(GetHeatValue(data, i) * mag);
                    }
                        

                    series.Points.AddXY(((int)(x[i, 0] / g.HUNDRED)).ToString("D4"), val);

                    data.Misc.y_min = Math.Min(data.Misc.y_min, val);
                    data.Misc.y_max = Math.Max(data.Misc.y_max, val);

                    iLast = i;
                }

                if (series.Points.Count < 2)
                {
                    chart.Series.Remove(series);
                    continue;
                }

                // ✅ 실제 그려진 마지막 row 기준 endEx 재계산
                int endExDrawn = (iLast >= 0) ? (iLast + 1) : endEx;

                Mark(chart, series, endExDrawn);
                Label(chart, series, endExDrawn);

                anyAdded = true;



            }

            if (!anyAdded)
            {
                data.Misc.y_min = 0;
                data.Misc.y_max = 0;
            }

            return anyAdded;
        }

        private static int PointValueIndex(StockData data, int k, int id)
        {
            double mag = 1.0;

            if (id == 10)
            {
                Magnifier(data.Stock, 1, ref mag);   // 가격선 배율
                return (int)(GetHeatValue(data, k) * mag);
            }

            Magnifier(data.Stock, id, ref mag);
            return (int)(data.Api.x[k, id] * mag);
        }

        // Index stock version: full chart update per call
        public static void UpdateSeries(Chart chart, StockData data)
        {
            if (chart == null || data?.Api?.x == null) return;

            // ✅ UI thread 보장 (원본 구조 유지)
            if (chart.InvokeRequired)
            {
                chart.Invoke((Action)(() => UpdateSeries(chart, data)));
                return;
            }

            if (!ChartLayoutUtils.TryGetDrawRange(data, out int start, out int endEx))
                return;

            if (endEx <= start) return;

            var api = data.Api;
            int idx = endEx - 1;
            if (idx < 0 || idx >= api.nrow) return;

            int timeKey = api.x[idx, 0];
            if (timeKey == 0) return;

            string stock = data.Stock;

            // ✅ AddSeriesLines와 동일 포맷
            string xLabel = ((int)(timeKey / g.HUNDRED)).ToString("D4");

            // ✅ 이번 업데이트 기준 (원본 유지)
            data.Misc.y_min = int.MaxValue;
            data.Misc.y_max = int.MinValue;

            int[] seriesIds = { 1, 3, 4, 5, 6, 10, 11 };

            foreach (int id in seriesIds)
            {
                string seriesName = $"{stock} {id}";
                if (chart.Series.IsUniqueName(seriesName))
                    continue;

                var series = chart.Series[seriesName];
                int value = PointValueIndex(data, idx, id);
                
                    

                // 이전 마지막 라벨 지우기
                int oldIndex = series.Points.Count - 1;
                if (oldIndex >= 0)
                    series.Points[oldIndex].Label = "";

                // 동일 시간축이면 업데이트, 아니면 새 포인트
                if (series.Points.Count > 0 &&
                    series.Points[series.Points.Count - 1].AxisLabel == xLabel)
                {
                    series.Points[series.Points.Count - 1].YValues[0] = value;
                }
                else
                {
                    series.Points.AddXY(xLabel, value);
                }

                // 이번 업데이트 기준 min/max (원본 유지)
                if (value < data.Misc.y_min) data.Misc.y_min = value;
                if (value > data.Misc.y_max) data.Misc.y_max = value;

                Label(chart, series, endEx);
                Mark(chart, series, endEx);
            }

            // ✅ y_min/y_max 방어
            if (data.Misc.y_min == int.MaxValue) data.Misc.y_min = 0;
            if (data.Misc.y_max == int.MinValue) data.Misc.y_max = 0;

            // ---------------------------
            // ✅ 축 설정 (General과 동일: "안에 있으면 유지, 벗어나면 확장")
            // ---------------------------
            if (!chart.ChartAreas.IsUniqueName(stock))
            {
                try
                {
                    var area = chart.ChartAreas[stock];

                    string s1 = $"{stock} 1";
                    if (chart.Series.IndexOf(s1) == -1) return;

                    var series1 = chart.Series[s1];
                    int totalPoints = series1.Points.Count;
                    if (totalPoints < 2) return;

                    // 이번 업데이트 기준으로 계산된 y_min/y_max 사용
                    int yMin = (int)data.Misc.y_min;
                    int yMax = (int)data.Misc.y_max;

                    if (yMin == yMax)
                    {
                        area.AxisY.Minimum = double.NaN;
                        area.AxisY.Maximum = double.NaN;
                        area.RecalculateAxesScale();
                    }
                    else
                    {
                        double pad = (yMax - yMin) * 0.10;
                        double newMin = yMin - 0.2 * pad;
                        double newMax = yMax + 1.5 * pad;

                        double curMin = area.AxisY.Minimum;
                        double curMax = area.AxisY.Maximum;

                        bool curValid = !(double.IsNaN(curMin) ||
                                          double.IsNaN(curMax) ||
                                          curMax <= curMin);

                        if (!curValid)
                        {
                            area.AxisY.Minimum = newMin;
                            area.AxisY.Maximum = newMax;
                        }
                        else
                        {
                            // ✅ 안에 있으면 유지, 벗어나면 필요한 방향만 확장
                            if (newMin < curMin) area.AxisY.Minimum = newMin;
                            if (newMax > curMax) area.AxisY.Maximum = newMax;
                        }
                    }

                    // X축 라벨 간격(좌/우만)
                    area.AxisX.Interval = Math.Max(1, totalPoints - 1);
                }
                catch
                {
                    // 기존 정책: 무시
                }
            }
        }

        private static void Magnifier(string stock, int id, ref double magnifier)
        {
            magnifier = 1.0;
            int i = -1;
            switch (stock) // "가격" : 1, "프외" : "나스닥" : 10, 3, "기타" : 4, 5, 6, 11 
            {
                case "KODEX 레버리지": i = 0; break;
                case "KODEX 코스닥150레버리지": i = 1; break;
            }

            int j = -1;
            if (id == 1)
                j = 0; // 가격
            else if (id == 3 || id == 4 || id == 5 || id == 6 || id == 11)
                j = 1; // 금액 (프외, 기관, 외인, 개인, 연기)
            else
                j = 2; // 기타

            if (i >= 0 && j >= 0)
                magnifier = g.KodexMagnifier[i, j];
        }

        public static void Mark(Chart chart, Series series, int end)
        {
            string stock = "";
            string chartArea = "";
            int columnIndex = 0;

            int seriesEndPoint = 0;
            ChartHandler.SeriesInfomation(series, ref stock, ref chartArea, ref columnIndex, ref seriesEndPoint);

            var data = g.StockRepo.TryGetDataOrNull(stock);
            if (data == null) return;
            var x = data.Api.x;


            if (columnIndex != 1 || x == null)
                return;


            for (int m = 1; m <= seriesEndPoint; m++)
            {
                int mReal = m;
                if (data.Misc.ShrinkDraw)
                {
                    if (end - g.NptsForShrinkDraw >= 0)
                        mReal = m + (end - g.NptsForShrinkDraw);
                }
                if (mReal >= 382)
                    mReal = 381;

                int priceChange = x[mReal, 1] - x[mReal - 1, 1];

                if (priceChange >= 20)
                {
                    Color markerColor = Color.Red;
                    if (priceChange > 40) markerColor = Color.Blue;
                    else if (priceChange > 30) markerColor = Color.Green;

                    series.Points[m].MarkerColor = markerColor;
                    series.Points[m].MarkerSize = 7;
                    series.Points[m].MarkerStyle = MarkerStyle.Circle;
                }
            }
        }

        public static void Label(Chart chart, Series series, int end)
        {
            string stock = "";
            string area = "";
            int columnIndex = 0;

            int seriesEndPoint = 0;
            ChartHandler.SeriesInfomation(series, ref stock, ref area, ref columnIndex, ref seriesEndPoint);

            var data = g.StockRepo.TryGetDataOrNull(stock);
            if (data == null) return;



            var x = data.Api.x;
            string label = "      " + ((int)x[end - 1, columnIndex]).ToString();

            for (int k = end - 1; k >= end - 4; k--)
            {
                if (k - 1 < 0) break;
                double delta = x[k, columnIndex] - x[k - 1, columnIndex];
                label += delta >= 0 ? "+" + delta.ToString() : delta.ToString();
            }

            series.Points[seriesEndPoint].Label = label;
            series.LabelForeColor = colorKODEX[columnIndex];
            series.Font = new Font("Arial Bold", g.v.font + 1, FontStyle.Regular); // 20260331
        }


        private static double GetHeatValue(
            StockData data,
            int i)
        {
            ChartIndexHeat heat =
                data.Stock == "KODEX 레버리지"
                    ? _kospiHeat
                    : _kosdaqHeat;

            if (heat != null &&
                heat.FittedNq != null &&
                i >= 0 &&
                i < heat.FittedNq.Length)
            {
                return heat.FittedNq[i];
            }

            return data.Api.x[i, 10];
        }
    }
}
