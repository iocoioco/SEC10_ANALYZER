using CPUTILLib;
using New_Tradegy.Library.Core;
using New_Tradegy.Library.Models;
using New_Tradegy.Library.PostProcessing;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace New_Tradegy.Library.IO
{
    internal class FileInStockData
    {
        //public static void read_or_set_stocks()
        //{
        //    string path = $@"C:\BJS\분\{g.date}";
        //    Directory.CreateDirectory(path);

        //    foreach (var d in g.StockRepo.AllDatas
        //                 .Where(x => x != null &&
        //                             (x.Kind == StockData.InstrumentKind.Stock ||
        //                              x.Kind == StockData.InstrumentKind.Index)))
        //    {
        //        LoadStockData(d, path);
        //    }
        //}
        public static void LoadStockData(StockData t, string file)
        {
            if (!file.Contains(".txt"))
            {
                file += "\\" + t.Stock + ".txt";
            }

            if (!File.Exists(file))
            {
                if (!File.Exists(file))
                {
                    if (g.test)
                        return;

                    bool isIndex = g.StockManager.IndexList.Contains(t.Stock);
                    EnsureStockFile(file, isIndex);
                    t.Api.분의시간[0] = 0;
                    return;
                }
            }

            var lines = File.ReadAllLines(file);
            if (lines.Length == 0) return;

            t.Api.nrow = 0;
            foreach (var line in lines)
            {
                if (!TryParseLineToApi(t, line)) break;
            }

            if (g.test && t.Api.nrow > g.Npts[1])
            {
                g.Npts[1] = 2;
                g.TestMaximumRow = t.Api.nrow;
            }

            if (t.Api.nrow == 1) return;

            ApplyLastTickData(t);
            PostProcessStock(t);

            if (t.Api.nrow > 1)
                t.Api.분의시간[0] = t.Api.x[t.Api.nrow - 1, 0] / 100;  // hhmm
            else
                t.Api.분의시간[0] = 0;

        }
        private static void EnsureStockFile(string file, bool isIndex)
        {
            if (!File.Exists(file) || File.ReadAllLines(file).Length == 0)
            {
                string line = isIndex
                    ? "85959\t0\t100\t0\t0\t0\t0\t0\t0\t0\t0\t0"
                    : "85959\t0\t100\t10000\t0\t0\t0\t0\t0\t0\t0\t0";

                File.WriteAllText(file, line + Environment.NewLine);
            }
        }
        private static bool TryParseLineToApi(StockData t, string line)
        {
            var words = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length < 12) return true;

            int time = words[0].Contains(":")
                ? Utils.TimeUtils.TimeToInt(words[0])
                : Convert.ToInt32(words[0]);

            if (time < 85959 || time >= 152100) return false;

            t.Api.x[t.Api.nrow, 0] = time;
            for (int j = 1; j < words.Length; j++)
            {
                t.Api.x[t.Api.nrow, j] = Convert.ToInt32(words[j]);
            }

            t.Api.nrow++;
            return true;
        }
        private static void ApplyLastTickData(StockData t)
        {
            int row = t.Api.nrow - 1;
            int[] last = new int[12];
            for (int i = 0; i < 12; i++)
                last[i] = t.Api.x[row, i];

            int 거래량 = last[7];

            double intensity = 0.0;
            if (last[3] > 100000)
                intensity = last[3] / g.MILLION;
            else
                intensity = last[3] / g.HUNDRED;

            t.Api.당일프로그램순매수량 = last[4];
            t.Api.당일외인순매수량 = last[5];
            t.Api.당일기관순매수량 = last[6];

            t.Api.틱의시간[0] = last[0] * 1000;
            t.Api.틱의가격[0] = last[1];
            t.Api.틱의수급[0] = last[2];
            t.Api.틱의체강[0] = last[3];

            if (intensity > 0)
            {
                t.Api.틱수누량[0] = (int)(거래량 * intensity / (100.0 + intensity));
                t.Api.틱도누량[0] = (int)(거래량 * 100.0 / (100.0 + intensity));
            }

            t.Api.틱매수배[0] = last[8];
            t.Api.틱매도배[0] = last[9];

            t.Post.종누천 = (int)(t.Api.전일종가 * 거래량 / g.천만원); // 거래량(마지막 라인, 분) 
        }
        private static void PostProcessStock(StockData t)
        {
            var x = t.Api.x;

            if (!g.StockManager.IndexList.Contains(t.Stock) &&
                t.Api.nrow >= 2)
            {
                for (int j = 1; j < t.Api.nrow; j++)
                {
                    x[j, 10] = (x[j, 7] == x[j - 1, 7])
                        ? x[j - 1, 10]
                        : (x[j, 2] > x[j - 1, 2] ? x[j - 1, 10] + 1 : 0);

                    x[j, 11] = (x[j, 7] == x[j - 1, 7])
                        ? x[j - 1, 11]
                        : ((x[j, 3] / g.MILLION) > (x[j - 1, 3] / g.MILLION) ? x[j - 1, 11] + 1 : 0);
                }
            }

            for (int j = 0; j < t.Api.nrow; j++)
            {
                //PostProcessor.PostPassing(t, j, false);
            }
        }
    }

    internal class FileIn
    {
        private static CPUTILLib.CpStockCode _cpstockcode = new CPUTILLib.CpStockCode();

        static CPUTILLib.CpCodeMgr _cpcodemgr;

        public static List<string> txtFilesInGivenDirectory(string directory)
        {
            if (!Directory.Exists(directory))
            {
                return null;
            }
            var txtFiles = Directory.GetFiles(directory, "*.txt")
                     .Select(Path.GetFileName)
                     .ToList();

            return txtFiles;
        }

        public static void read_제어()
        {
            string 바탕화면 = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string filename = 바탕화면 + @"\제어.txt";


            if (!File.Exists(filename)) return;
            string[] grlines = System.IO.File.ReadAllLines(filename, Encoding.Default);

            string[] strs = grlines[0].Split(' ');
            if (strs[0] == "t")
                g.test = true;
            else
                g.test = false;

            strs = grlines[1].Split(' ');
            if (strs[0] == "s")
                g.shortform = true;
            else
                g.shortform = false;

            strs = grlines[2].Split(' ');
            g.Account = strs[0];

            strs = grlines[3].Split(' ');
            g.date = Convert.ToInt32(strs[0]);

            strs = grlines[4].Split(' ');
            double.TryParse(strs[0], out double RithmicValue);
            double.TryParse(strs[1], out double Percentage);

            // ✅ 핵심: 0% 기준가(basis) 역산
            g.RithmicBasis = RithmicValue / (1.0 + Percentage / 100.0);

            // (원하면) 시작 직후 화면에 “그 시점의 %”도 세팅
            MajorIndex.Instance.NasdaqIndex = (float)Percentage;

        }
        public static void read_변수()
        {
            string filename = @"C:\BJS\data work\변수.txt";

            if (!File.Exists(filename)) return;
            string[] grlines = System.IO.File.ReadAllLines(filename, Encoding.Default);


            foreach (string line in grlines)
            {
                var words = line.Split('\t');

                switch (words[0])
                {

                    case "q_advance_lines":
                        라인분리(line, ref g.v.q_advance_lines);
                        break;
                    case "Q_advance_lines":
                        라인분리(line, ref g.v.Q_advance_lines);
                        break;
                    case "r3_display_lines":
                        라인분리(line, ref g.v.r3_display_lines);
                        break;



                    default:
                        break;
                }
            }
        }

        public static void 라인분리(string line, ref int data) // scalar -> no 비중, dev, mkc
        {
            string[] words = line.Split('\t');
            Int32.TryParse(words[1], out data);
        }

        // 네 기존 함수들 (이미 프로젝트에 있으니 시그니처만 맞추면 됨)
        public static int read_전일종가(string stock)
        {

            string path = @"C:\BJS\data work\일\" + stock + ".txt";
            if (!File.Exists(path))
            {
                return -1;
            }

            string lastline = File.ReadLines(path).Last(); // last line read 

            string[] words = lastline.Split(' ');
            return Convert.ToInt32(words[4]);
        }

        public static void read_삼성_코스피_코스닥_전체종목(bool masterOnly)
        {
            string filename = @"C:\BJS\data work\삼성_코스피_코스닥_전체종목.txt";
            if (!File.Exists(filename))
            {
                MessageBox.Show("삼성_코스피_코스닥_전체종목.txt Not Exist");
                return;
            }

            // 두 번 호출에도 안전하게: 매번 초기화
            g.kospi_mixed.stocks.Clear(); g.kospi_mixed.weights.Clear();
            g.kosdaq_mixed.stocks.Clear(); g.kosdaq_mixed.weights.Clear();

            var lines = File.ReadAllLines(filename);
            bool kosdaqSection = false;
            var seen = new HashSet<string>(); // 중복 방지

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) { kosdaqSection = true; continue; }

                var words = line.Split('\t');
                if (words.Length < 6) continue;

                string stock = words[1]?.Trim();
                if (string.IsNullOrEmpty(stock)) continue;

                // ETF/파생 필터
                if (!wk.isStock(stock) || stock.Contains("KODEX") || stock.Contains("PLUS") ||
                    stock.Contains("200") || stock.Contains("RISE") || stock.Contains("SOL"))
                    continue;

                if (!seen.Add(stock)) continue;

                // % 제거 후 double 파싱 (문화권 고정)
                string t = words[5].Trim('%', '(', ')', ' ');
                if (!double.TryParse(t, System.Globalization.NumberStyles.Any,
                                     System.Globalization.CultureInfo.InvariantCulture, out double w))
                    continue;

                if (!kosdaqSection)
                {
                    if (g.kospi_mixed.stocks.Count >= 50) continue; // 🔥 KOSPI 최대 50개
                    g.kospi_mixed.stocks.Add(stock);
                    g.kospi_mixed.weights.Add(w);
                }
                else
                {
                    if (g.kosdaq_mixed.stocks.Count >= 50) continue; // 🔥 KOSDAQ 최대 50개
                    g.kosdaq_mixed.stocks.Add(stock);
                    g.kosdaq_mixed.weights.Add(w);
                }
            }

            // masterOnly=false이면 레포지토리와 교차(없는 종목 제거)
            if (!masterOnly)
            {
                FilterByRepositoryAndRenormalize(g.kospi_mixed);
                FilterByRepositoryAndRenormalize(g.kosdaq_mixed);
            }
            else
            {
                // 1차 호출에서는 그냥 1.0로 정규화만
                g.kospi_mixed.weights = AdjustToSumOne(g.kospi_mixed.weights);
                g.kosdaq_mixed.weights = AdjustToSumOne(g.kosdaq_mixed.weights);
            }

            // 간단한 점검
            string msg = $"KOSPI {g.kospi_mixed.stocks.Count} / sum={g.kospi_mixed.weights.Sum():0.000}\n" +
                         $"KOSDAQ {g.kosdaq_mixed.stocks.Count} / sum={g.kosdaq_mixed.weights.Sum():0.000}";
            // MessageBox.Show(msg, "Constituent Stock Check"); // 필요시


        }

        static void FilterByRepositoryAndRenormalize(dynamic group)
        {
            var have = new HashSet<string>(
                g.StockRepo.AllDatas.Select(d => d.Stock)   // d.Code 등 실제 필드명으로 변경
            );

            var newStocks = new List<string>();
            var newWeights = new List<double>();

            for (int i = 0; i < group.stocks.Count; i++)
            {
                string s = group.stocks[i];
                if (have.Contains(s))
                {
                    newStocks.Add(s);
                    newWeights.Add(i < group.weights.Count ? group.weights[i] : 1.0);
                }
                else
                {
                    // 누락 로그 (원하면 CSV 누적)
                    // LogMissing(s, "NoData");
                }
            }

            group.stocks = newStocks;
            group.weights = AdjustToSumOne(newWeights);
        }

        static List<double> AdjustToSumOne(List<double> w)
        {
            if (w == null || w.Count == 0) return new List<double>();
            double sum = w.Sum();
            if (sum <= 0) return Enumerable.Repeat(1.0 / w.Count, w.Count).ToList();
            for (int i = 0; i < w.Count; i++) w[i] /= sum;
            return w;
        }
        public static bool read_단기과열(string stock)
        {
            _cpcodemgr = new CPUTILLib.CpCodeMgr();
            _cpstockcode = new CPUTILLib.CpStockCode();
            int t = (int)_cpcodemgr.GetOverHeating(_cpstockcode.NameToCode(stock));

            if (t == 2 || t == 3)
                return true;
            else
                return false;
        }
        public static char read_코스피코스닥시장구분(string stock)
        {
            _cpcodemgr = new CPUTILLib.CpCodeMgr();
            _cpstockcode = new CPUTILLib.CpStockCode();
            int marketKind = (int)_cpcodemgr.GetStockMarketKind(_cpstockcode.NameToCode(stock));
            if (marketKind == 1)

            {
                return 'S';

            }
            else if (marketKind == 2)
            {
                return 'D';
            }
            else
                return 'N';
        }

        public static void LoadOrSaveKodexMagnifier(string to_do)
        {
            string filename = @"C:\BJS\data work\KodexMagnifier.txt";

            const double SCALE_DOWN_THRESHOLD = 5000.0;
            const double SCALE_UP_THRESHOLD = 0.005;
            const double SCALE_FACTOR = 2.0;

            // ===== READ =====
            if (string.Equals(to_do, "read", StringComparison.OrdinalIgnoreCase))
            {
                if (!File.Exists(filename))
                {
                    MessageBox.Show("KodexMagnifier.txt Not Exist");
                    return;
                }

                string[] lines = File.ReadAllLines(filename);

                for (int i = 0; i < Math.Min(2, lines.Length); i++)
                {
                    if (string.IsNullOrWhiteSpace(lines[i])) continue;

                    string[] tokens = lines[i].Split('\t');

                    for (int j = 0; j < Math.Min(3, tokens.Length); j++)
                    {
                        if (double.TryParse(tokens[j], out double v))
                            g.KodexMagnifier[i, j] = v;
                    }
                }

                return;
            }

            // ===== WRITE =====
            // 1) Normalize (폭주/소멸 방지) : 행 단위 스케일링
            for (int i = 0; i < 2; i++)
            {
                double max = g.KodexMagnifier[i, 0];
                double min = g.KodexMagnifier[i, 0];

                for (int j = 1; j < 3; j++)
                {
                    double val = g.KodexMagnifier[i, j];
                    if (val > max) max = val;
                    if (val < min) min = val;
                }

                if (max > SCALE_DOWN_THRESHOLD)
                {
                    for (int j = 0; j < 3; j++)
                        g.KodexMagnifier[i, j] /= SCALE_FACTOR;
                }
                else if (min < SCALE_UP_THRESHOLD)
                {
                    for (int j = 0; j < 3; j++)
                        g.KodexMagnifier[i, j] *= SCALE_FACTOR;
                }
            }

            // 2) Save file
            var sb = new StringBuilder();
            for (int i = 0; i < 2; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    sb.Append(g.KodexMagnifier[i, j].ToString("F12"));
                    if (j < 2) sb.Append('\t');
                }
                sb.AppendLine();
            }
            File.WriteAllText(filename, sb.ToString());
        }

        public static void read_파일관심종목()
        {
            string filename = @"C:\BJS\data work\관심.txt";

            g.StockManager.InterestedInFile.Clear();

            if (File.Exists(filename))
            {
                string[] grlines = File.ReadAllLines(filename, Encoding.Default);

                List<string> temp_list = new List<string>();
                foreach (string line in grlines)
                {
                    string[] words = line.Split(' '); // empty spaces also recognized as words, word.lenght can be larger than 4

                    for (int i = 0; i < words.Length; i++)
                    {
                        if (words[i] == "//")
                            break;
                        if (words[i] == "")
                            continue;
                        string stock = words[i].Replace('_', ' ');
                        if (g.StockRepo.Contains(stock))
                            g.StockManager.InterestedInFile.Add(stock);
                    }
                }
            }
        }

        public static int read_전일거래액_천만원(string stock)
        {
            string path = @"C:\BJS\data work\일\" + stock + ".txt";
            if (!File.Exists(path))
            {
                return -1;
            }

            string lastline = File.ReadLines(path).Last(); // last line read 

            string[] words = lastline.Split(' ');
            int 전일종가 = Convert.ToInt32(words[4]);
            ulong 전일거래량 = Convert.ToUInt64(words[5]);
            return (int)(전일종가 * (전일거래량 / g.천만원));
        }
    }

    public static class StandardCurveCache
    {
        private static double[] _gStd;
        private static readonly int _startHHmm = 900;   // 09:00
        private static readonly int _endHHmm = 1520;  // 15:20

        public static void LoadFromFile(string path)
        {
            var vals = new List<double>();
            foreach (var line in File.ReadLines(path))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (line[0] == '#') continue;

                var parts = line.Split(',');
                if (parts.Length < 3) continue;

                if (double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var g))
                    vals.Add(g);
            }
            if (vals.Count == 0) throw new InvalidOperationException("표준환산표 데이터가 비어 있습니다.");
            _gStd = vals.ToArray();
        }

        // 핵심: 분단위 g_std 배열에서 초/밀리초까지 선형보간
        public static double GetG(int hhmm, int ss = 0, int fff = 0)
        {
            if (_gStd == null) throw new InvalidOperationException("표준환산표가 로드되지 않았습니다.");

            int idx = MinuteIndexFromHHmm(_startHHmm, hhmm); // 0-based
            if (idx < 0) return _gStd[0];
            if (idx >= _gStd.Length - 1) return _gStd[_gStd.Length - 1];


            if (ss < 0) ss = 0; else if (ss > 59) ss = 59;
            if (fff < 0) fff = 0; else if (fff > 999) fff = 999;

            double frac = (ss + fff / 1000.0) / 60.0; // [0,1)
            double g0 = _gStd[idx];
            double g1 = _gStd[idx + 1];
            return g0 + frac * (g1 - g0);
        }

        // HHmmss → HHmm + ss 분해
        public static double GetGFromHHmmss(int hhmmss)
        {
            // 합리적 범위 검증 (00:00:00 ~ 23:59:59)
            if (hhmmss < 0 || hhmmss > 235959)
                throw new ArgumentOutOfRangeException(nameof(hhmmss), "HHmmss 범위는 000000~235959 이어야 합니다.");

            int H = (hhmmss / 10000) % 100;
            int M = (hhmmss / 100) % 100;
            int ss = hhmmss % 100;

            if (H > 23 || M > 59 || ss > 59)
                throw new ArgumentOutOfRangeException(nameof(hhmmss), "시/분/초 범위를 벗어났습니다.");

            int hhmm = H * 100 + M;
            return GetG(hhmm, ss, 0);
        }

        // HHmmssfff → HHmm + ss + fff 분해
        public static double GetGFromHHmmssfff(int hhmmssfff)
        {
            // 합리적 범위 검증 (00:00:00.000 ~ 23:59:59.999)
            if (hhmmssfff < 0 || hhmmssfff > 235959999)
                throw new ArgumentOutOfRangeException(nameof(hhmmssfff), "HHmmssfff 범위는 000000000~235959999 이어야 합니다.");

            int H = (hhmmssfff / 10000000) % 100;
            int M = (hhmmssfff / 100000) % 100;
            int ss = (hhmmssfff / 1000) % 100;
            int fff = hhmmssfff % 1000;

            if (H > 23 || M > 59 || ss > 59 || fff > 999)
                throw new ArgumentOutOfRangeException(nameof(hhmmssfff), "시/분/초/밀리초 범위를 벗어났습니다.");

            int hhmm = H * 100 + M;
            return GetG(hhmm, ss, fff);
        }

        // 값 하나로 자동 판별: <=235959 → HHmmss, 그 외 → HHmmssfff
        public static double GetGAuto(int time)
        {
            return (time <= 235959) ? GetGFromHHmmss(time) : GetGFromHHmmssfff(time);
        }

        // ---- 내부 유틸 ----
        private static int MinuteIndexFromHHmm(int startHHmm, int hhmm)
        {
            int sH = startHHmm / 100, sM = startHHmm % 100;
            int H = hhmm / 100, M = hhmm % 100;
            return (H * 60 + M) - (sH * 60 + sM);
        }
    }


    // usage
    // double g3 = StandardCurve.GetG(93000000);             // 09:30:00.000 (9자리 → fff 포함)
    // double g4 = StandardCurve.GetG(93559);
    public static class StandardCurve
    {
        private static readonly object _lock = new object();
        private static volatile bool _loaded = false;
        private static string _path;
        private static DateTime _lastWrite;

        public static void Init(string path)
        {
            if (_loaded && string.Equals(path, _path, StringComparison.OrdinalIgnoreCase)) return;

            lock (_lock)
            {
                if (_loaded && string.Equals(path, _path, StringComparison.OrdinalIgnoreCase)) return;

                StandardCurveCache.LoadFromFile(path);
                _path = path;
                _lastWrite = File.GetLastWriteTimeUtc(path);
                _loaded = true;
            }
        }

        public static void ReloadIfChanged()
        {
            if (!_loaded || string.IsNullOrEmpty(_path)) return;
            var ts = File.GetLastWriteTimeUtc(_path);
            if (ts <= _lastWrite) return;

            lock (_lock)
            {
                ts = File.GetLastWriteTimeUtc(_path);
                if (ts <= _lastWrite) return;
                StandardCurveCache.LoadFromFile(_path);
                _lastWrite = ts;
            }
        }

        // ===== 공개 API (int만 사용) =====
        // 1) HHmmss (예: 090025 → 9시 0분 25초)
        public static double GetGFromHHmmss(int hhmmss)
        {
            EnsureLoaded();
            return StandardCurveCache.GetGFromHHmmss(hhmmss);
        }

        // 2) HHmmssfff (예: 090025123 → 9시 0분 25초 123ms)
        public static double GetGFromHHmmssfff(int hhmmssfff)
        {
            EnsureLoaded();
            return StandardCurveCache.GetGFromHHmmssfff(hhmmssfff);
        }

        // 3) 자동 판별 (<=235959 → HHmmss, 그 외 → HHmmssfff)
        public static double GetG(int time)
        {
            EnsureLoaded();
            return StandardCurveCache.GetGAuto(time);
        }

        // (호환 유지) 필요하면 직접 HHmm + 초/밀리초로 호출
        public static double GetG_HHmm_SecMs(int hhmm, int ss = 0, int fff = 0)
        {
            EnsureLoaded();
            return StandardCurveCache.GetG(hhmm, ss, fff);
        }

        private static void EnsureLoaded()
        {
            if (!_loaded) throw new InvalidOperationException("StandardCurve.Init()을 먼저 호출하세요.");
        }
    }
}




