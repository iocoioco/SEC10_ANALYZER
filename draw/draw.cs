
using glbl;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using work;
using misc;
using System.Diagnostics;

namespace draw
{
    public class dr
    {
        public static void draw_chart()
        {
            
            foreach (Series series in g.chart.Series)
            {
                series.Points.Clear();
            }
            g.chart.Series.Clear();
            g.chart.ChartAreas.Clear();
            g.chart.Legends.Clear();

            List<string> 보유관심종목 = new List<string>();   // 클릭된 종목, Toggle로 선택 & 취소

            보유관심종목.Add("KODEX 레버리지");
            보유관심종목.Add("KODEX 코스닥150레버리지");

            foreach (string name in g.보유종목) // if g.보유종목 Contains KODEX // MODIFIED ON 201911171304
            {
                if (!보유관심종목.Contains(name))
                {
                    보유관심종목.Add(name);
                }
            }
            foreach (string name in g.관심종목) // if g.관심종목 선택시 KODEX 종목 선제외
            {
                if (보유관심종목.Contains(name))
                {
                    continue;
                }
                보유관심종목.Add(name);
            }

            int seq = 0;
            switch (g.q)
            {
               
                case "o&s":
                    #region
                    g.nCol = g.rqwey_nCol;
                    g.nRow = g.rqwey_nRow;

                    g.dl.Clear();

                    foreach (string name in 보유관심종목)
                    {
                        g.dl.Add(name);
                    }

                    for (seq = g.gid; seq < g.sl.Count; seq++)
                    {
                        if (보유관심종목.Contains(g.sl[seq])) // KODEX 선제외
                        {
                            continue;
                        }

                        g.dl.Add(g.sl[seq]);
                        if (g.dl.Count == 2+ g.nRow * (g.nCol - 1))
                        {
                            break;
                        }
                    }

                    for (seq = 0; seq < g.dl.Count; seq++)
                    {
                        if (seq >= 2+ (g.nCol - 1) * (g.nRow))
                        {
                            break;
                        }
                        if(g.dl[seq] == "삼성중공우")
                        {
                            int index = wk.return_index_of_ogldata(g.dl[seq]);
                            g.stock o = g.ogl_data[index];
                        }

                        switch (g.draw_selection)
                        {
                            case 1:
                                draw_stock(seq, g.dl[seq]);
                                break;
                            case 2:
                                draw_foreign_institute_price(seq, g.npts_fi_dwm, g.dl[seq]);
                                break;
                            case 3:
                                draw_stock_daily(seq, g.npts_fi_dwm, "일", g.dl[seq]);
                                break;

                            default:
                                break;
                        }
                    }
                    #endregion
                    break;

                case "s&s": // not used
                    #region
                    //if (g.Group_ranking_Gid < 0)
                    //{
                    //    g.Group_ranking_Gid = 0;
                    //}

                    //g.nCol = g.rqwey_nCol;
                    //g.nRow = 4; // 3 이하가 되면 그룹내 종목 갯수보다 작아져 draw_stock에서 에러 발생 

                    //g.DL.Clear();

                    //if (보유관심종목.Count > 0) // 
                    //{
                    //    List<string> s_list = new List<string>();


                    //    for (int j = 0; j < 보유관심종목.Count; j++)
                    //    {
                    //        s_list.Add(보유관심종목[j]);

                    //        if (s_list.Count == 4 || j == 1) // first column 2 KODEXs, and after on 4 stocks each column
                    //        {
                    //            g.DL.Add(s_list.ToList());
                    //            s_list.Clear();
                    //        }
                    //    }
                    //    if (s_list.Count > 0 && s_list.Count < 4) // 보유관심종목을 4개 컬럼에 차례로 채우고, 자투리가 있으면 하나의 컬럼으로 배치
                    //    {
                    //        g.DL.Add(s_list.ToList());
                    //    }
                    //}

                    //for (int i = g.Group_ranking_Gid; i < g.Group_ranking.Count; i++)
                    //{
                    //    List<string> c_list = new List<string>(g.Group_ranking[i].종목들);
                    //    if (c_list.Count > 0)
                    //    {
                    //        if (c_list[0].Contains("KODEX")) // 불필요한 데 ... 추가 검토 필요
                    //        {
                    //            continue;
                    //        }
                    //    }

                    //    for (int j = 0; j < 보유관심종목.Count; j++)
                    //    {
                    //        if (c_list.Contains(보유관심종목[j]))
                    //        {
                    //            c_list.Remove(보유관심종목[j]);
                    //        }
                    //    }

                    //    g.DL.Add(c_list);

                    //    // more than column count, break
                    //    if (g.DL.Count >= g.rqwey_nCol)
                    //    {
                    //        break;
                    //    }
                    //}

                    //for (int i = 0; i < g.DL.Count; i++)
                    //{
                    //    if (i >= g.rqwey_nCol)
                    //    {
                    //        break;
                    //    }
                    //    for (int id = 0; id < g.DL[i].Count; id++)
                    //    {
                    //        if (id > g.rqwey_nRow)
                    //            break;

                    //        if (i == 0)
                    //        {
                    //            seq = id;
                    //        }
                    //        else
                    //        {
                    //            seq = (i - 1) * g.nRow + id + 2;
                    //        }

                    //        switch (g.draw_selection)
                    //        {
                    //            case 1:
                    //                draw_stock(seq, g.DL[i][id]);
                    //                break;
                    //            case 2:
                    //                draw_foreign_institute_price(seq, g.npts_fi_dwm, g.DL[i][id]);
                    //                break;
                    //            case 3:
                    //                draw_stock_daily(seq, g.npts_fi_dwm, "일", g.DL[i][id]);
                    //                break;
                    //            default:
                    //                break;
                    //        }
                    //    }
                    //}
                    #endregion
                    break;

                case "a&s": // not used, 4개 종목 묶음으로 중첩 가격만 차트
                    #region
                    //    g.nCol = g.rqwey_nCol;
                    //    g.nRow = g.rqwey_nRow;

                    //    for (seq = g.Group_ranking_Gid; seq < g.Group_ranking_Gid + g.nCol * g.nRow; seq++)
                    //    {
                    //        if (seq >= g.Group_ranking.Count)
                    //        {
                    //            return;
                    //        }

                    //        draw_multiple(seq - g.Group_ranking_Gid, g.Group_ranking[seq].종목들);
                    //    }
                    //    break;

                    //case "h&s": // 1개 종목 과거 일자별 차트  
                    //    g.nCol = g.rqwey_nCol;
                    //    g.nRow = g.rqwey_nRow;

                    //    if (g.clicked_Stock == null)
                    //    {
                    //        return;
                    //    }

                    //    g.date = g.saved_date; // 현재 날짜을 1번째 Plot
                    //    draw_stock(0, g.clicked_Stock); // 현재 날짜을 1번째 Plot

                    //    g.date = g.moving_reference_date;

                    //    int count = 0;
                    //    g.date_list_for_history[count++] = g.saved_date;
                    //    for (int jndex = 1; jndex < g.nCol * g.nRow; jndex++)
                    //    {
                    //        int temp_date = wk.directory_분전후(g.date, -1); // 거래전일
                    //        if (temp_date == -1)
                    //        {
                    //            continue;
                    //        }
                    //        else
                    //        {
                    //            g.date = temp_date;
                    //            g.date_list_for_history[count++] = temp_date;
                    //        }
                    //        draw_stock(jndex, g.clicked_Stock);
                    //    }
                    #endregion
                    break;

                case "o&g":
                case "s&g":
                case "a&g":
                case "h&g":
                case "kodex_leverage_single":
                case "kodex_inverse_single":
                case "kodex_kospi_group":
                case "kodex_kosdaq_group":
                    if (g.clicked_title == null && 
                        g.q != "kodex_kospi_group" && 
                        g.q != "kodex_kosdaq_group" &&
                        g.q != "kodex_leverage_single" &&
                        g.q != "kodex_inverse_single")
                        return;

                    List<string> a_list = new List<string>();
                    if (g.q == "kodex_kospi_group") // key press 6
                    {
                        a_list = g.코스피합성.ToList();
                    }
                    else if (g.q == "kodex_kosdaq_group") // key press 7
                    {
                        a_list = g.코스닥합성.ToList();
                    }
                    else if (g.q == "kodex_leverage_single" || g.q == "kodex_inverse_single") // key press 4 and 5
                    {
                    }
                    else // 일반 그룹
                    {
                        int index = g.oGL_data.FindIndex(x => x.title == g.clicked_title);
                        if (index < 0)
                            return;

                        a_list = g.oGL_data[index].stocks.ToList(); // display group list founded by clicked_stock
                    }

                    if (a_list == null || a_list.Count <= 1 && !g.clicked_Stock.Contains("KODEX"))
                        return;

                    g.dl.Clear();

                    if (g.q == "kodex_leverage_single")
                    {
                        g.dl.Add("KODEX 레버리지");
                        g.dl.Add("KODEX 코스닥150레버리지");
                    }
                    else if (g.q == "kodex_inverse_single")
                    {
                        g.dl.Add("KODEX 200선물인버스2X");
                        g.dl.Add("KODEX 코스닥150선물인버스");
                    }
                    else if (g.q == "kodex_kospi_group")
                    {
                        g.dl.Add("KODEX 레버리지");
                        g.dl.Add("KODEX 200선물인버스2X");
                        g.dl.Add("코스피혼합");
                        for (int i = 0; i < 9; i++)
                        {
                            g.dl.Add(g.지수종목[i]);
                        }
                    }
                    else if (g.q == "kodex_kosdaq_group")
                    {
                        g.dl.Add("KODEX 코스닥150레버리지");
                        g.dl.Add("KODEX 코스닥150선물인버스");
                        g.dl.Add("코스닥혼합");
                        for (int i = 10; i < 19; i++)
                        {
                            g.dl.Add(g.지수종목[i]);
                        }
                    }
                    else
                    {
                        if (a_list == null || a_list.Count < 1)
                        {
                            break;
                        }

                        foreach (var t in 보유관심종목)
                        {
                            if (!g.dl.Contains(t))    // && !g.관심종목.Contains(t)) // 지수, 보유 포함, 관심 제외
                                g.dl.Add(t);
                        }

                        // if (g.current_key_char != 'l')
                        wk.종가기준추정누적거래액_천만원순서(a_list);

                        List<string> b_list = new List<string>();
                        foreach (string stock in a_list)
                        {
                            int index = wk.return_index_of_ogldata(stock);

                            if (index < 0 || g.ogl_data[index].분당거래액_천만원 < 0) // KODEX 제외
                            {
                                continue;
                            }

                            b_list.Add(stock);
                        }

                        for (seq = 0; seq < b_list.Count; seq++)
                        {
                            if (g.dl.Contains(b_list[seq])) // KODEX, 보유주식 제외
                            {
                                continue;
                            }
                            g.dl.Add(b_list[seq]);
                        }
                    }

                    if (g.dl.Count <= 2)
                    {
                        g.nCol = 2;
                        g.nRow = 1;
                    }
                    else if (g.dl.Count <= 4)
                    {
                        g.nCol = 4;
                        g.nRow = 1;
                    }
                    else if (g.dl.Count <= 8)
                    {
                        g.nCol = 4;
                        g.nRow = 2;
                    }
                    else if (g.dl.Count <= 12)
                    {
                        g.nCol = 6;
                        g.nRow = 2;
                    }
                    else
                    {
                        g.nCol = 6;
                        g.nRow = 3;
                    }

                    for (seq = 0; seq < g.dl.Count; seq++)
                    {
                        int index = wk.return_index_of_ogldata(g.dl[seq]);
                        if (index < 0)
                        {
                            continue;
                        }

                        if (seq >= g.nCol * g.nRow)
                        {
                            break;
                        }

                        switch (g.draw_selection)
                        {
                            case 1:
                                draw_stock(seq, g.dl[seq]);
                                break;
                            case 2:
                                draw_foreign_institute_price(seq, g.npts_fi_dwm, g.dl[seq]);
                                break;
                            case 3:
                                draw_stock_daily(seq, g.npts_fi_dwm, "일", g.dl[seq]);
                                break;

                            case 4: // case 2의 draw_foreign_institute_price의 사용 데이터가 네이버와 차이 발생으로 시도 중인 API 데이터
                                    //g.npts_fi_dwm = 17;
                                    //draw_deal_history(seq, g.npts_fi_dwm, g.dl[seq]);
                                break;
                            default:
                                break;
                        }
                    }
                    break;

                default:
                    break;
            }
            g.chart.Focus();
        }


        public static void draw_stock(int seq, string stock)
        {
            int index = wk.return_index_of_ogldata(stock);
            bool shrink_draw = false;

            if (index < 0) // 혼합과 ogl_data 등록된 종목만 draw
            {
                return;
            }

            g.stock o = g.ogl_data[index];

            // 관심, 보유, 그룹 shrink_draw 적용
            if (g.관심종목.Contains(stock) || g.보유종목.Contains(stock) || g.q.Contains("g")
                || g.q.Contains("레버리지") || g.q.Contains("인버스"))
            {
                shrink_draw = g.draw_stock_shrink_or_not;
            }

            float[] size = new float[2];
            float[] location = new float[2];
            int y_min = 100000;
            int y_max = -100000;

            // 크기, 위치 결정
            draw_size_location(seq, location, size);

            string sid = "";

            if (g.q == "h&s" || g.q == "h&g") // after h&s, o.nrow - 0 and not drawn, ex.흥구석유, why ?
                              // if (g.q == "h&s" || g.previous_working_q == "h&s") // after h&s, o.nrow - 0 and not drawn, ex.흥구석유, why ?
            {
                //if (stock == g.clicked_Stock)
                //{
                o.nrow = 0;
                o.nrow = wk.Read_Stock_Minute(g.date, stock, o.x);
                if (o.nrow == 0)
                    return;
                //}
            }

            if (g.testing)
            {
                if (g.time[1] < 1) // g.time[0] = 0 means starting from 0859
                    return;
            }
            else
            {
                if (o.nrow <= 1) // no data yet, i.e. only 0859 
                    return;
            }

            // checking data corruption for (int i = 1; i < 5; i++)
            for (int i = 1; i < 5; i++)
            {
                if (o.nrow == i + 1 && o.x[i - 1, 1] == 0 && o.x[i - 1, 2] == 0 && o.x[i - 1, 3] == 0) // 정다운 2 줄, 솔브레인 모든 데이터가 0, 축 개체 문제 발생
                    return;
            }
            int count_of_zero = 0;
            for (int i = 1; i < 5; i++)
            {
                if (o.x[i, 1] == 0 && o.x[i, 2] == 0 && o.x[i, 3] == 0)
                    count_of_zero++;
            }
            if (count_of_zero == 4)
                return;

            // g.draw_shrink_time is controlled by 'o' and 'O'
            int start_time = 0;
            int end_time = -1;
            if (shrink_draw == true) 
            {
                if (g.testing)
                {
                    start_time = g.time[1] - g.draw_shrink_time;
                    if (start_time < g.time[0])
                    {
                        start_time = g.time[0];
                    }
                }
                else
                {
                    start_time = o.nrow - g.draw_shrink_time;
                    if (start_time < g.time[0])
                    {
                        start_time = g.time[0];
                    }
                }
            }
            else
            {
                start_time = g.time[0];
            }

            // modification of a and i
            int max_amount = 0;
            double amount_factor = 1.0;
            int amount_borderwidth = 1;
            int flattening_region = 10;

            for (int k = start_time; k < g.time[1]; k++) // g.time[0] = 0 means starting from 0859
            {
                if (o.x[k, 0] == 0 || o.x[k, 0] > 152100) // g.time[1]이 저장된 데이터 갯수보다 큰 경우 
                {
                    end_time = k; //CHECK endtime = o.nrow. maybe no need of iteration
                    break;
                }
                if (o.x[k, 2] > max_amount) // find maximum value of a
                {
                    max_amount = o.x[k, 2];
                }
            }
            if (end_time < 0)
            {
                end_time = g.time[1]; // start of end_time equals -1, the time of whole data not equals 0 or >152100
            }

            // amount value and thickness assignment vase on the maximum amount
            if (max_amount < 500)
            {
                amount_borderwidth = 1;
                amount_factor = 1.0;
            }
            else if (max_amount < 1000)
            {
                amount_borderwidth = 3;
                amount_factor = 0.5;
            }
            else
            {
                amount_borderwidth = 5;
                amount_factor = 1250.0 / max_amount;
            }

            // some stock with less data cannot be drawn at shrinked status
            if (end_time - start_time < 2)
            {
                return;
            }
            // 단일가거래 종목은 차트 포함시키지 않음
            if (o.x[end_time - 1, 3] == 0)
                return;

            // The start of area and drawing of stock
            string area = seq.ToString();
            g.chart.ChartAreas.Add(area);

            // 1 가격, 2 수급, 3 체강, 8 배수, 9 중심
            int total_number_of_point = 0;
            for (int i = 1; i < 10; i++) // draw lines of price, amount, intensity
            {
                // i = 1 price, i = 2 amount, i = 3 intensity, 
                // i = 8 minute multiple, 9 = center and tick multiple
                if (i == 4 || i == 5 || i == 6 || i == 7)
                {
                    continue;
                }

                sid = stock + " " + area + " " + i.ToString();

                Series t = new Series(sid);
                g.chart.Series.Add(t);

                total_number_of_point = 0;
                //int expected_number_interval = end_time - start_time;

                for (int k = start_time; k < end_time; k++)
                {
                    if (o.x[k, 0] == 0)  // time no data 
                    {
                        if (total_number_of_point < 2)
                        {
                            return;
                        }
                        else
                        {
                            break;
                        }
                    }

                    int value = 0;


                    if (i == 1) // price & 
                    {
                        if (stock.Contains("KODEX"))
                        {
                            double magnifier = 1.0;
                            double shifter = 0.0;
                            if (draw_stock_magnifier_shifter(stock, i, ref magnifier, ref shifter) == -1)
                                continue;

                            magnifier *= 5.0; //20201105, 차트 상에 가의 값이 너무 적어 표시가 잘 나지 않아 임의 숫자 5.0 곱해줌
                            value = (int)(o.x[k, 1] * magnifier);
                            if (stock.Contains("인버스"))
                            {
                                value = (int)(value - shifter);
                            }
                            else
                            {
                                value = (int)(value + shifter);
                            }

                        }
                        else
                        {
                            value = (int)(o.x[k, 1]);
                        }
                    }

                    if (i == 2) // amount
                    {
                        value = (int)(o.x[k, 2] * amount_factor);
                        if (end_time > flattening_region * 3 && k < flattening_region)
                        {
                            if (value > 200)
                                value = 200;
                            if (value < -200)
                                value = -200;
                        }
                    }

                    if (i == 3) // intensity
                    {
                        value = (int)(o.x[k, 3] / g.HUNDREAD); // CHECK

                        if (value > 500) // intensity is above 500, then change to 500
                        {
                            value = 500; // 0430
                        }

                        if (end_time > flattening_region * 3 && k < flattening_region)
                        {
                            if (value > 200)
                                value = 200;
                        }

                        if ((g.q == "kodex_leverage_single" || g.q == "kodex_inverse_single") && k < flattening_region)
                        {
                            if (value > 200)
                                value = 200;
                        }
                    }

                    if (i == 8) // minute multiple
                    {
                        value = (int)((o.x[k, 8] - o.x[k, 9]) * g.v.배수과장배수); // minute multiple draw, if inverse, swapped in Read_Stock
                        if (stock.Contains("KODEX"))
                            value *= 4;
                        if (value > 1000) // value is much larger to fit in chart sometimes, especially at 0859
                            value = 1000;
                        if (value < -1000) // value is much larger to fit in chart sometimes, especially at 0859
                            value = -1000;

                        if (end_time > flattening_region * 3 && k < flattening_region)
                        {
                            if (value > 60)
                                value = 60;
                            if (value < -60)
                                value = -60;
                        }
                    }

                    if (i == 9) // center line and tick multiple
                    {
                        value = 0;
                        if (k >= end_time - g.array_size && !g.testing) // tick multiple draw
                        {
                            int limit = (int)(100 * g.v.배수과장배수);
                            int multiple_factor = (int)(g.v.배수과장배수 * 10); // five times of min. for tick

                            int tick_index = (end_time - 1) - k;
                            if (tick_index < 0)
                            {
                                continue;
                            }

                            if (stock.Contains("코스피혼합") && g.dl[0].Contains("레브리지"))
                            {
                                value = (int)(g.kospi_틱매수배[tick_index] -
                                                g.kospi_틱매도배[tick_index]) * multiple_factor;
                            }
                            else if (stock.Contains("코스피혼합") && g.dl[0].Contains("인버스"))
                            {
                                value = (int)(g.kospi_틱매도배[tick_index] -
                                    g.kospi_틱매수배[tick_index]) * multiple_factor;
                            }
                            else if (stock.Contains("코스닥혼합") && g.dl[2].Contains("레브리지"))
                            {
                                value = (int)(g.kosdaq_틱매수배[tick_index] -
                                                g.kosdaq_틱매도배[tick_index]) * multiple_factor;
                            }
                            else if (stock.Contains("코스닥혼합") && g.dl[2].Contains("인버스"))
                            {
                                value = (int)(g.kosdaq_틱매도배[tick_index] -
                                                g.kosdaq_틱매수배[tick_index]) * multiple_factor;
                            }
                            else if (o != null)
                            {
                                value = (int)(o.틱매수배[tick_index] -
                                          o.틱매도배[tick_index]) * multiple_factor;
                            }
                            else
                            {

                            }

                            if (value > limit)
                            {
                                value = limit;
                            }
                            if (value < -limit)
                            {
                                value = -limit;
                            }
                        }
                    }

                    t.Points.AddXY(((int)(o.x[k, 0] / g.HUNDREAD)).ToString(), value);

                    total_number_of_point++;
                    if (value < y_min)
                    {
                        y_min = value;
                    }
                    if (value > y_max)
                    {
                        y_max = value;
                    }
                }
                if (total_number_of_point < 2)
                    return;

                draw_stock_mark(stock, start_time, i, total_number_of_point, o.x, t);

                // ChartArea, ChartType, BorderWidth
                t.ChartArea = area;

                t.ChartType = SeriesChartType.Line;
                t.XValueType = ChartValueType.Date;

                // Line Color 
                switch (i)
                {
                    case 1: // 가격
                        t.Color = Color.Red;
                        t.BorderWidth = 1;
                        break;
                    case 2: // 수급
                        t.Color = Color.Green;
                        t.BorderWidth = amount_borderwidth;
                        if (amount_borderwidth == 1)
                        {
                            t.Color = Color.Green;
                        }
                        else if (amount_borderwidth == 3)
                        {
                            t.Color = Color.DarkGreen;
                        }
                        else
                        {
                            t.Color = Color.LawnGreen;
                        }
                        break;
                    case 3: // 체강
                        t.Color = Color.Blue;
                        t.BorderWidth = 1;
                        break;
                    case 8: // 매수 & 매도 배수 차이
                        t.Color = Color.Orange;
                        t.BorderWidth = 2;
                        break;

                    case 9:  // 배수의 중심선
                        t.Color = Color.Black;
                        t.BorderWidth = 2;
                        break;
                    default:
                        break;
                }
                t.IsVisibleInLegend = false;
            }







            if (g.v.수급과장배수 != 0)
            {
                if (stock.Contains("KODEX")) // KODEX
                {
                    double magnifier = 1.0;
                    double shifter = 0.0;

                    //magnifier *= g.v.수급과장배수 / 300.0;
                    int[] id1 = { 4, 5, 6, 10, 11 }; // 기관, 외인, 개인, us_index, 연기 각각 거래액
                    for (int i = 0; i < id1.Length; i++)
                    {
                        draw_stock_magnifier_shifter(stock, id1[i], ref magnifier, ref shifter); // shift for KODEX
                        sid = stock + area + " " + id1[i].ToString();
                        Series t = new Series(sid);
                        g.chart.Series.Add(t);

                        for (int k = start_time; k < end_time; k++)
                        {
                            if (o.x[k, 0] == 0) // if time = 0, break
                            {
                                break;
                            }

                            int value;
                            if (id1[i] == 10)
                            {
                                value = o.x[k, id1[i]] * 50; // US (Kosdaq + SP) / 2.0, and four times mangnified // 20201105 차트 상에 가의 값이 너무 적어 표시가 잘 나지 않아 임의 숫자 5.0 곱해줌
                                value = (int)(value * magnifier);
                                value += (int)shifter;
                                if (o.종목.Contains("인버스"))
                                {
                                    value *= -1;
                                }
                            }
                            else
                            {
                                value = (int)(o.x[k, id1[i]] * magnifier);
                                if (id1[i] == 11)
                                    value *= 5; // 다섯 배 확대하여 본다
                                value += (int)shifter;
                            }


                            t.Points.AddXY(((int)(o.x[k, 0] / g.HUNDREAD)).ToString(), value);
                            if (value < y_min)
                            {
                                y_min = value;
                            }
                            if (value > y_max)
                            {
                                y_max = value;
                            }
                        }
                        draw_stock_mark(stock, start_time, id1[i], total_number_of_point, o.x, t); // DO I NEED THIS

                        t.ChartArea = area;
                        t.ChartType = SeriesChartType.Line;
                        t.XValueType = ChartValueType.Date;
                        t.IsVisibleInLegend = false;
                        // Line Color 
                        switch (id1[i])
                        {
                            case 4:
                                t.Color = Color.Cyan; // institute
                                t.BorderWidth = 1;
                                break;
                            case 5:
                                t.Color = Color.Magenta; // foreign
                                t.BorderWidth = 1;
                                break;
                            case 6:
                                t.Color = Color.Black; // individual
                                t.BorderWidth = 1;
                                break;
                            case 10:
                                t.Color = Color.Blue; // US (Nasdaq + SP) / 2.0
                                t.BorderWidth = 1;
                                break;
                            case 11:
                                t.Color = Color.Brown; // pension
                                t.BorderWidth = 1;
                                break;
                            default:
                                break;
                        }
                        if (g.q == "kodex_leverage_single" || g.q == "kodex_inverse_single")
                        {
                            switch (id1[i])
                            {
                                case 5:
                                    t.BorderWidth = 2;
                                    break;
                                case 6:
                                    t.BorderWidth = 2;
                                    break;
                                case 10:
                                    t.BorderWidth = 2;
                                    break;
                                default:
                                    break;
                            }
                        }
                        else
                        {
                            t.BorderWidth = 1;
                        }
                    }
                }











                else if (stock.Contains("혼합")) // 혼합
                {
                    // 4, 상해, 11 US
                    // 5, 항생, 6 일본 제외 
                    int[] id2 = { 4, 6, 11 }; // institue, foreign, individual, financial investment, pension (money dealt)
                    for (int i = 0; i < id2.Length; i++)
                    {

                        sid = stock + area + " " + id2[i].ToString();
                        Series t = new Series(sid);
                        g.chart.Series.Add(t);

                        for (int k = start_time; k < end_time; k++)
                        {
                            if (o.x[k, 0] == 0) // if time = 0, break
                            {
                                break;
                            }

                            int value = o.x[k, id2[i]];
                            if (id2[i] == 11)
                            {
                                value = (int)(o.x[k, 10] + o.x[k, 11]) * 2; // four times mangnified 
                            }

                            //if (id2[i] == 10 || id2[i] == 11)
                            //{
                            //    if (g.dl.Count == 4)
                            //    {
                            //        if (g.dl[0].Contains("인버스"))
                            //        {
                            //            value *= -1;
                            //        }
                            //    }
                            //}

                            t.Points.AddXY(((int)(o.x[k, 0] / g.HUNDREAD)).ToString(), value);
                            if (value < y_min)
                            {
                                y_min = value;
                            }
                            if (value > y_max)
                            {
                                y_max = value;
                            }
                        }
                        draw_stock_mark(stock, start_time, id2[i], total_number_of_point, o.x, t);

                        t.ChartArea = area;
                        t.ChartType = SeriesChartType.Line;
                        t.XValueType = ChartValueType.Date;
                        t.IsVisibleInLegend = false;
                        switch (id2[i])
                        {
                            case 4:
                                t.Color = Color.DarkRed; // Shanghi
                                t.BorderWidth = 1;
                                break;

                            case 5:
                                t.Color = Color.YellowGreen; // Hangsehng
                                t.BorderWidth = 2;
                                break;

                            case 6:
                                t.Color = Color.Orange; // Japan
                                t.BorderWidth = 1;
                                break;
                            case 11:
                                t.Color = Color.Blue; // US average
                                t.BorderWidth = 1;
                                break;
                            default:
                                break;
                        }
                    }
                }
                else // general stocks 
                {
                    double percentile_multiplier = 1;
                    if (o.x[end_time - 1, 7] > g.EPS) // 누적거래량 ! = 0
                    {
                        percentile_multiplier = 100.0 / o.x[end_time - 1, 7]; // marketeye 
                    }

                    int[] id3 = { 4, 5, 6 }; // program, foreign, institute (amount dealt)
                    for (int i = 0; i < id3.Length; i++)
                    {
                        sid = stock + area + " " + id3[i].ToString();
                        Series t = new Series(sid);
                        g.chart.Series.Add(t);

                        for (int k = start_time; k < end_time; k++)
                        {
                            if (o.x[k, 0] == 0) // if time = 0, end of data, break
                            {
                                break;
                            }
                            int value = (int)(o.x[k, id3[i]] * percentile_multiplier * g.v.수급과장배수 * o.수급과장배수); // 전체 & 종목 (수급과장배수)
                            t.Points.AddXY(((int)(o.x[k, 0] / g.HUNDREAD)).ToString(), value);
                            if (value < y_min)
                            {
                                y_min = value;
                            }
                            if (value > y_max)
                            {
                                y_max = value;
                            }
                        }
                        draw_stock_mark(stock, start_time, id3[i], total_number_of_point, o.x, t);
                        t.ChartArea = area;
                        t.ChartType = SeriesChartType.Line;
                        t.XValueType = ChartValueType.Date;
                        t.IsVisibleInLegend = false;
                        switch (id3[i])
                        {
                            case 4:
                                t.Color = Color.Black; // 당일프로그램순매수량
                                t.BorderWidth = 3;
                                break;
                            case 5:
                                t.Color = Color.Magenta; // 당일외인순매수량
                                t.BorderWidth = 2;
                                break;
                            case 6:
                                t.Color = Color.Cyan; // 당일기관순매수량
                                t.BorderWidth = 2;
                                break;
                            default:
                                break;
                        }
                    }
                }
            }

            string label_title = draw_stock_title(o, seq, index, o.x, start_time, end_time, o.nrow); // g.testing, o.nrow = g.MAX_ROW

            //int a = g.dl.Count(stock);
            //foreach(var t in g.dl)
            //{
            //    if(t == stock)
            //    {

            //    }
            //}
            //int count = 0;
            //foreach (string y in g.dl.FindAll(x => x == stock))
            //{ 
            //    if(y == stock)
            //    {
            //        count++;
            //    }
            //}
            //if (count >= 2)
            //    label_title += "seq";
            

            // label_title += seq.ToString() + "\n";


            g.chart.Series.Add(label_title);
            g.chart.Legends.Add(new Legend(label_title));
            g.chart.Legends[label_title].Docking = Docking.Top;//0505
            g.chart.Legends[label_title].Position =
                new ElementPosition(location[0], location[1], 15, 7);
            g.chart.Legends[label_title].IsTextAutoFit = true;
            g.chart.Legends[label_title].Enabled = true;
            g.chart.Legends[label_title].BorderWidth = 1;
            g.chart.Legends[label_title].TitleForeColor = Color.Cyan;
            g.chart.Series[label_title].Legend = label_title;
            g.chart.Series[label_title].IsVisibleInLegend = true;


            //g.chart.Titles.Add("이 것은 테스트입니다");




            //g.chart.Legends[label_title].Font = new Font("Arial", 11F); //not working 
            //g.chart.Legends[label_title].ForeColor = Color.FromArgb(102, 102, 102);
            //g.chart.Legends[label_title].InsideChartArea = "label_title";
            //g.chart.Legends[label_title].M = LegendStyle.Column;
            //g.chart.Legends[label_title].

            //g.chart.Series[label_title].MarkerBorderWidth = 5;
            //g.chart.Series[label_title].Label.AlignmentHorizontal = Right;

            // No X Grid, Interval of Label

            //y_max = y_max + (int)((y_max - y_min) * 0.4); // 0505 0.8 from 0.6, not display upper part in 's'
            //y_min = y_min - (int)((y_max - y_min) * 0.1);

            y_min = (int)Math.Floor((double)y_min / 100) * 100; //0505

            double y_top_margin_factor = 0.45;

            if ((stock.Contains("KODEX") && (g.q == "kodex_leverage_single" || g.q == "kodex_inverse_single")) ||
                stock.Contains("혼합")) // modified on 20200812
            {
                y_top_margin_factor = 0.1;
            }


            y_max = (int)(y_max + (y_max - y_min) * y_top_margin_factor);
            y_max = (int)Math.Ceiling((double)y_max / 100) * 100;
            if (y_min == 0 && y_max == 0) // 화면 전체 X자로 표시되면서 중지되는 이유는 y_min = 0, y_max = 0가 되기때문인 데 이를 방지하기 위해 다음의 편법 사용
            {
                y_min = -100;
                y_max = 100;
            }





            if (stock.Contains("KODEX"))
            {
                ms.kodex_shifter_calculation(stock, 0.0, y_min, y_max);
            }
            g.chart.ChartAreas[area].AxisY.Minimum = y_min;
            g.chart.ChartAreas[area].AxisY.Maximum = y_max;
            g.chart.ChartAreas[area].AxisX.MajorGrid.Enabled = false;
            g.chart.ChartAreas[area].AxisY.MajorGrid.Enabled = false;
            g.chart.ChartAreas[area].AxisX.Interval = total_number_of_point - 1;
            g.chart.ChartAreas[area].AxisX.IntervalOffset = 1;
            g.chart.ChartAreas[area].AxisX.LabelStyle.Enabled = true;

            g.chart.ChartAreas[area].Position.X = location[0];
            g.chart.ChartAreas[area].Position.Y = location[1];
            g.chart.ChartAreas[area].Position.Width = size[0];
            g.chart.ChartAreas[area].Position.Height = size[1];
        }


        public static int draw_stock_magnifier_shifter(string stock, int id, ref double magnifier, ref double shifter)
        {
            int i = 0;
            int j = 0;


            switch (stock)
            {
                case "KODEX 레버리지":
                    i = 0;
                    break;
                case "KODEX 200선물인버스2X":
                    i = 1;
                    break;
                case "KODEX 코스닥150레버리지":
                    i = 2;
                    break;
                case "KODEX 코스닥150선물인버스":
                    i = 3;
                    break;
            }

            switch (id)
            {
                case 1: // price
                    j = 0;
                    break;
                case 4: // money
                case 5:
                case 6:
                case 11:
                    j = 1;
                    break;
                case 10: // US
                    j = 2;
                    break;
                default:
                    return -1; // input mitake
            }

            magnifier = g.k.magnifier[i, j];
            shifter = g.k.shifter[i, j];

            return 0;
        }


        public static void draw_size_location(int seq, float[] location, float[] size)
        {
            size[0] = (100.0F - 20.0F) / g.nCol; // g.nCol is always even number
            size[1] = 100F / g.nRow;
            int row;
            int col;
            switch (g.q)
            {
                case "o&s":
                case "s&s":
                case "o&g":
                case "s&g":
                case "a&g":
                    if (seq == 0)
                    {
                        size[1] = 50.0F;
                        location[0] = 0.0F;
                        location[1] = 0.0F;
                    }
                    else if (seq == 1)
                    {
                        size[1] = 50.0F;
                        location[0] = 0.0F;
                        location[1] = 50.0F;
                    }
                    else
                    {
                        row = (seq - 2) % g.nRow;
                        col = (seq - 2) / g.nRow + 1;

                        if (col >= g.nCol / 2)
                        {
                            location[0] = size[0] * col + 20.0F;
                        }
                        else
                        {
                            location[0] = size[0] * col;
                        }
                        location[1] = size[1] * row;
                    }
                    break;

                case "h&s":
                case "a&s":
                
                case "kodex_leverage_single":
                case "kodex_inverse_single":
                case "kodex_kospi_group":
                case "kodex_kosdaq_group":
                    row = seq % g.nRow;
                    col = seq / g.nRow;

                    if (col >= g.nCol / 2)
                    {
                        location[0] = size[0] * col + 20.0F;
                    }
                    else
                    {
                        location[0] = size[0] * col;
                    }
                    location[1] = size[1] * row;
                    break;
            }
        }


        public static string draw_stock_title(g.stock o, int seq, int index, int[,] x, int start_time, int end_time, int total_nrow)
        {
            string stock = o.종목;

            string label_title = "";

            if (g.q == "h&s" && o != null)
            {
                if (seq == 0)
                {
                    label_title = stock; // 종목
                }
                else
                {
                    label_title = g.date.ToString(); // h&s의 경우 날짜
                }

                ms.testing_eval_stock_calculation(o);
                label_title += " " + seq.ToString() +                        //sid.ToString() + // 날짜 중복 문제로 생각없이 추가하였는 데 필요 ?
                                  " " + (o.종가기준추정거래액_천만원 / 10).ToString() +
                                  " " + (o.프로그램누적거래액_천만원 / 10).ToString() +
                                  " " + (o.외인누적거래액_천만원 / 10).ToString() +
                                  " " + (o.기관누적거래액_천만원 / 10).ToString();
            }

            else if (stock.Contains("혼합"))
            {
                if ((stock == "코스피혼합") && g.dl[0].Contains("인버스"))  // kodex&s 
                    label_title = stock + "                 " + "인버스";
                if ((stock == "코스피혼합") && g.dl[0].Contains("레버리지")) // kodex&s and kodex_kospi_group
                    label_title = stock + "                 " + "레버리지";
                if ((stock == "코스닥혼합") && g.dl[2].Contains("인버스")) // kodex&s
                    label_title = stock + "                 " + "인버스";
                if ((stock == "코스닥혼합") && (g.dl[0].Contains("코스닥") || g.dl[2].Contains("레버리지"))) // kodex&s and kodex_kosdaq_group
                    label_title = stock + "                 " + "레버리지";
            }
            else
            {
                //int 프로그램누적거래액_천만원 = 0;
                //int 외인누적거래액_천만원 = 0;
                //int 기관누적거래액_천만원 = 0;

                //if (end_time != 0) // 
                //{
                //    if (o.x[end_time - 1, 7] > g.EPS) // 누적거래량 ! = 0
                //    {
                //        double factor = 0.0;
                //        if (g.testing)
                //            factor = o.전일종가 / 10000000.0; // 천만원 단위
                //        else
                //            factor = o.현재가 / 10000000.0;

                //        프로그램누적거래액_천만원 = (int)(o.x[end_time - 1, 4] *factor); // marketeye
                //        외인누적거래액_천만원 = (int)(o.x[end_time - 1, 5] * factor); // marketeye
                //        기관누적거래액_천만원 = (int)(o.x[end_time - 1, 6] * factor); // marketeye
                //    }
                //}

                if (g.q != "h&s")
                {
                    //List<string> a_list = wk.종목포함_최고가그룹리스트(stock); // 20201107
                    //if (a_list == null || a_list.Count <= 1 && !stock.Contains("KODEX"))
                    //{
                    //    label_title = "n " + label_title;
                    //}

                    if (g.보유종목.Contains(stock))
                    {
                        label_title = "&&" + label_title;
                    }
                    if (g.관심종목.Contains(stock))
                    {
                        label_title = "**" + label_title;
                    }


                    if(o.분당거래액_천만원 > 50)
                    {
                        label_title += stock + " " +
                                      (Math.Round(o.분당거래액_천만원 /10).ToString("#")) + "/" +
                                      (Math.Round(o.분당프로그램매수액_천만원/10)).ToString("#") + " " +
                                      (o.종가기준추정거래액_천만원 / 10).ToString() + " " +
                                      ((int)(o.프로그램누적거래액_천만원 / 10)).ToString() + " " +
                                      ((int)(o.외인누적거래액_천만원 / 10)).ToString() + " " +
                                      ((int)(o.기관누적거래액_천만원 / 10)).ToString();
                    }
                    else
                    {
                        label_title += stock + " " +
                                      (Math.Round(o.분당거래액_천만원 / 10)).ToString("#.#") + "/" +
                                      (Math.Round(o.분당프로그램매수액_천만원 / 10)).ToString("#.#") + " " +
                                      (o.종가기준추정거래액_천만원 / 10).ToString() + " " +
                                      ((int)(o.프로그램누적거래액_천만원 / 10)).ToString() + " " +
                                      ((int)(o.외인누적거래액_천만원 / 10)).ToString() + " " +
                                      ((int)(o.기관누적거래액_천만원 / 10)).ToString();
                    }

                }
            }

            if (index >= 0 && g.testing && o.selected_for_group == false)
            {
                label_title = "$$$ " + label_title;
            }

            label_title += ("\n" + draw_stock_title_tick_minute_string(o, x, start_time, end_time)); // ERROR 더 있다는 메시지

            // 일반, 프로그램의 틱당 거래액
            if (!(stock.Contains("KODEX") || stock.Contains("혼합")))
            {
                label_title += "\n";
                for (int i = 0; i < 5; i++)
                {
                    label_title += o.틱프로돈[i].ToString() + " ";
                }
                //label_title += "\n";
                //for (int i = 0; i < 5; i++)
                //{
                //    label_title += o.분프로돈[i].ToString() + " ";
                //}
                //for (int i = end_time - 1; i > end_time - 5; i--)
                //{
                //    if (i < 1)
                //        break;


                //    if (i == end_time - 1)
                //    {
                //        label_title += " " + ((int)o.분당프로그램매수액_천만원).ToString();
                //        if (o.분당거래액_천만원 > 0)
                //        {
                //            label_title += "(" + ((int)(o.분당프로그램매수액_천만원 * 100 / o.분당거래액_천만원)).ToString() + ") "; // 분당프로그램매수_천만원 단위 천만원, 분당거래액_천만원 단위 억원
                //        }
                //        else
                //        {
                //            label_title += "(0) ";
                //        }
                //    }
                //    else
                //    {
                //        double factor = 0.0;
                //        if (g.testing)
                //            factor = o.전일종가 / 10000000.0; // 천만원
                //        else
                //            factor = o.현재가 / 10000000.0; // 천만원

                //        label_title += ((int)((o.x[i, 4] - x[i - 1, 4]) * factor)).ToString() + " ";
                //    }
                //}

                //string str = wk.return_Group_ranking(stock) + "(" + wk.return_Group_ranking_통과종목수(stock) + ")"; 
                    //if (end_id - start_time > 3)
                    //    t.Points[(end_id - start_time) / 2].Label = str; // ranking of the group in the first of point of amount
                    //else
                    //t.Points[start_time].Label = str; // ranking of the group in the first of point of amount
                    //t.Points[0].Label = str; // ranking of the group in the first of point of amount

                // label_title += "  " + str; 
                label_title += "  " + x[end_time - 1, 10] + "/" + x[end_time - 1, 11] + " " + o.dev_avr; // 수급, 체강의 연속점수
            }
            return label_title;
        }
    

        public static void draw_stock_mark(string stock, int start_time, int i, int total_number_of_point, int[,] x, Series t) // i (가격, 수급, 체강 순으로 Series)
        {
            // draw mark for price, amount, intensity
            // npts : total number of o.x[,], end_xid : end x id of o.x[,]
            int index = wk.return_index_of_ogldata(stock);

            //MessageBox.Show("체결과정 체크 중 문제 발생");

            if (index < 0)
            {
                return;
            }

            g.stock o = g.ogl_data[index];

            if (stock.Contains("혼합"))
            {
                if (i == 5 || i == 6) // 항생 & 일본 그리지않음
                    return;
            }

            if (total_number_of_point == 0) { return; }
            string[] difference = new string[7];
            int k;

            string a = g.q; // 삭제

            int end_id = start_time + total_number_of_point - 1;
            // if price increases more than the specified value, than circle mark
            for (int m = start_time + 1; m <= end_id; m++)
            {
                if (i == 1) // price
                {
                    int p_id = m - start_time; // g.time[0] != 0, shifting needed
                    int price_change = x[m, 1] - x[m - 1, 1];

                    if (stock.Contains("KODEX")) 
                    {
                        price_change = x[m, 1] - x[m - 1, 1];
                        if (price_change >= 20) // KODEX 가격 변화가 20 이상 상승하는 경우 Circle로 표시
                        {
                            if (price_change > 40)
                            {
                                t.Points[p_id].MarkerColor = Color.Blue;
                            }
                            else if (price_change > 30)
                            {
                                t.Points[p_id].MarkerColor = Color.Green;
                            }
                            else
                            {
                                t.Points[p_id].MarkerColor = Color.Red;
                            }
                            t.Points[p_id].MarkerSize = 11;
                            t.Points[p_id].MarkerStyle = MarkerStyle.Circle;
                        }
                    }
                    else
                    {
                        if (price_change >= 100)
                        {
                            if (price_change > 300)
                            {
                                t.Points[p_id].MarkerColor = Color.Black;
                            }

                            else if (price_change > 200)
                                t.Points[p_id].MarkerColor = Color.Blue;
                            else if (price_change > 150)
                                t.Points[p_id].MarkerColor = Color.Green;
                            else
                                t.Points[p_id].MarkerColor = Color.Red;

                            t.Points[p_id].MarkerSize = 9;
                            t.Points[p_id].MarkerStyle = MarkerStyle.Cross;
                        }







                        if (x[m, 0] >= 90200) // 시간이 9:02 이후 배수 차이가 100 이상일 때
                        {
                            int multiple_difference = x[m, 8] - x[m, 9]; // 배수 차이 
                            if (multiple_difference > 100)
                            {

                                t.Points[p_id].MarkerSize = 9;
                                t.Points[p_id].MarkerStyle = MarkerStyle.Circle;

                                if (multiple_difference > 500)
                                    t.Points[p_id].MarkerColor = Color.Black;
                                else if (multiple_difference > 300)
                                    t.Points[p_id].MarkerColor = Color.Blue;
                                else if (multiple_difference > 200)
                                    t.Points[p_id].MarkerColor = Color.Green;
                                else
                                    t.Points[p_id].MarkerColor = Color.Red;


                            }
                        }
                    }

                    if (g.q != "h&s")
                    {
                        int mark_size = 0;

                        if (o.분당거래액_천만원 < 10)
                        {
                        }
                        else if (o.분당거래액_천만원 < 50)
                        {
                            t.Points[0].MarkerColor = Color.Red;
                            mark_size = 10;
                        }
                        else if (o.분당거래액_천만원 < 100)
                        {
                            t.Points[0].MarkerColor = Color.Red;
                            mark_size = 15;
                        }
                        else if (o.분당거래액_천만원 < 200)
                        {
                            t.Points[0].MarkerColor = Color.Green;
                            mark_size = 15;
                        }
                        else if (o.분당거래액_천만원 < 300)
                        {
                            t.Points[0].MarkerColor = Color.Green;
                            mark_size = 20;
                        }
                        else if (o.분당거래액_천만원 < 500)
                        {
                            t.Points[0].MarkerColor = Color.Blue;
                            mark_size = 20;
                        }
                        else if (o.분당거래액_천만원 < 800)
                        {
                            t.Points[0].MarkerColor = Color.Blue;
                            mark_size = 30;
                        }
                        else if (o.분당거래액_천만원 < 1200)
                        {
                            t.Points[0].MarkerColor = Color.Black;
                            mark_size = 30;
                        }
                        else if (o.분당거래액_천만원 < 1700)
                        {
                            t.Points[0].MarkerColor = Color.Black;
                            mark_size = 40;
                        }
                        else
                        {
                            t.Points[0].MarkerColor = Color.Black;
                            mark_size = 50;
                        }

                        if (g.q == "kodex_leverage_single" || g.q == "kodex_inverse_single")
                            t.Points[0].MarkerSize = mark_size * 5;
                        else
                            t.Points[0].MarkerSize = mark_size;

                        if (price_change >= 0)
                            t.Points[0].MarkerStyle = MarkerStyle.Circle;
                        else
                            t.Points[0].MarkerStyle = MarkerStyle.Cross;
                    }
                }








                // magenta and cyan cross mark on the lines of amount and intensity
                if (i == 2 || i == 3)
                {
                    if (stock.Contains("KODEX") || stock.Contains("혼합"))
                    {
                        int positive_count = 0; // count of positive intensity
                        int sequence_id = m - start_time;

                        if (sequence_id - g.npts_for_magenta_cyan_mark < 0)
                        {
                            continue;
                        }

                        for (int n = 0; n < g.npts_for_magenta_cyan_mark; n++)
                        {
                            if (m - 1 - n < 0)
                            {
                                break;
                            }

                            int diff = x[m - n, i] - x[m - 1 - n, i];
                            //if (i == 3) // already divided by g.HUNDRED somewhere

                            if (diff > 0) // modified from ">=" to ">"
                            {
                                positive_count++;
                            }
                        }
                        if (positive_count >= g.npts_for_magenta_cyan_mark) // magenta and cyan
                                                                            // if (positive_count >= g.npts_for_magenta_cyan_mark && x[m, 1] - x[m - 1, 1] >= 0) // the last price lowering magenta and cyan excluded
                        {
                            if (i == 2)
                            {
                                t.Points[sequence_id].MarkerColor = Color.Magenta; // amount
                            }
                            else
                            {
                                t.Points[sequence_id].MarkerColor = Color.Cyan;  // intensity
                            }

                            t.Points[sequence_id].MarkerStyle = MarkerStyle.Cross;
                            t.Points[sequence_id].MarkerSize = 11;
                        }
                    }

                    else // 일반
                    {
                        if (x[m, i + 8] >= g.npts_for_magenta_cyan_mark) // the last price lowering magenta and cyan excluded
                                                                            //if (x[m, i + 8] >= g.npts_for_magenta_cyan_mark && x[m, 1] - x[m - 1, 1] >= 0) // the last price lowering magenta and cyan excluded
                        {
                            int sequence_id = m - start_time;
                            if (i == 2)
                            {

                                t.Points[sequence_id].MarkerColor = Color.Magenta; // amount
                            }
                            else
                            {
                                t.Points[sequence_id].MarkerColor = Color.Cyan;  // intensity
                            }

                            t.Points[sequence_id].MarkerStyle = MarkerStyle.Cross;
                            t.Points[sequence_id].MarkerSize = 11;
                        }
                    }
                }
            }






            //Label of price, amount and intensity at the end point
            if (i == 1 || i == 2 || i == 3 ||
                (i == 4) ||
                (i == 5 && stock.Contains("KODEX")) ||
                (i == 6 && stock.Contains("KODEX")) ||
                (i == 10 && stock.Contains("KODEX")) ||
                (i == 11 && stock.Contains("KODEX")))
            {

                int plus_count = 0;
                int sum = 0;

                // 랭킹 표시 (중간, 시초의 경우는 처음)
                if (i == 2 && !stock.Contains("KODEX"))
                {
                    //string str = wk.return_Group_ranking(stock) + "(" + wk.return_Group_ranking_통과종목수(stock) + ")"; 
                   
                    //t.Points[0].Label = str; // ranking of the group in the first of point of amount 
                }

                // 
                if (i == 3)
                {
                    t.Points[total_number_of_point - 1].Label = "      " + ((int)(x[end_id, i] / g.HUNDREAD)).ToString();
                }
                else if (i == 4 && !stock.Contains("KODEX"))
                {
                    // 천만원을 억원으로 수정하기 위해 "/10" 추가 20210909
                    t.Points[total_number_of_point - 1].Label = ((int)o.프로그램누적거래액_천만원/10).ToString();
                }
                else
                {
                    t.Points[total_number_of_point - 1].Label = "      " + x[end_id, i].ToString();
                }

                // Curve End Label
                for (k = 0; k < 4; k++)
                {
                    if (end_id - k - 1 < 0)
                    {
                        break;
                    }

                    int d;
                    if (i == 3) // modified on 2020/0801/1013. due to intensity = 0 display problem
                        d = (int)(x[end_id - k, i] / g.HUNDREAD) - (int)(x[end_id - k - 1, i] / g.HUNDREAD);
                    else if (i == 4 && !stock.Contains("KODEX"))
                    {
                        // 천만원을 억원으로 수정하기 위해 "/10" 추가 20210909
                        d = o.분프로돈[k]/10;
                    }
                    else
                        d = x[end_id - k, i] - x[end_id - k - 1, i]; // difference

                    if (k < 5)
                    {
                        if (d > 0)
                        {
                            plus_count++;
                        }

                        if (d < 0)
                        {
                            plus_count--;
                        }
                        sum += d;
                    }

                    if (d >= 0)
                    {
                        difference[k] = "+" + d.ToString();
                    }
                    else
                    {
                        difference[k] = d.ToString();
                    }

                    if (difference[k] != null)
                    {
                        t.Points[total_number_of_point - 1].Label += difference[k];
                        if (i == 1)
                        {
                            t.LabelForeColor = Color.Red;  // label of price
                        }

                        if (i == 2)
                        {
                            t.LabelForeColor = Color.Green;  // label of amount
                        }

                        if (i == 3)
                        {
                            t.LabelForeColor = Color.Blue; // label of intensity
                        }
                        if (i == 4)
                        {
                            if(!stock.Contains("KODEX"))
                            {
                                t.LabelForeColor = Color.Black;  // 프로그램 매수 (분)
                               // not working t.Font = new Font("Calibri", 12);
                            }
                            else
                                t.LabelForeColor = Color.DarkCyan;  // KODEX 기관
                        }

                        if (i == 5)
                        {
                            t.LabelForeColor = Color.Magenta;  // KODEX 외인
                        }

                        if (i == 6)
                        {
                            t.LabelForeColor = Color.Black; // KODEX 개인
                        }

                        if (i == 10)
                        {
                            t.LabelForeColor = Color.Blue;  // KODEX US index
                        }

                        if (i == 11)
                        {
                            t.LabelForeColor = Color.Brown; // KODEX 연기
                            //t.BorderWidth = 5;
                        }
                    }
                }
            }
        }


        public static string draw_stock_title_tick_minute_string(g.stock o, int[,] x, int start_time, int end_time)
        {
            string tick_minute_string = "";
            string stock = o.종목;

            if (stock.Contains("코스피혼합") && g.dl[0].Contains("레버리지"))
            {
                for (int i = 0; i < 4; i++) // draw_stock_title_tick_minute_string
                {
                    //tick_minute_string += ((int)(g.kospi_틱매수배[i] * g.TEN)).ToString() + "/" + //ToString("0.#");
                    //     ((int)(g.kospi_틱매도배[i] * g.TEN)).ToString() + " ";
                    tick_minute_string += ((int)(g.kospi_틱매수배[i])).ToString() + "/" + //ToString("0.#");
                            ((int)(g.kospi_틱매도배[i])).ToString() + " ";
                }
                tick_minute_string += "\n";
            }
            else if (stock.Contains("코스피혼합") && g.dl[0].Contains("인버스"))
            {
                for (int i = 0; i < 4; i++) // draw_stock_title_tick_minute_string
                {
                    //tick_minute_string += ((int)(g.kospi_틱매도배[i] * g.TEN)).ToString() + "/" + //ToString("0.#");
                    //     ((int)(g.kospi_틱매수배[i] * g.TEN)).ToString() + " ";
                    tick_minute_string += ((int)(g.kospi_틱매도배[i])).ToString() + "/" + //ToString("0.#");
                            ((int)(g.kospi_틱매수배[i])).ToString() + " ";
                }
                tick_minute_string += "\n";
            }

            else if (stock.Contains("코스닥혼합") && g.dl[0].Contains("레버리지"))
            {
                for (int i = 0; i < 4; i++) // draw_stock_title_tick_minute_string
                {
                    //tick_minute_string += ((int)(g.kosdaq_틱매수배[i] * g.TEN)).ToString() + "/" + //ToString("0.#");
                    //     ((int)(g.kosdaq_틱매도배[i] * g.TEN)).ToString() + " ";
                    tick_minute_string += ((int)(g.kosdaq_틱매수배[i])).ToString() + "/" + //ToString("0.#");
                            ((int)(g.kosdaq_틱매도배[i])).ToString() + " ";
                }
                tick_minute_string += "\n";
            }
            else if (stock.Contains("코스닥혼합") && g.dl[0].Contains("인버스"))
            {
                for (int i = 0; i < 4; i++) // draw_stock_title_tick_minute_string
                {
                    //tick_minute_string += ((int)(g.kosdaq_틱매도배[i] * g.TEN)).ToString() + "/" + //ToString("0.#");
                    //     ((int)(g.kosdaq_틱매수배[i] * g.TEN)).ToString() + " ";
                    tick_minute_string += ((int)(g.kosdaq_틱매도배[i])).ToString() + "/" + //ToString("0.#");
                        ((int)(g.kosdaq_틱매수배[i])).ToString() + " ";
                }
                tick_minute_string += "\n";
            }
            else // 
            {
                for (int i = 0; i < 4; i++) // draw_stock_title_tick_minute_string
                {
                    tick_minute_string += ((int)(o.틱매수배[i])).ToString() + "/" + //ToString("0.#");
                            ((int)(o.틱매도배[i])).ToString() + " ";
                }
                tick_minute_string += "\n";
            }


            // x[k, 8 & 9]
            for (int i = end_time - 1; i > end_time - 5; i--)
            {
                if (i < 1)
                {
                    break;
                }
                if (i == end_time - 1)
                    tick_minute_string += x[i, 8].ToString() + "/" + //ToString("0.#");
                        x[i, 9].ToString() + " ";
                else
                    tick_minute_string += x[i, 8].ToString() + "/" + //ToString("0.#");
                        x[i, 9].ToString() + " ";
            }
            return tick_minute_string;
        }


        public static void draw_stock_daily(int seq, int npts_toplot, string 일주월, string stock)
        {

            int npts_toread = npts_toplot + 120; // need 120 ?
            double maxy = -10000000;
            double miny = 10000000;


            string path = @"C:\Work\" + 일주월 + "\\" + stock + ".txt";

            if (!File.Exists(path))
            {
                return;
            }

            int lineCount = File.ReadAllLines(path).Length;
            if (lineCount < npts_toread)
            {
                return;
            }
            List<string> lines = File.ReadLines(path).Reverse().ToList();

            // Read Data File
            double[,] x = new double[npts_toread, 10];

            int n = 0;
            foreach (string line in lines)
            {
                string[] words = line.Split(' ');

                if (g.date >= Convert.ToInt32(words[0]))
                {
                    for (int i = 0; i < 10; i++)
                    {
                        x[n, i] = Convert.ToDouble(words[i]);
                        if (i >= 1 && i <= 4)
                        {
                            if (maxy < x[n, i])
                            {
                                maxy = x[n, i];
                            }
                        }
                    }
                    n++;
                }
                if (n >= npts_toread)
                {
                    break;
                }
            }


            // Max x[,] = 100 Rearrangement of Data
            for (int i = 0; i < npts_toread; i++)
            {
                for (int j = 1; j <= 4; j++)
                {
                    x[i, j] /= (maxy / 100); // divide by max and mupltiply by 100 to make 100%
                }
            }

            maxy = -10000000;
            miny = 10000000;


            // Calculate Moving Average (5, 20, 60)
            for (int k = 0; k < npts_toplot + 60; k++) // why add 60 ? 
            {
                // 5  days moving average
                x[k, 5] = 0;
                for (int i = 0; i < 5; i++)
                {
                    x[k, 5] += x[k + i, 4];
                }

                x[k, 5] = x[k, 5] / 5.0;

                // 20 days moving average
                x[k, 6] = 0;
                for (int i = 0; i < 20; i++)
                {
                    x[k, 6] += x[k + i, 4];
                }

                x[k, 6] = x[k, 6] / 20.0;

                // 60 days moving average
                x[k, 7] = 0;
                for (int i = 0; i < 60; i++)
                {
                    x[k, 7] += x[k + i, 4];
                }

                x[k, 7] = x[k, 7] / 60.0;
            }

            // Calculate Bollinger Sigma
            for (int k = 0; k < npts_toplot; k++)
            {
                // Bollinger 1 Sigma 
                double sum = 0;
                for (int i = 0; i < 20; i++)
                {
                    sum += (x[k + i, 6] - x[k + i, 4]) * (x[k + i, 6] - x[k + i, 4]);
                }

                sum /= 20.0;
                x[k, 9] = Math.Sqrt(sum); // 1 Standard Deviation
            }

            string area = seq.ToString();
            g.chart.ChartAreas.Add(area);


            // candle stickstring id = mov.ToString();
            string sid = stock + seq.ToString() + "candle";
            Series t = new Series(sid);
            g.chart.Series.Add(t);


            // Set series chart type
            t.ChartType = SeriesChartType.Candlestick;
            //g.chart.Series["price"].ChartType = SeriesChartType.Candlestick;
            //g.chart.Series["price"].ChartType = SeriesChartType.Candlestick;

            // Set the style of the open-close marks
            t["OpenCloseStyle"] = "Triangle";
            //g.chart.Series["price"]["OpenCloseStyle"] = "Triangle";

            // Show both open and close marks
            t["ShowOpenClose"] = "Both";

            // Set point width
            t["PointWidth"] = "0.5";

            // Set colors bars
            t["PriceUpColor"] = "Red"; // <<== use text indexer for series
            t["PriceDownColor"] = "Blue"; // <<== use text indexer for series

            t.IsVisibleInLegend = false;
            t.ChartArea = area;


            for (int k = npts_toplot - 1; k >= 0; k--)
            {
                string s = "";
                if (일주월 == "일")
                {
                    s = x[k, 0].ToString();
                    s = s.Substring(s.Length - 4); // 천보 데이터 부족시 Error Occured, Because S.Lenght - 4 < 0 : Debug Later
                }

                // adding date and high
                t.Points.AddXY(s, x[k, 2]);
                // adding low
                t.Points[npts_toplot - k - 1].YValues[1] = x[k, 3];
                //adding open
                t.Points[npts_toplot - k - 1].YValues[2] = x[k, 1];
                // adding close
                t.Points[npts_toplot - k - 1].YValues[3] = x[k, 4];
            }

            // Moving Average Draw
            for (int mov = 0; mov < 3; mov++) //
            {
                string mid = "moving" + seq.ToString() + mov.ToString();
                Series m = new Series(mid); // <<== make sure to name the series "price"
                g.chart.Series.Add(m);

                // Set series chart type
                m.ChartType = SeriesChartType.Line;

                for (int k = npts_toplot - 1; k >= 0; k--)
                {
                    string s;
                    if (일주월 == "일")
                    {
                        s = x[k, 0].ToString();
                        s = s.Substring(s.Length - 4);
                    }
                    g.chart.Series[mid].Points.AddXY(x[k, 0].ToString(), x[k, mov + 5]);

                    if (x[k, mov + 5] > maxy)
                    {
                        maxy = x[k, mov + 5];
                    }

                    if (x[k, mov + 5] < miny)
                    {
                        miny = x[k, mov + 5];
                    }
                }

                m.IsVisibleInLegend = false;
                m.ChartArea = area;
                m.BorderWidth = 1;

                if (mov == 0)
                {
                    m.Color = Color.Red;
                }

                if (mov == 1)
                {
                    m.Color = Color.Green;
                }

                if (mov == 2)
                {
                    m.Color = Color.Blue;
                }
            }

            // Bollinger Band Draw

            for (int bol = 0; bol < 2; bol++)
            {
                if (일주월 != "일")
                {
                    break;
                }

                string bid = "bollinger" + seq.ToString() + bol.ToString();
                Series b = new Series(bid); // <<== make sure to name the series "price"
                g.chart.Series.Add(b);

                // Set series chart type
                b.ChartType = SeriesChartType.Line;

                for (int k = npts_toplot - 1; k >= 0; k--)
                {
                    string s;
                    if (일주월 == "일")
                    {
                        s = x[k, 0].ToString();
                        s = s.Substring(s.Length - 4);
                    }

                    if (bol == 0) // lower bound curve
                    {
                        b.Points.AddXY(x[k, 0].ToString(), x[k, 6] - x[k, 9] * 2);
                        if (x[k, 6] - x[k, 9] * 2 < miny)
                        {
                            miny = x[k, 6] - x[k, 9] * 2;
                        }
                    }
                    else    // upper boudn curve
                    {
                        b.Points.AddXY(x[k, 0].ToString(), x[k, 6] + x[k, 9] * 2);
                        if (x[k, 6] + x[k, 9] * 2 > maxy)
                        {
                            maxy = x[k, 6] + x[k, 9] * 2;
                        }
                    }
                }
                b.IsVisibleInLegend = false;
                b.ChartArea = area;
                b.BorderWidth = 2;
                b.Color = Color.Black;
            }

            float[] location = new float[2];
            float[] size = new float[2];
            draw_size_location(seq, location, size);

            g.chart.Size = Screen.PrimaryScreen.WorkingArea.Size;
            g.chart.ChartAreas[area].AxisX.MajorGrid.Enabled = false;
            g.chart.ChartAreas[area].AxisY.MajorGrid.Enabled = false;

            g.chart.ChartAreas[area].AxisX.Interval = npts_toplot - 1;
            g.chart.ChartAreas[area].AxisX.IntervalOffset = 1;
            g.chart.ChartAreas[area].AxisY.Interval = npts_toplot + 1000;
            g.chart.ChartAreas[area].AxisY.IntervalOffset = -1;

            g.chart.ChartAreas[area].AxisY.Maximum = maxy * 1.05;
            g.chart.ChartAreas[area].AxisY.Minimum = miny * 0.95;

            g.chart.ChartAreas[area].Position.X = location[0];
            g.chart.ChartAreas[area].Position.Y = location[1];
            g.chart.ChartAreas[area].Position.Width = size[0]; // width
            g.chart.ChartAreas[area].Position.Height = size[1]; // height



            /*

            if (g.q != "h&s")
            {
                g.chart.Series.Add(label_title);
                g.chart.Legends.Add(new Legend(label_title));
                g.chart.Legends[label_title].Position.X = location[0];
                g.chart.Legends[label_title].Position.Y = location[1];
                g.chart.Legends[label_title].Position.Width = 12.0F;
                g.chart.Legends[label_title].Position.Height = 5.0F;// Was 2.5 -> only 2 lines appears
                g.chart.Series[label_title].Legend = label_title;
                g.chart.Series[label_title].BorderWidth = 1;
                g.chart.Series[label_title].IsVisibleInLegend = true;
                g.chart.Series[label_title].MarkerBorderWidth = 0;
            }
            */

            // Legend

            int index = wk.return_index_of_ogldata(stock);
            if (index < 0)
            {
                return;
            }

            string label_title = draw_foreign_institute_price_daily_stock_title(stock, index, x, 0);
            label_title = stock;

            g.chart.Series.Add(label_title); // empty series assigned for legend
            g.chart.Legends.Add(new Legend(label_title));
            g.chart.Legends[label_title].Position.X = location[0];
            g.chart.Legends[label_title].Position.Y = location[1];
            g.chart.Legends[label_title].Position.Width = 12.0F;
            g.chart.Legends[label_title].Position.Height = 1.5F; // was 1.5
            g.chart.Series[label_title].Legend = label_title;
            g.chart.Series[label_title].BorderWidth = 1;
            g.chart.Series[label_title].IsVisibleInLegend = true;
            g.chart.Series[label_title].MarkerBorderWidth = 0;
        }


        public static void draw_multiple(int seq, List<string> 종목들)
        {
            if (종목들.Count == 0 || 종목들 ==  null)
                return;

            List<string> stocklist = new List<string>();   // changing single list

            for (int i = 0; i < 종목들.Count; i++)
            {
                stocklist.Add(종목들[i]);
                if (stocklist.Count == 4)
                    break;
            }

            float[] size = new float[2];
            float[] location = new float[2];

            draw_size_location(seq, location, size);
            string sid = "";
            int[,] x = new int[383, 5];

            string area = seq.ToString();
            g.chart.ChartAreas.Add(area);

            int max_time = 0;
            int[] npts = new int[4];

            if (stocklist.Count >= 2)
            {
                for (int i = 0; i < stocklist.Count; i++)
                {
                    g.stock o = null;

                    int index = wk.return_index_of_ogldata(stocklist[i]);

                    if (index < 0) // 혼합과 ogl_data 등록된 종목만 draw
                    {
                        return;
                    }
                    else
                    {
                        o = g.ogl_data[index];
                        npts[i] = o.nrow;
                        if (max_time < npts[i])
                            max_time = npts[i];
                    }
                }
            }

            for (int i = 0; i < stocklist.Count; i++)
            {
                g.stock o = null;

                int index = wk.return_index_of_ogldata(stocklist[i]);

                if (index < 0) // 혼합과 ogl_data 등록된 종목만 draw
                {
                    return;
                }
                else
                {
                    o = g.ogl_data[index];
                }
                for (int j = 0; j < o.nrow; j++)
                {
                    if (i == 0)
                    {
                        x[j, 0] = o.x[j, 0]; // time copy once, if i = 0
                    }
                    x[j, i + 1] = o.x[j, 1]; // price of each stock only copy
                }
            }

            for (int i = 0; i < stocklist.Count; i++)
            {
                sid = stocklist[i] + area + " " + i.ToString();

                Series t = new Series(sid);
                g.chart.Series.Add(t);

                int npoint = 0;
                for (int k = g.time[0]; k < g.time[1]; k++)
                {
                    if (x[k, 0] <= 0)
                    {
                        break;
                    }

                    if (x[0, 0] < 90000)
                    {
                        for (int m = 2; m <= 3; m++) // 수급, 강도 너무 큰 값 가질 수 있음
                        {
                            x[0, m] = x[1, m];
                        }
                    }
                    t.Points.AddXY(((int)(x[k, 0] / g.HUNDREAD)).ToString(), x[k, i + 1]);
                    npoint++;
                }
                if (npoint < 1) // in case the file is empty 
                {
                    continue;
                }

                g.stock o = new g.stock();
                int index = wk.return_index_of_ogldata(stocklist[i]);
                if (index >= 0)
                    o = g.ogl_data[index];
                else
                    o = null;
                if (o.분당거래액_천만원 < 1)
                {
                }
                else if (o.분당거래액_천만원 < 5)
                {
                    t.Points[0].MarkerColor = Color.Red;
                    t.Points[0].MarkerSize = 10;
                    //t.Points[0].MarkerStyle = MarkerStyle.Circle;
                }
                else if (o.분당거래액_천만원 < 10)
                {
                    t.Points[0].MarkerColor = Color.Red;
                    t.Points[0].MarkerSize = 15;
                    //t.Points[0].MarkerStyle = MarkerStyle.Circle;
                }
                else if (o.분당거래액_천만원 < 20)
                {
                    t.Points[0].MarkerColor = Color.Green;
                    t.Points[0].MarkerSize = 15;
                    //t.Points[0].MarkerStyle = MarkerStyle.Circle;
                }
                else if (o.분당거래액_천만원 < 30)
                {
                    t.Points[0].MarkerColor = Color.Green;
                    t.Points[0].MarkerSize = 20;
                    //t.Points[0].MarkerStyle = MarkerStyle.Circle;
                }
                else if (o.분당거래액_천만원 < 50)
                {
                    t.Points[0].MarkerColor = Color.Blue;
                    t.Points[0].MarkerSize = 20;
                    //t.Points[0].MarkerStyle = MarkerStyle.Circle;
                }
                else if (o.분당거래액_천만원 < 100)
                {
                    t.Points[0].MarkerColor = Color.Blue;
                    t.Points[0].MarkerSize = 30;
                    //t.Points[0].MarkerStyle = MarkerStyle.Circle;
                }
                else if (o.분당거래액_천만원 < 150)
                {
                    t.Points[0].MarkerColor = Color.Blue;
                    t.Points[0].MarkerSize = 40;
                    //t.Points[0].MarkerStyle = MarkerStyle.Circle;
                }
                else if (o.분당거래액_천만원 < 200)
                {
                    t.Points[0].MarkerColor = Color.Blue;
                    t.Points[0].MarkerSize = 50;
                    //t.Points[0].MarkerStyle = MarkerStyle.Circle;
                }
                else
                {
                    t.Points[0].MarkerColor = Color.Blue;
                    t.Points[0].MarkerSize = 60;
                    //t.Points[0].MarkerStyle = MarkerStyle.Circle;
                }

                if (g.time[1] > 1)
                {
                    int price_change = o.x[g.time[1] - 1, 1] - o.x[g.time[1] - 2, 1];
                    if (price_change >= 0)
                        t.Points[0].MarkerStyle = MarkerStyle.Circle;
                    else
                        t.Points[0].MarkerStyle = MarkerStyle.Cross;
                }
                else
                    t.Points[0].MarkerStyle = MarkerStyle.Circle;


                g.chart.ChartAreas[area].AxisX.MajorGrid.Enabled = false;
                g.chart.ChartAreas[area].AxisX.Interval = npoint - 1;
                g.chart.ChartAreas[area].AxisX.IntervalOffset = 1;
                g.chart.ChartAreas[area].AxisX.LabelStyle.Enabled = true;

                t.Points[npoint - 1].Label = "    " + stocklist[i];

                //t.Points[npoint - 1].Label = "    " + (i + 1).ToString();

                t.BorderWidth = 2;

                // ChartArea, ChartType, BorderWidth
                t.ChartArea = area;
                t.ChartType = SeriesChartType.Line;
                t.XValueType = ChartValueType.Date;

                // Line Color 
                switch (i)
                {
                    case 0:
                        t.Color = Color.Red;
                        break;
                    case 1:
                        t.Color = Color.Green;
                        break;
                    case 2:
                        t.Color = Color.Blue;
                        break;
                    case 3:
                        t.Color = Color.Purple;
                        break;
                    case 4:
                        t.Color = Color.Magenta;
                        break;
                    default:
                        break;
                }
                t.IsVisibleInLegend = false;
            }

            // Area Setting
            g.chart.ChartAreas[seq.ToString()].Position.X = location[0];
            g.chart.ChartAreas[seq.ToString()].Position.Y = location[1];
            g.chart.ChartAreas[seq.ToString()].Position.Width = size[0];
            g.chart.ChartAreas[seq.ToString()].Position.Height = size[1];
        }


        public static void draw_foreign_institute_price(int seq, int npts_toplot, string stock)
        {
            //stock = "삼성전자";

            string path = @"C:\Work\일\\" + stock + ".txt";
            if (!File.Exists(path))
            {
                return;
            }

            List<string> lines = File.ReadLines(path).Reverse().Take(npts_toplot).ToList();

            // read data file
            double[,] x = new double[npts_toplot, 5];
            int n = 0;
            foreach (string line in lines)
            {
                string[] words = line.Split(' ');
                x[n, 0] = Convert.ToDouble(words[0]); // 일자
                x[n, 1] = Convert.ToDouble(words[4]); // 종가
                x[n, 2] = Convert.ToDouble(words[8]); // 외인
                if (stock == "삼성전자")
                {
                    x[n, 2] *= 1;
                }

                x[n, 3] = Convert.ToDouble(words[9]); // 기관
                x[n, 4] = (x[n, 2] + x[n, 3]) * -1.0; // 개인

                n++;
            }

            if (n < npts_toplot)
            {
                npts_toplot = n;
            }

            double pmax = 0.0;        // 종가 절대값 최대치
            double nmax = 0.0;
            for (int k = 0; k < n; k++)
            {
                for (int i = 1; i <= 4; i++)
                {

                    if (i == 1)
                    {
                        x[k, i] = (x[k, i] - x[n - 1, i]) / x[n - 1, i] * 100;
                        if (pmax < Math.Abs(x[k, i]))  // 가격 최대
                        {
                            pmax = Math.Abs(x[k, i]);
                        }
                    }
                    else
                    {
                        x[k, i] -= x[n - 1, i];
                        if (nmax < Math.Abs(x[k, i])) // 외인 기관 개인 최대수량
                        {
                            nmax = Math.Abs(x[k, i]);
                        }
                    }
                }
            }

            for (int k = 0; k < n; k++)
            {
                for (int i = 2; i <= 4; i++)
                {
                    x[k, i] /= (nmax / pmax); // Zero Divider ? 
                }
            }

            string area = seq.ToString();
            g.chart.ChartAreas.Add(area);
            // Draw Lines
            for (int i = 1; i <= 4; i++)
            {
                string sid = stock + i.ToString(); //종가, 외인, 기관, 개인 순서
                Series t = new Series(sid);
                g.chart.Series.Add(t);

                if (i == 1)
                {
                    t.Color = Color.Red;
                    t.BorderWidth = 1;
                }

                if (i == 2)
                {
                    t.Color = Color.Magenta;
                    t.BorderWidth = 2;
                }

                if (i == 3)
                {
                    t.Color = Color.Cyan;
                    t.BorderWidth = 2;
                }

                if (i == 4)
                {
                    t.Color = Color.Black;
                    t.BorderWidth = 2;
                }

                for (int k = npts_toplot - 1; k >= 0; k--)
                {
                    string s = "";
                    {
                        s = x[k, 0].ToString();
                        if (s == "0")
                        {
                            break;
                        }

                        s = s.Substring(s.Length - 4);
                    }
                    t.Points.AddXY(s, x[k, i]);
                }
                t.ChartType = SeriesChartType.Line;
                t.IsVisibleInLegend = false;


                /*
                if(i==4)
                    t.BorderWidth = 2;
                    */

                t.ChartArea = area;


            }

            float[] location = new float[2];
            float[] size = new float[2];
            draw_size_location(seq, location, size);

            g.chart.Size = Screen.PrimaryScreen.WorkingArea.Size;
            g.chart.ChartAreas[area].AxisX.MajorGrid.Enabled = false;
            g.chart.ChartAreas[area].AxisY.MajorGrid.Enabled = false;
            g.chart.ChartAreas[area].Position.X = location[0];
            g.chart.ChartAreas[area].Position.Y = location[1];
            g.chart.ChartAreas[area].Position.Width = size[0]; // width
            g.chart.ChartAreas[area].Position.Height = size[1];// height
            g.chart.ChartAreas[area].AxisX.Interval = npts_toplot - 1;
            g.chart.ChartAreas[area].AxisX.IntervalOffset = 1;


            // Legend
            int index = wk.return_index_of_ogldata(stock);
            if (index < 0)
            {
                return;
            }
            string label_title = draw_foreign_institute_price_daily_stock_title(stock, index, x, 0);
            if (index >= 0)
            {
                if (g.testing && g.ogl_data[index].selected_for_group == false)
                {
                    label_title = "$$$ " + label_title;
                }
            }


            g.chart.Series.Add(label_title); // empty series assigned for legend
            g.chart.Legends.Add(new Legend(label_title));
            g.chart.Legends[label_title].Position.X = location[0];
            g.chart.Legends[label_title].Position.Y = location[1];
            g.chart.Legends[label_title].Position.Width = 12.0F;
            g.chart.Legends[label_title].Position.Height = 1.50F;
            g.chart.Series[label_title].Legend = label_title;
            g.chart.Series[label_title].BorderWidth = 1;
            g.chart.Series[label_title].IsVisibleInLegend = true;
            g.chart.Series[label_title].MarkerBorderWidth = 0;
        }


        public static string draw_foreign_institute_price_daily_stock_title(string stock, int index, double[,] x, int end_time)
        {
            int 당외량백분율 = 0;
            int 당기량백분율 = 0;

            int 시총 = (int)g.ogl_data[index].시총; // this is double type, so converted to integer
            int 종가기준추정누적거래액_천만원 = 0;

           
            string label_title = "";
            if (g.q != "h&s")
            {
                if (g.보유종목.Contains(stock))
                {
                    label_title += "&&" + " ";
                }
                if (g.관심종목.Contains(stock))
                {
                    label_title += "**" + " ";
                }

                label_title += stock + " " + 종가기준추정누적거래액_천만원 + " " + 시총 + " " + 당외량백분율.ToString() + " " + 당기량백분율.ToString();
                if (g.보유종목.Contains(stock))
                {
                    index = wk.return_index_of_ogldata(stock);

                    if (index >= 0)
                    {
                        label_title += " " + g.ogl_data[index].보유량.ToString() + "(" +
                g.ogl_data[index].수익률.ToString("0.##") + ")";
                    }
                }
            }
            return label_title;
        }
    }
}
