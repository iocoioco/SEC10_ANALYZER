using New_Tradegy.Library.Deals;
using New_Tradegy.Library.Listeners;
using New_Tradegy.Library.Models;
using New_Tradegy.Library.UI;
using New_Tradegy.Library.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static New_Tradegy.Library.Deals.QuickTradePopup;
using static New_Tradegy.Library.PostProcessing.ProgramImpulseDetector;
using static OpenQA.Selenium.BiDi.Modules.Script.LocalValue;
using static OpenQA.Selenium.BiDi.Modules.Script.RemoteValue;

namespace New_Tradegy.Library.PostProcessing
{
    public static class ProgramImpulseDetector
    {
        public struct Stat
        {
            public double Mean;
            public double Std;
            public Stat(double mean, double std) { Mean = mean; Std = std; }
            public double Z(double x) => (Std <= 1e-9) ? 0.0 : (x - Mean) / Std;
        }

        public sealed class Param
        {
            // zscore 기준(소프트 필터)
            public double Z_Pro = 1.5;
            public double Z_Money = 1.5;
            public double Z_Sum = 1.5;
            public double Z_Diff = 1.5;

            // 하드 필터
            public double DominanceMin = 0.20;
            public int DiffAbsMin = 15;
            public int SumAbsMin = 20;
            public double ProPctMin = 15.0;
            public int PriceDeltaMin = 0;

            // 소프트 3중2
            public int SoftNeedCount = 2;

            // sum drop penalty
            public double SumDropRatio = 0.90;   // 10% drop
            public double SumDropPenalty = 1.0;

            // score weights
            public double W_Pro = 1.5;
            public double W_Diff = 1.0;
            public double W_Money = 1.0;
            public double W_Dom = 0.5;


            // ⭐ 여기 추가
            public double LiqLogLow = 7.5;
            public double LiqLogHigh = 9.5;
            public double LiqScaleMin = 0.35;


        }

        public struct ImpulseSignal
        {
            public string Symbol;
            public double Score;
            public string Why;
            public int Tick;

            public ImpulseSignal(string symbol, double score, string why, int tick)
            {
                Symbol = symbol;
                Score = score;
                Why = why;
                Tick = tick;
            }
        }

        public static void GetStatsFromData(
            StockData data,
            out Stat proStat, out Stat moneyStat, out Stat diffStat, out Stat sumStat)
        {
            proStat = new Stat(data.Statistics.푀분_avr, data.Statistics.푀분_dev);
            moneyStat = new Stat(data.Statistics.거분_avr, data.Statistics.거분_dev);
            diffStat = new Stat(data.Statistics.배차_avr, data.Statistics.배차_dev);
            sumStat = new Stat(data.Statistics.배합_avr, data.Statistics.배합_dev);
        }

        static double SafeZ(Stat stat, double v)
        {
            double z = stat.Z(v);
            return (double.IsNaN(z) || double.IsInfinity(z)) ? 0.0 : z;
        }

        //        1. 거래정지 제외
        //2. +27% 이상 / -27% 이하 제외
        //3. 장중 호가 유동성 통과
        //4. 최근20일 정상범위 상위5 평균 ref 계산
        //5. 현재 10초/분 값이 ref 대비 충분히 큼
        //6. 프로%, 배수차/배수합, 가격반응 확인
        //7. score = 1~5, why 작성

        //double refMoney = Math.Max(p.MinMoneyFloor, data.Statistics.Top5AvgMoney);
        //double refPro = Math.Max(p.MinProFloor, data.Statistics.Top5AvgProBuy);

        //double refSum = Math.Max(p.MinSumFloor, data.Statistics.Top5AvgMultSum);


        //    if (post.분10거래천Rank > 3 &&
        //post.분10프로천Rank > 3 &&

        //배수합rank > 10 &&
        //배수차rank > 10 )
        //return false;


// 시초 프로 매수액 증가하면 지수 자체 상승 할 수 있다 특히, 삼전 등 주요 종목에 돈이 급하게 들어오면 지수 상승 가능    
// 시초 갭하
// 장중 급락
// 시초 종목 프로, 배수, 대량 변경경
// 같은 섹터 종목 다수 상위
// 시초 기관 계속 매수
// 지수 급상 및 지속 가능
// 배수차 크고 배수합 크고 dominance of 배수차
// dominance of 포로매수수
// 급상 모멘텀 종목 상승(하락 바로 하차)
// 3초 Burst3초 Acceleration 틱 이용 
// 섹터 내 한 종목 폭등시 전파 가능성 있음
        // Δ배수차 / Δt
        // Δ배수합 / Δt
        // Δ프로천 / Δt
        // Δ가격 / Δt
        // 10초 거래액 / 프로금액 rank 계산
        // 10초 거래액 / 프로금액 rank 계산
        public static bool CanEnter(
    StockData data, PostData post, PostData prev, Param p,
    out double score, out string why)
        {
            score = 0;
            why = "";

            if (data?.Statistics == null || post == null)
                return false;

            var st = data.Statistics;

            double money10 = post.분10거래천;     // 10초 거래액, 천만원
            double major10 = post.분10프로천;     // 10초 푀/프로 계열, 천만원
            double proPct10 = post.분10푀퍼;
            double px10 = post.분10가격차;

            // 1) 기본 가격 반응
            if (px10 < p.PriceDeltaMin) // 가격이 하락하지 않는다 0 이상
                return false;

            // 2) 프로/푀 기여도
            if (proPct10 < p.ProPctMin) // 15% 이상 프로가 매수
                return false;

            // 3) 최근 10초 rank 조건
            if (post.분10거래천Rank > g.v.RankLimit) // 분10거래천Rank 분10프로천Rank 5위안 <5 조정 가능>
                return false;

            if (post.분10프로천Rank > g.v.RankLimit)  // 분10거래천Rank 분10프로천Rank 5위안 <5 조정 가능>
                return false;

            // 4) 통계 기준값 확인
            if (st.거분_top5 <= 0 || st.푀분_top5 <= 0) // 거분 5위값, 푀분 5위 값
                return false;

            // 5) 10초 값을 분속도로 환산
            double money10AsMin = money10 * 6.0;
            double major10AsMin = major10 * 6.0;

            double moneyRatio = money10AsMin / st.거분_top5;
            double majorRatio = major10AsMin / st.푀분_top5;

            // 6) eruption ratio 통과,
            // g.v.EruptionRatio = 5 default
            if (moneyRatio < g.v.EruptionRatio) // 10초 거래액 * 6 > 지난 20일 top 5 거래평균액(분) * g.v.EruptionRatio
                return false;

            // 7) eruption 강도 기준
            if (majorRatio < g.v.EruptionRatio) // 10초 프로액 * 6 > 지난 20일 top 5 프로평균액(분) * g.v.EruptionRatio
                return false;

            // 7) score는 우선 등급 개념으로 단순화
            score = 1;

            if (moneyRatio >= g.v.EruptionRatio * 1.5 &&
                majorRatio >= g.v.EruptionRatio * 1.5)
                score = 2;

            if (moneyRatio >= g.v.EruptionRatio * 2.0 &&
                majorRatio >= g.v.EruptionRatio * 2.0)
                score = 3;

            if (moneyRatio >= g.v.EruptionRatio * 3.0 &&
                majorRatio >= g.v.EruptionRatio * 3.0)
                score = 5;

            why =
                $"Erpt m={moneyRatio:F2} f={majorRatio:F2} " +
                $"rM={post.분10거래천Rank} rF={post.분10프로천Rank} " +
                $"px={px10:F0} f%={proPct10:F0}";

            return true;
        }

        public static bool CanEnter_20260510(
            StockData data, PostData post, PostData prev, Param p,
            out double score, out string why)
        {
            score = 0;
            why = "";
            if (data?.Statistics == null || post == null) return false;

            GetStatsFromData(data, out var proStat, out var moneyStat, out var diffStat, out var sumStat);

            double pro10 = post.분10프로천;
            double money10 = post.분10거래천;
            double diff10 = post.분10배수차;
            double sum10 = post.분10배수합;
            double proPct10 = post.분10푀퍼;
            double px10 = post.분10가격차;

            // 1) 하드 필터 (p 사용)

            if (px10 < p.PriceDeltaMin) return false; // 0

            if (sum10 < p.SumAbsMin) return false; // 20
            if (proPct10 < p.ProPctMin) return false; // 25


            // 1) dominance (기존)
            double dominance = diff10 / sum10;
            if (dominance < p.DominanceMin) return false; // 0.3

            // 2) zscore (기존)
            double zPro = SafeZ(proStat, pro10);
            double zMoney = SafeZ(moneyStat, money10);
            double zDiff = SafeZ(diffStat, diff10);
            double zSum = SafeZ(sumStat, sum10);

            // 3) 소프트 (3중2) (기존)
            bool proOk = (zPro >= p.Z_Pro); // 1.5
            bool diffOk = (zDiff >= p.Z_Diff); // 1.5
            bool actOk = (zMoney >= p.Z_Money); // 1.5

            int cnt = (proOk ? 1 : 0) + (diffOk ? 1 : 0) + (actOk ? 1 : 0);
            if (cnt < p.SoftNeedCount) return false;



            // 4.5) ✅ 유동성 스케일: 낮으면 0.3~0.6배로 눌리고, 높으면 1.0배에 가까워짐
            // - log 스케일이라 "꼬맹이 z 폭발"을 눌러주면서 대형도 살아남
            double liqScale = 1.0;
            {
                // 거래대금    log10
                // 1천만         7
                // 1억           8
                // 10억          9
                // 예: p.LiqLogLow=7.5(≈3천만), p.LiqLogHigh=8.47(≈3억)

                double x = Math.Log10(money10 * 10_000_000 + 1.0);
                double t = (x - p.LiqLogLow) / (p.LiqLogHigh - p.LiqLogLow); // 9.5 7.5 
                t = Math.Max(0.0, Math.Min(1.0, t));
                liqScale = p.LiqScaleMin + (1.0 - p.LiqScaleMin) * t; // 0.35
            }

            // 5) score (기존 + liqScale 곱)
            dominance = Math.Max(0.0, Math.Min(1.0, dominance));

            score =
                (p.W_Pro * zPro + // 1.5
                 p.W_Diff * zDiff + // 1.5
                 p.W_Money * zMoney + // 1.0
                 p.W_Dom * dominance) // 0.8
                * liqScale;

            // 6) why
            var sb = new StringBuilder();
            sb.Append($"IMP px={px10:F0} diff={diff10:F0} sum={sum10:F0} dom={dominance:F2} ");
            sb.Append($"zP={zPro:F1} zM={zMoney:F1} zS={zSum:F1} zD={zDiff:F1} | ");
            if (proOk) sb.Append("pro ");
            if (diffOk) sb.Append("diff ");
            if (actOk) sb.Append("act ");

            why = sb.ToString().TrimEnd();

            return true;
        }
    }

    public sealed class PendingBestGate
    {
        private ProgramImpulseDetector.ImpulseSignal? _pending;

        // ====== Popup Gate ======
        private bool _popupActive = false;
        private string _popupSymbol = null;
        private bool _isImpulseActive = false;


        // ====== Impulse Pending ======

        public bool HasPending => _pending.HasValue;

        public ProgramImpulseDetector.ImpulseSignal Pending
        {
            get
            {
                if (!_pending.HasValue)
                    throw new InvalidOperationException("No pending signal.");
                return _pending.Value;
            }
        }

        public bool Offer(ProgramImpulseDetector.ImpulseSignal s, out string oldSymbol)
        {
            oldSymbol = null;

            if (!_pending.HasValue)
            {
                _pending = s;
                return true;
            }

            var cur = _pending.Value;

            if (s.Score <= cur.Score)
                return false;

            oldSymbol = cur.Symbol;
            _pending = s;
            return true;
        }

        public bool PromoteNextToActive(out ProgramImpulseDetector.ImpulseSignal activated)
        {
            activated = default(ProgramImpulseDetector.ImpulseSignal);

            if (!_pending.HasValue)
                return false;

            activated = _pending.Value;
            _pending = null;
            return true;
        }

        public void Clear()
        {
            _pending = null;
        }
    }

    public sealed class ImpulseRunner
    {
        private readonly ProgramImpulseDetector.Param _p;
        private readonly PendingBestGate _gate;
        private readonly Dictionary<string, int> _rejectUntil = new Dictionary<string, int>();
        private const int RejectCooldownMs = 60_000; // 1분

        public ImpulseRunner(PendingBestGate gate)
        {
            _gate = gate;
            _p = new ProgramImpulseDetector.Param();
        }

        public void OnDownloadedTick()
        {
            var symbols = BuildCandidateSymbols();
            if (symbols == null || symbols.Count == 0)
                return;

            // =====================================================
            // 20260510
            // 10초 거래액 / 프로금액 rank 계산
            // =====================================================

            var stocks = symbols
                .Select(sym => g.StockRepo.TryGetDataOrNull(sym))
                .Where(d => d?.Post != null)
                .ToList();

            var moneyRanked = stocks
                .OrderByDescending(d => d.Post.분10거래천)
                .ToList();

            for (int i = 0; i < moneyRanked.Count; i++)
                moneyRanked[i].Post.분10거래천Rank = i + 1;

            var proRanked = stocks
                .OrderByDescending(d => d.Post.분10프로천)
                .ToList();

            for (int i = 0; i < proRanked.Count; i++)
                proRanked[i].Post.분10프로천Rank = i + 1;

            // =====================================================

            int now = Environment.TickCount;

            foreach (var sym in symbols)
            {
                if (string.IsNullOrEmpty(sym))
                    continue;

                if (_rejectUntil.TryGetValue(sym, out int until) && Environment.TickCount < until)
                    continue;

                var data = g.StockRepo.TryGetDataOrNull(sym);
                if (data?.Statistics == null)
                    continue;

                var post = data.Post;
                if (post == null)
                    continue;

                int hogaLimit = 2 * g.v.호가거래액이상_백만원;

                if (post.매도호가거래액_백만원 + post.매수호가거래액_백만원 < hogaLimit)
                    continue;

                if (!ProgramImpulseDetector.CanEnter(
                        data,
                        post,
                        null,
                        _p,
                        out var score,
                        out var why))
                    continue;

                // ✅ 1 이상만 실제 eruption 처리
                if (score < 1)
                    continue;

                TryConsume(new ProgramImpulseDetector.ImpulseSignal(
                    sym,
                    score,
                    why,
                    now));

                return;
            }
        }

        private static async Task<int> WaitHogaReadyAsync(string stock, int timeoutMs = 700)
        {
            if (string.IsNullOrWhiteSpace(stock))
                return 0;

            var sw = System.Diagnostics.Stopwatch.StartNew();

            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                var grid = g.BookBidManager.FindGrid(stock);

                if (grid != null && !grid.IsDisposed)
                {
                    int price = TryReadBuyPriceFromGrid(grid);

                    if (price > 0)
                        return price;
                }

                await Task.Delay(50);
            }

            return 0;
        }

        private static int TryReadBuyPriceFromGrid(DataGridView grid)
        {
            if (grid == null || grid.IsDisposed)
                return 0;

            if (grid.InvokeRequired)
            {
                try
                {
                    return (int)grid.Invoke(new Func<int>(() => TryReadBuyPriceFromGrid(grid)));
                }
                catch
                {
                    return 0;
                }
            }

            try
            {
                // TODO: 여기만 친구 호가창 매도1호가 위치에 맞춰 수정
                // 예시: 매도1호가 셀
                var v = grid.Rows[4].Cells[1].Value;

                if (v == null)
                    return 0;

                string s = v.ToString().Replace(",", "").Trim();

                if (int.TryParse(s, out int price) && price > 0)
                    return price;
            }
            catch
            {
                return 0;
            }

            return 0;
        }

        private List<string> BuildCandidateSymbols()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var data in g.StockRepo.AllGeneralStocks)
            {
                if (data?.Post == null) continue;

                var post = data.Post;

                // 초저비용 Early Reject (필요시 완화)
                if (post.분10배수차 <= 0) continue;
                if (post.분10프로천 <= 0) continue;

                set.Add(data.Stock);
            }

            return new List<string>(set);
        }

        // Consume 단계
        private bool _isImpulseActive = false;

        private async void TryConsume(ProgramImpulseDetector.ImpulseSignal? signal)
        {
            if (signal == null)
                return;

            if (_isImpulseActive)
                return;

            var s = signal.Value;

            _isImpulseActive = true;

            try
            {
                string sym = s.Symbol;
                if (string.IsNullOrEmpty(sym))
                    return;

                var data = g.StockRepo.TryGetDataOrNull(sym);
                if (data == null)
                    return;

                // 1) 관심/Active에 올림
                if (!g.StockManager.InterestedWithBidList.Contains(sym))
                    g.StockManager.InterestedWithBidList.Add(sym);

                g.StockManager.Active = sym;

                // 2) 호가창/메인차트 표시
                // 6) 차트 : 메인 & 서브
                PostProcessor.ManageChart1Invoke();

                // 3) 소리/flash
                // PlayImpulseSound();
                // FlashImpulseChart(sym, s.Why);

                // 3) 주문 가격/수량 산출
                // 우선 현재가 기준. 나중에 필요하면 최우선매도호가로 교체.

                int price;
                int hogaPrice = await WaitHogaReadyAsync(sym, 700);

                if (hogaPrice > 0)
                    price = hogaPrice;
                else
                    price = 0;




                int qty = 0;

                if (price > 0)
                {
                    qty = g.일회거래액 * 10000 / price;

                    if (qty <= 0)
                        qty = 1;
                }

                // 4) 매수 이유
                string 매수이유 = s.Why;
                if (string.IsNullOrWhiteSpace(매수이유))
                    매수이유 = BookBidGeneratorStock.BuildSignalText(data, data.Api);

                매수이유 = null;


                string hudText;

                if (price > 0)
                    hudText = $"P{price} Q{qty}";
                else
                    hudText = "WAIT";

    //            AreaHud.ShowHud(
    //g.ChartManager.Chart1,
    //sym,
    //hudText,
    //durationMs: 3000,
    //fontSize: 12f,
    //foreColor: Color.Red,
    //cellOffsetX: 0);


                // 5) 클릭 매수와 동일한 popup
                //var kind = await QuickPopupHelper.ConfirmByQuickPopupAsync(
                //    isBuy: true,
                //    stock: sym,
                //    price: price,
                //    qty: qty,
                //    reason: 매수이유,
                //    commitSource: "impulse"
                //);

                //if (kind == PopupResultKind.Confirm)
                //{
                //    //DealManager.DealExec("매수", sym, price, qty, "01");
                //}
                //else if (kind == PopupResultKind.CancelRemove)
                //{
                //    g.StockManager.RemoveInterestedWithBid(sym);
                //    _rejectUntil[sym] = Environment.TickCount + RejectCooldownMs;
                //}
                //else if (kind == PopupResultKind.CancelKeep)
                //{
                //    _rejectUntil[sym] = Environment.TickCount + RejectCooldownMs;
                //}
            }
            finally
            {
                _isImpulseActive = false;
            }
        }

        public async void TestImpulseHud(string sym)
        {
            int price = await WaitHogaReadyAsync(sym, 700);

            int qty = 0;
            if (price > 0)
                qty = g.일회거래액 * 10000 / price;

            string hudText = price > 0 ? $"P{price} Q{qty}" : "WAIT";

            AreaHud.ShowHud(
    g.ChartManager.Chart1,
    "삼성전자",
    "TEST",
    durationMs: 5000,
    fontSize: 14f,
    foreColor: Color.Red,
    cellOffsetX: 0);



            //AreaHud.ShowHud(
            //    g.ChartManager.Chart1,
            //    sym,
            //    hudText,
            //    durationMs: 3000,
            //    fontSize: 8f,
            //    foreColor: price > 0 ? Color.White : Color.Yellow,
            //    cellOffsetX: 2);
        }
    }
    
}



