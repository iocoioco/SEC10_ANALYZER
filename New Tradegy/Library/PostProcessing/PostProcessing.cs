using New_Tradegy.Library.Core;
using New_Tradegy.Library.Deals;
using New_Tradegy.Library.IO;
using New_Tradegy.Library.Models;
using New_Tradegy.Library.UI;
using New_Tradegy.Library.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;


/* 배점 : 대형주 배차 조건 충족시, 수일 과다 움직임 높게(dev 큰 종목 가능성 높음) + 배차
 *  1차 상승시 접근, 2차 이상 자제
 * 그룹 전체도 동일조건 적용
 * 
 * 순간 엄청난 거분 + 푀분
 * 누적 프돈, 거돈
 * 
 * 추가조건 : 간단접근 -> 세밀접근
 * 
 * 프돈의 손실종목
 * 
 * 프 매수 가 하락 또는 횡보 -> 가 하락 가능
 * 프 매도 가 상승 -> 프 매수 전환 가능
 * 
 * 기본 : 검둥이 급상 후 매도
 * 분거래액이 일정이상이어야 검토
 * 체강 낮은 종목 제외
 * 돌파 + 배수차 + 분거래액
 * 분거래액이 과대
 * 
 * 돌파 정의 : 강한 돌파 동시 진입 전저조건 -50
 * 
 * 매수 종목 손절 잡고 진입(횟수 줄이고 베팅액 증가)
 * 
 * 자동 매도
 * 
 * 양쪽 지수 프돈 지속 증가 또는 큰 양전
*/
namespace New_Tradegy.Library.PostProcessing
{//20260528  
    public class PostProcessor
    {
        private static bool isDrawingMainNow = false;
        private static bool drawMainRequestedWhileBusy = false;
        static int lastLoggedRow_KOSPI = -1;
        private static readonly TaskTimer _maindrawTimer = new TaskTimer("MainDraw", TimeSpan.FromMinutes(10));
        private static readonly TaskTimer _subdrawTimer = new TaskTimer("SubDraw", TimeSpan.FromMinutes(10));

        static readonly TimingMeter _tmRank = new TimingMeter("RankRuntime", cap: 600, printEvery: 20);

        private static bool isDrawingSubNow = false;
        private static bool drawSubRequestedWhileBusy = false;
        private static readonly object _mainDrawLock = new object();

        private static readonly object _subDrawLock = new object();



        private static bool _preOpenSaved = false;

        private static void SavePreOpenData()
        {
            string date = DateTime.Now.ToString("yyyyMMdd");
            string dir = Path.Combine(@"C:\BJS\분전", date);

            Directory.CreateDirectory(dir);

            foreach (var sd in g.StockRepo.Stocks())
            {
                if (sd == null || sd.PreOpen == null)
                    continue;

                if (sd.PreOpen.Records.Count == 0)
                    continue;

                string stockName = string.IsNullOrWhiteSpace(sd.Stock)
                    ? sd.Code
                    : sd.Stock;

                string fileName = MakeSafeFileName(stockName) + ".txt";
                string path = Path.Combine(dir, fileName);

                using (var sw = new StreamWriter(path, false, Encoding.UTF8))
                {
                    sw.WriteLine(
                        "HHmmss,CurrentPrice,ExpectedRate100,ExpectedPrice,ExpectedVolume,AskPrice1,AskQty1,BidPrice1,BidQty1");

                    foreach (var r in sd.PreOpen.Records)
                    {
                        sw.WriteLine(
                            $"{r.HHmmss}," +
                            $"{r.CurrentPrice}," +
                            $"{r.ExpectedRate100}," +
                            $"{r.ExpectedPrice}," +
                            $"{r.ExpectedVolume}," +
                            $"{r.AskPrice1}," +
                            $"{r.AskQty1}," +
                            $"{r.BidPrice1}," +
                            $"{r.BidQty1}");
                    }
                }
            }

            Debug.WriteLine($"PreOpen saved: {DateTime.Now:HH:mm:ss} -> {dir}");
        }

        private static string MakeSafeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "UNKNOWN";

            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');

            return name.Trim();
        }

        public static void post_test()
        {
            foreach (var data in g.StockRepo.AllDatas)
            {
                post(data); // RankRuntime & Composite is in post_minute()
            }
            //g.MainForm?.RefreshBoardSafe();
        }

        // called from MarketEyeBatchDownLoader()
        public static void post_real(List<StockData> batch)
        {
            if (batch == null || batch.Count == 0) return;

            if (!_preOpenSaved)
            {
                int hhmmss = Convert.ToInt32(DateTime.Now.ToString("HHmmss"));

                if (hhmmss >= 90500)
                {
                    SavePreOpenData();
                    _preOpenSaved = true;
                }
            }

            foreach (var data in batch)
            {
                if (data == null) continue;   // 방어
                post(data);                   // 원본 후처리 (배열 교체 금지)
            }

            NqMotionState.UpdateNqMotion();

            // 인덱스
            IndexMarket.IndexMarketSec10_20_30_Real(); // using kospi & kosdaq mixed stocks seperately

            // 보유종목 소리  처리
            // 보유종목 소리 처리
            // post_real() 안 보유종목 처리 부분
            bool needUiRefresh = false;

            foreach (var stock in g.StockManager.HoldingList.ToList())
            {
                var data1 = g.StockRepo.TryGetDataOrNull(stock);
                if (data1 == null) continue;

                marketeye_received_보유종목_푀분의매수매도_소리내기(data1);
                needUiRefresh = true;
            }

            if (needUiRefresh)
            {
                g.tradePane?.SafeBeginInvoke(() =>
                {
                    g.tradePane?.RefreshTradePane();
                });
            }

            // 1. 매 틱 최신 점수 계산
            ScoreRankEngine.Build(g.StockRepo.AllGeneralStocks, ScoreKeySets.GeneralActive);
            ScoreRankEngine.Build(g.StockRepo.AllSectorStocks, ScoreKeySets.SectorActive);

            // 2. 매 틱 impulse 판단
            g.ImpulseRunner.OnDownloadedTick();

            // 3. 무거운 작업만 phase 분산
            int phase = g.MarketeyeCount % 3;

            if (phase == 1)
            {
                g.PassedUniverse = g.StockRepo.AllGeneralStocks
                    .Where(x => RankLogic.EvalInclusion(x))
                    .ToList();

                g.controlPane.SetCellValue(1, 0,
                    g.PassedUniverse.Count + "/" + g.StockRepo.AllGeneralStocks.Count);

                g.PassedSnapshotStocks = new List<StockData>(g.PassedUniverse);

                RankLogic.RankGeneral(g.PassedSnapshotStocks);

                ManageChart1Invoke();
            }
            else if (phase == 2)
            {
                SectorPostBuilder.UpdateSectorPosts();

                int HHmmss =
                    DateTime.Now.Hour * 10000 +
                    DateTime.Now.Minute * 100 +
                    DateTime.Now.Second;

                SectorApiChartUpdater.UpdateSectorApiX(HHmmss);

                RankLogic.RankSector(g.StockRepo.AllSectorStocks);

                if (g.MarketeyeCount % 6 == 2)
                    ManageChart2Invoke();
            }
            else
            {
                // 여기는 가벼운 기타 작업만
            }

            //if (g.MarketeyeCount % 101 == 1)
            //    DealManager.DealProfit();
        }

        public static void post(StockData data)
        {
            string stock = data.Stock;

            if (!ChartLayoutUtils.TryGetDrawRange(data, out int start, out int end))
                return;

            if (!data.Stock.Contains("KODEX"))
                post_minute(data, end - 1); // last row


        }
        public static void post_minute(StockData data, int lastRow)
        {
            var api = data.Api;
            var post = data.Post;

            if (api == null || post == null) return;
            if (api.nrow < 2) return;
            if (lastRow <= 0) return;
            if (lastRow >= api.nrow) lastRow = api.nrow - 1;

            // ✅ 0 방어 (초기 로딩/이상치)
            double money_factor = Math.Max(1.0, (double)api.전일종가) / Math.Max(1.0, (double)g.천만원);
            double dayProgressRatio = StandardCurve.GetG(api.x[lastRow, 0]);
            if (dayProgressRatio <= 0) dayProgressRatio = 1.0;

            // 누적(천만원 단위)
            post.프누천 = api.x[lastRow, 4] * money_factor;
            post.외누천 = api.x[lastRow, 5] * money_factor;
            post.기누천 = api.x[lastRow, 6] * money_factor;
            post.종누천 = api.x[lastRow, 7] * money_factor / dayProgressRatio;

            if (g.test)
            {
                // 최소 2개 row 필요
                if (lastRow <= 0 || lastRow >= api.nrow)
                    return;

                for (int i = 0; i < MajorIndex.MinuteArraySize; i++)
                {
                    int baseRow = lastRow - i;
                    int prevRow = baseRow - 1;

                    if (prevRow < 0)
                        break;

                    // 분 키 (디버깅 및 일관성)
                    api.분의시간[i] = api.x[baseRow, 0] / 100;   // hhmm

                    // 누적 차이 기반 계산
                    api.분프로천[i] = (int)((api.x[baseRow, 4] - api.x[prevRow, 4]) * money_factor);
                    api.분외인천[i] = (int)((api.x[baseRow, 5] - api.x[prevRow, 5]) * money_factor);
                    api.분거래천[i] = (int)((api.x[baseRow, 7] - api.x[prevRow, 7]) * money_factor);

                    api.분매수배[i] = api.x[baseRow, 8];
                    api.분매도배[i] = api.x[baseRow, 9];
                    api.분배수차[i] = api.분매수배[i] - api.분매도배[i];
                    api.분배수합[i] = api.분매수배[i] + api.분매도배[i];
                }

                // 가장 최근 분 가격차 (선택)
                post.분30프로천 = api.분프로천[0];
                post.분30외인천 = api.분외인천[0];
                post.분30거래천 = api.분거래천[0];

                post.분30매수배 = api.분매수배[0];
                post.분30매도배 = api.분매도배[0];
                post.분30배수차 = api.분배수차[0];
                post.분30배수합 = api.분배수합[0];

                post.분30의가격 = api.틱의가격[0];
                post.분30가격차 = api.x[lastRow, 1] - api.x[lastRow - 1, 1];

                post.분30프퍼 = api.분거래천[0] > 0 ? 100.0 * api.분프로천[0] / api.분거래천[0] : 0.0;
                post.분30푀퍼 = api.분거래천[0] > 0 ? 100.0 * (api.분프로천[0] + api.분외인천[0]) / api.분거래천[0] : 0.0;

            }

            else // real
            {
                post.매도호가거래액_백만원 = (int)(api.매도1호가잔량 * money_factor * 10);
                post.매수호가거래액_백만원 = (int)(api.매수1호가잔량 * money_factor * 10);

                if (api.틱의시간[1] == 0 || data.Statistics.일평균거래량 == 0 || api.전일종가 == 0)
                    return;

                int[] intervals = { 10_000, 20_000, 30_000 };
                int[] selected = new int[intervals.Length];
                for (int i = 0; i < intervals.Length; i++)
                    selected[i] = MajorIndex.TickArraySize - 1;

                for (int i = 1; i < MajorIndex.TickArraySize; i++)
                {
                    if (api.틱의시간[i] == 0) break;
                    double elapsed = Utils.TimeUtils.ElapsedMillisecondsDouble(api.틱의시간[i], api.틱의시간[0]);
                    if (elapsed <= 0) break;
                    for (int j = 0; j < intervals.Length; j++)
                    {
                        if (selected[j] == MajorIndex.TickArraySize - 1 && elapsed > intervals[j])
                            selected[j] = i;
                    }
                }

                for (int j = 0; j < intervals.Length; j++)
                {
                    int sel = selected[j];
                    if (sel >= api.틱의시간.Length
                        || sel >= api.틱수누량.Length || sel >= api.틱도누량.Length
                        || sel >= api.틱프누량.Length || sel >= api.틱외누량.Length
                        || sel >= api.틱총매도호가잔량.Length || sel >= api.틱총매수호가잔량.Length)
                        continue;

                    double totalMilliSeconds = TimeUtils.ElapsedMillisecondsDouble(api.틱의시간[sel], api.틱의시간[0]);
                    if (totalMilliSeconds <= 0 || totalMilliSeconds > 21_600_000) continue;

                    //Debug.WriteLine($"[TickΔ] sel={sel}, tSel={api.틱의시간[sel]}, t0={api.틱의시간[0]}, Δms={totalMilliSeconds:F3}");

                    int amount = (api.틱수누량[0] - api.틱수누량[sel]) + (api.틱도누량[0] - api.틱도누량[sel]);
                    int progAmount = api.틱프누량[0] - api.틱프누량[sel];
                    int foreignAmount = api.틱외누량[0] - api.틱외누량[sel];

                    double moneyFactor = api.전일종가 / g.천만원 / totalMilliSeconds * 60_000;
                    double avgVol = (data.Statistics.일평균거래량 > 0)
                     ? (double)data.Statistics.일평균거래량
                     : double.MaxValue;
                    double multipleFactor = 60000.0 / totalMilliSeconds * 380.0 / avgVol * 10;
                    //double multipleFactor = (60_000.0 / elapsed) * (380.0 / avgVol) * 10.0;


                    int 분프로천 = (int)(progAmount * moneyFactor);
                    int 분외인천 = (int)(foreignAmount * moneyFactor);
                    int 분거래천 = (int)(amount * moneyFactor);

                    int 분매수배 = (int)((api.틱수누량[0] - api.틱수누량[sel]) * multipleFactor);
                    int 분매도배 = (int)((api.틱도누량[0] - api.틱도누량[sel]) * multipleFactor);
                    int 분배수차 = 분매수배 - 분매도배;
                    int 분배수합 = 분매수배 + 분매도배;

                    int 분가격차 = (int)(api.틱의가격[0] - api.틱의가격[sel]);

                    //                Debug.WriteLine(
                    //$"[MIN] P:{분프로천,7} F:{분외인천,7} V:{분거래천,7} " +
                    //$"B:{분매수배,6} S:{분매도배,6} Δp/min:{분가격차,7}");

                    // ✅ 1호가(Top1) 매수+매도 잔량 평균 계산
                    int top1BookAvgValue = 0;

                    if (api.틱최우선매도호잔량 != null &&
                        api.틱최우선매수호잔량 != null &&
                        sel >= 0 &&
                        sel < api.틱최우선매도호잔량.Length &&
                        sel < api.틱최우선매수호잔량.Length)
                    {
                        long sum = 0;
                        int cnt = 0;

                        for (int i = 0; i <= sel; i++)
                        {
                            if (i >= api.틱최우선매도호잔량.Length ||
                                i >= api.틱최우선매수호잔량.Length)
                                break;

                            sum += (long)api.틱최우선매도호잔량[i]
                                 + (long)api.틱최우선매수호잔량[i];

                            cnt++;
                        }

                        if (cnt > 0)
                            top1BookAvgValue = (int)(sum / cnt * api.전일종가 / 10_000_000);
                    }

                    switch (intervals[j])
                    {
                        case 10_000:
                            post.분10프로천 = 분프로천;
                            post.분10외인천 = 분외인천;
                            post.분10거래천 = 분거래천;

                            post.분10매수배 = 분매수배;
                            post.분10매도배 = 분매도배;
                            post.분10배수차 = 분배수차;
                            post.분10배수합 = 분배수합;

                            post.분10가격차 = 분가격차;

                            post.분10프퍼 = 분거래천 > 0 ? 100.0 * 분프로천 / 분거래천 : 0.0;
                            post.분10푀퍼 = 분거래천 > 0 ? 100.0 * (분프로천 + 분외인천) / 분거래천 : 0.0;

                            post.Sec10Top1BookAvgValue = top1BookAvgValue;
                            break;

                        case 20_000:
                            post.분20프로천 = 분프로천;
                            post.분20외인천 = 분외인천;
                            post.분20거래천 = 분거래천;

                            post.분20매수배 = 분매수배;
                            post.분20매도배 = 분매도배;
                            post.분20배수차 = 분배수차;
                            post.분20배수합 = 분배수합;

                            post.분20가격차 = 분가격차;

                            post.분20프퍼 = 분거래천 > 0 ? 100.0 * 분프로천 / 분거래천 : 0.0;
                            post.분20푀퍼 = 분거래천 > 0 ? 100.0 * (분프로천 + 분외인천) / 분거래천 : 0.0;

                            post.Sec20Top1BookAvgValue = top1BookAvgValue;
                            break;

                        case 30_000:
                            post.분30프로천 = 분프로천;
                            post.분30외인천 = 분외인천;
                            post.분30거래천 = 분거래천;

                            post.분30매수배 = 분매수배;
                            post.분30매도배 = 분매도배;
                            post.분30배수차 = 분배수차;
                            post.분30배수합 = 분배수합;

                            post.분30의가격 = api.틱의가격[0];
                            post.분30가격차 = 분가격차;

                            post.분30프퍼 = 분거래천 > 0 ? 100.0 * 분프로천 / 분거래천 : 0.0;
                            post.분30푀퍼 = 분거래천 > 0 ? 100.0 * (분프로천 + 분외인천) / 분거래천 : 0.0;

                            post.Sec30Top1BookAvgValue = top1BookAvgValue; // 얇은종목 스킵, 최종점수 보정, Eruption 신뢰도 판별, 얇은종목 섹터 왜곡 방지






                            AppendOrReplace(data, lastRow);
                            break;
                    }
                }
            }
        }


        public static void AppendOrReplace(StockData data, int lastRow)
        {
            if (data?.Api?.x == null || data.Post == null) return;

            var api = data.Api;
            var post = data.Post;

            if (lastRow <= 0 || lastRow >= api.nrow) return;

            int hhmmss = api.x[lastRow, 0];
            if (hhmmss == 0) return;

            int minuteKey = hhmmss / 100; // hhmm
            int curKey0 = (int)api.분의시간[0];

            // ✅ 최초 1회: "첫 가격 스냅샷"을 x[0,1]에 남김 (가격만)
            if (curKey0 == 0)
            {
                int firstPrice = api.x[lastRow, 1];
                if (firstPrice != 0)
                    api.x[0, 1] = firstPrice;

                api.분의시간[0] = minuteKey;
                curKey0 = minuteKey;
            }

            if (curKey0 != minuteKey)
            {
                double money_factor = Math.Max(1.0, (double)api.전일종가) / Math.Max(1.0, (double)g.천만원);

                for (int i = 1; i < MajorIndex.MinuteArraySize; i++)
                {
                    int baseRow = lastRow - i;
                    int prevRow = baseRow - 1;
                    if (prevRow < 0) break;

                    api.분의시간[i] = api.x[baseRow, 0] / 100;

                    api.분프로천[i] = (int)((api.x[baseRow, 4] - api.x[prevRow, 4]) * money_factor);
                    api.분외인천[i] = (int)((api.x[baseRow, 5] - api.x[prevRow, 5]) * money_factor);
                    api.분거래천[i] = (int)((api.x[baseRow, 7] - api.x[prevRow, 7]) * money_factor);

                    api.분매수배[i] = api.x[baseRow, 8];
                    api.분매도배[i] = api.x[baseRow, 9];
                    api.분배수차[i] = api.분매수배[i] - api.분매도배[i];
                    api.분배수합[i] = api.분매수배[i] + api.분매도배[i];
                }
            }

            api.분의시간[0] = minuteKey;

            api.분프로천[0] = post.분30프로천;
            api.분외인천[0] = post.분30외인천;
            api.분거래천[0] = post.분30거래천;

            api.분매수배[0] = (int)Math.Round(post.분30매수배);
            api.분매도배[0] = (int)Math.Round(post.분30매도배);
            api.분배수차[0] = api.분매수배[0] - api.분매도배[0];
            api.분배수합[0] = api.분매수배[0] + api.분매도배[0];

            post.분가격차 = (int)post.분30가격차;
        }






        public static void marketeye_received_보유종목_푀분의매수매도_소리내기(StockData data)
        {
            var api = data.Api;
            var stat = data.Statistics;

            if (data.Deal.보유량 * api.전일종가 < 200000)
                return;

            double sound_indicator = 0;

            if (Math.Abs(api.틱프로천[0]) > 0.1 || Math.Abs(api.틱외인천[0]) > 0.1)
            {
                string sound = "";
                int orderIndex = -1;

                for (int i = 0; i < 3; i++)
                {
                    if (g.StockManager.HoldingList[i] == data.Stock)
                    {
                        orderIndex = i;
                        sound = i == 0 ? "one " : i == 1 ? "two " : "three ";
                        break;
                    }
                }
                if (orderIndex < 0)
                    return;

                if (stat.푀분_dev > 0)
                {
                    sound_indicator = (api.틱프로천[0] + api.틱외인천[0]) / stat.푀분_dev;

                    if (sound_indicator > 2) sound += "buyest";
                    else if (sound_indicator > 1) sound += "buyer";
                    else if (sound_indicator > 0) sound += "buy";
                    else if (sound_indicator > -1) sound += "sell";
                    else if (sound_indicator > -2) sound += "seller";
                    else sound += "sellest";
                }
                Utils.SoundUtils.Sound("가", sound);
            }
        }


        //public static void PostPassing(StockData data, int checkRow, bool add)
        //{
        //    if (checkRow >= data.Api.x.GetLength(0))
        //    {
        //        //Console.WriteLine($"Invalid checkRow={checkRow}, max={data.Api.x.GetLength(0)}");
        //        return;
        //    }

        //    var pass = data.Pass;
        //    var api = data.Api;
        //    bool previousPriceReset = false;

        //    int price = api.x[checkRow, 1];

        //    if (price < pass.PreviousPriceHigh)
        //    {
        //        pass.PriceStatus = 0;
        //    }
        //    else if (add && price > pass.PreviousPriceHigh && pass.PreviousPriceLow.HasValue)
        //    {
        //        if (price > pass.PreviousPriceLow.Value)
        //        {
        //            pass.PreviousPriceHigh = price;
        //            previousPriceReset = true;
        //            pass.PreviousPriceLow = null;
        //            pass.PriceStatus = 1;

        //            if (!g.StockManager.InterestedOnlyList.Contains(data.Stock) &&
        //                !data.Stock.Contains("KODEX") &&
        //                api.분거래천[0] > 50 &&
        //                api.분배수차[0] > 100 &&
        //                api.분프로천[0] >= 0 &&
        //                g.add_interest)
        //            {
        //                if (g.StockManager.InterestedOnlyList.Count > 2)
        //                {
        //                    g.StockManager.InterestedOnlyList.RemoveAt(0);
        //                }
        //                g.StockManager.InterestedOnlyList.Add(data.Stock);
        //            }
        //        }
        //        else
        //        {
        //            pass.PriceStatus = 2;
        //        }
        //    }
        //    else if (price > pass.PreviousPriceHigh && !pass.PreviousPriceLow.HasValue)
        //    {
        //        pass.PreviousPriceHigh = price;
        //        pass.PriceStatus = 2;
        //    }

        //    if (pass.PreviousPriceHigh - 50 > price && !previousPriceReset)
        //    {
        //        pass.PreviousPriceLow = price;
        //    }

        //    if (pass.PreviousPriceHigh > pass.Month)
        //        pass.MonthStatus = 1;
        //    if (pass.PreviousPriceHigh > pass.Quarter)
        //        pass.QuarterStatus = 1;
        //    if (pass.PreviousPriceHigh > pass.Half)
        //        pass.HalfStatus = 1;
        //    if (pass.PreviousPriceHigh > pass.Year)
        //        pass.YearStatus = 1;
        //}


        public static void ManageChart1Invoke()
        {
            lock (_mainDrawLock)
            {
                if (isDrawingMainNow)
                {
                    drawMainRequestedWhileBusy = true;
                    return;
                }
                isDrawingMainNow = true;
            }

            Task.Run(() => DoDrawMain());
        }

        private static void DoDrawMain()
        {
            try
            {
                var form1 = Application.OpenForms["Form1"] as Form;
                if (form1 == null || form1.IsDisposed)
                    return;

                // ✅ RefreshMainChart는 UI 스레드에서
                if (form1.InvokeRequired)
                    form1.Invoke(new Action(() => g.ChartMain.RefreshMainChart()));
                else
                    g.ChartMain.RefreshMainChart();
            }
            catch
            {

            }
            finally
            {
                bool needAgain;
                lock (_mainDrawLock)
                {
                    needAgain = drawMainRequestedWhileBusy;
                    drawMainRequestedWhileBusy = false;
                    isDrawingMainNow = false;
                }

                if (needAgain)
                    ManageChart1Invoke();
            }
        }


        public static void ManageChart2Invoke()
        {
            lock (_subDrawLock)
            {
                if (isDrawingSubNow)
                {
                    drawSubRequestedWhileBusy = true;
                    return;
                }
                isDrawingSubNow = true;
            }

            Task.Run(() => DoDrawSub());
        }

        private static void DoDrawSub()
        {
            try
            {
                var subForm = Application.OpenForms["FormSub"] as FormSub;
                if (subForm == null || subForm.IsDisposed)
                    return;

                if (subForm.InvokeRequired)
                    subForm.Invoke(new Action(subForm.FormSubDraw));
                else
                    subForm.FormSubDraw();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.ToString());
            }
            finally
            {
                bool needAgain;
                lock (_subDrawLock)
                {
                    needAgain = drawSubRequestedWhileBusy;
                    drawSubRequestedWhileBusy = false;
                    isDrawingSubNow = false;
                }

                if (needAgain)
                    ManageChart2Invoke();
            }
        }

    }

    public static class ScoreKeySets
    {
        // 최소(가볍게)
        public static readonly ScoreKey[] GeneralActive =
        {
        ScoreKey.MinPro_10M,
        ScoreKey.MinMoney_10M,
        ScoreKey.MinDiff,
        // 필요하면
        // ScoreKey.MinSum,
        // ScoreKey.MinFor_10M,
    };

        public static readonly ScoreKey[] SectorActive =
        {
        ScoreKey.MinPro_10M,
        ScoreKey.MinMoney_10M,
        ScoreKey.MinDiff,
        ScoreKey.MinSum, // 섹터 composite를 순위화/표시하면
    };
    }


    /// <summary>
    /// 분당거래대금(분거래천[0]) + 1호가 유동성(post.매수1호가거래액_백만원 + post.매도1호가거래액_백만원)
    /// 두 조건이 모두 퍼센타일 하한을 통과하고, 그 상태가 3틱 연속 유지되어야 통과로 간주.
    /// 절대값이 아닌 상대 기준이므로 장세/시간대 변화에 자동 적응.
    /// </summary>
    /// 



    //개략 소요 시간 추정(ms)
    //구간 호출/루프 예상 소요(ms)   변동 요인(커짐/작아짐)
    //foreach batch -> post(data) batch.Count 회   20 ~ 250 ms post() 안에서 Clone/배열 shift/지표 계산/딕셔너리 접근/락 여부.배치 2000 + 무거운 계산이면 300ms+도 가능
    //IndexMarketSec10_20_30_Real()   1회  1 ~ 15 ms 내부가 “선정 종목만 스냅샷”이면 작고, 전체 순회/정렬/복사 많으면 커짐
    //보유종목 루프 + marketeye_received_...() holdings 회  1 ~ 30 ms 소리 재생(비동기/동기), 문자열 처리, 조건 체크.동기 사운드/IO 있으면 급증
    //g.tradePane?.Update() 조건부 1회  2 ~ 30 ms WinForms UI 업데이트 범위/Invalidate/레이아웃/그리드 리렌더. “전체 리빌드”면 50ms+ 가능
    //(3틱에1번) SectorBuilder.UpdateSectorsOnDownloadTick()	0~1회	10 ~ 200 ms 섹터 멤버 전체를 순회하며 집계하면 커짐.정렬/필터/가중치/리포 등록까지 하면 더 큼
    //(3틱에1번) HardUniverseFilter.UpdatePassedUniverseAndScores(true)	0~1회	5 ~ 120 ms 대상 종목 수(1000), 조건 계산, 점수 맵/정규화, 컬럼별 통계 계산량
    //PassedSnapshotStocks = new List<>(PassedUniverse)   0~1회	0.1 ~ 3 ms passed 개수(수십~수백). 거의 무시 가능
    //controlPane.SetCellValue(...)	0~1회	0.1 ~ 5 ms UI 쓰레드 invoke 여부/그리드 리렌더
    //RankLogic.RankGeneral(g.PassedUniverse)	0~1회	2 ~ 60 ms 정렬 O(n log n), 계산 복잡도, 부가 통계.n=500이면 수ms~수십ms
    //ManageChart1Invoke()    0~1회	5 ~ 120 ms	“Invoke만”이면 작고, 실제 차트 데이터 바인딩/축 재계산/포인트 재생성이면 매우 큼
    //ManageChart2Invoke()    0~1회	5 ~ 120 ms 위와 동일
    //g.ImpulseRunner.OnDownloadedTick()  1회	1 ~ 80 ms 전 종목 스캔/큐 처리/룰 평가/팝업 후보 계산/정렬/디바운스 정도에 따라 편차 큼






}
