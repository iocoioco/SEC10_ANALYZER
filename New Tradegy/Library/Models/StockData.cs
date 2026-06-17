using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static New_Tradegy.Library.g;

namespace New_Tradegy.Library.Models
{
    public class StockData
    {
        public PreOpenState PreOpen { get; set; } = new PreOpenState();

        public enum InstrumentKind
        {
            Stock,   // 일반 개별종목 (평가/랭킹 대상)
            Sector,  // 합성 섹터 (섹터 평가/랭킹 대상, 다운로드 X)
            Index    // KODEX/지수/인덱스 (다운로드 O, 평가 X)
        }

        public InstrumentKind Kind { get; set; } = InstrumentKind.Stock;

        public string Stock { get; set; }
        public string Code { get; set; }

        public PricePassData Pass { get; set; } = new PricePassData();
        public ScoreData Score { get; set; } = new ScoreData();
        public StatisticsData Statistics { get; set; } = new StatisticsData();
        // public LevelData Level { get; set; } = new LevelData();
        public ApiData Api { get; set; } = new ApiData();
        public PostData Post { get; set; } = new PostData();
        public DealStatus Deal { get; set; } = new DealStatus();
        public MiscData Misc { get; set; } = new MiscData();

    }


    // public PricePassData Pass { get; set; } = new PricePassData();


    public class PreOpenRecord
    {
        public int HHmmss { get; set; }
        public int CurrentPrice { get; set; }

        public int ExpectedPrice { get; set; }
        public long ExpectedVolume { get; set; }

        public int AskPrice1 { get; set; }
        public long AskQty1 { get; set; }

        public int BidPrice1 { get; set; }
        public long BidQty1 { get; set; }
    }

    public class PreOpenState
    {
        public List<PreOpenRecord> Records { get; } = new List<PreOpenRecord>();

        public bool IsCandidate { get; set; }
        public double Score { get; set; }

        public void Clear()
        {
            Records.Clear();
            IsCandidate = false;
            Score = 0;
        }
    }


    public class PricePassData
    {
        public int upperPassingPrice = 0;
        public int lowerPassingPrice = 0;

        public int PreviousPriceHigh = int.MinValue;
        public int? PreviousPriceLow = null;
        public int PriceStatus = 0;

        public int PreviousProgramHigh = int.MinValue;
        public int? PreviousProgramLow = null;
        public int ProgramStatus = 0;

        public int MonthStatus, QuarterStatus, HalfStatus, YearStatus;
        public int Month, Quarter, Half, Year;


    }







    public enum ScoreKey
    {
        // ✅ 누적 (dayProgress 적용된 누적 천만원)
        CumMoney_10M,   // 누적거래액(천만원)
        CumPro_10M,     // 누적프로그램(천만원)
        CumFor_10M,     // 누적외인(천만원)
        CumInt_10M,

        // ✅ 분30 (천만원/정수)
        MinMoney_10M,   // 분30거래천
        MinPro_10M,     // 분30프로천
        MinFor_10M,     // 분30외인천

        // ✅ 분30 강도(정수 스케일 유지: 네가 이미 쓰는 값 그대로)
        MinDiff,        // 분30배수차
        MinSum,         // 분30배수합
    }

    public class ScoreData
    {
        public readonly Dictionary<ScoreKey, double> Raw = new Dictionary<ScoreKey, double>();
        public readonly Dictionary<ScoreKey, double> PctMax = new Dictionary<ScoreKey, double>();
        public readonly Dictionary<ScoreKey, int> Rank = new Dictionary<ScoreKey, int>();

        public void Clear()
        {
            Raw.Clear();
            PctMax.Clear();
            Rank.Clear();
        }

        public double GetRaw(ScoreKey k) => Raw.TryGetValue(k, out var v) ? v : 0;
        public double GetPct(ScoreKey k) => PctMax.TryGetValue(k, out var v) ? v : 0;
        public int GetRank(ScoreKey k) => Rank.TryGetValue(k, out var v) ? v : 0;

        public int SectorRank;
    }







    // public StatData Stat { get; set; } = new StatData();
    public class StatisticsData
    {
        public int 푀분_count;

        public double 푀분_avr, 푀분_dev;
        public double 거분_avr, 거분_dev;
        public double 배차_avr, 배차_dev;
        public double 배합_avr, 배합_dev;
        public double 종누_avr, 종누_dev;
        public double 푀누_avr, 푀누_dev;
        public double 기누_avr, 기누_dev;

        public double 푀분_top5;
        public double 거분_top5;

        public double 매도1호가잔량_avr, 매도1호가잔량_dev;
        public double 매수1호가잔량_avr, 매수1호가잔량_dev;
        public double 총매도호가잔량_avr, 총매도호가잔량_dev;
        public double 총매수호가잔량_avr, 총매수호가잔량_dev;


        public char 시장구분; // P 코스피, D 코스닥
        public double 시총;

        public string 일간변동평균편차;
        public double 일간변동평균;
        public double 일간변동편차;

        public int 일최대거래액;
        public int 일최저거래액;
        public int AvgDailyTurnover_10M;
        public ulong 일평균거래량;


    }


    // public ApiData Api { get; set; } = new ApiData();
    public class ApiData
    {
        public long 현재가; //4 long
        public long 시초가; //5 long  
        public int 시초;
        public long 전고가; //6 long

        public long 전저가; //7 long
        public int 전저;

        public long 매수1호가;
        public long 매도1호가; // 8 long public long 매수1호가; // 9 long

        public ulong 거래량; // 10 ulong

        public double 전일거래액_천만원; // marketeye not provide, calculated from "일"

        public char 장구분; // 12 char '0' 장전 '1' 동시호가 '2' 장중
        public int 총매도호가잔량; //13 ulong
        public int 총매수호가잔량; //14 ulong
        public int 매도1호가잔량; //15 (ulong) converted to int
        public int 매수1호가잔량; //16 (ulong) converted to int

        public long 전일종가; //23 long

        public long 예상체결가; //28 long // not down
        public ulong 예상체결수량; //31 ulong // not down

        public char 시간외단일대비부호; //36 char +, - // not down
        public long 시간외단일전일대비; //37 long, 36 필히 하여야 함 // not down
        public long 시간외단일현재가; //38 long // not down
        public ulong 시간외단일거래대금; //45 ulonglong // not down

        public long 당일프로그램순매수량; // 116 long
        public long 당일외인순매수량; //118 long

        public long 당일기관순매수량; //120 long

        public ulong 공매도수량; //127 ulong



        public int 가격;
        public int 수급;
        public double 체강;

        public int nrow = 0;
        public int[,] x = new int[382, 12];

        // 틱 데이터
        public int[] 틱의시간 = new int[MajorIndex.TickArraySize]; // 틱의시간    // 호가창 tT
        public int[] 틱의가격 = new int[MajorIndex.TickArraySize]; // 틱의가격    // 호가창
        public int[] 틱의수급 = new int[MajorIndex.TickArraySize]; // 틱의수급
        public int[] 틱의체강 = new int[MajorIndex.TickArraySize]; // 틱의체강

        public int[] 틱수누량 = new int[MajorIndex.TickArraySize]; // 틱수누량
        public int[] 틱도누량 = new int[MajorIndex.TickArraySize]; // 틱도누량
        public int[] 틱매수배 = new int[MajorIndex.TickArraySize]; // 틱매수배
        public int[] 틱매도배 = new int[MajorIndex.TickArraySize]; // 틱매도배

        public int[] 틱배수차 = new int[MajorIndex.TickArraySize];  // 틱배수차
        public int[] 틱배수합 = new int[MajorIndex.TickArraySize];  // 틱배수합
        public int[] 틱푀퍼 = new int[MajorIndex.TickArraySize];  // 틱프외퍼

        public int[] 틱프누량 = new int[MajorIndex.TickArraySize]; // 틱프누량  
        public double[] 틱프로천 = new double[MajorIndex.TickArraySize]; // 틱프돈천

        public int[] 틱외누량 = new int[MajorIndex.TickArraySize]; // 틱외누량
        public double[] 틱외인천 = new double[MajorIndex.TickArraySize]; // 틱외돈천 
        public double[] 틱거래천 = new double[MajorIndex.TickArraySize]; // 틱거돈천


        public int[] 틱최우선매도호잔량 = new int[MajorIndex.TickArraySize]; // 매도1호가잔량
        public int[] 틱최우선매수호잔량 = new int[MajorIndex.TickArraySize]; // 매수1호가잔량

        public int[] 틱총매도호가잔량 = new int[MajorIndex.TickArraySize]; // 매도1호가잔량
        public int[] 틱총매수호가잔량 = new int[MajorIndex.TickArraySize]; // 매수1호가잔량


        public int[] 틱기관천 = new int[MajorIndex.TickArraySize];  // 틱프외퍼
        public int[] 틱개인천 = new int[MajorIndex.TickArraySize];  // 틱프외퍼
        public double[] 틱나스닥 = new double[MajorIndex.TickArraySize];  // 틱프외퍼
        public double[] 틱K200 = new double[MajorIndex.TickArraySize];  // 틱프외퍼



        // 분 데이터
        public double[] 분의시간 = new double[MajorIndex.MinuteArraySize]; // 분의시간
        public double[] 분프로천 = new double[MajorIndex.MinuteArraySize]; // 분프로천
        public double[] 분외인천 = new double[MajorIndex.MinuteArraySize]; // 분외인천
        public double[] 분기관천 = new double[MajorIndex.MinuteArraySize]; // 분프로천

        public double[] 분개인천 = new double[MajorIndex.MinuteArraySize]; // 분외인천
        public double[] 분나스닥 = new double[MajorIndex.MinuteArraySize]; // 분외인천
        public double[] 분K200 = new double[MajorIndex.MinuteArraySize]; // 분외인천
        public double[] 분거래천 = new double[MajorIndex.MinuteArraySize]; // 분거래천

        public int[] 분매수배 = new int[MajorIndex.MinuteArraySize]; // 분매수배
        public int[] 분매도배 = new int[MajorIndex.MinuteArraySize]; // 분매도배
        public int[] 분배수차 = new int[MajorIndex.MinuteArraySize];  // 분배수차
        public int[] 분배수합 = new int[MajorIndex.MinuteArraySize];  // 분배수차



        public void AppendTick(
            string stock,
            int[] t,
            int HHmmssfff,
            double 현누적매수체결거래량,
            double 현누적매도체결거래량,
            double 틱매수체결배수,
            double 틱매도체결배수,
            double moneyFactor)   // multipleFactor 제거
        {
            // ----------------------------
            // 1. shift (0-based 유지)
            // ----------------------------
            for (int i = MajorIndex.TickArraySize - 1; i >= 1; i--)
            {
                틱의시간[i] = 틱의시간[i - 1];
                틱의가격[i] = 틱의가격[i - 1];
                틱의수급[i] = 틱의수급[i - 1];
                틱의체강[i] = 틱의체강[i - 1];

                틱수누량[i] = 틱수누량[i - 1];
                틱도누량[i] = 틱도누량[i - 1];

                틱매수배[i] = 틱매수배[i - 1];
                틱매도배[i] = 틱매도배[i - 1];
                틱배수차[i] = 틱배수차[i - 1];
                틱배수합[i] = 틱배수합[i - 1];

                틱프누량[i] = 틱프누량[i - 1];
                틱프로천[i] = 틱프로천[i - 1];
                틱외누량[i] = 틱외누량[i - 1];
                틱외인천[i] = 틱외인천[i - 1];
                틱거래천[i] = 틱거래천[i - 1];
                틱푀퍼[i] = 틱푀퍼[i - 1];

                틱최우선매도호잔량[i] = 틱최우선매도호잔량[i - 1];
                틱최우선매수호잔량[i] = 틱최우선매수호잔량[i - 1];
                틱총매도호가잔량[i] = 틱총매도호가잔량[i - 1];
                틱총매수호가잔량[i] = 틱총매수호가잔량[i - 1];

                if (stock.Contains("KODEX"))
                {
                    틱기관천[i] = 틱기관천[i - 1];
                    틱개인천[i] = 틱개인천[i - 1];
                    틱나스닥[i] = 틱나스닥[i - 1];
                    틱K200[i] = 틱K200[i - 1];
                }
            }

            // ----------------------------
            // 2. [0] 세팅
            // ----------------------------

            틱의시간[0] = HHmmssfff;
            틱의가격[0] = 가격;
            틱의수급[0] = 수급;

            틱의체강[0] = (int)(체강 * g.MILLION);

            // 누적 체결량 (음수 방어)
            int curBuy = (int)Math.Max(0, 현누적매수체결거래량);
            int curSell = (int)Math.Max(0, 현누적매도체결거래량);

            틱수누량[0] = curBuy;
            틱도누량[0] = curSell;

            // ----------------------------
            // 3. 틱 매수배 / 매도배
            // ----------------------------
            틱매수배[0] = (int)Math.Round(틱매수체결배수);
            틱매도배[0] = (int)Math.Round(틱매도체결배수);

            틱배수차[0] = 틱매수배[0] - 틱매도배[0];
            틱배수합[0] = 틱매수배[0] + 틱매도배[0];

            // ----------------------------
            // 4. 프로그램/외인 틱 간 차이 (단위 일관성 유지)
            // t[4], t[5]는 누적 수량 기준이라고 가정
            // ----------------------------

            틱프누량[0] = t[4];
            틱외누량[0] = t[5];

            int dPro = 틱프누량[0] - 틱프누량[1];
            int dFor = 틱외누량[0] - 틱외누량[1];

            if (dPro < 0) dPro = 0;
            if (dFor < 0) dFor = 0;

            틱프로천[0] = (int)Math.Round(dPro * moneyFactor);
            틱외인천[0] = (int)Math.Round(dFor * moneyFactor);

            // ----------------------------
            // 5. 틱 거래천 (체결 누적 차이 기반)
            // ----------------------------

            int dBuy = 틱수누량[0] - 틱수누량[1];
            int dSell = 틱도누량[0] - 틱도누량[1];

            if (dBuy < 0) dBuy = 0;
            if (dSell < 0) dSell = 0;

            틱거래천[0] = (int)Math.Round((dBuy + dSell) * moneyFactor);

            if (틱거래천[0] > 0)
            {
                틱푀퍼[0] =
                    (int)Math.Round(
                        (double)(틱프로천[0] + 틱외인천[0]) /
                        틱거래천[0] * 100.0);
            }
            else
            {
                틱푀퍼[0] = 0;
            }

            // ----------------------------
            // 6. 호가 잔량
            // ----------------------------

            틱최우선매도호잔량[0] = 매도1호가잔량;
            틱최우선매수호잔량[0] = 매수1호가잔량;
            틱총매도호가잔량[0] = 총매도호가잔량;
            틱총매수호가잔량[0] = 총매수호가잔량;

            // ----------------------------
            // 7. KODEX 전용
            // ----------------------------

            if (stock == "KODEX 레버리지")
            {
                틱기관천[0] = MajorIndex.Instance.KospiInstitutionNetBuy;
                틱개인천[0] = MajorIndex.Instance.KospiRetailNetBuy;
                틱나스닥[0] = MajorIndex.Instance.NasdaqIndex;
                틱K200[0] = 1.0;
            }
            else if (stock == "KODEX 코스닥150레버리지")
            {
                틱기관천[0] = MajorIndex.Instance.KosdaqInstitutionNetBuy;
                틱개인천[0] = MajorIndex.Instance.KosdaqRetailNetBuy;
                틱나스닥[0] = MajorIndex.Instance.NasdaqIndex;
                틱K200[0] = 1.0;
            }
        }

        public void UpdateIndexMinuteSeries(string stock, bool append)
        {
            // 지수 종목만
            if (!stock.Contains("KODEX"))
                return;

            // 1) append이면 [0] 자리 만들기 위해 전체 shift
            if (append)
            {
                for (int i = MajorIndex.MinuteArraySize - 1; i >= 1; i--)
                {
                    분기관천[i] = 분기관천[i - 1];
                    분개인천[i] = 분개인천[i - 1];
                    분나스닥[i] = 분나스닥[i - 1];
                    분K200[i] = 분K200[i - 1];
                }
            }

            // 2) 항상 [0]은 현재값으로 덮어쓰기
            if (stock == "KODEX 레버리지")
            {
                분기관천[0] = MajorIndex.Instance.KospiInstitutionNetBuy;
                분개인천[0] = MajorIndex.Instance.KospiRetailNetBuy;
                분나스닥[0] = MajorIndex.Instance.NasdaqIndex;
                분K200[0] = 1.0;
                return;
            }

            if (stock == "KODEX 코스닥150레버리지")
            {
                분기관천[0] = MajorIndex.Instance.KosdaqInstitutionNetBuy;
                분개인천[0] = MajorIndex.Instance.KosdaqRetailNetBuy;
                분나스닥[0] = MajorIndex.Instance.NasdaqIndex;
                분K200[0] = 1.0;
                return;
            }
        }
    }




    // public DealStatus Deal { get; set; } = new DealStatus();
    public class DealStatus
    {
        public int 보유량;
        public double 수익률;
        public double 손절률;
        public double 전수익률;
        public double 장부가;

        public long 평가금액; // Error if changed to int
        public long 손익단가; // Error if changed to int


    }


    // public PostData Post { get; set; } = new PostData();
    public class PostData
    {
        // 20260510 eruption rank
        public int 분10거래천Rank;
        public int 분10프로천Rank;

        public int 매수호가거래액_백만원;  // ok
        public int 매도호가거래액_백만원;  // ok
        public int 분가격차;             // ok
        public int 분기관차천;
        public int 분개인차천;
        public double 분나스닥;
        public int 분총매수매도잔량평균;

        public double 프퍼;
        public double 푀퍼;

        public double 종누천; // 종가 기준 추정거래액
        public double 프누천;
        public double 외누천;
        public double 기누천;
        public double 개누천;
        public double 기관효;
        public double 개인효;


        // 10 Sec
        public double 분10프로천;
        public double 분10외인천;
        public double 분10기관천;
        public double 분10개인천;
        public double 분10나스닥;
        public double 분10K200;
        public double 분10거래천;

        public double 분10매수배;
        public double 분10매도배;
        public double 분10배수차;
        public double 분10배수합;
        public double 분10가격차;
        public double 분10프퍼;
        public double 분10푀퍼;
        public double Sec10Top1BookAvgValue;

        // 20 Sec
        public double 분20프로천;
        public double 분20외인천;
        public double 분20기관천;
        public double 분20개인천;
        public double 분20나스닥;
        public double 분20K200;
        public double 분20거래천;

        public double 분20매수배;
        public double 분20매도배;
        public double 분20배수차;
        public double 분20배수합;
        public double 분20가격차;
        public double 분20프퍼;
        public double 분20푀퍼;
        public double Sec20Top1BookAvgValue;

        // 30 Sec
        public double 분30프로천;
        public double 분30외인천;
        public double 분30기관천; // etf에서 사용
        public double 분30개인천; // etf에서 사용
        public double 분30나스닥; // etf에서 사용
        public double 분30K200;  // etf에서 사용
        public double 분30거래천;

        public double 분30매수배;
        public double 분30매도배;
        public double 분30배수차;
        public double 분30배수합;
        public double 분30의가격;
        public double 분30가격차;
        public double 분30프퍼;
        public double 분30푀퍼;
        public double Sec30Top1BookAvgValue;
    }


    // public MiscData Misc { get; set; } = new MiscData();
    public class MiscData
    {
        public double 수급과장배수 = 1;
        public double 가격과장배수 = 1;
        public int oGL_sequence_id;
        public bool ShrinkDraw = false;
        public bool CreateNewChartArea = false;


        public int y_min = int.MaxValue;
        public double y_max = int.MinValue;

        public Dictionary<string, double> Corr { get; private set; }
        = new Dictionary<string, double>(StringComparer.Ordinal);

        public FlowSnapshot Flow;   // null 가능, 필요시 생성
        public class FlowSnapshot
        {
            public int EndIndex;              // end-1
            public int TimeKey;               // api.x[end-1,0] 같은 시간키(예: HHMM)
            public double PctProgram;         // 4
            public double PctForeign;         // 5
            public double PctInstitute;       // 6
            public double PctTotal;           // 7 (원하면)
        }

    }

}