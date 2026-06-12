using New_Tradegy.Library.IO;
using New_Tradegy.Library.Models;
using New_Tradegy.Library.Utils;
using OpenQA.Selenium.DevTools.V129.WebAuthn;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using New_Tradegy.Library.Core;
using static New_Tradegy.Library.Models.MiscData;

namespace New_Tradegy.Library.Trackers
{
    internal class ChartGeneral
    {
        // 0, 1(가격), 2(수급), 3(체강), 4(프로그램), 5(외인), 6(기관)
        private static Color[] colorGeneral = { Color.White, Color.Red, Color.DarkGray,
        Color.LightCoral, Color.DarkBlue, Color.Magenta, Color.RoyalBlue, Color.Brown };

        public static (ChartArea area, Annotation anno) UpdateChartArea(Chart chart, StockData data)
        {
            string areaName = data.Stock;
            if (string.IsNullOrEmpty(areaName))
            {
                //Console.WriteLine($"[Error] Empty chart area name for {areaName}");
                return (null, null); // or just null for ChartIndex
            }

            ChartArea area = null;
            Annotation anno = null;

            // Update exiting chartarea
            if (chart.ChartAreas.IndexOf(areaName) >= 0 && !data.Misc.CreateNewChartArea && !data.Misc.ShrinkDraw) // stockName(areaName) exists, update series
            {
                area = chart.ChartAreas[areaName];
                if (data != null && data.Api != null && data.Api.nrow > 0)
                    UpdateSeries(chart, data);
            }
            // Generate a new chartarea
            else
            {
                ChartBasic.RemoveChartBlock(chart, data.Stock); // delete chartarea & related series
                area = CreateChartArea(chart, data);
                if (data.Misc.CreateNewChartArea)
                    data.Misc.CreateNewChartArea = false;
            }

            if (area != null) // anno.Text = annotationText; 존재하면 text만 교체
            {
                anno = Annotation(chart, data); // existing annotation will be deleted in GeneralAnnotation
                //area.BackColor = Color.FromArgb(30, 30, 30); // 20260328
            }


          

            if (area != null)
                UpdateStopLossOverlay(area, data);

            return (area, anno);
        }

        private static void UpdateStopLossOverlay(ChartArea area, StockData data)
        {
            if (data?.Deal == null)
                return;

            if (data.Deal.보유량 == 0)
            {
                data.Deal.수익률 = 0;
                area.BackColor = Color.White;
                return;
            }

            double loss = -data.Deal.수익률;

            // 수익 중 또는 본전
            if (loss <= 0)
            {
                area.BackColor = Color.White;
            }
            // 약한 손실
            else if (loss < 0.3)
            {
                area.BackColor = Color.FromArgb(15, 255, 120, 120);
            }
            // 경계
            else if (loss < 0.6)
            {
                area.BackColor = Color.FromArgb(35, 255, 100, 100);
            }
            // 위험
            else if (loss < 1.0)
            {
                area.BackColor = Color.FromArgb(60, 255, 80, 80);
            }
            // 큰 손실
            else
            {
                area.BackColor = Color.FromArgb(130, 255, 40, 40);
            }
        }

        public static ChartArea CreateChartArea(Chart chart, StockData data)
        {
            var stock = data.Stock;

            if (data.Api.nrow <= 1)
                return null;


            // 시작/끝 행 계산
            if (!ChartLayoutUtils.TryGetDrawRange(data, out int start, out int end))
                return null;

            // 차트 영역 생성
            var area = new ChartArea(stock);
            chart.ChartAreas.Add(area);
            area.Visible = false;

            // 시리즈 추가
            bool success = AddSeriesLines(chart, data, data.Stock, area.Name, start, end);
            if (!success)
            {
                chart.ChartAreas.Remove(area);
                return null;
            }

            if (area.AxisY.Maximum <= area.AxisY.Minimum || area.AxisX.Maximum <= area.AxisX.Minimum)
            {
                area.RecalculateAxesScale();
                return null;
            }


            // X축 설정
            int TotalNumberPoint = end - start;
            area.AxisX.LabelStyle.Enabled = true;
            area.AxisX.MajorGrid.Enabled = false;
            area.AxisX.Interval = TotalNumberPoint - 1;
            area.AxisX.IntervalOffset = 1;


            // Y축 설정
            area.AxisY.LabelStyle.Enabled = false;
            area.AxisY.MajorTickMark.Enabled = false;
            area.AxisY.MinorTickMark.Enabled = false;
            area.AxisY.MajorGrid.Enabled = false;
            area.AxisY.MinorGrid.Enabled = false;

            double padding = (data.Misc.y_max - data.Misc.y_min) * 0.1;
            area.AxisY.Minimum = data.Misc.y_min - 0.2 * padding;
            area.AxisY.Maximum = data.Misc.y_max + 3.5 * padding;

            // 폰트 및 내부 여백
            area.InnerPlotPosition = new ElementPosition(10, 15, 70, 75);
            area.AxisX.LabelStyle.Font = new Font("Arial Bold", 9); // 20260331
            area.AxisY.LabelStyle.Font = new Font("Arial Bold", 9); // 20260331

            if (g.q == "o&s" && data.Score.SectorRank < 5 && !stock.Contains("SECTOR"))
            {
                area.BackColor = g.Colors[data.Score.SectorRank];
            }

            if (data.Api.분프로천[0] > 5 && data.Api.분외인천[0] > 5 && data.Api.분배수차[0] > 0
                && !stock.Contains("SECTOR"))
            {
                area.BackColor = g.Colors[5];
            }

            area.BackColor = Color.Transparent;

            if (stock == g.StockManager.Next)
                area.BackColor = Color.FromArgb(60, 80, 160, 255);   // 연파랑
            else if (stock == g.StockManager.Active)
                area.BackColor = Color.FromArgb(70, 255, 120, 120);  // 연빨강

            

            return area;
        }


        public static void UpdateSeries(Chart chart, StockData data)
        {
            if (chart == null || data?.Api?.x == null) return;

            if (!ChartLayoutUtils.TryGetDrawRange(data, out int start, out int end))
                return;

            string stock = data.Stock;

            int lastRow = end - 1;
            if (lastRow < 0) return;
            if (data.Api.x[lastRow, 0] == 0) return;

            // ✅ General: 1,4,5,6
            int[] seriesIds = { 1, 4, 5, 6 };

            string xLabel = ((int)(data.Api.x[lastRow, 0] / g.HUNDRED)).ToString("D4");

            int priceMin = int.MaxValue, priceMax = int.MinValue;
            int flowMin = int.MaxValue, flowMax = int.MinValue;

            // 이번 업데이트 기준 min/max
            data.Misc.y_min = int.MaxValue;
            data.Misc.y_max = int.MinValue;

            Action updateAction = () =>
            {
                foreach (int typeId in seriesIds)
                {
                    string seriesName = stock + " " + typeId;

                    if (chart.Series.IsUniqueName(seriesName))
                        continue;

                    var series = chart.Series[seriesName];
                    int value = PointValue(data, lastRow, typeId);

                    // 이전 마지막 라벨 제거
                    int oldIndex = series.Points.Count - 1;
                    if (oldIndex >= 0)
                        series.Points[oldIndex].Label = "";

                    // 동일 시간축이면 업데이트
                    if (series.Points.Count > 0 &&
                        series.Points[series.Points.Count - 1].AxisLabel == xLabel)
                    {
                        series.Points[series.Points.Count - 1].YValues[0] = value;
                    }
                    else
                    {
                        series.Points.AddXY(xLabel, value);
                    }

                    // 전체 min/max 갱신
                    if (value < data.Misc.y_min) data.Misc.y_min = value;
                    if (value > data.Misc.y_max) data.Misc.y_max = value;

                    // price vs flow 분리
                    if (typeId == 1)
                    {
                        if (value < priceMin) priceMin = value;
                        if (value > priceMax) priceMax = value;
                    }
                    else
                    {
                        if (value < flowMin) flowMin = value;
                        if (value > flowMax) flowMax = value;
                    }

                    Label(chart, series, end);
                    Mark(chart, series, end);
                }

                // 값이 하나도 없으면 방어
                if (data.Misc.y_min == int.MaxValue) data.Misc.y_min = 0;
                if (data.Misc.y_max == int.MinValue) data.Misc.y_max = 0;

                // ---------------------------
                // ✅ 축 설정 (확장 방식)
                // ---------------------------
                if (!chart.ChartAreas.IsUniqueName(stock))
                {
                    try
                    {
                        var area = chart.ChartAreas[stock];

                        string s1 = stock + " 1";
                        if (chart.Series.IndexOf(s1) == -1)
                            return;

                        var series1 = chart.Series[s1];
                        int totalPoints = series1.Points.Count;
                        if (totalPoints < 2)
                            return;

                        // 사용할 yMin/yMax 결정
                        int yMin = (priceMin == int.MaxValue || priceMax == int.MinValue || priceMin == priceMax)
                                    ? (int)data.Misc.y_min
                                    : priceMin;

                        int yMax = (priceMin == int.MaxValue || priceMax == int.MinValue || priceMin == priceMax)
                                    ? (int)data.Misc.y_max
                                    : priceMax;

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
                            double newMax = yMax + 3.5 * pad;

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
                                // ✅ 안에 있으면 유지
                                // ✅ 벗어나면 필요한 방향만 확장
                                if (newMin < curMin)
                                    area.AxisY.Minimum = newMin;

                                if (newMax > curMax)
                                    area.AxisY.Maximum = newMax;
                            }
                        }

                        // X축 간격 유지
                        area.AxisX.Interval = Math.Max(1, totalPoints - 1);
                    }
                    catch
                    {
                        // 기존 정책 유지
                    }
                }
            };

            if (chart.InvokeRequired)
                chart.Invoke(updateAction);
            else
                updateAction();
        }

        private static bool AddSeriesLines(
            Chart chart,
            StockData data,
            string stockName,
            string area,
            int start,
            int end)
        {
            if (chart == null || data == null || data.Api == null || data.Api.x == null)
                return false;

            // 7 제거 (price=1, program=4, foreign=5, institute=6)
            int[] seriesIds = { 1, 4, 5, 6 };

            var lineTypes = new Dictionary<int, (string label, Color color, int width)>
    {
        { 1, ("price",      colorGeneral[1], g.LineWidth) },
        { 4, ("program",    colorGeneral[4], g.LineWidth) },
        { 5, ("foreign",    colorGeneral[5], g.LineWidth) },
        { 6, ("institute",  colorGeneral[6], g.LineWidth) },
    };

            // ✅ 기존 Misc.y_min/y_max만 사용 (단, start~end 구간 기준으로 계산)
            data.Misc.y_min = int.MaxValue;
            data.Misc.y_max = int.MinValue;

            // ✅ 데이터 유효성 체크: start~end 범위에서 실제 값이 있는지
            bool hasData = false;
            for (int k = start; k < end; k++)
            {
                if (data.Api.x[k, 0] == 0) break;

                foreach (var id in seriesIds)
                {
                    if (data.Api.x[k, id] != 0)
                    {
                        hasData = true;
                        break;
                    }
                }

                if (hasData) break;
            }
            if (!hasData) return false;

            bool anySeriesAdded = false;

            for (int i = 0; i < seriesIds.Length; i++)
            {
                int typeId = seriesIds[i];

                if (!lineTypes.TryGetValue(typeId, out var lt))
                    continue;

                string sid = $"{stockName} {typeId}";

                // 중복 Add 방지 (이미 있으면 이번 호출에서는 스킵)
                if (!chart.Series.IsUniqueName(sid))
                    continue;

                // 포인트 먼저 모아두고, UI thread에서 한 번에 Add (Invoke 최소화)
                var points = new List<(string xLabel, int value)>(Math.Max(0, end - start));


                for (int k = start; k < end; k++)
                {


                    int value = PointValue(data, k, typeId);

                    // 예: 0951, 1020 같은 형태로 표시하고 싶으면 "D4" 추천
                    // (지금처럼 951도 괜찮으면 ToString() 유지해도 됨)
                    int hhmm = (int)(data.Api.x[k, 0] / g.HUNDRED);
                    string xLabel = hhmm.ToString("D4");

                    points.Add((xLabel, value));


                    if (value < data.Misc.y_min) data.Misc.y_min = value;
                    if (value > data.Misc.y_max) data.Misc.y_max = value;
                }

                // 이 시리즈는 데이터가 부족하면 스킵 (전체 실패로 보지 않음)
                if (points.Count < 2)
                    continue;

                var series = new Series(sid)
                {
                    ChartArea = area,
                    ChartType = SeriesChartType.Line,

                    // xLabel이 문자열이므로 String이 맞음
                    XValueType = ChartValueType.String,

                    IsVisibleInLegend = false,
                    Color = lt.color,
                    BorderWidth = lt.width
                };

                Action addSeriesAndPoints = () =>
                {
                    chart.Series.Add(series);

                    // 포인트 한 번에 추가
                    for (int p = 0; p < points.Count; p++)
                    {
                        var (xLabel, value) = points[p];
                        series.Points.AddXY(xLabel, value);
                    }

                    // Mark/Label은 "실제 마지막 유효 인덱스" 기준으로 호출
                    // (기존 Mark/Label 시그니처가 end를 원하더라도,
                    //  내부가 인덱스를 참조한다면 lastK가 안전)
                    Mark(chart, series, end);
                    Label(chart, series, end);
                };

                if (chart.InvokeRequired)
                    chart.Invoke(addSeriesAndPoints);
                else
                    addSeriesAndPoints();

                anySeriesAdded = true;
            }

            // 모든 시리즈가 스킵된 경우 (예: 특정 구간에만 price만 있고 나머지 없음 등)
            if (!anySeriesAdded)
                return false;

            // y_min/y_max가 갱신되지 않은 경우 방어
            if (data.Misc.y_min == int.MaxValue) data.Misc.y_min = 0;
            if (data.Misc.y_max == int.MinValue) data.Misc.y_max = 0;

            return true;
        }

        private static int PointValue(StockData data, int k, int id)
        {
            if (data?.Api == null || data.Api.x == null) return 0;

            bool isSector = !string.IsNullOrEmpty(data.Stock) &&
                            data.Stock.StartsWith("SECTOR:", StringComparison.Ordinal);

            switch (id)
            {
                case 1:
                    // ✅ 1만 가격과장배수
                    return (int)Math.Round(data.Api.x[k, 1] * data.Misc.가격과장배수);

                case 4:
                case 5:
                case 6:
                    {
                        //double dayProgressRatio = StandardCurve.GetG(data.Api.x[k, 0]); // 필요하면 나중에 곱

                        double avg20_10M = data.Statistics?.AvgDailyTurnover_10M ?? 0.0; // (천만원)
                        if (avg20_10M <= 0) return 0;

                        double money10M;

                        if (isSector)
                        {
                            // ✅ 섹터: 이미 누적 돈(10M) 저장
                            money10M = data.Api.x[k, id];
                        }
                        else
                        {
                            // ✅ 일반종목: 수량 → 돈(10M)
                            double price = data.Api.전일종가;
                            if (price <= 0) return 0;

                            double qty = data.Api.x[k, id]; // 4,5,6 : 수량(누적)
                            if (qty == 0) return 0;

                            money10M = qty * price / g.천만원;
                        }

                        if (money10M == 0) return 0;

                        // ✅ avg20 대비 %
                        double pct = money10M / avg20_10M * 100.0;
                        if (double.IsNaN(pct) || double.IsInfinity(pct)) return 0;

                        // ✅ 기존과 동일: %를 100배 스케일 (즉 1% = 100)
                        return (int)Math.Round(pct * 100); // * dayProgressRatio (원하면)
                    }

                case 7:
                    // ✅ 7은 라인으로 안 그리므로 여기서도 차단
                    return 0;

                default:
                    return 0;
            }
        }

        public static void Mark(Chart chart, Series series, int endEx)
        {
            string stock = "";
            string chartAreaName = "";
            int columnIndex = 0;
            int seriesEndPoint = 0;

            ChartHandler.SeriesInfomation(
                series,
                ref stock,
                ref chartAreaName,
                ref columnIndex,
                ref seriesEndPoint);

            var data = g.StockRepo.TryGetDataOrNull(stock);
            if (data?.Api?.x == null) return;

            var x = data.Api.x;
            int nrow = data.Api.nrow;
            if (nrow <= 1) return;

            // price / amount / intensity only
            if (columnIndex > 3) return;

            // endEx 방어
            if (endEx <= 1 || endEx > nrow)
                endEx = Math.Min(Math.Max(endEx, 2), nrow);

            int lastRow = endEx - 1;

            // shrink 시 화면 startRow
            int startRow = 0;
            if (data.Misc.ShrinkDraw)
                startRow = Math.Max(0, endEx - g.NptsForShrinkDraw);

            // series 포인트 범위 방어
            int ptCount = series.Points.Count;
            int mMax = Math.Min(seriesEndPoint, ptCount - 1);
            if (mMax < 1) return;

            // Point 0 마킹은 루프 밖에서 한 번만
            if (columnIndex == 1 &&
                data.Api.분거래천 != null &&
                data.Api.분거래천.Length > 0 &&
                data.Api.분거래천[0] > 10)
            {
                double val = data.Api.분거래천[0];
                int markSize;
                Color color;

                if (val < 50) { color = Color.Red; markSize = 10; }
                else if (val < 100) { color = Color.Red; markSize = 15; }
                else if (val < 200) { color = Color.Green; markSize = 15; }
                else if (val < 300) { color = Color.Green; markSize = 20; }
                else if (val < 500) { color = Color.Blue; markSize = 20; }
                else if (val < 800) { color = Color.Blue; markSize = 30; }
                else if (val < 1200) { color = Color.Black; markSize = 30; }
                else if (val < 1700) { color = Color.Black; markSize = 40; }
                else { color = Color.Black; markSize = 50; }

                series.Points[0].MarkerColor = color;
                series.Points[0].MarkerSize = markSize;

                int lastPriceChange = x[lastRow, 1] - x[lastRow - 1, 1];
                series.Points[0].MarkerStyle =
                    lastPriceChange >= 0 ? MarkerStyle.Circle : MarkerStyle.Cross;
            }

            for (int m = 1; m <= mMax; m++)
            {
                int mReal = startRow + m;
                if (mReal <= 0) continue;
                if (mReal >= nrow) break;

                if (columnIndex == 1)
                {
                    int priceChange = x[mReal, 1] - x[mReal - 1, 1];

                    // 1) 큰 가격변화
                    if (priceChange >= 100)
                    {
                        //if (priceChange > 300)
                        //    series.Points[m].MarkerColor = Color.Black;
                        //else if (priceChange > 200)
                        //    series.Points[m].MarkerColor = Color.Blue;inflextion 
                        //else if (priceChange > 150)
                        //    series.Points[m].MarkerColor = Color.Green;
                        //else
                        //    series.Points[m].MarkerColor = Color.Red;

                        //series.Points[m].MarkerSize = 6;
                        //series.Points[m].MarkerStyle = MarkerStyle.Cross;
                    }

                    // 2) 09:02 이후 배수차
                    if (x[mReal, 0] >= 90100)
                    {
                        //int diff = x[mReal, 8] - x[mReal, 9];

                        //if (diff > 100)
                        //{
                        //    series.Points[m].MarkerStyle = MarkerStyle.Diamond;

                        //    if (diff > 300)
                        //    {
                        //        series.Points[m].MarkerColor = Color.Black;
                        //        series.Points[m].MarkerSize = 9;
                        //    }
                        //    else if (diff > 210)
                        //    {
                        //        series.Points[m].MarkerColor = Color.Blue;
                        //        series.Points[m].MarkerSize = 9;
                        //    }
                        //    else if (diff > 150)
                        //    {
                        //        series.Points[m].MarkerColor = Color.Green;
                        //        series.Points[m].MarkerSize = 9;
                        //    }
                        //    else
                        //    {
                        //        series.Points[m].MarkerColor = Color.Red;
                        //        series.Points[m].MarkerSize = 9;
                        //    }
                        //}
                    }

                    // 3) 09:02 이후 배수합
                    // 제일 마지막에 두어 가격변화/배수차 마크를 최종 덮어씀
                    if (x[mReal, 0] >= 90000)
                    {
                        int sum = x[mReal, 8] + x[mReal, 9];

                        if (sum > 100)
                        {
                            series.Points[m].MarkerStyle = MarkerStyle.Circle;

                            if (sum > 300)
                            {
                                series.Points[m].MarkerColor = Color.Black;
                                series.Points[m].MarkerSize = 10;
                            }
                            else if (sum > 210)
                            {
                                series.Points[m].MarkerColor = Color.Blue;
                                series.Points[m].MarkerSize = 9;
                            }
                            else if (sum > 150)
                            {
                                series.Points[m].MarkerColor = Color.Green;
                                series.Points[m].MarkerSize = 9;
                            }
                            else
                            {
                                series.Points[m].MarkerColor = Color.Red;
                                series.Points[m].MarkerSize = 9;
                            }
                        }
                    }
                }

                //if (columnIndex == 2 || columnIndex == 3)
                //{
                //    int threshold = g.npts_for_magenta_cyan_mark;
                //    int v = x[mReal, columnIndex + 8];

                //    if (v >= threshold)
                //    {
                //        series.Points[m].MarkerColor =
                //            (columnIndex == 2) ? Color.Magenta : Color.Cyan;

                //        series.Points[m].MarkerStyle = MarkerStyle.Cross;
                //        series.Points[m].MarkerSize = 7;
                //    }
                //}
            }
        }


        public static void Label(Chart chart, Series t, int endEx)
        {
            string stock = "", area = "";
            int columnIndex = 0, seriesEndPoint = 0;
            ChartHandler.SeriesInfomation(t, ref stock, ref area, ref columnIndex, ref seriesEndPoint);

            var data = g.StockRepo.TryGetDataOrNull(stock);
            if (data?.Api?.x == null)
                return;

            int lastPt = t.Points.Count - 1;
            if (lastPt < 0)
                return;
            if (seriesEndPoint < 0 || seriesEndPoint > lastPt)
                seriesEndPoint = lastPt;

            var api = data.Api;
            var x = api.x;

            // endEx 방어
            if (endEx <= 1) endEx = api.nrow;
            endEx = Math.Min(endEx, api.nrow);
            if (endEx <= 1)
                return;

            // ✅ 화면 포인트와 같은 기준으로 idx 계산 (shrink 동기화)
            int startRow = data.Misc.ShrinkDraw
                ? Math.Max(0, endEx - g.NptsForShrinkDraw)
                : 0;

            int idx = startRow + seriesEndPoint;
            if (idx < 0 || idx >= api.nrow)
                return;

            var post = data.Post;

            t.LabelForeColor = colorGeneral[columnIndex];
            t.Font = new Font("Arial Bold", g.v.font + 2, FontStyle.Regular, GraphicsUnit.Point, 0);

            string s = "";

            bool isSector = !string.IsNullOrEmpty(stock) &&
                            stock.StartsWith("SECTOR:", StringComparison.OrdinalIgnoreCase);



            // 공통: idx 기반 r0,r1,r2,r3 (라벨용 4틱)
            void GetRows(out int r0, out int r1, out int r2, out int r3)
            {
                r0 = idx;
                r1 = (r0 > 0) ? (r0 - 1) : 0;
                r2 = (r1 > 0) ? (r1 - 1) : 0;
                r3 = (r2 > 0) ? (r2 - 1) : 0;
            }

            // 공통: 섹터 누적/차분(분/분전/분전전) 라벨 문자열 만들기
            string BuildSectorCumDeltaLabel(int col)
            {
                GetRows(out int r0, out int r1, out int r2, out int r3);

                int cum10M = x[r0, col];
                int d0_10M = x[r0, col] - x[r1, col];
                int d1_10M = x[r1, col] - x[r2, col];
                int d2_10M = x[r2, col] - x[r3, col];

                double cum = ToEokFrom10M(cum10M);
                double d0 = ToEokFrom10M(d0_10M);
                double d1 = ToEokFrom10M(d1_10M);
                double d2 = ToEokFrom10M(d2_10M);

                return FormatEok(cum)
                     + (d0 >= 0 ? "+" : "") + FormatEok(d0)
                     + (d1 >= 0 ? "+" : "") + FormatEok(d1)
                     + (d2 >= 0 ? "+" : "") + FormatEok(d2);
            }

            switch (columnIndex)
            {
                case 1:
                    s = "      " + x[idx, 1].ToString();
                    for (int k = idx; k >= idx - 2; k--)
                    {
                        if (k - 1 < 0) break;
                        int d = x[k, 1] - x[k - 1, 1];
                        s += (d >= 0 ? "+" : "") + d.ToString();
                    }
                    break;

                case 2:
                    s = x[idx, 2].ToString();
                    break;

                case 3:
                    s = (x[idx, 3] / 100).ToString();
                    break;

                case 4: // ===== 프로 =====
                    if (!isSector)
                    {
                        if (post == null || api.분프로천 == null) return;

                        s = FormatEok(post.프누천 / 10.0);
                        int kMax = Math.Min(3, api.분프로천.Length);
                        for (int k = 0; k < kMax; k++)
                        {
                            double d = api.분프로천[k] / 10.0; // 억원
                            s += (d >= 0 ? "+" : "") + FormatEok(d);
                        }
                    }
                    else
                    {
                        // ✅ 섹터: x[*,4]는 proCum10M
                        s = BuildSectorCumDeltaLabel(4);
                    }
                    break;

                case 5: // ===== 외인 =====
                    if (!isSector)
                    {
                        if (post == null || api.분외인천 == null) return;

                        s = FormatEok(post.외누천 / 10.0);
                        int kMax = Math.Min(3, api.분외인천.Length);
                        for (int k = 0; k < kMax; k++)
                        {
                            double d = api.분외인천[k] / 10.0; // 억원
                            s += (d >= 0 ? "+" : "") + FormatEok(d);
                        }
                    }
                    else
                    {
                        // ✅ 섹터: x[*,5]는 forCum10M
                        s = BuildSectorCumDeltaLabel(5);
                    }
                    break;

                case 6: // ===== 기관 =====
                    if (!isSector)
                    {
                        if (post == null || api.분기관천 == null) return;

                        s = FormatEok(post.기누천 / 10.0);
                        int kMax = Math.Min(3, api.분기관천.Length);
                        for (int k = 0; k < kMax; k++)
                        {
                            double d = api.분기관천[k] / 10.0; // 억원
                            s += (d >= 0 ? "+" : "") + FormatEok(d);
                        }
                    }
                    else
                    {
                        // ✅ 섹터: x[*,6]는 instCum10M
                        s = BuildSectorCumDeltaLabel(6);
                    }
                    break;

                case 7:
                    {
                        var flow = data.Misc?.Flow;
                        if (flow == null) return;

                        double v = flow.PctTotal;
                        s = v.ToString("0.00");
                        t.Points[seriesEndPoint].Label = s;

                        t.Font = new Font("Arial Bold", g.v.font + 2.5f, FontStyle.Bold, GraphicsUnit.Point, 0);
                        return;
                    }

                default:
                    return;
            }

            t.Points[seriesEndPoint].Label = s;
        }

        private static double ToEokFrom10M(int money10M)
        {
            return money10M / 10.0;
        }

        public static Annotation CreateAnnotationOrOverlay(
            Chart chart,
            string areaName,
            RectangleF rect,
            string text,
            Color textColor,
            Color backgroundColor)
        {
            if (chart.ChartAreas.IndexOf(areaName) < 0)
                return null;

            float font = g.v.font;
            if (areaName.Contains("KODEX"))
                font += 2;

            // ★ DPI / 랩탑 대응
            float w = rect.Width * 1.25f;
            float h = rect.Height * 1.25f;

            // 너무 커져서 옆 영역 침범 방지
            if (rect.X + w > 100f)
                w = 100f - rect.X;

            if (rect.Y + h > 100f)
                h = 100f - rect.Y;

            var annotation = new RectangleAnnotation
            {
                Name = areaName,
                Text = text,
                Font = new Font("Arial", font + 1, FontStyle.Bold),
                ForeColor = textColor,
                BackColor = backgroundColor,
                LineColor = Color.Transparent,
                Alignment = ContentAlignment.TopLeft,
                IsSizeAlwaysRelative = true,

                // ★ 핵심
                AxisX = null,
                AxisY = null,
                ClipToChartArea = null,

                X = rect.X,
                Y = rect.Y,
                Width = rect.Width,
                Height = rect.Height
            };

            chart.Annotations.Add(annotation);
            return annotation;
        }
     


        // ===== api.x 컬럼 인덱스 (너가 말한 기준) =====
        private const int COL_TIME = 0;
        private const int COL_PRICE = 1;
        private const int COL_PRO = 4;
        private const int COL_FOR = 5;
        private const int COL_INST = 6;
        private const int COL_MONEY = 7;
        private const int COL_BUY_M = 8;
        private const int COL_SEL_M = 9;

        public static Annotation Annotation(Chart chart, StockData data)
        {
            if (chart == null || data?.Api?.x == null || string.IsNullOrEmpty(data.Stock))
                return null;

            var api = data.Api;
            var x = api.x;
            int nrow = api.nrow;
            if (nrow <= 1) return null;

            if (!ChartLayoutUtils.TryGetDrawRange(data, out int start, out int end))
                return null;

            if (end <= start) return null;

            int lastRow = end - 1;
            if (lastRow < 0 || lastRow >= nrow) return null;

            bool isSector = data.Stock.StartsWith("SECTOR:", StringComparison.OrdinalIgnoreCase);

            string text = isSector
                ? SectorAnnoationText(chart, data, x, start, lastRow, nrow)
                : GeneralAnnotationText(chart, data, x, start, lastRow, nrow);

            string wrapped = text ?? string.Empty;
            if (!isSector)
                wrapped = Utils.StringUtils.TruncateLinesRemovePartial(wrapped, 20);

            Color back = (g.q == "o&s" && data.Score.SectorRank < 5 && !isSector)
                ? g.Colors[data.Score.SectorRank]
                : Color.White;

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

            var name = data.Stock;
            if (chart.Annotations.FindByName(name) is TextAnnotation t)
            {
                t.Text = wrapped;
                t.BackColor = back;
                t.Visible = false;
                return t;
            }

            var anno = CreateAnnotationOrOverlay(
                chart, name,
                new RectangleF(0f, yLocation, width, 7f),
                wrapped, Color.Black, back);

            anno.Visible = false;
            return anno;
        }

        // ============================================================
        // SECTOR 텍스트 생성 (4줄)
        //
        // 1) 첫째줄: 섹터명 + 랭킹 4개 (Money|Pro|For|Inst)
        // 2) 둘째줄: 배수차/배수합 (분|분전|분전전)  +diff/sum 형태
        // 3) 셋째줄: (현재분) Pro/Money, For/Money 따로  | (분전/분전전) (Pro+For)/Money
        // 4) 넷째줄: dayprogress 반영 MoneyPctAdj | ProShare | ForShare | InstShare
        // ============================================================
        public static string SectorAnnoationText(
            Chart chart,
            StockData data,
            int[,] api,
            int startRow,
            int lastRow,
            int nrow,
            int? testRow = null
        )
        {
            if (data == null || api == null || nrow <= 0) return string.Empty;

            int baseRow = testRow ?? lastRow;
            if (baseRow < 0) return string.Empty;
            baseRow = Clamp(baseRow, 0, nrow - 1);

            int r0 = baseRow;
            int r1 = (r0 > 0) ? (r0 - 1) : 0;
            int r2 = (r1 > 0) ? (r1 - 1) : 0;

            var sb = new StringBuilder(128);

            // ---- 1) 첫째 줄 ----
            string titleKey = data.Stock.StartsWith("SECTOR:", StringComparison.OrdinalIgnoreCase)
                ? data.Stock.Substring("SECTOR:".Length)
                : data.Stock;

            // ----- passed 기준 0~100 점수 (M, P, F, I 순서 - 라벨 생략) -----
            double m = data?.Score?.GetPct(ScoreKey.MinMoney_10M) ?? 0;
            double p = data?.Score?.GetPct(ScoreKey.MinPro_10M) ?? 0;
            double f = data?.Score?.GetPct(ScoreKey.MinFor_10M) ?? 0;
            double d = data?.Score?.GetPct(ScoreKey.MinDiff) ?? 0;
            double s = data?.Score?.GetPct(ScoreKey.MinSum) ?? 0;

            sb.Append(titleKey);
            sb.Append($" {d:0}/{s:0} {m:0} {p:0} {f:0}");

            // ---- 2) 둘째 줄 ---- 배수차/배수합 (8/9는 매수배/매도배 그대로)
            sb.AppendLine();
            sb.Append(FormatDiffSum(api[r0, COL_BUY_M], api[r0, COL_SEL_M])).Append("  ")
              .Append(FormatDiffSum(api[r1, COL_BUY_M], api[r1, COL_SEL_M])).Append("  ")
              .Append(FormatDiffSum(api[r2, COL_BUY_M], api[r2, COL_SEL_M]));

            // ---- 3) 셋째 줄 ---- (프로+외인)/거래 : "해당 분"이 아니라 r0시점 누적 기준 표시
            sb.AppendLine();

            // 몇 분 전까지 표시할지
            const int LOOKBACK = 3;   // 현재 포함 4개 (0,1,2,3)

            // 루프: 0 = 현재분, 1 = 분전, 2 = 분전전, 3 = 분전전전
            for (int i = 0; i < LOOKBACK; i++)
            {
                int row = r0 - i;
                if (row < 0) break;

                int prevRow = row - 1;

                int moneyCur = api[row, COL_MONEY];
                int proCur = api[row, COL_PRO];
                int forCur = api[row, COL_FOR];

                int moneyPrev = (prevRow >= 0) ? api[prevRow, COL_MONEY] : 0;
                int proPrev = (prevRow >= 0) ? api[prevRow, COL_PRO] : 0;
                int forPrev = (prevRow >= 0) ? api[prevRow, COL_FOR] : 0;

                int dealt10M = moneyCur - moneyPrev;
                int pro10M = proCur - proPrev;
                int for10M = forCur - forPrev;

                if (dealt10M < 0) dealt10M = 0;
                if (pro10M < 0) pro10M = 0;
                if (for10M < 0) for10M = 0;

                double proEok = pro10M / 10.0;
                double forEok = for10M / 10.0;
                double dealtEok = dealt10M / 10.0;

                if (i > 0)
                    sb.Append("  ");   // 분 사이 간격

                if (i == 0)
                {
                    sb.Append(proEok.ToString("0"))
                  .Append("+")
                  .Append(forEok.ToString("0"))
                  .Append("/")
                  .Append(dealtEok.ToString("0"));
                }
                else
                {
                    double sumEok = proEok + forEok;
                    sb.Append(sumEok.ToString("0"))
                  .Append("/")
                  .Append(dealtEok.ToString("0"));
                }
            }






            // ---- 4) 넷째 줄(dayprogress 보정) ----
            sb.AppendLine();

            // minuteKey는 api 시간열에서 가져오는 게 제일 정확
            // COL_TIME이 실제 시간 컬럼이면 이걸로 교체해줘
            int hhmmss = api[r0, COL_TIME];

            int moneyCum10M = api[r0, COL_MONEY];
            double dayProgressRatio = (hhmmss > 0 && hhmmss <= 235959) ? StandardCurve.GetG(hhmmss) : 0.0;
            double dayFactor = (dayProgressRatio > 0.0001) ? (1.0 / dayProgressRatio) : 1.0;

            // ✅ dayprogress 보정된 누적 거래대금(10M)
            double moneyAdj10M = moneyCum10M * dayFactor;

            // ✅ 20일평균(10M) 대비 배수(또는 %). 너의 화면 스타일에 맞춰 택1.
            double avg20_10M = data.Statistics?.AvgDailyTurnover_10M ?? 0.0;
            if (avg20_10M <= 0) avg20_10M = 1;

            // "배수"로 보고 싶으면:
            double moneyMult = moneyAdj10M / avg20_10M;      // 예: 1.25배
                                                             // "퍼센트"로 보고 싶으면:
                                                             // double moneyPct = moneyAdj10M / avg20_10M * 100.0;

            // 점유율(누적 기준): pro/for/inst 비중
            int proShare = (int)Math.Round(SafeRatioPct(api[r0, COL_PRO], moneyCum10M), MidpointRounding.AwayFromZero);
            int forShare = (int)Math.Round(SafeRatioPct(api[r0, COL_FOR], moneyCum10M), MidpointRounding.AwayFromZero);
            int instShare = (int)Math.Round(SafeRatioPct(api[r0, COL_INST], moneyCum10M), MidpointRounding.AwayFromZero);

            // 출력: "총강도(배수) | pro% | for% | inst%"
            sb.Append(moneyMult.ToString("0.00")).Append(" | ")
              .Append(proShare).Append(" | ")
              .Append(forShare).Append(" | ")
              .Append(instShare);

            return sb.ToString();
        }

        //private static int SafeRatioPctInt(int num, int money)
        //{
        //    if (money == 0) return 0;
        //    // num/money * 100 (정수 %)
        //    double pct = (double)num * 100.0 / money;
        //    if (double.IsNaN(pct) || double.IsInfinity(pct)) return 0;
        //    return (int)Math.Round(pct, MidpointRounding.AwayFromZero);
        //}

        //// 부호 포맷:
        //// -3 => "-3"
        ////  0 => "0"
        ////  5 => (plusForPositive=false) "5", (true) "+5"
        //private static string FormatSigned(int v, bool plusForPositive)
        //{
        //    if (v == 0) return "0";
        //    if (v > 0) return plusForPositive ? ("+" + v.ToString()) : v.ToString();
        //    return v.ToString(); // 음수는 -가 포함됨
        //}


        // ====== (선택) 철자 교정 버전도 같이 제공: 호출부가 SectorAnnotationText를 쓰고 싶을 때 ======
        public static string SectorAnnotationText(Chart chart, StockData data, int[,] api, int startRow, int lastRow, int nrow)
            => SectorAnnoationText(chart, data, api, startRow, lastRow, nrow);

        // ---------------- helper ----------------

        private static int Clamp(int v, int lo, int hi) => (v < lo) ? lo : (v > hi) ? hi : v;

        private static string FormatDiffSum(int buyMult, int sellMult)
        {
            int diff = buyMult - sellMult;
            int sum = buyMult + sellMult;
            // 예: +12/34, -3/19
            return $"{diff:#;-#;0}/{sum}";
        }

        private static void AppendCurProForOverMoney(StringBuilder sb, int[,] api, int r)
        {
            // 현재분: Pxx Fyy 형태 (xx,yy는 %)
            sb.Append("P").Append(FormatPctSafe(api[r, COL_PRO], api[r, COL_MONEY]))
              .Append(" ")
              .Append("F").Append(FormatPctSafe(api[r, COL_FOR], api[r, COL_MONEY]));
        }

        // 분자/분모가 같은 스케일(누적%×100)이라면 비율은 그대로 의미있음
        private static string FormatPctSafe(int num, int den)
        {
            if (den == 0) return "0";
            double pct = (double)num / den * 100.0;
            // 너무 큰 값/NaN 방지
            if (double.IsNaN(pct) || double.IsInfinity(pct)) return "0";
            // 표시를 짧게: 정수
            int v = (int)Math.Round(pct, MidpointRounding.AwayFromZero);
            return v.ToString();
        }

        private static double SafeRatioPct(int num, int den)
        {
            if (den == 0) return 0;
            double pct = (double)num / den * 100.0;
            if (double.IsNaN(pct) || double.IsInfinity(pct)) return 0;
            // 필요하면 클램프
            if (pct > 999) pct = 999;
            if (pct < -999) pct = -999;
            return pct;
        }

        /// <summary>
        /// dayprogress factor를 얻는다. (예: 진행률 50%면 2.0, 80%면 1.25)
        /// 네 프로젝트에 이미 dayprogress 계산 함수가 있으면 그걸로 대체하면 됨.
        /// 여기선 "없어도 안전하게 1.0"이 목표.
        /// </summary>
        //private static bool TryGetDayProgressFactor(Chart chart, StockData data, out double factor)
        //{
        //    factor = 1.0;

        //    // 1) 이미 data에 계산되어 있다면 (예시: data.DayProgressFactor 같은 필드/프로퍼티)
        //    // if (data.DayProgressFactor > 0) { factor = data.DayProgressFactor; return true; }

        //    // 2) Api에 있다면 (예시)
        //    // if (data.Api.DayProgressFactor > 0) { factor = data.Api.DayProgressFactor; return true; }

        //    // 3) ChartLayoutUtils에 함수가 있다면 (예시)
        //    // if (ChartLayoutUtils.TryGetDayProgressFactor(data, out factor)) return true;

        //    // 지금은 연결점이 불명확하니 기본 1.0
        //    return false;
        //}


        // ✅ annotation용: "거래량/프로/외인/기관" 숫자 요약
        private static string BuildFlowSummaryNoPct(StockData data, int row)
        {
            if (data?.Api == null) return "0/0/0/0";

            bool isSector = !string.IsNullOrEmpty(data.Stock) &&
                            data.Stock.StartsWith("SECTOR:");

            int vol, pro, frn, ins;

            if (isSector)
            {
                // ✅ SECTOR: 이미 %/강도로 들어있음 (SectorBuilder에서 만든 값)
                vol = data.Api.x[row, 7];
                pro = data.Api.x[row, 4];
                frn = data.Api.x[row, 5];
                ins = data.Api.x[row, 6];
            }
            else
            {
                // ✅ General: 거래량(진행률)은 누적거래량을 거래대금으로 환산해 계산
                vol = CalcVolumePctInt(data, row);
                pro = PointValue(data, row, 4) / 100;
                frn = PointValue(data, row, 5) / 100;
                ins = PointValue(data, row, 6) / 100;
            }

            return $"{vol}";
            //return $"{vol}/{pro}/{frn}/{ins}";
        }


        // ✅ annotation용 거래량%: 7번(누적거래량)을 "거래대금 기준"으로 20일평균대비 환산
        // - 7번 라인은 안 그리지만, 데이터(누적거래량) 자체는 여기서만 활용
        public static int CalcVolumePctInt(StockData data, int row)
        {
            if (data?.Api == null) return 0;

            // 1) 20일 평균 하루 거래대금 (천만원 단위)
            double avg20_10M = data.Statistics?.AvgDailyTurnover_10M ?? 0.0;
            if (avg20_10M <= 0) return 0;

            // 2) 현시점 누적 수량 (네 정의: x[row,7])
            double qty = data.Api.x[row, 7];
            if (qty <= 0) return 0;

            // 3) 가격 (전일종가 우선)
            double price = data.Api.전일종가;
            if (price <= 0) price = data.Api.현재가;
            if (price <= 0) return 0;

            // 4) 현시점 누적 거래대금 (천만원)
            double money10M = qty * price / 10_000_000; //g.천만원 아래의 double g 정의로 안 먹음
            if (money10M <= 0) return 0;

            // 5) 원래 방식: 단순 누적 비율 (%)
            double pctRaw = money10M / avg20_10M * 100.0;

            // 6) 시간 진행률 (day progress)
            // x[row,0] : HHmmss (6자리)
            int hhmmss = (int)data.Api.x[row, 0];
            double g = StandardCurve.GetG(hhmmss);

            // 초반 보호 (너무 이른 시간 튐 방지)
            //if (g <= 0.05) return (int)Math.Round(pctRaw);

            // 7) 종가기준 환산 (%)
            double pctEOD = pctRaw / g;

            if (double.IsNaN(pctEOD) || double.IsInfinity(pctEOD))
                return 0;

            return (int)Math.Round(pctEOD);
        }

        // =========================
        // ✅ 표시만 담당: 억원 규칙
        // - 천만원 -> 억원(÷10)
        // - 10억 이상: 반올림 정수
        // - 10억 미만: 소수 1자리, 단 .0 제거 (1.0 -> 1)
        // =========================
        static string FormatEok(double eok)
        {
            double abs = Math.Abs(eok);

            if (abs >= 10.0)
                return Math.Round(eok, 0, MidpointRounding.AwayFromZero).ToString("0");

            string s = eok.ToString("0.0");
            if (s.EndsWith(".0", StringComparison.Ordinal))
                s = s.Substring(0, s.Length - 2);

            if (s == "-0") s = "0";
            return s;
        }

        static string FormatEokFromCheonMan(double cheonMan)
            => FormatEok(cheonMan / 10.0);



        public static string GeneralAnnotationText(
            Chart chart,
            StockData data,
            int[,] x,
            int startRow,
            int lastRow,
            int nrow)
        {
            if (data == null || x == null || nrow <= 0) return string.Empty;

            // ✅ lastRow 범위 고정
            if (lastRow < 0) lastRow = 0;
            if (lastRow >= nrow) lastRow = nrow - 1;

            var api = data.Api;
            if (api == null) return string.Empty;

            string stock = data.Stock;
            string stock_title = "";

            // ---- prefix ----
            if (g.StockManager.HoldingList.Contains(stock)) stock_title = "$$" + stock_title;
            else if (g.StockManager.InterestedWithBidList.Contains(stock)) stock_title = "@ " + stock_title;
            else if (g.StockManager.InterestedOnlyList.Contains(stock)) stock_title = "@ " + stock_title;

            // ✅ sector 처리 제거: title 가공/SECTOR: 파싱 등은 SectorAnnotationText에서만
            stock_title += stock.Length > 4
             ? stock.Substring(0, 4)
             : stock;


            var groupTitle = g.GroupManager.FindGroupByStock(stock);
            if (groupTitle == null) stock_title += "%";

            // ---- 첫째 줄 ----
            // 종가기준 누적거래액 / 프로 / 외인 / 기관 (천만원 -> 억원 표시)
            // passed 기준 0~100 점수 (M, P, F, I 순서 – 라벨 생략)

            double m = data?.Score?.GetPct(ScoreKey.MinMoney_10M) ?? 0;
            double p = data?.Score?.GetPct(ScoreKey.MinPro_10M) ?? 0;
            double f = data?.Score?.GetPct(ScoreKey.MinFor_10M) ?? 0;
            double d = data?.Score?.GetPct(ScoreKey.MinDiff) ?? 0;
            double s = data?.Score?.GetPct(ScoreKey.MinSum) ?? 0;

            // 보기 좋게 폭 3 정렬 (0~100 대응)
            stock_title += $" {d:0}/{s:0} {m:0} {p:0} {f:0}";

            // ---- 둘째 줄 (배수) ----
            stock_title += "\n" + AnnotationMultiple(data, x, startRow, lastRow);

            // ---- (일반 종목) 셋째 줄: 분 구성 ----
            stock_title += "\n";

            int lenP = api.분프로천?.Length ?? 0;
            int lenF = api.분외인천?.Length ?? 0;
            int lenM = api.분거래천?.Length ?? 0;
            int kMax = Math.Min(3, Math.Min(lenP, Math.Min(lenF, lenM))); // 분 / 분전 / 분전전

            if (kMax > 0)
            {
                // === 분 ===
                double p0 = api.분프로천[0];
                double f0 = api.분외인천[0];
                double m0 = api.분거래천[0];

                // 분은 구성 표시: 프로 + 외인 / 거래액
                if (f0 >= 0)
                    stock_title += FormatEokFromCheonMan(p0) + "+" + FormatEokFromCheonMan(f0) + "/" + FormatEokFromCheonMan(m0);
                else
                    stock_title += FormatEokFromCheonMan(p0) + FormatEokFromCheonMan(f0) + "/" + FormatEokFromCheonMan(m0);

                double sum0 = p0 + f0;

                // === 분전 / 분전전 ===
                for (int j = 1; j < kMax; j++)
                {
                    double pi = api.분프로천[j];
                    double fi = api.분외인천[j];
                    double mi = api.분거래천[j];

                    double sumi = pi + fi;

                    // 분과 합이 같으면 합계만 표시
                    if (Math.Abs(sumi - sum0) < 0.0001)
                        stock_title += "   " + FormatEokFromCheonMan(sumi) + "/" + FormatEokFromCheonMan(mi);
                    else
                        stock_title += "   " + FormatEokFromCheonMan(pi) + "+" + FormatEokFromCheonMan(fi) + "/" + FormatEokFromCheonMan(mi);
                }
            }

            // ---- (일반 종목) 넷째 줄: vol 직접 계산 ----
            stock_title += "\n";

            // ✅ sector 분기 제거: General은 항상 20일 평균 거래액 대비 진행률
            int vol = CalcVolumePctInt(data, lastRow);
            stock_title += (vol / 100.0).ToString("0.##");

            // ---- 다섯째 줄: % / 꼬리 ----
            int moneyCum = x[lastRow, 7];

            int proPct = (int)Math.Round(SafeRatioPct(x[lastRow, 4], moneyCum));
            int forPct = (int)Math.Round(SafeRatioPct(x[lastRow, 5], moneyCum));
            int instPct = (int)Math.Round(SafeRatioPct(x[lastRow, 6], moneyCum));

            stock_title += " | " + proPct + " | " + forPct + " | " + instPct;
            stock_title += " (" + x[lastRow, 1] + " | " + api.현재가.ToString("#,##0") + ")";

            // ---- 마지막 꼬리 ----
            stock_title += " " + x[lastRow, 10] + "/" + x[lastRow, 11] + "  " + data.Statistics.일간변동평균편차;

            return stock_title;
        }




        public static string AnnotationMultiple(StockData data, int[,] x, int StartNpts, int checkRow)
        {
            var sb = new StringBuilder();
            string stock = data.Stock;

            // Handle special KODEX types with predefined tick data
            if (stock == "KODEX 레버리지")
            {
                for (int i = 0; i < 5; i++)
                {
                    sb.Append($"{(int)MajorIndex.Instance.KospiTickBuyPower[i]}/{(int)MajorIndex.Instance.KospiTickSellPower[i]}  ");
                }
                sb.AppendLine();
            }
            else if (stock == "KODEX 200선물인버스2X")
            {
                for (int i = 0; i < 5; i++)
                {
                    sb.Append($"{(int)MajorIndex.Instance.KospiTickSellPower[i]}/{(int)MajorIndex.Instance.KospiTickBuyPower[i]}  ");
                }
                sb.AppendLine();
            }
            else if (stock == "KODEX 코스닥150레버리지")
            {
                for (int i = 0; i < 5; i++)
                {
                    sb.Append($"{(int)MajorIndex.Instance.KosdaqTickBuyPower[i]}/{(int)MajorIndex.Instance.KosdaqTickSellPower[i]}  ");
                }
                sb.AppendLine();
            }
            else if (stock == "KODEX 코스닥150선물인버스")
            {
                for (int i = 0; i < 5; i++)
                {
                    sb.Append($"{(int)MajorIndex.Instance.KosdaqTickSellPower[i]}/{(int)MajorIndex.Instance.KosdaqTickBuyPower[i]}  ");
                }
                sb.AppendLine();
            }

            // Add latest 5 minutes of multiplier differences
            for (int i = checkRow; i >= checkRow - 4; i--)
            {
                if (i < 1) break;
                sb.Append($"{x[i, 8] - x[i, 9]}/{x[i, 8] + x[i, 9]} ");
            }

            return sb.ToString();
        }

        public static void UpdateFlowSnapshot(StockData data)
        {
            if (data?.Api?.x == null) return;

            if (!ChartLayoutUtils.TryGetDrawRange(data, out int start, out int end))
                return;
            int idx = end - 1;
            if (idx < 0 || idx >= data.Api.nrow)
                return;

            int timeKey = (int)data.Api.x[idx, 0];
            if (timeKey == 0) return;

            var flow = data.Misc.Flow ?? (data.Misc.Flow = new FlowSnapshot());
            flow.EndIndex = idx;
            flow.TimeKey = timeKey;

            // ✅ Sector는 x에 이미 "배수*100"을 넣고 있으니 그대로 /100 해주면 됨
            // ✅ General은 원하면 여기서 분모로 %계산해서 넣도록 분기
            bool isSector = data.Stock != null && data.Stock.StartsWith("SECTOR:");

            if (isSector)
            {
                flow.PctProgram = data.Api.x[idx, 4];
                flow.PctForeign = data.Api.x[idx, 5];
                flow.PctInstitute = data.Api.x[idx, 6];
                flow.PctTotal = data.Api.x[idx, 7];
                return;
            }

            // ✅ General: 분모가 어딘가에 이미 있다면(예: data.Post.프20평누적 등),
            // 여기서 %를 계산해서 저장 (예시는 구조만)
            // 분모 0/너무작음은 너가 preprocessor에서 제거한다 했으니 안전.
            var post = data.Post;
            if (post != null)
            {
                // 예시: "오늘누적 / 20일평균누적 * 100" -> 최종표시는 1.34 형태면 /100
                // 아래 분모/분자 필드명은 네 실제 필드로 맞춰.


                double avg = data.Statistics.AvgDailyTurnover_10M; // 천만원 단위 (확정)
                double dayProgressRatio = StandardCurve.GetG(data.Api.x[idx, 0]);


                if (MathUtils.IsSafeToDivide(avg)) flow.PctProgram = (post.프누천 / avg / dayProgressRatio * 100);
                if (MathUtils.IsSafeToDivide(avg)) flow.PctForeign = (post.외누천 / avg / dayProgressRatio * 100);
                if (MathUtils.IsSafeToDivide(avg)) flow.PctInstitute = (post.기누천 / avg / dayProgressRatio * 100);
                if (MathUtils.IsSafeToDivide(avg)) flow.PctTotal = (post.종누천 / avg / dayProgressRatio * 100);
            }
        }

    }
}
