using CPTRADELib;
using glbl;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using work;
using misc;
using CPUTILLib;

namespace deal
{
    public class dl
    {
        private static CPUTILLib.CpStockCode _cpstockcode = new CPUTILLib.CpStockCode();
        private static readonly CPUTILLib.CpCodeMgr _cpcodemgr = new CPUTILLib.CpCodeMgr();
        public static CPUTILLib.CpCybos _cpcybos;
        private static CPTRADELib.CpTdUtil _cptdutil;
        private CPTRADELib.CpTd0303 _cptd0303; //주식 주문관리 (매매 유형 정정 주문) 데이터를 요청
        private static CPTRADELib.CpTd0311 _cptd0311; //주문(현금 주문) 데이터를 요청
        private readonly CPTRADELib.CpTd0312 _cptd0312; //신용 주문 데이터를 요청
        private readonly CPTRADELib.CpTd0313 _cptd0313; //주문관리 (가격 정정 주문) 데이터를 요청
        private readonly CPTRADELib.CpTd0732 _cptd0732; //주식 결제예정 예수금 가계산 데이터를 요청
        private static CPTRADELib.CpTd0314 _cptd0314; // 주문관리 (취소 주문) 데이터를 요청
        private static CPTRADELib.CpTdNew5331A _cptdnew5331a; //계좌별 매수주문 가능 금액/수량 데이터를 요청
        private readonly CPTRADELib.CpTdNew5331B _cptdnew5331b; //계좌별 매도주문 가능 수량 데이터를 요청
        private static CPTRADELib.CpTd5339 _cptd5339; //계좌별 미체결 잔량 데이터를 요청
        private readonly CPTRADELib.CpTd5341 _cptd5341; //금일 계좌별 주문/체결 내역 조회 데이터를 요청ㅍ
        private static CPTRADELib.CpTd6033 _cptd6033; //계좌별 잔고 및 주문체결 평가 현황 데이터를 요청

        private static bool _checkedTradeInit = false;

        public static string accountNo;
        public static string accountGoodsStock;
        public static string[] arrAccountGoodsStock;



        private static DSCBO1Lib.StockJpbid _stockjpbid1;
        private static DSCBO1Lib.StockJpbid _stockjpbid3;
        private static DSCBO1Lib.StockJpbid _stockjpbid4;
        private static DataTable dtb1;
        private static DataTable dtb3;
        private static DataTable dtb4;
        private static int index1;
        private static int index3;
        private static int index4;

        private static DSCBO1Lib.StockJpbid2 _stockjpbid2;
        private static DataTable dtb2;

        // 초기 Account 설정
        public static string deal_setting()
        {
            if (g.testing == true)
                return "";

            _cptdutil = new CpTdUtil();

            if (_checkedTradeInit)
            {
                return "설정완료";
            }

            int rv = _cptdutil.TradeInit(0);

            if (rv == 0)
            {
                _checkedTradeInit = true;

                string[] arrAccount = (string[])_cptdutil.AccountNumber;
                if (arrAccount.Length > 0)
                {
                    accountNo = arrAccount[0];

                    arrAccountGoodsStock = (string[])_cptdutil.get_GoodsList(accountNo, CPE_ACC_GOODS.CPC_STOCK_ACC);  // 주식
                    if (arrAccountGoodsStock.Length > 0)
                    {
                        accountGoodsStock = arrAccountGoodsStock[0];
                    }
                }
            }
            else if (rv == -1)
            {
                _checkedTradeInit = false;
                return "계좌 비밀번호 오류 포함 에러.";
            }
            else if (rv == 1)
            {
                _checkedTradeInit = false;
                return "OTP/보안카드키가 잘못되었습니다.";
            }
            else
            {
                _checkedTradeInit = false;
                return "Error";
            }
            return "설정완료";
        }


        //매수주문 가능 금액 :  _cptdnew5331a
        public static void deal_deposit() // tr(1)
        {
            if (g.testing == true)
                return;

            _cptdnew5331a = new CpTdNew5331A();

            int result = 1;

            if (_cptdnew5331a.GetDibStatus() == 1)
            {
                Trace.TraceInformation("DibRq 요청 수신대기 중 입니다. 수신이 완료된 후 다시 호출 하십시오.");
                return;
            }

            _cptdnew5331a.SetInputValue(0, g.Account);// 계좌번호
            _cptdnew5331a.SetInputValue(1, "01"); // 상품관리구분코드 : 거래소
                                                  //_cptdnew5331a.SetInputValue(2, ""); // 종목코드
                                                  //_cptdnew5331a.SetInputValue(3, "01"); // 주문호가구분코드, "01" : 보통 "02" 임의, "03" 시장가
                                                  //_cptdnew5331a.SetInputValue(4, 0); // 주문단가, default : 0(long)
            _cptdnew5331a.SetInputValue(5, "Y");  // N 계좌별deposit, Y deposit 100
            _cptdnew5331a.SetInputValue(6, '1'); // '1' 금액조회, '2' 수량조회

            result = _cptdnew5331a.BlockRequest();

            string textBox = _cptdnew5331a.GetDibMsg1();

            g.deposit = (int)(Convert.ToInt64(_cptdnew5331a.GetHeaderValue(10)) / 10000); // 현금주문가능금액 
        }


        //계좌별 미체결 잔량 데이터를 요청하고 수신 : _cptd5339
        public static void deal_processing() // tr(1)
        {
            if (g.testing == true)
                return;

            _cptd5339 = new CPTRADELib.CpTd5339();

            string textBox = "";
            int result = 1;

            //if (_cptdnew5331a.GetDibStatus() == 1)
            //{
            //	Trace.TraceInformation("DibRq 요청 수신대기 중 입니다. 수신이 완료된 후 다시 호출 하십시오.");
            //	return;
            //}

            _cptd5339.SetInputValue(0, g.Account); // 계좌번호
            _cptd5339.SetInputValue(1, "01");    // 상품관리구분코드 : 거래소주식
                                                 //_cptd5339.SetInputValue(3, "A006800"); // 생략하면 전체 조회
            _cptd5339.SetInputValue(4, "0"); // 전체
            _cptd5339.SetInputValue(5, "0"); // 순차
            _cptd5339.SetInputValue(7, 20); // 최대

            result = _cptd5339.BlockRequest();

            textBox = _cptd5339.GetDibMsg1();

            // 미체결 (매수, 매도 포함)
            string 계좌번호 = (string)_cptd5339.GetHeaderValue(0); // 계좌번호 안 나오네 iklee217에서는 나오는 데 
            string 계좌명 = (string)_cptd5339.GetHeaderValue(4); // 계좌명
            int count = (int)_cptd5339.GetHeaderValue(5); // 획득 데이터 숫자 

            //삭제 ? 왜 썼는 지 기억이 안 나네
            g.체결진행.Clear();

            if (result == 0)
            {
                //오랜 된 것부터 순차적으로 표시
                for (int i = 0; i < int.Parse(_cptd5339.GetHeaderValue(5).ToString()); i++)
                {
                    //string 상품관리구분 = (string)_cptd5339.GetDataValue(0, i); // 상품관리구분코드
                    int 주문번호 = (int)_cptd5339.GetDataValue(1, i); // 주문번호
                    int 원주문번호 = (int)_cptd5339.GetDataValue(2, i); // 원주문번호 : 0으로 나와서 혼란스러움
                                                                   //string 종목코드 = (string)_cptd5339.GetDataValue(3, i); // 종목코드
                    string 종목 = (string)_cptd5339.GetDataValue(4, i); // 종목명
                    string 주문구분 = (string)_cptd5339.GetDataValue(5, i); // 주문구분내용 : 다음 라인 설명
                                                                        //"현금매도" "현금매수" "신용매도" "신용매수" ...
                    int 주문수량 = (int)_cptd5339.GetDataValue(6, i); // 주문수량
                    int 주문단가 = (int)_cptd5339.GetDataValue(7, i); // 주문단가
                    int 체결수량 = (int)_cptd5339.GetDataValue(8, i); // 체결수량
                                                                  //string 계좌번호 =(string) _cptd5339.GetDataValue(10, i); // 계좌번호
                    string 매매구분 = (string)_cptd5339.GetDataValue(13, i);// 매수(2), 매도(1) 시장구분 

                    if (주문구분.Contains("매수"))
                    {
                        주문구분 = "매수";
                    }
                    else if (주문구분.Contains("매도"))
                    {
                        주문구분 = "매도";
                    }
                    else
                    {
                        MessageBox.Show("체결과정 체크 중 문제 발생");
                        return;
                    }

                    string t = 종목 + "\t" + 주문번호.ToString() + "\t" + 주문구분 + "\t" + 주문단가.ToString() + "\t" + 주문수량 + "\t" + 체결수량;
                    g.체결진행.Add(t);
                }
                if (count > 0)
                {
                    dtb2.Rows.Add();
                }
            }
        }


        // cancel both selling and buying trade 
        public static void deal_cancel_if_processing(string stock, DataGridView dgv2) // tr(2)
        {
            deal_processing(); // tr(1)
            
                foreach (var line in g.체결진행)
                {
                    string[] words = line.Split('\t');
                    if (words[0] == stock)
                    {
                        deal_cancel(stock, Convert.ToInt32(words[1]), dgv2); // tr(1)
                    }
                }
        }


        public static void deal_hold() // tr(1)
        {
            if (g.testing == true)
                return;

            _cptd6033 = new CPTRADELib.CpTd6033();

            if (_cptd6033.GetDibStatus() == 1)
            {
                Trace.TraceInformation("DibRq 요청 수신대기 중 입니다. 수신이 완료된 후 다시 호출 하십시오.");
                return;
            }

            int result = 1;

            _cptd6033.SetInputValue(0, g.Account); // 계좌번호
            _cptd6033.SetInputValue(1, "01");    // 상품관리구분코드
            _cptd6033.SetInputValue(2, 50);    // 요청건수(최대: 50[default : 14]) // blocked on 20210110 due to trouble

            result = _cptd6033.BlockRequest(); // 축 개체 에러 발생 20200716


            //List<string> temp_list = new List<string>();
            if (result == 0)
            {
                int count = (int)_cptd6033.GetHeaderValue(7); // 보유종목 수, 최대 50개 가능
                if (count > 51) // 51개 이상이면 아상 발생
                    return;

                g.보유종목.Clear();
                foreach (g.stock t in g.ogl_data)
                {
                    t.보유량 = 0;
                }

                for (int i = count - 1; i >= 0; i--)
                {
                    string 종목 = (string)_cptd6033.GetDataValue(0, i);

                    int index = wk.return_index_of_ogldata(종목);
                    if (index < 0)
                        continue;
                    g.stock o = g.ogl_data[index];

                    bool show = true;
                    if (o.보유량 * o.현재가 <= 500000) // 50만원 이하는 감추기
                    {
                        if (!g.show_small_amount)
                            show = false;
                    }

                    if (index >= 0 && show)
                    {
                        o.보유량 = (int)_cptd6033.GetDataValue(15, i);
                        o.수익률 = (double)_cptd6033.GetDataValue(11, i);
                        o.평가금액 = (long)_cptd6033.GetDataValue(9, i);
                        o.손익단가 = (long)_cptd6033.GetDataValue(18, i);
                        g.보유종목.Add(종목); // 20201104
                    }
                }

                // 보유종목에 있고 관심종목에 있으면 관심종목에서 제외
                foreach (string stock in g.보유종목)
                {
                    if (g.관심종목.Contains(stock))
                        g.관심종목.Remove(stock);
                }
                deal_hold_order();
            }
        }
        public static void deal_hold_order()
        {
            var 종목 = new List<Tuple<int, string>> { };

            foreach (var stock in g.보유종목)
            {
                int index = wk.return_index_of_ogldata(stock);
                if (index < 0)
                    return;
                g.stock o = g.ogl_data[index];


                int total_amount = o.보유량 * (int) o.현재가;
                종목.Add(Tuple.Create(total_amount, stock));
            }
            
            종목 = 종목.OrderByDescending(t => t.Item1).ToList();
            g.보유종목.Clear();

            foreach (var item in 종목)
                g.보유종목.Add(item.Item2);

        }


        public static void dgv1_trade_by_key(DataGridView dgv1, DataGridView dgv2, string buyorsell, string money_to_buy) // 매도 tr(6), 매수 tr(3)
        {
            string stock = wk.return_dgv_stock(dgv1);
            if (stock == null || !money_to_buy.All(char.IsDigit))
            {
                return;
            }
            int 일회거래액 = Convert.ToInt32(money_to_buy);

            int index = wk.return_index_of_ogldata(stock);
            if (index < 0) return;
            g.stock o = g.ogl_data[index];

            int 보유량 = o.보유량;

            int selected_price = 0;
            if (buyorsell == "매수" || buyorsell == "대기매도")
            {
                selected_price = wk.return_integer_from_mixed_string(dgv1.Rows[4].Cells[1].Value.ToString());
            }
            else if (buyorsell == "매도" || buyorsell == "대기매수")
            {
                selected_price = wk.return_integer_from_mixed_string(dgv1.Rows[5].Cells[1].Value.ToString());
            }
            else
            {
            }

            int 거래량 = 0;
            if (selected_price == 0) // 상한가 진입한 종목은 리턴
                return;

            if (buyorsell != "비상매도" && selected_price != 0)
            {
                거래량 = 일회거래액 * 10000 / selected_price;
                if (거래량 == 0)
                    거래량 = 1;
            }
            if (buyorsell == "매수" || buyorsell == "대기매수") // 매수
            {
                if (거래량 == 0)
                    거래량 = 1;
                if (보유량 == 3) // 확인되지 않는 상태에서 지속 1주씩 매수된 적인 있음
                    return;
            }
            else if (buyorsell == "매도" || buyorsell == "대기매도") // 매도
            {
                if (보유량 < 거래량)
                {
                    dl.deal_cancel_if_processing(stock, dgv2); // tr(3 or 1)
                    dl.deal_hold(); // tr(1), 매도 or 대기매도
                    보유량 = o.보유량;
                    if (보유량 == 0)
                    {
                        return;
                    }
                    거래량 = 보유량;
                }
            }
            else if (buyorsell == "비상매도")// 지정된 종목 보유분 전부 시장가 매도
            {
                dl.deal_cancel_if_processing(stock, dgv2); // tr(2)
                dl.deal_hold(); // tr(1), 비상매도
                보유량 = o.보유량;
                if (보유량 == 0)
                {
                    return;
                }
                거래량 = 보유량;
            }
            else
            {
            }

            string 주문조건구분 = "0"; // "0" 없음, "1" IOC, "2" FOK
            string 주문호가구분 = "01"; // "01" 보통, "03" 시장가
            if (buyorsell == "매수" || buyorsell == "대기매수")
            {
                buyorsell = "매수";
                주문조건구분 = "0"; // 매수1호가창에 있는 수량까지만 매수
                주문호가구분 = "01"; // 지정가
            }
            if (buyorsell == "대기매도")
            {
                buyorsell = "매도";
                주문조건구분 = "0"; // 없음
                주문호가구분 = "01"; // 지정가
            }
            //if (buyorsell == "시장가매수")
            //{
            //    buyorsell = "매수";
            //    selected_price = 0;
            //    주문조건구분 = "0"; // 없음
            //    주문호가구분 = "03"; // 시장가
            //}
            if (buyorsell == "비상매도")
            {
                buyorsell = "매도";
                selected_price = 0;
                주문조건구분 = "0"; // 없음
                주문호가구분 = "03"; // 시장가
            }

            deal_trade(buyorsell, stock, 거래량, selected_price, 주문조건구분, 주문호가구분, dgv2); // dgv1_trade_by_key tr(1)
            dl.dgv2_update(dgv2, true); // dgv1_trade_by_key
        }


        public static void deal_trade(string buyorsell, string stock,
        long numberofstock, long price, string 주문조건구분, string 주문호가구분, DataGridView dgv2) // tr(1)
        {
            if (g.testing == true)
                return;

            if (numberofstock == 0)
                return;

            string stockcode = _cpstockcode.NameToCode(stock);
            _cptd0311 = new CPTRADELib.CpTd0311();


            if (_cptd0311.GetDibStatus() == 1)
            {
                Trace.TraceInformation("DibRq 요청 수신대기 중 입니다. 수신이 완료된 후 다시 호출 하십시오.");
                return;
            }

            if (buyorsell == "매수")
            {
                _cptd0311.SetInputValue(0, "2");    // 2:매수
                int index = wk.return_index_of_ogldata(stock);
                if (index < 0)
                    return;
                g.stock o = g.ogl_data[index];

                if (numberofstock > 1 && g.confirm_buy || g.click_trade || price > 100000) // 다량 매수 또는 10만원 이상일 때는 다시 확인 절차 진행
                {
                    string caption = stock + " : " + price.ToString() + " X " + numberofstock.ToString() +
                       " = " + (numberofstock * price / 10000).ToString() + "만원";
                    string message = "";
                    if (o.시총 > 50)
                    {
                        message = "                     매수 ?";
                    }
                    MessageBoxButtons buttons = MessageBoxButtons.YesNo;
                    DialogResult result;
                    // Displays the MessageBox
                    result = MessageBox.Show(message, caption, buttons);

                    if (result == System.Windows.Forms.DialogResult.No)
                    {
                        return;
                    }
                }
            }

            else // sell
            {
                int index = wk.return_index_of_ogldata(stock);
                if (index < 0)
                    return;
                g.stock o = g.ogl_data[index];

                if (g.click_trade || (g.confirm_sell && numberofstock != 1)) // && 주문호가구분 != "03") // dgv2를 클릭하여 매도할 경우, 우발적 클릭을 방지하기 위해 확인 후 매도
                                                                            // "03" <- 시장가 매도 ... 비상매도
                {
                    string caption = stock + " : " + price.ToString() + " X " + numberofstock.ToString() +
                       " = " + (numberofstock * price / 10000).ToString() + "만원";
                    string message = "";

                    message = "                  매도 ?";

                    MessageBoxButtons buttons = MessageBoxButtons.YesNo;
                    DialogResult result;
                    // Displays the MessageBox
                    result = MessageBox.Show(message, caption, buttons);

                    if (result == System.Windows.Forms.DialogResult.No)
                    {
                        return;
                    }
                }
                _cptd0311.SetInputValue(0, "1");    // 1:매도
            }

            _cptd0311.SetInputValue(1, g.Account);  // 계좌번호
            _cptd0311.SetInputValue(2, "01");     // 상품구분코드
            _cptd0311.SetInputValue(3, stockcode);  // 종목코드
            _cptd0311.SetInputValue(4, numberofstock);  // 주문수량 
            _cptd0311.SetInputValue(5, price);    // 매수단가
            _cptd0311.SetInputValue(7, 주문조건구분);  // 주문조건구분("0":없음,"1":IOC, "2":FOK)
            _cptd0311.SetInputValue(8, 주문호가구분);    // 주문호가구분("01":보통,"02":임의,"03":시장가,"05":조건부지정가 etc)

            
            int check = _cptd0311.BlockRequest();
            // dl.dgv2_update(dgv2, true); // consume too many transaction remained
        }


        //주문관리(취소 주문)
        public static string deal_cancel(string stock, long 원주문번호, DataGridView dgv2) // tr(1)
        {
            if (g.testing == true)
                return "";

            string stockcode = _cpstockcode.NameToCode(stock);
            _cptd0314 = new CPTRADELib.CpTd0314();

            // 미체결 주문번호입력에 따른 선택적 취소
            _cptd0314.SetInputValue(1, 원주문번호);// 원주문번호 
            _cptd0314.SetInputValue(2, g.Account);// 계좌번호 
            _cptd0314.SetInputValue(3, "01"); // 상품관리구분코드
            _cptd0314.SetInputValue(4, stockcode);// 종목코드
            _cptd0314.SetInputValue(5, 0);// 0 : 전체 취소, 숫자입력 : 입력숫자만큼 취소

            int result = _cptd0314.BlockRequest();

            //dl.dgv2_update(dgv2, false);

            return _cptd0314.GetDibMsg1();
        }


        //주문관리(정정 주문)
        private string deal_correct(long 원주문번호, string stock, long 주문수량, long 주문단가) // tr(1)
        {
            if (g.testing == true)
                return "";

            string stockcode = _cpstockcode.NameToCode(stock);
            _cptd0303 = new CPTRADELib.CpTd0303();

            // 정정 : 주문번호가 아닌 원주문번호 입력 에러 발생
            _cptd0303.SetInputValue(1, 원주문번호); // 원주문번호
            _cptd0303.SetInputValue(2, g.Account); // 계좌번호
            _cptd0303.SetInputValue(3, "1");   // 상품관리구분코드
            _cptd0303.SetInputValue(4, stockcode);   // 종목코드
            _cptd0303.SetInputValue(5, 주문수량);   // 주문수량
            _cptd0303.SetInputValue(6, 주문단가);   // 주문단가

            int result = _cptd0303.BlockRequest();

            return _cptd0303.GetDibMsg1();
        }


        public static void dgv2_update(DataGridView dgv2, bool deal_processing_hold) // tr(2)
        {
            dgv2.Refresh();

            dtb2 = new DataTable();

            dtb2.Columns.Add("종목");
            dtb2.Columns.Add("시장구분");
            dtb2.Columns.Add("매수");
            dtb2.Columns.Add("매도");

            // tr = GetRemainTR();
            if (deal_processing_hold)
            {
                deal_processing(); // tr(1) ??
                deal_hold(); // tr(1) dgv2_update ??
            }

            // tr = GetRemainTR();

            //dtb2.Rows.Add(종목, 주문구분 + " " + 주문단가.ToString() + " " + 주문번호.ToString(),주문수량 + "/" + 체결수량); // 
            //string t = 종목 + " " + 주문번호.ToString() + " " + 주문구분 + " " + 주문단가.ToString() + " " + 주문수량 + " " + 체결수량;
            string n;
            foreach (var item in g.체결진행)
            {
                string[] words = item.Split('\t');

                string 종목 = words[0];
                string 주문번호 = words[1];
                string 주문구분 = words[2];
                string 주문단가 = words[3];
                string 주문수량 = words[4];
                string 체결수량 = words[5];
                //n = dtb2.Rows[0][1].ToString();
                dtb2.Rows.Add(종목, 주문구분 + " " + 주문단가.ToString() + " " + 주문번호.ToString(), 주문수량 + "/" + 체결수량);
                //n = dtb2.Rows[1][1].ToString();
            }
            //n = dtb2.Rows[0][1].ToString();
            foreach (string stock in g.보유종목)
            {
                //설정매수수량_설정매도수량(stock, dgv2, ref 설정매수수량, ref 설정매도수량);
                //int index = g.ogl_data.FindIndex(x => x.종목 == stock);

                int index = wk.return_index_of_ogldata(stock);
                if (index < 0)
                    return;
                g.stock o = g.ogl_data[index];

                bool show = false;
                if (o.보유량 * o.현재가 <= 500000) // 50만원 이하는 감추기
                {
                    if (g.show_small_amount)
                        show = true;
                }
                else
                {
                    show = true;
                }

                if (show) // 
                {
                    double 매수1호가량_매도1호가량_비례 = (double)o.매수1호가잔량 / (double)o.매도1호가잔량;
                    dtb2.Rows.Add(stock,
                                        o.보유량.ToString() + "/" + (o.매수1호가 / 10000.0).ToString("0.###"),
                                        o.매수1호가잔량.ToString() + "/" + 매수1호가량_매도1호가량_비례.ToString("0.#"),
                                        o.수익률.ToString("0.##"));
                }
            }
            /*
			foreach (var stock in g.관심종목)
			{
			  설정매수수량_설정매도수량(stock, dgv2, ref 설정매수수량, ref 설정매도수량);
			  int index = g.ogl_data.FindIndex(x => x.종목 == stock);
			  dtb2.Rows.Add(stock,"",
				설정매수수량, 설정매도수량);
			}*/

            dtb2.Rows.Add("Working Stocks", // dgv2_update
                g.ogl_data.Count.ToString(), "", g.sl.Count.ToString());

            group_대비(dtb2); // dtb2를 패스하고 각 그룹의 대비순으로 입력하여 dgv2에 표시

            dgv2.DataSource = dtb2;

            dgv2.Columns[0].Width = dgv2.Width / 20 * 5; // 음수가 아니어야 한다는 에러가 날 때도 있고 안 날 때도 있고 (확인 필요)
            dgv2.Columns[1].Width = dgv2.Width / 20 * 5;
            dgv2.Columns[2].Width = dgv2.Width / 20 * 5;
            dgv2.Columns[3].Width = dgv2.Width / 20 * 5;
        }


        private static void group_대비(DataTable passed_dtb)
        {
            char temp_key = 'v';
            for (int i = 0; i < g.oGL_data.Count; i++)
            {
                double previous_average_price = 0.0;
                g.oGL_data[i].average_price = 0.0;
                g.oGL_data[i].value = 0.0;
                int numberStockNotCounted = 0;
                int numberStockNotCountedPrevious = 0;
                for (int j = 0; j < g.oGL_data[i].stocks.Count; j++)
                {
                    int index = wk.return_index_of_ogldata(g.oGL_data[i].stocks[j]);
                    if (index < 0)
                        continue;

                    if (!g.testing)
                    {
                        switch (temp_key)
                        {
                            case 'a': // 배수차
                                g.oGL_data[i].value += g.ogl_data[index].x[g.ogl_data[index].nrow - 1, 8] - g.ogl_data[index].x[g.ogl_data[index].nrow - 1, 9];
                                break;
                            case 's':
                                g.oGL_data[i].value += g.ogl_data[index].분당프로그램매수액_천만원;
                                break;
                            case 'd':
                                g.oGL_data[i].value += g.ogl_data[index].프로그램누적거래액_천만원;
                                break;
                            case 'v': // 가격순
                                int current_time = g.ogl_data[index].nrow - 1; // // current_time >= 0, always
                                if (current_time < 0)
                                    continue;

                                if (g.ogl_data[index].x[current_time, 1] < 3001 && g.ogl_data[index].x[current_time, 1] > -3001)
                                {
                                    g.oGL_data[i].average_price += g.ogl_data[index].x[current_time, 1];
                                    g.oGL_data[i].value += g.ogl_data[index].x[current_time, 1];
                                }
                                else
                                    numberStockNotCounted++;

                                int previous_time = g.ogl_data[index].nrow - 3;
                                if (previous_time >= 0) // previous_time can be negative
                                    if (g.ogl_data[index].x[previous_time, 1] < 3001 && g.ogl_data[index].x[previous_time, 1] > -3001)
                                        previous_average_price += g.ogl_data[index].x[previous_time, 1];
                                    else
                                        numberStockNotCountedPrevious++;
                                else
                                    numberStockNotCountedPrevious++;
                                break;
                        }
                    }
                    else
                    {
                        switch (temp_key)
                        {
                            case 'a': // 배수차
                                g.oGL_data[i].value += g.ogl_data[index].x[g.time[1] - 1, 8] - g.ogl_data[index].x[g.time[1] - 1, 9];
                                break;
                            case 's':
                                g.oGL_data[i].value += g.ogl_data[index].분당프로그램매수액_천만원;
                                break;
                            case 'd':
                                g.oGL_data[i].value += g.ogl_data[index].프로그램누적거래액_천만원;
                                break;
                            case 'v': // 가격순
                                int current_time = g.time[1] - 1; // current_time >= 0, always
                                if (g.ogl_data[index].x[current_time, 1] < 3001 && g.ogl_data[index].x[current_time, 1] > -3001)
                                {
                                    g.oGL_data[i].average_price += g.ogl_data[index].x[current_time, 1];
                                    g.oGL_data[i].value += g.ogl_data[index].x[current_time, 1];
                                }
                                else
                                    numberStockNotCounted++;

                                int previous_time = g.time[1] - 3; 
                                if (previous_time >= 0) // previous_time can be negative
                                    if(g.ogl_data[index].x[previous_time, 1] < 3001 && g.ogl_data[index].x[previous_time, 1] > -3001)
                                        previous_average_price += g.ogl_data[index].x[previous_time, 1];
                                    else
                                        numberStockNotCountedPrevious++;
                                else
                                    numberStockNotCountedPrevious++;
                                break;
                        }
                    }
                }
                if ((g.oGL_data[i].stocks.Count - numberStockNotCounted) > 0)
                {
                    g.oGL_data[i].value /= (g.oGL_data[i].stocks.Count - numberStockNotCounted); // current value, if char
                    g.oGL_data[i].average_price /= (g.oGL_data[i].stocks.Count - numberStockNotCounted); // current average price
                }
               
                if((g.oGL_data[i].stocks.Count - numberStockNotCountedPrevious) > 0)
                {
                    previous_average_price /= (g.oGL_data[i].stocks.Count - numberStockNotCountedPrevious); // average price before 2 minutes
                }
                    
                g.oGL_data[i].difference_price = g.oGL_data[i].average_price - previous_average_price;
            }

            g.oGL_data = g.oGL_data.OrderByDescending(x => x.value).ToList();
            for (int i = 0; i < g.oGL_data.Count / 2 ; i++)
            {
                passed_dtb.Rows.Add(g.oGL_data[i * 2 + 0].title, (int)g.oGL_data[i * 2 + 0].value + "/" + g.oGL_data[i * 2 + 0].difference_price.ToString("#"), 
                                             g.oGL_data[i * 2 + 1].title, (int)g.oGL_data[i * 2 + 1].value + "/" + g.oGL_data[i * 2 + 1].difference_price.ToString("#"));
            }
                
           



            //// setting group data to zero
            //foreach (var t in g.oGL_data)
            //{
            //    t.대비평균 = 0.0;
            //    t.통과종목수 = 0;
            //}

            //List<g.stock> temporary_ogl_data = new List<g.stock>();
            //lock (g.lockObject)
            //{
            //    temporary_ogl_data = g.ogl_data.ToList();
            //}
            //foreach (var o in temporary_ogl_data)
            //{
            //    string stock = o.종목;
            //    int index = temporary_ogl_data.FindIndex(r => r.종목 == stock);

            //    if (index < 0 || o.oGL_sequence_id < 0 ||   // o.oGL_sequence_id < 0 : the stock not in g.oGL
            //        stock.Contains("코스피") ||
            //        stock.Contains("코스닥") ||
            //        stock.Contains("KODEX") ||
            //        stock.Contains("KOSEF") ||
            //        stock.Contains("TIGER") ||
            //        stock.Contains("KBSTAR")) // 지수종목, 평불종목 제외
            //    {
            //        continue;
            //    }

            //    o.inclusion_passed = wk.stock_inclusion(o);
            //    if (o.inclusion_passed) // 조건 만족 종목만 포함하여 대비 계산
            //    {
            //        g.oGL_data[o.oGL_sequence_id].대비평균 += o.점수.총점;
            //        g.oGL_data[o.oGL_sequence_id].통과종목수++;
            //    }
            //}


            //List<Tuple<double, string, int>> a_tuple = new List<Tuple<double, string, int>> { };
            //foreach (g.group_data t in g.oGL_data)
            //{
            //    if (t.통과종목수 >= 1 && t.통과종목수 < 50)
            //    {
            //        t.대비평균 /= t.통과종목수; // 그룹 대비 평균 = 대비합계 / 종목수
            //    }
            //    a_tuple.Add(Tuple.Create(t.대비평균, t.대표종목, t.통과종목수));
            //}

            //a_tuple = a_tuple.OrderByDescending(t => t.Item1).ToList();

            //int a_tuple_count = 0;
            //string t1 = "";
            //string t2 = "";

            //foreach (Tuple<double, string, int> item in a_tuple) // the last 그룹 생략
            //{
            //    if (!g.그룹대비배수)
            //    {
            //        if (a_tuple_count % 2 == 0)
            //        {
            //            t1 = item.Item2.ToString() + " 그룹";
            //            t2 = item.Item1.ToString("#.#") + " / " + item.Item3;
            //        }
            //        else
            //        {
            //            passed_dtb.Rows.Add(t1, t2, item.Item2 + " 그룹", item.Item1.ToString("#.#") + " / " + item.Item3);
            //        }
            //        a_tuple_count++;

            //        string t = item.Item2.ToString() + " 그룹" + "," + item.Item1.ToString("#.#") + " / " + item.Item3;
            //    }
            //    else
            //    {
            //        if (a_tuple_count % 2 == 0)
            //        {
            //            t1 = item.Item2.ToString() + " 그룹";
            //            t2 = item.Item1.ToString("0") + " / " + item.Item3;
            //        }
            //        else
            //        {
            //            passed_dtb.Rows.Add(t1, t2, item.Item2 + " 그룹", item.Item1.ToString("0") + " / " + item.Item3);
            //        }
            //        a_tuple_count++;

            //        string t = item.Item2.ToString() + " 그룹" + "," + item.Item1.ToString("0") + " / " + item.Item3;
            //    }
            //}

            ////g.passed_Gl.Clear();
            //List<string> temp_list = new List<string>();
            //List<string> found_list = new List<string>();

            ////int passed_count = 0;
            //foreach (var small_group in a_tuple)
            //{
            //    found_list = wk.종목포함_그룹리스트찾기(small_group.Item2);
            //    temp_list = wk.분당거래액_천만원순서(found_list); // this subroutine return whole stocks based on 분당거래액_천만원순서

            //    //if (temp_list != null)
            //        //g.passed_Gl.Add(temp_list.ToList());
            //}

            //g.Group_ranking.Clear();
            //int ranking = 1;
            //foreach (var small_group in a_tuple)
            //{
            //    var t = new g.group_ranking();
            //    t.대비평균 = small_group.Item1; // 통과된 종목들의 평점 double
            //    t.대표종목 = small_group.Item2; //통과된 종목 중 대표
            //    t.통과종목수 = small_group.Item3; //통과된 종목 수
            //    t.랭킹 = ranking++;
            //    found_list = wk.종목포함_그룹리스트찾기(small_group.Item2);
            //    t.종목들 = wk.분당거래액_천만원순서(found_list); // this subroutine return whole stocks based on 분당거래액_천만원순서

            //    g.Group_ranking.Add(t);
            //}
        }


        public static void 설정매수수량_설정매도수량(string stock, DataGridView dgv2, ref int 설정매수수량, ref int 설정매도수량)
        {
            for (int i = 0; i < dgv2.Rows.Count; i++)
            {
                if (!dgv2.Rows[i].Cells[2].Value.ToString().Contains("/")) // 체결종목이 아니면  
                {
                    if (stock == dgv2.Rows[i].Cells[0].Value.ToString()) // 보유 관심 종목이면
                    {
                        bool success = int.TryParse((string)dgv2.Rows[i].Cells[2].Value, out 설정매수수량);
                        if (!success)
                        {
                            설정매수수량 = 0;
                        }

                        success = int.TryParse((string)dgv2.Rows[i].Cells[3].Value, out 설정매도수량);
                        if (!success)
                        {
                            설정매도수량 = 0;
                        }
                    }
                }
            }
        }


        public static void deal_호가_추가(g.stock o, DataTable dtb) ///
        {
            double divider = 10;
            for (int i = 0; i < 5; i++)
            {
                dtb.Rows[i + 5]["매도"] = " " + ((int)((o.틱매수량[i] - o.틱매수량[i + 1]) / divider)).ToString()
                          + "/" +
                         ((int)((o.틱매도량[i] - o.틱매도량[i + 1]) / divider)).ToString();
            }
            for (int i = 0; i < 5; i++)
            {
                dtb.Rows[i]["매수"] ="(" + o.틱프로돈[i].ToString() + ")" + 
                    ((int)(o.틱매수배[i])).ToString()+ "/" +
                    ((int)(o.틱매도배[i])).ToString();
            }

            o.매도1호가잔량 = (ulong)Convert.ToInt32(dtb.Rows[4]["매도"]);
            o.매수1호가잔량 = (ulong)Convert.ToInt32(dtb.Rows[5]["매수"]);
            o.매도1호가 = Convert.ToInt32(dtb.Rows[4]["호가"]);
            o.매수1호가 = Convert.ToInt32(dtb.Rows[5]["호가"]);
            o.현재가 = o.매수1호가;

            // 한 호가 차이 슬리피지 % 계산
            if (o.매수1호가 > 0)
                dtb.Rows[10]["호가"] = ((o.매도1호가 - o.매수1호가) / (double)o.매수1호가 * 100.0).ToString("  0.##");
            else
                dtb.Rows[10]["호가"] = "No Data";

            // StopLoss 호가에 괄호표시
            if (o.보유량 > 0 && o.StopLoss > 0)
            {
                for (int i = 0; i < 10; i++)
                {
                    if (Convert.ToInt32(dtb.Rows[i]["호가"]) == o.StopLoss)
                    {
                        dtb.Rows[i]["호가"] = "(" + dtb.Rows[i]["호가"] + ")";

                        if (i <= 5)
                        {
                            ms.Sound("","Emergency");
                            o.StopLoss = 0;
                        }
                    }
                }
            }

            string 보유량_손익단가 = "";
            if (o.보유량 > 0)
            {
                if (o.현재가 > 0)
                    보유량_손익단가 = o.보유량 + "(" +
                          ((o.현재가 - o.손익단가) / (double)o.현재가 * 100.0).ToString("0.##") + ")";
                else
                {
                    if (o.전일종가 > 0)
                        보유량_손익단가 = o.보유량 + "(" +
                         (((int)o.전일종가 - o.손익단가) / (double)o.전일종가 * 100.0).ToString("0.##") + ")";
                }
            }
            dtb.Rows[11][0] = o.종목; dtb.Rows[11][1] = o.dev_avr; dtb.Rows[11][2] = 보유량_손익단가;
        }


        public static void deal_호가(string stock, DataGridView dgv1, DataGridView dgv3, DataGridView dgv4) // tr(1)
        {
            if (g.testing == true)
                return;
            
            string stock1 = "", stock3 = "", stock4 = "";
            if (dgv1.RowCount == 12)
                stock1 = dgv1.Rows[11].Cells[0].Value.ToString();
            if (dgv3.RowCount == 12)
                stock3 = dgv3.Rows[11].Cells[0].Value.ToString();
            if (dgv4.RowCount == 12)
                stock4 = dgv4.Rows[11].Cells[0].Value.ToString();

            if (stock == stock1)
                return;
            else if (stock == stock3)
            {
                stock4 = stock4;
                stock3 = stock1;
                stock1 = stock;
            }
            else if (stock == stock4)
            {
                stock4 = stock3;
                stock3 = stock1;
                stock1 = stock;
            }
            else
            {
                stock4 = stock3;
                stock3 = stock1;
                stock1 = stock;
            }

            deal_hold(); // tr(1), deal_호가

            index1 = wk.return_index_of_ogldata(stock1);
            index3 = wk.return_index_of_ogldata(stock3);
            index4 = wk.return_index_of_ogldata(stock4);

            if (index1 > 0)
            {
                dtb1 = new DataTable();
                dtb1.Columns.Add("매도");
                dtb1.Columns.Add("호가");
                dtb1.Columns.Add("매수");

                _stockjpbid1 = new DSCBO1Lib.StockJpbid();
                string stockcode1 = _cpstockcode.NameToCode(stock1);

                request_호가(stock1, dtb1);
                deal_호가_추가(g.ogl_data[index1], dtb1);

                _stockjpbid1.SetInputValue(0, stockcode1);
                _stockjpbid1.Received += new DSCBO1Lib._IDibEvents_ReceivedEventHandler(stockjpbid1_Received);
                _stockjpbid1.Subscribe();

                dgv1.DataSource = dtb1;
            }

            if (index3 > 0)
            {
                dtb3 = new DataTable();
                dtb3.Columns.Add("매도");
                dtb3.Columns.Add("호가");
                dtb3.Columns.Add("매수");

                _stockjpbid3 = new DSCBO1Lib.StockJpbid();
                string stockcode3 = _cpstockcode.NameToCode(stock3);

                request_호가(stock3, dtb3);
                deal_호가_추가(g.ogl_data[index3], dtb3);

                _stockjpbid3.SetInputValue(0, stockcode3);
                _stockjpbid3.Received += new DSCBO1Lib._IDibEvents_ReceivedEventHandler(stockjpbid3_Received);
                _stockjpbid3.Subscribe();

                dgv3.DataSource = dtb3;
            }

            if (index4 > 0)
            {
                dtb4 = new DataTable();
                dtb4.Columns.Add("매도");
                dtb4.Columns.Add("호가");
                dtb4.Columns.Add("매수");

                _stockjpbid4 = new DSCBO1Lib.StockJpbid();
                string stockcode4 = _cpstockcode.NameToCode(stock4);

                request_호가(stock4, dtb4);
                deal_호가_추가(g.ogl_data[index4], dtb4);

                _stockjpbid4.SetInputValue(0, stockcode4);
                _stockjpbid4.Received += new DSCBO1Lib._IDibEvents_ReceivedEventHandler(stockjpbid4_Received);
                _stockjpbid4.Subscribe();


                dgv4.DataSource = dtb4;
            }
            return;
        }


        private static void request_호가(string stock, DataTable dtb)
        {
            if (g.testing == true)
                return;

            _stockjpbid2 = new DSCBO1Lib.StockJpbid2();
            if (_stockjpbid2.GetDibStatus() == 1)
            {
                Trace.TraceInformation("DibRq 요청 수신대기 중 입니다. 수신이 완료된 후 다시 호출 하십시오.");
                return;
            }

            dtb.Clear();
            for (int i = 0; i < 12; i++)
            {
                dtb.Rows.Add("", "", "");
            }

            //dataGridView2.Refresh();
            string stockcode = _cpstockcode.NameToCode(stock);
            _stockjpbid2.SetInputValue(0, stockcode);

            int result = _stockjpbid2.BlockRequest();

            if (result == 0)
            {
                dtb.Rows[10]["매도"] = _stockjpbid2.GetHeaderValue(4).ToString();
                dtb.Rows[10]["매수"] = _stockjpbid2.GetHeaderValue(6).ToString();

                int IndexSell = 4;
                int IndexBuy = 5;

                for (int i = 0; i < int.Parse(_stockjpbid2.GetHeaderValue(1).ToString()) / 2; i++)
                {
                    dtb.Rows[IndexSell]["호가"] = _stockjpbid2.GetDataValue(0, i);
                    dtb.Rows[IndexBuy]["호가"] = _stockjpbid2.GetDataValue(1, i);

                    dtb.Rows[IndexSell]["매도"] = _stockjpbid2.GetDataValue(2, i);
                    dtb.Rows[IndexBuy]["매수"] = _stockjpbid2.GetDataValue(3, i);

                    IndexSell -= 1;
                    IndexBuy += 1;
                }

                // 한 호가 차이 슬리피지 % 계산
                int valup = (int)_stockjpbid2.GetDataValue(0, 0);
                int valdn = (int)_stockjpbid2.GetDataValue(1, 0);

                if (valdn > g.EPS)
                {
                    dtb.Rows[10]["호가"] = ((valup - valdn) / (double)valdn * 100.0).ToString("  0.##");
                }
                else
                {
                    dtb.Rows[10]["호가"] = "No Data";
                }
            }
        }


        private static void stockjpbid1_Received()
        {
            if (index1 < 0 || g.testing == true)
                return;

            g.stock o = g.ogl_data[index1];

            //object code = _stockjpbid1.GetHeaderValue(0);
            //object time = _stockjpbid1.GetHeaderValue(1);
            //object amount = _stockjpbid1.GetHeaderValue(2);

            dtb1.Rows[4]["호가"] = _stockjpbid1.GetHeaderValue(3);
            dtb1.Rows[5]["호가"] = _stockjpbid1.GetHeaderValue(4);
            dtb1.Rows[4]["매도"] = _stockjpbid1.GetHeaderValue(5);
            dtb1.Rows[5]["매수"] = _stockjpbid1.GetHeaderValue(6);

            dtb1.Rows[3]["호가"] = _stockjpbid1.GetHeaderValue(7);
            dtb1.Rows[6]["호가"] = _stockjpbid1.GetHeaderValue(8);
            dtb1.Rows[3]["매도"] = _stockjpbid1.GetHeaderValue(9);
            dtb1.Rows[6]["매수"] = _stockjpbid1.GetHeaderValue(10);

            dtb1.Rows[2]["호가"] = _stockjpbid1.GetHeaderValue(11);
            dtb1.Rows[7]["호가"] = _stockjpbid1.GetHeaderValue(12);
            dtb1.Rows[2]["매도"] = _stockjpbid1.GetHeaderValue(13);
            dtb1.Rows[7]["매수"] = _stockjpbid1.GetHeaderValue(14);

            dtb1.Rows[1]["호가"] = _stockjpbid1.GetHeaderValue(15);
            dtb1.Rows[8]["호가"] = _stockjpbid1.GetHeaderValue(16);
            dtb1.Rows[1]["매도"] = _stockjpbid1.GetHeaderValue(17);
            dtb1.Rows[8]["매수"] = _stockjpbid1.GetHeaderValue(18);

            dtb1.Rows[0]["호가"] = _stockjpbid1.GetHeaderValue(19);
            dtb1.Rows[9]["호가"] = _stockjpbid1.GetHeaderValue(20);
            dtb1.Rows[0]["매도"] = _stockjpbid1.GetHeaderValue(21);
            dtb1.Rows[9]["매수"] = _stockjpbid1.GetHeaderValue(22);

            dtb1.Rows[10]["매도"] = _stockjpbid1.GetHeaderValue(23);
            dtb1.Rows[10]["매수"] = _stockjpbid1.GetHeaderValue(24);

            deal_호가_추가(o, dtb1);
        }


        private static void stockjpbid3_Received()
        {
            if (index3 < 0 || g.testing == true)
                return;

            g.stock o = g.ogl_data[index3];

            dtb3.Rows[4]["호가"] = _stockjpbid3.GetHeaderValue(3);
            dtb3.Rows[5]["호가"] = _stockjpbid3.GetHeaderValue(4);
            dtb3.Rows[4]["매도"] = _stockjpbid3.GetHeaderValue(5);
            dtb3.Rows[5]["매수"] = _stockjpbid3.GetHeaderValue(6);

            dtb3.Rows[3]["호가"] = _stockjpbid3.GetHeaderValue(7);
            dtb3.Rows[6]["호가"] = _stockjpbid3.GetHeaderValue(8);
            dtb3.Rows[3]["매도"] = _stockjpbid3.GetHeaderValue(9);
            dtb3.Rows[6]["매수"] = _stockjpbid3.GetHeaderValue(10);

            dtb3.Rows[2]["호가"] = _stockjpbid3.GetHeaderValue(11);
            dtb3.Rows[7]["호가"] = _stockjpbid3.GetHeaderValue(12);
            dtb3.Rows[2]["매도"] = _stockjpbid3.GetHeaderValue(13);
            dtb3.Rows[7]["매수"] = _stockjpbid3.GetHeaderValue(14);

            dtb3.Rows[1]["호가"] = _stockjpbid3.GetHeaderValue(15);
            dtb3.Rows[8]["호가"] = _stockjpbid3.GetHeaderValue(16);
            dtb3.Rows[1]["매도"] = _stockjpbid3.GetHeaderValue(17);
            dtb3.Rows[8]["매수"] = _stockjpbid3.GetHeaderValue(18);

            dtb3.Rows[0]["호가"] = _stockjpbid3.GetHeaderValue(19);
            dtb3.Rows[9]["호가"] = _stockjpbid3.GetHeaderValue(20);
            dtb3.Rows[0]["매도"] = _stockjpbid3.GetHeaderValue(21);
            dtb3.Rows[9]["매수"] = _stockjpbid3.GetHeaderValue(22);

            dtb3.Rows[10]["매도"] = _stockjpbid3.GetHeaderValue(23);
            dtb3.Rows[10]["매수"] = _stockjpbid3.GetHeaderValue(24);

            deal_호가_추가(o, dtb3);
        }


        private static void stockjpbid4_Received()
        {
            if (index4 < 0 || g.testing == true)
                return;

            g.stock o = g.ogl_data[index4];

            dtb4.Rows[4]["호가"] = _stockjpbid4.GetHeaderValue(3);
            dtb4.Rows[5]["호가"] = _stockjpbid4.GetHeaderValue(4);
            dtb4.Rows[4]["매도"] = _stockjpbid4.GetHeaderValue(5);
            dtb4.Rows[5]["매수"] = _stockjpbid4.GetHeaderValue(6);

            dtb4.Rows[3]["호가"] = _stockjpbid4.GetHeaderValue(7);
            dtb4.Rows[6]["호가"] = _stockjpbid4.GetHeaderValue(8);
            dtb4.Rows[3]["매도"] = _stockjpbid4.GetHeaderValue(9);
            dtb4.Rows[6]["매수"] = _stockjpbid4.GetHeaderValue(10);

            dtb4.Rows[2]["호가"] = _stockjpbid4.GetHeaderValue(11);
            dtb4.Rows[7]["호가"] = _stockjpbid4.GetHeaderValue(12);
            dtb4.Rows[2]["매도"] = _stockjpbid4.GetHeaderValue(13);
            dtb4.Rows[7]["매수"] = _stockjpbid4.GetHeaderValue(14);

            dtb4.Rows[1]["호가"] = _stockjpbid4.GetHeaderValue(15);
            dtb4.Rows[8]["호가"] = _stockjpbid4.GetHeaderValue(16);
            dtb4.Rows[1]["매도"] = _stockjpbid4.GetHeaderValue(17);
            dtb4.Rows[8]["매수"] = _stockjpbid4.GetHeaderValue(18);

            dtb4.Rows[0]["호가"] = _stockjpbid4.GetHeaderValue(19);
            dtb4.Rows[9]["호가"] = _stockjpbid4.GetHeaderValue(20);
            dtb4.Rows[0]["매도"] = _stockjpbid4.GetHeaderValue(21);
            dtb4.Rows[9]["매수"] = _stockjpbid4.GetHeaderValue(22);

            dtb4.Rows[10]["매도"] = _stockjpbid4.GetHeaderValue(23);
            dtb4.Rows[10]["매수"] = _stockjpbid4.GetHeaderValue(24);

            deal_호가_추가(o, dtb4);
        }
    }
}

