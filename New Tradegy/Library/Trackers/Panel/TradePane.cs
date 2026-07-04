using New_Tradegy.Library.Core;
using New_Tradegy.Library.Deals;
using New_Tradegy.Library.Models;
using New_Tradegy.Library.Trackers;
using New_Tradegy.Library.Utils;
using System;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace New_Tradegy.Library.Trackers
{
    public class TradePane
    {
        private readonly DataGridView _view;
        private readonly DataTable _table;

        public Control View => _view;

        public TradePane(DataGridView dgv, DataTable dtb)
        {
            _table = dtb;
            _view = dgv;

            InitializeDgv(_view);
            BindGrid(_view); // has InitializeSetting()
        }

        private void InitializeDgv(DataGridView dgv)
        {
            _view.DefaultCellStyle.Font = new Font("Arial Bold", 9, FontStyle.Bold);
            // === Visual settings ===
            dgv.ColumnHeadersVisible = false;
            dgv.RowHeadersVisible = false;
            dgv.BorderStyle = BorderStyle.None;
            dgv.CellBorderStyle = DataGridViewCellBorderStyle.Single;
            dgv.ForeColor = Color.Black;

            // === Behavior settings ===
            dgv.ReadOnly = true;
            dgv.TabStop = false;
            dgv.ScrollBars = ScrollBars.None; // ? Vertical bar only
            dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;

            dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgv.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;

            dgv.AllowUserToResizeColumns = false;
            dgv.AllowUserToResizeRows = false;
            dgv.AllowUserToAddRows = false;
            dgv.AllowUserToDeleteRows = false;

            _view.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;

            // === Optional row height setting ===
            dgv.RowTemplate.Height = g.cellHeight + 1;

            // === Events ===
            dgv.CellFormatting += CellFormatting;
            dgv.CellMouseClick += CellMouseClick;

            // === Reset scroll position (if needed) ===
            if (dgv.Rows.Count > 0)
                dgv.FirstDisplayedScrollingRowIndex = 0;
        }

        private void BindGrid(DataGridView _view)
        {
            _view.AutoGenerateColumns = true;

            // Setup columns
            _table.Columns.Add("Stock"); // stock
            _table.Columns.Add("BuyorSell"); // 매수/매도
            _table.Columns.Add("Price"); // 가격
            _table.Columns.Add("Processing"); // 거래진행

            // Add rows
            int Rows = 10; // or configurable
            for (int j = 0; j < Rows; j++)
                _table.Rows.Add("", "", "", "");

            // Bind table to DataGridView
            _view.DataSource = _table;


            if (_view.InvokeRequired)
            {
                _view.Invoke((MethodInvoker)(() => _view.Visible = true));
            }
            else
            {
                _view.Visible = true;
            }

        }

        // TradePane.cs 안에 추가
        public void SafeBeginInvoke(Action action)
        {
            if (_view == null || _view.IsDisposed || !_view.IsHandleCreated)
                return;

            try
            {
                if (_view.InvokeRequired)
                    _view.BeginInvoke(action);
                else
                    action();
            }
            catch
            {
            }
        }
        public void RefreshTradePane()
        {
            lock (OrderItemTracker.orderLock)
            {
                _view.SuspendLayout();

                int rowCount = 0;

                if (OrderItemTracker.OrderMap != null)
                {
                    foreach (var data in OrderItemTracker.OrderMap.Values)
                    {
                        _table.Rows[rowCount][0] = data.stock;
                        _table.Rows[rowCount][1] = data.buyorSell;
                        _table.Rows[rowCount][2] = data.m_nPrice;
                        _table.Rows[rowCount][3] = data.m_nAmt + "/" + data.m_nContAmt;
                        rowCount++;
                    }
                }

                FillEmptyRow(rowCount++);

                int 순서 = 0;

                foreach (var stock in g.StockManager.HoldingList.ToList())
                {
                    var data = g.StockRepo.TryGetDataOrNull(stock);
                    if (data == null) continue;

                    if (data.Deal.장부가 > 0 && data.Api.매수1호가 > 0)
                    {
                        data.Deal.수익률 =
                            (double)(data.Api.매수1호가 - data.Deal.장부가)
                            / data.Deal.장부가 * 100;
                    }

                    EnsureDefaultStopLoss(data);

                    _table.Rows[rowCount][0] = data.Stock;
                    _table.Rows[rowCount][1] = Math.Round(data.Api.매수1호가 / 10000.0, 4);

                    // 컬럼2: 손절폭(%)
                    _table.Rows[rowCount][2] = Math.Round(data.Deal.손절률, 1);

                    // 컬럼3: 평가금액(백만원 단위) / 수익률(%)
                    double 평가금액_백만원 =
                        data.Api.매수1호가 * data.Deal.보유량 / 1_000_000.0;

                    string 평가금액표시 =
                        평가금액_백만원 < 1.0
                            ? Math.Round(평가금액_백만원, 1).ToString("0.0")
                            : Math.Round(평가금액_백만원).ToString("0");

                    _table.Rows[rowCount][3] =
                        평가금액표시 + "/" + Math.Round(data.Deal.수익률, 1);

                    UpdateSound(data, 순서, rowCount);
                    CheckForceStopLoss(data, 순서, rowCount);

                    rowCount++;
                    순서++;

                    if (rowCount == 10) break;
                }

                for (int i = rowCount; i < _table.Rows.Count; i++)
                {
                    FillEmptyRow(i);
                }

                _view.ResumeLayout();
            }
        }

        private void EnsureDefaultStopLoss(StockData data)
        {
            if (data?.Deal == null) return;
            if (data.Deal.보유량 <= 0) return;

            if (data.Deal.손절률 > 0) return;

            bool isIndexOrEtf =
                data.Stock.Contains("레버리지") ||
                data.Stock.Contains("KODEX");

            data.Deal.손절률 = isIndexOrEtf ? 0.3 : 0.5;
        }

        private void CheckForceStopLoss(StockData data, int 순서, int rowCount)
        {
            if (data?.Deal == null) return;
            if (data.Deal.보유량 <= 0) return;
            if (data.Deal.손절률 <= 0) return;

            bool 손절도달 = data.Deal.수익률 <= -data.Deal.손절률;
            if (!손절도달) return;

            // 1) TradePane 행 경고
            _view.Rows[rowCount].DefaultCellStyle.BackColor = Color.IndianRed;
            _view.Rows[rowCount].DefaultCellStyle.ForeColor = Color.White;

            // 2) 소리
            SoundUtils.Sound("Keys", "warning");

            // 3) 차트 경고 flash
            FlashChartAreaStopLoss(data);
        }

        private async void FlashChartAreaStopLoss(StockData data)
        {
            try
            {
                var chart = g.chart1;
                if (chart == null) return;

                if (!chart.ChartAreas.IsUniqueName(data.Stock))
                {
                    var area = chart.ChartAreas[data.Stock];

                    Color oldColor = area.BackColor;

                    area.BackColor = Color.FromArgb(255, 80, 80);

                    chart.Invalidate();

                    await Task.Delay(300);

                    area.BackColor = oldColor;

                    chart.Invalidate();
                }
            }
            catch
            {
            }
        }

        private void CellMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _view.Rows.Count)
                return;

            string stock = _view.Rows[e.RowIndex].Cells[0].Value?.ToString();
            if (string.IsNullOrEmpty(stock)) return;

            var data = g.StockRepo.TryGetDataOrNull(stock);
            if (data == null || data.Deal == null) return;

            switch (e.ColumnIndex)
            {
                case 1: // 체결중 주문 취소
                    if (e.Button != MouseButtons.Left) return;
                    if (g.test) return;

                    if (e.RowIndex < OrderItemTracker.OrderMap.Count)
                    {
                        DealManager.DealCancelRowIndex(e.RowIndex);
                        SoundUtils.Sound("Keys", "cancel");
                    }
                    break;

                case 2: // 손절률 조정
                    {
                        bool isIndex = data.Stock.Contains("KODEX");

                        double step = isIndex ? 0.05 : 0.1;
                        double min = isIndex ? 0.05 : 0.1;

                        if (e.Button == MouseButtons.Left)
                        {
                            data.Deal.손절률 += step;
                        }
                        else if (e.Button == MouseButtons.Right)
                        {
                            data.Deal.손절률 -= step;
                        }
                        else
                        {
                            return;
                        }

                        // 최소값 방어
                        if (data.Deal.손절률 < min)
                            data.Deal.손절률 = min;

                        data.Deal.손절률 =
                            Math.Round(data.Deal.손절률, 2);

                        _view.Rows[e.RowIndex].Cells[2].Value =
                            data.Deal.손절률.ToString(isIndex ? "0.00" : "0.0");

                        SoundUtils.Sound("Keys", "click"); // No Sound Yet

                        break;
                    }

                case 3:
                    // 현재는 평가금액/수익률 표시용
                    // 클릭 기능 없음
                    break;
            }
        }

        private void CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            _view.Rows[0].DefaultCellStyle.Font = new Font("Arial Bold", 9, FontStyle.Bold);

        }

        private void FillEmptyRow(int row)
        {
            for (int c = 0; c < _table.Columns.Count; c++)
                _table.Rows[row][c] = "";

            if (row >= 0 && row < _view.Rows.Count)
            {
                _view.Rows[row].DefaultCellStyle.BackColor = Color.White;
                _view.Rows[row].DefaultCellStyle.ForeColor = Color.Black;
                _view.Rows[row].DefaultCellStyle.SelectionBackColor = Color.DodgerBlue;
                _view.Rows[row].DefaultCellStyle.SelectionForeColor = Color.White;
            }
        }

        private void UpdateSound(StockData o, int index, int row)
        {
            string[] names = { "one", "two", "three" };
            if (index < names.Length && o.Deal.보유량 * o.Api.현재가 > 500000)
            {
                string postfix = o.Deal.전수익률 == o.Deal.수익률 ? "" : (o.Deal.전수익률 < o.Deal.수익률 ? " up" : " down");
                Utils.SoundUtils.Sound("가", names[index] + postfix);
            }

            //double r = Math.Max(-0.1, Math.Min(0.1, o.Deal.수익률));

            //int red = 255, green = 255;
            //if (r > 0)
            //    red = Math.Max(0, 255 - (int)(255.0 * r / 10.0));
            //else if (r < 0)
            //    green = Math.Max(0, 255 + (int)(255.0 * r / 10.0));

            //if (row >= 0 && row < _view.Rows.Count)
            //{
            //    _view.Rows[row].DefaultCellStyle.BackColor = Color.FromArgb(red, green, 255);
            //}

            o.Deal.전수익률 = o.Deal.수익률;
        }



        public void SetCellValue(int row, int col, object value)
        {
            if (_view.InvokeRequired)
                _view.Invoke(new Action(() => _table.Rows[row][col] = value)); // Invoke = 동기
            else
                _table.Rows[row][col] = value;

            // 바로 그리게 하려면 (선택)
            _view.InvalidateCell(col, row);
            _view.Update(); // 또는 _view.Refresh();
        }

        public string GetCellValue(int row, int col)
        {
            return _table.Rows[row][col]?.ToString() ?? string.Empty;
        }

        public bool HasRows()
        {
            return _table != null && _table.Rows.Count > 0;
        }
    }
}
