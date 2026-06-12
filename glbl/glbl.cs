using System.Collections.Generic;
using System.Windows.Forms.DataVisualization.Charting;


namespace glbl
{
    public class g
    {
        public static string Account = "782261082"; //  "782236683"; // 

        public static object lockObject = new object(); // BLOCK
        
        public static int MAX_ROW = 382; // XX 382 -> 500
        public static int saved_nrow;
       

        // list of variables``
        public static int revolving_number_for_kospi = 0; // used in misc : revoling_naver(int kospi_or_kosdaq) - not used
        public static int revolving_number_for_kosdaq = 0; // used in misc : revoling_naver(int kospi_or_kosdaq) - not used

        //public static double 일중거래액환산율;
        public static double 천만원 = 10000000.0;
        public static double 억원 = 100000000.0;
        public static Chart chart;

        public static char device = 'S'; // S Samsung, L Lg, C B2, c small notebook
        public static char current_key_char = 'd';
        //public static char previous_KeyChar;
        public static char saved_key_char_qQ;


        public static string clicked_Stock;
        public static string clicked_title;
        public static string previous_clicked_title;


        public static string q;
        public static string saved_q;
        public static string saved_check_q;
        //public static string previous_working_q;



        public static int[,] saved_x = new int[MAX_ROW, 12];

        
        //public static List<string> ogl = new List<string>();   // total set of single stock list
        public static List<g.stock> ogl_data = new List<g.stock>();
        public static List<string> sl = new List<string>();   // selected single stock list from eval_stock
        public static List<string> dl = new List<string>();   // selected stocks for display

        

        public static List<List<string>> oGL = new List<List<string>>(); // large group set list
        //public static List<string> oGL_title = new List<string>();
        public class group_data
        {
            public int ranking;
            public int number_of_stocks_passed_inclusion; // inclusion_check 통과 종목수
            public List<string> stocks_passed_inclusion = new List<string>();
            //public string 대표종목;
            public List<string> stocks = new List<string>();
            public double value;
            public double average_price;
            public double difference_price;
            public string title;
        }
        public static List<g.group_data> oGL_data = new List<group_data>();
        public static List<List<string>> DL = new List<List<string>>(); // temporary working space for group list
        

    

        public static List<string> 보유종목 = new List<string>();   // 매수된 종목
        public static List<string> 관심종목 = new List<string>();   // 클릭된 종목, Toggle로 선택 & 취소
        public static List<string> 주요종목 = new List<string>();   // 클릭된 종목, Toggle로 선택 & 취소
        public static List<string> 체결진행 = new List<string>();   // 매수된 종목
        public static List<string> 평불종목 = new List<string>();   // 매수된 종목

        public static List<string> 지수보유관심종목 = new List<string>();
        public static List<string> 지수종목 = new List<string>();
        public static List<string> 코스피합성 = new List<string>();   // 매수된 종목
        public static List<string> 코스닥합성 = new List<string>();   // 클릭된 종목, Toggle로 선택 & 취소

        public static List<List<string>> aV = new List<List<string>>(); // 평균거래량
        

        public static bool 시초 = true; // at the begining of market, try all stocks first and then use the order of evaluation
        public static bool 그룹대비배수 = true;
        public static bool 돌파적용 = false;
        public static bool testing;
        public static bool draw_history_forwards = false;

        public static bool timer_첵_first = true;
        //public static bool draw_kodex_inverse = false;
        public static bool click_trade = false;
        public static bool confirm_buy = true;
        public static bool add_interest = false;
        public static bool confirm_sell = false;
        public static bool draw_stock_shrink_or_not = false;
        public static bool show_small_amount = true;
        //public static bool eval_stock = true;


 


        public static int array_size = 12; //
        public static int stocks_per_marketeye = 200;
        // public static int picture_number = 0;
        // public static int purchase_amount_reset_count = 0;
        public static int setting_text_count = 0;
        

        public static int kospi_value = -3000;
        public static int kosdq_value = -3000;
        //public static int[] kospi_ceiling_floor = new int[2];
        //public static int[] kosdaq_ceiling_floor = new int[2];
        

        //public static int BILLION = 100000000;

        public static int ogl_data_next = 0;
        public static int deposit = 0;

        public static int 코스피개인순매수 = 0;
        public static int 코스피외인순매수 = 0;
        public static int 코스피기관순매수 = 0;
        public static int 코스피금투순매수 = 0;
        public static int 코스피연기순매수 = 0;

        public static int 코스닥개인순매수 = 0;
        public static int 코스닥외인순매수 = 0;
        public static int 코스닥기관순매수 = 0;
        public static int 코스닥금투순매수 = 0;
        public static int 코스닥연기순매수 = 0;

        public static float 코스피지수 = 0;
        public static float 코스닥지수 = 0;
        public static float 상해종합지수 = 0;
        public static float 항생지수 = 0;
        public static float 니케이지수 = 0;
        public static float 대만가권 = 0;
        public static float SP_지수 = 0;
        public static float Nasdaq_지수 = 0;
        public static float usd_krw = 0;

        public static int window_x_size;
        public static int window_y_size;

        public static int rqwey_nCol = 6;
        public static int rqwey_nRow = 3;

        public static int eval_per_marketeyes = 5;

        public static int marketeye_count = 0;
        public static int previous_marketeye_count = 0;
        public static int minute_at_save_all = 0;

        //public static int processing = 0;
        //public static int stocksinCode = 110; // used in divide_그룹 : not used function
        public static int date;
        
        public static int moving_reference_date = 0;
        public static int saved_date = 0;
        public static int saved_hs_date = 0;
        public static int[] date_list_for_history = new int[100];
        public static int[] time = new int[2];
        //public static int saved_time = 0;
        public static int[] saved_time = new int[2];
        public static int[] saved_check_time = new int[2];
        public static string saved_stock;
        public static int[,] eval_score = new int[10, 12];


        public static int nCol;
        public static int nRow;

        public static int gid;
        public static int Gid;
        public static int Group_ranking_Gid;
        public static int saved_Gid;
        public static int draw_selection = 1;
        public static int npts_fi_dwm = 40;
        public static int draw_shrink_time = 30;
        public static int npts_for_magenta_cyan_mark = 4;

        //public static int money_shift = 2;


        public static double EPS = 0.0000001;
        public static double HUNDREAD = 100.0;
        public static double THOUSAND = 1000.0;
        public static double TEN = 10.0;
        //public static double 일중거래액환산율;

        public static double[] 누적 = new double[60 * 7];

        public static double[] kospi_틱매수배 = new double[array_size]; // 
        public static double[] kospi_틱매도배 = new double[array_size]; // 
        public static double[] kosdaq_틱매수배 = new double[array_size]; // 
        public static double[] kosdaq_틱매도배 = new double[array_size]; // 

        public static double[] screenFactor = new double[2];


        public static string previous_clicked_stock;
        public static double previous_col_percentage;



        public class variable
        {
            public char 코스피코스닥 = 's'; // p : 코스피, d : 코스닥, s : 코스피 및 코스닥
            //public int working_stocks = 0;
            //public int total_stocks = 0;

            
            public double 분당거래액이상 = 30;
            public double 편차이상 = 3;
            public int 종가기준추정거래액이상_천만원 = 0;
          
            public double 시총이상 = 3;
            public int minimum_bidding = 10;
            public int 틱간프로그램매수이상 = 100;

            public double 수급과장배수 = 5; // 수급 과장하여 표시하는 배수

            public double 배수과장배수 = 1.0; // 수급 과장하여 표시하는 배수, p & P key control the value

            public string [] files_to_open_by_clicking_edge = new string [8];   // selected stocks for display

            public int q_advance_lines = 20;
            public int Q_advance_lines = 200;
            public int r3_display_lines = 35;

            public int[] kospi_difference_for_sound = new int[3];
            public int[] kosdq_difference_for_sound = new int[3];

            public double 틱프로돈이상 = 10;
            public double 분프로돈퍼센티지이상 = 50;
        }
        public static variable v = new variable();

        public class score
        {
            public List<List<double>> dev = new List<List<double>>();
            public List<List<double>> mkc = new List<List<double>>();

            public List<List<double>> 돌파 = new List<List<double>>();
            public List<List<double>> 눌림 = new List<List<double>>();

            public List<List<double>> 가연 = new List<List<double>>();
            public List<List<double>> 가분 = new List<List<double>>();
            public List<List<double>> 가틱 = new List<List<double>>();
            public List<List<double>> 가반 = new List<List<double>>();
            public List<List<double>> 가지 = new List<List<double>>();

            public List<List<double>> 수연 = new List<List<double>>();
            public List<List<double>> 수지 = new List<List<double>>();
            public List<List<double>> 수위 = new List<List<double>>();

            public List<List<double>> 강연 = new List<List<double>>();
            public List<List<double>> 강지 = new List<List<double>>();
            public List<List<double>> 강위 = new List<List<double>>();

            public List<List<double>> 프분 = new List<List<double>>();
            public List<List<double>> 프틱 = new List<List<double>>();
            public List<List<double>> 프지 = new List<List<double>>();
            public List<List<double>> 프퍼 = new List<List<double>>();
            public List<List<double>> 프일 = new List<List<double>>();
            public List<List<double>> 프가 = new List<List<double>>();

            public List<List<double>> 거분 = new List<List<double>>();
            public List<List<double>> 거틱 = new List<List<double>>();
            public List<List<double>> 거일 = new List<List<double>>();

            public List<List<double>> 배차 = new List<List<double>>();
            public List<List<double>> 배반 = new List<List<double>>();

            public List<List<double>> 표편 = new List<List<double>>();

            public List<List<double>> 급락 = new List<List<double>>();
            public List<List<double>> 잔잔 = new List<List<double>>();

            public List<List<double>> 그룹 = new List<List<double>>();
        }
        public static score s = new score();

        public class kodex_magnifier_shifter
        {
            public int[,] shifter = new int[4, 3];
            // price, money, US
            public double[,] magnifier = new double[4, 3];
            // price, money, U
            public double[,] max_min = new double[4, 2];
            // i = 0 KODEX 레버리지, i = 1 KODEX 200선물인버스2X
            // i = 2 KODEX 코스닥150레버리지, i = 3 KODEX 코스닥150레버리지

            public double previous_row_percentage = 0.0;
            public string previous_click_time = "00:00:00";
            public string current_click_time;
            public int index_magnifier_shifter;
        }
        public static kodex_magnifier_shifter k = new kodex_magnifier_shifter();


        public class stock
        {
            public class score
            {
                public double 돌파, 눌림;
                public double 가연, 가분, 가틱, 가반, 가지;
                public double 수연, 수지, 수위;
                public double 강연, 강지, 강위;
                public double 프분, 프틱, 프지, 프퍼, 프일, 프가;
                public double 거분, 거틱, 거일;
                public double 배차,배반;
                public double 표편;
                public double 급락, 잔잔;

                public double 그룹;

                public double 총점;
            } public score 점수 = new score();

            public class level
            {
                public double 돌파, 눌림, 가반, 가지, 강지, 배반, 프퍼, 프지, 프가, 급락, 잔잔;
            }
            public level 정도 = new level();

            public bool marketeye_started = false;
            
            public int from_time, to_time;

            public bool inclusion_passed = false;

            public string 종목;

            public string code; //0
                                //1 시간ulong hhmm
            public char 전일대비부호; // 2 char
            public long 전일대비; // 3 long
            public long 현재가; //4 long
            public long 시초가; //5 long  
            public int 시초;
            public long 전고가; //6 long
            public int 전고;
            public long 전저가; //7 long
            public int 전저;
           
            public long 매도1호가; // 8 long
            public long 매수1호가; // 9 long

            public ulong 거래량; // 10 ulong
            public ulong 거래액_원; // 11 ulong
            public ulong 전일거래액_천만원; // marketeye not provide, calculated from "일"
            
            public char 장구분; // 12 char '0' 장전 '1' 동시호가 '2' 장중
            public ulong 총매도호가잔량; //13 ulong
            public ulong 총매수호가잔량; //14 ulong
            public ulong 매도1호가잔량; //15 (ulong)
            public ulong 매수1호가잔량; //16 (ulong)
            public ulong 전일거래량; //22 ulong
            public long  전일종가; //23 long
            public double 체결강도; //24 float

            public long 예상체결가; //28 long
            public ulong 예상체결수량; //31 ulong

            public char 시간외단일대비부호; //36 char +, -
            public long 시간외단일전일대비; //37 long, 36 필히 하여야 함
            public long 시간외단일현재가; //38 long
            public ulong 시간외단일거래대금; //45 ulonglong

            public double 수급과장배수 = 1;
            public long 당일프로그램순매수량; // 116 long
            public long 당일외인순매수량; //118 long
           
            public long 당일기관순매수량; //120 long
            //public long 전일외국인순매수; //121 long

            //public long 전일기관순매수; //122 long
            public ulong 공매도수량; //127 ulong

             
            public int 종가기준추정거래액_천만원;
            public int 프로그램누적거래액_천만원; // 
            public int 외인누적거래액_천만원; // 
            public int 기관누적거래액_천만원; // 
            public double 분당거래액_천만원;
            public double 분당프로그램매수액_천만원;
            public int 배수차;
            public int 배수합;
            public int 매수체결배수;
            public int 매도체결배수;
            public int 매수호가거래액_천만원;
            public int 매도호가거래액_천만원;

            public int StopLoss = 0;

            public double 시총;

            public int 시간;
            public int 가격;
            
            public int 수급;
            //public doubl종가기준추정누적거래액_천만원;

            public double 체강;

            public int 보유량;
            public double 수익률;
            public int 매수가;
            public long 평가금액; // Error, if use int instead of long
            public long 손익단가; // Error, if use int instead of long


            public string dev_avr;
            public double avr;
            public double dev;

            public int 일최대거래액_억원; // in testing, these are diplayed in textBox 10, 11,, 12 etc
            public int 일최소거래액_억원;
            public int 일평균거래액_억원;
            public ulong 일평균거래량;

            public char 시장구분; // p 코스피, d 코스닥, s 코스피 및 

            public int nrow = 0;
            public int[,] x = new int[MAX_ROW, 12];

            public int[] 틱의시간 = new int[array_size]; // 틱의시간
            public int[] 틱의가격 = new int[array_size]; // 틱의가격
            public int[] 틱의수급 = new int[array_size]; // 틱의수급
            public int[] 틱의체강 = new int[array_size]; // 틱의체강
            public int[] 틱매수량 = new int[array_size]; // 틱매수량
            public int[] 틱매도량 = new int[array_size]; // 틱매도량
            public int[] 틱매수배 = new int[array_size]; // 틱매수배
            public int[] 틱매도배 = new int[array_size]; // 틱매도배

            public int[] 틱프로량 = new int[array_size]; // 틱프로량
            public int[] 틱프로돈 = new int[array_size]; // 틱프로돈

            public int[] 틱거래돈 = new int[array_size]; // 틱거래돈


            public int[] 분프로돈 = new int[array_size]; // 분프로돈
            public int[] 분거래돈 = new int[array_size]; // 분거래돈
            public int[] 분매수배 = new int[array_size]; // 분매수배
            public int[] 분매도배 = new int[array_size]; // 분매도배
            public int[] 분배수차 = new int[array_size];  // 분배수차
            public int[] 분배수합 = new int[array_size];  // 분배수차

            public int 전고시간;
            public int 전저시간;
            public int 돌파정도;
            public int 돌파시간;

            public bool selected_for_group = true; // used, but the value is fixed in glbl

            public int oGL_sequence_id; // assigned in the program, but not used 
        }
    }
}

