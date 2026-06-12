using glbl;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using work;
using misc;

namespace exam
{
    public class ex
    {
        public static void testing()
        {

            testing_배수_잔잔_급증(20220401, 20220402);

            //while (true)
            //{
            //    DateTime date = DateTime.Now;
            //    int HHmm = Convert.ToInt32(date.ToString("HHmm")); // run_task_read_eval_score()
            //    int HHmmss = Convert.ToInt32(date.ToString("HHmmss"));


            //    double 일중거래액환산율 = wk.시분초_일중환산율(HHmmss); // return value of 분별누적인수 > 0 && 분별누적인수 <=1.0 // testing
            //}
            //testing_분당프돈및분당프돈퍼센티지높은종목(20210927, 20211119);


            //testing_개인매수액분증이추후가격에미치는영향(20200801, 20200814);

            //testing_체결배수_가격점프_매수결과(20200110, 20200110); // 시작날짜 마지막날짜 디렉토리 내의  _proceeded 파일 읽고 계산

            //testing_가격급등후일정시간가격추이해석(sw);
            //testing_수급체결지상후가격변화_종목별(sw);

            //testing_양매수(); // 전 날 외,기 양매수한 경우 무조건 매수 후 당일 승률 및 평균 대비
            //testing_ETF(); // etf를 전 날 장마감에 사서 다음 날 시초가에 던지는 경우 시뮬레이션
            //testing_ETF일중분별평균(sw)

        }

        public static void testing_배수_잔잔_급증(int from_date, int to_date)
        {
            int[,] x = new int[400, 12];

            List<string> 그룹제외 = new List<string>();
            그룹제외 = wk.read_그룹제외();

            for (int i = from_date; i <= to_date; i++)
            {
                g.date = i;

                g.sl.Clear();
                wk.read_stocks_in_directory(g.sl);

                foreach (string stock in g.sl)
                {
                    if (그룹제외.Contains(stock))
                    {
                        continue;
                    }
                    if (stock.Contains("KODEX") || stock.Contains("혼합"))
                    {
                        continue;
                    }

                    int column = wk.Read_Stock_Minute(i, stock, x);


                    for (int j = 4; j < 360; j++)
                    {
                        if (x[j, 0] <= 0 || x[j, 0] > 150000) // 150000 이후 매수 안 함
                        {
                            continue;
                        }

                        //if (x[j, 7] < x[j - 1, 7] ||  // 거래량이 시간경과하면서 감소시 무시
                        //    x[j - 1, 7] < x[j - 2, 7] ||
                        //    x[j - 2, 7] < x[j - 3, 7])
                        //    continue;

                        if (x[j, 2] < 100 ||  // 수요계수 < 100, 체강계수 < 100 제외
                            x[j, 3] < 10000)
                            continue;

                        if (x[j, 1] - x[j - 1, 1] > 100 &&

                            //x[j - 3, 8] - x[j - 3, 9] < x[j - 2, 8] - x[j - 2, 9] &&
                            //x[j - 2, 8] - x[j - 2, 9] < x[j - 1, 8] - x[j - 1, 9] &&
                            //x[j - 1, 8] - x[j - 1, 9] < x[j - 0, 8] - x[j - 0, 9] &&

                            (x[j - 3, 8] + x[j - 3, 9] + x[j - 2, 8] + x[j - 2, 9] + x[j - 1, 8] + x[j - 1, 9]) / 3 < 30
                            && x[j, 8] - x[j, 9] > 100)
                        //&&
                        //(x[j - 3, 8] - x[j - 3, 9] + x[j - 2, 8] - x[j - 2, 9] + x[j - 1, 8] - x[j - 1, 9]) / 3 < 10) // 배수잔잔 후 가격급증)
                        {
                            string[] str = new string[3];
                            str[0] = g.date.ToString();
                            str[1] = stock;
                            str[1] += "\n";
                            for (int m = j - 3; m <= j + 10; m++) // bound exist not the size
                            {
                                if (x[m, 0] == 0 || x[m, 0] > 152100) // if time is not set then stop writing
                                    continue;

                                for (int k = 0; k < 12; k++) // bound exist not the size
                                {
                                    if (k == 0)
                                        str[2] += x[m, k];

                                    else
                                        str[2] += "\t " + x[m, k];
                                }
                                str[2] += "\n";
                            }
                            str[2] += "\n";
                            wk.write_on_temp(str);
                            j += 10;
                        }
                    }
                }
            }

        }

        public static void testing_매수후일정시간경과결과(int date, string stock,
        int start_time_in_minute, int elapse_time)
        {
            int[,] x = new int[400, 12];
            
            wk.Read_Stock_Minute(date, stock, x);

            int start_time = -1;
            for (int i = 0; i < x.GetLength(0); i++)
            {
                if(x[i, 0] == start_time_in_minute)
                {
                    start_time = i;
                    break;
                }
            }
            if (start_time == -1)
                return;

            int allHigh = x[start_time, 1];
            int startPrice = x[start_time, 1];
            int exitPrice = 0;
            int max_time = 0, positive_difference = 0;
            int min_time = 0, negative_difference = 0;

            for (int i = start_time + 1; i < start_time+elapse_time; i++)
            {
                if (i == x.GetLength(0) - 1)
                    break;

                int dif = x[i, 1] - startPrice;
                if (allHigh < x[i, 1])
                    allHigh = x[i, 1];

                if (dif > positive_difference)
                {
                    positive_difference = dif;
                }
                if (dif < negative_difference)
                {
                    negative_difference = dif;
                }
                if (x[i, 1] - x[i - 1, 1] < -50)
                {
                    exitPrice = x[i, 1];
                    break;
                }
            }

            // 당일삭제 
            //int differencePrice = exitPrice - startPrice;
            // if(differencePrice  < 0)
            //{
            //    g.임시누적손실 += differencePrice;
            //    g.임시상승종목++;
            //}
            //else
            //{
            //    g.임시누적수익 += differencePrice;
            //    g.임시하락종목++;
            //}
            //g.임시종목숫자 += 1;


            string path = @"C:\WORK\temp.txt";
            StreamWriter sw = File.AppendText(path);
            sw.WriteLine("{0}\t\t\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}", date, stock, x[start_time, 0], max_time,
                                                                           positive_difference, min_time, negative_difference);
            sw.Close();
        }

        public static void testing_개인매수액분증이추후가격에미치는영향(int from_date, int to_date)
        {
            int price_difference = 30;
            int[,] x = new int[400, 12];

            string path = @"c:\work\temp.txt";
            StreamWriter sw = File.AppendText(path);

            //sw.close();
            for (int date = from_date; date <= to_date; date++)
            {
                int total_count = 0;
                int nrow = wk.Read_Stock_Minute(date, "KODEX 레버리지", x);
                if (nrow < 10)
                {
                    continue;
                }

                for (int i = 1; i < 400; i++)
                {
                    if (x[i, 0] == 0 || x[i, 0] > 1500)
                    {
                        break;
                    }
                    int t = Math.Abs(x[i + 1, 1] - x[i, 1]);
                    if (t > price_difference)
                    {
                        total_count++;

                        //sw.WriteLine("{0}\t{1}\t{2}", x[i, 0], x[i, 1], x[i, 6]);
                        for (int j = 0; j < 10; j++)
                        {
                            sw.WriteLine("{0}\t{1}\t{2}", x[i + j, 0], x[i + j, 1], x[i + j, 6]);
                        }
                        sw.WriteLine();

                    }
                }

                sw.WriteLine("{0}\t{1}", date, total_count);

            }
            sw.Close();
        }
        
       
        public static void testing_체결배수_가격점프_매수결과_리스팅(int date, string stock,
        int start, int[,] x, double price_down, ref int end)
        {
            double earning = -3100;
            for (int i = start; i < 390; i++)
            {
                double dif = x[i + 1, 1] - x[start, 1];
                if (dif > earning)
                {
                    earning = dif;
                }
                if (dif < price_down || x[i + 1, 1] - x[i, 1] < price_down)
                {
                    end = i;
                    break;
                }
                if (end > start + 10)
                {
                    break;
                }
            }

            string path = @"C:\WORK\temp.txt";
            StreamWriter sw = File.AppendText(path);


            int index = g.ogl_data.FindIndex(r => r.종목 == stock);
            if (index < 0)
            {
                sw.Close();
                return;
            }


            if (g.ogl_data[index].dev > 2.5)
            {
                sw.WriteLine("{0,-10}{1,-15}{2,-10}{3,-10}{4,-10}{5,-10}{6,-10}{7,-10}",
                  date, stock, earning, x[start, 0], x[start, 1], g.ogl_data[index].dev_avr,
                  (int)((x[start, 2] + x[start, 3]) * g.ogl_data[index].현재가 / 10000.0),
                  g.ogl_data[index].현재가);


                /*
                for (int i = start-3; i < end+3; i++)
                {
                  if (i < 0 || i > 1530)
                  continue;

                  sw.WriteLine("{0:0}\t{1:0}\t{2:0}\t{3:0}\t{4:0.0}\t{5:0.0}", x[i, 0], x[i, 1], x[i, 2], x[i, 3], x[i, 4], x[i, 5]);
                  if (i > start + 10)
                  continue;

                }
                sw.WriteLine();*/
            }

            sw.Close();

        }

        public static void testing_분당프돈및분당프돈퍼센티지높은종목(int from_date, int to_date)
        {
            int[,] x = new int[400, 12];


            for (int i = from_date; i <= to_date; i++)
            {

                
                if (i % 100 > 31)
                    continue;
               

                g.date = i;
                string str1 = g.date.ToString();
                wk.write_on_temp("");
                wk.write_on_temp(str1);

                g.sl.Clear();
                wk.read_stocks_in_directory(g.sl);


                string[] str = new string[9];
                str[0] = "Date"; //stock + "/" + dev.ToString("#.#");
                str[1] = "Price"; //x[j, 1].ToString();
                str[2] = "프돈 / 프퍼"; // program_money.ToString("#") + "/" + percentage.ToString() + "\t\t\t\t";
                str[3] = "가차"; //price_up.ToString();
                str[4] = "최대 / 최대위치"; // max.ToString() + "/" + (maxpos - j).ToString();
                str[5] = "최소 / 최소위치"; // min.ToString() + "/" + (minpos - j).ToString();
                wk.write_on_temp(str);

                foreach (string stock in g.sl)
                {
                    double avr = 0.0, dev = 0.0;
                    int a = 0, b = 0, c = 0;

                    wk.calcurate_종목일중변동평균편차(stock, 10, ref avr, ref dev, ref a, ref b, ref c); // a,b,c -> avrage deal, max and min
                    if (stock.Contains("KODEX") || stock.Contains("혼합") || dev < 1.5)
                    {
                        continue;
                    }

                    int nrow = wk.Read_Stock_Minute(i, stock, x); // i is given date

                    int yesterday_close = wk.read_전일종가(stock);
                    if (yesterday_close < 0)
                        continue;

                    for (int j = 1; j < nrow - 11; j++)
                    {
                        double program_money = (double)(x[j, 4] - x[j-1, 4]) * (double) yesterday_close;
                        program_money /= 10000000;




                        if (program_money >  100) // 천만 단위
                                                 //if (program_money > 10 && percentage > 50 && dev > 5.0)
                        {
                            double money_per_minute = x[j, 7] - x[j - 1, 7];
                            money_per_minute *= yesterday_close;
                            money_per_minute /= 10000000;
                            int percentage = (int)(program_money / (double)money_per_minute * 100);

                            int currently_accumulated_money_dealt = (int)x[j, 7] * (int)yesterday_close / 10000000;
                            int closing_accumulated_money_dealt = (int)x[nrow - 1, 7] * (int)yesterday_close / 10000000;

                            if (percentage < 0)
                            {
                                continue; ;
                            }

                            if (percentage > 110)
                            {
                                continue;
                            }

                            int price_up = 0;
                            if (j - 1 > 0)
                                price_up = x[j, 1] - x[j - 1, 1];

                            int maxpos = 0, max = 0, minpos = 0, min = 0;
                            wk.read_멏분후_값차이_최대_최소(x, j, 10, 1, ref maxpos, ref max, ref minpos, ref min);// 10 lapse_time, 1 column i.e. price
                   
                            str[0] = stock + "/" + dev.ToString("#.#");
                            str[1] = x[j, 1].ToString();
                            str[2] = program_money.ToString("#") + "/" + percentage.ToString() + "\t\t\t\t" ;
                            str[3] = price_up.ToString();
                            str[4] = max.ToString() + "/" + (maxpos - j).ToString();
                            str[5] = min.ToString() + "/" + (minpos - j).ToString();
                            wk.write_on_temp(str);
                            //wk.write_xdata_on_temp(x, j - 3, j + 2);
                        }
                    }
                }
            }
        }

        public static void testing_분전분당거래액작으나가격100이상급상(int from_date, int to_date)
        {
            int[,] x = new int[400, 12];


            for (int i = from_date; i <= to_date; i++)
            {
                g.date = i;
                string str1 = g.date.ToString();
                wk.write_on_temp("");
                wk.write_on_temp(str1);

                g.sl.Clear();
                wk.read_stocks_in_directory(g.sl);

                foreach (string stock in g.sl)
                {
                    if (stock.Contains("KODEX") || stock.Contains("혼합"))
                    {
                        continue;
                    }

                    int nrow = wk.Read_Stock_Minute(i, stock, x); // i is given date

                    int 전일종가 = wk.read_전일종가(stock);
                    for (int j = 3; j < nrow - 3; j++)
                    {
                        if (x[j, 1] - x[j - 1, 1] > 100)
                        {
                            ulong 삼분전동안누적거래액 = (((ulong)x[j, 7] - (ulong)x[j - 3, 7]) * (ulong)전일종가);
                            삼분전동안누적거래액 /= 10000000;
                            ulong 현시점누적거래액 = (ulong)x[j, 7] * (ulong)전일종가 / 10000000;
                            ulong 종가후당일누적거래액 = (ulong)x[nrow - 1, 7] * (ulong)전일종가 / 10000000;
                            if (삼분전동안누적거래액 < 3)
                            {
                                string[] str = new string[6];
                                str[0] = stock;
                                str[1] = 삼분전동안누적거래액.ToString();
                                str[2] = 현시점누적거래액.ToString();
                                str[3] = 종가후당일누적거래액.ToString();
                                str[4] = "";
                                str[5] = 전일종가.ToString();
                                
                                if (종가후당일누적거래액 > 1000)
                                {
                                    wk.write_on_temp(str);
                                    wk.write_on_temp(x, j - 3, j + 2);
                                }
                            }
                        }
                    }
                }
            }          
        }

        public static void testing_시총이상_가격급상(int from_date, int to_date)
        {
            int[,] x = new int[400, 12];

            List<string> 그룹제외 = new List<string>();
            그룹제외 = wk.read_그룹제외();

            for (int i = from_date; i <= to_date; i++)
            {
                g.date = i;

                g.sl.Clear();
                wk.read_stocks_in_directory(g.sl);

                foreach (string stock in g.sl)
                {
                    if (그룹제외.Contains(stock))
                    {
                        continue;
                    }
                    if (stock.Contains("KODEX") || stock.Contains("혼합"))
                    {
                        continue;
                    }

                    int column = wk.Read_Stock_Minute(i, stock, x);
                    int index = g.ogl_data.FindIndex(r => r.종목 == stock);
                    if (index < 0)
                        continue;

                    for (int j = 4; j < 360; j++)
                    {
                        if (x[j, 0] <= 0 || x[j, 0] > 150000) // 150000 이후 매수 안 함
                        {
                            continue;
                        }

                        if (x[j, 1] - x[j - 1, 1] > 100 && g.ogl_data[index].시총 > 200) // 첫 마젠타 + 가격 50 이상 + 배수차 50 이상
                        {
                            string[] str = new string[3];
                            str[0] = g.date.ToString();
                            str[1] = stock;

                            for (int m = j - 3; m <= j + 10; m++) // bound exist not the size
                            {
                                if (x[m, 0] == 0 || x[m, 0] > 152100) // if time is not set then stop writing
                                    continue;

                                for (int k = 0; k < 12; k++) // bound exist not the size
                                {
                                    if (k == 0)
                                        str[2] += x[m, k];

                                    else
                                        str[2] += "\t" + x[m, k];
                                }
                                str[2] += "\n";
                            }
                            
                            j += 10;
                        }
                    }
                }
            }
        }

        public static void testing_체결배수_가격점프_매수결과_리스팅(int date, string stock, 
        int start, double [,] x, double price_down, ref int end)
        {

        double earning = -3100;
        for (int i = start; i < 390; i++)
        {
          double dif = x[i+1, 1] - x[start, 1];
          if(dif > earning)
          {
            earning = dif;
          }
          if (dif < price_down || x[i+1,1] - x[i, 1] < price_down)
          {
            end = i;
            break;
          }
        }

        string path = @"C:\WORK\temp.txt";
        StreamWriter sw = File.AppendText(path);



        sw.WriteLine("{0}\t{1}\t{2}", date,stock, earning);
        for (int i = start-3; i < end+3; i++)
        {
          if (i < 0 || i > 1530)
            continue;

          sw.WriteLine("{0:0}\t{1:0}\t{2:0}\t{3:0}\t{4:0.0}\t{5:0.0}", x[i, 0], x[i, 1], x[i, 2], x[i, 3], x[i, 4], x[i, 5]);

        }
        sw.WriteLine();
        sw.Close();

        }
        
        public static void read_write_7Columns(string stock)
        {
            string path = @"C:\WORK\분\" + g.date.ToString() + "\\" + stock + "_processed" + ".txt";
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            Stream FS = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
            StreamWriter sw = new System.IO.StreamWriter(FS, System.Text.Encoding.Default);
            sw.Close();


            double[,] x = new double[400, 7];
            //wk.read_stock_marketeye(g.date, stock, x);
            int index = g.ogl_data.FindIndex(r => r.종목 == stock);

            double pufac = 0;
            double pdfac = 0;



            for (int i = 0; i < 400; i++)
            {
                if (x[i, 0] == 0)
                {
                    break;
                }

                double ufac = 100.0 / (x[i, 3] + 100.0);
                double dfac = x[i, 3] / (x[i, 3] + 100.0);

                ufac = (int)(x[i, 4] * ufac);
                dfac = (int)(x[i, 4] * dfac);

                if (ufac < 0 || dfac < 0)
                {

                }


                /*

                ufac = ufac / g.ogl_data[index].일평균거래량 / g.mF[i];
                dfac = dfac / g.ogl_data[index].일평균거래량 / g.mF[i];

                x[i,2] = (int)(ufac * 100);
                x[i,3] = (int)(dfac * 100);
                */





                sw = File.AppendText(path);
                sw.WriteLine("{0}\t{1}\t{2}\t{3}\t{4:0.0}\t{5:0.0}", x[i, 0], x[i, 1], dfac - pdfac, ufac - pufac,
                  (dfac - pdfac) / g.ogl_data[index].일평균거래량 * 381.0, (ufac - pufac) / g.ogl_data[index].일평균거래량 * 381.0);
                sw.Close();




                /*

                // The following lines are for test purpose
                if (i > 10 && !written && (double)(dfac - pdfac) / g.ogl_data[index].일평균거래량 * 381 > 20.0 && x[i, 1] - x[i - 1, 1] > 150)

                  //if (i > 20 && !written && (double)(dfac - pdfac) / g.ogl_data[index].일평균거래량 * 381 > 20.0 && x[i, 1] - x[i - 1, 1] > 150 &&
                  //Convert.ToDouble(g.ogl_data[index].dev) > 2.0)
                  {
                  string file = @"C:\WORK\temp.txt";
                  sw = File.AppendText(file);
                  sw.WriteLine("{0}\t{1:0.0}\t{2:0.0}\t{3:0.0}\t{4:0.0}", stock, g.ogl_data[index].avr, g.ogl_data[index].dev,
                  (double)(dfac - pdfac) / g.ogl_data[index].일평균거래량 * 381, (double)(ufac - pufac) / g.ogl_data[index].일평균거래량 * 381.0);

                  for (int j = i-2; j < i + 12; j++)
                  {
                  sw.WriteLine("\t{0}\t{1}\t{2}\t{3}", x[j,0],x[j, 1],x[j,2],x[j,3]);
                  }

                  i = i + 10;

                  sw.WriteLine();
                  sw.Close();
                }


                */





                // The following 2 lines should be preserved
                pdfac = dfac;
                pufac = ufac;



                /*  program comment
                 *  i dn, a up then p can up for the time being
                 *  if p is higher than 1000, treat seperately or p should go up steeply for the minute considered
                 *  trend should be upwards, too low p also should be treated seperately or excluded
                 *  include ave and dev 
                 *  if p not moving, try to sell "buy p"
                 *  if i or a too low, neglect
                 */

            }
        }

        public static void read_write_10Columns(string stock)
        {
            string path = @"C:\WORK\분\" + g.date.ToString() + "\\" + stock + "_processed" + ".txt";
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            Stream FS = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
            StreamWriter sw = new System.IO.StreamWriter(FS, System.Text.Encoding.Default);
            sw.Close();


            int[,] x = new int[400, 10];
            wk.Read_Stock_Minute(g.date, stock, x); // columns number is flexible
            int index = g.ogl_data.FindIndex(r => r.종목 == stock);

            double pufac = 0;
            double pdfac = 0;


            for (int i = 0; i < 400; i++)
            {
                if (x[i, 0] == 0)
                {
                    break;
                }

                double ufac = 100.0 / (x[i, 3] + 100.0);
                double dfac = x[i, 3] / (x[i, 3] + 100.0);

                ufac = (int)(x[i, 7] * ufac);
                dfac = (int)(x[i, 7] * dfac);

                if (ufac < 0 || dfac < 0)
                {

                }

                /*
                ufac = ufac / g.ogl_data[index].일평균거래량 / g.mF[i];
                dfac = dfac / g.ogl_data[index].일평균거래량 / g.mF[i];

                x[i,2] = (int)(ufac * 100);
                x[i,3] = (int)(dfac * 100);
                */

                sw = File.AppendText(path);
                sw.WriteLine("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7:0.0}\t{8:0.0}\t{9}", x[i, 0], x[i, 1], x[i, 2], x[i, 3],
                  x[i, 4], x[i, 5], x[i, 6],
                  (dfac - pdfac) / g.ogl_data[index].일평균거래량 * 381.0,
                  (ufac - pufac) / g.ogl_data[index].일평균거래량 * 381.0, x[i, 7]);
                sw.Close();

                /*
                // The following lines are for test purpose
                if (i > 10 && !written && (double)(dfac - pdfac) / g.ogl_data[index].일평균거래량 * 381 > 20.0 && x[i, 1] - x[i - 1, 1] > 150)

                  //if (i > 20 && !written && (double)(dfac - pdfac) / g.ogl_data[index].일평균거래량 * 381 > 20.0 && x[i, 1] - x[i - 1, 1] > 150 &&
                  //Convert.ToDouble(g.ogl_data[index].dev) > 2.0)
                  {
                  string file = @"C:\WORK\temp.txt";
                  sw = File.AppendText(file);
                  sw.WriteLine("{0}\t{1:0.0}\t{2:0.0}\t{3:0.0}\t{4:0.0}", stock, g.ogl_data[index].avr, g.ogl_data[index].dev,
                  (double)(dfac - pdfac) / g.ogl_data[index].일평균거래량 * 381, (double)(ufac - pufac) / g.ogl_data[index].일평균거래량 * 381.0);

                  for (int j = i-2; j < i + 12; j++)
                  {
                  sw.WriteLine("\t{0}\t{1}\t{2}\t{3}", x[j,0],x[j, 1],x[j,2],x[j,3]);
                  }

                  i = i + 10;

                  sw.WriteLine();
                  sw.Close();
                }
                */


                // The following 2 lines should be preserved
                pdfac = dfac;
                pufac = ufac;

                /*  program comment
                 *  i dn, a up then p can up for the time being
                 *  if p is higher than 1000, treat seperately or p should go up steeply for the minute considered
                 *  trend should be upwards, too low p also should be treated seperately or excluded
                 *  include ave and dev 
                 *  if p not moving, try to sell "buy p"
                 *  if i or a too low, neglect
                 */

            }
        }

        public static void testing_수급체결지상후가격변화_종목별(StreamWriter sw)
        {

            int[] time = new int[2];

            // 테스트 변수 : 아래 세팅은 오전 9시10분부터 오전 12시까지, 분당 변화 100일 경우 
            time[0] = 10;
            time[1] = 120;


            int lapse_time = 30;

            string[] dirs = Directory.GetDirectories(@"C:\WORK\분", "*", SearchOption.TopDirectoryOnly);

            g.date = 20190419;



            foreach (string stock in g.sl)
            {

                int n_happening = 0;

                int earn = 0;
                int loss = 0;

                foreach (string dir in dirs)
                {
                    g.date = int.Parse(dir.Split('\\')[3]);
                    if (g.date > 20190331 || g.date < 20190102)
                    {
                        continue;
                    }

                    // KODEX 제외
                    if (stock.Contains("KODEX") || g.date < 20180102)
                    {
                        continue;
                    }

                    int[,] x = new int[400, 4];
                    //wk.read_stock2(g.date, stock, x);
                    for (int t = time[0]; t < time[1]; t++)
                    {

                        int a_positive_count = 0;
                        for (int n = 0; n < g.npts_for_magenta_cyan_mark; n++)
                        {
                            if (t - 1 - n < 0)
                            {
                                break;
                            }

                            int diff = x[t - n, 2] - x[t - 1 - n, 2];
                            if (diff >= 1)
                            {
                                a_positive_count++;
                            }
                        }

                        int i_positive_count = 0;
                        for (int n = 0; n < g.npts_for_magenta_cyan_mark; n++)
                        {
                            if (t - 1 - n < 0)
                            {
                                break;
                            }

                            int diff = x[t - n, 3] - x[t - 1 - n, 3];
                            if (diff >= 1)
                            {
                                i_positive_count++;
                            }
                        }
                        if (a_positive_count >= 4 && i_positive_count >= 4)
                        {
                            int min = 0;
                            int max = 0;

                            int col = 1;
                            int maxpos = -1;
                            int minpos = -1;
                            wk.read_멏분후_값차이_최대_최소(x, t, lapse_time, col,
                                ref maxpos, ref max, ref minpos, ref min);
                            /*
                            if(x[t, 2] - x[t - 6, 2] > 20 &&  x[t, 3] - x[t - 6, 3] > 40)
                            {
                            */
                            sw.WriteLine("{0} \t{1} \t{2} \t{3} \t{4} \t{5} \t{6} \t{7} \t{8}",
                            x[t, 2] - x[t - 6, 2], x[t, 3] - x[t - 6, 3], max, min, stock, g.date, t, maxpos - t, minpos - t);

                            n_happening++;


                            earn += max;
                            loss += min;
                            /*
                            }
                            */
                        }
                    }
                }

                if (n_happening > 0)
                {
                    sw.WriteLine("{0} \t{1} \t{2}",
                        stock, earn / (double)n_happening, loss / (double)n_happening);
                }
            }
        }

        public static void testing_가격수급체결상승후일정시간가격추이해석_종목별(StreamWriter sw)
        {

            int[] time = new int[2];

            // 테스트 변수 : 아래 세팅은 오전 9시10분부터 오전 12시까지, 분당 변화 100일 경우 
            time[0] = 1;
            time[1] = 10;
            int price_difference = 0;
            double amount_times = 3;
            double intensity_times = 3;
            int lapse_time = 180;

            string[] dirs = Directory.GetDirectories(@"C:\WORK\분", "*", SearchOption.TopDirectoryOnly);

            g.date = 20190419;

            int total_earn = 0;
            int total_loss = 0;
            int total_positive_happening = 0;
            int total_negative_happening = 0;


            foreach (string stock in g.sl)
            {
                int positive_happening = 0;
                int negative_happening = 0;

                int earn = 0;
                int loss = 0;

                foreach (string dir in dirs)
                {
                    g.date = int.Parse(dir.Split('\\')[3]);
                    if (g.date > 20190419)
                    {
                        continue;
                    }

                    // KODEX 제외
                    if (stock.Contains("KODEX") || g.date < 20180102)
                    {
                        continue;
                    }

                    int[,] x = new int[400, 4];
                    //wk.read_stock2(g.date, stock, x);
                    for (int t = time[0]; t < time[1]; t++)
                    {
                        // amount and intesity check
                        if (x[t, 2] < 151 || x[t, 3] < 151)
                        {
                            continue;
                        }

                        // price check

                        if (x[t, 1] - x[t - 1, 1] < price_difference)
                        {
                            continue;
                        }


                        double minute_before_100 = wk.시분초_일중환산율(t - 1); // testing
                        double minute_now_100 = wk.시분초_일중환산율(t); // testing

                        double amount_now = x[t, 2] * minute_now_100 - x[t - 1, 2] * minute_before_100;
                        double amount_times_now = amount_now / x[t - 1, 2];

                        if (amount_times_now < amount_times)
                        {
                            continue;
                        }

                        double intensity_now = x[t, 3] * minute_now_100 - x[t - 1, 3] * minute_before_100;
                        double intensity_times_now = intensity_now / x[t - 1, 3];

                        if (intensity_times_now < intensity_times)
                        {
                            continue;
                        }

                        int min = 0;
                        int max = 0;

                        int col = 1;
                        int maxpos = -1;
                        int minpos = -1;
                        wk.read_멏분후_값차이_최대_최소(x, t, lapse_time, col,
                            ref maxpos, ref max, ref minpos, ref min);
                        /*
                        if(maxpos>minpos)
                        {*/
                        sw.WriteLine("{0} \t{1} \t{2} \t{3} \t{4} \t{5} \t{6}",
                            maxpos, max, minpos, min, stock, g.date, t);
                        /* For Later Use
                        sw.WriteLine("{0} \t{1} \t{2} \t{3} \t{4:F1}, \t{5:F1} \t{6} \t{7} \t{8}",
                            maxpos, max, minpos, min, amount_times_now, intensity_times_now, stock, g.date, t);
                            */

                        if (min > -70)
                        {
                            earn += max;
                            positive_happening++;
                        }
                        else
                        {
                            loss += min;
                            negative_happening++;
                        }
                        /*}*/
                    }
                }

                if (positive_happening != 0)
                {
                    sw.WriteLine("{0} \t{1}", positive_happening, earn / positive_happening);
                    total_earn += earn;
                    total_positive_happening += positive_happening;
                    if (negative_happening == 0)
                    {
                        sw.WriteLine(" ");
                    }
                }
                if (negative_happening != 0)
                {
                    sw.WriteLine("{0} \t{1}", negative_happening, loss / negative_happening);

                    total_loss += loss;
                    total_negative_happening += negative_happening;
                    sw.WriteLine(" ");
                }

            }
            if (total_positive_happening != 0)
            {
                sw.WriteLine("{0} \t{1}", total_positive_happening, total_earn / total_positive_happening);
                sw.WriteLine(" ");
                if (total_negative_happening == 0)
                {
                    sw.WriteLine(" ");
                }
            }
            if (total_negative_happening != 0)
            {
                sw.WriteLine("{0} \t{1}", total_negative_happening, total_loss / total_negative_happening);
                sw.WriteLine(" ");
            }
        }

        public static void testing_가격수급체결상승후일정시간가격추이해석_일자별(StreamWriter sw)
        {

            int[] time = new int[2];

            // 테스트 변수 : 아래 세팅은 오전 9시10분부터 오전 12시까지, 분당 변화 100일 경우 
            time[0] = 1;
            time[1] = 10;
            int price_difference = 0;
            double amount_times = 3;
            double intensity_times = 3;
            int lapse_time = 180;

            string[] dirs = Directory.GetDirectories(@"C:\WORK\분", "*", SearchOption.TopDirectoryOnly);

            g.date = 20190419;

            int total_earn = 0;
            int total_loss = 0;
            int total_positive_happening = 0;
            int total_negative_happening = 0;


            foreach (string dir in dirs)
            {
                g.date = int.Parse(dir.Split('\\')[3]);
                if (g.date > 20190419)
                {
                    break;
                }


                int positive_happening = 0;
                int negative_happening = 0;

                int earn = 0;
                int loss = 0;


                foreach (string stock in g.sl)
                {

                    // KODEX 제외
                    if (stock.Contains("KODEX") || g.date < 20180102)
                    {
                        continue;
                    }

                    int[,] x = new int[400, 4];
                    //wk.read_stock2(g.date, stock, x);
                    for (int t = time[0]; t < time[1]; t++)
                    {
                        // amount and intesity check
                        if (x[t, 2] < 151 || x[t, 3] < 151)
                        {
                            continue;
                        }

                        // price check

                        if (x[t, 1] - x[t - 1, 1] < price_difference)
                        {
                            continue;
                        }


                        double minute_before_100 = wk.시분초_일중환산율(t - 1); // testing
                        double minute_now_100 = wk.시분초_일중환산율(t);

                        double amount_now = x[t, 2] * minute_now_100 - x[t - 1, 2] * minute_before_100;
                        double amount_times_now = amount_now / x[t - 1, 2];

                        if (amount_times_now < amount_times)
                        {
                            continue;
                        }

                        double intensity_now = x[t, 3] * minute_now_100 - x[t - 1, 3] * minute_before_100;
                        double intensity_times_now = intensity_now / x[t - 1, 3];

                        if (intensity_times_now < intensity_times)
                        {
                            continue;
                        }



                        int min = 0;
                        int max = 0;

                        int col = 1;
                        int maxpos = -1;
                        int minpos = -1;
                        wk.read_멏분후_값차이_최대_최소(x, t, lapse_time, col,
                            ref maxpos, ref max, ref minpos, ref min);
                        /*
                        if(maxpos>minpos)
                        {*/
                        sw.WriteLine("{0} \t{1} \t{2} \t{3} \t{4} \t{5} \t{6}",
                          maxpos, max, minpos, min, stock, g.date, t);
                        /* For Later Use
                        sw.WriteLine("{0} \t{1} \t{2} \t{3} \t{4:F1}, \t{5:F1} \t{6} \t{7} \t{8}",
                          maxpos, max, minpos, min, amount_times_now, intensity_times_now, stock, g.date, t);
                          */

                        if (min > -70)
                        {
                            earn += max;
                            positive_happening++;
                        }
                        else
                        {
                            loss += min;
                            negative_happening++;
                        }
                        /*}*/
                    }
                }

                if (positive_happening != 0)
                {
                    sw.WriteLine("{0} \t{1}", positive_happening, earn / positive_happening);
                    total_earn += earn;
                    total_positive_happening += positive_happening;
                    if (negative_happening == 0)
                    {
                        sw.WriteLine(" ");
                    }
                }
                if (negative_happening != 0)
                {
                    sw.WriteLine("{0} \t{1}", negative_happening, loss / negative_happening);

                    total_loss += loss;
                    total_negative_happening += negative_happening;
                    sw.WriteLine(" ");
                }

            }
            if (total_positive_happening != 0)
            {
                sw.WriteLine("{0} \t{1}", total_positive_happening, total_earn / total_positive_happening);
                sw.WriteLine(" ");
                if (total_negative_happening == 0)
                {
                    sw.WriteLine(" ");
                }
            }
            if (total_negative_happening != 0)
            {
                sw.WriteLine("{0} \t{1}", total_negative_happening, total_loss / total_negative_happening);
                sw.WriteLine(" ");
            }
        }

        public static void testing_가격급등후일정시간가격추이해석(StreamWriter sw)
        {
            int[,] x = new int[400, 4];
            int[] time = new int[2];
            // 테스트할 시간 구간 : 아래 세팅은 오전 9시10분부터 오전 12시까지, 분당 변화 100일 경우 
            time[0] = 2;
            time[1] = 15;

            string[] dirs = Directory.GetDirectories(@"C:\WORK\분", "*", SearchOption.TopDirectoryOnly);

            foreach (string dir in dirs)
            {
                g.date = int.Parse(dir.Split('\\')[3]);
                if (g.date > 20180120)
                {
                    continue;
                }


                foreach (string stock in g.sl)
                {
                    if (stock.Contains("KODEX") || g.date < 20180102)
                    {
                        continue;
                    }
                    //

                    //wk.read_stock2(g.date, stock, x);
                    for (int t = time[0]; t < time[1]; t++)
                    {
                        /*
                        int positive = 0;
                        for(int i=t; i>=t-3;i--)
                        {
                          if(x[i,1]-x[i-1,1]>0)
                          {
                            positive++;
                          }
                        }
                        */

                        if (x[t, 1] - x[t - 1, 1] > 100)
                        {
                            int min = 0;
                            int max = 0;


                            //if (x[t, 3] > 200)
                            //{
                            // time[0] start time, 30 lapse time, 1 col means price, return value 최대하락 최대상승
                            int lapse_time = 10;
                            int col = 1;
                            int maxpos = -1;
                            int minpos = -1;
                            wk.read_멏분후_값차이_최대_최소(x, t, lapse_time, col,
                                ref maxpos, ref max, ref minpos, ref min);
                            // count++;
                            // min_sum += min;
                            // max_sum += max;
                            sw.WriteLine("{0} \t{1} \t{2} \t{3} \t{4}, \t{5} \t{6}",
                               minpos, min, maxpos, max, stock, g.date, t);

                            /*
                              for (int i = t - 1; i < t + 10; i++)
                              {
                              sw.WriteLine("{0} \t{1} \t{2} \t{3}", i, x[i, 1], x[i, 2], x[i, 3]);
                              }
                              sw.WriteLine(" ");
                            //} 
                            */
                        }
                    }
                }

            }
        }

        public static void testing_ETF일중분별평균(StreamWriter sw)
        {


            //string path = @"C:\Work\" + "일" + "\\" + "KODEX 코스닥150레버리지" + ".txt";
            //string path = @"C:\Work\" + "일" + "\\" + "KODEX 레버리지" + ".txt";
            //string path = @"C:\Work\" + "일" + "\\" + "KODEX 200선물인버스2X" + ".txt";

            int count = 0;
            g.date = 20180903;
            int[,] avr = new int[1000, 4];


            while (true)
            {
                int return_date = wk.directory_분전후(g.moving_reference_date, 1); // 거래익일
                if (return_date == -1)
                {
                    return;
                }
                else
                {
                    g.date = return_date;
                }

                //g.date = wk.directory_분전후(g.date, 1); // 거래익일
                if (g.date > 20190307)
                {
                    break;
                }

                string stock = "KODEX 레버리지";
                string path = @"C:\Work\분\" + g.date.ToString() + "\\" + stock + ".txt";
                if (!File.Exists(path))
                {
                    continue;
                }

                int[,] x = new int[1000, 4];
                wk.read_stock분데이터(g.date, stock, x);
                if (x[0, 0] != 901)
                {
                    continue;
                }

                count++;
                for (int i = 0; i < 500; i++)
                {
                    if (x[i, 0] < 100)
                    {
                        break;
                    }

                    avr[i, 0] += x[i, 0];
                    avr[i, 1] += x[i, 1];
                }
            }

            for (int i = 0; i < 500; i++)
            {
                if (avr[i, 0] < 100)
                {
                    break;
                }

                avr[i, 0] /= count;
                avr[i, 1] /= count;
                sw.WriteLine("{0} \t{1}", avr[i, 0], avr[i, 1]);
            }
        }

        public static void testing_양매수()
        {
            // Simple File Open and Write
            string path = @"C:\WORK\temp.txt";
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            Stream FS = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
            StreamWriter sw = new System.IO.StreamWriter(FS, System.Text.Encoding.Default);



            foreach (string stock in g.sl)
            {
                //var lines = new List<string>(path);


                string a = @"C:\WORK\일\" + stock + ".txt";

                if (!File.Exists(a))
                {
                    return;
                }

                List<string> lines = File.ReadLines(a).ToList();


                // Read Data File
                double[,] x = new double[1000, 10];
                int n = 0;
                foreach (string line in lines)
                {
                    string[] words = line.Split(' ');

                    for (int i = 0; i < 10; i++)
                    {
                        x[n, i] = Convert.ToInt32(words[i]);
                    }
                    n++;
                }

                int total_count = 0;
                int positive_count = 0;
                double positive_average = 0.0;
                int negative_count = 0;
                double negative_average = 0.0;

                if (n < 602)
                {
                    continue;
                }

                for (int i = n - 1; i > n - 201; i--)
                {

                    if ((x[i - 1, 8] - x[i - 2, 8] > 0.0 && x[i - 1, 9] - x[i - 2, 9] > 0.0) &&
                    (x[i - 2, 8] - x[i - 3, 8] > 0.0 && x[i - 2, 9] - x[i - 3, 9] > 0.0)) // 전일 기관(8), 외인(9) 양매수
                    {

                        total_count++;
                        double percent = ((x[i, 4] - x[i - 1, 4]) / x[i, 4] * 100); // 당일 대비
                        if (percent > 0) // 당일종가 - 전일종가 > 0
                        {
                            positive_average += percent;
                            positive_count++;
                        }
                        else
                        {
                            negative_average += percent;
                            negative_count++;
                        }

                    }
                }
                if (positive_count > 0)
                {
                    positive_average /= positive_count;
                }
                else
                {
                    positive_average = 0.0;
                }

                if (negative_count > 0)
                {
                    negative_average /= negative_count;
                }
                else
                {
                    negative_average = 0.0;
                }

                sw.WriteLine("{0} \t\t{1} \t{2} \t{3:N2} \t{4} \t{5:N2}", stock, total_count, positive_count, positive_average, negative_count, negative_average);

            }
            sw.Close();

        }

        public static void testing_ETF()
        {
            string path = @"C:\Work\" + "일" + "\\" + "KODEX 코스닥150선물인버스" + ".txt";
            //string path = @"C:\Work\" + "일" + "\\" + "KODEX 코스닥150레버리지" + ".txt";
            //string path = @"C:\Work\" + "일" + "\\" + "KODEX 레버리지" + ".txt";
            //string path = @"C:\Work\" + "일" + "\\" + "KODEX 200선물인버스2X" + ".txt";

            //var lines = new List<string>(path);
            if (!File.Exists(path))
            {
                return;
            }

            List<string> lines = File.ReadLines(path).ToList();


            // Read Data File
            double[,] x = new double[1000, 10];
            int n = 0;
            foreach (string line in lines)
            {
                string[] words = line.Split(' ');

                for (int i = 0; i < 10; i++)
                {
                    x[n, i] = Convert.ToInt32(words[i]);
                }
                n++;
            }

            double diff = 0;
            int count = 0;
            for (int i = 2; i < n; i++)
            {
                if (true) //x[i - 1, 4] - x[i - 1, 1] > 0.0) // && x[i - 1, 9] - x[i - 2, 9] > 0.0) // 전일 기관(8), 외인(9) 양매수
                {
                    diff += ((x[i, 1] - x[i - 1, 4]) / x[i - 1, 4] * 100);
                    count++;
                }

            }
            diff /= count;
            diff -= 0.0098104; // 수수료
            diff *= count;
            /* 
             * 지난 599일 대상으로 테스트 
             * 전날 장마감 매수 익일 시초가 매도 
             *  코스피 레버리지  30.7% 누적(599일)
             *  코스피 인버스레  -5.3% 누적(626일)
             *  코스닥 레버리지  43.0% 누적(698일)
             *  코스닥 인버스레 -42.7% 누적(599일)
             * 전 날 기관매수시 무조건 매수 및 매도 365일 20.3%
             * 전 날 기관매수시 무조건 매수 및 매도 360일 -0.9%
             * 전 날 양매수시 무조건 매수 및 매도 360일 3.9%
             * 
             * 결론 기관이 모니터링하면 매수 매도하므로 기관을 따라 매수 매도하면 년 10% 가까이 가능할 것임
             * 위에 추가하여 etf는 against wind 하지말 것
             * 
             * nav 계산하여 유리할 때 매수하는 것도 추가하면 이익 증가 가능할 것으로 생각
             * (외, 기에게 2초 advantage 주고 하는 게임)
             * */
        }
    }
}
