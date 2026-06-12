using System;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.Threading.Tasks;

namespace New_Tradegy.Library.Utils
{
    using System;
    using System.IO;

    public sealed class RqLogEntry
    {
        public DateTime Timestamp { get; set; }
        public string TrCode { get; set; }              // "7222-KOSPI", "7222-KOSDAQ", "MarketEye", ...
        public int Remain { get; set; }                 // Form1.GetRemainRQ()
        public int DelayAppliedMs { get; set; }         // 이번 루프에 실제 적용된 대기(ms)
        public bool Success { get; set; }               // BlockRequest 성공/실패(최종)
        public string Message { get; set; }             // "ok", "skip-zero-before-0910", "retry", "timeout" 등
    }

    public static class RequestLogger
    {
        public static bool Enabled = false;

        private static readonly object _lock = new object();
        private static readonly string _dir = Path.Combine(@"C:\BJS\Z Log\", "RQ_logs");

        public static void Write(RqLogEntry e)
        {
            try
            {
                if (!Directory.Exists(_dir)) Directory.CreateDirectory(_dir);
                string file = Path.Combine(_dir, "RQ_" + DateTime.Now.ToString("yyyyMMdd") + ".txt");

                bool needHeader = !File.Exists(file);
                string line = string.Format("{0},{1},{2},{3},{4},{5}",
                                            e.Timestamp.ToString("HH:mm:ss.fff"),
                                            e.TrCode,
                                            e.Remain,
                                            e.DelayAppliedMs,
                                            e.Success ? 1 : 0,
                                            (e.Message ?? "").Replace(',', ';'));

                lock (_lock)
                {
                    if (needHeader)
                    {
                        File.AppendAllLines(file, new[] { "time,tr,remain,delay_ms,success,msg" });
                    }
                    File.AppendAllLines(file, new[] { line });
                }
            }
            catch
            {
                // 로그 오류로 본업을 방해하지 않도록 조용히 무시
            }
        }
    }


    public static class RqPacing
    {
        /// <summary>
        ///  - 기본 주기: 09:05 전 2s, 이후 5s
        ///  - RemainRQ 낮으면 자동으로 +1~2s
        ///  - RemainRQ 넉넉(>=10)하면 7222 같은 보조지표는 3s까지 당김(옵션)
        ///  - 로그를 CSV로 남김
        /// </summary>

        public static async Task SmartDelayAsync(
            string trCode,
            int HHmm,
            int remainRq,
            bool lastSuccess,
            string message,
            bool allowSpeedUpTo3s,             // 7222에서만 true 권장
            CancellationToken token)
        {
            // 1) 기본 주기
            int baseInterval = (HHmm < 905) ? 2000 : 5000;

            // 2) 여유면 속도 ↑ (옵션) : RemainRQ가 넉넉하면 3초까지 당김
            if (allowSpeedUpTo3s && remainRq >= 10 && HHmm >= 905)
            {
                baseInterval = Math.Min(baseInterval, 3000);
            }

            // 3) 부족하면 속도 ↓ : RemainRQ가 낮으면 여유 더 주기
            int extra = 0;
            if (remainRq <= 3) extra = 2000;
            else if (remainRq <= 5) extra = 1000;

            int applied = baseInterval + extra;

            // 4) 로그 남기기


            if (RequestLogger.Enabled)
            {
                RequestLogger.Write(new RqLogEntry
                {
                    Timestamp = DateTime.Now,
                    TrCode = trCode,
                    Remain = remainRq,
                    DelayAppliedMs = applied,
                    Success = lastSuccess,
                    Message = message
                });
            }

            await Task.Delay(applied, token);
        }
    }

}
