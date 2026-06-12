using New_Tradegy.Library;
using New_Tradegy.Library.Models;
using New_Tradegy.Library.PostProcessing;
using New_Tradegy.Library.Trackers;
using New_Tradegy.Library.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace New_Tradegy
{
    public class BoardItem
    {
        public string Name;
        public double Rate;   // 등락
        public double Delta;  // 변동
        public double Turn;   // 누적거래
        public double P;      // 프로
        public double F;      // 외인
        public double I;      // 기관

        public bool IsHeader;
        public bool HideNumbers;
        public bool BoldNameOnly;
        public bool IsSectorSummary;

        public double DisplayScore;   // 추후 색상용, 지금은 0

        public Color? TurnBackColor;
        public Color? PBackColor;
        public Color? FBackColor;
    }

    public class BoardForm : Form
    {
        private static readonly Color[] RankColors =
{
    Color.FromArgb(255, 120, 120), // 1
    Color.FromArgb(255, 140, 140), // 2
    Color.FromArgb(255, 160, 160), // 3
    Color.FromArgb(255, 180, 180), // 4
    Color.FromArgb(255, 200, 200), // 5
    Color.FromArgb(255, 220, 220), // 6
    Color.FromArgb(255, 240, 240), // 7
};

        private readonly Panel panelSector = new Panel();
        private readonly Panel panelStock = new Panel();
        private readonly Panel panelKospi = new Panel();
        private readonly Panel panelKosdaq = new Panel();

        private readonly Font normalFont = new Font("Consolas", 12, FontStyle.Regular);
        private readonly Font boldFont = new Font("Consolas", 12, FontStyle.Bold);

        private List<BoardItem> rowsSector = new List<BoardItem>();
        private List<BoardItem> rowsStock = new List<BoardItem>();
        private List<BoardItem> rowsKospi = new List<BoardItem>();
        private List<BoardItem> rowsKosdaq = new List<BoardItem>();

        // 종목 panel 정렬 전 원본 보관
        private List<BoardItem> rowsStockOriginal = new List<BoardItem>();

        private readonly Timer stockSortResetTimer = new Timer();

        private const int Gap = 10;
        private const int PaddingLeft = 6;
        private const int PaddingTop = 6;
        private const int LineHeight = 22;

        // 이름 칸 확대: 한글 4글자 정상 표시용
        private const int NameW = 92;
        private const int RateW = 52;
        private const int DeltaW = 52;
        private const int TurnW = 46;
        private const int PW = 52;
        private const int FW = 52;
        private const int IW = 52;

        private readonly Dictionary<string, string> shortToFull = new Dictionary<string, string>();
        private readonly Dictionary<string, string> shortNamePairs = new Dictionary<string, string>();
        private bool shortNamePairsBuilt = false;

        private void ClearRankColors(List<BoardItem> rows)
        {
            if (rows == null) return;

            foreach (var r in rows)
            {
                if (r == null) continue;
                r.TurnBackColor = null;
                r.PBackColor = null;
                r.FBackColor = null;
            }
        }

        private void ApplyRankToList(
            List<BoardItem> items,
            Func<BoardItem, double> selector,
            Action<BoardItem, Color?> assignColor)
        {
            var ranked = items
                .OrderByDescending(selector)
                .Take(RankColors.Length)
                .ToList();

            for (int i = 0; i < ranked.Count; i++)
            {
                assignColor(ranked[i], RankColors[i]);
            }
        }

        private List<BoardItem> BuildUniqueStockPool()
        {
            var dict = new Dictionary<string, BoardItem>(StringComparer.OrdinalIgnoreCase);

            AddStockRowsToPool(dict, rowsSector);
            AddStockRowsToPool(dict, rowsStock);
            AddStockRowsToPool(dict, rowsKospi);
            AddStockRowsToPool(dict, rowsKosdaq);

            return dict.Values.ToList();
        }

        private void AddStockRowsToPool(Dictionary<string, BoardItem> dict, List<BoardItem> rows)
        {
            if (rows == null) return;

            foreach (var r in rows)
            {
                if (r == null) continue;
                if (r.IsHeader) continue;
                if (r.IsSectorSummary) continue;
                if (string.IsNullOrWhiteSpace(r.Name)) continue;

                if (!dict.ContainsKey(r.Name))
                    dict[r.Name] = r;
            }
        }

        private Dictionary<string, Color?> BuildRankMap(
            List<BoardItem> items,
            Func<BoardItem, double> selector)
        {
            var map = new Dictionary<string, Color?>(StringComparer.OrdinalIgnoreCase);

            var ranked = items
                .OrderByDescending(selector)
                .Take(RankColors.Length)
                .ToList();

            for (int i = 0; i < ranked.Count; i++)
            {
                map[ranked[i].Name] = RankColors[i];
            }

            return map;
        }

        private void ApplyStockRankMap(
            List<BoardItem> rows,
            Dictionary<string, Color?> turnMap,
            Dictionary<string, Color?> pMap,
            Dictionary<string, Color?> fMap)
        {
            if (rows == null) return;

            foreach (var r in rows)
            {
                if (r == null) continue;
                if (r.IsHeader) continue;
                if (r.IsSectorSummary) continue;
                if (string.IsNullOrWhiteSpace(r.Name)) continue;

                if (turnMap.TryGetValue(r.Name, out var tc))
                    r.TurnBackColor = tc;

                if (pMap.TryGetValue(r.Name, out var pc))
                    r.PBackColor = pc;

                if (fMap.TryGetValue(r.Name, out var fc))
                    r.FBackColor = fc;
            }
        }

        private void ApplyRankColors()
        {
            ClearRankColors(rowsSector);
            ClearRankColors(rowsStock);
            ClearRankColors(rowsKospi);
            ClearRankColors(rowsKosdaq);

            // 1) 섹터행 rank 
            var sectorItems = rowsSector
    .Where(r => r != null && r.IsSectorSummary)
    .ToList();

            ApplyRankToList(sectorItems, x => x.Turn, (x, c) => x.TurnBackColor = c);
            ApplyRankToList(sectorItems, x => x.P, (x, c) => x.PBackColor = c);
            ApplyRankToList(sectorItems, x => x.F, (x, c) => x.FBackColor = c);

            // 2) 종목 pool
            var stockPool = BuildUniqueStockPool();

            var turnMap = BuildRankMap(stockPool, x => x.Turn);
            var pMap = BuildRankMap(stockPool, x => x.P);
            var fMap = BuildRankMap(stockPool, x => x.F);

            // 3) 모든 board에 적용
            ApplyStockRankMap(rowsSector, turnMap, pMap, fMap);
            ApplyStockRankMap(rowsStock, turnMap, pMap, fMap);
            ApplyStockRankMap(rowsKospi, turnMap, pMap, fMap);
            ApplyStockRankMap(rowsKosdaq, turnMap, pMap, fMap);
        }
        public BoardForm()
        {
            Text = "Board";
            StartPosition = FormStartPosition.Manual;
            FormBorderStyle = FormBorderStyle.SizableToolWindow;
            BackColor = Color.White;
            DoubleBuffered = true;

            panelSector.BackColor = Color.White;
            panelStock.BackColor = Color.White;
            panelKospi.BackColor = Color.White;
            panelKosdaq.BackColor = Color.White;

            SetDoubleBuffered(panelSector);
            SetDoubleBuffered(panelStock);
            SetDoubleBuffered(panelKospi);
            SetDoubleBuffered(panelKosdaq);

            Controls.Add(panelSector);
            Controls.Add(panelStock);
            Controls.Add(panelKospi);
            Controls.Add(panelKosdaq);

            panelSector.Paint += (s, e) => DrawPanel(e.Graphics, panelSector, rowsSector);
            panelStock.Paint += (s, e) => DrawPanel(e.Graphics, panelStock, rowsStock);
            panelKospi.Paint += (s, e) => DrawPanel(e.Graphics, panelKospi, rowsKospi);
            panelKosdaq.Paint += (s, e) => DrawPanel(e.Graphics, panelKosdaq, rowsKosdaq);


            //this.MouseClick += BoardForm_MouseClick;

            panelSector.MouseClick += PanelAny_MouseClick;
            panelStock.MouseClick += PanelAny_MouseClick;
            panelKospi.MouseClick += PanelAny_MouseClick;
            panelKosdaq.MouseClick += PanelAny_MouseClick;

            Resize += (s, e) => LayoutPanels();
            LayoutPanels();

            stockSortResetTimer.Interval = 10000;
            stockSortResetTimer.Tick += (s, e) =>
            {
                stockSortResetTimer.Stop();

                if (rowsStockOriginal != null && rowsStockOriginal.Count > 0)
                {
                    rowsStock = rowsStockOriginal.ToList();
                    panelStock.Invalidate();
                }
            };

            BuildShortNamePairs();
        }

        public void RefreshBoard()
        {
            if (!shortNamePairsBuilt)
            {
                BuildShortNamePairs();
                shortNamePairsBuilt = true;
            }

            rowsSector = BuildSectorRows();
            rowsStock = BuildStockRows();
            rowsStockOriginal = rowsStock.ToList();

            rowsKospi = BuildSimpleRows(g.kospi_mixed?.stocks, sortByTurn: false, takeCount: 12, addHeader: false, headerName: "");
            rowsKosdaq = BuildSimpleRows(g.kosdaq_mixed?.stocks, sortByTurn: false, takeCount: 12, addHeader: false, headerName: "");

            ApplyRankColors();   // ← 여기

            panelSector.Invalidate();
            panelStock.Invalidate();
            panelKospi.Invalidate();
            panelKosdaq.Invalidate();
        }

        private void BuildShortNamePairs()
        {
            shortNamePairs.Clear();

            var allNames = g.StockRepo?.AllGeneralStocks
     ?.Select(x => x?.Stock)
     .Where(x => !string.IsNullOrWhiteSpace(x))
     .ToList();

            if (allNames == null) return;

            // 1. 기본 4글자 그룹화
            var groups = allNames
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .GroupBy(name => name.Length <= 4 ? name : name.Substring(0, 4));

            foreach (var grp in groups)
            {
                // 1개면 그대로 사용
                if (grp.Count() == 1)
                {
                    var name = grp.First();
                    string short4 = name.Length <= 4 ? name : name.Substring(0, 4);

                    if (!shortNamePairs.ContainsKey(short4))
                        shortNamePairs[short4] = name;

                    continue;
                }

                // 여러 개면 충돌 해결
                HashSet<string> used = new HashSet<string>();

                foreach (var name in grp)
                {
                    string shortName = MakeUniqueShort(name, used);
                    shortNamePairs[shortName] = name;
                    used.Add(shortName);
                }
            }
        }

        private string MakeUniqueShort(string name, HashSet<string> used)
        {
            if (string.IsNullOrWhiteSpace(name))
                return name;

            if (name.Length <= 4)
                return name;

            string prefix = name.Substring(0, 3);

            // 4번째 글자를 뒤에서 가져와서 교체
            for (int i = 3; i < name.Length; i++)
            {
                string candidate = prefix + name[i];
                if (!used.Contains(candidate))
                    return candidate;
            }

            // 그래도 안 되면 길이 늘림
            for (int len = 5; len <= name.Length; len++)
            {
                string candidate = name.Substring(0, len);
                if (!used.Contains(candidate))
                    return candidate;
            }

            return name;
        }

        public void LayoutPanels()
        {
            int W = ClientSize.Width;
            int H = ClientSize.Height;

            int leftW = (W - Gap * 3) / 2;
            int rightW = leftW;

            // 좌측 전체: 섹터
            panelSector.SetBounds(Gap, Gap, leftW, H - Gap * 2);

            int rightX = Gap * 2 + leftW;
            int rightH = H - Gap * 2;

            // ✔ 코스피 / 코스닥: 10줄 고정
            int fixedRows = 10;
            int kospiH = PaddingTop * 2 + fixedRows * LineHeight;
            int kosdaqH = PaddingTop * 2 + fixedRows * LineHeight;

            // ✔ 종목: 남는 공간
            int stockH = rightH - kospiH - kosdaqH - Gap * 2;

            // ✔ 종목 최소 4줄 보장
            int minStockH = PaddingTop * 2 + 4 * LineHeight;
            if (stockH < minStockH)
                stockH = minStockH;

            panelStock.SetBounds(rightX, Gap, rightW, stockH);
            panelKospi.SetBounds(rightX, Gap + stockH + Gap, rightW, kospiH);
            panelKosdaq.SetBounds(rightX, Gap + stockH + Gap + kospiH + Gap, rightW, kosdaqH);
        }
        //public void RefreshBoard()
        //{
        //    rowsSector = BuildSectorRows();

        //    rowsStock = BuildStockRows();
        //    rowsStockOriginal = rowsStock.ToList();

        //    rowsKospi = BuildSimpleRows(g.kospi_mixed?.stocks, sortByTurn: false, takeCount: 12, addHeader: false, headerName: "");
        //    rowsKosdaq = BuildSimpleRows(g.kosdaq_mixed?.stocks, sortByTurn: false, takeCount: 12, addHeader: false, headerName: "");

        //    panelSector.Invalidate();
        //    panelStock.Invalidate();
        //    panelKospi.Invalidate();
        //    panelKosdaq.Invalidate();
        //}

        private List<BoardItem> BuildSectorRows()
        {
            var rows = new List<BoardItem>();

            // 사진 1 방식: SECTOR: 기반 정렬
            var ordered = g.StockRepo.AllSectorStocks
                .Where(s => s != null &&
                            s.Stock != null &&
                            s.Stock.StartsWith("SECTOR:"))
                .OrderBy(s => s.Score.SectorRank)
                .ToList();

            foreach (var sector in ordered)
            {
                // "SECTOR:반도체" → "반도체"
                string title = sector.Stock.Substring("SECTOR:".Length);

                // title로 grp 찾기
                var grp = g.GroupManager.Groups
                    .FirstOrDefault(gp => gp != null && gp.Title == title);

                if (grp == null) continue;
                if (grp.Stocks == null || grp.Stocks.Count == 0) continue;

                var stocks = grp.Stocks
                    .Where(x => !string.IsNullOrEmpty(x))
                    .Where(x => !IsExcludedStock(x))
                    .Distinct()
                    .ToList();

                // 섹터는 최소 2종목
                if (stocks.Count < 2) continue;

                wk.거분순서(stocks);

                // 섹터 줄 = 첫 종목 데이터 사용, 이름만 그룹명
                var sectorItem = BuildBoardItemFromStock(stocks[0]);
                if (sectorItem == null) continue;

                sectorItem.Name = ShortName(title);   // grp.Title 대신 title 사용
                sectorItem.IsHeader = true;
                sectorItem.IsSectorSummary = true;
                sectorItem.BoldNameOnly = true;
                sectorItem.HideNumbers = false;
                sectorItem.DisplayScore = 0;
                rows.Add(sectorItem);

                // 섹터별 표시 종목 수: 최대 3
                int count = Math.Min(3, stocks.Count);
                for (int i = 0; i < count; i++)
                {
                    var item = BuildBoardItemFromStock(stocks[i]);
                    if (item != null)
                    {
                        item.IsHeader = false;
                        item.BoldNameOnly = false;
                        item.HideNumbers = false;
                        item.DisplayScore = 0;
                        rows.Add(item);
                    }
                }

                // 섹터 사이만 한 줄 띄움
                rows.Add(null);
            }

            while (rows.Count > 0 && rows[rows.Count - 1] == null)
                rows.RemoveAt(rows.Count - 1);

            return rows;
        }

        private List<BoardItem> BuildStockRows()
        {
            var ranking = g.StockManager?.StockRankingList;
            return BuildSimpleRows(ranking, sortByTurn: false, takeCount: 999, addHeader: false, headerName: "");
        }

        private List<BoardItem> BuildSimpleRows(
            List<string> stocks,
            bool sortByTurn,
            int takeCount,
            bool addHeader,
            string headerName)
        {
            var items = new List<BoardItem>();

            if (stocks != null)
            {
                foreach (var stock in stocks)
                {
                    if (string.IsNullOrEmpty(stock)) continue;
                    if (IsExcludedStock(stock)) continue;

                    var item = BuildBoardItemFromStock(stock);
                    if (item != null)
                    {
                        item.DisplayScore = 0;
                        items.Add(item);
                    }
                }
            }

            if (sortByTurn)
            {
                items = items
                    .OrderByDescending(x => x.Turn)
                    .ToList();
            }

            if (takeCount > 0 && items.Count > takeCount)
                items = items.Take(takeCount).ToList();

            var rows = new List<BoardItem>();

            if (addHeader)
            {
                rows.Add(new BoardItem
                {
                    Name = ShortName(headerName),
                    IsHeader = true,
                    HideNumbers = true,
                    BoldNameOnly = true,
                    DisplayScore = 0
                });
            }

            rows.AddRange(items);
            return rows;
        }

        private BoardItem BuildBoardItemFromStock(string stock)
        {
            var data = g.StockRepo.TryGetDataOrNull(stock);
            if (data == null || data.Api == null) return null;

            if (!ChartLayoutUtils.TryGetDrawRange(data, out int start, out int end))
                return null;

            int lastRow = end - 1;
            if (lastRow < 0) return null;

            // 중요: x는 Api.x
            int[,] x = data.Api?.x;
            if (x == null) return null;

            int nrow = x.GetLength(0);
            if (nrow <= 0) return null;
            if (lastRow >= nrow) lastRow = nrow - 1;

            var api = data.Api;

            // 중요: 기존 volume 계산 경로 유지
            int vol = ChartGeneral.CalcVolumePctInt(data, lastRow);
            int moneyCum = x[lastRow, 7];

            double rate = api.가격 / 100.0;
            // 중요: delta는 Post.분30가격차
            double delta = (data.Post?.분30가격차 ?? 0) / 100.0;
            double turn = vol / 100.0;

            double p = Math.Round(SafeRatioPct(x[lastRow, 4], moneyCum), 1);
            double f = Math.Round(SafeRatioPct(x[lastRow, 5], moneyCum), 1);
            double i = Math.Round(SafeRatioPct(x[lastRow, 6], moneyCum), 1);

            return new BoardItem
            {
                Name = ShortName(stock),
                Rate = rate,
                Delta = delta,
                Turn = turn,
                P = p,
                F = f,
                I = i,
                IsHeader = false,
                HideNumbers = false,
                BoldNameOnly = false,
                IsSectorSummary = false,
                DisplayScore = 0,
                TurnBackColor = null,
                PBackColor = null,
                FBackColor = null
            };
        }

        private void DrawPanel(Graphics g, Panel panel, List<BoardItem> rows)
        {
            g.Clear(Color.White);
            if (rows == null || rows.Count == 0) return;

            int y = PaddingTop;
            int maxRows = Math.Max(1, (panel.Height - PaddingTop * 2) / LineHeight);

            int drawn = 0;
            foreach (var row in rows)
            {
                if (drawn >= maxRows) break;

                if (row == null)
                {
                    y += LineHeight;
                    drawn++;
                    continue;
                }

                DrawRow(g, row, y, panel.Width);

                y += LineHeight;
                drawn++;
            }
        }





        private void FillCellBack(Graphics g, Color? color, int x, int y, int width)
        {
            if (!color.HasValue) return;

            using (var b = new SolidBrush(color.Value))
            {
                g.FillRectangle(b, x, y, width, LineHeight);
            }
        }
        private void DrawRow(Graphics g, BoardItem row, int y, int panelWidth)
        {
            Color backColor = GetRowBackColor(row.DisplayScore);
            using (var backBrush = new SolidBrush(backColor))
            {
                g.FillRectangle(backBrush, 0, y, panelWidth, LineHeight);
            }

            int x = PaddingLeft;

            int xName = x; x += NameW;
            int xRate = x; x += RateW;
            int xDelta = x; x += DeltaW;
            int xTurn = x; x += TurnW;
            int xP = x; x += PW;
            int xF = x; x += FW;
            int xI = x;

            var textBrush = Brushes.Black;

            if (!row.HideNumbers)
            {
                FillCellBack(g, row.TurnBackColor, xTurn, y, TurnW);
                FillCellBack(g, row.PBackColor, xP, y, PW);
                FillCellBack(g, row.FBackColor, xF, y, FW);
            }

            // 이름만 Bold 가능
            Font nameFont = row.BoldNameOnly ? boldFont : normalFont;
            DrawText(g, row.Name, xName, y, NameW, nameFont, textBrush, rightAlign: false);

            if (row.HideNumbers) return;

            // 숫자는 항상 normalFont
            DrawText(g, FormatSigned5(row.Rate), xRate, y, RateW, normalFont, textBrush, rightAlign: true);
            DrawText(g, FormatSigned5(row.Delta), xDelta, y, DeltaW, normalFont, textBrush, rightAlign: true);
            DrawText(g, FormatUnsigned4(row.Turn), xTurn, y, TurnW, normalFont, textBrush, rightAlign: true);
            DrawText(g, FormatSigned5Pct(row.P), xP, y, PW, normalFont, textBrush, rightAlign: true);
            DrawText(g, FormatSigned5Pct(row.F), xF, y, FW, normalFont, textBrush, rightAlign: true);
            DrawText(g, FormatSigned5Pct(row.I), xI, y, IW, normalFont, textBrush, rightAlign: true);
        }

        private void DrawText(Graphics g, string text, int x, int y, int width, Font font, Brush brush, bool rightAlign)
        {
            var rect = new RectangleF(x, y, width, LineHeight);
            using (var sf = new StringFormat())
            {
                sf.Alignment = rightAlign ? StringAlignment.Far : StringAlignment.Near;
                sf.LineAlignment = StringAlignment.Near;
                sf.FormatFlags = StringFormatFlags.NoWrap;
                g.DrawString(text, font, brush, rect, sf);
            }
        }

        private void HandleStockSortClick(int mouseX, int mouseY)
        {
            if (rowsStockOriginal == null || rowsStockOriginal.Count == 0) return;

            // 첫 줄 숫자 영역 클릭만 허용
            if (mouseY < PaddingTop || mouseY >= PaddingTop + LineHeight) return;

            string key = HitTestNumericColumn(mouseX);
            if (string.IsNullOrEmpty(key)) return;

            List<BoardItem> sorted;
            switch (key)
            {
                case "Rate":
                    sorted = rowsStockOriginal.OrderByDescending(x => x.Rate).ToList();
                    break;
                case "Delta":
                    sorted = rowsStockOriginal.OrderByDescending(x => x.Delta).ToList();
                    break;
                case "Turn":
                    sorted = rowsStockOriginal.OrderByDescending(x => x.Turn).ToList();
                    break;
                case "P":
                    sorted = rowsStockOriginal.OrderByDescending(x => x.P).ToList();
                    break;
                case "F":
                    sorted = rowsStockOriginal.OrderByDescending(x => x.F).ToList();
                    break;
                case "I":
                    sorted = rowsStockOriginal.OrderByDescending(x => x.I).ToList();
                    break;
                default:
                    return;
            }

            rowsStock = sorted;
            panelStock.Invalidate();

            stockSortResetTimer.Stop();
            stockSortResetTimer.Interval = 5000;   // 5초
            stockSortResetTimer.Start();
        }

        // 종목 panel 첫 줄 숫자 클릭 -> rankinglist 내 종목만 정렬, 10초 후 원복
        private void PanelStock_MouseClick(object sender, MouseEventArgs e)
        {
            if (rowsStockOriginal == null || rowsStockOriginal.Count == 0) return;

            // 첫 줄 숫자 영역 클릭만 허용
            if (e.Y < PaddingTop || e.Y >= PaddingTop + LineHeight) return;

            string key = HitTestNumericColumn(e.X);
            if (string.IsNullOrEmpty(key)) return;

            List<BoardItem> sorted;
            switch (key)
            {
                case "Rate":
                    sorted = rowsStockOriginal.OrderByDescending(x => x.Rate).ToList();
                    break;
                case "Delta":
                    sorted = rowsStockOriginal.OrderByDescending(x => x.Delta).ToList();
                    break;
                case "Turn":
                    sorted = rowsStockOriginal.OrderByDescending(x => x.Turn).ToList();
                    break;
                case "P":
                    sorted = rowsStockOriginal.OrderByDescending(x => x.P).ToList();
                    break;
                case "F":
                    sorted = rowsStockOriginal.OrderByDescending(x => x.F).ToList();
                    break;
                case "I":
                    sorted = rowsStockOriginal.OrderByDescending(x => x.I).ToList();
                    break;
                default:
                    return;
            }

            rowsStock = sorted;
            panelStock.Invalidate();

            stockSortResetTimer.Stop();
            stockSortResetTimer.Start();
        }

        private void PanelAny_MouseClick(object sender, MouseEventArgs e)
        {
            // 클릭 처리 후 메인 차트로 포커스 복귀
            g.ChartManager.Chart1.FindForm()?.Activate();
            g.ChartManager.Chart1.Focus();

            var panel = sender as Panel;
            if (panel == null) return;

            var rows = GetRowsByPanel(panel);
            if (rows == null || rows.Count == 0) return;

            int rowIndex = GetRowIndexFromY(e.Y, rows, panel.Height);
            if (rowIndex < 0 || rowIndex >= rows.Count) return;

            var row = rows[rowIndex];
            if (row == null) return;   // 섹터 사이 공백줄

            // 1) 이름 칸 클릭 → 이름 처리
            if (e.X >= PaddingLeft && e.X < PaddingLeft + NameW)
            {
                HandleBoardNameClick(row.Name);

            }

            // 2) 숫자 칸 클릭 → 종목 panel만 정렬
            if (panel == panelStock)
            {
                HandleStockSortClick(e.X, e.Y);

            }


        }

        private List<BoardItem> GetRowsByPanel(Panel panel)
        {
            if (panel == panelSector) return rowsSector;
            if (panel == panelStock) return rowsStock;
            if (panel == panelKospi) return rowsKospi;
            if (panel == panelKosdaq) return rowsKosdaq;
            return null;
        }

        private int GetRowIndexFromY(int y, List<BoardItem> rows, int panelHeight)
        {
            int currentY = PaddingTop;
            int maxRows = Math.Max(1, (panelHeight - PaddingTop * 2) / LineHeight);

            int drawn = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                if (drawn >= maxRows) break;

                int rowH = LineHeight;
                if (rows[i] != null && rows[i].BoldNameOnly)
                    rowH = LineHeight;   // 지금 친구가 넣은 섹터 헤더 아래 간격 반영

                if (y >= currentY && y < currentY + rowH)
                    return i;

                currentY += rowH;
                drawn++;
            }

            return -1;
        }

        private void HandleBoardNameClick(string shortName)
        {
            if (string.IsNullOrEmpty(shortName)) return;

            var grp = g.GroupManager?.Groups?
                .FirstOrDefault(x => x != null && ShortName(x.Title) == shortName);

            if (grp != null)
            {
                // 기존 사용 중 종목 제외 리스트
                var existing = new List<string>();
                existing.AddRange(g.StockManager.HoldingList);
                existing.AddRange(g.StockManager.InterestedWithBidList);
                existing.AddRange(g.StockManager.IndexList);

                // 1️⃣ 그룹 종목 먼저 가져오기
                var stocks = g.GroupManager.GetStocksByTitle(grp.Title, existing);

                // 2️⃣ 이미 선택된 상태인지 체크
                bool alreadySelected = stocks.Any(s =>
                    g.StockManager.InterestedOnlyList.Contains(s));

                // 3️⃣ 이미 선택된 상태면 → 전체 해제
                if (alreadySelected)
                {
                    g.StockManager.InterestedOnlyList.Clear();
                    PostProcessor.ManageChart1Invoke();
                    return;
                }

                // 4️⃣ 아니면 새로 채우기
                g.StockManager.InterestedOnlyList.Clear();

                foreach (var stock in stocks)
                {
                    if (!g.StockManager.InterestedOnlyList.Contains(stock))
                        g.StockManager.InterestedOnlyList.Add(stock);
                }

                PostProcessor.ManageChart1Invoke();
                return;
            }

            string resolvedName = ResolveBoardStockName(shortName);
            if (string.IsNullOrEmpty(resolvedName))
                return;

            var stockData = g.StockRepo.TryGetDataOrNull(resolvedName);
            if (stockData != null)
            {
                if (!g.StockManager.InterestedWithBidList.Contains(resolvedName))
                    g.StockManager.InterestedWithBidList.Add(resolvedName);
                else
                    g.StockManager.InterestedWithBidList.Remove(resolvedName);

                PostProcessor.ManageChart1Invoke();
            }
        }
        private string ResolveBoardStockName(string shortName)
        {
            if (string.IsNullOrWhiteSpace(shortName))
                return null;

            if (shortNamePairs.TryGetValue(shortName, out var fullName))
                return fullName;

            var allNames = g.StockRepo?.AllGeneralStocks
    ?.Select(x => x?.Stock)
    .Where(x => !string.IsNullOrWhiteSpace(x))
    .ToList();

            if (allNames == null) return null;

            var found = allNames.FirstOrDefault(x => x != null && x.StartsWith(shortName, StringComparison.Ordinal));
            return found;
        }

        private string HitTestNumericColumn(int mouseX)
        {
            int x = PaddingLeft;
            x += NameW;

            int xRate = x; x += RateW;
            int xDelta = x; x += DeltaW;
            int xTurn = x; x += TurnW;
            int xP = x; x += PW;
            int xF = x; x += FW;
            int xI = x; x += IW;

            if (mouseX >= xRate && mouseX < xRate + RateW) return "Rate";
            if (mouseX >= xDelta && mouseX < xDelta + DeltaW) return "Delta";
            if (mouseX >= xTurn && mouseX < xTurn + TurnW) return "Turn";
            if (mouseX >= xP && mouseX < xP + PW) return "P";
            if (mouseX >= xF && mouseX < xF + FW) return "F";
            if (mouseX >= xI && mouseX < xI + IW) return "I";

            return null;
        }

        private static string FormatSigned5(double v)
        {
            return v.ToString("0.0").PadLeft(5);
        }

        private static string FormatUnsigned4(double v)
        {
            return v.ToString("0.0").PadLeft(4);
        }

        private static string FormatSigned5Pct(double v)
        {
            return v.ToString("0.0").PadLeft(5);
        }

        private static string ShortName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            return name.Length <= 4 ? name : name.Substring(0, 4);
        }

        private static bool IsExcludedStock(string name)
        {
            if (string.IsNullOrEmpty(name)) return true;

            if (name.EndsWith("우")) return true;
            if (name.EndsWith("우B")) return true;

            return false;
        }

        private static double SafeRatioPct(double value, double total)
        {
            if (Math.Abs(total) < 0.000001) return 0;
            return value * 100.0 / total;
        }

        private static Color GetRowBackColor(double score)
        {
            // 현재는 전부 0
            return Color.White;
        }

        private static void SetDoubleBuffered(Control c)
        {
            var pi = typeof(Control).GetProperty("DoubleBuffered",
                BindingFlags.NonPublic | BindingFlags.Instance);
            pi?.SetValue(c, true, null);
        }


    }
}