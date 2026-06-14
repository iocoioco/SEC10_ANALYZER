using CPTRADELib;
using New_Tradegy.Library.Core;
using New_Tradegy.Library.Models;
using New_Tradegy.Library.Trackers;
using New_Tradegy.Library.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;

namespace New_Tradegy.Library.Deals
{
    public class DealManager
    {
        private static CPUTILLib.CpStockCode _cpstockcode = new CPUTILLib.CpStockCode();
        private static readonly CPUTILLib.CpCodeMgr _cpcodemgr = new CPUTILLib.CpCodeMgr();
        public static CPUTILLib.CpCybos _cpcybos;
        private static CPTRADELib.CpTdUtil _cptdutil;
        private CPTRADELib.CpTd0303 _cptd0303; // 주식 주문관리 (매매 유형 정정 주문) 데이터를 요청
        private static CPTRADELib.CpTd0311 _cptd0311; // 주문(현금 주문) 데이터를 요청
        private readonly CPTRADELib.CpTd0312 _cptd0312; // 신용 주문 데이터를 요청
        private readonly CPTRADELib.CpTd0313 _cptd0313; // 주문관리 (가격 정정 주문) 데이터를 요청
        private readonly CPTRADELib.CpTd0732 _cptd0732; // 주식 결제예정 예수금 가계산 데이터를 요청
        private static CPTRADELib.CpTd0314 _cptd0314; // 주문관리 (취소 주문) 데이터를 요청

        private static CPTRADELib.CpTdNew5331A _cptdnew5331a; //계좌별 매수주문 가능 금액/수량 데이터를 요청;

        private readonly CPTRADELib.CpTdNew5331B _cptdnew5331b; // 계좌별 매도주문 가능 수량 데이터를 요청
        private static CPTRADELib.CpTd5339 _cptd5339; // 계좌별 미체가장 잔량 데이터를 요청
        private readonly CPTRADELib.CpTd5341 _cptd5341; // 금일 계좌별 주문/체가 내역 조회 데이터를 요청폗
        private static CPTRADELib.CpTd6033 _cptd6033; // 계좌별 잔고 및 주문체가 평가 현황 데이터를 요청
        private static CPTRADELib.CpTd5342 _profitQuery;
        private static bool _checkedTradeInit = false;

        public static string accountNo;
        public static string accountGoodsStock;
        public static string[] arrAccountGoodsStock;

        public static void TradeInit()
        {
            if (_checkedTradeInit || !g.connected)
                return;

            if (_cptdutil == null)
            {
                _cptdutil = new CpTdUtil();
            }

            int rv = _cptdutil.TradeInit(0);
            if (rv == 0)
            {
                _checkedTradeInit = true;

                string[] arrAccount = (string[])_cptdutil.AccountNumber;
                if (arrAccount.Length > 0)
                {
                    accountNo = arrAccount[0];

                    arrAccountGoodsStock = (string[])_cptdutil.get_GoodsList(accountNo, CPE_ACC_GOODS.CPC_STOCK_ACC); // 주식
                    if (arrAccountGoodsStock.Length > 0)
                        accountGoodsStock = arrAccountGoodsStock[0];
                }
            }
            else if (rv == -1)
            {
                MessageBox.Show("계좌 비밀로 오류 포함 에러.");
                _checkedTradeInit = false;
            }
            else if (rv == 1)
            {
                MessageBox.Show("OTP/보안카드키가 잘못되었습니다.");
                _checkedTradeInit = false;
            }
            else
            {
                MessageBox.Show("Error");
                _checkedTradeInit = false;
            }
        }

        public static void DealProfit()
        {
            TradeInit();
            if (!_checkedTradeInit)
                return;

            _profitQuery = new CpTd5342();
            if (_profitQuery.GetDibStatus() == 1)
            {
                Trace.TraceInformation("DibRq 요청 수신대기 중 입니다. 수신이 완료된 후 다시 호출 하십시오.");
                return;
            }

            _profitQuery.SetInputValue(0, g.Account);
            _profitQuery.SetInputValue(1, "01"); // 상품관리구분코드
            _profitQuery.SetInputValue(2, 20);   // 요청개수 - 최대 20개
            _profitQuery.SetInputValue(3, "1");  // "1": 금일, "2": 전일
            _profitQuery.SetInputValue(4, "");   // 모든 종목

            int result = _profitQuery.BlockRequest();

            if (result != 0)
                return;

            long sellAmt = (long)_profitQuery.GetHeaderValue(9); // 매도정산금합
            long buyAmt = (long)_profitQuery.GetHeaderValue(10); // 매수정산금합

            g.DealProfit = (int)((sellAmt - buyAmt) / 10000); // 만원 단위

            if (g.MarketeyeCount == 0)
                g.DealProfit = 0;

            if (g.controlPane.GetCellValue(1, 0) != g.DealProfit.ToString())
                g.controlPane.SetCellValue(1, 0, g.DealProfit.ToString());
        }

        // Sensei 20250426
        public static void DealDeposit()
        {
            if (!g.connected)
                return;

            TradeInit();
            if (_checkedTradeInit == false)
                return;

            _cptdnew5331a = new CpTdNew5331A();

            if (_cptdnew5331a.GetDibStatus() == 1)
            {
                Trace.TraceInformation("DibRq 요청 수신대기 중 입니다. 수신이 완료된 후 다시 호출 하십시오.");
                return;
            }

            _cptdnew5331a.SetInputValue(0, g.Account);  // 계좌번호
            //_cptdnew5331a.SetInputValue(2, ""); // 종목코드
                                                //_cptdnew5331a.SetInputValue(3, "01"); // 주문호가구분
            _cptdnew5331a.SetInputValue(1, "01");       // 상품관리구분코드 : 거래소
            _cptdnew5331a.SetInputValue(5, "Y");        // Y: Deposit 100 기준
            _cptdnew5331a.SetInputValue(6, '1');         // '1': 금액조회

            int result = _cptdnew5331a.BlockRequest();

            string message = _cptdnew5331a.GetDibMsg1();
        
            try
            {
                g.예치금 = (int)Convert.ToInt64(_cptdnew5331a.GetHeaderValue(10) / 10000);
            }
            catch
            {
                Trace.TraceWarning("GetHeaderValue(10) parsing failed.");
                return;
            }

            //if (g.controlPane.GetCellValue(0, 3) != g.예치금.ToString())
            //{
            //    g.controlPane.SetCellValue(0, 3, g.예치금.ToString());
            //}
        }


        public static void DealProcessing() // tr(1)
        {
            if (!g.connected)
                return;

            TradeInit();
            if (!_checkedTradeInit)
                return;

            _cptd5339 = new CPTRADELib.CpTd5339();

            // Setup request
            _cptd5339.SetInputValue(0, g.Account); // 계좌번호
            _cptd5339.SetInputValue(1, "01");    // 상품관리구분코드: 거래소주식
            _cptd5339.SetInputValue(4, "0");     // 전체
            _cptd5339.SetInputValue(5, "0");     // 순수
            _cptd5339.SetInputValue(7, 20);       // 최대 20개 요청

            int result = _cptd5339.BlockRequest();
            string textBox = _cptd5339.GetDibMsg1();

            if (result != 0)
                return;

            // Retrieve header data
            string accountNumber = (string)_cptd5339.GetHeaderValue(0);
            string accountName = (string)_cptd5339.GetHeaderValue(4);
            int recordCount = (int)_cptd5339.GetHeaderValue(5);

            // Parse each record
            for (int i = 0; i < recordCount; i++)
            {
                var item = new OrderItem
                {
                    stock = (string)_cptd5339.GetDataValue(4, i),
                    m_ordKey = (int)_cptd5339.GetDataValue(1, i),
                    m_ordOrgKey = (int)_cptd5339.GetDataValue(2, i),
                    m_sCode = _cpstockcode.NameToCode((string)_cptd5339.GetDataValue(4, i)),
                    m_nAmt = (int)_cptd5339.GetDataValue(6, i),
                    m_nPrice = (int)_cptd5339.GetDataValue(7, i),
                    m_nContAmt = (int)_cptd5339.GetDataValue(8, i),
                    m_nModAmt = (int)_cptd5339.GetDataValue(11, i),
                    m_sHogaFlag = _cptd5339.GetDataValue(21, i)
                };

                string bsCode = (string)_cptd5339.GetDataValue(13, i);
                item.buyorSell = bsCode == "1" ? "매도" : bsCode == "2" ? "매수" : "";

                OrderItemTracker.Add(item);
            }
        }


        public static bool CheckPreviousLoss(string stock)
        {
            var data = g.StockRepo.TryGetDataOrNull(stock);
            if (data == null) return false; // Stock not found

            // Ensure valid purchase price exists and at least 1 share is held
            if (data.Api.매수1호가 > 0 && data.Deal.보유량 >= 1)
            {
                double 수익률 = (double)(data.Api.매수1호가 - data.Deal.장부가) / data.Api.매수1호가 * 100;

                if (수익률 < -0.45 && data.Deal.평가금액 > 4_500_000)
                {
                    Utils.SoundUtils.Sound("alarm", "lost already");
                    
                }
            }
            return false;
        }

        public static bool DealHold()
        {
            if (!g.connected)
                return false;

            TradeInit();
            if (_checkedTradeInit == false)
                return false;

            var before = new HashSet<string>(g.StockManager.HoldingList);

            _cptd6033 = new CPTRADELib.CpTd6033();

            if (_cptd6033.GetDibStatus() == 1)
            {
                Trace.TraceInformation("DibRq 요청 수신대기 중 입니다. 수신이 완료된 후 다시 호출 하십시오.");
                return false;
            }

            _cptd6033.SetInputValue(0, g.Account);  // 계좌번호
            _cptd6033.SetInputValue(1, "01");       // 상품관리구분코드
            _cptd6033.SetInputValue(2, 50);         // 요청건수 (최대 50)

            int result = _cptd6033.BlockRequest();

            if (result != 0)
                return false;

            int count = (int)_cptd6033.GetHeaderValue(7); // 보유종목 수
            if (count > 51) return false;

            g.StockManager.HoldingList.Clear();

            // Reset all stocks' 보유량
            foreach (var data in g.StockRepo.AllDatas
                .Where(d => d.Kind == StockData.InstrumentKind.Stock
                  || d.Kind == StockData.InstrumentKind.Index))

            {
                data.Deal.보유량 = 0;
            }

            for (int i = count - 1; i >= 0; i--)
            {
                string stock = (string)_cptd6033.GetDataValue(0, i);

                if (!g.StockRepo.Contains(stock))
                    continue;

                var data = g.StockRepo.TryGetDataOrNull(stock);
                if (data == null) continue;
                var api = data.Api;
                var deal = data.Deal;

                deal.보유량 = (int)_cptd6033.GetDataValue(15, i);
                deal.장부가 = (double)_cptd6033.GetDataValue(17, i);
                deal.평가금액 = (long)_cptd6033.GetDataValue(9, i);
                deal.손익단가 = (long)_cptd6033.GetDataValue(18, i);

                if (api.매수1호가 > 0)
                {
                    deal.수익률 = (api.매수1호가 - deal.장부가) / api.매수1호가 * 100;
                }

                g.StockManager.HoldingList.Add(stock);
            }

            // Remove 보유종목 from 호가종목
            foreach (string code in g.StockManager.HoldingList)
            {
                g.StockManager.InterestedWithBidList.Remove(code);
            }

            DealHoldReorder();

            var after = new HashSet<string>(g.StockManager.HoldingList);
            return after.Any(x => !before.Contains(x));
        }

        public static void DealHoldReorder()
        {
            var stocksTuple = new List<Tuple<long, string>>();

            var repo = g.StockRepo;
            foreach (var stock in g.StockManager.HoldingList)
            {
                if (!repo.Contains(stock))
                    continue;

                var data = repo.TryGetDataOrNull(stock);
                if (data == null) continue;
                long holdingValue = data.Deal.보유량 * data.Api.현재가;

                stocksTuple.Add(Tuple.Create(holdingValue, stock));
            }

            // Sort descending by holding value
            var sorted = stocksTuple.OrderByDescending(t => t.Item1).ToList();

            g.StockManager.HoldingList.Clear();
            foreach (var item in sorted)
            {
                g.StockManager.HoldingList.Add(item.Item2);
            }
        }

        public static void CommitDeal(
            string side,
            string stock,
            (int price, int qty) result,
            string sourceTag)
        {


            DealManager.DealExec(
                side,
                stock,
                result.price,
                result.qty,
                "01");
        }

        public static void DealExec(string buyOrSell, string stockName, long price, long quantity, string orderType) // tr(1)
        {
            const string orderCondition = "0"; // 주문조건구분: "0" 없음, "1" IOC, "2" FOK

            TradeInit();
            if (!_checkedTradeInit || quantity == 0)
                return;
             
            // -------------------------------
            // 매수 차단 조건
            // -------------------------------
            //if (buyOrSell == "매수")
            //{
                DateTime now = DateTime.Now;

                // 1) 10:00 이후 신규 매수 금지
                //if (now.TimeOfDay >= new TimeSpan(15, 20, 0))
                //{
                //    Trace.TraceInformation($"[매수차단] 10시 이후 매수 금지: {stockName} {now:HH:mm:ss}");
                //    return;
                //}

                // 2) 손실 한도 초과 시 신규 매수 금지
                // 예: 50 = 50만원 손실 시 차단
                //int lossLimitManwon = 50;   // 전역변수로 두기 권장
                //if (g.DealProfit <= -lossLimitManwon)
                //{
                //    Trace.TraceInformation($"[매수차단] 손실한도 초과: {stockName} 손익={g.DealProfit}만원 한도={lossLimitManwon}만원");
                //    return;
                //}
            //}

            string code = _cpstockcode.NameToCode(stockName); // 종목 이름 → 코드 변환
            if (string.IsNullOrEmpty(code))
                return;

            _cptd0311 = new CPTRADELib.CpTd0311();

            if (_cptd0311.GetDibStatus() == 1)
            {
                Trace.TraceInformation("DibRq 요청 수신대기 중 입니다. 수신이 완료된 후 다시 호출 하십시오.");
                return;
            }

            // 매수: 2, 매도: 1
            string buyOrSellCode = (buyOrSell == "매수") ? "2" : "1";

            _cptd0311.SetInputValue(0, buyOrSellCode);    // 주문유형
            _cptd0311.SetInputValue(1, g.Account);        // 계좌번호
            _cptd0311.SetInputValue(2, "01");             // 상품구분코드
            _cptd0311.SetInputValue(3, code);             // 종목코드
            _cptd0311.SetInputValue(4, quantity);         // 주문수량
            _cptd0311.SetInputValue(5, price);            // 주문단가
            _cptd0311.SetInputValue(7, orderCondition);   // 주문조건
            _cptd0311.SetInputValue(8, orderType);        // 주문호가구분

            int result = _cptd0311.BlockRequest();
        }
        

        public static void DealCancelRowIndex(int rowindex)
        {
            var data = OrderItemTracker.GetOrderByRowIndex(rowindex);
            if (data == null)
                return;

            DealCancelOrder(data);
        }


        public static void DealCancelOrder(OrderItem data)
        {
            if (!g.connected)
                return;

            TradeInit();
            if (_checkedTradeInit == false)
            {
                MessageBox.Show("Trade initialization failed.", "Error", MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            int 원주문번호 = data.m_ordKey;
            string stockcode = _cpstockcode.NameToCode(data.stock);

            if (string.IsNullOrEmpty(stockcode))
            {
                MessageBox.Show("Invalid stock code.", "Error", MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            _cptd0314 = new CPTRADELib.CpTd0314();

            _cptd0314.SetInputValue(1, 원주문번호);     // 원주문번호
            _cptd0314.SetInputValue(2, g.Account);     // 계좌번호
            _cptd0314.SetInputValue(3, "01");          // 상품관리구분코드
            _cptd0314.SetInputValue(4, stockcode);     // 종목코드
            _cptd0314.SetInputValue(5, 0);             // 0: 전체 취소

            int result = _cptd0314.BlockRequest();
            if (result != 0)
            {
                MessageBox.Show($"Order cancellation failed with result code: {result}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        public static void DealCancelStock(string stock) // tr(1)
        {
            if (!g.connected)
                return;

            TradeInit();
            if (_checkedTradeInit == false)
                return;

            string stockCode = _cpstockcode.NameToCode(stock);
            if (string.IsNullOrEmpty(stockCode))
                return;

            var orders = OrderItemTracker.OrderMap.Values
                .Where(o => o.stock == stock)
                .ToList(); // ← this makes 'orders' a List<T>

            if (orders.Count() == 0)
                return;

            foreach (var data in orders)
            {
                int 원주문번호 = data.m_ordOrgKey;

                _cptd0314 = new CPTRADELib.CpTd0314();
                _cptd0314.SetInputValue(1, 원주문번호);     // 원주문번호
                _cptd0314.SetInputValue(2, g.Account);      // 계좌번호
                _cptd0314.SetInputValue(3, "01");           // 상품관리구분코드
                _cptd0314.SetInputValue(4, stockCode);      // 종목코드
                _cptd0314.SetInputValue(5, 0);              // 0: 전체 취소

                int result = _cptd0314.BlockRequest();
                if (result != 0)
                {
                    Trace.TraceWarning($"[Cancel Failed] {stock}, OrderKey: {원주문번호}, result: {result}");
                }
            }
        }

        private string DealCorrect(long 원주문번호, string stock, long 주문수량, long 주문단가) // tr(1)
        {
            if (!g.connected)
                return "";

            TradeInit();
            if (!_checkedTradeInit)
                return "";

            string stockCode = _cpstockcode.NameToCode(stock);
            if (string.IsNullOrEmpty(stockCode))
                return "Invalid stock code.";

            _cptd0303 = new CPTRADELib.CpTd0303();

            // 정정주문 입력
            _cptd0303.SetInputValue(1, 원주문번호);       // 원주문번호
            _cptd0303.SetInputValue(2, g.Account);        // 계좌번호
            _cptd0303.SetInputValue(3, "1");              // 상품관리구분코드
            _cptd0303.SetInputValue(4, stockCode);        // 종목코드
            _cptd0303.SetInputValue(5, 주문수량);         // 정정 수량
            _cptd0303.SetInputValue(6, 주문단가);         // 정정 단가

            int result = _cptd0303.BlockRequest();

            if (result != 0)
            {
                Trace.TraceWarning($"[정정실패] {stock} / 수량: {주문수량}, 단가: {주문단가}, 원주문번호: {원주문번호}");
            }

            return _cptd0303.GetDibMsg1();
        }

    }
}
