
using New_Tradegy.Library.Core;
using New_Tradegy.Library.Deals;
using New_Tradegy.Library.Listeners;
using New_Tradegy.Library.Models;
using New_Tradegy.Library.PostProcessing;
using New_Tradegy.Library.Trackers;
using New_Tradegy.Library.Trackers.Charting;
using New_Tradegy.Library.UI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using New_Tradegy.Library.PostProcessing;

namespace New_Tradegy.Library
{

    public static class g
    {
        public static int DealProfit = 0;
        public static Sec10Engine Sec10Kospi;
        public static Sec10Engine Sec10Kosdaq;

        //public static string kospiSec10File;
        //public static string kosdaqSec10File;

        public static MinuteZEngine KospiMinuteZ;
        public static MinuteZEngine KosdaqMinuteZ;


        public static int ChartHeatFitMin = 20;
        public static int ChartHeatOpenMin = 20;


        public static volatile QuickTradePopup PopupCurrent;

        public static PendingBestGate PendingBestGate;

        public static New_Tradegy.Library.PostProcessing.PendingBestGate ImpulseGate;
        public static ImpulseRunner ImpulseRunner;

        public static List<StockData> PassedSnapshotStocks;
        public static volatile List<StockData> PassedUniverse = new List<StockData>();

        public static volatile Dictionary<string, int> Score_Money = new Dictionary<string, int>();
        public static volatile Dictionary<string, int> Score_Pro = new Dictionary<string, int>();
        public static volatile Dictionary<string, int> Score_For = new Dictionary<string, int>();
        public static volatile Dictionary<string, int> Score_Inst = new Dictionary<string, int>();



        // 기본값 HardUniverse 사용 변수
        public static double PassControlBasePct = 0.20;   // 분거 기준 (20%)
        public static double PassControlHogaRatio = 1.5;  // 호가 비율 (30% = 20% * 1.5)

        // 키 입력으로 조정되는 델타 (±2% 단위)
        public static double PassControlDelta = 0.0;      // 예: -0.02, +0.02

        // 최종 적용 %
        public static double PassPct_Bung = PassControlBasePct + PassControlDelta;
        public static double PassPct_Hoga = PassPct_Bung * PassControlHogaRatio;






        public static New_Tradegy.Library.Deals.TradePlanManager TradePlanManager;
        public static New_Tradegy.Library.UI.TradePlanUiHandler TradePlanUiHandler;
        public static Form1 MainForm;


        public static int 일회거래액 = 100;   // 예시. 원래 쓰던 값으로 유지






        public static DataGridView TopQuoteDgv;
        public static DataGridView BottomQuoteDgv;


        // 윈도우(ms) -> 스팬(개수) 매핑
        public static readonly Dictionary<int, int> Spans = new Dictionary<int, int>
        {
            { 10_000, 60 },
            { 20_000, 60 },
            { 30_000, 60 },
        };

        public static readonly SigmaManager Sigma =
            new SigmaManager(SigmaMode.Welford, Spans); // Rolling/Ema로 바꿔도 됨

        // 선택: 여기서 한 번만 로드되게 할 수도 있습니다.
        static g() => Sigma.LoadAtStart();



        public static List<PendingBookBid> PendingBookBids = new List<PendingBookBid>(); // add lock if needed


        public static Dictionary<string, IBookBidGenerator> BookBidInstances
    = new Dictionary<string, IBookBidGenerator>();

        public static BookBidManager BookBidManager;
        public static ChartMain ChartMain;
       // public static NotifyBox NotifyBox;
        public static bool chart1Focus = true;

        public static ChartManager ChartManager;

        public static StockManager StockManager;
        public static StockRepository StockRepo;

        //public static ICompositePickNotifier CompositeNotifier
        //  = new TraceCompositePickNotifier();

        public static GroupManager GroupManager;
        //public static Dictionary<string, StockData> Ogldata = new Dictionary<string, StockData>();

        public static ControlPane controlPane;
        /// <summary>
        /// </summary>
        public static TradePane tradePane;

        // AppState
        public static bool test; // test or real
        public static bool IsMainMode;
        public static int date; // current day for display

        // ScreenConfig



        public static int screenWidth = 1920; // window height
        public static int screenHeight = 1032; // window width
        public static int cellHeight = 29; // was 28

        // TradingSettings

        public static bool optimumTrading = false; // fire buy or sell along with book bid/ask ratio 
        public static bool add_interest = false; // add stock or not to the 관심종목 when stocks active
        public static bool confirm_buy = true;
        public static bool confirm_sell = false; // sell by click or modal display to confirm sell condition

        public static int 예치금 = 0;



        // Constants
        public static string Account; // DaiShin Account number
        public static double 천만원 = 10000000.0; // unit for division

        public static double HUNDRED = 100.0;
        public static double THOUSAND = 1000.0;
        public static double MILLION = 1000000.0;


        // InputControl
        public static bool shortform; // shortform includes less number of stocks for test purpose 


        // ChartManager
        public static Chart chart1; // main chart


        public static Chart chart2; // sub chart

        public static bool draw_history_forwards = false; // move date forwards or backwards for history analysis



        // APIControl
        public static bool connected = false; // Daishin API connected or not

        // PostControl
        public static int postIntervalTime = 30 * 1000; // seconds to be interpolated to 1 minutes
        public static double RithmicBasis;
        public static double NasdaqPercentage;
        // MiscControl
        public static char PeoridNews = 'd'; // google search 'd' past day, 'w' past month

        // FontAndThemeAndColor
        public static Color[] Colors = new Color[] // five color set for annotation and chartarea based on activity
        {
            Color.FromArgb(255, 200, 255),    // 보통 빨강
            Color.FromArgb(255, 200, 200),   // 옅은 빨강
            Color.FromArgb(200, 255, 255),   // 보통 주황
            Color.FromArgb(200, 255, 200),   // 옅은 주황
            Color.FromArgb(255, 255, 200),   // 보통 노랑
            Color.FromArgb(200, 200, 255), // 옅은 노랑
               
        };

        public static int LineWidth = 2; // curve width for default 

        public static readonly object lockObject = new object(); // Make sure it's initialized

        public static int RealMaximumRow = 382; // maximum rows of minute data for each stock from 0859 to 1520
        public static int TestMaximumRow = 0; // maximum rows of minute data for each stock from 0859 to 1520

        public static string clickedStock = ""; // target stock to be used
        public static string clickedTitle = ""; // target group title to be used

        public static string q; // mode of display (h : history etc.)

        public static string oGl_data_selection = "푀분"; // group ranking calculation ciriterion

        public class kospi_kosdaq_mixed // 
        {
            public List<string> stocks = new List<string>();
            public List<double> weights = new List<double>();
            public List<int> 전시간 = new List<int>(); // reset
            public List<int> 전누적매수체결량 = new List<int>(); // reset
            public List<int> 전누적매도체결량 = new List<int>(); // reset
        }

        public static kospi_kosdaq_mixed kospi_mixed = new kospi_kosdaq_mixed(); // stocks for kospi index 
        public static kospi_kosdaq_mixed kosdaq_mixed = new kospi_kosdaq_mixed(); // stocks for kosdaq index 

        public static int stocks_per_marketeye = 200; // max. number of stocks to be downloaded at a time thru. API 

        public static int MarketeyeCount = 0; // total count of API download from start of market
        public static int MarketeyeCountDivider = 10; // evaluation of stocks after how many MarketeyeCount increases

        public static int AlarmedHHmm = 0; // to bock alarm repeated like "Taiwan market open"

        public static int DataOffset = 0;

        public static int moving_reference_date = 0; // to check history data

        public static int[] Npts = new int[2]; // total number of point from start Npts[0] to current Npts[1] used for test

        public static int[] SavedNpts = new int[2]; // when check history data and get back to the current data 

        public static int EndNptsBeforeExtend; // shrink draw i.e to check recent 30 points data not whole data
        public static bool EndNptsExtendedOrNot; // shrink draw i.e to check recent 30 points data not whole data
        public static int NptsForShrinkDraw = 30; // how many points backwards used from the last minute

        public static int nCol = 10;  // number of chart area columns in main chart
        public static int nRow = 3; // number of chart area rows in main chart

        public static int gid; // start index of stock to display in the rank
        public static int Gid; // start index of group to display in the rank
        public static int saved_Gid; // saved Gid for return from history display
        public static int draw_selection = 1; // 1, normal, 2, days 3, Bollinder
        public static int npts_fi_dwm = 40; // draw_selection = 2, how many days to display

        public static int npts_for_magenta_cyan_mark = 4; // magenta and cyan marking on price curve repetition criterion
        public class Variable
        {
            public string MainChartDisplayMode = "푀분";

            public string SubChartDisplayMode = "피올";
            public string PreviousMainChartDisplayMode = "등헙";



            // 20260510
            public double EruptionRatio;
            public double RankLimit;

            public int 분당거래액이상_천만원; // in setting 10
            public int 호가거래액이상_백만원; // in setting 10
            public int 편차이상;  // in setting 1
            public int 배차이상; // in setting 0, not used
            public int 종가기준추정거래액이상_천만원; // insetting 0
            public int 시총이상; // in setting 0

            public int 푀플 = 1;
            public int 배플 = 1;
            public int 수급과장배수 = 10; // 수급 과장하여 표시하는 배수
            public int 배수과장배수 = 2;

            public int q_advance_lines = 15;
            public int Q_advance_lines = 150;
            public int r3_display_lines = 20;

            public float font = 11.5F;

            public DateTime SubChartManualUntil = DateTime.MinValue;
            public int SubChartAutoIndex = 0;
        }
        public static Variable v = new Variable();
        public static double[,] KodexMagnifier = new double[2, 3];





    }
}




