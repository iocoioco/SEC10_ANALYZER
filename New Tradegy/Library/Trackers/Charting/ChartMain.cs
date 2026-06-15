using New_Tradegy.Library.IO;
using New_Tradegy.Library.Models;
using New_Tradegy.Library.PostProcessing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Messaging;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace New_Tradegy.Library.Trackers.Charting
{
    


    public class ChartMain
    {
        private Chart chart => g.ChartManager.Chart1;

        public List<string> DisplayList { get; private set; }

        private static readonly Stopwatch _chartAreaStopwatch = Stopwatch.StartNew();
        private static readonly TimeSpan _rebuildInterval = TimeSpan.FromMinutes(1);

        public void RefreshMainChart()
        {
            // Reference to the chart

            chart.SuspendLayout(); /*chart.Visible = false;*/

            // Constants and major collections (defined clearly at the top)
            const int MaxChartSlots = 20;

            var leverageList = g.StockManager.LeverageList;
            var holdings = g.StockManager.HoldingList;
            var interestedWithBid = g.StockManager.InterestedWithBidList;
            var interestedOnly = g.StockManager.InterestedOnlyList;
            var excluded = g.StockManager.IndexList;

            // Step 1: Create withBookBid list (holdings + interestedWithBid - exclude index stocks)
            var withBookBid = holdings
                .Concat(interestedWithBid)
                .Distinct()
               .Where(stock => !excluded.Contains(stock))
                .Take(MaxChartSlots / 2)
                .ToList();

            // Step 2: Calculate remaining slots and build withoutBookBid from InterestedOnlyList + Ranking
            int usedSlots = withBookBid.Count * 2;
            int remainingSlots = MaxChartSlots - usedSlots;

            var withoutBookBid = interestedOnly
                .Concat(g.StockManager.StockRankingList.Skip(g.gid))
                .Where(s => !withBookBid.Contains(s) && !excluded.Contains(s))
                .Distinct()
                .Take(remainingSlots)
                .ToList();

            // Step 3: Combine into displayList (index first, then with/without bid)
            var displayList = new List<string>();
            displayList.AddRange(leverageList);      // Fixed index stocks
            displayList.AddRange(withBookBid);    // Main with bid
            displayList.AddRange(withoutBookBid); // Main without bid

            // Step 4: Initialize used tracking lists
            var usedChartAreas = new List<string>();
            var usedAnnotations = new List<string>();
            var usedBookbids = new List<string>();


            int areasCount = g.ChartManager.Chart1.ChartAreas.Count;
            int annotationsCount = g.ChartManager.Chart1.Annotations.Count;
            int seriesCount = g.ChartManager.Chart1.Series.Count;

            // every minute, rebuild chart areas
            if (_chartAreaStopwatch.Elapsed >= _rebuildInterval)
            {
                foreach (string stock in displayList)
                {
                    if (string.IsNullOrEmpty(stock))
                        continue;

                    var data = g.StockRepo.TryGetDataOrNull(stock);
                    if (data != null)
                        data.Misc.CreateNewChartArea = true;
                }

                _chartAreaStopwatch.Restart(); // reset the stopwatch
            }

            // Step 5: Render each chart area and prepare book bids
            foreach (var stock in displayList)
            {
                var data = g.StockRepo.TryGetDataOrNull(stock);
                if (data == null) continue;

                bool isIndex = leverageList.Contains(stock);

                if (isIndex)
                {
                    var (area, anno) = ChartIndex.UpdateChartArea(chart, data);
                    if (area == null) continue;
                    usedChartAreas.Add(area.Name);
                    //usedAnnotations.Add(anno.Name);
                }
                else
                {
                    var (area, anno) = ChartGeneral.UpdateChartArea(chart, data);
                    if (area == null || anno == null) continue;
                    usedChartAreas.Add(area.Name);
                    usedAnnotations.Add(anno.Name);
                }

                // BookBid 추가: index 또는 withBookBid 리스트에 있으면
                if (isIndex || withBookBid.Contains(stock))
                {
                    int row = -1, col = -1;

                    // leverageList 인덱스 접근 전 안전 확인
                    if (isIndex && leverageList.Count >= 2)
                    {
                        if (stock == leverageList[0]) { row = 0; col = 0; }
                        else if (stock == leverageList[1]) { row = 0; col = 2; }
                    }
                    else
                    {
                        // withBookBid 안에 실제로 있을 때만 IndexOf
                        int idx = withBookBid.IndexOf(stock); // Contains를 통과했으므로 -1 아님
                        if (idx >= 0)
                        {
                            row = idx / 3;
                            col = (idx % 3) + 2;
                        }
                    }

                    if (row < 0) continue;   // 위치 계산 실패 시 스킵

                    g.BookBidManager.GetOrCreate(stock);
                    usedBookbids.Add(stock);
                    // TODO: row/col을 실제 배치에 사용한다면 여기서 적용,
                    // 현재는 저장만 하므로 의도대로면 OK.
                }
            }

            //            RelocateChartAreasAndAnnotations(
            //    g.StockManager.LeverageList,   // ✅ 순서 보장
            //    withBookBid,
            //    withoutBookBid
            //);
            var leverageStocks =
                g.StockManager.LeverageList
                  .Where(s => g.StockRepo.TryGetDataOrNull(s) != null) // 존재하는 것만
                  .ToList();

            RelocateChartAreasAndAnnotations(leverageStocks, withBookBid, withoutBookBid);

            RelocateBookbids(usedBookbids); // done



            CleanupUnusedChartObjects(usedChartAreas, usedAnnotations, usedBookbids); // done

            //DisplayList = GenerateDisplayList(leverageList, withBookBid, withoutBookBid, g.nRow, g.nCol);

            chart.ResumeLayout(); /*chart.Visible = true;*/
            chart.Invalidate();

            seriesCount = chart.Series.Count;
            int chartAreaCount = chart.ChartAreas.Count;
            int annotationCount = chart.Annotations.Count;

            if (!g.test && g.MarketeyeCount > 0)
                CheckChartTimeLag(displayList);
        }
        private void CheckChartTimeLag(List<string> displayList)
        {
            int nowHHmm = DateTime.Now.Hour * 100 + DateTime.Now.Minute;

            foreach (var stock in displayList)
            {
                string s1 = stock + " 1";
                if (chart.Series.IndexOf(s1) < 0) continue;
                if (chart.ChartAreas.IndexOf(stock) < 0) continue;

                var series = chart.Series[s1];
                var area = chart.ChartAreas[stock];

                int chartHHmm = 0;
                if (series.Points.Count > 0)
                    int.TryParse(series.Points[series.Points.Count - 1].AxisLabel, out chartHHmm);

                bool lag = chartHHmm > 0 && chartHHmm < nowHHmm;

                area.BackColor = lag
                    ? Color.FromArgb(80, Color.Yellow)
                    : Color.White;
            }
        }

        private void CleanupUnusedChartObjects(
            List<string> keepAreas,
            List<string> keepAnnotations,
            List<string> keepBookbids)
        {
            var chart = g.ChartManager.Chart1; // 사용 중인 차트 참조

            // 빠른 포함 검사용 집합
            var keepAreaSet = new HashSet<string>(keepAreas ?? Enumerable.Empty<string>());
            var keepAnnotationSet = new HashSet<string>(keepAnnotations ?? Enumerable.Empty<string>());
            var keepBookbidSet = new HashSet<string>(keepBookbids ?? Enumerable.Empty<string>());

            // 예약/고정 이름들
//            var reservedAreaNames = new HashSet<string>
//{
//    "Main Info",
//    "Sub Info",
//    "Group Pane",

//    "ETF_NQ_KOSPI",
//    "ETF_NQ_KOSDAQ"
//};

            // 예약 영역은 무조건 유지 대상으로 포함
            //foreach (var name in reservedAreaNames)
            //    keepAreaSet.Add(name);

            //// 예약 annotation도 쓴다면 같이 유지
            //foreach (var name in reservedAreaNames)
            //    keepAnnotationSet.Add(name);

            // 1) ChartAreas 정리
            foreach (var area in chart.ChartAreas.ToList())
            {
                if (area == null || string.IsNullOrEmpty(area.Name)) continue;

                if (!keepAreaSet.Contains(area.Name))
                {
                    chart.ChartAreas.Remove(area);
                }
            }

            // 2) Annotations 정리
            //    - keepAnnotations에 없거나
            //    - 대응 ChartArea가 이미 제거된 경우 둘 다 삭제
            // 2) Annotations 정리
            //    - keepAnnotations에 없으면 삭제
            //    - ClipToChartArea가 지정된 경우에만 대응 ChartArea 존재 여부 검사
            foreach (var anno in chart.Annotations.ToList())
            {
                if (anno == null || string.IsNullOrEmpty(anno.Name)) continue;

                if (!keepAnnotationSet.Contains(anno.Name))
                {
                    chart.Annotations.Remove(anno);
                }
            }

            // 3) Series 정리
            //    종목별 유지해야 할 시리즈 이름 집합 구성
            int[] generalIds = { 1, 2, 3, 4, 5, 6 }; // p, a, i, p, f, i
            int[] indexIds = { 1, 3, 4, 5, 6, 10, 11 };

            var keepSeriesSet = new HashSet<string>();

            foreach (string areaName in keepAreaSet)
            {
                if (string.IsNullOrWhiteSpace(areaName)) continue;

                // 예약 영역은 종목 차트가 아니므로 series 유지 대상 생성 안 함
                //if (reservedAreaNames.Contains(areaName))
                //    continue;

                bool isIndex = (areaName == "KODEX 레버리지" || areaName == "KODEX 코스닥150레버리지");
                var ids = isIndex ? indexIds : generalIds;

                foreach (int id in ids)
                    keepSeriesSet.Add($"{areaName} {id}");
            }

            foreach (var series in chart.Series.ToList())
            {
                if (series == null || string.IsNullOrEmpty(series.Name)) continue;

                // 대응 ChartArea가 사라졌거나, 유지 목록에 없으면 제거
                var areaName = series.ChartArea;
                bool areaExists = !string.IsNullOrEmpty(areaName) && chart.ChartAreas.IndexOf(areaName) >= 0;

                if (!areaExists || !keepSeriesSet.Contains(series.Name))
                {
                    chart.Series.Remove(series);
                }
            }

            // 4) BookBid 정리
            g.BookBidManager.CleanupAllExcept(keepBookbidSet.ToList());
        }


        private void RelocateChartAreasAndAnnotations(
     List<string> leverageStocks,
     List<string> withBookBid,
     List<string> withoutBookBid)
        {
            var chart = g.ChartManager.Chart1;
            if (chart == null) return;

            int nCol = g.nCol;
            int nRow = g.nRow;
            float cellW = 100f / nCol;
            float cellH = 100f / nRow;




            int total = 2 + (nCol - 2) * nRow;   // col0 leverage 2칸 + col2.. 배치
            var displayList = new List<string>(new string[total]);

            int ToLinear(int c, int r) => (c - 2) * nRow + r + 2;   // c >= 2

            bool IsInside(int c, int r)
            {
                if (c < 2 || c >= nCol) return false;
                if (r < 0 || r >= nRow) return false;
                int k = ToLinear(c, r);
                return k >= 2 && k < total;
            }




            bool IsEmptyCell(int c, int r)
            {
                if (!IsInside(c, r)) return false;
                int k = ToLinear(c, r);
                return string.IsNullOrEmpty(displayList[k]);
            }

            void ReserveCell(int c, int r, string name)
            {
                if (!IsInside(c, r)) return;
                int k = ToLinear(c, r);
                displayList[k] = name;
            }

            void PlaceAt(string stock, int c, int r)
            {
                if (string.IsNullOrEmpty(stock)) return;
                if (!IsInside(c, r)) return;
                if (chart.ChartAreas.IndexOf(stock) < 0) return;

                float x = c * cellW;
                float y = r * cellH;

                var area = chart.ChartAreas[stock];
                area.Position = new ElementPosition(x, y, cellW, cellH);
                if (!area.Visible) area.Visible = true;

                var anno = chart.Annotations.FirstOrDefault(a => a.Name == stock);
                if (anno is RectangleAnnotation rect)
                {
                    rect.X = x;
                    rect.Y = y;
                }
                if (anno != null && !anno.Visible)
                    anno.Visible = true;

                displayList[ToLinear(c, r)] = stock;
            }

            // ============================================================
            // 1) 좌측 leverage 2칸 고정 (col0)
            // ============================================================
            for (int i = 0; i < leverageStocks.Count && i < 2; i++)
            {
                string s = leverageStocks[i];
                if (string.IsNullOrEmpty(s)) continue;
                if (chart.ChartAreas.IndexOf(s) < 0) continue;

                float x = 0f;
                float y = i * 50f;

                var area = chart.ChartAreas[s];
                area.Position = new ElementPosition(x, y, cellW * 2, 50f);
                if (!area.Visible) area.Visible = true;

                var anno = chart.Annotations.FirstOrDefault(a => a.Name == s);
                if (anno is RectangleAnnotation rect)
                {
                    rect.X = x + 0.5f;
                    rect.Y = y;
                }
                if (anno != null && !anno.Visible)
                    anno.Visible = true;

                displayList[i] = s;   // 0,1
            }

            // ============================================================
            // 2) 예약칸
            //    (2,0) = KP BookBid
            //    (2,1) = Control / Trade Pane
            //    (2,2) = KQ BookBid
            //    (3,1) = Trade Plan
            // ============================================================
            ReserveCell(2, 0, "KP BookBid");
            ReserveCell(2, 1, "Control Trade Pane");
            ReserveCell(2, 2, "KQ BookBid");
            ReserveCell(3, 1, "Trade Plan");

            // ============================================================
            // 3) withBookBid 먼저
            //    [차트][호가창] 2칸 블록
            // ============================================================
            foreach (var s in withBookBid)
            {
                if (string.IsNullOrEmpty(s)) continue;
                if (chart.ChartAreas.IndexOf(s) < 0) continue;

                bool placed = false;

                for (int c = 3; c <= nCol - 2 && !placed; c++)   // c+1 필요
                {
                    for (int r = 0; r < nRow; r++)
                    {
                        // Trade Plan 자리 비움
                        if (c == 3 && r == 1) continue;

                        if (!IsEmptyCell(c, r)) continue;
                        if (!IsEmptyCell(c + 1, r)) continue;

                        PlaceAt(s, c, r);
                        ReserveCell(c + 1, r, "BookBid");
                        placed = true;
                        break;
                    }
                }
            }

            // ============================================================
            // 4) withoutBookBid
            // ============================================================
            foreach (var s in withoutBookBid)
            {
                if (string.IsNullOrEmpty(s)) continue;
                if (chart.ChartAreas.IndexOf(s) < 0) continue;

                bool placed = false;

                for (int c = 3; c < nCol && !placed; c++)
                {
                    for (int r = 0; r < nRow; r++)
                    {
                        if (c == 3 && r == 1) continue; // Trade Plan

                        if (!IsEmptyCell(c, r)) continue;

                        PlaceAt(s, c, r);
                        placed = true;
                        break;
                    }
                }
            }

            g.ChartMain.DisplayList = displayList;
            chart.Invalidate();
        }




        private void RelocateBookbids(List<string> stockList)
        {
            if (stockList == null || stockList.Count == 0)
                return;

            var reservedNames = new HashSet<string>
    {
        "KP BookBid",
        "Control Trade Pane",
        "KQ BookBid",
        "Trade Plan",
        "BookBid"
    };

            foreach (var stock in stockList)
            {
                if (string.IsNullOrWhiteSpace(stock))
                    continue;

                if (reservedNames.Contains(stock))
                    continue;

                // 👉 그냥 배치 (오른쪽 칸이 화면 밖이면 자동으로 안 보임)
                g.BookBidManager.Relocate(stock);
            }
        }

        public void DisplayDatewiseStockHistory(string stock, int offset)
        {
            g.ChartManager.Chart1Handler?.Clear();                 // ✅ Clear Chart1 only
            g.BookBidManager.CleanupAllExcept(Enumerable.Empty<string>());         // ✅ Clear all bookbids

            var chart = g.ChartManager.Chart1;

            var folders = Directory.GetDirectories(@"C:\BJS\분\")
                .Select(Path.GetFileName)
                .Where(name => int.TryParse(name, out _))
                .OrderByDescending(name => name)
                .ToList();

            int startIndex = offset * 24;
            var selectedDates = folders.Skip(startIndex).Take(24).ToList();


            int row = 0, col = 0;

            foreach (var date in selectedDates)
            {
                string file = $@"C:\BJS\분\{date}\{stock}.txt";
                if (!File.Exists(file)) continue;

                var sd = new StockData();
                sd.Stock = g.clickedStock;
                FileInStockData.LoadStockData(sd, file);  // ✅ now correct
                if (sd.Api.nrow < 2)
                {
                    row++;
                    if (row >= 3)
                    {
                        row = 0;
                        col++;
                    }
                    continue;
                }

                string MMdd = date.Length >= 8 ? date.Substring(4, 4) : stock;
                sd.Stock = MMdd;

                // add/update
                g.StockRepo.AddOrUpdate(MMdd, sd);

                PostProcessor.post(sd);

                var (area, anno) = ChartGeneral.UpdateChartArea(chart, sd);

                // === 🧭 Relocate immediately to col 2–9, row 0–2 ===
                float cellWidth = 100f / g.nCol;
                float cellHeight = 100f / g.nRow;
                float margin = 0.0f;

                int absCol = 2 + col;
                float x = absCol * cellWidth + margin;
                float y = row * cellHeight;
                float width = cellWidth - 2 * margin;

                if (area != null)
                {
                    area.Position = new ElementPosition(x, y, width, cellHeight);
                    area.Visible = true;
                }

                if (anno is RectangleAnnotation rect)
                {
                    rect.X = x;
                    rect.Y = y;
                    rect.BackColor = Color.White;
                    rect.Visible = true;
                }

                // === Move down and to the right ===
                row++;
                if (row >= 3)
                {
                    row = 0;
                    col++;
                }

                g.StockRepo.RemoveStock(MMdd);
            }

            chart.Invalidate();  // ✅ refresh the chart after drawing
        }
    }
}
