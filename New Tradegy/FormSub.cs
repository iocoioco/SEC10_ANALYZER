using New_Tradegy.Library;
using New_Tradegy.Library.Core;
using New_Tradegy.Library.IO;
using New_Tradegy.Library.Trackers;
using New_Tradegy.Library.UI.ChartClickHandlers;
using New_Tradegy.Library.UI.KeyBindings;
using New_Tradegy.Library.Utils;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using New_Tradegy.Library.UI;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using static System.Net.Mime.MediaTypeNames;
namespace New_Tradegy
{
    public partial class FormSub : Form
    {
        private int dataGridView1Height = 25;
        public int nRow;
        public int nCol;

        public static List<string> displayList = new List<string>();
        private DataTable dtb;

        private int _maxSpace = 15;

        private Point _location = new Point();

        private Size _size = new Size();
        private Chart chart => g.ChartManager.Chart2;

        private Form1 _mainForm;

        private static readonly Stopwatch _chartAreaStopwatch = Stopwatch.StartNew();
        private static readonly TimeSpan _rebuildInterval = TimeSpan.FromMinutes(1);

        public void SetMainForm(Form1 mainForm)
        {
            _mainForm = mainForm;
        }

        private void FormSubShow(object sender, EventArgs e)
        {
            this.Name = "FormSub";
            g.chart2 = chart2;

            int dataGridView1Height = 25; // Your actual value

            dataGridView1.Dock = DockStyle.Top;
            dataGridView1.Height = dataGridView1Height;

            chart2.Dock = DockStyle.Fill;
            chart2.Margin = new Padding(0);

            this.Padding = new Padding(0);
            this.AutoScroll = false;



            // 🔥 This forces WinForms to recalculate layout
            this.PerformLayout();
        }

        public FormSub()
        {
            InitializeComponent();
            this.Shown += FormSubShow;
        }

        private void FormSub_Load(object sender, EventArgs e)
        {
            g.ChartManager.SetChart2(chart2);

            // Configure DataGridView appearance
            ConfigureDataGridView();
            ConfigureChartAndGridSize();

            // Initialize DataTable
            InitializeDataTable();

            FormSubDraw();

            // defer the call until after FormSub_Load completes
            //this.BeginInvoke((Action)(() =>
            //{
            //    if (g.groupPane?.View != null)
            //        g.groupPane.BindGrid(g.groupPane.View);
            //}));
        }

        private void ConfigureChartAndGridSize()
        {
            // 1) 사용할 모니터 선택: 보조 모니터(Primary 아닌 것), 없으면 Primary
            Screen targetScreen;
            var screens = Screen.AllScreens;

            if (screens.Length >= 2)
            {
                // Primary가 아닌 첫 번째 모니터 = 보조 모니터
                targetScreen = screens.First(s => !s.Primary);
            }
            else
            {
                targetScreen = Screen.PrimaryScreen;
            }

            Rectangle work = targetScreen.WorkingArea;

    
            this.StartPosition = FormStartPosition.Manual;
            this.Size = new Size(work.Width / 2 + 10, work.Height + 10);
            this.Location = new Point(work.X + work.Width / 2, work.Y);



            //// 2) 폼 크기 설정 (지금처럼 절반만 쓰고 싶으면 그대로)
            //this.Size = work.Size;
            //this.Width = this.Width / 2 + 10;
            //this.Height = this.Height + 10;

            //// 3) 위치 설정: 보조 모니터의 좌상단 기준
            //this.StartPosition = FormStartPosition.Manual;
            //this.Location = new Point(work.X / 2, work.Y); // 보조 모니터 맨 왼쪽 위

            // 필요하면 가운데 정렬로 배치할 수도 있음:
            // this.Location = new Point(
            //     work.X + (work.Width  - this.Width)  / 2,
            //     work.Y + (work.Height - this.Height) / 2);
        }

        private void ConfigureDataGridView()
        {
            dataGridView1.DataError += (s, f) => FileOut.DataGridView_DataError(s, f, "보조차트 dgv");
            dataGridView1.DefaultCellStyle.Font = new Font("Arial Bold", 9, FontStyle.Bold);
            dataGridView1.ColumnHeadersDefaultCellStyle.Font = new Font("Arial Bold", 9, FontStyle.Bold);
            dataGridView1.RowTemplate.Height = g.cellHeight;
            dataGridView1.ForeColor = Color.Black;
            dataGridView1.ScrollBars = ScrollBars.None;
        }

        private void InitializeDataTable()
        {
            dtb = new DataTable();
            dtb.Columns.AddRange(new DataColumn[]
            {
                new DataColumn("상관"), new DataColumn("보유"), new DataColumn("누순"),
                new DataColumn("관심"), new DataColumn("닥올"), new DataColumn("피올"),
                new DataColumn("절친"), new DataColumn("섹터")
            });

            dtb.Rows.Add("상관", "보유", "누순", "관심", "닥올", "피올", "절친", "섹터");
            dataGridView1.DataSource = dtb;

            for (int i = 0; i < 8; i++)
            {
                dataGridView1.Columns[i].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                dataGridView1.Columns[i].Width = this.Width / 8;
                dataGridView1.Height = dataGridView1Height;
            }
        }

        public void FormSubDraw()
        {
            chart.SuspendLayout();

            if (g.EndNptsBeforeExtend == 0) // if not zero, ShortMove or LongMove test : use old displayList
                DisplayListGivenDisplayMode(g.v.SubChartDisplayMode, displayList, g.clickedStock, g.clickedTitle);
            if (displayList.Count == 0) return;

            // Update form title 
            UpdateFormTitle();

            // Determine grid layout based on the number of displayList
            SetGridDimensions(ref _maxSpace);

            var chartAreas = new List<ChartArea>();
            var annotations = new List<Annotation>();

            // every minute, rebuild chart areas
            if (_chartAreaStopwatch.Elapsed >= _rebuildInterval) // after 1 min set all chart areas to create new
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

            for (int i = 0; i < displayList.Count; i++)
            {
                if (i >= _maxSpace)
                    break;
                string stock = displayList[i];
                if (string.IsNullOrEmpty(stock))
                {
                    continue;
                }

                var data = g.StockRepo.TryGetDataOrNull(stock);
                if (data == null)
                {
                    continue;
                }

                // Index
                if (g.StockManager.IndexList.Contains(data.Stock))
                {
                    var (area, anno) = ChartIndex.UpdateChartArea(chart2, data);
                    if (area != null && anno != null)
                    {
                        chartAreas.Add(area);
                        annotations.Add(anno);   // ✅ 추가
                    }
                }

                // General
                else
                {
                    var (area, anno) = ChartGeneral.UpdateChartArea(chart2, data);
                    if (area != null && anno != null)
                    {
                        chartAreas.Add(area);
                        annotations.Add(anno);
                    }
                }
            }

            int annotationsCount = g.ChartManager.Chart2.Annotations.Count;


            RelocateChart2AreasAndAnnotations(); // done

            

            CleanupChart2();

            dataGridView1.Refresh();

            chart.ResumeLayout();
            chart.Invalidate();

            // 🔹 Return focus to main chart
            // _mainForm?.BeginInvoke((Action)(() => _mainForm.Activate()));
            int seriesCount = chart.Series.Count;
            int chartAreaCount = chart.ChartAreas.Count;
            int annotationCount = chart.Annotations.Count;

         
            //for (int i = 0; i < 5; i++)
            //    AreaHud.ShowHud(chart, displayList[i], displayList[i], 3000 * (i + 5), 15f, Color.Black);
        
        }

        //areasCount = g.ChartManager.Chart2.ChartAreas.Count;
        //annotationsCount = g.ChartManager.Chart2.Annotations.Count;
        //seriesCount = g.ChartManager.Chart2.Series.Count;

        public void RelocateChart2AreasAndAnnotations()
        {
            float cellWidth = 100f / nCol; // nCol is number of columns, not fixed 
            float cellHeight = 100f / nRow; // nRow is number of rows, not fixed

            for (int i = 0; i < displayList.Count; i++)
            {
                if (i >= _maxSpace)
                    break;

                string stock = displayList[i];
                if (string.IsNullOrEmpty(stock))
                {
                    //Console.WriteLine($"[Warning] Empty or null stock in SubChart displayList at index {i}");
                    continue;
                }

                string areaName = stock;

                int row = i % nRow;
                int col = i / nRow;

                float x = col * cellWidth;
                float y = row * cellHeight;

                if (chart2.ChartAreas.IndexOf(areaName) >= 0)
                {
                    var area = chart2.ChartAreas[areaName];
                    area.Position = new ElementPosition(x, y, cellWidth, cellHeight);
                   
             
                    if (!area.Visible)
                        area.Visible = true;
                }
                else
                {
                    continue;
                }

                var anno = chart2.Annotations.FirstOrDefault(a => a.Name == areaName);
                if (anno is RectangleAnnotation rect)
                {
                    rect.X = x;
                    rect.Y = y;
                } 

                if (anno != null && !anno.Visible)
                    anno.Visible = true;

            }
            chart2.Invalidate();
        }

        private void CleanupChart2()
        {
            var chart = g.ChartManager.Chart2;

            // 유효 종목 목록 (비어 있지 않은 항목만)
            var validStocks = new HashSet<string>(
                displayList.Where(s => !string.IsNullOrEmpty(s)).Take(_maxSpace)
            );

            // === 1️⃣ ChartAreas 정리 ===
            var areasToRemove = chart.ChartAreas
                .Where(area => string.IsNullOrEmpty(area.Name) || !validStocks.Contains(area.Name))
                .ToList();

            foreach (var area in areasToRemove)
            {
                if (area == null) continue;
                chart.ChartAreas.Remove(area);
            }

            // === 2️⃣ Annotations 정리 (index / general 모두 포함) ===
            var annotationsToRemove = chart.Annotations
                .Where(anno => string.IsNullOrEmpty(anno.Name) || !validStocks.Contains(anno.Name))
                .ToList();

            foreach (var anno in annotationsToRemove)
            {
                if (anno == null) continue;
                chart.Annotations.Remove(anno);
            }

            // === 3️⃣ Series 정리 ===
            var seriesToRemove = chart.Series
                .Where(series =>
                {
                    if (string.IsNullOrEmpty(series.Name)) return true;
                    // Series 이름이 유효한 종목 이름으로 시작하지 않으면 제거
                    return !validStocks.Any(stock => series.Name.StartsWith(stock));
                })
                .ToList();

            foreach (var s in seriesToRemove)
            {
                if (s == null) continue;
                chart.Series.Remove(s);
            }

            // === 4️⃣ 불필요한 개체 완전 정리 후 갱신 ===
            chart.Invalidate();
        }


        // 상관, 보유, 누순, 관심, 닥올, 피올, 절친, 섹터
        public static void DisplayListGivenDisplayMode(string SubChartDisplayMode, List<string> displayList, string clickedStock, string clickedTitle)
        {
            displayList.Clear();

            switch (SubChartDisplayMode)
            {
                case "지수":
                    for (int i = 0; i < 2; i++)
                    {
                        string s = g.StockManager.LeverageList[i];
                        if (!string.IsNullOrWhiteSpace(s) && !displayList.Contains(s))
                            displayList.Add(s);
                    }
                    break;

                case "보유":
                    foreach (string s in g.StockManager.HoldingList)
                    {
                        if (!string.IsNullOrWhiteSpace(s) && !displayList.Contains(s))
                            displayList.Add(s);
                    }
                    break;

                case "누순":

                    if (g.GroupManager.GroupRankingList.Count > 0)
                    {
                        var topStocks = g.GroupManager.GetTopStocksFromTopGroups(existing: displayList);
                        foreach (var s in topStocks)
                        {
                            if (!string.IsNullOrWhiteSpace(s) && !displayList.Contains(s))
                                displayList.Add(s);
                        }
                    }
                    break;

                case "상관":
                    var relatedStocks = g.GroupManager.GetStocksByTitle(clickedTitle, displayList);
                    foreach (var s in relatedStocks)
                    {
                        if (!string.IsNullOrWhiteSpace(s) && !displayList.Contains(s))
                            displayList.Add(s);
                    }
                    break;

                case "절친":
                    var data = g.StockManager.Repository.TryGetDataOrNull(g.clickedStock);
                    if (data != null && data.Misc.Corr.Count > 0)
                    {
                        if (!string.IsNullOrWhiteSpace(g.clickedStock) && !displayList.Contains(g.clickedStock))
                            displayList.Add(g.clickedStock);

                        foreach (var kv in data.Misc.Corr)
                        {
                            string friendStock = kv.Key;   // 종목 코드
                            double rho = kv.Value;         // 상관계수

                            if (!string.IsNullOrWhiteSpace(friendStock) && !displayList.Contains(friendStock))
                                displayList.Add(friendStock);
                        }
                    }

                    break;

                case "관심":
                    FileIn.read_파일관심종목();
                    foreach (string s in g.StockManager.InterestedInFile)
                    {
                        if (!string.IsNullOrWhiteSpace(s) && !displayList.Contains(s))
                            displayList.Add(s);
                    }
                    break;

                case "피올":

                    foreach (string s in g.kospi_mixed.stocks)
                    {
                        if (!string.IsNullOrWhiteSpace(s) && !displayList.Contains(s))
                            displayList.Add(s);
                    }
                    break;

                case "닥올":

                    foreach (string s in g.kosdaq_mixed.stocks)
                    {
                        if (!string.IsNullOrWhiteSpace(s) && !displayList.Contains(s))
                            displayList.Add(s);
                    }
                    break;


                case "섹터":
                    {
                        var ordered = g.StockRepo.AllSectorStocks
                            .Where(s => s != null &&
                                        s.Stock != null &&
                                        s.Stock.StartsWith("SECTOR:"))
                            .OrderBy(s => s.Score.SectorRank)
                            .ToList();

                        foreach (var sector in ordered)
                        {
                            if (!displayList.Contains(sector.Stock))
                                displayList.Add(sector.Stock);
                        }
                    }
                    break;

                case "푀손":
                    {
                        var a_tuple = new List<Tuple<double, string>>();

                        foreach (var sd in g.StockRepo.Stocks())
                        {
                            string stock = sd.Stock;

                            if (string.IsNullOrWhiteSpace(stock))
                                continue;


      
                            if (!ChartLayoutUtils.TryGetDrawRange(sd, out int start, out int end))
                                return;

                            if (sd.Api.x[end - 1, 4] < 0)
                                continue;

                            double value = 0.0;
                            var x = sd.Api.x;

                            for (int i = end - 2; i > start; i--)
                            {
                                double deltaPrice = x[end - 1, 1] - (x[i, 1] + x[i - 1, 1]) / 2.0;
                                int deltaVolume = x[i, 4] - x[i - 1, 4];
                                value += deltaPrice * deltaVolume;
                            }

                            a_tuple.Add(Tuple.Create(value, stock));
                        }

                        a_tuple = a_tuple.OrderBy(t => t.Item1).ToList();

                        foreach (var t in a_tuple)
                        {
                            string stock = t.Item2;
                            if (!string.IsNullOrWhiteSpace(stock) && !displayList.Contains(stock))
                                displayList.Add(stock);
                        }
                        break;
                    }
            }
        }

        private void UpdateFormTitle()
        {
            switch (g.v.SubChartDisplayMode)
            {
                case "상관":
                    this.Text = $"{g.v.SubChartDisplayMode} ({g.clickedTitle})";
                    break;
                case "절친":
                    this.Text = $"{g.v.SubChartDisplayMode} ({g.clickedStock})";
                    break;
               
                default:
                    this.Text = g.v.SubChartDisplayMode;
                    break;
            }
        }

        private void SetGridDimensions(ref int maxSpace)
        {
            int count = displayList.Count;
            if (count <= 2) { nCol = 2; nRow = 1; maxSpace = 2; }
            else if (count <= 4) { nCol = 2; nRow = 2; maxSpace = 4; }
            else if (count <= 6) { nCol = 3; nRow = 2; maxSpace = 6; }
            else if (count <= 9) { nCol = 3; nRow = 3; maxSpace = 9; }
            else if (count <= 12) { nCol = 4; nRow = 3; maxSpace = 12; }
            else { nCol = 5; nRow = 3; maxSpace = 15; }
        }

        private void chart2_MouseClick(object sender, MouseEventArgs e)
        {
            string selection = "";


            int row_id = 0, col_id = 0;

            //chart2_info(e, ref selection, ref xval, ref yval, ref row_percentage,
            //    ref col_percentage, ref row_id, ref col_id, ref col_divider);
            g.clickedStock = ChartClickMapper.CoordinateMapping(chart2, nRow, nCol, displayList, e, ref selection, ref col_id, ref row_id);


            if (Control.ModifierKeys == Keys.Control)
            {
                ChartClickHandler.HandleControlClick(chart2, selection, row_id, col_id);
            }
            else
            {
                ChartClickHandler.HandleClick(chart2, selection, row_id, col_id);
            }
        }

        public void FormSub_ResizeEnd(object sender, EventArgs e)
        {
            chart2.Size = new Size((int)(this.Width), (int)(this.Height - dataGridView1Height));
            chart2.Location = new Point(0, dataGridView1Height);

            dataGridView1.Size = new Size(this.Width, dataGridView1Height);
            dataGridView1.Location = new Point(0, 0);
            dtb.Rows[0][0] = "상관";
            dtb.Rows[0][1] = "보유";
            dtb.Rows[0][2] = "누순";
            dtb.Rows[0][3] = "관심";
            dtb.Rows[0][4] = "닥올";
            dtb.Rows[0][5] = "피올";
            dtb.Rows[0][6] = "절친";
            dtb.Rows[0][7] = "섹터";

            dataGridView1.DataSource = dtb;
            for (int i = 0; i < 8; i++)
            {
                dataGridView1.Columns[i].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                dataGridView1.Columns[i].Width = this.Width / 8;
            }





            //int totalCols = 5;
            //int totalRows = 3;

            //int cellWidth = this.ClientSize.Width / totalCols;
            //int cellHeight = this.ClientSize.Height / totalRows;


            //_location.X = chart2.Width / 5 * 4;
            //_location.Y = chart2.Height / 3 + 18;
            //_size.Width = chart2.Width / 5;
            //_size.Height = g.cellHeight * 11;
            //if (g.groupPane?.View != null)
            //{
            //    var view = g.groupPane.View;

            //    view.Location = _location;
            //    view.Size = _size;
            //    view.BringToFront();  // Optional, depending on layer
            //}




            //int scrollBarWidth = SystemInformation.VerticalScrollBarWidth;

            //int totalWidth = g.groupPane.View.Width - scrollBarWidth;
            //g.groupPane.View.Columns[0].Width = totalWidth / 2;
            //g.groupPane.View.Columns[1].Width = totalWidth / 4;
            //g.groupPane.View.Columns[2].Width = totalWidth / 4;
        }

        private void dataGridView1_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            switch (e.ColumnIndex)
            {
                case 0:
                    g.v.SubChartDisplayMode = "상관";
                    return;
                case 1:
                    g.v.SubChartDisplayMode = "보유";
                    break;
                case 2:
                    g.v.SubChartDisplayMode = "누순";
                    break;
                case 3:
                    g.v.SubChartDisplayMode = "관심";
                    break;
                case 4:
                    g.v.SubChartDisplayMode = "닥올";
                    break;
                case 5:
                    g.v.SubChartDisplayMode = "피올";
                    break;
                case 6:
                    g.v.SubChartDisplayMode = "절친";
                    break;
                case 7:
                    g.v.SubChartDisplayMode = "섹터";
                    break;
            }

            g.v.SubChartManualUntil = DateTime.Now.AddSeconds(10);

            FormSubDraw();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // KeyBindingManager.TryHandle encapsulates logics for what to do with certain keys.
            if (KeyBindingManager.TryHandle(keyData))
                return true;

            // custom logic doesn't handle
            return base.ProcessCmdKey(ref msg, keyData);
        }
        private void ConfigureChartAndGridSize_32인치용()
        {
            Rectangle workingRectangle = Screen.PrimaryScreen.WorkingArea;

            this.Size = workingRectangle.Size;
            this.Width /= 2;
            this.Width += 10;
            this.Height += 10;
            this.Padding = new Padding(0);
            this.AutoScroll = false;

            this.StartPosition = FormStartPosition.Manual;
            if (Environment.MachineName == "HP")
            {
                this.Location = new Point(-workingRectangle.Width / 2, 0);
            }
            else
            {
                this.Location = new Point(-workingRectangle.Width / 2, 0);
                if (Screen.AllScreens.Count() == 1)
                    this.Location = new Point(workingRectangle.Width / 2, 0); // one screen
            }

            // groupPane setting 
            //var groupDgv = new DataGridView();
            //var groupDtb = new DataTable();
            //this.Controls.Add(groupDgv); // ✅ added to Form
            //g.groupPane = new GroupPane(groupDgv, groupDtb); // logic wrapper
            //var view = g.groupPane.View;
            //view.BringToFront();


        }

    }


}
