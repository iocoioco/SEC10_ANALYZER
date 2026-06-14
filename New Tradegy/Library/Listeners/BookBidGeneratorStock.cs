using CPTRADELib;
using New_Tradegy.Library.Deals;
using New_Tradegy.Library.IO;
using New_Tradegy.Library.Models;
using New_Tradegy.Library.Trackers;
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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using static New_Tradegy.Library.Deals.QuickTradePopup;
using static OpenQA.Selenium.BiDi.Modules.Script.LocalValue;
using static OpenQA.Selenium.BiDi.Modules.Script.RemoteValue;

namespace New_Tradegy.Library.Listeners
{
    public class BookBidGeneratorStock : IBookBidGenerator
    {

        public enum TradeSide
        {
            Buy = 0,
            Sell = 1
        }

        // ============================================================================
        //  BookBid UI 20fps (50ms) throttling + Extra(0.55~0.65s) 유지
        // ============================================================================
        public int Rows { get; set; } = 5;          // ✅ 기본값 지정 (5 or else)

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

        public BookBidGeneratorStock(string stock, StockExchange caller)
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

            int w0 = 58, w1 = 63, w2 = 58;

            int requiredRows = 2 * Rows + 2;


            _dataTable = new DataTable();
            _dataTable.Columns.Add("매도");
            _dataTable.Columns.Add("호가");
            _dataTable.Columns.Add("매수");

            for (int i = 0; i < requiredRows; i++)
                _dataTable.Rows.Add("", "", "");

            _cellColors = new Color[2 * Rows + 2, 3];

          
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
            // 1) 클릭한 종목을 로컬로 고정 (절대 다른 변수로 바꾸지 마)
            string clickedSymbol = _stock;     // 너 코드에 맞게: 셀에서 읽은 종목 코드



            // 0) 클릭한 셀의 가격 얻기
            int price = GetClickedPrice(_dataGridView, e);
            if (price <= 0)
                return;

            if (!g.StockManager.Repository.TryGet(clickedSymbol, out var data))
                return;

            bool isSell = (e.ColumnIndex == 0);
            bool isBuy = (e.ColumnIndex == 2);

            // ============================================================
            // 1) RIGHT CLICK = "계획 라인 추가" (실제 주문은 절대 실행 안 함)
            // ============================================================
            // 1) RIGHT CLICK = "계획 라인 추가" (실제 주문은 절대 실행 안 함)
            if (e.Button == MouseButtons.Right)
            {
                // 매수/매도 호가 컬럼이 아닌 경우 무시
                if (!isSell && !isBuy)
                    return;

                var side = isBuy ? "매수" : "매도";



                g.TradePlanManager.AddPlanFromOrderBook(clickedSymbol, side, price, g.일회거래액);
                System.Diagnostics.Debug.WriteLine($"Plans.Count = {g.TradePlanManager.Plans.Count}");

                g.MainForm.RefreshPlanUi();

                return;  // Right click은 여기서 종료
            }

            // ============================================================
            // 2) LEFT CLICK = 기존 즉시매매 로직 그대로 실행
            // ============================================================
            if (e.Button != MouseButtons.Left)
                return;

            // Column 1: Passing price / hoga depth 토글 (기존 코드 유지)
            if (e.ColumnIndex == 1)
            {
                if (e.RowIndex < Rows)
                {
                    data.Pass.upperPassingPrice =
                        (price == data.Pass.upperPassingPrice) ? 0 : price;
                }
                else if (e.RowIndex <= Rows)
                {
                    data.Pass.lowerPassingPrice =
                        (price == data.Pass.lowerPassingPrice) ? 0 : price;
                }
                else if (e.ColumnIndex <= Rows * 2 && !g.StockManager.IndexList.Contains(clickedSymbol))
                {
                    Rows = (Rows == 5) ? 10 : 5;
                }
                return;
            }

            // 호가 범위 밖이면 무시
            if (e.RowIndex >= Rows * 2)
                return;

            // ============================================================
            // 3) 기존 isBuy / isSell 거래 로직 그대로
            // ============================================================
            if (isBuy)
            {
                // 기존 주문 제거
                var existingOrder = StockExchange.buyOrders
                    .Find(o => o.Stock == clickedSymbol && o.Price == price);
                if (existingOrder != null)
                    StockExchange.buyOrders.Remove(existingOrder);

                int qty = g.일회거래액 * 10000 / price;
                if (qty <= 0) qty = 1;
                                                            
                SoundUtils.Sound("돈", g.일회거래액.ToString());


                string 매수이유 = BuildSignalText(data, data.Api);


                var kind = await QuickPopupHelper.ConfirmByQuickPopupAsync(
                    isBuy: true,
                    stock: clickedSymbol,
                    price: price,
                    qty: qty,
                    reason: 매수이유,
                    commitSource: "manual"
                );

                if (kind == PopupResultKind.Confirm)
                {
                    DealManager.DealExec("매수", clickedSymbol, price, qty, "01");
                }
                else if (kind == PopupResultKind.CancelRemove)
                {
                    g.StockManager.RemoveInterestedWithBid(clickedSymbol);
                }

                return;
            }
            else if (isSell)
            {
                // 기존 주문 제거
                var existingOrder = StockExchange.sellOrders
                    .Find(o => o.Stock == clickedSymbol && o.Price == price);
                if (existingOrder != null)
                    StockExchange.sellOrders.Remove(existingOrder);

                int qty = g.일회거래액 * 10000 / price;
                if (qty <= 0) qty = 1;

                // 보유량 체크/정리
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

                string 매도이유 = "떨어진다";

                //if (g.confirm_sell)
                //{
                //    // ✅ 여기 sideColor 버그 있었음: isBuy가 아니라 isSell/false로 넣어야 함
                //    var kind = await ConfirmByQuickPopupAsync(
                //        isBuy: false,
                //        stock: clickedSymbol,
                //        price: price,
                //        qty: qty,
                //        reason: 매도이유,
                //        commitSource: "manual"
                //    );
                //    if (kind == PopupResultKind.Confirm)
                //    {
                //        DealManager.DealExec("매도", clickedSymbol, price, qty, "01");
                //    }
                //    return;
                //}
                //else
                //{
                // 즉시 매도
                DealManager.DealExec("매도", clickedSymbol, price, qty, "01");
                return;
                //}
            }
        }

        public static string BuildSignalText(StockData data, ApiData api)
        {
            var sb = new System.Text.StringBuilder();

            if (data == null || api == null || api.x == null)
                return "";

            var post = data.Post;
            var x = api.x;

            int rowCount = Math.Min(api.nrow, x.GetLength(0));
            int colCount = x.GetLength(1);

            if (rowCount < 2 || colCount < 8)
                return "";

            if (!ChartLayoutUtils.TryGetDrawRange(data, out int start, out int end))
                return "";

            int lastRow = end - 1;

            if (lastRow >= rowCount)
                lastRow = rowCount - 1;

            if (lastRow <= 0)
                return "";

            // 가격차 (최근 최대 5개, 초기 row 부족 방어)
            int diffCount = Math.Min(5, lastRow);

            for (int i = 0; i < diffCount; i++)
            {
                int a = x[lastRow - i, 1];
                int b = x[lastRow - 1 - i, 1];
                int diff = a - b;
                sb.Append($"{diff,+5}");
            }

            sb.AppendLine().AppendLine();

            // 10/20/30초
            if (post != null)
            {
                AppendSecRow(sb, post.분10배수차, post.분10배수합,
                    SafeRatioPct(post.분10프로천, post.분10거래천));

                AppendSecRow(sb, post.분20배수차, post.분20배수합,
                    SafeRatioPct(post.분20프로천, post.분20거래천));

                AppendSecRow(sb, post.분30배수차, post.분30배수합,
                    SafeRatioPct(post.분30프로천, post.분30거래천));
            }

            // 1~4분전
            int minuteLen = Math.Min(
                Math.Min(api.분매수배?.Length ?? 0, api.분매도배?.Length ?? 0),
                Math.Min(api.분프로천?.Length ?? 0, api.분거래천?.Length ?? 0)
            );

            int maxMinute = Math.Min(4, minuteLen - 1);

            for (int i = 1; i <= maxMinute; i++)
            {
                int mulBuy = api.분매수배[i];
                int mulSell = api.분매도배[i];
                int mulDiff = mulBuy - mulSell;
                int mulSum = mulBuy + mulSell;
                int proPct = SafeRatioPct(api.분프로천[i], api.분거래천[i]);

                sb.Append($"{mulDiff,6}{mulSum,7}{proPct,6}");
                sb.AppendLine();
            }

            sb.AppendLine();

            // 누적
            int vol = ChartGeneral.CalcVolumePctInt(data, lastRow);
            double volDisp = vol / 100.0;

            int moneyCum = x[lastRow, 7];

            int proPctCum = SafeRatioPct(x[lastRow, 4], moneyCum);
            int forPctCum = SafeRatioPct(x[lastRow, 5], moneyCum);
            int instPctCum = SafeRatioPct(x[lastRow, 6], moneyCum);

            sb.Append($"{volDisp,6:0.##}{proPctCum,7}{forPctCum,7}{instPctCum,7}");

            return sb.ToString();
        }

       public static void AppendSecRow(StringBuilder sb, double diff, double sum, double proPct)
        {
            sb.Append($"{diff,6}{sum,7}{proPct,6}");
            sb.AppendLine();
        }
        static int SafeRatioPct(double part, double total)
        {
            if (total == 0)
                return 0;

            return (int)Math.Round(part * 100.0 / total);
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

            // ✅ UI 스레드 강제 (return 금지)
            if (_dataGridView.InvokeRequired)
            {
                _dataGridView.BeginInvoke((Action)FlushBookToUI_20Fps);
                return;
            }

            var now = DateTime.Now;

            // ===== 1) dirty 없으면 아무것도 안 함 =====
            bool needUpdate;
            lock (_bookDirtyLock)
            {
                needUpdate = _bookDirty;
                if (!needUpdate) return;
                _bookDirty = false;
            }

            // ===== 2) 20fps 제한 =====
            if ((now - _lastBookUiUpdate).TotalMilliseconds < BOOK_UI_INTERVAL_MS)
                return;

            _lastBookUiUpdate = now;

            // ===== Rows=5 고정: 총 12행 =====
            const int requiredRows = 12; // 2*5+2
            if (_dataTable.Rows.Count < requiredRows) return;

            // ===== 헤더 변환 헬퍼 =====
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
                // ✅ 더 단순/안전
                var cm = _dataGridView.BindingContext?[_dataTable] as CurrencyManager;

                cm?.SuspendBinding();
                _dataTable.BeginLoadData();

                try
                {
                    // ✅ (권장) COM/이벤트 꼬임 방지: 한 프레임을 lock으로 묶기
                    lock (_bookBidLock)
                    {
                        // ===== 3) (Rows=5) 호가표 DataTable 갱신 =====
                        _dataTable.Rows[4][1] = H(3);
                        _dataTable.Rows[5][1] = H(4);
                        _dataTable.Rows[4][0] = H(5);
                        _dataTable.Rows[5][2] = H(6);

                        _dataTable.Rows[3][1] = H(7);
                        _dataTable.Rows[6][1] = H(8);
                        _dataTable.Rows[3][0] = H(9);
                        _dataTable.Rows[6][2] = H(10);

                        _dataTable.Rows[2][1] = H(11);
                        _dataTable.Rows[7][1] = H(12);
                        _dataTable.Rows[2][0] = H(13);
                        _dataTable.Rows[7][2] = H(14);

                        _dataTable.Rows[1][1] = H(15);
                        _dataTable.Rows[8][1] = H(16);
                        _dataTable.Rows[1][0] = H(17);
                        _dataTable.Rows[8][2] = H(18);

                        _dataTable.Rows[0][1] = H(19);
                        _dataTable.Rows[9][1] = H(20);
                        _dataTable.Rows[0][0] = H(21);
                        _dataTable.Rows[9][2] = H(22);

                        _dataTable.Rows[10][0] = H(23);
                        _dataTable.Rows[10][2] = H(24);

                        // best ask/bid + qty
                        int.TryParse(H(3), out askPrice);
                        int.TryParse(H(4), out bidPrice);
                        int.TryParse(H(5), out askQty);
                        int.TryParse(H(6), out bidQty);
                    }

                    // ===== 4) 합계/표시 =====
                    long ask5Sum = 0, bid5Sum = 0;
                    for (int i = 0; i < 5; i++) ask5Sum += ParseLong(_dataTable.Rows[i][0]);
                    for (int i = 5; i < 10; i++) bid5Sum += ParseLong(_dataTable.Rows[i][2]);

                    long ask10Sum = ParseLong(_dataTable.Rows[10][0]);
                    long bid10Sum = ParseLong(_dataTable.Rows[10][2]);

                    double ask5_억 = ask5Sum * _moneyConverter_억원;
                    double ask10_억 = ask10Sum * _moneyConverter_억원;
                    double bid5_억 = bid5Sum * _moneyConverter_억원;
                    double bid10_억 = bid10Sum * _moneyConverter_억원;

                    string F(double v) => (v >= 10) ? v.ToString("0") : v.ToString("0.0");

                    _dataTable.Rows[10][0] = $"{F(ask5_억)}/{F(ask10_억)}";
                    _dataTable.Rows[10][2] = $"{F(bid5_억)}/{F(bid10_억)}";

                    // ===== 5) Extra(0.55s) =====
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
        }

        private void RequestQuote()
        {
            if (!g.StockRepo.TryGet(_stock, out var data))
                return;

            _jpbidSecondary = new DSCBO1Lib.StockJpbid2();

            if (_jpbidSecondary.GetDibStatus() == 1)
            {
                Trace.TraceInformation("DibRq 요청 수신대기 중 입니다. 수신이 완료된 후 다시 호출 하십시오.");
                return;
            }

            string stockcode = _stockCodeService.NameToCode(_stock);
            _jpbidSecondary.SetInputValue(0, stockcode);

            int result = _jpbidSecondary.BlockRequest();
            if (result != 0) return;

            _dataTable.Rows[2 * Rows][0] = int.Parse(_jpbidSecondary.GetHeaderValue(4).ToString()); // 매도잔량
            _dataTable.Rows[2 * Rows][2] = int.Parse(_jpbidSecondary.GetHeaderValue(6).ToString()); // 매수잔량

            if (Rows == 5)
            {
                int indexSell = 4;
                int indexBuy = 5;

                for (int i = 0; i < int.Parse(_jpbidSecondary.GetHeaderValue(1).ToString()) / 2; i++)
                {
                    _dataTable.Rows[indexSell][1] = _jpbidSecondary.GetDataValue(0, i).ToString();
                    _dataTable.Rows[indexBuy][1] = _jpbidSecondary.GetDataValue(1, i).ToString();

                    _dataTable.Rows[indexSell][0] = _jpbidSecondary.GetDataValue(2, i).ToString();
                    _dataTable.Rows[indexBuy][2] = _jpbidSecondary.GetDataValue(3, i).ToString();

                    indexSell--;
                    indexBuy++;
                }
            }
            else
            {
                int indexSell = 9;
                int indexBuy = 10;

                for (int i = 0; i < int.Parse(_jpbidSecondary.GetHeaderValue(1).ToString()); i++)
                {
                    _dataTable.Rows[indexSell][1] = _jpbidSecondary.GetDataValue(0, i).ToString();
                    _dataTable.Rows[indexBuy][1] = _jpbidSecondary.GetDataValue(1, i).ToString();

                    _dataTable.Rows[indexSell][0] = _jpbidSecondary.GetDataValue(2, i).ToString();
                    _dataTable.Rows[indexBuy][2] = _jpbidSecondary.GetDataValue(3, i).ToString();

                    indexSell--;
                    indexBuy++;
                }
            }

            int valUp = (int)_jpbidSecondary.GetDataValue(0, 0);
            int valDn = (int)_jpbidSecondary.GetDataValue(1, 0);

            if (g.StockManager.IndexList.Contains(_stock))
            {
                _dataTable.Rows[Rows - 2][2] = (MajorIndex.Instance.KospiIndex / 100.0).ToString("0.##");
                _dataTable.Rows[Rows - 1][2] = (MajorIndex.Instance.KosdaqIndex / 100.0).ToString("0.##");
            }
            else
            {
                _dataTable.Rows[Rows - 1][2] = (data.Post.프누천 / 10.0).ToString("0.##");
            }

            if (MathUtils.IsSafeToDivide(valDn))
            {
                _dataTable.Rows[2 * Rows]["호가"] = ((valUp - valDn) / (double)valDn * 100.0).ToString("0.##");
            }
            else
            {
                _dataTable.Rows[2 * Rows]["호가"] = "No Data";
            }

            if (data.Deal.보유량 > 0 && data.Api.매수1호가 > 0)
            {
                data.Deal.수익률 = (data.Api.매수1호가 - data.Deal.장부가) / (double)data.Api.매수1호가 * 100;
            }

            _dataTable.Rows[2 * Rows + 1][0] = data.Stock;
            _dataTable.Rows[2 * Rows + 1][1] = data.Statistics.일간변동평균편차;
            _dataTable.Rows[2 * Rows + 1][2] = data.Deal.보유량 + "/" + data.Deal.수익률.ToString("F2");

            RequestQuoteExtra_DataOnly(DateTime.Now); // RequestQuoteExtra();
        }

        private bool RequestQuoteExtra_DataOnly(DateTime now)
        {
            if (_dataTable == null || _dataGridView == null) return false;
            if (!g.StockRepo.TryGet(_stock, out var data)) return false;

            int required = 2 * Rows + 2; // Rows=5 => 12
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
            string[] col0Base = new string[Rows]; // row 5~9 col0 (틱수/틱도)
            for (int i = 0; i < Rows; i++)
            {
                int buy = (data.Api.틱수누량[i] - data.Api.틱수누량[i + 1]) / divider;
                int sell = (data.Api.틱도누량[i] - data.Api.틱도누량[i + 1]) / divider;
                col0Base[i] = buy + "/" + sell;
            }

            string[] col2Base = new string[Rows]; // row 0~4 col2 (프로+외인/거래)
            for (int i = 0; i < Rows; i++)
            {
                col2Base[i] = data.Api.틱프로천[i].ToString("F0") + "+" +
                              data.Api.틱외인천[i].ToString("F0") + "/" +
                              data.Api.틱거래천[i].ToString("F0");
            }

            // -----------------------------
            // 2) 호가/잔량(방어)
            // -----------------------------
            int ask1 = 0, bid1 = 0;
            int bestAskQty = 0, bestBidQty = 0;

            int.TryParse(Convert.ToString(_dataTable.Rows[Rows - 1][1]), out ask1);        // row4 col1
            int.TryParse(Convert.ToString(_dataTable.Rows[Rows][1]), out bid1);            // row5 col1
            int.TryParse(Convert.ToString(_dataTable.Rows[Rows - 1][0]), out bestAskQty);  // row4 col0
            int.TryParse(Convert.ToString(_dataTable.Rows[Rows][2]), out bestBidQty);      // row5 col2

            string gapText = (bid1 > 0)
                ? ((ask1 - bid1) / (double)bid1 * 100.0).ToString("0.##")
                : "No Data";

            // 수익률
            double profitRate = data.Deal.수익률;
            if (data.Deal.보유량 > 0 && bid1 > 0)
                profitRate = (bid1 - data.Deal.장부가) / (double)bid1 * 100.0;

            string row11c0 = data.Stock;
            string row11c1 = Convert.ToString(data.Statistics.일간변동평균편차) ?? "";
            string row11c2 = data.Deal.보유량 + "/" + profitRate.ToString("F2");

            // -----------------------------
            // 3) extra 텍스트 시그니처(최적화)
            //    ✅ 주문표시와 분리
            // -----------------------------
            string extraSig =
                string.Join("|", col0Base) + "::" +
                string.Join("|", col2Base) + "::" +
                gapText + "::" +
                row11c0 + "|" + row11c1 + "|" + row11c2;

            bool extraTextChanged = (extraSig != _lastExtraTextSig);
            if (extraTextChanged) _lastExtraTextSig = extraSig;

            if (extraTextChanged)
            {
                // col0 row5~9 (기본 틱수/틱도)
                for (int i = 0; i < Rows; i++)
                    _dataTable.Rows[i + Rows][0] = col0Base[i];

                // col2 row0~4 (기본 프로+외인/거래)
                for (int i = 0; i < Rows; i++)
                    _dataTable.Rows[i][2] = col2Base[i];

                // “푀누억/지수” 표시(네 로직 유지)
                if (_stock.Contains("KODEX"))
                {
                    _dataTable.Rows[Rows - 2][2] = (MajorIndex.Instance.KospiIndex / 100.0).ToString("0.##");
                    _dataTable.Rows[Rows - 1][2] = (MajorIndex.Instance.KosdaqIndex / 100.0).ToString("0.##");
                }
                else
                {
                    _dataTable.Rows[Rows - 1][2] = (data.Post.프누천 / 10.0).ToString("0.##");
                }

                _dataTable.Rows[2 * Rows][1] = gapText; // row10 col1

                _dataTable.Rows[2 * Rows + 1][0] = row11c0;
                _dataTable.Rows[2 * Rows + 1][1] = row11c1;
                _dataTable.Rows[2 * Rows + 1][2] = row11c2;
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

            if (data.Deal.보유량 > 0 && bid1 > 0)
                data.Deal.수익률 = profitRate;

            // -----------------------------
            // 5) Passing 사운드
            // -----------------------------
            if (data.Pass.upperPassingPrice > 0 && data.Api.매도1호가 >= data.Pass.upperPassingPrice)
            {
                Utils.SoundUtils.Sound("일반", "passing upper");
                data.Pass.upperPassingPrice = 0;
            }
            if (data.Pass.lowerPassingPrice > 0 && data.Api.매수1호가 <= data.Pass.lowerPassingPrice)
            {
                Utils.SoundUtils.Sound("일반", "passing lower");
                data.Pass.lowerPassingPrice = 0;
            }

            // -----------------------------
            // 6) _cellColors 계산 (매번)
            // -----------------------------
            for (int i = 0; i < _cellColors.GetLength(0); i++)
            {
                for (int j = 0; j < _cellColors.GetLength(1); j++)
                {
                    _cellColors[i, j] = Color.Empty;
                }
            }

            _cellColors[Rows - 1, 2] = Color.LightGreen;
            if (g.StockManager.IndexList.Contains(_stock))
                _cellColors[Rows - 2, 2] = Color.LightGreen;

            if (!g.confirm_sell)
                for (int i = 0; i < Rows; i++)
                    _cellColors[i, 0] = Color.Cyan;

            if (g.optimumTrading)
            {
                for (int i = Rows; i < 2 * Rows; i++)
                    _cellColors[i, 0] = Color.Yellow;

                int lim = g.StockManager.IndexList.Contains(_stock) ? Rows - 2 : Rows - 1;
                for (int i = 0; i < lim; i++)
                    _cellColors[i, 2] = Color.Yellow;
            }

            if (data.Pass.upperPassingPrice > 0 || data.Pass.lowerPassingPrice > 0)
            {
                for (int i = 0; i < 2 * Rows; i++)
                {
                    if (int.TryParse(Convert.ToString(_dataTable.Rows[i][1]), out int price))
                    {
                        if (price == data.Pass.upperPassingPrice || price == data.Pass.lowerPassingPrice)
                            _cellColors[i, 1] = Color.Yellow;
                    }
                }
            }

            // -----------------------------
            // 7) ✅ 주문표시 "잔상 제거" + 주문 찍기
            //    - row0~9 범위에서 col0/col2를 먼저 기본값으로 초기화
            //    - 주문은 그 위에 덮어쓰기
            // -----------------------------
            var ordersWithStock = StockExchange.buyOrders
                .Where(o => o.Stock == _stock)
                .Concat(StockExchange.sellOrders.Where(o => o.Stock == _stock))
                .ToList();

            // 주문 시그니처(변화없으면 굳이 다시 안해도 됨) - 하지만 안전하게 써두자
            string ordersSig = string.Join("|",
                ordersWithStock
                    .OrderBy(o => o.Price)
                    .Select(o => $"{o.Stock}:{o.Price}:{o.Quantity}:{(StockExchange.buyOrders.Contains(o) ? "B" : "S")}"));

            bool ordersChanged = (ordersSig != _lastOrdersSig);
            if (ordersChanged) _lastOrdersSig = ordersSig;

            // ✅ 안정 우선: 주문표시는 매번 해도 됨 (창 2~5개라 부담 거의 없음)
            //    그래도 최적화하려면: if (ordersChanged) { ... } 로 감싸면 됨
            {
                // 7-1) row0~4 col2는 기본 텍스트로 복구 (프로+외인/거래 or 지수/푀누억 포함)
                //      - extraTextChanged가 false인 경우에도 주문 잔상 제거 위해 필요
                for (int i = 0; i < Rows; i++)
                {
                    // 기본값은 “현재 DataTable 값”이 아니라, 우리가 계산한 base로 되돌리는 게 안전
                    _dataTable.Rows[i][2] = col2Base[i];
                }
                // KODEX/푀누억은 기존처럼 덮어
                if (_stock.Contains("KODEX"))
                {
                    _dataTable.Rows[Rows - 2][2] = (MajorIndex.Instance.KospiIndex / 100.0).ToString("0.##");
                    _dataTable.Rows[Rows - 1][2] = (MajorIndex.Instance.KosdaqIndex / 100.0).ToString("0.##");
                }
                else
                {
                    _dataTable.Rows[Rows - 1][2] = (data.Post.프누천 / 10.0).ToString("0.##");
                }

                // 7-2) row5~9 col0도 기본 틱수/틱도로 복구
                for (int i = 0; i < Rows; i++)
                {
                    _dataTable.Rows[i + Rows][0] = col0Base[i];
                }

                // 7-3) 이제 주문만 덮어쓰기
                foreach (var o in ordersWithStock)
                {
                    for (int i = 0; i < 2 * Rows; i++)
                    {
                        if (int.TryParse(Convert.ToString(_dataTable.Rows[i][1]), out int price) && price == o.Price)
                        {
                            if (StockExchange.buyOrders.Contains(o))
                            {
                                _dataTable.Rows[i][2] = o.Quantity.ToString();
                                _cellColors[i, 2] = Color.Red;
                            }
                            else
                            {
                                _dataTable.Rows[i][0] = o.Quantity.ToString();
                                _cellColors[i, 0] = Color.Red;
                            }
                        }
                    }
                }
            }

            return true; // 스타일 적용은 ApplyExtraStyles에서
        }

        // ✅ UI Style만 적용 (DataTable/Api 건드리지 않음)
        private void ApplyExtraStyles()
        {
            if (_dataGridView == null) return;
            if (_cellColors == null) return;

            int rowLimit = Math.Min(2 * Rows, _dataGridView.Rows.Count); // 10
            int colLimit = Math.Min(3, _dataGridView.Columns.Count);     // 3

            for (int i = 0; i < rowLimit; i++)
            {
                for (int j = 0; j < colLimit; j++)
                {
                    var c = _cellColors[i, j];
                    _dataGridView.Rows[i].Cells[j].Style.BackColor = (c == Color.Empty) ? Color.White : c;
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

        #region BookBid Utilities
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
        #endregion
    }
}
