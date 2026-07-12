using New_Tradegy.Library.Core;
using New_Tradegy.Library.Models;
using New_Tradegy.Library.PostProcessing;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace New_Tradegy.Library.IO
{
    // ✅ 부팅/초기화의 "골격"만: 어디서 무엇을 하는지 정리용
    // - FileIn.* 는 "데이터 읽기/생성"만
    // - Repo 커밋 후에만 Group/Interest/Sector 파생을 만든다

    public enum BootMode
    {
        Live,       // 시총.txt 기반 (장 시작/장중)
        TestMinute  // 분 디렉토리 기반 (테스트/재시작)
    }

    public static class BootStrap
    {
       
        public static void Initialize()
        {
            BootMode mode;

            
            if (!g.test) mode = BootMode.Live;
            else
            {
                mode = BootMode.TestMinute;
            }

            FileIn.read_변수();
            StandardCurve.Init(@"C:\BJS\data work\표준환산표.txt");
            FileIn.LoadOrSaveKodexMagnifier("read");

            var minuteDir = $@"C:\BJS\분\{g.date}";

            // 1) Universe 준비 + 검증 + Repo 커밋 (거래 가능한 종목만)
            BuildTradingUniverse(mode, minuteDir);     // ✅ 여기서 repo가 완성됨
        
            FileLoader.LoadUniverseMinuteData(minuteDir);
            FileLoader.LoadIndex10SecData();

            g.StockManager = new StockManager(g.StockRepo);
            
            // 2) 파생들 (repo 기준으로만)
            BuildDerivedAfterRepo();                   // group, 삼성/관심 prune, sector

            g.StockRepo.BuildAllGeneralCache();
            g.StockRepo.BuildAllSectorCache();
            g.StockRepo.BuildAllGeneralNamesCache();
        }

        private static void BuildTradingUniverse(BootMode mode, string minuteDir)
        {
            // candidates 준비 (시총 or 분디렉)
            var candidates = (mode == BootMode.TestMinute)
                ? UniverseBuilder.FromMinuteDirectory(minuteDir)
                : UniverseBuilder.FromMarketCapStockList();

            // 시총Map 준비 (live면 시총.txt에서, test면 필요하면 최소 맵을 만들거나 0으로 둬도 됨)
            var marketCapMap = UniverseBuilder.FromMarketCapDictionary();


            // 1) ogl 통과한 것만 tmp
            var tmp = RepoBuilder.BuildTmpFromOgl(candidates, marketCapMap);

            // 2) (다음 단계) tmp에 통계/절친 붙이고, 실패 제거
            UniversePipeline.EnrichAndPrune(tmp);

            // 3) 최종 repo 커밋
            RepoBuilder.CommitTradables(tmp);

            var path = @"C:\BJS\logs\tmp_names.txt";
            File.WriteAllLines(path, tmp.Keys, Encoding.Default);
        }

        private static void BuildDerivedAfterRepo()
        {
            // ✅ Group (LoadGroups가 repo 기반 prune 포함)
            g.GroupManager = new GroupManager();
            GroupRepository.LoadGroups();   // 내부에서 GroupRepository.LoadGroups() 호출 가능
                                           // g.GroupManager.PruneByRepo(); // 필요하면 추가 (지금 LoadGroups에서 이미 repo 체크 중)

            // ✅ 삼성/관심 종목 리스트도 repo 기준 prune (이미 되어있다고 했음)
            FileIn.read_삼성_코스피_코스닥_전체종목(masterOnly: false);
            FileIn.read_파일관심종목();

            
            // ✅ 섹터 생성 (repo+group 기반)
            SectorBuilder.BuildSectorsFromSavedMinuteData();
        }
    }


    // 후보 수집 책임
    public static class UniverseBuilder
    {
        // ✅ 라이브(장 시작/장중): 시총.txt에서 후보 종목 수집
        public static List<string> FromMarketCapStockList()
        {
            var 시총Map = new ConcurrentDictionary<string, double>(
                File.ReadAllLines(@"C:\BJS\data work\시총.txt", Encoding.Default)
                .Select(line => line.Trim()
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                .Where(parts => parts.Length >= 2)
                .GroupBy(parts => parts[0].Replace("_", " "))
                .ToDictionary(
                g => g.Key,
                g => double.TryParse(g.First()[1], out var v) ? v : -1
                )
                );


            // 후보 종목 키만 리턴
            return 시총Map.Keys.ToList();
        }

        public static Dictionary<string, double> FromMarketCapDictionary()
        {
            return File.ReadAllLines(@"C:\BJS\data work\시총.txt", Encoding.Default)
            .Select(line => line.Trim()
            .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            .Where(parts => parts.Length >= 2)
            .GroupBy(parts => parts[0].Replace("_", " "))
            .ToDictionary(
            g => g.Key,
            g => double.TryParse(g.First()[1], out var v) ? v : -1
            );
        }

        private static double Median(List<double> sorted)
        {
            int n = sorted.Count;
            if (n == 0) return 0;

            int mid = n / 2;
            if (n % 2 == 1)
                return sorted[mid];

            return (sorted[mid - 1] + sorted[mid]) / 2.0;
        }


        

        // ✅ "20260119" 같은 날짜 문자열만 받는다
        public static List<string> FromMinuteDirectory(string minuteDir)
        {
            if (!Directory.Exists(minuteDir))
                return new List<string>();

            // 1) txt 파일 목록
            var files = Directory.GetFiles(minuteDir, "*.txt", SearchOption.TopDirectoryOnly);
            if (files.Length == 0)
                return new List<string>();

            // 2) 파일 크기(KB) 리스트
            //    - 0KB/접근불가 같은 건 안전하게 제외
            var fileInfos = files
                .Select(p =>
                {
                    try { return new FileInfo(p); }
                    catch { return null; }
                })
                .Where(fi => fi != null && fi.Exists)
                .ToList();

            if (fileInfos.Count == 0)
                return new List<string>();

            var sizesKb = fileInfos
                .Select(fi => (double)fi.Length / 1024.0)
                .Where(kb => kb > 0) // 0KB 제거
                .OrderBy(kb => kb)
                .ToList();

            if (sizesKb.Count == 0)
                return new List<string>();

            // 3) 중앙값(median)
            double medianKb = Median(sizesKb);

            // 4) 중앙값의 1/3 미만 제거 (다운로드 중단/손상 파일 컷)
            double cutoffKb = medianKb / 3.0;

            // (선택) 너무 작은 파일 하드 컷도 원하면 여기서:
            // double hardMinKb = 2.0; // 예: 2KB 미만 무조건 컷
            // cutoffKb = Math.Max(cutoffKb, hardMinKb);

            // 5) 파일명 -> 종목명 추출
            //    - "CJ제일제당.txt" => "CJ제일제당"
            //    - 언더바는 공백으로 (시총.txt 규칙과 맞추기)
            var stocks = fileInfos
                .Where(fi => ((double)fi.Length / 1024.0) >= cutoffKb)
                .Select(fi => Path.GetFileNameWithoutExtension(fi.Name))
                .Select(name => (name ?? "").Trim().Replace('_', ' '))
                .Where(name => name.Length > 0)
                .Distinct()
                .ToList();

            return stocks;
        }
    }



    // OglStock 생성
    public static class RepoBuilder
    {
        // ✅ candidates 돌면서 TryBuildOglStockData 통과한 StockData만 모아서 리턴
        public static Dictionary<string, StockData> BuildTmpFromOgl(
            List<string> candidates,
            IDictionary<string, double> marketCapMap)
        {
            var tmp = new Dictionary<string, StockData>(candidates?.Count ?? 0);

            if (candidates == null || candidates.Count == 0)
                return tmp;
            
            var failPath = @"C:\BJS\logs\failed_tmp.txt";
            Directory.CreateDirectory(Path.GetDirectoryName(failPath));

            using (var swFail = new StreamWriter(failPath, false))
            {
                foreach (var stock in candidates)
                {
                    if (string.IsNullOrWhiteSpace(stock))
                        continue;

                    StockData data;
                    if (TryBuildOglStockData(stock, marketCapMap, out data))
                    {
                        tmp[data.Stock] = data; // 중복 덮어씀
                    }
                    else
                    {
                        swFail.WriteLine(stock); // ❌ 탈락 종목 기록
                    }
                }
            }

            return tmp;
        }
        private static readonly CPUTILLib.CpStockCode _cpStockCode = new CPUTILLib.CpStockCode();
        private static bool TryBuildOglStockData(
           string stock,
           IDictionary<string, double> map,
           out StockData data)
        {
            data = null;

            if (FileIn.read_단기과열(stock))
                return false;

            // (싸고 빠른 컷 먼저)
            if (!map.TryGetValue(stock, out var 시총값))
                return false;

            string code = _cpStockCode.NameToCode(stock);
            long 전일종가 = FileIn.read_전일종가(stock);

            // !stock.Contains("KODEX") 추가 이유는  코스닥 인버스 가격이 500원 ?
            if (code.Length != 7 || (전일종가 < 1000 && !stock.Contains("KODEX")))
                return false;

            double 전일거래액_천만원 = FileIn.read_전일거래액_천만원(stock);
            if (전일거래액_천만원 == -1)
                return false;

            char 시장구분 = FileIn.read_코스피코스닥시장구분(stock);
            if (시장구분 != 'S' && 시장구분 != 'D')
                return false;

            // (비싼 계산은 뒤로)
            int days = 20;
            wk.종목일중변동자료계산(
                stock, days,
                out double 일간변동평균, out double 일간변동편차,
                out int 평균거래액_10M, out int 일최저거래액, out int 일최대거래액,
                out ulong 일평균거래량, out string 일간변동평균편차);

            // ✅ 여기부터 data 채움 (성공 확정 구간)
            data = new StockData();

            data.Stock = stock;
            data.Code = code;
            if (stock.Contains("KODEX"))

                data.Kind = StockData.InstrumentKind.Index;
            else
                data.Kind = StockData.InstrumentKind.Stock;

            data.Statistics.일간변동평균 = 일간변동평균;
            data.Statistics.일간변동편차 = 일간변동편차;
            data.Statistics.AvgDailyTurnover_10M = 평균거래액_10M;
            data.Statistics.일최저거래액 = 일최저거래액;
            data.Statistics.일최대거래액 = 일최대거래액;
            data.Statistics.일평균거래량 = 일평균거래량;
            data.Statistics.일간변동평균편차 = 일간변동평균편차;
            data.Statistics.시장구분 = 시장구분;
            data.Statistics.시총 = 시총값 / 100.0;

            data.Api.전일종가 = 전일종가;
            data.Api.전일거래액_천만원 = 전일거래액_천만원;

            data.Score.SectorRank = 1000;

            return true;
        }


        // ✅ tmp에 든 것만 repo에 최종 커밋
        public static void CommitTradables(Dictionary<string, StockData> tmp)
        {
            if (tmp == null) return;

            var repo = g.StockRepo;

            repo.ClearAll(); // ✅ 이전 잔재 완전 제거

            foreach (var d in tmp.Values)
                repo.AddOrUpdate(d.Stock, d);
        }

    }

    internal static class FileLoader
    {
        public static void LoadIndex10SecData()
        {
            string directory = $@"C:\BJS\Study\지수10초\{g.date}";

            if (!Directory.Exists(directory))
                return;

            LoadIndex10SecFile(
                Path.Combine(directory, "KODEX 레버리지.txt"),
                true);

            LoadIndex10SecFile(
                Path.Combine(directory, "KODEX 코스닥150레버리지.txt"),
                false);
        }

        private static void LoadIndex10SecFile(string file, bool isKospi)
        {
            if (!File.Exists(file))
                return;

            var lines = File.ReadAllLines(file);

            int row = 0;

            foreach (var line in lines.Skip(1)) // header skip
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var sp = line.Split(',');

                if (sp.Length != 9)
                    continue;

                for (int k = 0; k < 9; k++)
                {
                    if (isKospi)
                        Sec10Store.Kospi[row, k] = int.Parse(sp[k]);
                    else
                        Sec10Store.Kosdaq[row, k] = int.Parse(sp[k]);
                }

                row++;
            }

            if (isKospi)
                Sec10Store.KospiRow = row;
            else
                Sec10Store.KosdaqRow = row;
        }


        public static void LoadUniverseMinuteData(string minuteDir)
        {
            Directory.CreateDirectory(minuteDir);

            // ✅ repo에서 대상 선정: Stock + Index 만
            foreach (var d in g.StockRepo.AllDatas
                         .Where(x => x != null &&
                                     (x.Kind == StockData.InstrumentKind.Stock ||
                                      x.Kind == StockData.InstrumentKind.Index)))
            {
                LoadStockData(d, minuteDir);
            }
        }

        public static void LoadStockData(StockData t, string dirOrFile)
        {
            string file = dirOrFile;
            if (!file.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                file = Path.Combine(file, t.Stock + ".txt");

            if (!File.Exists(file) || new FileInfo(file).Length == 0)
            {
                if (g.test)
                    return; // ✅ 테스트: 없으면 그냥 스킵

                bool isIndex = (t.Kind == StockData.InstrumentKind.Index)
                               || g.StockManager?.IndexList?.Contains(t.Stock) == true;

                EnsureStockFile(file, isIndex);
                // 생성만 하고 리턴할지, 바로 읽을지 선택:
                // return;  // (기존처럼 생성만)
                // 또는 계속 아래로 읽어서 nrow=1 세팅
            }

            // ✅ 여기서부터 실제 로딩
            var lines = File.ReadAllLines(file);
            if (lines.Length == 0) return;

            

            t.Api.nrow = 0;

            foreach (var line in lines)
            {
                if (!TryParseLineToApi(t, line))
                    break;
            }

            // test 모드에서 g.Npts[1] 자동 보정은 기존 로직 유지
            if (g.test && t.Api.nrow > g.Npts[1])
            {
                g.Npts[1] = 2;
                g.TestMaximumRow = t.Api.nrow;
            }

            if (t.Api.nrow <= 1)
                return;

            ApplyLastTickData(t);
            PostProcessStock(t);
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
            if (words.Length < 12) return true; // 빈/짧은 라인은 그냥 스킵

            int time = words[0].Contains(":")
                ? Utils.TimeUtils.TimeToInt(words[0])
                : Convert.ToInt32(words[0]);

            if (time < 85959 || time >= 152100) return false;

            int r = t.Api.nrow;

            // ✅ (중요) row overflow 방어
            if (r >= t.Api.x.GetLength(0)) return false;

            t.Api.x[r, 0] = time;
            for (int j = 1; j < 12 && j < words.Length; j++)
                t.Api.x[r, j] = Convert.ToInt32(words[j]);

            t.Api.nrow++;
            return true;
        }

        private static void ApplyLastTickData(StockData t)
        {
            int row = t.Api.nrow - 1;
            var x = t.Api.x;

            int 거래량 = x[row, 7];

            double intensity = (x[row, 3] > 100000)
                ? x[row, 3] / g.MILLION
                : x[row, 3] / g.HUNDRED;

            t.Api.당일프로그램순매수량 = x[row, 4];
            t.Api.당일외인순매수량 = x[row, 5];
            t.Api.당일기관순매수량 = x[row, 6];

            t.Api.틱의시간[0] = x[row, 0] * 1000;
            t.Api.틱의가격[0] = x[row, 1];
            t.Api.틱의수급[0] = x[row, 2];
            t.Api.틱의체강[0] = x[row, 3];

            if (intensity > 0)
            {
                t.Api.틱수누량[0] = (int)(거래량 * intensity / (100.0 + intensity));
                t.Api.틱도누량[0] = (int)(거래량 * 100.0 / (100.0 + intensity));
            }

            t.Api.틱매수배[0] = x[row, 8];
            t.Api.틱매도배[0] = x[row, 9];

            t.Post.종누천 = (int)(t.Api.전일종가 * 거래량 / g.천만원);
        }

        private static void PostProcessStock(StockData t)
        {
            var x = t.Api.x;

            bool isIndex = (t.Kind == StockData.InstrumentKind.Index)
                           || g.StockManager?.IndexList?.Contains(t.Stock) == true;

            if (!isIndex && t.Api.nrow >= 2)
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
        }
    }






    public static class UniversePipeline
    {
        public static void EnrichAndPrune(Dictionary<string, StockData> tmp)
        {
            // TODO: 절친/통계가 어디에 저장되는지에 맞춰
            // - stock별로 필요한 값 채움
            // - 없거나 파싱 실패하면 tmp.Remove(stock)

            TryAttachStatistics(tmp);  // 내부에서 tmp.Remove(...)까지 처리
            TryAttachCorrelation(tmp);
        }

        public static void TryAttachStatistics(Dictionary<string, StockData> tmp)
        {
            if (tmp == null || tmp.Count == 0) 
                return;

            var path = @"C:\BJS\data work\통계.txt";
            if (!File.Exists(path)) return;

            IEnumerable<string> lines;
            try { lines = File.ReadLines(path); }
            catch (Exception ex) { Trace.TraceError("TryAttachStatistics: 파일 열기 실패: " + ex); return; }

            var rxNum = new Regex(@"[+-]?\d+(?:\.\d+)?", RegexOptions.Compiled);

            bool TryParseNum(string s, out double v)
            {
                v = 0;
                if (string.IsNullOrWhiteSpace(s)) return false;
                s = s.Replace(",", "");
                return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v);
            }

            bool DevOK(double x) => x > 1e-9 && !double.IsNaN(x) && !double.IsInfinity(x);

            bool TryParseStockLine(string line, out string stock, out string rest)
            {
                stock = null; rest = null;
                var m = rxNum.Match(line);
                if (!m.Success) { stock = line.Trim(); rest = ""; return stock.Length > 0; }
                int p = m.Index;
                stock = line.Substring(0, p).TrimEnd();
                rest = line.Substring(p);
                return stock.Length > 0;
            }

            void AppendNums(string line, List<double> nums)
            {
                foreach (Match m in rxNum.Matches(line))
                    if (TryParseNum(m.Value, out var v)) nums.Add(v);
            }

            string curStock = null;
            var numsBuf = new List<double>(32);
            var attachedOk = new HashSet<string>();

            void Reset() { curStock = null; numsBuf.Clear(); }

            void Flush()
            {
                if (curStock == null || numsBuf.Count != 29) 
                    return;

                if (!tmp.TryGetValue(curStock, out var sd) || sd == null) { Reset(); return; }

                int prevClose = 0;
                if (sd.Api != null) { try { prevClose = (int)sd.Api.전일종가; } catch { } }
                if (prevClose <= 0) { try { prevClose = FileIn.read_전일종가(curStock); } catch { prevClose = 0; } }
                if (prevClose <= 0) { tmp.Remove(curStock); Reset(); return; }

                if (sd.Api == null) sd.Api = new ApiData();
                if (sd.Post == null) sd.Post = new PostData();
                if (sd.Statistics == null) sd.Statistics = new StatisticsData();
                if (sd.Pass == null) sd.Pass = new PricePassData();

                sd.Api.전일종가 = prevClose;

                int I(int i) => (int)Math.Round(numsBuf[i]);

                try
                {
                    sd.Pass.Month = (int)((I(0) - prevClose) * 10000.0 / prevClose);
                    sd.Pass.Quarter = (int)((I(1) - prevClose) * 10000.0 / prevClose);
                    sd.Pass.Half = (int)((I(2) - prevClose) * 10000.0 / prevClose);
                    sd.Pass.Year = (int)((I(3) - prevClose) * 10000.0 / prevClose);

                    var st = sd.Statistics;
                    st.푀분_count = I(4);

                    int idx = 5;
                    st.푀분_avr = numsBuf[idx++]; st.푀분_dev = numsBuf[idx++];
                    st.거분_avr = numsBuf[idx++]; st.거분_dev = numsBuf[idx++];
                    st.배차_avr = numsBuf[idx++]; st.배차_dev = numsBuf[idx++];
                    st.배합_avr = numsBuf[idx++]; st.배합_dev = numsBuf[idx++];

                    st.종누_avr = numsBuf[idx++]; st.종누_dev = numsBuf[idx++];
                    st.푀누_avr = numsBuf[idx++]; st.푀누_dev = numsBuf[idx++];
                    st.기누_avr = numsBuf[idx++]; st.기누_dev = numsBuf[idx++];

                    st.매도1호가잔량_avr = numsBuf[idx++]; st.매도1호가잔량_dev = numsBuf[idx++];
                    st.매수1호가잔량_avr = numsBuf[idx++]; st.매수1호가잔량_dev = numsBuf[idx++];
                    st.총매도호가잔량_avr = numsBuf[idx++]; st.총매도호가잔량_dev = numsBuf[idx++];
                    st.총매수호가잔량_avr = numsBuf[idx++]; st.총매수호가잔량_dev = numsBuf[idx++];

                    // 20260510 eruption ref
                    st.거분_top5 = numsBuf[idx++];
                    st.푀분_top5 = numsBuf[idx++];

                    if (!(DevOK(st.푀누_dev) && DevOK(st.종누_dev))) throw new Exception("dev invalid");
                }
                catch
                {
                    tmp.Remove(curStock);
                    Reset();
                    return;
                }

                attachedOk.Add(curStock);
                Reset();
            }

            foreach (var raw in lines)
            {
                var line = (raw ?? "").Trim();
                if (line.Length == 0) continue;

                bool numStart = char.IsDigit(line[0]) || line[0] == '-' || line[0] == '+';

                if (!numStart)
                {
                    Flush();

                    if (!TryParseStockLine(line, out var stock, out var rest)) 
                        continue;

                    curStock = stock;
                    numsBuf.Clear();
                    if (!string.IsNullOrWhiteSpace(rest)) 
                        AppendNums(rest, numsBuf);
                    Flush();
                }
                else
                {
                    if (curStock == null) continue;
                    AppendNums(line, numsBuf);
                    Flush();
                }
            }

            Flush();

            // ✅ 파일에 없었거나 / attach 실패한 종목은 attachedOk에 없음 → prune
            foreach (var k in tmp.Keys.ToList())
                if (!attachedOk.Contains(k) && !k.Contains("KODEX"))
                    tmp.Remove(k);
        }

        public static void TryAttachCorrelation(Dictionary<string, StockData> tmp)
        {
            if (tmp == null || tmp.Count == 0) return;

            var path = @"C:\BJS\data work\Correlation.txt";
            if (!File.Exists(path)) return;

            string[] lines;
            try { lines = File.ReadAllLines(path, Encoding.Default); }
            catch (Exception ex) { Trace.TraceError("TryAttachJeolchin: 파일 열기 실패: " + ex); return; }

            StockData cur = null;
            bool hasCur = false;
            var seenBase = new HashSet<string>();

            foreach (var raw in lines)
            {
                var line = (raw ?? "").Trim();
                if (line.Length == 0) continue;

                var words = line.Split('\t');
                if (words.Length == 0) continue;

                // ✅ 기준 종목 라인 (종목명만)
                if (words.Length == 1)
                {
                    var key = words[0].Trim();
                    if (key.Length == 0) { hasCur = false; cur = null; continue; }

                    seenBase.Add(key);

                    if (tmp.TryGetValue(key, out cur) && cur != null)
                    {
                        hasCur = true;
                        if (cur.Misc == null) 
                            cur.Misc = new MiscData();
                        else cur.Misc.Corr.Clear(); // ✅ 매번 새로(원치 않으면 이 줄 삭제)
                    }
                    else
                    {
                        hasCur = false;
                        cur = null;
                    }
                    continue;
                }

                // ✅ peer 라인: "0.888\t삼성전자우"
                if (!hasCur || cur == null) continue;

                if (!double.TryParse(words[0].Trim(),
                        NumberStyles.Float | NumberStyles.AllowThousands,
                        CultureInfo.InvariantCulture, out var rho))
                    continue;

                var peerName = (words.Length >= 2) ? words[1].Trim() : "";
                if (peerName.Length == 0) continue;

                // peer가 tmp에 없으면 저장 안 함
                if (!tmp.TryGetValue(peerName, out var peer) || peer == null) continue;

                if (cur.Misc.Corr.Count < 20)
                    cur.Misc.Corr[peer.Stock] = rho;
            }

            foreach (var k in tmp.Keys.ToList())
            {
                if (!seenBase.Contains(k) &&
                !k.Contains("KODEX"))
                {
                    tmp.Remove(k);
                }
            }
        }
    }
}

