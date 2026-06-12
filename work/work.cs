using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using glbl;

using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace work
{
    public class wk
    {
        static CPUTILLib.CpCodeMgr _cpcodemgr;
        static CPUTILLib.CpStockCode _cpstockcode;

        public static bool is_stock(string stock)
        {
            _cpstockcode = new CPUTILLib.CpStockCode();
            if (stock == "")
                return false;

  
            string code = _cpstockcode.NameToCode(stock); // 코스피혼합, 코스닥혼합 code.Length = 0 제외될 것임
            if (code.Length == 7)
                return true;
            else
                return false;
        }


        public static int return_integer_from_mixed_string(string input)
        {
            int value = 0;
            foreach (char c in input)
            {
                if ((c >= '0') && (c <= '9'))
                {
                    value = value * 10 + (c - '0');
                }
            }
            return value;
        }


        public static bool stock_inclusion(g.stock o)
        {

            // 누적거래액 이상
            if (g.v.종가기준추정거래액이상_천만원 > (int)o.종가기준추정거래액_천만원)
                return false;

            // real only 
            if (!g.testing && (o.매도호가거래액_천만원 < g.v.minimum_bidding && o.매수호가거래액_천만원 < g.v.minimum_bidding))
                return false; 

            // 수, 강의 크기가 일정 이하 제외, 실제로는 체결이상 = 0, 수급이상 = 0로 세팅되어 있고 
            // 다시 변수를 수정하지 않아 아래는 의미 없음(현재로는) XYZ
            //if ( o.체강 > g.v.체강이상 &&  o.수급 > g.v.수급이상)
            //    return false;

            // 편차 일정 이하 제외
            if (g.v.편차이상 > (int)o.dev)
                return false; // if testing, all pass this 

            // 분당 추정거래대금 일정 이하 제외
            if (g.v.분당거래액이상 > o.분당거래액_천만원)
                return false;

            // 시총 이하 제외 또는 시총 이상 제외
            if (g.v.시총이상 >= 0)
            {
                if (o.시총 < g.v.시총이상 - 0.01)
                    return false;
            }
            else
            {
                if (o.시총 > (g.v.시총이상 - 0.01) * -1.0)
                    return false;
            }

            // 시장구분
            if (g.v.코스피코스닥 == 'S' && o.시장구분 != 'S') // 코스피만 선택 : 코스닥종목 제외
            {
                return false;
            }
            if (g.v.코스피코스닥 == 'D' && o.시장구분 != 'D') // 코스닥만 선택 : 코스피종목 제외
            {
                return false;
            }
            return true;
        }


        public static void Swap<T>(ref T lhs, ref T rhs)
        {
            T temp = lhs;
            lhs = rhs;
            rhs = temp;
        }


        public static void calculate_cyan_magenta_in_stock()
        {
            for (int i = 0; i < g.ogl_data.Count; i++)
                //for (int i = 0; i < 30; i++)
                {
                int[,] x = new int[400, 12];
                int[,] d = new int[400, 2];

                int nrow = Read_Stock_Minute(g.date, g.ogl_data[i].종목, x);
                if (nrow <= 1)
                    continue;
                
                int[,] a = new int[400, 2];
                a[1, 0] = 1;
                a[1, 1] = 1;

                for (int k = 0; k < 2; k ++)
                {
                    for (int j = 2; j < nrow; j++)
                    {
                        int diff_price = x[j, 1] - x[j - 1, 1];
                        int diff_amount_or_intensity = x[j, k + 2] - x[j - 1, k + 2];
                        if (j == 1) // intensity multiplied by g.HUNDRED to make integer with accuracy
                        {
                            diff_amount_or_intensity = (int)(diff_amount_or_intensity / g.HUNDREAD);
                        }
                        if (diff_amount_or_intensity > 0)
                        {
                            d[j, k] = 1;
                            a[j, k] = a[j - 1, k] + 1;
                        }
                        else
                        {
                            a[j, k] = 0;
                        }
                    }
                }
            }
        }
    

        public static double convert_time_to_6_digit_integer(string t)
        {
            double value;
            string[] time = t.Split(':');

            value = Convert.ToInt32(time[0]) * 10000.0 +
                      Convert.ToInt32(time[1]) * 100.0 * 100.0 / 60.0 +
                      Convert.ToInt32(time[2]) * 100.0 / 60.0;
           
            return value;
        }


        //public static string return_Group_ranking(string stock)
        //{
        //    foreach (var t in g.Group_ranking)
        //    {
        //        if (t == null || t.종목들 == null)
        //            return "X";

        //        if (t.종목들.Contains(stock))
        //        {
        //            return t.랭킹.ToString();
        //        }
        //    }
        //    return "X";
        //}

        //public static string return_Group_ranking_통과종목수(string stock)
        //{
        //    foreach (var t in g.Group_ranking)
        //    {
        //        if (t == null || t.종목들 == null)
        //            return "X";

        //        if (t.종목들.Contains(stock))
        //        {
        //            return t.통과종목수.ToString();
        //        }
        //    }
        //    return "X";
        //}

        public static int return_index_of_ogldata(string stock)
        {
            int index = -1;
            index = g.ogl_data.FindIndex(x => x.종목 == stock);

            return index;
        }


        public static string return_dgv_stock(DataGridView dgv)
        {
            string dgv_stock = "";
            if (dgv.Rows[11].Cells[0].Value != null)
            {
                string a = dgv.Rows[11].Cells[0].Value.ToString(); // dgv에 표시된 주식
                string b = a.Replace("(", "");
                dgv_stock = b.Replace(")", "");
            }
            return dgv_stock;
        }

        
        public static void marketeye_종목(ref string[] codes)
        {
            int accumulated_count = 0;
            string code;

            g.지수보유관심종목.Clear();
           
            g.지수보유관심종목.Add("KODEX 레버리지");
            g.지수보유관심종목.Add("KODEX 코스닥150레버리지");

            foreach (var stock in g.보유종목)
            {
                if (!g.지수보유관심종목.Contains(stock))
                {
                    g.지수보유관심종목.Add(stock);
                }
            }
            foreach (var stock in g.관심종목)
            {
                if (!g.지수보유관심종목.Contains(stock))
                {
                    g.지수보유관심종목.Add(stock);
                }
            }

            foreach (var stock in g.지수보유관심종목) // g.지수보유관심종목 추가  
            {
                if (accumulated_count == g.stocks_per_marketeye)  // g.stocks_per_marketeye : max. number of stocks per one cycle
                    break;

                int index = g.ogl_data.FindIndex(x => x.종목 == stock);
                if (index < 0)
                    continue;

                if (!codes.Contains(g.ogl_data[index].code) && accumulated_count < g.stocks_per_marketeye) 
                {
                    codes[accumulated_count++] = g.ogl_data[index].code;
                }
            }

            if (g.시초)
            {
                for (int i = g.ogl_data_next; i < g.ogl_data.Count; i++)
                {
                    if (accumulated_count == g.stocks_per_marketeye) //0504
                        break;

                    if (g.ogl_data[i].code != null && g.ogl_data[i].code.Length == 7)
                    {
                        if (codes.Contains(g.ogl_data[i].code))
                            continue;

                        codes[accumulated_count++] = g.ogl_data[i].code;
                        g.ogl_data_next = i + 1;
                        if (g.ogl_data_next == g.ogl_data.Count)
                        {
                            g.ogl_data_next = 0;
                        }
                    }
                }
            }
            else
            {
                // stock_eval에서 최우선 순위의 g.stocks_per_marketeye 개 선택
                // 다운 전 초기에는 시총이 큰 종목 우선 선택
                // 위의 지수보유관심종목을 넣고 g.stocks_per_marketeye 개까지 codes에 순차적으로 입력

                lock (g.lockObject) // g.sl  추가
                {
                    foreach (var stock in g.sl) //BLOCK g.sl may be changed 컬렉션이 수정되었습니다 ... 메세지
                    {
                        if (accumulated_count >= g.stocks_per_marketeye / 2 ||
                            accumulated_count == g.stocks_per_marketeye)  // g.stocks_per_marketeye : max. number of stocks per one cycle
                            break;

                        code = _cpstockcode.NameToCode(stock); // 코스피혼합, 코스닥혼합 code.Length = 0 제외될 것임
                        if (codes.Contains(code))
                            continue;

                        if (code != null && code.Length == 7)
                        {
                            codes[accumulated_count++] = code;
                        }
                    }
                }

                // ogl_data 추가
                for (int i = g.ogl_data_next; i < g.ogl_data.Count; i++)
                {
                    if (accumulated_count == g.stocks_per_marketeye) //0504
                        break;

                    if (g.ogl_data[i].code != null && g.ogl_data[i].code.Length == 7)
                    {
                        if (codes.Contains(g.ogl_data[i].code))
                            continue;

                        codes[accumulated_count++] = g.ogl_data[i].code;
                        g.ogl_data_next = i + 1;
                        if (g.ogl_data_next == g.ogl_data.Count)
                        {
                            g.ogl_data_next = 0;
                        }
                    }
                }

                // ogl_data 끝까지 가면서 추가하여도 g.stocks_per_marketeye 안 되면 ogl_data 처음부터 추가
                if (accumulated_count != g.stocks_per_marketeye)
                {
                    for (int i = 0; i < g.ogl_data.Count; i++)
                    {
                        if (accumulated_count == g.stocks_per_marketeye) //0504
                            break;

                        if (g.ogl_data[i].code != null && g.ogl_data[i].code.Length == 7)
                        {
                            if (codes.Contains(g.ogl_data[i].code))
                                continue;

                            codes[accumulated_count++] = g.ogl_data[i].code;
                            g.ogl_data_next = i + 1;
                            if (g.ogl_data_next == g.ogl_data.Count)
                            {
                                g.ogl_data_next = 0;
                            }
                        }
                    }
                }
            }
        }


        public static bool gen_ogl_data(string stock)
        {
            _cpstockcode = new CPUTILLib.CpStockCode();

            var t = new g.stock();

            t.종목 = stock;
            t.code = _cpstockcode.NameToCode(t.종목);
            if (t.code.Length != 7)
            {
                return false; //루미마이크로, 메디포럼제약 코드 못 찾음, 합병된 것으로 추정
            }
            t.avr = 0.0;
            int days = 10;
            t.dev_avr = calcurate_종목일중변동평균편차(t.종목, days, ref t.avr, ref t.dev, ref t.일평균거래액_억원, ref t.일최소거래액_억원, ref t.일최대거래액_억원);
            t.일평균거래량 = calculate_종목20일기준일평균거래량(t.종목);

            if (t.일평균거래량 == 0)
            {
                return false;
            }
            t.시총 = read_시총(t.종목) / 100; // 시총 값 부정확 점검필요
            read_전일종가_전일거래액_천만원(t);
            if (t.시총 == 0)
            {
                return false;
            }
            t.시장구분 = read_코스피코스닥시장구분(t.종목);
            if (t.시장구분 != 'S' && t.시장구분 != 'D')
            {
                return false;
            }
            g.ogl_data.Add(t);
            return true;
        }


        public static void 전일거래액_천만원_순서(List<string> gl)
        {
            
            var 종목 = new List<Tuple<ulong, string>> { };

            foreach (var stock in gl)
            {
                if (stock.Contains("KODEX")) continue;

                int index = g.ogl_data.FindIndex(x => x.종목 == stock);
                if (index < 0) continue;

                종목.Add(Tuple.Create(g.ogl_data[index].전일거래액_천만원, stock));
            }
            종목 = 종목.OrderByDescending(t => t.Item1).ToList();

            gl.Clear();

            foreach (var item in 종목)
                gl.Add(item.Item2);
        }

        public static void 시총순서(List<string> gl)
        {
            var 종목 = new List<Tuple<double, string>> { };

            foreach (var stock in gl)
            {
                if (stock.Contains("KODEX")) continue;

                int index = g.ogl_data.FindIndex(x => x.종목 == stock);
                if (index < 0) continue;

                종목.Add(Tuple.Create(g.ogl_data[index].시총, stock));
            }
            종목 = 종목.OrderByDescending(t => t.Item1).ToList();

            gl.Clear();

            foreach (var item in 종목)
                gl.Add(item.Item2);
        }


        public static void 편차순서(List<string> gl)
        {
            var 종목 = new List<Tuple<double, string>> { };
            var uniqueItemsList = gl.Distinct().ToList();
         

            foreach (var stock in uniqueItemsList)
            {
                int index = g.ogl_data.FindIndex(x => x.종목 == stock);
                if (index < 0)
                    continue;

                종목.Add(Tuple.Create(g.ogl_data[index].dev, stock));
            }
            종목 = 종목.OrderByDescending(t => t.Item1).ToList();

            gl.Clear();

            foreach (var item in 종목)
                gl.Add(item.Item2);
        }


        public static void 종가기준추정누적거래액_천만원순서(List<string> gl)
        {
            var 종목 = new List<Tuple<int, string>> { };

            foreach (var stock in gl)
            {
                if (stock.Contains("KODEX")) continue;

                int index = g.ogl_data.FindIndex(x => x.종목 == stock);
                if (index < 0) continue;

                종목.Add(Tuple.Create(g.ogl_data[index].종가기준추정거래액_천만원, stock));
            }
            종목 = 종목.OrderByDescending(t => t.Item1).ToList();

            gl.Clear();
            gl.Add("KODEX 레버리지");
            gl.Add("KODEX 코스닥150레버리지");

            foreach (var item in 종목)
            {
                gl.Add(item.Item2);
            }
        }


        public static List<string> 분당거래액_천만원순서(List<string> gl)
        {
            var a_tuple = new List<Tuple<double, string>> { };
            List<string> list = new List<string>();


            if (gl == null) // 날짜변경 후 'f' 입력의 경우 발생
                return null;

            foreach (var stock in gl)
            {
                if (stock.Contains("KODEX")) continue;

                int index = g.ogl_data.FindIndex(x => x.종목 == stock);
                if (index < 0) continue;

                a_tuple.Add(Tuple.Create(g.ogl_data[index].분당거래액_천만원, stock));
            }
            a_tuple = a_tuple.OrderByDescending(t => t.Item1).ToList();

            foreach (var t in a_tuple)
            {
                list.Add(t.Item2);
            }
            return list;
        }

        public static void 순서(List<string> gl)
        {

            // deviation order
            var t = g.ogl_data.OrderByDescending(x => x.일평균거래액_억원).ToList();

            //var a_tuple = new List<Tuple<int, string>> { };
            //List<string> list = new List<string>();


            //if (gl == null) // 날짜변경 후 'f' 입력의 경우 발생
            //    return null;

            //foreach (var stock in gl)
            //{
            //    if (stock.Contains("KODEX")) continue;

            //    int index = g.ogl_data.FindIndex(x => x.종목 == stock);
            //    if (index < 0) continue;

            //    a_tuple.Add(Tuple.Create(g.ogl_data[index].일평균거래액_억원, stock));
            //}
            //a_tuple= a_tuple.OrderByDescending(t => t.Item1).ToList();

            foreach (var item in t)
            {
                string[] str = new string[6];
               
                str[0] = item.종목.ToString();
                str[1] = item.일평균거래액_억원.ToString();
                str[2] = item.일최대거래액_억원.ToString();
                str[3] = item.일최소거래액_억원.ToString();
                str[4] = item.dev.ToString();
                str[5] = item.avr.ToString();

                write_on_temp(str);
            }
        }

        //     public static void read_stocks_with_processed_in_directory(List<string> gl)
        //     {
        //         g.sl.Clear();
        //         string path = @"C:\Work\분\" + g.date.ToString();
        //         if (!Directory.Exists(path))
        //         {
        //             return;
        //         }
        //         var sl = Directory.GetFiles(path, "*.txt")
        //                  .Select(Path.GetFileName)
        //                  .ToList();

        //         /*
        //List<string> 그룹제외 = new List<string>();
        //그룹제외 = read_그룹제외();*/


        //         g.관심종목.Clear();
        //         foreach (var stock in sl)
        //         {
        //             if (!stock.Contains("processed"))
        //             {
        //                 continue;
        //             }
        //             string stock_without_txt = stock.Replace(".txt", "");

        //             /*
        //	if(!그룹제외.Contains(stock_without_txt))
        //	{
        //	  gl.Add(stock_without_txt);
        //	}*/

        //         }
        //         g.v.total_stocks = gl.Count;
        //         gen_ogl_data();
        //     }

        public static void read_stocks_in_directory(List<string> gl)
        {
            g.sl.Clear();
            string path = @"C:\Work\분\" + g.date.ToString();
            if (!Directory.Exists(path))
            {
                return;
            }
            var sl = Directory.GetFiles(path, "*.txt")
                     .Select(Path.GetFileName)
                     .ToList();
            
			List<string> 그룹제외 = new List<string>();
			그룹제외 = read_그룹제외();


            g.관심종목.Clear();
            foreach (var stock in sl)
            {
                if(그룹제외.Contains(stock))
                {
                    continue;
                }
                if (stock.Contains("processed"))
                {
                    continue;
                }
                string stock_without_txt = stock.Replace(".txt", "");

                gl.Add(stock_without_txt);
            }
        }


        public static char read_코스피코스닥시장구분(string stock)
        {
            _cpcodemgr = new CPUTILLib.CpCodeMgr();
            _cpstockcode = new CPUTILLib.CpStockCode();
            int marketKind = (int)_cpcodemgr.GetStockMarketKind(_cpstockcode.NameToCode(stock));
            if (marketKind == 1)

            {
                return 'S';

            }
            else if (marketKind == 2)
            {
                return 'D';
            }
            else
                return 'N';
        }


        public static double ComputeCoeff(string stockname, double[] values1, double[] values2)
        {
            if (values1.Length != values2.Length)
            {
                throw new ArgumentException("values must be the same length");
            }


            var avg1 = values1.Average();
            var avg2 = values2.Average();

            var sum1 = values1.Zip(values2, (x1, y1) => (x1 - avg1) * (y1 - avg2)).Sum();

            var sumSqr1 = values1.Sum(x => Math.Pow((x - avg1), 2.0));
            var sumSqr2 = values2.Sum(y => Math.Pow((y - avg2), 2.0));

            var result = sum1 / Math.Sqrt(sumSqr1 * sumSqr2);

            return result;
        }

        //     public static int read_외인기관일별매수동향(string stockname, g.stock data)
        //     {
        //         /* 양매수신뢰도 // 상관
        // 양매수수 // 전일 1, 2 전전일 3,4, 등
        // 외량 // 16거래일 외인거래량 
        // 기량 // 16거래일 기관거래량
        // 개량 // 16거래일 개인거래량
        //*/

        //         string path = @"C:\Work\매\" + stockname + ".txt";
        //         // 16일 데이터만 있는 데 신규는 적을 수 있음
        //         if (!File.Exists(path))
        //         {
        //             //string t = "매 file not exist " + stockname;
        //             //MessageBox.Show(t);
        //             return -1;
        //         }

        //         string[] grlines = System.IO.File.ReadAllLines(path, Encoding.Default);

        //         int count_매 = 0;
        //         int last_day_매 = 0;
        //         foreach (var line in grlines)
        //         {
        //             string[] words = line.Split(' ');
        //             if (count_매 == 0)
        //                 last_day_매 = Convert.ToInt32(words[0]);

        //             data.개량[count_매] = Convert.ToInt32(words[1]);
        //             data.외량[count_매] = Convert.ToInt32(words[2]);
        //             data.기량[count_매] = Convert.ToInt32(words[3]);
        //             count_매++;
        //         }

        //         path = @"C:\Work\일\" + stockname + ".txt";
        //         if (!File.Exists(path))
        //         {
        //             //string t = "일 file not exist " + stockname;
        //             //MessageBox.Show(t);
        //             return -1;
        //         }


        //         if (count_매 < 16)
        //         {
        //             count_매--;
        //         }
        //         List<string> lines = File.ReadLines(path).Reverse().Take(count_매 + 1).ToList();
        //         double[] pricediffer = new double[count_매];
        //         double[] instandfrig = new double[count_매];
        //         double[] closeprice = new double[count_매 + 1];
        //         double[] day_amount = new double[count_매 + 1];

        //         int count_일 = 0;
        //         int last_day_일 = 0;
        //         foreach (var line in lines)
        //         {
        //             string[] words = line.Split(' ');
        //             if (count_일 == 0)
        //                 last_day_일 = Convert.ToInt32(words[0]);
        //             closeprice[count_일] = Convert.ToDouble(words[4]);
        //             day_amount[count_일] = Convert.ToDouble(words[5]);
        //             count_일++;
        //         }
        //         if (last_day_일 != last_day_매)
        //         {
        //             //string t = "일 매 파일들의 마지막 날짜 불일치 " + stockname;
        //             //MessageBox.Show(t);
        //             return -1;
        //         }

        //         for (int i = 0; i < count_매; i++)
        //         {
        //             if (day_amount[i] == 0)
        //             {
        //                 continue;
        //             }
        //             pricediffer[i] = closeprice[i] - closeprice[i + 1];
        //             instandfrig[i] = data.개량[i] * -1.0 / day_amount[i];
        //             // 당일 개인 매도량 나누기 당일거래량
        //         }

        //         data.양매수신뢰도 = ComputeCoeff(stockname, pricediffer, instandfrig);


        //         if (data.외량[0] > 0 || data.기량[0] > 0)
        //             data.양매수수 = 1;
        //         if (data.외량[0] > 0 && data.기량[0] > 0)
        //             data.양매수수 = 2;
        //         if (data.외량[0] > 0 && data.기량[0] > 0 && (data.외량[1] > 0 || data.기량[1] > 0))
        //             data.양매수수 = 3;
        //         if (data.외량[0] > 0 && data.기량[0] > 0 && data.외량[1] > 0 && data.기량[1] > 0)
        //             data.양매수수 = 4;

        //         return 0;
        //     }


        public static string calcurate_종목일중변동평균편차(string stock, int days, ref double avr, ref double dev, ref int 일평균거래액_억원, 
                                                                            ref int 일최소거래액_억원, ref int 일최대거래액_억원)
        {
            string path = @"C:\Work\일\\" + stock + ".txt";
            if (!File.Exists(path))
                return " ";

            List<string> lines = File.ReadLines(path).Reverse().Take(days).ToList();

            List<Double> list = new List<Double>();
            
            int 일거래액;
            int 일거래량;
            일평균거래액_억원 = 0;
            일최대거래액_억원 = 0;           // 단위 억원
            일최소거래액_억원 = 1000000; // 단위 억원

            foreach (var line in lines)
            {
                string[] words = line.Split(' ');
                double start_price = Convert.ToDouble(words[1]); // 시가
                double close_price = Convert.ToDouble(words[4]); // 종가

   
                일거래량 = Convert.ToInt32(words[5]); // 누적거래량, the last day -> the first

                일거래액 = (int) (일거래량 * close_price / g.억원);
                일평균거래액_억원 += 일거래액;
                if (일거래액 > 일최대거래액_억원)
                    일최대거래액_억원 = 일거래액;
                if (일거래액 < 일최소거래액_억원)
                    일최소거래액_억원 = 일거래액;

                double diff = (close_price - start_price) / start_price * 100;

                list.Add(diff);
            }

            일평균거래액_억원 = 일평균거래액_억원 / list.Count;
            double temp_avr = 0.0;
            dev = 0.0;
            if (list.Count > 0)
            {
                temp_avr = list.Sum() / list.Count;
                if (list.Count <= 1)
                    dev = 0;
                else
                    dev = Math.Sqrt(list.Sum(x => Math.Pow(x - temp_avr, 2)) / (list.Count - 1));
            }

            string str = temp_avr.ToString("0.#") + "/" + dev.ToString("0.#");
            avr = temp_avr;
            return str;
        }


        public static int read_데이터컬럼들 (string filename, int[] c_id, string[,] x)
        {
            /* 파일이름, 구하고자하는 컬럼 번호를 주면 x[,] 저장 nrow 반환
	   * public static int read_데이터컬럼들
		 (string filename, int[] c_id, string[,] x)
	   * */

            if (!File.Exists(filename)) return -1;
            string[] grlines = System.IO.File.ReadAllLines(filename, Encoding.Default);

            int nrow = 0;
            foreach (string line in grlines)
            {
                List<string> alist = new List<string>();

                string[] words = line.Split(' ');
                for (int k = 0; k < c_id.Length; k++)
                {
                    x[nrow, k] = words[c_id[k]];
                }
                nrow++;


            }
            return nrow;
        }


        public static void read_변수()
        {
            string filename = @"C:\Work\변수.txt";
            if (!File.Exists(filename)) return;
            string[] grlines = System.IO.File.ReadAllLines(filename, Encoding.Default);


            foreach (string line in grlines)
            {
                var words = line.Split('\t');

                switch(words[0])
                {
                    //case "textbox_date_char_to_string":
                    //    라인분리(line, ref g.v.textbox_date_char_to_string);
                    //    break;
                    case "files_to_open_by_clicking_edge":
                        라인분리(line, ref g.v.files_to_open_by_clicking_edge);
                        break;
                    case "q_advance_lines":
                        라인분리(line, ref g.v.q_advance_lines);
                        break;
                    case "Q_advance_lines":
                        라인분리(line, ref g.v.Q_advance_lines);
                        break;
                    case "r3_display_lines":
                        라인분리(line, ref g.v.r3_display_lines);
                        break;
                    case "kospi_difference_for_sound":
                        라인분리(line, ref g.v.kospi_difference_for_sound);
                        break;
                    case "kosdq_difference_for_sound":
                        라인분리(line, ref g.v.kosdq_difference_for_sound);
                        break;



                    case "dev":
                        라인분리(line, ref g.s.dev);
                        break;
                    case "mkc":
                        라인분리(line, ref g.s.mkc);
                        break;

                    case "돌파":
                        라인분리(line, ref g.s.돌파);
                        break;
                    case "눌림":
                        라인분리(line, ref g.s.눌림);
                        break;

                    case "가연":
                        라인분리(line, ref g.s.가연);
                        break;
                    case "가분":
                        라인분리(line, ref g.s.가분);
                        break;
                    case "가틱":
                        라인분리(line, ref g.s.가틱);
                        break;
                    case "가반":
                        라인분리(line, ref g.s.가반);
                        break;
                    case "가지":
                        라인분리(line, ref g.s.가지);
                        break;

                    case "수연":
                        라인분리(line, ref g.s.수연);
                        break;
                    case "수지":
                        라인분리(line, ref g.s.수지);
                        break;
                    case "수위":
                        라인분리(line, ref g.s.수위);
                        break;

                    case "강연":
                        라인분리(line, ref g.s.강연);
                        break;
                    case "강지":
                        라인분리(line, ref g.s.강지);
                        break;
                    case "강위":
                        라인분리(line, ref g.s.강위);
                        break;


                    case "프분":
                        라인분리(line, ref g.s.프분);
                        break;
                    case "프틱":
                        라인분리(line, ref g.s.프틱);
                        break;
                    case "프지":
                        라인분리(line, ref g.s.프지);
                        break;
                    case "프퍼":
                        라인분리(line, ref g.s.프퍼);
                        break;
                    case "프일":
                        라인분리(line, ref g.s.프일);
                        break;
                    case "프가":
                        라인분리(line, ref g.s.프가);
                        break;

                    case "거분":
                        라인분리(line, ref g.s.거분);
                        break;
                    case "거틱":
                        라인분리(line, ref g.s.거틱);
                        break;
                    case "거일":
                        라인분리(line, ref g.s.거일);
                        break;

                    case "배차":
                        라인분리(line, ref g.s.배차);
                        break;
                    case "배반":
                        라인분리(line, ref g.s.배반);
                        break;

                    case "표편":
                        라인분리(line, ref g.s.표편);
                        break;

                    case "급락":
                        라인분리(line, ref g.s.급락);
                        break;
                    case "잔잔":
                        라인분리(line, ref g.s.잔잔);
                        break;

                    case "그룹":
                        라인분리(line, ref g.s.그룹);
                        break;

                    default:
                        break;
                }
            }
        }


        

        public static void 라인분리(string line, ref int data) // scalar -> no 비중, dev, mkc
        {
            string[] words = line.Split('\t');
            Int32.TryParse(words[1], out data);
        }

        public static void 라인분리(string line, ref string[] data) // single vector -> no 비중, dev, mkc
        {
            string[] words = line.Split('\t');

            for (int i = 1; i < words.Length; i++)
            {
                data[i - 1] = words[i];
            }
        }

        public static void 라인분리(string line, ref int [] data) // single vector -> no 비중, dev, mkc
        {
            string[] words = line.Split('\t');

            for (int i = 1; i < words.Length; i++)
            {
                bool success = false;
                success = Int32.TryParse(words[i], out data[i - 1]);
                if (!success)
                    return;
            }
        }

        public static void 라인분리(string line, ref List<List<double>> data) // double list, with 비중, dev. mkc
        {
            data.Clear();

            string[] words = line.Split('\t');

            List<double> t = new List<double>();
            for (int i = 1; i < words.Length; i++)
            {
                if (words[i].Contains("//"))
                    break;

                string[] items = words[i].Split('/');

                if (items.Length == 1)
                {
                    double a;
                    bool success = false;
                    success = double.TryParse(items[0], out a);

                    if (success)
                        t = new List<double> { a, 0.0 };
                    else
                        continue;
                }

                if (items.Length == 2)
                {
                    double a, b;

                    bool success_1 = false;
                    success_1 = double.TryParse(items[0], out a);

                    bool success_2 = false;
                    success_2 = double.TryParse(items[1], out b);
                    if (!success_2)
                        break;

                    if (success_1 && success_2)
                        t = new List<double> { a, b };
                    else
                        break;
                }
                data.Add(t);
            }

            // Integrity Check
            if(line.Contains("dev") || line.Contains("mkc") || line.Contains("잔잔"))
            {
            }
            else
            {
                bool error_exist = false;
                for (int i = 2; i < data.Count - 1; i++)
                {
                    if (data[i + 1][0] < data[i][0])
                        error_exist = true;
                    if (data[i + 1][1] < data[i][1])
                        error_exist = true;
                }
                if(error_exist)
                    MessageBox.Show("Error in 변수 파일 : " + line);
            }

            // the following lines to check the integrity of data
            string path = @"C:\WORK\temp_변수.txt";
            StreamWriter sw = File.AppendText(path);

            string str = line;
            if(data.Count > 0)
                str += "\t" + data[data.Count - 1][0].ToString() + "/" + data[data.Count - 1][1].ToString() + 
                         "\t" + data.Count.ToString();
            sw.WriteLine("{0}", str);
            sw.Close();
        }



        /*
    public static void read_외인기관일별매수동향(string stock, g.stock data)
    {

    string path = @"C:\Work\매\" + stock + ".txt";
    if (!File.Exists(path))
    {
      return;
    }

    string[] grlines = System.IO.File.ReadAllLines(path, Encoding.Default);

    int inc = 0;
    int day = 0;
    foreach(var line in grlines)
    {
      string[] words = line.Split(' ');
      if (inc == 0)
        day = Convert.ToInt32(words[0]);

      data.개량[inc] = Convert.ToInt32(words[1]); 
      data.외량[inc] = Convert.ToInt32(words[2]);
      data.기량[inc] = Convert.ToInt32(words[3]);
      inc++;
    }

    path = @"C:\Work\일\" + stock + ".txt";
    if (!File.Exists(path))
      return;

    List<string> lines = File.ReadLines(path).Reverse().Take(inc+1).ToList();
    double[] pricediffer = new double[inc  ];
    double[] closeprice =  new double[inc+1];
    int add = 0;
    foreach (var line in lines)
    {
      string[] words = line.Split(' ');
      closeprice[add++]= Convert.ToDouble(words[4]);
    }

    double[] instandfrig = new double[16];
    for(int i = 0; i < inc; i++)
    {
      pricediffer[i] = closeprice[i] - closeprice[i + 1];
      instandfrig[i] = data.개량[i] * -1.0;
    }

    data.양매수신뢰도 = cr.ComputeCoeff(pricediffer, instandfrig);


    if (data.외량[0] > 0 || data.기량[0] > 0)
      data.양매수수 = 1;
    if (data.외량[0] > 0 && data.기량[0] > 0)
      data.양매수수 = 2;
    if (data.외량[0] > 0 && data.기량[0] > 0 && (data.외량[1] > 0 || data.기량[1] > 0))
      data.양매수수 = 3;
    if (data.외량[0] > 0 && data.기량[0] > 0 && data.외량[1] > 0 && data.기량[1] > 0)
      data.양매수수 = 4;

    }

        /*  Calling Method : 그룹에서 전체를 읽고 group & single 반환
    *  스펠링 미스에 대한 점검 name to code를 하지 않아 오류 가능
    *  List<List<string>> groupList = new List<List<string>>();
    List<string> singleList = new List<string>();

    FileLib.Class.read_그룹(singleList, groupList);
    */
        /* 24일 거래량 중 상, 하 2개씩 극단을 제외하고 일평균거래량 환산 
   *  public static int calculate_종목20일기준일평균거래량(string stock)
    */
        public static ulong calculate_종목20일기준일평균거래량(string stock)
		{
			// Extract column 5 from stock filename
			string filename = @"C:\WORK\일\" + stock + ".txt";
			int[] c_id = new int[1]; // number of columns needed
			string[,] x = new string[1000, 1]; // array declaration
			List<double> alist = new List<double>();
			int nrow = 0;
			double average;

			c_id[0] = 5; // everyday amount dealed 

			nrow = read_데이터컬럼들(filename, c_id, x);

			if (nrow < 0)
			{
				average = 0.0;
				return (ulong)average;
			}
			else if (nrow < 24)
			{
				double sum = 0.0;
				for (int k = 0; k < 24; k++)
					sum += Convert.ToDouble(x[k, 0]);

				average = sum / nrow;
			}
			else
			{
				// The last 24 Rows Extraction


				for (int k = nrow - 1; k > nrow - 25; k--)
					alist.Add(Convert.ToDouble(x[k, 0]));

				alist.Sort();

				// Use 20 data and Calcurate Average
				double sum = 0.0;
				for (int k = 2; k < alist.Count - 2; k++)
					sum += alist[k];

				average = sum / (alist.Count - 4.0);
			}

			return (ulong)average;
		}

		/* 주어진 date, 두 개의 시간 구간 time[0]에서 time[1]로 1분 씩 증가시키면서 주어진 x[time[0], col]의 x[,col]의 값
	   * 의 최대 차이, 최소 차이를 구하여 반환한다. 예를 들면 가격이 일정량 점프하였는 데 그 후 30분 내 점프한 값으로부터 
		 최대 얼마나 하락할 지 최대 얼마나 상승할 지 알아보는 루틴 */
		public static void read_멏분후_값차이_최대_최소(int[,] x, int start_time, int lapse_time, int col,
					 ref int maxpos, ref int max, ref int minpos, ref int min)
		{
			min = 10000;
			max = -10000;

			for (int t = start_time + 1; t < start_time + lapse_time; t++)
			{
				int diff = x[t, col] - x[start_time, col];

				if (max < diff)
				{
					maxpos = t;
					max = diff;
				}

				if (min > diff)
				{
					minpos = t;
					min = diff;
				}


				/*
				if (min < -70 && t != start_time + 1)
				{
				  return;
				}
				*/
			}
		}

        public static void write_on_temp(double [] a)
        {
            lock (g.lockObject)
            {
                string path = @"C:\WORK\temp.txt";
                StreamWriter sw = File.AppendText(path);

                string str = "";
                foreach (var t in a)
                {
                    str += "\t" + t.ToString();
                }

                sw.WriteLine("{0}", str);
                sw.Close();
            }
        }


        public static void write_on_temp(List<List<double>> double_list)
        {
            lock (g.lockObject)
            {
                string path = @"C:\WORK\temp.txt";
                StreamWriter sw = File.AppendText(path);

                string str = "";
                foreach(var t in double_list)
                {
                    str += t[0] + "/" + t[1] + "\t";
                }

                sw.WriteLine("{0}", str);
                sw.Close();
            }
        }


        public static void write_on_temp(string t)
        {
            lock (g.lockObject)
            {
                string time_now = DateTime.Now.ToString("hh:mm:ss.ffff");
                string path = @"C:\WORK\temp.txt";
                StreamWriter sw = File.AppendText(path);

                //sw.WriteLine("{0}\t{1}", t, time_now);
                sw.WriteLine("{0}", t);
                sw.Close();
            }
        }


        public static void write_on_temp(string [] t)
        {
            lock (g.lockObject)
            {
                string time_now = DateTime.Now.ToString("hh:mm:ss.ffff");
                string path = @"C:\WORK\temp.txt";
                StreamWriter sw = File.AppendText(path);

                // sw.WriteLine(time_now);
                for (int i = 0; i < t.Length; i++)
                {
                    sw.Write(t[i]);
                    //if(i < t.Length - 1)
                    //    sw.Write("\t");
                    //else
                    //    sw.Write("\n");
                }
                //sw.WriteLine("{0}\t{1}", t, time_now);
                sw.Close();
            }
		}


        public static void write_on_temp(List<string> GL_title, List<List<string>> GL)
        {
            lock (g.lockObject)
            {
                string path = @"C:\WORK\temp.txt";
                StreamWriter sw = File.AppendText(path);

                string str = "";
                for (int i = 0; i < GL.Count; i++)
                {
                    str += GL_title[i];
                    foreach (var u in GL[i])
                    {
                        str += "\t" + u + "\n";
                    }
                    str += "\n";
                }
               
                sw.WriteLine("{0}", str);
                sw.Close();
            }
        }

        public static void write_on_temp(int[,] x, int start_line, int end_line)
        {
            lock (g.lockObject)
            {
                string path = @"C:\WORK\temp.txt";
                StreamWriter sw = File.AppendText(path);

                for (int i = start_line; i <= end_line; i++)
                {
                    if (i < 0)
                    {
                        break;
                    }
                    for (int j = 0; j < 12; j++)
                    {
                        sw.Write("{0, 10}", x[i, j]);
                    }
                    sw.Write("\n");
                }
                sw.Close();
            }
        }

        public static void write_with_time_on_temp(string t)
        {
            lock (g.lockObject)
            {
                string time_now = DateTime.Now.ToString("hh:mm:ss.ffff");
                string path = @"C:\WORK\temp.txt";
                StreamWriter sw = File.AppendText(path);


                sw.WriteLine("{0}\t{1}", t, time_now);
                sw.Close();
            }
        }


        public static void write_with_time_on_temp(string[] t)
        {
            lock (g.lockObject)
            {
                string time_now = DateTime.Now.ToString("hh:mm:ss.ffff");
                string path = @"C:\WORK\temp.txt";
                StreamWriter sw = File.AppendText(path);

                sw.WriteLine(time_now);
                for (int i = 0; i < t.Length; i++)
                {
                    sw.WriteLine(t[i]);
                }
                sw.WriteLine("{0}\t{1}", t, time_now);
                sw.Close();
            }
        }
        

        

        public static void save_all_stocks()
        {
            if (g.testing) // never save in test
                return;

            string path = @"C:\WORK\분" + "\\" + g.date.ToString();
            Directory.CreateDirectory(path); //if Exist, will pass

            for (int i = 0; i < g.ogl_data.Count; i++)
            {
                g.stock t = g.ogl_data[i];

                string file = path + "\\" + t.종목 + ".txt";
                if (File.Exists(file)) // if file not exist, create new
                {
                    File.Delete(file);
                }
                int lastRow = 381;      // int lastRow = x.GetUpperBound(0);
                int lastColumn = 11;   //  int lastColumn = x.GetUpperBound(1);

                string str = "";
                for (int j = 0; j <= lastRow; j++) // bound exist not the size
                {
                    if (t.x[j, 0] == 0 || t.x[j, 0] > 152100) // if time is not set then stop writing
                        break;

                    for (int k = 0; k <= lastColumn; k++) // bound exist not the size
                    {
                        if (k == 0)
                            str += t.x[j, k];

                        else
                            str += "\t" + t.x[j, k];
                    }
                    str += "\n";
                }
                File.WriteAllText(file, str);
            }
            
        }

        public static int read_stock분데이터(int date, string stock, int[,] x)
		{
			if (date < 10)
			{
				DateTime now = DateTime.Now;
				date = Convert.ToInt32(now.ToString("yyyyMMdd"));
			}

			string file = @"C:\WORK\분\" + date.ToString() + "\\" + stock + ".txt";
			if (!File.Exists(file)) return -1;
			string[] grlines = System.IO.File.ReadAllLines(file, Encoding.Default);

			int nrow = 0;
			foreach (string line in grlines)
			{
				List<string> alist = new List<string>();

				string[] words = line.Split(' ');
				for (int k = 0; k < words.Length; k++)
				{
					if (k == 4)
					{
						x[nrow, k] = Convert.ToInt32((int)Convert.ToDouble(words[k]));
					}
					else
					{
						x[nrow, k] = Convert.ToInt32(words[k]);
					}
				}
				if (x[nrow, 0] < 10)
					break;
				else
					nrow++;
			}
			return nrow;
		}


        public static double 시분초_일중환산율(int hhmmss)
        {
            int hh = hhmmss / 10000;
            int mm = hhmmss % 10000 / 100;
            int ss = hhmmss % 100;

            double numerator = 6 * 60 * 60 + 21 * 60;
            double denominator = (hh - 9) * 60 * 60 + mm * 60 + ss;
            if (denominator < 0.1)
                denominator = 1.0;
            if (denominator > numerator)
                numerator = denominator;

            return numerator / denominator;
        }


        public static double 누적거래액환산율(int hhmm) 
        {
            double value = 0;
            if (hhmm > 10000) // if 6 digit is passed, make it 4 digit
                hhmm /= 100;

            int hh = Convert.ToInt32(hhmm) / 100;
            int mm = Convert.ToInt32(hhmm) % 100;

            if (hh >= 15)
            {
                if (mm > 20)
                {
                    mm = 20;
                }
            }
            value = (hh - 9) * 60 + mm + 1;
            if (value < 0 || value > 6 * 60 + 20) // 0900 ~ 1520 이외 시간에는 380 리턴
                value = 6 * 60 + 21;

            double return_value = 381.0 / value;
            return return_value;
        }


        public static int directory_분전후(int date_int, int updn)
		{
			var subdirs = Directory.GetDirectories(@"C:\Work\분")
				   .Select(Path.GetFileName).ToList();

            List<string> selected_subdirs = new List<string>();   // changing single list
        
            foreach (var item in subdirs)
            {
                if (item.Length == 8)
                   selected_subdirs.Add(item);
            }

			string date_string = date_int.ToString();
			int index = selected_subdirs.IndexOf(date_string);
				
			if (updn == 1)
			{
				if (index == selected_subdirs.Count - 1)
					return -1;
				date_string = selected_subdirs[++index];
			}
			if (updn == -1)
			{
				if (index == 0)
					return -1;
				date_string = selected_subdirs[--index];
			}

			return Convert.ToInt32(date_string);
		}




        public static string 종목포함_최고가그룹리스트_title(string finding_stock)
        {
            string title = "";
            double average_price = -3100;
            //List<string> alist = new List<string>();

            foreach (var sublist in g.oGL_data)
            {
                foreach (string item in sublist.stocks)
                {
                    if (item == finding_stock)
                    {
                        if(sublist.average_price > average_price)
                        {
                            //alist.Clear();
                            average_price = sublist.average_price;
                            title = sublist.title;
                            //alist = sublist.stocks.ToList();
                        }
                    }
                }
            }
            return title;
        }

        public static List<string> 종목포함_그룹리스트(string finding_stock)
		{
			List<string> alist = new List<string>();

			foreach (var sublist in g.oGL_data)
			{
				foreach (string item in sublist.stocks)
				{
					if (item == finding_stock)
					{
						foreach (var name in sublist.stocks)
						{
							alist.Add(name);
						}
					}

				}
			}
			return alist;
		}


		public static int data_컬럼2(string filepath, int dcol, int[,] x, int xcol)
		{ // dcol = 원하는 컬럼위치, 
			if (!File.Exists(filepath))
			{
				return -1;
			}

			string[] grlines = File.ReadAllLines(filepath, Encoding.Default);
			int nrow = 0;
			foreach (string line in grlines)
			{

				string[] words = line.Split(' ');
				if (words.Length == 1)
				{
					words = line.Split('\t');
				}






				if (dcol == 0)
				{
					// values are crossed, later rearrange ZZZ
					string[] time = words[dcol].Split(':');


					if (time.Length == 1)
					{
						x[nrow, xcol] = Convert.ToInt32(words[0]);
					}
					else
					{
						x[nrow, xcol] = Convert.ToInt32(time[0]) * 100 + Convert.ToInt32(time[1]);
					}
				}

				else
				{
					x[nrow, xcol] = Convert.ToInt32(words[dcol]);
				}
				nrow++;
				/*
				  x[nrow, 1] = Convert.ToInt32(words[1]);   // price
				  x[nrow, 2] = Convert.ToInt32(words[2]);   // amount
				  x[nrow, 3] = Convert.ToInt32(words[3]);   // intensity

				  x[nrow, 4] = Convert.ToInt32(words[4]);   // institue from marketeye
				  x[nrow, 5] = Convert.ToInt32(words[5]);   // foreign 
				  x[nrow, 6] = Convert.ToInt32(words[6]);   // foreign from marketeye

				  x[nrow, 7] = Convert.ToInt32(words[7]);   // total amount dealt
				  x[nrow, 8] = Convert.ToInt32(words[8]);   // buy multiple 10 times
				  x[nrow, 9] = Convert.ToInt32(words[9]);   // sell multiple 10 times

				  nrow++;
				  if (nrow >= 391)
				  break;
				}
				x[nrow++, xcol] = Convert.ToInt32(words[dcol]);
				*/
				if (nrow > 399)
					break;
			}
			return nrow;
		}


		public static double read_시총(string stock)
		{
			string[] grlines = File.ReadAllLines(@"C:\WORK\시총.txt", Encoding.Default);
			foreach (string line in grlines)
			{
				string[] words = line.Split(' ');
				string newname = words[0].Replace("_", " ");
				if (string.Equals(newname, stock))
				{
					return Convert.ToDouble(words[1]);
				}

			}
			return -1;
		}


        public static List<string> read_gl_GL(List<List<string>> GL) // currently no reference
        {
            List<string> 그룹제외 = new List<string>();
            그룹제외 = read_그룹제외();

            List<string> gl_list = new List<string>();

            for (int i = 0; i < 2; i++)
            {
                string file;
                if (i == 0)
                {
                    file = @"C:\WORK\그룹 상관 원본.txt"; // if i == 0
                }
                else
                {
                    file = @"C:\WORK\그룹.txt";
                }


                if (!File.Exists(file))
                {
                    MessageBox.Show(file + " Not Exist");
                    continue;
                }

                string[] grlines = File.ReadAllLines(file, Encoding.Default);

                List<string> temp_list = new List<string>();
                foreach (string line in grlines)
                {
                    string[] words = line.Split(' '); // empty spaces also recognized as words, word.lenght can be larger than 4

                    if ((words[0] == "" || words[0].Contains("//")) && temp_list.Count >= 1) // && i == 0)
                    {
                        // if (temp_list.Count >= 2) // 그룹 상관 및 그룹, 2개 이상의 종목으로 구성된 그룹. if temp_list.Count = 1, add to gl not GL // 20210411
                        if (temp_list.Count >= 2 && i == 0) // // 그룹 상관, 2개 이상의 종목으로 구성된 그룹.  if temp_list.Count = 1, add to gl not GL
                        {
                            GL.Add(temp_list.ToList()); // 임시저장으로
                        }
                        temp_list.Clear();
                    }

                    foreach (string stock in words)
                    {
                        if (stock == "") // \N code check needed for misspelling
                            continue;

                        if (stock.Contains("//")) // \N code check needed for misspelling
                            break;

                        string newname = stock.Replace("_", " ");

                        if (gl_list.Contains(newname)) // if stock is already added to above list, then skip 
                            continue;

                        if (그룹제외.Contains(newname))
                            continue;

                        temp_list.Add(newname); // large group GL 
                        gl_list.Add(newname); // for gl
                    }
                }

                if (temp_list.Count >= 2 && i == 0) // 그룹 상관, 2개 이상의 종목으로 구성된 그룹
                {
                    GL.Add(temp_list.ToList()); // 임시저장으로
                }
            }

            var uniqueItemsList = gl_list.Distinct().ToList();
            return uniqueItemsList;
        }


        // 20200408
        //public static int read_stock2(int date, string stock, int[,] x)
        //{
        //	if (date < 0)
        //	{
        //		DateTime now = DateTime.Now;
        //		date = Convert.ToInt32(now.ToString("yyyyMMdd"));
        //	}

        //	string file = @"C:\WORK\분\" + date.ToString() + "\\" + stock + ".txt";
        //	if (!File.Exists(file))
        //	{
        //		return -1;
        //	}
        //	Stream sf = System.IO.File.Open(file,
        //			FileMode.Open,
        //			FileAccess.Read,
        //			FileShare.ReadWrite);

        //	StreamReader sr = new StreamReader(sf);

        //	string line;
        //	int nrow = 1;

        //	while ((line = sr.ReadLine()) != null)
        //	{
        //		string[] words = line.Split(' ');

        //		int hhmm = Convert.ToInt32(words[0]);
        //		if (hhmm < g.start_minute + 1 || hhmm > 1530)
        //			continue;

        //		x[nrow, 0] = hhmm;  // time
        //		x[nrow, 1] = Convert.ToInt32(words[1]);
        //		x[nrow, 2] = Convert.ToInt32(words[2]);
        //		x[nrow, 3] = Convert.ToInt32(words[3]);

        //		nrow++;
        //	}
        //	x[0, 0] = g.start_minute;
        //	for (int k = 0; k < 3; k++)
        //		x[0, k + 1] = x[1, k + 1]; //수정
        //	x[nrow, 0] = 0;
        //	return 0;
        //}


        //public static int read_stock_marketeye(int date, string stock, double[,] x)
        //{
        //	if (date < 0)
        //	{
        //		DateTime now = DateTime.Now;
        //		date = Convert.ToInt32(now.ToString("yyyyMMdd"));
        //	}

        //	string file = @"C:\WORK\분\" + date.ToString() + "\\" + stock + ".txt";
        //	if (!File.Exists(file))
        //	{
        //		return -1;
        //	}
        //	Stream sf = System.IO.File.Open(file,
        //			FileMode.Open,
        //			FileAccess.Read,
        //			FileShare.ReadWrite);

        //	StreamReader sr = new StreamReader(sf);

        //	string line;
        //	int nrow = 0;
        //	int numberofcolumn = 0;

        //	while ((line = sr.ReadLine()) != null)
        //	{
        //		string[] words = line.Split(' ');
        //		if (words.Length == 1)
        //		{
        //			words = line.Split('\t');
        //		}

        //		numberofcolumn = words.Length;

        //		int hhmm = Convert.ToInt32(words[0]);
        //		if (hhmm < g.start_minute + 1 || hhmm > 1530)
        //			continue;

        //		x[nrow, 0] = (double)hhmm;  // time
        //		x[nrow, 1] = Convert.ToDouble(words[1]);
        //		x[nrow, 2] = Convert.ToDouble(words[2]);
        //		x[nrow, 3] = Convert.ToDouble(words[3]);

        //		if (words.Length == 6)
        //		{
        //			x[nrow, 4] = Convert.ToDouble(words[4]);
        //			x[nrow, 5] = Convert.ToDouble(words[5]);

        //		}
        //		if (words.Length == 7)
        //		{
        //			x[nrow, 4] = Convert.ToDouble(words[4]);
        //			x[nrow, 5] = Convert.ToDouble(words[5]);
        //			x[nrow, 6] = Convert.ToDouble(words[6]);
        //		}
        //		else
        //		{

        //		}
        //		nrow++;
        //	}
        //	return numberofcolumn;
        //	/*
        //	x[0, 0] = 900;
        //	for (int k = 0; k < 3; k++)
        //	  x[0, k + 1] = x[1, k + 1]; //수정
        //	x[nrow, 0] = 0;
        //	return 0; */
        //}
        public static List<string> read_그룹제외()
		{
			List<string> gl_list = new List<string>();

			string filepath = @"C:\WORK\그룹 제외.txt";
			if (!File.Exists(filepath))
			{
				MessageBox.Show("그룹 제외.txt Not Exist");
				return gl_list;
			}
			string[] grlines = File.ReadAllLines(filepath);

			List<string> GL_list = new List<string>();

			foreach (string line in grlines)
			{
				string[] words = line.Split(' ');
				foreach (string stock in words)
				{
					if (stock == "") continue; // WN code check needed for misspelling
					string newname = stock.Replace("_", " ");

					if (gl_list.Contains(newname)) // if stock is already added to above list, then skip
					{
						continue;
					}
					gl_list.Add(newname); // for single
				}
			}
			var uniqueItemsList = gl_list.Distinct().ToList();
			return uniqueItemsList;
		}


        public static void read_KODEX()
        {
            // 종목별 배당된 주식숫자로부터 각 종목의 Weighting Factor 계산하여 저장
            string file = @"C:\WORK\KODEX.txt";
            if (!File.Exists(file))
            {
                MessageBox.Show("KODEX.txt Not Exist");
                return;
            }

            string[] grlines = File.ReadAllLines(file);
            List<string> list = new List<string>();

            bool empty_line_met = false;
            foreach (string line in grlines)
            {
                string[] words = line.Split('\t');
                if (words[0] == "")
                    empty_line_met = true;

                if (words[0].Length > 0)
                {
                    if (empty_line_met == false)
                        g.코스피합성.Add(words[0] + '\t' + words[1]);
                    else
                        g.코스닥합성.Add(words[0] + '\t' + words[1]);
                }
            }

            g.평불종목.Add("코스피혼합");
            g.평불종목.Add("코스닥혼합");

            g.지수종목.Clear();
            foreach (string t in g.코스피합성)
            {
                string[] words = t.Split('\t');
                g.지수종목.Add(words[0]);
            }
            foreach (string t in g.코스닥합성)
            {
                string[] words = t.Split('\t');
                g.지수종목.Add(words[0]);
            }

            g.지수종목.Add("KODEX 레버리지"); // 0504
            g.지수종목.Add("KODEX 200선물인버스2X");
            g.지수종목.Add("KODEX 코스닥150레버리지");
            g.지수종목.Add("KODEX 코스닥150선물인버스");

        }


        public static void read_그룹_네이버_테마(List<string> tsl, List<string> tsl_그룹_상관, List<string> GL_title, List<List<string>> GL)
        {
            string filepath = @"C:\WORK\그룹_네이버_테마.txt";
            if (!File.Exists(filepath))
                return;

            var lines = File.ReadAllLines(filepath, Encoding.Default);

            List<List<string>> temp_GL = new List<List<string>>(); // temporary working space for group list
            List<string> temp_Gl = new List<string>();

            foreach (var item in lines)
            {
                if (item == "" && temp_Gl.Count > 1)
                {
                    GL_title.Add(temp_Gl[0]);
                    temp_Gl.RemoveAt(0);
                    GL.Add(temp_Gl.ToList());
                    
                    foreach (var stock in temp_Gl)
                    {
                        if (!tsl.Contains(stock))
                            tsl.Add(stock);
                    }
                    temp_Gl.Clear();
                }
                if(item != "" && !tsl_그룹_상관.Contains(item))
                    temp_Gl.Add(item);
            }
        }




        

        //public static void read_그룹_네이버_테마(List<string> gl, List<string> tsl_그룹_상관, List<string> GL_title, List<List<string>> GL)
        //{
        //    List<string> temp_Gl = new List<string>();

        //    string filepath = @"C:\WORK\그룹_네이버_테마.txt";
        //    if (!File.Exists(filepath))
        //    {
        //        return;
        //    }
        //    string[] grlines = File.ReadAllLines(filepath, Encoding.Default);

        //    bool blank_read = true;
        //    foreach (string item in grlines)
        //    {
        //        if (blank_read == true)
        //        {
        //            if (item == "")
        //                continue;
        //            else
        //            {
        //                if (!tsl_그룹_상관.Contains(item))
        //                {
        //                    GL_title.Add(item);
        //                    blank_read = false;
        //                }
        //                else
        //                {
        //                    int a = 1;
        //                }
        //                continue;
        //            }
        //        }
        //        else
        //        {
        //            if (item == "")
        //            {
        //                GL.Add(temp_Gl.ToList()); // independant copy
        //                temp_Gl.Clear(); // clear temporary small group
        //                blank_read = true;
        //                continue;
        //            }
        //        }
        //        if (!gl.Contains(item))
        //            gl.Add(item); // for single
        //        if (!temp_Gl.Contains(item))
        //            temp_Gl.Add(item); // for small group
        //    }
        //}

        public static void read_그룹_상관(List<string> gl, List<string> GL_title, List<List<string>> GL)
		{
            string file = @"C:\WORK\그룹 상관.txt"; // if i == 0
            if (!File.Exists(file))
            {
                MessageBox.Show(file + " Not Exist");
                return;
            }
            string[] grlines = File.ReadAllLines(file, Encoding.Default);

            List<string> temp_gl = new List<string>();
            List<string> temp_Gl = new List<string>();
            string temp_title = "";

            foreach (var line in grlines)
            {
                string[] words = line.Split(' ');

                if (words[0].Contains("//"))
                {
                    if (temp_Gl.Count >= 2)
                    {
                        if (!GL_title.Contains(temp_title))
                        {
                            GL_title.Add(temp_title);
                            GL.Add(temp_Gl.ToList());
                        }
                    }
                    temp_Gl.Clear();
                    if(words.Length > 1)
                        temp_title = words[1];

                    continue;
                }
                else
                {
                    string new_name = "";
                    foreach (string stock in words)
                    {
                        if (stock == "//") // not a first word, then go to next line
                            break;

                        new_name = stock.Replace("_", " ");
                        if (!is_stock(new_name))
                            continue;

                        if(!temp_Gl.Contains(new_name))
                            temp_Gl.Add(new_name); // large group GL 
                        if(!gl.Contains(new_name))
                            gl.Add(new_name);
                    }
                }
            }

            if (temp_Gl.Count >= 2) // 그룹 상관, 2개 이상의 종목으로 구성된 그룹
            {
                GL_title.Add(temp_title);
                GL.Add(temp_Gl.ToList()); // 임시저장으로
            }
        }

        //public static void read_그룹_상관(List<string> gl, List<string> GL_title, List<List<string>> GL)
        //{
        //    string file = @"C:\WORK\그룹 상관.txt"; // if i == 0
        //    if (!File.Exists(file))
        //    {
        //        MessageBox.Show(file + " Not Exist");
        //        return;
        //    }
        //    string[] grlines = File.ReadAllLines(file, Encoding.Default);

        //    List<string> temp_gl = new List<string>();
        //    List<string> temp_Gl = new List<string>();
        //    string temp_title = "";

        //    foreach (var line in grlines)
        //    {
        //        string[] words = line.Split(' ');

        //        if (words[0].Contains("//"))
        //        {
        //            temp_title = words[1];
        //            continue;
        //        }
        //        else
        //        {
        //            if (words[0] == "")
        //            {
        //                if (temp_Gl.Count >= 2)
        //                {
        //                    if (!GL_title.Contains(temp_title))
        //                    {
        //                        GL_title.Add(temp_title);
        //                        GL.Add(temp_Gl.ToList());
        //                    }
        //                    temp_Gl.Clear();
        //                }
        //            }
        //            string new_name = "";
        //            foreach (string stock in words)
        //            {
        //                if (stock == "//") // not a first word, then go to next line
        //                    break;

        //                new_name = stock.Replace("_", " ");
        //                if (!is_stock(new_name))
        //                    continue;


        //                if (!temp_Gl.Contains(new_name))
        //                    temp_Gl.Add(new_name); // large group GL 
        //                if (!gl.Contains(new_name))
        //                    gl.Add(new_name);
        //            }
        //        }
        //    }

        //    if (temp_Gl.Count >= 2) // 그룹 상관, 2개 이상의 종목으로 구성된 그룹
        //    {
        //        GL.Add(temp_Gl.ToList()); // 임시저장으로
        //    }
        //}


        public static void gen_oGL_data(List<string> oGL_title, List<List<string>> oGL)
		{
			// rearrange oGL and genereate oGl by market cap
            foreach(var t in g.ogl_data) // assign -1 as default
            {
                t.oGL_sequence_id = -1; // needed ?
            }

			int oGL_count = 0;  // oGL_sequence_id will be assigned step by step

            for (int i = 0; i < oGL.Count; i++)
            {
                if (oGL[i].Count < 2)
                    continue;

                var data = new g.group_data();

                var items1 = new List<Tuple<double, string>> { };
                foreach (var stock in oGL[i])
                {
                    int index = g.ogl_data.FindIndex(r => r.종목 == stock);
                    if (index >= 0)
                    {
                        g.ogl_data[index].oGL_sequence_id = oGL_count; // needed ?
                        items1.Add(Tuple.Create(g.ogl_data[index].시총, stock)); // 시총순으로 그룹 내 종목 재배열
                    }
                    else
                    {
                        continue;
                    }
                }
                items1 = items1.OrderByDescending(t => t.Item1).ToList();
                List<string> list = new List<string>();
                foreach (var item in items1)
                {
                    list.Add(item.Item2);
                }
                if (list.Count < 2)
                    continue;

                data.stocks = list.ToList();
                data.title = oGL_title[i];

                g.oGL_data.Add(data);
                oGL_count++;
            }
		}


        public static int Read_Stock_Seven_Lines_Reverse(int date, string stock, int nline, int[,] x)
        {
            if (date < 0)
            {
                DateTime now = DateTime.Now;
                date = Convert.ToInt32(now.ToString("yyyyMMdd"));
            }

            string file = @"C:\WORK\분\" + date.ToString() + "\\" + stock + ".txt";
            if (!File.Exists(file))
                return -1;

            int n = File.ReadAllLines(file).Length;
            if (n < nline)
            {
                nline = n;
            }
            List<string> lines = File.ReadLines(file).Reverse().Take(nline).ToList();

            // Error Prone : Below 8 lines not Tested Exactly
            if (g.testing)
            {
                if (n < g.time[1] + 1)
                {
                    g.time[1] = n;
                }
                lines = File.ReadLines(file).Skip(g.time[1] - nline).Take(nline).Reverse().ToList();
            }

            int nrow = 0;
            foreach (var line in lines)
            {
                string[] words = line.Split('\t');
                if (words.Length == 1)
                {
                    words = line.Split(' ');
                }

                // values are crossed, later rearrange ZZZ
                string[] time = words[0].Split(':');
                if (time.Length == 1)
                {
                    x[nrow, 0] = Convert.ToInt32(words[0]);
                }
                else
                {
                    x[nrow, 0] = Convert.ToInt32(time[0]) * 100 + Convert.ToInt32(time[1]);
                }

                x[nrow, 1] = Convert.ToInt32(words[1]);   // price
                x[nrow, 2] = Convert.ToInt32(words[2]);   // amount
                x[nrow, 3] = Convert.ToInt32(words[3]);   // intensity

                x[nrow, 4] = Convert.ToInt32(words[4]);   // institue from marketeye
                x[nrow, 5] = Convert.ToInt32(words[5]);   // foreign 
                x[nrow, 6] = Convert.ToInt32(words[6]);   // foreign from marketeye

                x[nrow, 7] = Convert.ToInt32(words[7]);   // total amount dealt
                x[nrow, 8] = Convert.ToInt32(words[8]);   // buy multiple 10 times
                x[nrow, 9] = Convert.ToInt32(words[9]);   // sell multiple 10 times

                nrow++;
                if (nrow == nline) // can not exceeed the maximum allocation of x[nline,]
                {
                    break;
                }
            }
            return nrow;
        }


        // This is for eval. of stock for testing purpose
        public static int Read_Stock_Seven_Lines_Reverse_From_Endtime
            (int date, string stock, int end_time, int nline, int[,] x)
        {
            string file = @"C:\WORK\분\" + date.ToString() + "\\" + stock + ".txt";
            if (!File.Exists(file))
                return -1;

            int n = File.ReadAllLines(file).Length;

            if (end_time > n)
            {
                end_time = n;
            }
            else
            {
                if (end_time < nline)
                {
                    nline = end_time;
                }
            }


            //end_time - 1 : - 1 because start from end_time to reverse
            // Skip, Reverse, Take does not work, Reverse is done by using Reverse.array();
            var lines = File.ReadLines(file).Skip(end_time - nline).Take(nline);

            n = nline - 1; // to reverse

            int nrow = 0;
            foreach (var line in lines)
            {
                string[] words = line.Split('\t');
                if (words.Length == 1)
                {
                    words = line.Split(' ');
                }

                // values are crossed, later rearrange ZZZ
                string[] time = words[0].Split(':');
                if (time.Length == 1)
                {
                    x[n, 0] = Convert.ToInt32(words[0]);
                }
                else
                {
                    x[n, 0] = Convert.ToInt32(time[0]) * 100 + Convert.ToInt32(time[1]);
                }

                x[n, 1] = Convert.ToInt32(words[1]);   // price
                x[n, 2] = Convert.ToInt32(words[2]);   // amount
                x[n, 3] = Convert.ToInt32(words[3]);   // intensity

                x[n, 4] = Convert.ToInt32(words[4]);   // institue from marketeye
                x[n, 5] = Convert.ToInt32(words[5]);   // foreign 
                x[n, 6] = Convert.ToInt32(words[6]);   // foreign from marketeye

                x[n, 7] = Convert.ToInt32(words[7]);   // total amount dealt
                x[n, 8] = Convert.ToInt32(words[8]);   // buy multiple 10 times
                x[n, 9] = Convert.ToInt32(words[9]);   // sell multiple 10 times

                n--;

                nrow++;
            }

            //x[nrow, 0] = 0; // used to check the end of data, no need actually
            //if (x[nrow - 1, 1] == 0 && x[nrow - 1, 2] == 0 && x[nrow - 1, 3] == 0) // maybe trading suspended
            //{
            //    nrow = 0;
            //}

            return nrow; // nrow = nline as the result from above
        }


        public static void Write_Stock_Minute(int date, string stock, int[,] x)
        {
            string file = @"C:\WORK\분\" + date.ToString() + "\\" + stock + ".txt";
            if (File.Exists(file))
                File.Delete(file);

            int lastRow = x.GetUpperBound(0);
            int lastColumn = x.GetUpperBound(1);

            string str = "";
            for (int j = 0; j <= lastRow; j++) // bound exist not the size
            {
                if (x[j, 0] == 0 || x[j, 0] > 152100) // if time is not set then stop writing
                    break;

                for (int k = 0; k <= lastColumn; k++) // bound exist not the size
                {
                    if (k == 0)
                        str += x[j, k];
                    else
                        str += "\t" + x[j, k];
                }
                str += "\n";
            }
            File.WriteAllText(file, str);
        }


        public static int Read_Stock_Minute(int date, string stock, int[,] x)
        {
            if (date < 0)
            {
                DateTime now = DateTime.Now;
                date = Convert.ToInt32(now.ToString("yyyyMMdd"));
            }

            string file = @"C:\WORK\분\" + date.ToString() + "\\" + stock + ".txt";
            if (!File.Exists(file))
            {
                return 0;
            }
               

            
            string[] lines = File.ReadAllLines(file, Encoding.Default);
            if(lines.Length == 0)
            {
                return 0;
            }

            int nrow = 0;
            foreach (string line in lines)
            {
                string[] words = line.Split('\t');
                if (words.Length == 1)
                {
                    words = line.Split(' ');
                }

                // values are crossed, later rearrange ZZZ
                string[] time = words[0].Split(':');
                if (time.Length == 1)
                {
                    x[nrow, 0] = Convert.ToInt32(words[0]); // words[0] = time[0], no difference
                }
                else
                {
                    x[nrow, 0] = Convert.ToInt32(time[0]) * 10000 + Convert.ToInt32(time[1]) * 100 + Convert.ToInt32(time[2]);
                }

                x[nrow, 1] = Convert.ToInt32(words[1]);   // price
                x[nrow, 2] = Convert.ToInt32(words[2]);   // amount
                x[nrow, 3] = Convert.ToInt32(words[3]);   // intensity

                x[nrow, 4] = Convert.ToInt32(words[4]);   // institue from marketeye
                x[nrow, 5] = Convert.ToInt32(words[5]);   // foreign 
                x[nrow, 6] = Convert.ToInt32(words[6]);   // foreign from marketeye

                x[nrow, 7] = Convert.ToInt32(words[7]);   // total amount dealt
                x[nrow, 8] = Convert.ToInt32(words[8]);   // buy multiple 10 times
                x[nrow, 9] = Convert.ToInt32(words[9]);   // sell multiple 10 times

                if (words.Length == 12)
                {
                    x[nrow, 10] = Convert.ToInt32(words[10]);   // buy multiple 10 times
                    x[nrow, 11] = Convert.ToInt32(words[11]);   // sell multiple 10 times
                }
                nrow++;
                if (nrow == g.MAX_ROW)
                    break;
                //if (x[nrow - 1 , 0] > 152100) //0505 from nrow > 390 to current if
                //{
                //    x[nrow - 1, 0] = 0;
                //    nrow--;
                //    break;
                //}
            }
            for (int i = nrow; i < g.MAX_ROW; i++)
            {
                for (int j = 0; j < 12; j++)
                {
                    x[i, j] = 0;
                }
            }

            //x[nrow, 0] = 0; // used to check the end of data, no need actually
            //if (x[nrow - 1, 1] == 0 && x[nrow - 1, 2] == 0 && x[nrow - 1, 3] == 0) // maybe trading suspended, 아래 3 라인이 없으면 축 개체 문제 발생
            //{
            //    nrow = 0;
            //}

            if (g.dl.Count == 4) // 코스피혼합 & 코스닥혼합 인버스로 만들기 위해 가격 X -1 & 매수배수 매도배수 Swap
            {
                if ((g.dl[0] == "KODEX 200선물인버스2X" && stock == "코스피혼합") ||
                   (g.dl[2] == "KODEX 코스닥150선물인버스" && stock == "코스닥혼합"))
                {
                    for (int i = 0; i < nrow; i++)
                    {
                        x[i, 1] *= -1;
                        Swap(ref x[i, 8], ref x[i, 9]);
                    }
                }
            }

            //if(stock == "KODEX 레버리지" || 
            //  stock == "KODEX 200선물인버스2X" ||
            //  stock == "KODEX 코스닥150레버리지" ||
            //  stock == "KODEX 코스닥150선물인버스")
            //{
            //    for (int i = 0; i < g.money_shift; i++)
            //    {
            //        x[nrow + i, 4] = x[nrow - 1, 4];
            //        x[nrow + i, 5] = x[nrow - 1, 5];
            //        x[nrow + i, 6] = x[nrow - 1, 6];
            //        x[nrow + i, 10] = x[nrow - 1, 10];
            //        x[nrow + i, 11] = x[nrow - 1, 11];
            //    }
            //    for (int i = 0; i < nrow; i++)
            //    {
            //        x[i, 4] = x[i + g.money_shift, 4];
            //        x[i, 5] = x[i + g.money_shift, 5];
            //        x[i, 6] = x[i + g.money_shift, 6];
            //        x[i, 10] = x[i + g.money_shift, 10];
            //        x[i, 11] = x[i + g.money_shift, 11];
            //    }
            //}


            return nrow;
        }


        public static int Read_Stock_Minute_no_multiply(int date, string stock, int[,] x)
		{
			if (date < 0)
			{
				DateTime now = DateTime.Now;
				date = Convert.ToInt32(now.ToString("yyyyMMdd"));
			}

			string file = @"C:\WORK\분\" + date.ToString() + "\\" + stock + ".txt";
			if (!File.Exists(file))
				return 0;

			//var lineCount = File.ReadLines(file).Count();
			string[] lines = File.ReadAllLines(file, Encoding.Default);

			int nrow = 0;
			foreach (string line in lines)
			{
				string[] words = line.Split('\t');
				if (words.Length == 1)
				{
					words = line.Split(' ');
				}

				// values are crossed, later rearrange ZZZ
				string[] time = words[0].Split(':');
				if (time.Length == 1)
				{

					x[nrow, 0] = Convert.ToInt32(words[0]);
				}
				else
				{
					x[nrow, 0] = Convert.ToInt32(time[0]) * 100 + Convert.ToInt32(time[1]);
				}

				x[nrow, 1] = Convert.ToInt32(words[1]);   // price
				x[nrow, 2] = Convert.ToInt32(words[2]);   // amount
				x[nrow, 3] = Convert.ToInt32(words[3]);   // intensity

				x[nrow, 4] = Convert.ToInt32(words[4]);   // institue from marketeye
				x[nrow, 5] = Convert.ToInt32(words[5]);   // foreign 
				x[nrow, 6] = Convert.ToInt32(words[6]);   // foreign from marketeye

				x[nrow, 7] = Convert.ToInt32(words[7]);   // total amount dealt
				x[nrow, 8] = Convert.ToInt32(words[8]);   // buy multiple 10 times
				x[nrow, 9] = Convert.ToInt32(words[9]);   // sell multiple 10 times

                if(words.Length == 12)
                {
                    x[nrow, 10] = Convert.ToInt32(words[10]);   // buy multiple 10 times
                    x[nrow, 11] = Convert.ToInt32(words[11]);   // sell multiple 10 times
                }
                nrow++;
                if (x[nrow - 1, 0] > 1420) //0505 from nrow > 390 to current if
                {
                    x[nrow - 1, 0] = 0;
                    nrow--;
                    break;
                }
			}

			x[nrow, 0] = 0; // used to check the end of data, no need actually
            if (x[nrow - 1, 1] == 0 && x[nrow - 1, 2] == 0 && x[nrow - 1, 3] == 0) // maybe trading suspended
            {
                nrow = 0;
            }
            return nrow;
		}


		public static int read_일주월(string stock, string dwm, int nrow, int[] col, int[,] x)
		{
			string file = "";
			switch (dwm)
			{
				case "일":
				file = @"C:\WORK\일\" + stock + ".txt"; ;   // 틱,분,일,주,월 단위 데이터
				break;
				case "주":
				file = @"C:\WORK\주\" + stock + ".txt"; ;   // 틱,분,일,주,월 단위 데이터
				break;
				case "월":
				file = @"C:\WORK\월\" + stock + ".txt"; ;   // 틱,분,일,주,월 단위 데이터
				break;
				default:
				break;
			}
			if (!File.Exists(file))
				return 0;

			List<string> lines = File.ReadLines(file).Reverse().Take(nrow).ToList();

			nrow = 0;
			foreach (string line in lines)
			{
				string[] words = line.Split(' ');

				for (int i = 0; i < col.Length; i++)
				{
					x[nrow, i] = Convert.ToInt32(words[col[i]]);

				}
				nrow++;
			}
			return nrow;
		}


		public static void read_전일종가_전일거래액_천만원(g.stock t)
		{
			if (t == null)
				return;

			string path = @"C:\WORK\일\" + t.종목 + ".txt";
			if (!File.Exists(path))
			{
				return;
			}

			string lastline = File.ReadLines(path).Last(); // last line read 

			string[] words = lastline.Split(' ');
			t.전일종가 = Convert.ToUInt32(words[4]);
            t.전일거래량 = Convert.ToUInt32(words[5]);
            t.전일거래액_천만원 =(ulong)(t.전일종가 *((long)t.전일거래량 / g.천만원));
        }


        public static int read_전일종가(string stock)
        {
           
            string path = @"C:\WORK\일\" + stock + ".txt";
            if (!File.Exists(path))
            {
                return -1;
            }

            string lastline = File.ReadLines(path).Last(); // last line read 

            string[] words = lastline.Split(' ');
            return Convert.ToInt32(words[4]);
        }

        internal struct RECT
		{
			public int left;
			public int top;
			public int right;
			public int bottom;
		}

		[DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
		internal static extern IntPtr GetForegroundWindow();

		[DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, ExactSpelling = true, SetLastError = true)]
		internal static extern bool GetWindowRect(IntPtr hWnd, ref RECT rect);

		[DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, ExactSpelling = true, SetLastError = true)]
		internal static extern void MoveWindow(IntPtr hwnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);


		public static void call_네이버(string stock, int selection, double xval)
		{
			CPUTILLib.CpStockCode _cd = new CPUTILLib.CpStockCode();

			if (stock == null)
				Process.Start("https://finance.naver.com/");
			//Process.Start("microsoft-edge:https://finance.naver.com/");
			else
			{
				string basestring;
				if (selection == 0)
				{
                    //basestring = "https://finance.naver.com/item/frgn.nhn?code="; // 투자자별 매매동향
                    basestring = "microsoft-edge:https://finance.naver.com/item/main.nhn?code="; // 종합정보
					//basestring = "microsoft-edge:https://finance.naver.com/item/main.nhn?code=";
				}
				else
				{
					//basestring = "https://finance.naver.com/item/fchart.nhn?code=";
					basestring = "microsoft-edge:https://finance.naver.com/item/fchart.nhn?code=";
				}

				string code = _cd.NameToCode(stock);
				code = new String(code.Where(Char.IsDigit).ToArray());
				basestring += code;
				Process.Start(basestring);









				int nWidth = 0;
				if (g.testing)
				{
					nWidth = g.window_x_size * 6 / 10;
				}
				else
				{
					nWidth = g.window_x_size * 40 / 100;
				}

				int nHeight = g.window_y_size;

				int nxpos = 0;
				if (xval < 50)
				{
					if (g.testing)
					{
						nxpos = g.window_x_size * 4 / 10;
					}
					else
					{
						nxpos = g.window_x_size * 6 / 10;
					}
				}



				/*
				Process[] processlist = Process.GetProcesses();
				string s;
				foreach (Process process in processlist)
				{
				  if (!String.IsNullOrEmpty(process.MainWindowTitle))
				  {
				  Console.WriteLine("Process: {0} ID: {1} Window title: {2}", process.ProcessName, process.Id, process.MainWindowTitle);
				  }
				  if (process.MainWindowTitle.Contains("네이버 금융"))
				  {
				  s = Convert.ToString(process.Id, 16);
				  }
				}
				*/



				/*
				IntPtr id;
				RECT Rect = new RECT();
				Thread.Sleep(10);
				id = GetForegroundWindow();

				Random myRandom = new Random();
				GetWindowRect(id, ref Rect);
				MoveWindow(id, nxpos, 0, nWidth, nHeight, true);
				*/
			}
		}


        public static void call_네이버_차트(string stock, int selection, double xval)
        {
            CPUTILLib.CpStockCode _cd = new CPUTILLib.CpStockCode();

            if (stock == null)
                Process.Start("https://finance.naver.com/");
            //Process.Start("microsoft-edge:https://finance.naver.com/");
            else
            {
                string basestring;
                if (selection == 0)
                {
                    //basestring = "https://finance.naver.com/item/frgn.nhn?code="; // 투자자별 매매동향
                    basestring = "https://finance.naver.com/item/main.nhn?code="; // 종합정보
                                                                                  //basestring = "microsoft-edge:https://finance.naver.com/item/main.nhn?code=";
                }
                else
                {
                    //basestring = "https://finance.naver.com/item/fchart.nhn?code=";
                    basestring = "microsoft-edge:https://finance.naver.com/item/fchart.nhn?code=";
                }

                string code = _cd.NameToCode(stock);
                code = new String(code.Where(Char.IsDigit).ToArray());
                basestring += code;
                Process.Start(basestring);









                int nWidth = 0;
                if (g.testing)
                {
                    nWidth = g.window_x_size * 6 / 10;
                }
                else
                {
                    nWidth = g.window_x_size * 40 / 100;
                }

                int nHeight = g.window_y_size;

                int nxpos = 0;
                if (xval < 50)
                {
                    if (g.testing)
                    {
                        nxpos = g.window_x_size * 4 / 10;
                    }
                    else
                    {
                        nxpos = g.window_x_size * 6 / 10;
                    }
                }



                /*
				Process[] processlist = Process.GetProcesses();
				string s;
				foreach (Process process in processlist)
				{
				  if (!String.IsNullOrEmpty(process.MainWindowTitle))
				  {
				  Console.WriteLine("Process: {0} ID: {1} Window title: {2}", process.ProcessName, process.Id, process.MainWindowTitle);
				  }
				  if (process.MainWindowTitle.Contains("네이버 금융"))
				  {
				  s = Convert.ToString(process.Id, 16);
				  }
				}
				*/



                /*
				IntPtr id;
				RECT Rect = new RECT();
				Thread.Sleep(10);
				id = GetForegroundWindow();

				Random myRandom = new Random();
				GetWindowRect(id, ref Rect);
				MoveWindow(id, nxpos, 0, nWidth, nHeight, true);
				*/
            }
        }


        public static List<string> read_그룹_네이버_업종(List<List<string>> Gl, List<List<string>> GL)
		{
            _cpstockcode = new CPUTILLib.CpStockCode();

            List<string> gl_list = new List<string>();

			string filepath = @"C:\WORK\그룹_네이버_업종.txt";
			if (!File.Exists(filepath))
			{
				return gl_list;
			}


			string[] grlines = File.ReadAllLines(filepath, Encoding.Default);

			List<string> GL_list = new List<string>();

			int count = 0;
			foreach (string line in grlines)
			{
				List<string> Gl_list = new List<string>();

				string[] words = line.Split('\t');

				if (words[0] != "")
				{
					string stock = words[0].Replace(" *", "");
                    string code = _cpstockcode.NameToCode(stock);
                    if (code.Length != 7)
                    {
                        continue;
                    }
                    if (code[0] != 'A')
                        continue;

                    char marketKind = read_코스피코스닥시장구분(stock);
                    if (marketKind == 'S' || marketKind == 'D')
                    { }
                    else
                        continue;


                    gl_list.Add(stock); // for single
					GL_list.Add(stock); // for small group
					if (count == grlines.Length - 1) // the last stock added as group
					{
						GL.Add(GL_list.ToList()); //modified to create a new List when adding
						GL_list.Clear();
					}
				}
				else
				{
					GL.Add(GL_list.ToList()); //modified to create a new List when adding
					GL_list.Clear();
				}
				count++;
			}

			var uniqueItemsList = gl_list.Distinct().ToList();
			return uniqueItemsList;
		}

        public static List<string> read_그룹_네이버_업종() // this is for single list of stocks in 그룹_네이버_업종
        {
            _cpstockcode = new CPUTILLib.CpStockCode();

            List<string> gl_list = new List<string>();

            string filepath = @"C:\WORK\그룹_네이버_업종.txt";
            if (!File.Exists(filepath))
            {
                return gl_list;
            }

            string[] grlines = File.ReadAllLines(filepath, Encoding.Default);

            List<string> GL_list = new List<string>();


            foreach (string line in grlines)
            {
                string[] words = line.Split('\t');

                if (words[0] != "")
                {
                    string stock = words[0].Replace(" *", "");
                    string code = _cpstockcode.NameToCode(stock);
                    if (code.Length != 7)
                    {
                        continue;
                    }
                    if (code[0] != 'A')
                        continue;

                    char marketKind = read_코스피코스닥시장구분(stock);
                    if (marketKind == 'S' || marketKind == 'D')
                    { }
                    else
                        continue;

                    gl_list.Add(stock); // for single
                }
            }

            var uniqueItemsList = gl_list.Distinct().ToList();
            return uniqueItemsList;
        }
    }
}
