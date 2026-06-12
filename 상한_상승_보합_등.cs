using System;

public class Class1
{

        public void task_stock_add() // not used
        {
            int previous_mm = -1;
            while (true)
            {
                DateTime date = DateTime.Now;
                //int ss = Convert.ToInt32(date.ToString("ss")); // task_stock_add
                int mm = Convert.ToInt32(date.ToString("mm")); // task_stock_add

                Thread.Sleep(1000);
                if (previous_mm != mm)
                {
                    for (int I = 0; I < 3; I++) // 상승, 하락 OK
                    {
                        if (I % 3 == 0)
                            task_stock_add_choice = "상승";
                        else if (I % 3 == 1)
                            task_stock_add_choice = "보합";
                        else
                            task_stock_add_choice = "하락";

                        _cpsvrnew7043 = new CPSYSDIBLib.CpSvrNew7043();
                        _cpsvrnew7043.Received += new CPSYSDIBLib._ISysDibEvents_ReceivedEventHandler(task_stock_add_received);
                        _cpsvrnew7043.SetInputValue(0, '0'); // 거래소 + 코스닥

                        switch (task_stock_add_choice)
                        {
                            case "상한":
                                _cpsvrnew7043.SetInputValue(1, '1');
                                break;
                            case "상승":
                                _cpsvrnew7043.SetInputValue(1, '2'); // 상승
                                _cpsvrnew7043.SetInputValue(3, 21);  // was 61, 21 전일대비율 상위순, 22 전일대비하위순, 61 거래대금 상위
                                short start = 0;   // % unit such 0% -> 0
                                short end = 30;    // % unit such 30% -> 30
                                _cpsvrnew7043.SetInputValue(7, start);   // 등락율 시작
                                _cpsvrnew7043.SetInputValue(8, end);  // 등락율 끝 
                                break;
                            case "보합":
                                _cpsvrnew7043.SetInputValue(1, '3'); // 보합
                                                                     //start = 0;   // % unit such 0% -> 0
                                                                     //end = 0;    // % unit such 30% -> 30
                                                                     //_cpsvrnew7043.SetInputValue(7, start);   // 등락율 시작
                                                                     //_cpsvrnew7043.SetInputValue(8, end);  // 등락율 끝 
                                break;
                            case "하락":
                                _cpsvrnew7043.SetInputValue(1, '4'); // 하락
                                _cpsvrnew7043.SetInputValue(3, 21);  // was 61, 21 전일대비율 상위순, 22 전일대비하위순, 61 거래대금 상위
                                start = -30;   // % unit such 0% -> 0
                                end = 0;    // % unit such 30% -> 30
                                _cpsvrnew7043.SetInputValue(7, start);   // 등락율 시작
                                _cpsvrnew7043.SetInputValue(8, end);  // 등락율 끝 
                                break;
                            case "하한":
                                _cpsvrnew7043.SetInputValue(1, '4');
                                break;
                            case "신고":
                                _cpsvrnew7043.SetInputValue(1, '4');
                                break;
                            case "신저":
                                _cpsvrnew7043.SetInputValue(1, '4');
                                break;
                            case "상한후 하락":
                                _cpsvrnew7043.SetInputValue(1, '4');
                                break;
                            case "하한후 상승":
                                _cpsvrnew7043.SetInputValue(1, '4');
                                break;
                        }

                        _cpsvrnew7043.SetInputValue(2, '1'); // '1' 당일 ... '2' 전일 사용하여 추가정보 추출 가능할 듯
                        _cpsvrnew7043.SetInputValue(4, '1'); // '1' 관리제외, '2' 관리포함
                        _cpsvrnew7043.SetInputValue(5, '0'); // '0' 전체, '1' 1만주, '2' 5만주, '3' 10만주, '4' 50만주, '5' 100만주 이상
                        if (task_stock_add_choice == "상승" || task_stock_add_choice == "하락")
                            _cpsvrnew7043.SetInputValue(6, '0');      // 상승/하락일 경우 '0' 시가대비, '1' 고가대비 '2' 저가대비
                                                                      // '1':5일 '2':20일 '3':60일 '4':당해년,'5':52주
                                                                      // 이외의 경우 사용하지 않음
                        if (_cpsvrnew7043.GetDibStatus() == 1)
                        {
                            Trace.TraceInformation("DibRq 요청 수신대기 중 입니다. 수신이 완료된 후 다시 호출 하십시오.");
                            return;
                        }

                        int result = 1;


                        if (task_stock_add_choice == "상승" || task_stock_add_choice == "하락")
                        {
                            while (true)
                            {
                                int remain_count_nontrade_request =
                                        _cpcybos.GetLimitRemainCount(CPUTILLib.LIMIT_TYPE.LT_NONTRADE_REQUEST); //60건/15초ㅊ
                                if (remain_count_nontrade_request > 30)
                                {
                                    result = _cpsvrnew7043.BlockRequest(); // result = 0, success

                                    if (_cpsvrnew7043.Continue != 1)
                                    {
                                        break;
                                    }
                                }
                                else
                                {
                                    Thread.Sleep(100);
                                    continue;
                                }
                            }
                        }

                        else
                        {
                            result = _cpsvrnew7043.BlockRequest();
                            int repetition = total_count / 40 + 1;

                            for (int i = 0; i < repetition; i++)
                            {
                                result = _cpsvrnew7043.BlockRequest();
                                if (result != 0)
                                {
                                    i--;
                                    continue;
                                }

                                while (true)
                                {
                                    int remain_count_nontrade_request =
                                        _cpcybos.GetLimitRemainCount(CPUTILLib.LIMIT_TYPE.LT_NONTRADE_REQUEST);
                                    if (remain_count_nontrade_request < 30)
                                    {
                                        Thread.Sleep(100);
                                        continue;
                                    }
                                    else
                                        break;
                                }
                            }
                        }
                        previous_mm = mm;
                    }
                }
            }
        }


        private static void task_stock_add_received()
        {
            List<string> a_list = new List<string>();

            short 해당종목수 = _cpsvrnew7043.GetHeaderValue(0);
            total_count = _cpsvrnew7043.GetHeaderValue(1);
            //short 연속기준종목코드 = _cpsvrnew7043.GetHeaderValue(1);
            //long 연속기준데이터 = _cpsvrnew7043.GetHeaderValue(2); // null ?
            //short 데이터건수 = _cpsvrnew7043.GetHeaderValue(3);  // null ?

            for (short k = 0; k < 해당종목수; k++)
            {
                //string 종목코드 = _cpsvrnew7043.GetDataValue(0, k);
                string 종목 = _cpsvrnew7043.GetDataValue(1, k);
                long 현재가 = _cpsvrnew7043.GetDataValue(2, k);
                char 대비플래그 = (char)_cpsvrnew7043.GetDataValue(3, k);
                char 대비 = (char)_cpsvrnew7043.GetDataValue(4, k); // 전일종가 기준 상승 금액
                float 대비율 = _cpsvrnew7043.GetDataValue(5, k); // 전일종가 기준 상승 %
                long 거래량 = _cpsvrnew7043.GetDataValue(6, k); // 누적거래량
                long 시가 = _cpsvrnew7043.GetDataValue(7, k); // 시가, 상승의 경우 아래의 7, 8, 9, 10
                long 시가대비 = _cpsvrnew7043.GetDataValue(8, k); // 당일시가 대비 상승금액 

                // 아래 두 변수는 "상승" "하락"의 경우 float, "보합"의 경우 string으로 되어 있음
                double 시가대비율;
                //double 상승연속일수;
                if (task_stock_add_choice != "보합")
                {
                    시가대비율 = (double)_cpsvrnew7043.GetDataValue(9, k);
                    //상승연속일수 = (double)_cpsvrnew7043.GetDataValue(10, k);
                }
                else // 시가 = 0, 거래정지
                    시가대비율 = (현재가 - 시가) / (double)시가;

                //bool contained = true;
                //int a = g.ogl_data.FindIndex(x => x.종목 == 종목);
                //if (g.ogl_data.FindIndex(x => x.종목 == 종목) < 0)
                //    contained = false;
                //else
                //    contained = true;

                // 대비율, 시가대비율, 분당거래액, 분당가격상승, 당일누적거래액, 시총, dev, avr, 호가창
                //if ((대비율 > 2.0f || 시가대비율 > 2.0f) && wk.return_index_of_ogldata(종목) < 0);
                //ms.read_single_stock(종목);



            }
            Thread.Sleep(500);
        } // not used called by task_stock_add()

}
