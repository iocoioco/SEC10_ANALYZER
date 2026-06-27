using CPTRADELib;
using New_Tradegy.Library.Deals;
using New_Tradegy.Library.IO;
using New_Tradegy.Library.Models;
using New_Tradegy.Library.Utils;
using OpenQA.Selenium.BiDi.Modules.Input;
using OpenQA.Selenium.BiDi.Modules.Script;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using static New_Tradegy.Library.Deals.QuickTradePopup;
using static OpenQA.Selenium.BiDi.Modules.Script.RemoteValue;

namespace New_Tradegy.Library.Listeners
{
    public class BookBidGeneratorIndex : IBookBidGenerator
    {

        public enum TradeSide
        {
            Buy = 0,
            Sell = 1
        }

        // ============================================================================
        //  BookBid UI 20fps (50ms) throttling + Extra(0.55~0.65s) 유지
        // ============================================================================
        public int Rows { get; set; } = 3;          // ✅ 기본값 지정 (5 or else)

        // ✅ readonly 제거하고 기본값으로 고정 (원하면 생성자에서 덮어쓰기 가능)
        private readonly double _moneyConverter_억원;

        // ====== 20fps throttling 상태 ======
        private readonly object _bookDirtyLock = new object();
        private bool _bookDirty = false;

        private DateTime _lastBookUiUpdate = DateTime.MinValue;
        private const int BOOK_UI_INTERVAL_MS = 50;   // ✅ 20 fps

        // ====== Extra 업데이트 주기 유지(0.55~0.65초) ======
        private DateTime _lastExtraUpdate = DateTime.MinValue;
        private const int EXTRA_INTERVAL_MS = 400;

        // ====== UI Timer (UI thread에서만 Start/Stop 해야 함) ======
        private System.Windows.Forms.Timer _bookUiTimer;

        // ====== 기존 필드 ======
        private readonly CPUTILLib.CpStockCode _stockCodeService = new CPUTILLib.CpStockCode();
        private DSCBO1Lib.StockJpbid _jpbidPrimary;
        private DSCBO1Lib.StockJpbid2 _jpbidSecondary;

        private DataTable _dataTable;
        private DataGridView _dataGridView;

        private string _stock;

        private DateTime _lastReceivedTime = DateTime.Now;
        public DateTime LastReceivedTime => _lastReceivedTime;

        private static readonly object _bookBidLock = new object();

        private readonly StockExchange _caller;

        private string _lastOrdersSig = ""; // ✅ 클래스 필드로 추가

        // ⚠️ 이 필드는 "매 인스턴스마다 자기만의 generators"가 되어서 꼬이기 쉬움.
        // 일단 너 코드 그대로 두되, 나중에 구조 정리할 때 static/외부관리로 빼는 걸 추천.
        //private readonly Dictionary<string, BookBidGenerator> _generators
        //    = new Dictionary<string, BookBidGenerator>();

        private int _cellHeight;

        // ... (아래 메서드들)
        private string _lastExtraTextSig = "";
        private Color[,] _cellColors; // 12x3 재사용

        // class 필드로 선언 (한 번만)
        private int _lastKospi = int.MinValue;
        private int _lastKosdaq = int.MinValue;
        private double _lastNq = double.NaN;

        public BookBidGeneratorIndex(string stock, StockExchange caller)
        {
            _stock = stock;
            _caller = caller;
            var data = g.StockRepo.TryGetDataOrNull(stock);
            if (data == null)
                return;
            _moneyConverter_억원 = data.Api.전일종가 / g.천만원 / 10.0;
        }

        public DataGridView GenerateBookBidView(string stock)
        {
            if (!g.connected) return null;

            // ✅ 중복 방지: 맨 위에서
            if (g.BookBidInstances.ContainsKey(stock))
                return null;

            _stock = stock;

            int w0 = 63, w1 = 63, w2 = 63;

            int requiredRows = 2 * Rows + 1;


            _dataTable = new DataTable();
            _dataTable.Columns.Add("매도");
            _dataTable.Columns.Add("호가");
            _dataTable.Columns.Add("매수");

            for (int i = 0; i < requiredRows; i++)
                _dataTable.Rows.Add("", "", "");

            _cellColors = new Color[2 * Rows + 1, 3];

            
            _cellHeight = g.cellHeight;



            _dataGridView = new DataGridView
            {
                Name = _stock,
                Location = new Point(0, 0),
                Size = new Size(w0 + w1 + w2, _cellHeight * requiredRows),
                Dock = DockStyle.None,
                TabIndex = 1,
                DataSource = _dataTable,
                ColumnHeadersVisible = false,
                RowHeadersVisible = false,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AllowUserToResizeColumns = false,
                ScrollBars = ScrollBars.None,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                DefaultCellStyle = { Font = new Font("Arial Bold", 9, FontStyle.Bold), ForeColor = Color.Black },
                RowTemplate = { Height = _cellHeight },
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                BackgroundColor = Color.LightYellow
            };





            _dataGridView.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

            // ✅ DataError 1번만
            _dataGridView.DataError += (s, e) =>
            {
                FileOut.DataGridView_DataError(s, e, $"jpjd {_dataGridView.Name}");
                OnDataError(s, e);
            };




            // _dataGridView.CellMouseClick += OnCellMouse;



            if (_dataGridView == null)
                return null;

            // 중복 방지
            _dataGridView.CellMouseDown -= OnCellMouseDown;
            _dataGridView.CellMouseDown += OnCellMouseDown;





            // Form에 먼저 붙여서 핸들/바인딩 안정화
            g.MainForm.Controls.Add(_dataGridView);
            _dataGridView.BringToFront();


            // ✅ 여기서 20fps 타이머 init (우리가 만들었던 함수)
            InitBookUi20FpsTimer();

            // 초기 1회 요청
            RequestQuote();

            string stockcode = _stockCodeService.NameToCode(_stock);
            _jpbidPrimary = new DSCBO1Lib.StockJpbid();
            _jpbidPrimary.SetInputValue(0, stockcode);
            _jpbidPrimary.Received += new DSCBO1Lib._IDibEvents_ReceivedEventHandler(OnBookBidReceived);

            _jpbidPrimary.Subscribe();

            // ✅ 성공 등록 (중복 방지)
            g.BookBidInstances[_stock] = this;

            if (_dataGridView.Columns.Count >= 3)
            {
                _dataGridView.Columns[0].Width = w0;
                _dataGridView.Columns[1].Width = w1;
                _dataGridView.Columns[2].Width = w2;
            }
    
            return _dataGridView;
        }

        public void OpenOrUpdateConfirmationForm(bool isSell, string stockName, int Amount, int price, int Urgency, string str)
        {
            //Form_매수_매도 f = GetOpenTradeForm(stockName);
            //if (f != null)
            //{
            //    if (f._isSell == isSell)
            //    {
            //        Utils.SoundUtils.Sound("", "not sold");
            //        return;
            //    }
            //    else
            //    {
            //        // Update existing form
            //        f.UpdateForm(isSell, stockName, Amount, price, Urgency, str);
            //    }
            //}
            //else
            //{
            //    // Create and show a new non-blocking (modeless) confirmation form
            //    Form_매수_매도 form = new Form_매수_매도(isSell, stockName, Amount, price, Urgency, str);
            //    form.Show(); // Modeless (non-blocking)
            //}
        }

        private async void OnCellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;

            bool isSell = (e.ColumnIndex == 0);
            bool isBuy = (e.ColumnIndex == 2);

            if (!isSell && !isBuy)
                return;

            if (e.RowIndex < 0 || e.RowIndex >= Rows * 2)
                return;

            string clickedSymbol = _stock;

            int price = GetClickedPrice(_dataGridView, e);
            if (price <= 0)
                return;

            if (!g.StockManager.Repository.TryGet(clickedSymbol, out var data))
                return;

            if (isBuy)
            {
                var existingOrder = StockExchange.buyOrders
                    .Find(o => o.Stock == clickedSymbol && o.Price == price);
                if (existingOrder != null)
                    StockExchange.buyOrders.Remove(existingOrder);

                int qty = g.일회거래액 * 10000 / price;
                if (qty <= 0) qty = 1;

                SoundUtils.Sound("돈", g.일회거래액.ToString());

                string 매수이유 = "싸다";



                var kind = await QuickPopupHelper.ConfirmByQuickPopupAsync(
                    isBuy: true,
                    stock: clickedSymbol,
                    price: price,
                    qty: qty,
                    reason: 매수이유,
                    commitSource: "manual"
                );

                if (kind == PopupResultKind.Confirm)
                    DealManager.DealExec("매수", clickedSymbol, price, qty, "01");
                else if (kind == PopupResultKind.CancelRemove)
                    g.StockManager.RemoveInterestedWithBid(clickedSymbol);

                return;
            }

            if (isSell)
            {
                var existingOrder = StockExchange.sellOrders
                    .Find(o => o.Stock == clickedSymbol && o.Price == price);
                if (existingOrder != null)
                    StockExchange.sellOrders.Remove(existingOrder);

                int qty = g.일회거래액 * 10000 / price;
                if (qty <= 0) qty = 1;

                if (data.Deal.보유량 < qty)
                {
                    DealManager.DealCancelStock(clickedSymbol);
                    DealManager.DealHold();
                    if (data.Deal.보유량 == 0)
                        return;
                }

                if (data.Deal.보유량 < qty)
                    qty = data.Deal.보유량;

                if (qty <= 0) qty = 1;

                DealManager.DealExec("매도", clickedSymbol, price, qty, "01");
            }
        }



        public void Unsubscribe()
        {
            if (string.IsNullOrEmpty(_stock))
                return;

            // 0️⃣ UI Timer 중지 (제일 중요)
            if (_bookUiTimer != null)
            {
                _bookUiTimer.Stop();
                _bookUiTimer.Dispose();
                _bookUiTimer = null;
            }

            // 1️⃣ 이벤트 해제 + 실시간 해지
            if (_jpbidPrimary != null)
            {
                _jpbidPrimary.Received -= OnBookBidReceived;
                _jpbidPrimary.Unsubscribe();
                _jpbidPrimary = null;
            }

            // 2️⃣ Dictionary 제거 (lock 정석)
            lock (_bookBidLock)
            {
                g.BookBidInstances.Remove(_stock);
            }

            // 3️⃣ DataGridView 제거
            if (_dataGridView != null)
            {
                var parent = _dataGridView.Parent;
                if (parent != null)
                    parent.Controls.Remove(_dataGridView);

                _dataGridView.Dispose();
                _dataGridView = null;
            }
        }
        private void OnBookBidReceived()
        {
            _lastReceivedTime = DateTime.Now;

            lock (_bookDirtyLock)
            {
                _bookDirty = true;  // "새 호가 들어옴" 표시
            }
            // 끝. UI 갱신은 타이머가 한다.
        }

        private void InitBookUi20FpsTimer()
        {
            // 이미 죽었거나 아직 없음 → 안 함
            if (_dataGridView == null || _dataGridView.IsDisposed)
                return;

            // 이미 타이머 있으면 중복 생성 금지
            if (_bookUiTimer != null)
                return;

            _bookUiTimer = new System.Windows.Forms.Timer
            {
                Interval = 20 // 50ms = 20fps 게이트는 FlushBookToUI에서
            };

            _bookUiTimer.Tick += (s, e) =>
            {
                try
                {
                    FlushBookToUI_20Fps();
                }
                catch
                {
                    // UI 타이머는 절대 예외로 죽으면 안 됨
                }
            };

            _bookUiTimer.Start();
        }

        private void FlushBookToUI_20Fps()
        {
            if (_dataGridView == null || _dataTable == null) return;
            if (_dataGridView.IsDisposed) return;

            if (_dataGridView.InvokeRequired)
            {
                _dataGridView.BeginInvoke((Action)FlushBookToUI_20Fps);
                return;
            }

            var now = DateTime.Now;

            bool needUpdate;
            lock (_bookDirtyLock)
            {
                needUpdate = _bookDirty;
                if (!needUpdate) return;
                _bookDirty = false;
            }

            if ((now - _lastBookUiUpdate).TotalMilliseconds < BOOK_UI_INTERVAL_MS)
                return;

            _lastBookUiUpdate = now;

            int requiredRows = 2 * Rows + 1;   // Rows=3 -> 8
            if (_dataTable.Rows.Count < requiredRows) return;

            string H(int idx)
            {
                object o;
                try { o = _jpbidPrimary.GetHeaderValue(idx); }
                catch { o = null; }
                return Convert.ToString(o) ?? string.Empty;
            }

            long ParseLong(object cellObj)
            {
                if (cellObj == null) return 0;
                if (cellObj is long l) return l;
                if (cellObj is int i) return i;
                var s = cellObj.ToString();
                if (string.IsNullOrEmpty(s)) return 0;
                s = s.Replace(",", "").Trim();
                return long.TryParse(s, out var v) ? v : 0;
            }

            int askPrice = 0, bidPrice = 0, askQty = 0, bidQty = 0;
            bool applyStyles = true;

            _dataGridView.SuspendLayout();
            try
            {
                var cm = _dataGridView.BindingContext?[_dataTable] as CurrencyManager;

                cm?.SuspendBinding();
                _dataTable.BeginLoadData();

                try
                {
                    lock (_bookBidLock)
                    {
                        // 3호가, 7행
                        // row 0,1,2 : 매도(위→아래 = 3호가, 2호가, 1호가)
                        // row 3,4,5 : 매수(위→아래 = 1호가, 2호가, 3호가)
                        // row 6     : 하단 정보

                        // 매도 1호가 / 매수 1호가
                        _dataTable.Rows[2][1] = H(3);   // best ask
                        _dataTable.Rows[3][1] = H(4);   // best bid
                        _dataTable.Rows[2][0] = H(5);   // ask qty1
                        _dataTable.Rows[3][2] = H(6);   // bid qty1

                        // 매도 2호가 / 매수 2호가
                        _dataTable.Rows[1][1] = H(7);
                        _dataTable.Rows[4][1] = H(8);
                        _dataTable.Rows[1][0] = H(9);
                        _dataTable.Rows[4][2] = H(10);

                        // 매도 3호가 / 매수 3호가
                        _dataTable.Rows[0][1] = H(11);
                        _dataTable.Rows[5][1] = H(12);
                        _dataTable.Rows[0][0] = H(13);
                        _dataTable.Rows[5][2] = H(14);

                        int.TryParse(H(3), out askPrice);
                        int.TryParse(H(4), out bidPrice);
                        int.TryParse(H(5), out askQty);
                        int.TryParse(H(6), out bidQty);
                    }

                    long ask3Sum = 0, bid3Sum = 0;
                    for (int i = 0; i < 3; i++) ask3Sum += ParseLong(_dataTable.Rows[i][0]);
                    for (int i = 3; i < 6; i++) bid3Sum += ParseLong(_dataTable.Rows[i][2]);

                    double ask3_억 = ask3Sum * _moneyConverter_억원;
                    double bid3_억 = bid3Sum * _moneyConverter_억원;

                    string F(double v) => (v >= 10) ? v.ToString("0") : v.ToString("0.0");

                    // 하단 정보 1행
                    _dataTable.Rows[6][0] = F(ask3_억);
                    _dataTable.Rows[6][1] = H(23);   // 호가갭 또는 가운데 정보 쓰는 값
                    _dataTable.Rows[6][2] = F(bid3_억);

                    if ((now - _lastExtraUpdate).TotalMilliseconds >= EXTRA_INTERVAL_MS)
                    {
                        _lastExtraUpdate = now;
                        applyStyles = RequestQuoteExtra_DataOnly(now);
                    }
                }
                finally
                {
                    _dataTable.EndLoadData();
                    cm?.ResumeBinding();
                }
            }
            finally
            {
                _dataGridView.ResumeLayout(true);
            }

            if (applyStyles)
                ApplyExtraStyles();

            var data = g.StockRepo.TryGetDataOrNull(_stock);
            bool isHolding =
     data?.Deal != null &&
     data.Deal.보유량 > 0;

            if (isHolding)
            {
                g.TradePlanManager.OnBookTick(
                    now,
                    _stock,
                    askPrice,
                    bidPrice,
                    askQty,
                    bidQty);
            }

            // 계산 및 표시
            long qtySum = (long)askQty + bidQty;

            if (qtySum <= 0)
                return;

            if (data.Api.전일종가 <= 0)
                return;

            double micro =
                (askPrice * bidQty + bidPrice * askQty) / (double)qtySum;

            double rate = (micro / data.Api.전일종가 - 1.0) * 100.0;

            if (double.IsNaN(rate) || double.IsInfinity(rate))
                return;

            int index = (int)(rate * 100);

            if (_stock.Contains("KODEX 레버리지"))
                MajorIndex.Instance.KospiIndex = index;
            else
                MajorIndex.Instance.KosdaqIndex = index;

            // ----- 화면 갱신 -----
            _dataTable.Rows[0][2] = MajorIndex.Instance.NasdaqIndex.ToString("F3");
            _dataGridView.Rows[0].Cells[2].Style.BackColor = Color.LightCoral;

            _dataTable.Rows[1][2] = (MajorIndex.Instance.KospiIndex / 100.0).ToString("F2");
            _dataGridView.Rows[1].Cells[2].Style.BackColor = Color.LightGreen;

            _dataTable.Rows[2][2] = (MajorIndex.Instance.KosdaqIndex / 100.0).ToString("F2");
            _dataGridView.Rows[2].Cells[2].Style.BackColor = Color.LightGreen;

            int newKospi = MajorIndex.Instance.KospiIndex;
            int newKosdaq = MajorIndex.Instance.KosdaqIndex;
            double nqNow = MajorIndex.Instance.NasdaqIndex;

            bool changed =
                _lastKospi == int.MinValue ||
                _lastKosdaq == int.MinValue ||
                double.IsNaN(_lastNq) ||

                Math.Abs(newKospi - _lastKospi) >= 1 ||
                Math.Abs(newKosdaq - _lastKosdaq) >= 1 ||
                Math.Abs(nqNow - _lastNq) >= 0.001;

            if (changed)
            {
                _lastKospi = newKospi;
                _lastKosdaq = newKosdaq;
                _lastNq = nqNow;

                g.MainForm.UpdateHud();
            }
        }

        private void RequestQuote()
        {
            if (!g.StockRepo.TryGet(_stock, out var data))
                return;

            _jpbidSecondary = new DSCBO1Lib.StockJpbid2();

            if (_jpbidSecondary.GetDibStatus() == 1)
                return;

            string stockcode = _stockCodeService.NameToCode(_stock);
            _jpbidSecondary.SetInputValue(0, stockcode);

            int result = _jpbidSecondary.BlockRequest();
            if (result != 0) return;

            int requiredRows = 2 * Rows + 1;   // Rows=3 -> 7
            if (_dataTable == null || _dataTable.Rows.Count < requiredRows)
                return;

            _dataTable.Rows[2 * Rows][0] = _jpbidSecondary.GetHeaderValue(4).ToString(); // 매도잔량
            _dataTable.Rows[2 * Rows][2] = _jpbidSecondary.GetHeaderValue(6).ToString(); // 매수잔량

            int indexSell = Rows - 1;   // 2
            int indexBuy = Rows;        // 3

            int n = int.Parse(_jpbidSecondary.GetHeaderValue(1).ToString()) / 2;
            if (n > Rows) n = Rows;

            for (int i = 0; i < n; i++)
            {
                _dataTable.Rows[indexSell][1] = _jpbidSecondary.GetDataValue(0, i).ToString();
                _dataTable.Rows[indexBuy][1] = _jpbidSecondary.GetDataValue(1, i).ToString();

                _dataTable.Rows[indexSell][0] = _jpbidSecondary.GetDataValue(2, i).ToString();
                _dataTable.Rows[indexBuy][2] = _jpbidSecondary.GetDataValue(3, i).ToString();

                indexSell--;
                indexBuy++;
            }

            int valUp = (int)_jpbidSecondary.GetDataValue(0, 0);
            int valDn = (int)_jpbidSecondary.GetDataValue(1, 0);


            // 보완필요 microprice로
            _dataTable.Rows[Rows - 2][2] = (MajorIndex.Instance.KospiIndex / 100.0).ToString("0.##");
            _dataTable.Rows[Rows - 1][2] = (MajorIndex.Instance.KosdaqIndex / 100.0).ToString("0.##");






            if (MathUtils.IsSafeToDivide(valDn))
                _dataTable.Rows[2 * Rows]["호가"] = ((valUp - valDn) / (double)valDn * 100.0).ToString("0.##");
            else
                _dataTable.Rows[2 * Rows]["호가"] = "No Data";









            RequestQuoteExtra_DataOnly(DateTime.Now);
        }












        private bool RequestQuoteExtra_DataOnly(DateTime now)
        {
            if (_dataTable == null || _dataGridView == null) return false;
            if (!g.StockRepo.TryGet(_stock, out var data)) return false;

            int required = 2 * Rows + 1;   // Rows=3 -> 7
            if (_dataTable.Rows.Count < required) return false;

            if (_cellColors == null ||
                _cellColors.GetLength(0) < required ||
                _cellColors.GetLength(1) < 3)
            {
                _cellColors = new Color[required, 3];
            }

            int divider = 10;

            // -----------------------------
            // 1) Extra 텍스트 준비
            // -----------------------------
            string[] col0Base = new string[Rows];   // row 3~5 col0

            for (int i = 0; i < Rows; i++)
            {
                int buy = (data.Api.틱수누량[i] - data.Api.틱수누량[i + 1]) / divider;
                int sell = (data.Api.틱도누량[i] - data.Api.틱도누량[i + 1]) / divider;

                col0Base[i] = $"{buy}/{sell}";
            }

            // col2Base 제거
            // row 0~2 col2 는 인덱스 화면에서
            // NQ / KOSPI ETF / KOSDAQ ETF 표시용으로 사용한다.

            // -----------------------------
            // 2) 호가/잔량(방어)
            // -----------------------------
            int ask1 = 0, bid1 = 0;
            int bestAskQty = 0, bestBidQty = 0;

            int.TryParse(Convert.ToString(_dataTable.Rows[Rows - 1][1]), out ask1);        // row 2
            int.TryParse(Convert.ToString(_dataTable.Rows[Rows][1]), out bid1);            // row 3
            int.TryParse(Convert.ToString(_dataTable.Rows[Rows - 1][0]), out bestAskQty);  // row 2
            int.TryParse(Convert.ToString(_dataTable.Rows[Rows][2]), out bestBidQty);      // row 3

            string gapText = (bid1 > 0)
                ? ((ask1 - bid1) / (double)bid1 * 100.0).ToString("0.##")
                : "No Data";

            // -----------------------------
            // 3) extra 텍스트 시그니처
            // -----------------------------
            string extraSig =
                string.Join("|", col0Base) + "::" +
                gapText;

            bool extraTextChanged = (extraSig != _lastExtraTextSig);
            if (extraTextChanged) _lastExtraTextSig = extraSig;

            if (extraTextChanged)
            {
                // row 3~5 col0 : 0.7초 체결 흐름
                for (int i = 0; i < Rows; i++)
                    _dataTable.Rows[i + Rows][0] = col0Base[i];

                // row 0~2 col2 는 건드리지 않는다.
                // 인덱스 화면에서는 NQ / KOSPI ETF / KOSDAQ ETF 를 표시한다.

                // row 6 col1 : 호가갭 %
                _dataTable.Rows[2 * Rows][1] = gapText;
            }

            // -----------------------------
            // 4) data.Api 갱신(매번)
            // -----------------------------
            data.Api.틱의시간[0] = Convert.ToInt32(now.ToString("HHmmssfff"));
            data.Api.매도1호가 = ask1;
            data.Api.매수1호가 = bid1;
            data.Api.매도1호가잔량 = bestAskQty;
            data.Api.매수1호가잔량 = bestBidQty;

            if (data.Api.전일종가 > 0)
                data.Api.틱의가격[0] = (int)((data.Api.매수1호가 - data.Api.전일종가) * 10000.0 / data.Api.전일종가);

            double differ = 0.0;
            if (data.Api.전일종가 > 0)
                differ = (data.Api.매도1호가 - data.Api.매수1호가) * 10000.0 / data.Api.전일종가;

            double factor = 0.0;
            int sumQty = data.Api.매도1호가잔량 + data.Api.매수1호가잔량;
            if (sumQty > 0)
                factor = (double)data.Api.매수1호가잔량 / sumQty;

            data.Api.틱의가격[0] += (int)(differ * factor);
            data.Api.가격 = data.Api.틱의가격[0];

            data.Api.틱최우선매도호잔량[0] = data.Api.매도1호가잔량;
            data.Api.틱최우선매수호잔량[0] = data.Api.매수1호가잔량;

            // -----------------------------
            // 5) _cellColors 계산
            // -----------------------------
            for (int i = 0; i < _cellColors.GetLength(0); i++)
            {
                for (int j = 0; j < _cellColors.GetLength(1); j++)
                {
                    _cellColors[i, j] = Color.Empty;
                }
            }

            if (!g.confirm_sell)
            {
                for (int i = 0; i < Rows; i++)
                    _cellColors[i, 0] = Color.Cyan;
            }

            if (g.optimumTrading)
            {
                for (int i = Rows; i < 2 * Rows; i++)
                    _cellColors[i, 0] = Color.Yellow;
            }

            return true;
        }



        // ✅ UI Style만 적용 (DataTable/Api 건드리지 않음)
        private void ApplyExtraStyles()
        {
            if (_dataGridView == null) return;
            if (_cellColors == null) return;

            int rowLimit = Math.Min(2 * Rows + 1, _dataGridView.Rows.Count); // Rows=3 -> 7
            int colLimit = Math.Min(3, _dataGridView.Columns.Count);

            for (int i = 0; i < rowLimit; i++)
            {
                for (int j = 0; j < colLimit; j++)
                {
                    var c = _cellColors[i, j];
                    _dataGridView.Rows[i].Cells[j].Style.BackColor =
                        (c == Color.Empty) ? Color.White : c;
                }
            }
        }

        private void OnDataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            // Log the error details to a file or logging system
            LogError(e.Exception);

            // Prevent the default error dialog from showing
            e.ThrowException = false;
        }
        private void LogError(Exception ex)
        {
            // Example logging to a text file
            string filePath = @"C:\BJS\Z Log\LogFile.txt";

            try
            {
                using (StreamWriter writer = new StreamWriter(filePath, true))
                {
                    writer.WriteLine($"[{DateTime.Now}] An error occurred: {ex.Message}");
                    writer.WriteLine(ex.StackTrace);
                }
            }
            catch
            {
                // 로그 실패 시 아무 것도 하지 않음 (또는 Debug.WriteLine 정도만)
            }
        }

        public static int GetClickedPrice(DataGridView dgv, DataGridViewCellMouseEventArgs e)
        {
            if (e.ColumnIndex != 0 && e.ColumnIndex != 2) // click sell or buy side
                return 0;

            if (e.RowIndex < 0 || e.RowIndex >= dgv.Rows.Count) // in the range of bookbid row
                return 0;

            var cellValue = dgv.Rows[e.RowIndex].Cells[1].Value?.ToString();

            if (int.TryParse(cellValue?.Replace(",", ""), out int price))
                return price;

            return StringUtils.ExtractIntFromString(cellValue);
        }
    }
}
