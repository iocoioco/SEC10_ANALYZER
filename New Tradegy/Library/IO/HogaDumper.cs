using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace New_Tradegy.Library.IO
{
    using New_Tradegy.Library.Models;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    public static class HogaDumper
    {
        private static CancellationTokenSource _cts;

        public static Task RunAsync(CancellationToken token)
        {
            return Task.Run(async () =>
            {
                DateTime lastLoggedMinute = DateTime.MinValue;

                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        var now = DateTime.Now;

                        // 장중 시간만 (09:00 ~ 15:19)
                        bool isMarket =
                            now.Hour >= 9 &&
                            (now.Hour < 15 || (now.Hour == 15 && now.Minute <= 19));

                        if (isMarket)
                        {
                            // 이번 분의 기준 시각(초=0)
                            var minuteStamp = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);

                            // "분이 바뀔 때" 딱 1회 기록 (초==59 같은 타이밍 의존 제거)
                            if (minuteStamp != lastLoggedMinute)
                            {
                                lastLoggedMinute = minuteStamp;

                                // 컬렉션 변경 예외 방지: 스냅샷 만들어 순회
                                var snapshot = g.StockRepo.AllDatas?
                                    .Where(d =>
                                        d != null &&
                                        (d.Kind == StockData.InstrumentKind.Stock ||
                                         d.Kind == StockData.InstrumentKind.Index))
                                    .ToArray();
                                if (snapshot != null)
                                {
                                    foreach (var data in snapshot)
                                    {
                                        // 데이터 유효성 체크
                                        if (data == null || string.IsNullOrEmpty(data.Stock))
                                            continue;

                                        var api = data.Api;
                                        if (api == null) continue;

                                        HogaLogger.AppendOncePerMinute(
                                            data.Stock,
                                            minuteStamp,
                                            api.매도1호가잔량,
                                            api.매수1호가잔량,
                                            api.총매도호가잔량,
                                            api.총매수호가잔량
                                        );
                                    }
                                }
                            }
                        }

                        // 200ms 바쁜 루프 불필요. 1초면 충분 + cancellation 존중
                        await Task.Delay(1000, token);
                    }
                }
                catch (OperationCanceledException)
                {
                    // 정상 취소
                }
                catch (Exception ex)
                {
                    // Task가 "조용히 죽는" 문제 방지: 디버그에 남김
                    System.Diagnostics.Debug.WriteLine("HogaDumper crashed: " + ex);
                }
            }, token);
        }

    }

    public static class HogaLogger
    {
        // private static readonly Dictionary<string, string> _lastMinuteKey = new Dictionary<string, string>();

        // 1) Dictionary 대신 ConcurrentDictionary 권장
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _lastMinuteKey
            = new System.Collections.Concurrent.ConcurrentDictionary<string, string>();

        public static void AppendOncePerMinute(
            string stock,
            DateTime minuteStamp,
            int bestAsk,
            int bestBid,
            int totalAsk,
            int totalBid)
        {
            if (string.IsNullOrWhiteSpace(stock))
                return;

            // minuteStamp는 이미 초=0으로 들어오게 쓰는 게 좋음
            string minuteKey = minuteStamp.ToString("yyyyMMddHHmm");

            // 2) 중복 방지: 원자적으로 처리
            //    - 기존 값이 같은 분이면 return
            //    - 다르면 갱신
            if (_lastMinuteKey.TryGetValue(stock, out var last) && last == minuteKey)
                return;

            _lastMinuteKey.AddOrUpdate(stock, minuteKey, (_, __) => minuteKey);

            // 3) 날짜별 디렉토리 (CreateDirectory는 이미 존재해도 안전)
            string baseDir = @"C:\BJS\Z Data\호가변동자료";
            string dayDir = Path.Combine(baseDir, minuteStamp.ToString("yyyyMMdd"));
            Directory.CreateDirectory(dayDir);

            // 4) 파일명 안전화 (종목이 코드가 아닐 가능성 대비)
            foreach (var c in Path.GetInvalidFileNameChars())
                stock = stock.Replace(c, '_');

            string path = Path.Combine(dayDir, stock + ".txt");

            try
            {
                // 5) 헤더 중복 방지(여러 스레드 대비): 파일 길이 0이면 헤더
                using (var fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read, 64 * 1024))
                {
                    bool writeHeader = fs.Length == 0;

                    fs.Seek(0, SeekOrigin.End);
                    using (var sw = new StreamWriter(fs))
                    {
                        if (writeHeader)
                            sw.WriteLine("HHmm bestAsk bestBid totalAsk totalBid");

                        sw.WriteLine($"{minuteStamp:HHmm} {bestAsk} {bestBid} {totalAsk} {totalBid}");
                    }
                }
            }
            catch (Exception ex)
            {
                // 파일/권한/락/경로 문제 등 추적용
                System.Diagnostics.Debug.WriteLine("HogaLogger file error: " + ex);
            }
        }

    }
}


