using New_Tradegy.Library.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace New_Tradegy.Library.Listeners
{
    internal class PreMarketEyeBatchDownloader
    {
        private static readonly TimeSpan Period = TimeSpan.FromMilliseconds(1400);
        private static readonly TimeSpan MinPause = TimeSpan.FromMilliseconds(50);
        private static readonly SemaphoreSlim _once = new SemaphoreSlim(1, 1);

        private static CPSYSDIBLib.MarketEye _marketeye;
        private static readonly CPUTILLib.CpStockCode _cpstockcode = new CPUTILLib.CpStockCode();

        private static int _offset = 0;

        public static async Task RunDownloaderLoop(CancellationToken ct)
        {
            if (!g.connected) return;

            while (!ct.IsCancellationRequested)
            {
                var started = Stopwatch.StartNew();

                try
                {
                    await _once.WaitAsync(ct);
                    try
                    {
                        var now = DateTime.Now;
                        int HHmmss = int.Parse(now.ToString("HHmmss"));

                        // 08:50:00 ~ 08:59:59 만 동작
                        if (HHmmss >= 85000 && HHmmss < 90000)
                        {
                            int remainRq = Form1.GetRemainRQ();

                            if (remainRq >= 3)
                            {
                                await Task.Run((Action)DownloadBatch, ct)
                                          .ConfigureAwait(false);
                            }
                        }
                    }
                    finally
                    {
                        _once.Release();
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    // 장전 정찰 실패는 루프 중단하지 않음
                }

                var remain = Period - started.Elapsed;
                if (remain < MinPause) remain = MinPause;

                try
                {
                    await Task.Delay(remain, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private static void DownloadBatch()
        {
            int remain = Form1.GetRemainRQ();
            if (remain <= 2)
                return;

            if (_marketeye == null)
            {
                _marketeye = new CPSYSDIBLib.MarketEye();
                _marketeye.Received +=
                    new CPSYSDIBLib._ISysDibEvents_ReceivedEventHandler(HandleBatchData);
            }

            object[] fields = new object[]
            {
        0,   // 종목코드
        1,   // 시간 hhmm
        2,   // 대비부호
        3,   // 전일대비
        4,   // 현재가

        8,   // 매도호가
        9,   // 매수호가
        13,  // 총매도호가잔량
        14,  // 총매수호가잔량
        15,  // 최우선매도호가잔량
        16,  // 최우선매수호가잔량
        17,  // 종목명

        28,  // 예상체결가
        29,  // 예상체결가대비
        30,  // 예상체결가대비부호
        31   // 예상체결수량
            };

            var selected = BuildPreOpenSelected200();

            if (selected.Count == 0)
                return;

            string[] codes = selected
                .Select(sd => sd.Code)
                .Where(code => !string.IsNullOrEmpty(code))
                .Take(200)
                .ToArray();

            if (codes.Length == 0)
                return;

            _marketeye.SetInputValue(0, fields);
            _marketeye.SetInputValue(1, codes);

            int result = _marketeye.BlockRequest();

            if (result != 0)
            {
                // 필요하면 나중에 로그
                return;
            }
        }




       

        private static List<StockData> BuildPreOpenSelected200()
        {
            var all = new List<StockData>();

            // --------------------------------------------------
            // 1. 지수 4개 먼저
            // --------------------------------------------------
            all.AddRange(
                g.StockRepo.Indices()
                .Take(4)
            );

            // --------------------------------------------------
            // 2. 일반 종목 전체
            // --------------------------------------------------
            foreach (string stock in g.StockRepo.AllGeneralStockNames)
            {
                var data = g.StockRepo.TryGetDataOrNull(stock);

                if (data != null)
                    all.Add(data);
            }

            // --------------------------------------------------
            // 3. 유효성 체크
            // --------------------------------------------------
            all = all
                .Where(x => x != null)
                .Where(x => !string.IsNullOrEmpty(x.Code))
                .GroupBy(x => x.Code)
                .Select(g => g.First())
                .ToList();

            if (all.Count == 0)
                return new List<StockData>();

            // --------------------------------------------------
            // 4. 200개 순환 선택
            // --------------------------------------------------
            if (_offset >= all.Count)
                _offset = 0;

            var selected = new List<StockData>();

            int count = Math.Min(200, all.Count);

            for (int i = 0; i < count; i++)
            {
                int idx = (_offset + i) % all.Count;
                selected.Add(all[idx]);
            }

            _offset = (_offset + 200) % all.Count;

            return selected;
        }

        private static void HandleBatchData()
        {
            DateTime now = DateTime.Now;
            int HHmmss = Convert.ToInt32(now.ToString("HHmmss"));

            // 장전 08:50:00 ~ 08:59:59 만 처리
            if (HHmmss < 85000 || HHmmss >= 90000)
                return;

            int count = (int)_marketeye.GetHeaderValue(2);
            if (count <= 0)
                return;

            lock (g.lockObject)
            {
                for (int k = 0; k < count; k++)
                {
                    string code = _marketeye.GetDataValue(0, k);
                    string stock = _cpstockcode.CodeToName(code);

                    if (!g.StockRepo.Contains(stock))
                        continue;

                    var data = g.StockRepo.TryGetDataOrNull(stock);
                    if (data == null)
                        continue;

                    int currentPrice = SafeInt(_marketeye.GetDataValue(4, k));
                    int askPrice1 = SafeInt(_marketeye.GetDataValue(5, k));   // fields 배열 기준 주의
                    int bidPrice1 = SafeInt(_marketeye.GetDataValue(6, k));
                    long askQty1 = SafeLong(_marketeye.GetDataValue(9, k));
                    long bidQty1 = SafeLong(_marketeye.GetDataValue(10, k));

                    int expectedPrice = SafeInt(_marketeye.GetDataValue(12, k));
                    long expectedVol = SafeLong(_marketeye.GetDataValue(15, k));

                    if (expectedPrice <= 0)
                        continue;

                    var po = data.PreOpen;

                    // 가격/예상체결량 변화 없으면 저장 생략
                    var last = po.Records.Count > 0
                        ? po.Records[po.Records.Count - 1]
                        : null;

                    if (last != null &&
                        last.ExpectedPrice == expectedPrice &&
                        last.ExpectedVolume == expectedVol)
                    {
                        continue;
                    }

                    po.Records.Add(new PreOpenRecord
                    {
                        HHmmss = HHmmss,

                        CurrentPrice = currentPrice,

                        ExpectedPrice = expectedPrice,
                        ExpectedVolume = expectedVol,

                        AskPrice1 = askPrice1,
                        AskQty1 = askQty1,

                        BidPrice1 = bidPrice1,
                        BidQty1 = bidQty1
                    });
                }
            }
        }


        private static int SafeInt(object value)
        {
            if (value == null) return 0;

            try
            {
                return Convert.ToInt32(value);
            }
            catch
            {
                return 0;
            }
        }

        private static long SafeLong(object value)
        {
            if (value == null) return 0;

            try
            {
                return Convert.ToInt64(value);
            }
            catch
            {
                return 0;
            }
        }



    }
}
