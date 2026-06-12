using System;
using System.Collections.Generic;
using System.IO;
using System.Media;
using System.Threading;
using System.Threading.Tasks;

namespace New_Tradegy.Library.Utils
{
    internal class SoundUtils
    {
        // ---- 캐시: key(또는 fullpath) -> SoundPlayer ----
        private static readonly Dictionary<string, SoundPlayer> _cache = new Dictionary<string, SoundPlayer>();

        // SoundPlayer는 thread-safe 보장이 약하므로 lock 필요
        private static readonly object _lock = new object();

        // ---- 사운드 파일 매핑 (key → 파일 경로) ----
        private static readonly Dictionary<string, string> _soundMap
            = new Dictionary<string, string>
            {
                { "매수", @"C:\BJS\data work\소\Deal\매수.wav" },
                { "매도", @"C:\BJS\data work\소\Deal\매도.wav" },
                { "취소", @"C:\BJS\data work\소\Deal\취소.wav" }
            };

        /// <summary>
        /// 선택된 사운드 키들을 미리 메모리에 로딩한다.
        /// </summary>
        public static void Preload(params string[] keys)
        {
            if (keys == null || keys.Length == 0) return;

            foreach (var key in keys)
            {
                if (string.IsNullOrWhiteSpace(key)) continue;
                if (!_soundMap.TryGetValue(key, out string filePath)) continue;
                if (!File.Exists(filePath)) continue;

                try
                {
                    lock (_lock)
                    {
                        if (_cache.ContainsKey(key))
                            continue;

                        var player = new SoundPlayer(filePath);
                        player.Load(); // 메모리 로드
                        _cache[key] = player;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Sound preload failed for {key}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 매핑된 key(매수/매도/취소 등) 재생. (비동기)
        /// </summary>
        public static void Play(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return;

            // UI block 방지: 항상 비동기로
            Task.Run(() =>
            {
                try
                {
                    SoundPlayer player = null;

                    lock (_lock)
                    {
                        // 캐시에 있으면 즉시 재생
                        if (_cache.TryGetValue(key, out player))
                        {
                            // 그냥 사용
                        }
                        else
                        {
                            // 캐시에 없으면 로딩 후 캐시(가능하면)
                            if (_soundMap.TryGetValue(key, out string filePath) && File.Exists(filePath))
                            {
                                var p = new SoundPlayer(filePath);
                                // Load()를 호출하면 첫 재생 지연이 줄지만, 파일이 크면 시간이 들 수 있음
                                // 여기서는 안정성 위해 try-load
                                try { p.Load(); } catch { /* ignore */ }

                                _cache[key] = p;
                                player = p;
                            }
                        }
                    }

                    player?.Play(); // 비동기 재생
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Sound play error for {key}: {ex.Message}");
                }
            });
        }

        public static void Sound_돈(int total_amount)
        {
            if (g.optimumTrading)
            {
                switch (total_amount)
                {
                    case 0: Sound("돈", "single stock opt"); break;
                    case 100: Sound("돈", "one hundred opt"); break;
                    case 500: Sound("돈", "five hundred opt"); break;
                    case 1000: Sound("돈", "one thousand opt"); break;
                    case 2000: Sound("돈", "two thousand opt"); break;
                    case 4000: Sound("돈", "four thousand opt"); break;
                    case 8000: Sound("돈", "eight thousand opt"); break;
                    case 16000: Sound("돈", "sixteen thousand opt"); break;
                    case 32000: Sound("돈", "thirty two thousand opt"); break;
                    case 64000: Sound("돈", "sixty four thousand opt"); break;
                    default: break;
                }
            }
            else
            {
                switch (total_amount)
                {
                    case 0: Sound("돈", "single stock"); break;
                    case 100: Sound("돈", "one hundred"); break;
                    case 500: Sound("돈", "five hundred"); break;
                    case 1000: Sound("돈", "one thousand"); break;
                    case 2000: Sound("돈", "two thousand"); break;
                    case 4000: Sound("돈", "four thousand"); break;
                    case 8000: Sound("돈", "eight thousand"); break;
                    case 16000: Sound("돈", "sixteen thousand"); break;
                    case 32000: Sound("돈", "thirty two thousand"); break;
                    case 64000: Sound("돈", "sixty four thousand"); break;
                    default: break;
                }
            }
        }

        public static async Task MarketTimeAlarmsAsync(int HHmm)
        {
            int[] alarm_HHmm = { 1000, 1030, 1450, 1455, 1500, 1505, 1510, 1515, 1518, 1519 };

            for (int i = 0; i < alarm_HHmm.Length; i++)
            {
                if (HHmm == alarm_HHmm[i] && HHmm != g.AlarmedHHmm)
                {
                    g.AlarmedHHmm = HHmm;

                    if (HHmm == 1000) Sound("time", "taiwan open");
                    else if (HHmm == 1030) Sound("time", "china open");
                    else if (HHmm == 1450) Sound("time", "30");
                    else if (HHmm == 1455) Sound("time", "25");
                    else if (HHmm == 1500) Sound("time", "20");
                    else if (HHmm == 1505) Sound("time", "15");
                    else if (HHmm == 1510) Sound("time", "10");
                    else if (HHmm == 1515) Sound("time", "5");
                    else if (HHmm == 1518) Sound("time", "2");
                    else if (HHmm == 1519) Sound("time", "1");
                    else if (HHmm == 1521) Sound("time", "Transfer Money");
                }
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// (subdir, filename) 형태의 wav를 재생한다. **절대 UI를 막지 않음**
        /// </summary>
        public static void Sound(string sub_directory, string sound)
        {
            if (string.IsNullOrWhiteSpace(sound))
                return;

            string sound_file;
            if (string.IsNullOrWhiteSpace(sub_directory))
                sound_file = @"C:\BJS\data work\소\" + sound + ".wav";
            else
                sound_file = @"C:\BJS\data work\소\" + sub_directory + "\\" + sound + ".wav";

            if (!File.Exists(sound_file))
                return;

            // UI block 금지: 항상 비동기로
            Task.Run(() =>
            {
                try
                {
                    SoundPlayer player;

                    // 파일 경로 단위로 캐시 (같은 파일 반복 호출 대비)
                    lock (_lock)
                    {
                        if (!_cache.TryGetValue(sound_file, out player))
                        {
                            player = new SoundPlayer(sound_file);
                            try { player.Load(); } catch { /* ignore */ }
                            _cache[sound_file] = player;
                        }
                    }

                    player.Play(); // 비동기 재생 (PlaySync 금지)
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Sound play error ({sub_directory}/{sound}): {ex.Message}");
                }
            });
        }
    }
}
