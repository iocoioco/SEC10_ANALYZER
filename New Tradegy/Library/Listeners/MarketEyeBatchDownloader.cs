using New_Tradegy.Library.IO;
using New_Tradegy.Library.Models;
using New_Tradegy.Library.PostProcessing;
using New_Tradegy.Library.Trackers;
using New_Tradegy.Library.UI;
using New_Tradegy.Library.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace New_Tradegy.Library.Listeners
{
    internal class MarketEyeBatchDownloader
    {
        private static CPSYSDIBLib.MarketEye _marketeye;
        private static readonly CPUTILLib.CpStockCode _cpstockcode = new CPUTILLib.CpStockCode();
        private static readonly CPUTILLib.CpCybos _cpCybos = new CPUTILLib.CpCybos();

        private static readonly IndexRangeTracker indexRangeTracker = new IndexRangeTracker();

        // BlockRequest 진단 (느린 구간·오류만 기록)
        private static readonly object _meLogLock = new object();
        private static DateTime _meLastOkUtc = DateTime.MinValue;
        private const double MeLogSlowMs = 500;
        private const double MeLogGapMs = 60000;

        // 주기/제어
        private static readonly TimeSpan Period = TimeSpan.FromMilliseconds(700); // 목표 주기
        private static readonly TimeSpan MinPause = TimeSpan.FromMilliseconds(50); // 최소 휴지
        private static readonly SemaphoreSlim _once = new SemaphoreSlim(1, 1);    // 겹침 방지

        // 필요 시 외부에서 멈출 수 있도록 오버로드 제공
        public static Task RunDownloaderLoop()
        {
            return RunDownloaderLoop(CancellationToken.None);
        }

        // 메인 루프 (드리프트 보정형)
        public static async Task RunDownloaderLoop(CancellationToken ct)
        {
            if (!g.connected) return;

            int minuteSaveAll = -1;

            while (!ct.IsCancellationRequested)
            {
                var started = Stopwatch.StartNew();

                try
                {
                    await _once.WaitAsync(ct); // 회차 겹침 금지
                    try
                    {
                        var now = DateTime.Now;
                        int HHmm = int.Parse(now.ToString("HHmm"));
                        // int HHmmss = int.Parse(now.ToString("HHmmss")); // 필요 시 사용

                        // ── 분당 전체 저장 트리거 (한 번만) ────────────────
                        if (wk.isWorkingHour())
                        {
                            // 09:00 기준 트리거: 10:00/11:00/12:00/13:00/14:00/15:20
                            if ((HHmm == 1000 || HHmm == 1100 || HHmm == 1200 ||
                                 HHmm == 1300 || HHmm == 1400 || HHmm == 1520) &&
                                minuteSaveAll != HHmm)
                            {
                                await FileOut.SaveAllStocksAsync().ConfigureAwait(false);
                                minuteSaveAll = HHmm;
                            }
                        }

                        // ── 메인 다운로드 ─────────────────────────────────
                        if (wk.isWorkingHour())
                        {
                            SoundUtils.MarketTimeAlarmsAsync(HHmm);

                            int remainRq = Form1.GetRemainRQ();
                            if (remainRq < 3)
                            {
                                //Debug.WriteLine(string.Format("{0:HH:mm:ss.fff} ▶ throttle RemainRQ={1}", now, remainRq)); //
                                // MarketEye는 RQ(비매매) 소모 — TR(매매)과 분리
                            }
                            else
                            {
                                // 동기/COM 호출이 섞여 길어질 수 있으므로 별 Task로 격리
                                await Task.Run((Action)DownloadBatch, ct).ConfigureAwait(false);
                            }
                        }
                    }
                    finally
                    {
                        _once.Release();
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    //Debug.WriteLine(string.Format("{0:HH:mm:ss.fff} ! RunLoop error: {1}", DateTime.Now, ex.Message));
                    // 예외는 루프를 끊지 않음
                }

                // ── 드리프트 보정 대기 ────────────────────────────────
                var remain = Period - started.Elapsed;
                if (remain < MinPause) remain = MinPause;
                try { await Task.Delay(remain, ct); }
                catch (OperationCanceledException) { break; }
            }
        }

        private static void DownloadBatch()
        {
            #region
            /*
			0 code string
			1 시간ulong hhmm
			2.전일대비부호 char
			3 전일대비 long
			4 현재가 long
			5 시가 long
			6 전고 long
			7 전저long
			8 매도호가long
			9 매수호가long

			10 거래량ulong
			11 거래대금원 ulong
			12 장구분char '0' 장전 '1' 동시호가 '2' 장중
			13 총매도호가잔량ulong
			14 총매수호가잔량ulong
			15:매도1호가잔량(ulong)
			16:매수1호가잔량(ulong)
			22 전일거래량ulong
			23 전일종가long
			24 체결강도float

			28 예상체결가 long
			31 예상체결수량 ulong
			36 시간외단일대비부호char +, -
			37 시간외단일전일대비long, 36 필히 하여야 함
			38 시간외단일현재가long
			45 시간외단일거래대금ulonglong
			116 당일프로그램순매수량long
			118 당일외인순매수량long
			120 당일기관순매수량long
			121 전일외국인순매수long

			122 전일기관순매수long
			127 공매도수량ulong

            not used
            63:52주최고가(long or float)
            64:52주최저가(long or float)
			*/
            #endregion

            int remain = Form1.GetRemainRQ();
            if (remain <= 2)
            {
                return;   // 이번 다운로드는 양보
            }

            if (_marketeye == null)
            {
                _marketeye = new CPSYSDIBLib.MarketEye();
                _marketeye.Received +=
                    new CPSYSDIBLib._ISysDibEvents_ReceivedEventHandler(HandleBatchData);
            }

            object[] fields = new object[]
            {
                0, 1, 2, 3, 4, 5, 6, 7, 8, 9,
                10, 11, 12, 13, 14, 15, 16, 22, 23, 24,
                28, 31, 36, 37, 38, 45, 116, 118, 120, 121,
                122, 127
            };

            // ✅ Fill the codes using your BatchSelector
            var selected = MarketEyeBatchSelector.Select200Batch(
                indexList: g.StockRepo.Indices()
            .Select(d => d.Stock)
            .Take(4)
            .ToList(),
                holding: g.StockManager.HoldingList,
                interestedWithBid: g.StockManager.InterestedWithBidList,
                interestedOnly: g.StockManager.InterestedOnlyList,
                rankedStockList: g.StockManager.StockRankingList
            );

            string[] codes = new string[selected.Count];

            for (int i = 0; i < codes.Length && i < selected.Count; i++)
            {
                var data = g.StockManager.Repository.TryGetDataOrNull(selected[i]);
                 
                codes[i] = data.Code;
            }

            _marketeye.SetInputValue(0, fields);
            _marketeye.SetInputValue(1, codes);




            var t1 = DateTime.UtcNow;
            DateTime startLocal = DateTime.Now;

            int blockDone = 0;
            var hudCts = new CancellationTokenSource();
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(2500, hudCts.Token);

                    while (Interlocked.CompareExchange(ref blockDone, 0, 0) == 0)
                    {
                        double sec = (DateTime.UtcNow - t1).TotalSeconds;

                        if (g.MainForm != null && !g.MainForm.IsDisposed)
                        {
                            if (g.MainForm.InvokeRequired)
                            {
                                g.MainForm.BeginInvoke(new Action(() =>
                                {
                                    CenterHudForm.Show($"ME WAIT {sec:F1}s", 3000, 43f);
                                }));
                            }
                            else
                            {
                                CenterHudForm.Show($"ME WAIT {sec:F1}s", 3000, 43f);
                            }
                        }

                        await Task.Delay(1000, hudCts.Token);
                    }
                }
                catch (TaskCanceledException)
                {
                }
                catch (Exception ex)
                {
                    Trace.TraceWarning("ME HUD ERROR " + ex.Message);
                }
            });

            int result = _marketeye.BlockRequest();

            Interlocked.Exchange(ref blockDone, 1);

            // HUD 중지
            hudCts.Cancel();

            // 화면 지우기
            CenterHudForm.HideHud();

            DateTime endUtc = DateTime.UtcNow;
            DateTime endLocal = DateTime.Now;

            double elapsed = (endUtc - t1).TotalMilliseconds;



            if (elapsed >= 2500)
            {
                CenterHudForm.Show(
                    $"ME DELAY {(int)(elapsed / 1000)}s",
                    10000,
                    43f);
            }

            int status = _marketeye.GetDibStatus();
            string msg = SanitizeMeLog(_marketeye.GetDibMsg1());

            bool isSlow = elapsed >= MeLogSlowMs;      // 예: 500ms
            bool isVerySlow = elapsed >= 5000;         // 5초 이상
            bool isTimeoutLike = elapsed >= 59000;     // 60초급 지연

            double gapMs = _meLastOkUtc == DateTime.MinValue
            ? 0
            : (t1 - _meLastOkUtc).TotalMilliseconds;

            bool isGapSlow = gapMs >= MeLogGapMs;

            if (result != 0 || isSlow || isGapSlow)
            {
                LogMeBlockRequest(
                    $"result={result} status={status} msg={msg} " +
                    $"ms={elapsed:F0} rq={remain} sel={selected.Count} gap={gapMs:F0} " +
                    $"start={startLocal:HH:mm:ss.fff} end={endLocal:HH:mm:ss.fff}");
            }

            if (isVerySlow)
            {
                LogMeBlockRequest(
                    $"WARN MarketEye delayed. Watch manually. " +
                    $"ms={elapsed:F0} rq={remain} sel={selected.Count} " +
                    $"time={endLocal:HH:mm:ss.fff}");
            }

            if (isTimeoutLike)
            {
                LogMeBlockRequest(
                    $"DANGER MarketEye timeout-like delay. Manual watch only. " +
                    $"ms={elapsed:F0} rq={remain} sel={selected.Count} " +
                    $"start={startLocal:HH:mm:ss.fff} end={endLocal:HH:mm:ss.fff}");
            }

            if (result == 0)
                _meLastOkUtc = endUtc;
        }

        private static string SanitizeMeLog(object value)
        {
            if (value == null) return "";
            return value.ToString()
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .Replace(',', ';');
        }

        private static void LogMeBlockRequest(string message)
        {
            try
            {
                string dir = @"C:\BJS\Z Log\ME_logs";
                Directory.CreateDirectory(dir);
                string line = $"{DateTime.Now:HH:mm:ss.fff} ME {message}";
                lock (_meLogLock)
                {
                    File.AppendAllText(
                        Path.Combine(dir, "ME_BlockRequest_" + DateTime.Now.ToString("yyyyMMdd") + ".txt"),
                        line + Environment.NewLine);
                }
            }
            catch
            {
                // 진단 로그 실패는 무시
            }
        }

        // CONTINUED REFACTORED _marketeye_received()
        private static void HandleBatchData()
        {
            DateTime date = DateTime.Now;
            int HHmm = Convert.ToInt32(date.ToString("HHmm"));
            int HHmmss = Convert.ToInt32(date.ToString("HHmmss"));
            int HHmmssfff = Convert.ToInt32(date.ToString("HHmmssfff"));

            if (HHmmss < 90010) 
                return;

            if (!wk.isWorkingHour()) 
                return;

            int count = (int)_marketeye.GetHeaderValue(2); // number of stocks downloaded
            if (count != 200)
                return;

            var batch = new List<StockData>();
            List<StockData> localCopy;
            lock (g.lockObject)
            {
                for (int k = 0; k < count; k++)
                {
                    string code = _marketeye.GetDataValue(0, k);
                    var stock = _cpstockcode.CodeToName(code);

                    if (!g.StockRepo.Contains(stock))
                        continue;

                    var data = g.StockRepo.TryGetDataOrNull(stock);
                    if (data == null)
                        continue;
                    var api = data.Api;

                    var existing = g.StockRepo.TryGetDataOrNull(data.Stock);
                    if (existing != null)
                    {
                        batch.Add(data);
                    }

                    api.시초가 = _marketeye.GetDataValue(5, k); // 5
                    api.전고가 = _marketeye.GetDataValue(6, k);  // 6
                    api.전저가 = _marketeye.GetDataValue(7, k);  // 7 
                    api.매도1호가 = _marketeye.GetDataValue(8, k); // 8
                    api.매수1호가 = _marketeye.GetDataValue(9, k); // 9

                    api.현재가 = api.매수1호가;


                    api.거래량 = _marketeye.GetDataValue(10, k);
                    api.총매도호가잔량 = (int)_marketeye.GetDataValue(13, k);
                    api.총매수호가잔량 = (int)_marketeye.GetDataValue(14, k);
                    api.매도1호가잔량 = (int)_marketeye.GetDataValue(15, k);
                    api.매수1호가잔량 = (int)_marketeye.GetDataValue(16, k);


                    api.전일종가 = _marketeye.GetDataValue(18, k);     // 23
                    api.체강 = (double)_marketeye.GetDataValue(19, k); // 24

                    if (api.전일종가 > 100)
                    {
                        api.가격 = (int)((api.현재가 - (int)api.전일종가) * 10000.0 / api.전일종가); // % 변환 * 100
                        api.시초 = (int)((api.시초가 - (int)api.전일종가) * 10000.0 / api.전일종가); // % 변환 * 100
                        api.전저 = (int)((api.전저가 - (int)api.전일종가) * 10000.0 / api.전일종가); // % 변환 * 100
                    }
                    else
                        continue;

                    if (api.매도1호가 > 100 && api.시초가 > 100 && api.전저가 > 100)
                    {
                        double differ = (api.매도1호가 - api.매수1호가) * 10000.0 / api.전일종가;
                        double factor = 0.0;
                        if (api.매도1호가잔량 + api.매수1호가잔량 > 0)
                        {
                            factor = (double)api.매수1호가잔량 / (api.매도1호가잔량 + api.매수1호가잔량);
                        }
                        api.가격 += (int)(differ * factor);
                    }

                    if (!g.StockManager.IndexList.Contains(data.Stock))
                    {
                        api.당일프로그램순매수량 = _marketeye.GetDataValue(26, k); // 116
                        //api.당일외인순매수량 = _marketeye.GetDataValue(27, k); // 118, 지수 데이터는 계산으로 대체
                        api.당일기관순매수량 = _marketeye.GetDataValue(28, k); // 120
                    }

                    api.공매도수량 = _marketeye.GetDataValue(31, k);

                    double dayProgressRatio = StandardCurve.GetG(HHmmss);

                    if (data.Statistics.일평균거래량 > 100)
                        api.수급 = (int)((double)api.거래량 / data.Statistics.일평균거래량 * 100.0 / dayProgressRatio);

                    double 현누적매수체결거래량 = 0.0;
                    double 현누적매도체결거래량 = 0.0;
                    if (api.체강 > 0)
                    {
                        현누적매수체결거래량 = api.거래량 * (api.체강 / (100.0 + api.체강));
                        현누적매도체결거래량 = api.거래량 * 100.0 / (100.0 + api.체강);
                    }

                    if (api.nrow <= 0 || api.nrow >= g.RealMaximumRow)
                        continue;

                    int time_befr_4int = api.x[api.nrow - 1, 0] / 100;
                    bool append = HHmm != time_befr_4int || time_befr_4int == 859;
                    int comparison_row = append ? api.nrow - 1 : api.nrow - 2;

                    ///if (time_befr_4int == 859)
                       // api.x[0, 1] = api.가격;

                    int 전거래량 = Convert.ToInt32(api.x[comparison_row, 7]);
                    double 전체강 = Convert.ToInt32(api.x[comparison_row, 3]) / g.MILLION;
                    double 전누적매수체결거래량 = 0.0;
                    double 전누적매도체결거래량 = 0.0;
                    if (전체강 > 0)
                    {
                        전누적매수체결거래량 = 전거래량 * (전체강 / (100.0 + 전체강));
                        전누적매도체결거래량 = 전거래량 * 100.0 / (100.0 + 전체강);
                    }

                    double totalMilliSeconds = TimeUtils.ElapsedMillisecondsDouble(
                        api.x[comparison_row, 0] * 1000, HHmmssfff);
                    if (totalMilliSeconds <= 0)
                        continue;
                    double multiple_factor = 0.0;

                    int 매수배 = 0;
                    int 매도배 = 0;
                    if (MathUtils.IsSafeToDivide(totalMilliSeconds) && data.Statistics.일평균거래량 > 100)
                    {
                        multiple_factor = 60.0 / totalMilliSeconds * 380.0 * 1000 / data.Statistics.일평균거래량 * 10.0;
                        매수배 = (int)((현누적매수체결거래량 - 전누적매수체결거래량) * multiple_factor);
                        매도배 = (int)((현누적매도체결거래량 - 전누적매도체결거래량) * multiple_factor);
                    }

                    // ⏩ Next: build "t" array, append/replace api.x[nrow], shift tick arrays, update continuity


                    // 0, 시간, 1, 가격, 2 수급, 3 체강 * 100, 4 프로그램 매수액(억), 5 외인 매수액(억), 6 기관 매수액(억)
                    // 7 거래량, 8 매수배, 9 매도배, 10 수급연속횟수, 11 체강연속횟수



                    int[] t = new int[12];
                    t[0] = HHmmss;
                    t[1] = api.가격;
                    t[2] = api.수급;

                    // 20260215 시초 흔들리는 값이 들어와 보완
                    double ch = api.체강 * g.MILLION;
                    if (double.IsNaN(ch) || double.IsInfinity(ch) || ch < -2_000_000_000 || ch > 2_000_000_000)
                        t[3] = 0;
                    else
                        t[3] = (int)Math.Round(ch);

                    t[7] = (int)api.거래량;
                    //? 지수종목에서는 전혀 맞지않음
                    t[8] = 매수배; // 59초에 분 단위 계산 완성, api.분매수배[0]는 30초 단위 계산으로 real 사용
                    t[9] = 매도배; // 59초에 분 단위 계산 완성, api.분매도배[0]는 30초 단위 계산으로 real 사용

                    if (data.Stock.Contains("KODEX 레버리지") || data.Stock.Contains("KODEX 200선물인버스2X"))
                    {
             
                        t[3] = (int)(MajorIndex.Instance.KospiProgramNetBuy + MajorIndex.Instance.KospiForeignNetBuy);
                        t[4] = (int)MajorIndex.Instance.KospiInstitutionNetBuy;
                        t[5] = (int)MajorIndex.Instance.KospiForeignNetBuy;
                        t[6] = (int)MajorIndex.Instance.KospiRetailNetBuy;
                        for (int i = 3; i < 7; i++)
                        {
                            t[i] /= 10; // 억원으로 저장
                        }
                        t[11] = (int)MajorIndex.Instance.KospiPensionNetBuy / 10; // 억원으로 저장

                        t[10] = (int)(MajorIndex.Instance.NasdaqIndex * g.THOUSAND); // Scale NasdaqIndex to int by * 1000 for display
                        //g.kospiEngine.TryAppend(t);
                    }
                    else if (data.Stock.Contains("KODEX 코스닥150레버리지") || data.Stock.Contains("KODEX 코스닥150선물인버스"))
                    {
                        t[3] = (int)(MajorIndex.Instance.KosdaqProgramNetBuy + MajorIndex.Instance.KosdaqForeignNetBuy);
                        t[4] = (int)MajorIndex.Instance.KosdaqInstitutionNetBuy;
                        t[5] = (int)MajorIndex.Instance.KosdaqForeignNetBuy;
                        t[6] = (int)MajorIndex.Instance.KosdaqRetailNetBuy;
                        for (int i = 3; i < 7; i++)
                        {
                            t[i] /= 10; // 억원으로 저장
                        }
                        t[11] = (int)MajorIndex.Instance.KosdaqPensionNetBuy / 10; // 억원으로 저장

                        t[10] = (int)(MajorIndex.Instance.NasdaqIndex * g.THOUSAND);
                       // g.kosdaqEngine.TryAppend(t);
                    }
                    else // General
                    {
                        t[4] = (int)api.당일프로그램순매수량;
                        t[5] = (int)api.당일외인순매수량;
                        t[6] = (int)api.당일기관순매수량;
                    }

                    int append_or_replace_row = append ? api.nrow : api.nrow - 1;
                    if (append_or_replace_row >= g.RealMaximumRow) continue;

                    for (int i = 0; i < 12; i++)
                    {
                        api.x[append_or_replace_row, i] = t[i];
                    }

                    api.nrow = append_or_replace_row + 1;

                    if (!(g.StockManager.IndexList.Contains(data.Stock) && api.nrow >= 2))
                    {
                        // Continuity of amount ratio
                        if (api.x[api.nrow - 1, 7] == api.x[api.nrow - 2, 7])
                            api.x[api.nrow - 1, 10] = api.x[api.nrow - 2, 10];
                        else if (api.x[api.nrow - 1, 2] > api.x[api.nrow - 2, 2])
                            api.x[api.nrow - 1, 10] = api.x[api.nrow - 2, 10] + 1;
                        else // decrease of amount ratio
                            api.x[api.nrow - 1, 10] = 0;


                        if (api.x[api.nrow - 1, 7] == api.x[api.nrow - 2, 7])
                            api.x[api.nrow - 1, 11] = api.x[api.nrow - 2, 11];
                        else if (api.x[api.nrow - 1, 3] > api.x[api.nrow - 2, 3])
                            api.x[api.nrow - 1, 11] = api.x[api.nrow - 2, 11] + 1;
                        else
                            api.x[api.nrow - 1, 11] = 0;
                    }

                    totalMilliSeconds = Utils.TimeUtils.ElapsedMillisecondsDouble(api.틱의시간[0], HHmmssfff);
                    if (totalMilliSeconds <= 0)
                        continue;

                    double 틱매수체결배수 = 0.0;
                    double 틱매도체결배수 = 0.0;

                    if (MathUtils.IsSafeToDivide(totalMilliSeconds))
                    {
                        multiple_factor = 0.0;
                        if (data.Statistics.일평균거래량 > 100)
                            multiple_factor = 60.0 / totalMilliSeconds * 380.0 * 1000 / data.Statistics.일평균거래량 * 10.0;
                        틱매수체결배수 = (현누적매수체결거래량 - api.틱수누량[0]) * multiple_factor;
                        틱매도체결배수 = (현누적매도체결거래량 - api.틱도누량[0]) * multiple_factor;
                    }

                    /// 아래 method는 real 에서만 유효하고 사용됨
                    api.AppendTick(stock,
                        t, 
                        HHmmssfff,
                        현누적매수체결거래량,
                        현누적매도체결거래량,
                        틱매수체결배수,
                        틱매도체결배수,
                        multiple_factor
                        //api.전일종가 / g.천만원 // used as money_factor
                    );

                    // 아래 method는 real 에서만 유효하고 사용됨
                    api.UpdateIndexMinuteSeries(stock, append); // if append == false, just return 

                    
                }
            }

            // Kospi Index 종목 10 이상 상승시 소리내기
            if (g.StockRepo.Contains("KODEX 레버리지"))
            {
               
                int kospiIndex = MajorIndex.Instance.KospiIndex;
                indexRangeTracker.CheckIndexAndSound(kospiIndex, "Kospi");
                
            }

            // Kosdaq Index 종목 10 이상 상승시 소리내기
            if (g.StockRepo.Contains("KODEX 코스닥150레버리지"))
            {
                
                int kosdaqIndex = MajorIndex.Instance.KosdaqIndex;
                indexRangeTracker.CheckIndexAndSound(kosdaqIndex, "Kosdaq");
               
            }

            g.MarketeyeCount++;

            //var sw = Stopwatch.StartNew();

            PostProcessor.post_real(batch);

            //sw.Stop();
            //Trace.WriteLine($"post_real {sw.ElapsedMilliseconds} ms");

            
        }
    }
}
