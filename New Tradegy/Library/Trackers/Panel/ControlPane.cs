using MathNet.Numerics.Distributions;
using New_Tradegy.Library.Deals;
using New_Tradegy.Library.UI.KeyBindings;
using New_Tradegy.Library.Utils;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace New_Tradegy.Library.Trackers
{
    public class ControlPane
    {
        // to assign value to a cell
        // var panel = new ControlPane();
        // DataTable table = manager._table;
        // _table.Rows[rowIndex][columnIndex] = value;
        private class ControlSetting
        {
            public string Name { get; set; }
            public int[] Values { get; set; }
            public Func<int> Get { get; set; }
            public Action<int> Set { get; set; }
        }

        public readonly DataTable _table;
        public readonly DataGridView _view;

        private readonly Dictionary<string, ControlSetting> Settings = new Dictionary<string, ControlSetting>();
        public Control View => _view;
        public ControlPane(DataGridView dgv, DataTable dtb)
        {
            _table = dtb;
            _view = dgv;

            InitializeDgv();
            BindGrid(); // has InitializeSetting()
        }

        private void InitializeDgv()
        {
            _view.ScrollBars = ScrollBars.None;
            _view.ColumnHeadersVisible = false;
            _view.RowHeadersVisible = false;

            _view.ReadOnly = true;
            _view.DefaultCellStyle.Font = new Font("Arial Bold", 10, FontStyle.Bold);
            //_view.ColumnHeadersDefaultCellStyle.Font = new Font("Arial Bold", 9, FontStyle.Bold);
            _view.ScrollBars = ScrollBars.None;
            _view.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            _view.AllowUserToResizeColumns = false;
            _view.AllowUserToResizeRows = false;
            _view.AllowUserToAddRows = false;
            _view.AllowUserToDeleteRows = false;
            _view.SelectionMode = DataGridViewSelectionMode.FullRowSelect;

            _view.RowTemplate.Height = g.cellHeight;
            _view.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;

            // _view.RowTemplate.Height = g.cellHeight;
            _view.ForeColor = Color.Black;
            _view.TabStop = false;
        }

        public void BindGrid()
        {
            _view.AutoGenerateColumns = true;

            if (Settings.Count == 0)
                InitializeSettings();

            int Rows = 15, Columns = 4;

            for (int i = 0; i < Columns; i++)
                _table.Columns.Add(i.ToString());

            for (int i = 0; i < Rows; i++)
                _table.Rows.Add("", "", "", "");



            _view.DataSource = _table; // rebind

            _view.Refresh();           // ensure binding completes

            // Optional: You may also want to scroll to top
            if (_view.Rows.Count > 0)
                _view.FirstDisplayedScrollingRowIndex = 0;

            // Appearance
            _view.ReadOnly = true;
            _view.ColumnHeadersVisible = false;
            _view.RowHeadersVisible = false;
            _view.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _view.AllowUserToResizeRows = false;
            _view.AllowUserToResizeColumns = false;
            _view.AllowUserToAddRows = false;
            _view.AllowUserToDeleteRows = false;
            _view.ScrollBars = ScrollBars.None;




            int rows = 15;
            int cellHeight = _view.RowTemplate.Height;

            cellHeight = g.cellHeight;
            int fullHeight = _view.ColumnHeadersHeight + (rows * cellHeight) + 2;

            _view.Height = fullHeight;





            _view.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;



            _view.CellMouseClick += CellMouseClick;




            //for (int i = 0; i < Columns; i++)
            //    _view.Columns[i].Width = _view.Width / Columns;

            DataGridViewBindingCompleteEventHandler handler = null;
            handler = (s, e) =>
            {
                _view.DataBindingComplete -= handler; // ✅ Now matches event type

                int retryCount = 0;
                const int maxRetries = 10;

                void TryFill()
                {
                    if (_view.Columns.Count == 0)
                    {
                        if (++retryCount < maxRetries)
                            _view.BeginInvoke((MethodInvoker)(() => TryFill()));
                        return;
                    }

                    //for (int i = 0; i < _view.Columns.Count; i++)
                    //    _view.Columns[i].Width = _view.Width / _view.Columns.Count;
                    // Initialize key rows
                    int month = g.date % 10000 / 100;
                    int day = g.date % 100;
                    g.일회거래액 = 0;
                    _table.Rows[0][0] = $"{month}/{day}";
                    _table.Rows[0][2] = g.일회거래액;
                    _table.Rows[0][3] = g.예치금;





                    g.v.EruptionRatio = 5;
                    _table.Rows[4][0] = "Erpt";
                    _table.Rows[4][1] = g.v.EruptionRatio;

                    g.v.RankLimit = 5;                    
                    _table.Rows[4][2] = "Rank";
                    _table.Rows[4][3] = g.v.RankLimit;


                    g.v.종가기준추정거래액이상_천만원 = 1000;
                    _table.Rows[5][0] = "종거"; _table.Rows[5][1] = g.v.종가기준추정거래액이상_천만원;

                    g.v.분당거래액이상_천만원 = 20;
                    _table.Rows[5][2] = "분거"; _table.Rows[5][3] = g.v.분당거래액이상_천만원;

                    g.v.호가거래액이상_백만원 = 30;
                    _table.Rows[6][0] = "호가"; _table.Rows[6][1] = g.v.호가거래액이상_백만원;

                    g.v.편차이상 = 1;
                    _table.Rows[6][2] = "편차"; _table.Rows[6][3] = g.v.편차이상;

                    g.v.배차이상 = 0;
                    _table.Rows[7][0] = "배차"; _table.Rows[7][1] = g.v.배차이상;

                    _table.Rows[7][2] = "시총"; _table.Rows[7][3] = 0; g.v.시총이상 = 0;

                    // _table.Rows[8][0] = "수과"; _table.Rows[8][1] = 10; g.v.수급과장배수 = 10; // 사용하지 않고 있음
                    _table.Rows[8][2] = "배과"; _table.Rows[8][3] = 1; g.v.배수과장배수 = 1;

                    _table.Rows[9][0] = "평가"; _table.Rows[9][1] = 10; g.MarketeyeCountDivider = 10;
                    _table.Rows[9][2] = "초간"; _table.Rows[9][3] = 30 * 1000; g.postIntervalTime = 30 * 1000;

                    _table.Rows[10][0] = "Font"; _table.Rows[10][1] = g.v.font = 22; g.v.font /= 2.0f;

                    _table.Rows[12][0] = "푀플"; _table.Rows[12][1] = "1"; g.v.푀플 = 1;
                    _table.Rows[12][2] = "배플"; _table.Rows[12][3] = "1"; g.v.배플 = 1;

                    _table.Rows[13][0] = "선폭"; _table.Rows[13][1] = 2; g.LineWidth = 2;

                }

                if (_view.IsHandleCreated)
                {
                    _view.BeginInvoke((MethodInvoker)(() => TryFill()));
                }
                else
                {
                    _view.HandleCreated += (ss, ee) => _view.BeginInvoke((MethodInvoker)(() => TryFill()));
                }
            };

            _view.DataBindingComplete += handler;



        }

        public void InitializeSettings()
        {
            // 20260510
            Settings["Erpt"] = new ControlSetting
            {
                Name = "Erpt",
                Values = new[] { 10, 15, 20, 25, 30, 35, 40, 45, 50, 60, 70, 100, 150 }, // 1/10으로 사용
                Get = () => (int)(g.v.EruptionRatio * 10),
                Set = val =>
                {
                    g.v.EruptionRatio = val / 10.0;
                    _table.Rows[4][1] = val / 10.0;
                }
            };

            Settings["Rank"] = new ControlSetting
            {
                Name = "Rank",
                Values = new[] { 1, 2, 3, 5, 7, 10, 15, 20, 30 },
                Get = () => (int)g.v.RankLimit,
                Set = val =>
                {
                    g.v.RankLimit = val;
                    _table.Rows[4][3] = val;
                }
            };







            Settings["종거"] = new ControlSetting
            {
                Name = "종거",
                Values = new[] { 0, 1000, 2000, 3000, 5000, 10000, 15000, 20000 },
                Get = () => (int)(g.v.종가기준추정거래액이상_천만원),
                Set = val =>
                {
                    g.v.종가기준추정거래액이상_천만원 = val;
                    _table.Rows[5][1] = val;
                }
            };

            Settings["분거"] = new ControlSetting
            {
                Name = "분거",
                Values = new[] { 0, 5, 10, 20, 30, 50, 70, 100, 200, 300 },
                Get = () => g.v.분당거래액이상_천만원,
                Set = val =>
                {
                    g.v.분당거래액이상_천만원 = val;
                    _table.Rows[5][3] = val;
                }
            };

            Settings["호가"] = new ControlSetting
            {
                Name = "호가",
                Values = new[] { 0, 10, 20, 30, 50, 70, 100, 200, 300, 500, 700, 1000 },
                Get = () => g.v.호가거래액이상_백만원,
                Set = val =>
                {
                    g.v.호가거래액이상_백만원 = val;
                    _table.Rows[6][1] = val;
                }
            };

            Settings["편차"] = new ControlSetting
            {
                Name = "편차",
                Values = new[] { 0, 1, 2, 3, 5, 7 },
                Get = () => g.v.편차이상,
                Set = val =>
                {
                    g.v.편차이상 = val;
                    _table.Rows[6][3] = val;
                }
            };

            Settings["배차"] = new ControlSetting
            {
                Name = "배차",
                Values = new[] { 0, 10, 25, 50, 75, 100 },
                Get = () => g.v.배차이상,
                Set = val =>
                {
                    g.v.배차이상 = val;
                    _table.Rows[7][1] = val;
                }
            };

            Settings["시총"] = new ControlSetting
            {
                Name = "시총",
                Values = new[] { 0, 10, 30, 50, 100, 200, 500, 1000 },
                Get = () => g.v.시총이상,
                Set = val =>
                {
                    g.v.시총이상 = val;
                    _table.Rows[7][3] = val;
                }
            };

            //Settings["수과"] = new ControlSetting
            //{
            //    Name = "수과",
            //    Values = new[] { 5, 10, 15, 20, 25, 30, 40, 60, 100 },
            //    Get = () => g.v.수급과장배수,
            //    Set = val =>
            //    {
            //        g.v.수급과장배수 = val;
            //        _table.Rows[8][1] = val;
            //    }
            //};

            Settings["배과"] = new ControlSetting
            {
                Name = "배과",
                Values = new[] { 1, 2, 3, 4, 5, 7, 10 },
                Get = () => (int)(g.v.배수과장배수),
                Set = val =>
                {
                    g.v.배수과장배수 = val;
                    _table.Rows[8][3] = val;
                }
            };

            Settings["평가"] = new ControlSetting
            {
                Name = "평가",
                Values = new[] { 2, 5, 7, 10, 13, 15, 17, 20, 25, 40, 60 },
                Get = () => g.MarketeyeCountDivider,
                Set = val =>
                {
                    g.MarketeyeCountDivider = val;
                    _table.Rows[9][1] = val;
                }
            };

            Settings["초간"] = new ControlSetting
            {
                Name = "초간",
                Values = new[] { 10 * 1000, 15 * 1000, 20 * 1000, 25 * 1000,
                    30 * 1000, 40 * 1000, 50 * 1000, 60 * 1000},
                Get = () => g.MarketeyeCountDivider,
                Set = val =>
                {
                    g.MarketeyeCountDivider = val;
                    _table.Rows[9][3] = val;
                }
            };

            Settings["Font"] = new ControlSetting
            {
                Name = "Font",
                Values = new[] { 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25 },
                Get = () => (int)(g.v.font * 2),
                Set = val =>
                {
                    g.v.font = val / 2f;
                    _table.Rows[10][1] = val;
                }
            };

            Settings["푀플"] = new ControlSetting
            {
                Name = "푀플",
                Values = new[] { 0, 1 },
                Get = () => g.v.푀플,
                Set = val =>
                {
                    g.v.푀플 = val;
                    _table.Rows[12][1] = val.ToString();
                }
            };

            Settings["배플"] = new ControlSetting
            {
                Name = "배플",
                Values = new[] { 0, 1 },
                Get = () => g.v.배플,
                Set = val =>
                {
                    g.v.배플 = val;
                    _table.Rows[12][3] = val.ToString();
                }
            };

            Settings["선폭"] = new ControlSetting
            {
                Name = "선폭",
                Values = new[] { 1, 2, 3 },
                Get = () => g.LineWidth,
                Set = val =>
                {
                    g.LineWidth = val;
                    _table.Rows[13][1] = val;
                }
            };
        }

        private int GetNextValue(int[] array, int current, bool increase)
        {
            int index = Array.IndexOf(array, current);
            if (index == -1) return current;

            if (increase && index < array.Length - 1)
                return array[index + 1];
            if (!increase && index > 0)
                return array[index - 1];

            return current;
        }
        private void CellMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex < 0 || (e.ColumnIndex > 3)) return;



            // ✅ [0][2] = 일회거래액 셀: Left(+), Right(-)
            if (e.RowIndex == 0 && e.ColumnIndex == 2)
            {
                if (!g.test)
                {
                    char op = '\0';

                    if (e.Button == MouseButtons.Left) op = '+';
                    else if (e.Button == MouseButtons.Right) op = '-';
                    else return; // middle 등은 무시

                    g.일회거래액 = KeyHelper.DealMoney(g.일회거래액, op);

                    // 화면 셀 갱신
                    string newText = g.일회거래액.ToString();
                    if (g.controlPane.GetCellValue(0, 2) != newText)
                    {
                        g.controlPane.SetCellValue(0, 2, g.일회거래액);
                    }

                    // 사운드 비동기
                    Task.Run(() =>
                    {
                        try { SoundUtils.Sound("돈", newText); }
                        catch { /* ignore or log */ }
                    });
                }

                // ✅ 여기서 끝내야 기존 switch/케이스 로직 안 타서 중복 실행 방지
                return;
            }




            if (e.RowIndex < 4)
            {
                switch (e.RowIndex)
                {
                    case 0:
                        switch (e.ColumnIndex)
                        {
                            case 0:
                            case 1:
                            case 2:
                                bool isControlPaneOnTop =
                                    _view.Parent.Controls.GetChildIndex(_view) == 0;
                                int height;
                                if (isControlPaneOnTop)
                                {
                                    height = g.cellHeight * 3;
                                    _view.Height = height;
                                    g.tradePane.View.BringToFront();
                                }

                                else
                                {
                                    height = g.cellHeight * 15;
                                    _view.Height = height;
                                    _view.BringToFront();
                                }


                                break;
                            case 3:
                                DealManager.DealDeposit();
                                break;
                        }
                        break;



                    #region
                    //case 0:
                    //            DealManager.DealProfit();
                    //            break;
                    //        case 1:
                    //            Process.Start("chrome.exe", "--new-tab https://kr.investing.com/currencies/usd-krw");
                    //            break;
                    //        case 2:
                    //            Process.Start("chrome.exe", "--new-tab https://www.investing.com/indices/nq-100-futures?cid=1175151");
                    //            break;
                    //        case 3:
                    //            Process.Start("chrome.exe", "--new-tab https://finviz.com/map.ashx?t=sec");
                    //        break;
                    //}
                    #endregion

                    case 2:
                        Process.Start("chrome.exe", "--new-tab https://www.investing.com/indices/major-indices");
                        break;

                    case 3:
                        if (e.ColumnIndex == 0 || e.ColumnIndex == 1)
                            Process.Start("chrome.exe", "--new-tab https://www.investing.com/crypto/bitcoin/chart");
                        break;


                    //case 4:
                    //    if (e.ColumnIndex == 0 || e.ColumnIndex == 1)
                    //    {
                    //        if (e.ColumnIndex == 0)
                    //        {
                    //            g.v.q_advance_lines -= 5;
                    //            if (g.v.q_advance_lines < 5)
                    //                g.v.q_advance_lines = 5;
                    //        }
                    //        if (e.ColumnIndex == 1)
                    //        {
                    //            g.v.q_advance_lines += 5;
                    //        }
                    //        _table.Rows[4][1] = g.v.q_advance_lines;
                    //    }
                    //    break;
                }
            }
            else
            {
                int selecteddColumnIndex = 0;
                if (e.ColumnIndex >= 2)
                {
                    selecteddColumnIndex = 2;
                }
                string key = _view.Rows[e.RowIndex].Cells[selecteddColumnIndex].Value?.ToString();
                if (string.IsNullOrWhiteSpace(key) || !Settings.ContainsKey(key)) return;


                var setting = Settings[key];
                int current = setting.Get();
                bool increase = (e.ColumnIndex % 2 == 1);
                int next = GetNextValue(setting.Values, current, increase);

                if (next != current)
                {
                    setting.Set(next);

                    var evalDrawKeys = new HashSet<string>
                    {
                        "종거", "분거", "호가", "편차", "배차", "시총"
                    };

                    var postEvalDrawKeys = new HashSet<string>
                    {
                        "초간"
                    };

                    var drawKeys = new HashSet<string>
                    {
                        "수과", "배과", "Font"
                        //,
                        //"Erpt", "Rank"
                    };

                    if (evalDrawKeys.Contains(key))
                        ActionCode.New('B', false, eval: true, draw: 'B').Run();

                    if (postEvalDrawKeys.Contains(key))
                        ActionCode.New('B', true, eval: true, draw: 'B').Run();

                    if (drawKeys.Contains(key))
                        ActionCode.New('B', false, eval: false, draw: 'B').Run();
                }
            }

        }

        public void SetCellValue(int row, int col, object value)
        {
            if (_view == null || _view.IsDisposed) return;
            if (!_view.IsHandleCreated)
                return; // 폼/컨트롤 아직 안뜸. 호출 측에서 타이밍 조정하거나 큐잉해도 됨.

            if (_view.InvokeRequired)
            {
                _view.BeginInvoke((MethodInvoker)(() => SetCellValue(row, col, value)));
                return;
            }

            if (row < 0 || col < 0) return;
            if (row >= _table.Rows.Count || col >= _table.Columns.Count) return;

            _table.Rows[row][col] = value;

            // 화면 반영(가벼움)
            _view.InvalidateCell(col, row);
            // _view.Update();  // ← 크로스스레드 위험 + 과한 호출, 제거
            // _view.Refresh(); // ← 동일, 제거
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
