using New_Tradegy.Library.Models;
using New_Tradegy.Library.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using static New_Tradegy.Library.PostProcessing.IndexScoreEngine;

namespace New_Tradegy.Library.PostProcessing
{
    public static class IndexMarket
    {
        // ── Sigma 창/매니저 (PostMarket에서 이관) ─────────────────────────────
        private static readonly Dictionary<int, int> spans = new Dictionary<int, int> {
            { 10_000, 60 },   // 10초 시리즈 최근 60개
            { 20_000, 60 },
            { 30_000, 60 },
        };

        // IndexMarket.cs(클래스 필드)
        private static readonly object _saveLock = new object();
        private static DateTime _nextSaveUtc = DateTime.MinValue;  // 다음 저장 허용 시각
        private static bool _dirty = false;

        private static void TrySaveIfDue()
        {
            if (!_dirty) return;

            var now = DateTime.UtcNow;
            if (now < _nextSaveUtc) return;

            lock (_saveLock)
            {
                if (!_dirty) return; // 다른 스레드가 이미 저장했을 수 있음
                                     // ★ 실제 저장 주체 호출 (예: sigmaManager.Save())
                g.Sigma.Save();


                _dirty = false;
                _nextSaveUtc = now.AddSeconds(45);   // 저장 간격: 30~60초 권장
            }
        }


        private struct Totals
        {
            public double ProgTh;     // 프로(천) 합계
            public double ForeTh;     // 외인(천) 합계
            public double DealTh;     // 거래(천) 합계
            public double BuyMult;    // 매수배 합계
            public double SellMult;   // 매도배 합계
            public double DepthAvg;   // 깊이 평균 합
            public double DepthCnt;
        }

        // ====== 외부 진입점 ======
        public static void IndexMarketSec10_20_30_Real()
        {
            ProcessMarket("Kospi", g.kospi_mixed, "KODEX 레버리지");
            ProcessMarket("Kosdaq", g.kosdaq_mixed, "KODEX 코스닥150레버리지");

            if (!g.test)
            {
                // reset policy가 있으면 유지(없으면 삭제)
                // if (g.MarketeyeCount % 10 == 1) g.Sigma.ApplyResetPolicy(DateTime.Now);

                // TrySaveIfDue(); // (선택) SigmaState_Current 저장을 유지할 때만
            }
        }


        private static void ProcessMarket(string marketName, dynamic mixed, string etfName)
        {
            var repo = g.StockRepo;
            if (mixed?.stocks == null || repo == null) return;

            var intervals = new[] { 10_000, 20_000, 30_000 };
            int J = intervals.Length;

            // ✅ 1) 1-pass 저장 버퍼
            int N = mixed.stocks.Count;

            // [j] 전체 분거래천 합 (분모)
            double[] totalDealTh = new double[J];

            // [i,j] 종목별 분거래천(분자) + 원시 지표 저장
            double[,] dealTh = new double[N, J];
            double[,] progTh = new double[N, J];
            double[,] foreTh = new double[N, J];
            double[,] buyMultRaw = new double[N, J];
            double[,] sellMultRaw = new double[N, J];

            // ⬇ 누적(현재시점) 합계 버킷 (프/외) - 너 기존 유지
            double cumProgTh = 0;
            double cumForeTh = 0;

            for (int idx = 0; idx < N; idx++)
            {
                var stock = mixed.stocks[idx];
                var data = repo.TryGetDataOrNull(stock);
                var api = data?.Api;

                if (api == null || api.틱의시간 == null || api.틱의시간.Length == 0) continue;
                if (api.틱수누량 == null || api.틱도누량 == null || api.틱프누량 == null ||
                    api.틱외누량 == null || api.틱의가격 == null) continue;

                if (data.Post != null)
                {
                    cumProgTh += data.Post.프누천;
                    cumForeTh += data.Post.외누천;
                }

                var selected = FindSelectedIndices(api.틱의시간, intervals);
                if (selected == null) continue;

                // 기존 고정 weight (지수 비중 등) - "섞기"용으로만 남김
                double wIndex = 1.0;
                if (mixed.weights != null && idx < mixed.weights.Count)
                    wIndex = mixed.weights[idx];

                for (int j = 0; j < J; j++)
                {
                    int sel = selected[j];
                    if (sel < 0) continue;

                    if (sel >= api.틱의시간.Length ||
                        sel >= api.틱수누량.Length || sel >= api.틱도누량.Length ||
                        sel >= api.틱프누량.Length || sel >= api.틱외누량.Length ||
                        sel >= api.틱의가격.Length) continue;

                    double elapsed = TimeUtils.ElapsedMillisecondsDouble(api.틱의시간[sel], api.틱의시간[0]);
                    if (elapsed <= 0 || elapsed > 21_600_000) continue;

                    double dBuy = api.틱수누량[0] - api.틱수누량[sel];
                    double dSell = api.틱도누량[0] - api.틱도누량[sel];
                    double dProg = api.틱프누량[0] - api.틱프누량[sel];
                    double dFore = api.틱외누량[0] - api.틱외누량[sel];
                    double dAmount = dBuy + dSell;
                    if (dBuy < 0 || dSell < 0 || dAmount < 0) continue; // 누적 리셋 방어

                    // ✅ (권장) 거래대금은 전일종가보다 "현재가"가 더 낫다.
                    // double px = api.전일종가;
                    double px = api.틱의가격[0] > 0 ? api.틱의가격[0] : api.전일종가;

                    // 분당 환산: (주식수 * 가격) / 경과ms * 60_000
                    double moneyFactor = px / g.천만원 / elapsed * 60_000.0;

                    // 기존 multipleFactor는 유지(원시 값)
                    double avgVol = (data.Statistics?.일평균거래량 > 0) ? (double)data.Statistics.일평균거래량 : 0.0;
                    if (avgVol <= 0) continue;

                    double multipleFactor = (60_000.0 / elapsed) * (380.0 / avgVol) * 10.0;

                    double vProgTh = dProg * moneyFactor;
                    double vForeTh = dFore * moneyFactor;
                    double vDealTh = dAmount * moneyFactor;

                    // 원시 배수(지수 weight는 일단 raw에 포함시키지 말고, 2-pass에서 섞자)
                    double vBuyMultRaw = dBuy * multipleFactor;
                    double vSellMultRaw = dSell * multipleFactor;

                    dealTh[idx, j] = vDealTh;
                    progTh[idx, j] = vProgTh;
                    foreTh[idx, j] = vForeTh;

                    // ✅ raw 저장 (2-pass에서 wMoney로 가중)
                    buyMultRaw[idx, j] = vBuyMultRaw;
                    sellMultRaw[idx, j] = vSellMultRaw;

                    // 분모 누적
                    totalDealTh[j] += vDealTh;
                }
            }

            // ✅ 2) 2-pass: 분거래 비중으로 가중합
            var totals = new Totals[J];
            for (int j = 0; j < J; j++) totals[j] = new Totals();

            // 지수 weight와 “거래대금 비중”을 섞을지 선택 (0~1)
            // 1.0이면 전부 거래대금 비중, 0이면 전부 기존 지수 weight
            const double alphaMoney = 1.0;

            for (int idx = 0; idx < N; idx++)
            {
                double wIndex = 1.0;
                if (mixed.weights != null && idx < mixed.weights.Count)
                    wIndex = mixed.weights[idx];

                for (int j = 0; j < J; j++)
                {
                    double denom = totalDealTh[j];
                    if (denom <= 0) continue;

                    double wMoney = dealTh[idx, j] / denom; // ✅ 원하는 "분당거래액/분당전체거래액" 비중

                    // 섞기(선택): wFinal = alpha*wMoney + (1-alpha)*wIndexNormalized? 가 이상적이지만
                    // wIndex는 보통 합이 1이 아닐 수 있으니, 여기서는 곱 방식이 가장 안전.
                    double wFinal = (alphaMoney >= 1.0)
                        ? wMoney
                        : (alphaMoney <= 0.0)
                            ? wIndex
                            : (wMoney * alphaMoney + wIndex * (1.0 - alphaMoney));

                    totals[j].DealTh += dealTh[idx, j];
                    totals[j].ProgTh += progTh[idx, j];
                    totals[j].ForeTh += foreTh[idx, j];

                    // ✅ 배수는 거래대금 비중으로 “가격 동조” 강화
                    totals[j].BuyMult += buyMultRaw[idx, j] * wFinal;
                    totals[j].SellMult += sellMultRaw[idx, j] * wFinal;
                }
            }

            // cumProgTh/cumForeTh는 너 로직대로 별도로 기록/표시하면 됨
            // totals[]는 이후 snapshot 저장/표시에 사용

            // ---------- ETF post로 주입 ----------
            var etfData = repo.TryGetDataOrNull(etfName);
            var etfApi = etfData?.Api;
            var etfPost = etfData?.Post;
            if (etfApi == null || etfPost == null || etfApi.틱의시간 == null || etfApi.틱의시간.Length == 0 ||
                etfApi.틱의가격 == null) return;

            // ──────────────────────────────────────────────────────────────
            // 누적치 주입: 프누천/외누천(구성종목 합계), 기누천/개누천(다운로드 값 그대로)
            // 단위: 모두 "천만원 누적" (분당 환산 아님)
            // ──────────────────────────────────────────────────────────────
            const string ETF_KOSPI = "KODEX 레버리지";
            const string ETF_KOSDAQ = "KODEX 코스닥150레버리지";

            var mi = MajorIndex.Instance;
            bool isKospi = string.Equals(etfName, ETF_KOSPI, StringComparison.Ordinal);

            // 구성종목 합계(프로그램/외인) → 해당 ETF.Post에 저장
            etfPost.프누천 = cumProgTh;
            etfPost.외누천 = cumForeTh;

            // 기관/개인 누적(거래소 발표값) → 해당 ETF.Post에 저장
            etfPost.기누천 = isKospi ? mi.KospiInstitutionNetBuy : mi.KosdaqInstitutionNetBuy;
            etfPost.개누천 = isKospi ? mi.KospiRetailNetBuy : mi.KosdaqRetailNetBuy;

            // MajorIndex에도 동시 업데이트 (대시보드/다른 모듈 참조용)
            if (isKospi)
            {
                mi.KospiProgramNetBuy = (int)cumProgTh;
                mi.KospiForeignNetBuy = (int)cumForeTh;
            }
            else
            {
                mi.KosdaqProgramNetBuy = (int)cumProgTh;
                mi.KosdaqForeignNetBuy = (int)cumForeTh;
            }



            // (B) 델타(sec10/20/30): 기존 + 기관/개인/나스닥/K200 "차이" 반영
            var etfSel = FindSelectedIndices(etfApi.틱의시간, intervals);
            if (etfSel == null) return;

            for (int j = 0; j < intervals.Length; j++)
            {
                int sel = etfSel[j];
                int 분가격차 = 0;
                if (sel >= 0 && sel < etfApi.틱의가격.Length)
                {
                    double elapsed = TimeUtils.ElapsedMillisecondsDouble(etfApi.틱의시간[sel], etfApi.틱의시간[0]);
                    if (elapsed > 0 && elapsed <= 21_600_000)
                        분가격차 = (int)((etfApi.틱의가격[0] - etfApi.틱의가격[sel]) / elapsed * 60_000.0);
                }

                // ★ 구간 차이 계산(가드 포함)
                int Δ기관천 = 0, Δ개인천 = 0;
                double Δ나스닥 = 0.0, ΔK200 = 0.0;

                if (sel >= 0 &&
                    etfApi.틱기관천 != null && sel < etfApi.틱기관천.Length &&
                    etfApi.틱개인천 != null && sel < etfApi.틱개인천.Length)
                {
                    Δ기관천 = etfApi.틱기관천[0] - etfApi.틱기관천[sel];
                    Δ개인천 = etfApi.틱개인천[0] - etfApi.틱개인천[sel];
                }

                if (sel >= 0 &&
                    etfApi.틱나스닥 != null && sel < etfApi.틱나스닥.Length)
                {
                    Δ나스닥 = etfApi.틱나스닥[0] - etfApi.틱나스닥[sel];
                }

                if (sel >= 0 &&
                    etfApi.틱K200 != null && sel < etfApi.틱K200.Length)
                {
                    ΔK200 = etfApi.틱K200[0] - etfApi.틱K200[sel];
                }

                double 프퍼 = (totals[j].DealTh > 0) ? 100.0 * (double)totals[j].ProgTh / (double)totals[j].DealTh : 0.0;
                double 푀퍼 = (totals[j].DealTh > 0) ? 100.0 * (double)(totals[j].ProgTh + totals[j].ForeTh) / (double)totals[j].DealTh : 0.0;
                int 잔량평균 = (totals[j].DepthCnt > 0) ? (int)(totals[j].DepthAvg / totals[j].DepthCnt) : 0;
                int top1AvgValue = (totals[j].DepthCnt > 0) ? (int)(totals[j].DepthAvg / totals[j].DepthCnt) : 0;

                // ✅ Top3는 아직 누적값 없으면 우선 top1 값으로 채움 (나중에 totals에 Top3 누적 붙이면 여기만 바꾸면 끝)
                int top3AvgValue = top1AvgValue;

                // 아래 테이터가 제대로 들어오는 지 확인 
                switch (intervals[j])
                {
                    case 10_000:
                        etfPost.분10프로천 = (int)totals[j].ProgTh;
                        etfPost.분10외인천 = (int)totals[j].ForeTh;

                        // ★ 구간 차이 주입
                        etfPost.분10기관천 = Δ기관천;
                        etfPost.분10개인천 = Δ개인천;
                        etfPost.분10나스닥 = Δ나스닥;
                        etfPost.분10K200 = ΔK200;

                        etfPost.분10거래천 = (int)totals[j].DealTh;
                        etfPost.분10매수배 = (int)totals[j].BuyMult;
                        etfPost.분10매도배 = (int)totals[j].SellMult;
                        etfPost.분10배수차 = (int)(totals[j].BuyMult - totals[j].SellMult);
                        etfPost.분10배수합 = (int)(totals[j].BuyMult + totals[j].SellMult);

                        etfPost.분10가격차 = 분가격차;
                        etfPost.분10프퍼 = 프퍼;
                        etfPost.분10푀퍼 = 푀퍼;
                        etfPost.Sec10Top1BookAvgValue = top1AvgValue;
                        break;

                    case 20_000:
                        etfPost.분20프로천 = (int)totals[j].ProgTh;
                        etfPost.분20외인천 = (int)totals[j].ForeTh;

                        // ★ 구간 차이 주입
                        etfPost.분20기관천 = Δ기관천;
                        etfPost.분20개인천 = Δ개인천;
                        etfPost.분20나스닥 = Δ나스닥;
                        etfPost.분20K200 = ΔK200;

                        etfPost.분20거래천 = (int)totals[j].DealTh;
                        etfPost.분20매수배 = (int)totals[j].BuyMult;
                        etfPost.분20매도배 = (int)totals[j].SellMult;
                        etfPost.분20배수차 = (int)(totals[j].BuyMult - totals[j].SellMult);
                        etfPost.분20배수합 = (int)(totals[j].BuyMult + totals[j].SellMult);

                        etfPost.분20가격차 = 분가격차;
                        etfPost.분20프퍼 = 프퍼;
                        etfPost.분20푀퍼 = 푀퍼;
                        etfPost.Sec20Top1BookAvgValue = top1AvgValue;
                        break;

                    case 30_000:
                        etfPost.분30프로천 = (int)totals[j].ProgTh;
                        etfPost.분30외인천 = (int)totals[j].ForeTh;

                        // ★ 구간 차이 주입
                        etfPost.분30기관천 = Δ기관천;
                        etfPost.분30개인천 = Δ개인천;
                        etfPost.분30나스닥 = Δ나스닥;
                        etfPost.분30K200 = ΔK200;   // ← K200로 통일

                        etfPost.분30거래천 = (int)totals[j].DealTh;
                        etfPost.분30매수배 = (int)totals[j].BuyMult;
                        etfPost.분30매도배 = (int)totals[j].SellMult;
                        etfPost.분30배수차 = (int)(totals[j].BuyMult - totals[j].SellMult);
                        etfPost.분30배수합 = (int)(totals[j].BuyMult + totals[j].SellMult);

                        etfPost.분30가격차 = 분가격차;
                        etfPost.분30프퍼 = 프퍼;
                        etfPost.분30푀퍼 = 푀퍼;
                        etfPost.Sec30Top1BookAvgValue = top1AvgValue;
                        break;
                }

                // (A) 배수차/배수합 EMA 갱신 (10/20/30 모두)
                UpdateEmaDiffSum(marketName, etfPost);

                // (B) Sigma 누적: 10/20/30 계층 조건(%18/%36/%54)에서만 실행
                //     -> 실제로 누적이 일어난 경우에만 dirty=true
                bool updatedSigma = UpdateSigmaHierarchical(marketName, etfPost, g.MarketeyeCount);
                if (updatedSigma) _dirty = true;



                // (C) Score 계산/저장
                // (C) Score 계산/저장
                var res = IndexScoreEngine.Compute(marketName, etfPost, null);
                // ✅ 키 통일: marketName ("Kospi","Kosdaq")
                IndexScoreStore.Update(marketName, res);





                TrySaveSec10IfNeeded(marketName, etfName);
            }
        }


        private static void TrySaveSec10IfNeeded(string marketName, string etfName)
        {
            DateTime now = DateTime.Now;

            // 10초 bucket
            int bucket = (now.Hour * 3600 + now.Minute * 60 + now.Second) / 10;

            bool isKospi = marketName.Equals("Kospi", StringComparison.OrdinalIgnoreCase);
            bool isKosdaq = marketName.Equals("Kosdaq", StringComparison.OrdinalIgnoreCase);

            if (!isKospi && !isKosdaq)
                return;

            // 같은 10초 구간이면 저장 안 함
            if (isKospi)
            {
                if (bucket == Sec10Store.LastKospiBucket) return;
                Sec10Store.LastKospiBucket = bucket;
            }
            else
            {
                if (bucket == Sec10Store.LastKosdaqBucket) return;
                Sec10Store.LastKosdaqBucket = bucket;
            }

            int[] row = BuildSec10Row(now, etfName);
            if (row == null)
                return;

            if (isKospi)
                AddRowToArray(Sec10Store.Kospi, ref Sec10Store.KospiRow, row);
            else
                AddRowToArray(Sec10Store.Kosdaq, ref Sec10Store.KosdaqRow, row);
        }

        private static int[] BuildSec10Row(DateTime now, string etfName)
        {
            var repo = g.StockRepo;
            if (repo == null) return null;

            var etfData = repo.TryGetDataOrNull(etfName);
            if (etfData == null) return null;

            var etfApi = etfData.Api;
            var etfPost = etfData.Post;

            if (etfApi == null || etfPost == null) return null;

            int[] row = new int[Sec10Store.COLS];

            row[(int)Sec10Col.Time] = ToHHmmssfff(now);
            row[(int)Sec10Col.Etf] = SafeInt(etfApi.틱의가격[0]);
            row[(int)Sec10Col.Nq] = SafeInt(MajorIndex.Instance.NasdaqIndex * g.THOUSAND); // 셋쩨 자리까지 표현

            row[(int)Sec10Col.ProAcc] = SafeInt(etfPost.프누천 / 10); // 억원으로 자장
            row[(int)Sec10Col.ForAcc] = SafeInt(etfPost.외누천 / 10); // 억원으로 자장
            row[(int)Sec10Col.InstAcc] = SafeInt(etfPost.기누천 / 10); // 억원으로 자장
            row[(int)Sec10Col.IndiAcc] = SafeInt(etfPost.개누천 / 10); // 억원으로 자장

            row[(int)Sec10Col.Diff] = SafeInt(etfPost.분10배수차);
            row[(int)Sec10Col.Sum] = SafeInt(etfPost.분10배수합);

            return row;
        }
        private static int ToHHmmssfff(DateTime dt)
        {
            return dt.Hour * 10000000
                 + dt.Minute * 100000
                 + dt.Second * 1000
                 + dt.Millisecond;
        }

        private static int SafeInt(int v) => v;

        private static int SafeInt(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v))
                return 0;

            return (int)Math.Round(v);
        }

        private static int SafeInt(decimal v)
        {
            return (int)Math.Round(v);
        }
        private static void AddRowToArray(int[,] target, ref int rowIndex, int[] row)
        {
            if (target == null || row == null) return;

            int maxRow = target.GetLength(0);
            int maxCol = target.GetLength(1);

            if (rowIndex < 0 || rowIndex >= maxRow)
                return;

            int n = Math.Min(maxCol, row.Length);
            for (int c = 0; c < n; c++)
                target[rowIndex, c] = row[c];

            rowIndex++;
        }


        // ====== (A) 배수차/배수합 EMA ======
        private static void UpdateEmaDiffSum(string marketName, dynamic etfPost)
        {
            // 10s
            {
                int diff_raw = (int)(etfPost.분10매수배 - etfPost.분10매도배);
                int sum_raw = (int)(etfPost.분10매수배 + etfPost.분10매도배);
                double a = EmaStore.AlphaForInterval(10_000);
                (int diff, int sum) = EmaStore.Update(marketName, 10_000, diff_raw, sum_raw, a);
                etfPost.분10배수차 = diff;
                etfPost.분10배수합 = sum;
            }

            // 20s
            {
                int diff_raw = (int)(etfPost.분20매수배 - etfPost.분20매도배);
                int sum_raw = (int)(etfPost.분20매수배 + etfPost.분20매도배);
                double a = EmaStore.AlphaForInterval(20_000);
                (int diff, int sum) = EmaStore.Update(marketName, 20_000, diff_raw, sum_raw, a);
                etfPost.분20배수차 = diff;
                etfPost.분20배수합 = sum;
            }

            // 30s
            {
                int diff_raw = (int)(etfPost.분30매수배 - etfPost.분30매도배);
                int sum_raw = (int)(etfPost.분30매수배 + etfPost.분30매도배);
                double a = EmaStore.AlphaForInterval(30_000);
                (int diff, int sum) = EmaStore.Update(marketName, 30_000, diff_raw, sum_raw, a);
                etfPost.분30배수차 = diff;
                etfPost.분30배수합 = sum;
            }
        }

        // ====== (B) Sigma 누적 (계층 조건) ======
        // 10초:%18, 20초:%36, 30초:%54
        private static bool UpdateSigmaHierarchical(string marketName, dynamic etfPost, int marketeyeCount)
        {
            bool updated = false;

            // 10초
            if (marketeyeCount % 18 == 1)
            {
                UpdateSigmaSet(10_000, etfPost,
                    etfPost.분10프로천,
                    etfPost.분10외인천,
                    etfPost.분10기관천,
                    etfPost.분10개인천,
                    etfPost.분10나스닥,
                    etfPost.분10K200,
                    etfPost.분10가격차,
                    etfPost.분10배수차,
                    etfPost.분10배수합);
                updated = true;
            }

            // 20초
            if (marketeyeCount % 36 == 1)
            {
                UpdateSigmaSet(20_000, etfPost,
                    etfPost.분20프로천,
                    etfPost.분20외인천,
                    etfPost.분20기관천,
                    etfPost.분20개인천,
                    etfPost.분20나스닥,
                    etfPost.분20K200,
                    etfPost.분20가격차,
                    etfPost.분20배수차,
                    etfPost.분20배수합);
                updated = true;
            }

            // 30초
            if (marketeyeCount % 54 == 1)
            {
                UpdateSigmaSet(30_000, etfPost,
                    etfPost.분30프로천,
                    etfPost.분30외인천,
                    etfPost.분30기관천,
                    etfPost.분30개인천,
                    etfPost.분30나스닥,
                    etfPost.분30K200,
                    etfPost.분30가격차,
                    etfPost.분30배수차,
                    etfPost.분30배수합);
                updated = true;
            }

            return updated;
        }

        private static void UpdateSigmaSet(
            int windowMs,
            dynamic etfPost,
            double program,
            double foreign,
            double institution,
            double individual,
            double nasdaq,
            double k200,
            double price,
            double diff,
            double sum)
        {
            g.Sigma.Update(windowMs, SignalType.Program, program);
            g.Sigma.Update(windowMs, SignalType.Foreign, foreign);
            g.Sigma.Update(windowMs, SignalType.Institution, institution);
            g.Sigma.Update(windowMs, SignalType.Individual, individual);
            g.Sigma.Update(windowMs, SignalType.Nasdaq, nasdaq);
            g.Sigma.Update(windowMs, SignalType.K200, k200);
            g.Sigma.Update(windowMs, SignalType.Price, price);
            g.Sigma.Update(windowMs, SignalType.배수차, diff);
            g.Sigma.Update(windowMs, SignalType.배수합, sum);
        }



        private static int[] FindSelectedIndices(int[] tickTimes, int[] intervals)
        {
            if (tickTimes == null || tickTimes.Length == 0) return null;
            var selected = Enumerable.Repeat(-1, intervals.Length).ToArray();

            for (int i = 1; i < tickTimes.Length; i++)
            {
                if (tickTimes[i] == 0) break;
                double elapsed = TimeUtils.ElapsedMillisecondsDouble(tickTimes[i], tickTimes[0]);
                if (elapsed <= 0) break;

                for (int j = 0; j < intervals.Length; j++)
                    if (selected[j] == -1 && elapsed > intervals[j])
                        selected[j] = i;

                bool allFound = true;
                for (int j = 0; j < intervals.Length; j++)
                    if (selected[j] == -1) { allFound = false; break; }
                if (allFound) break;
            }
            return selected;
        }
    }

    public enum Sec10Col
    {
        Time = 0,        // HHmmssfff
        Etf = 1,         // ETF 현재가
        Nq = 2,          // 나스닥
        ProAcc = 3,      // 프로 누적
        ForAcc = 4,      // 외인 누적
        InstAcc = 5,     // 기관 누적
        IndiAcc = 6,     // 개인 누적
        Diff = 7,        // 분10배수차
        Sum = 8,         // 분10배수합
        Reserved9 = 9,
        Reserved10 = 10,
        Reserved11 = 11
    }
    public static class Sec10Store
    {
        public const int ROWS = 382 * 6;
        public const int COLS = 12;

        public static int[,] Kospi = new int[ROWS, COLS];
        public static int[,] Kosdaq = new int[ROWS, COLS];

        public static int KospiRow = 0;
        public static int KosdaqRow = 0;

        public static int LastKospiBucket = -1;
        public static int LastKosdaqBucket = -1;

        public static void Reset()
        {
            Kospi = new int[ROWS, COLS];
            Kosdaq = new int[ROWS, COLS];
            KospiRow = 0;
            KosdaqRow = 0;
            LastKospiBucket = -1;
            LastKosdaqBucket = -1;
        }
    }

    public static class IndexScoreEngine
    {
        // z 폭주 방지
        private static double ClampZ(double z) => Clamp(z, -3.0, 3.0);

        // score(-9..+9 정도)를 HUD(-100..+100)로 매핑 (|6| => 100)
        //private static int ToHud(double score)
        //{
        //    double s = Clamp(score, -9.0, 9.0);

        //    int hud = (int)Math.Round((s / 6.0) * 100.0);

        //    if (hud > 100) return 100;
        //    if (hud < -100) return -100;

        //    return hud;
        //}

        static int ToHud(double s)
        {
            // 표준정규 CDF 근사
            double p = NormalCdf(s);

            // -100 ~ +100 변환
            int hud = (int)Math.Round((p - 0.5) * 200);

            if (hud > 100) return 100;
            if (hud < -100) return -100;

            return hud;
        }
        static double NormalCdf(double x)
        {
            return 0.5 * (1.0 + Erf(x / Math.Sqrt(2.0)));
        }
        static double Erf(double x)
        {
            // Abramowitz & Stegun approximation
            double t = 1.0 / (1.0 + 0.3275911 * Math.Abs(x));

            double a1 = 0.254829592;
            double a2 = -0.284496736;
            double a3 = 1.421413741;
            double a4 = -1.453152027;
            double a5 = 1.061405429;

            double poly = (((((a5 * t) + a4) * t) + a3) * t + a2) * t + a1;

            double y = 1.0 - poly * t * Math.Exp(-x * x);

            return x >= 0 ? y : -y;
        }

        public sealed class Config
        {
            // 신호 가중치(윈도우 내부)
            public double wProg = 1.1;
            public double wFore = 1.0;
            public double wPrice = 0.8;
            public double wDiff = 1.2;   // 배수차: 방향성/추진력
            public double wSum = 0.6;    // 배수합: 과열/질량
            public double wNasdaq = 0.0; // 나스닥 실시간 변화량
            public double wK200 = 0.0;   // K200 변화량
            public double wInst = 0.0;   // 기관
            public double wIndi = 0.0;   // 개인

            // 10/20/30초 윈도우 가중치(최종 합성)
            public double w10 = 1.0;
            public double w20 = 0.7;
            public double w30 = 0.5;

            // 배수합을 “과열”로 볼지, “질량”으로 볼지
            public bool SumAsPenalty = false;
            public bool SumUseAbs = true;
        }

        // 시장별 파라미터 세트
        public static Config KospiConfig { get; } = new Config();
        public static Config KosdaqConfig { get; } = new Config();

        public sealed class Result
        {
            public DateTime Time;
            public int HudScore;        // -100..+100
            public double RawScore;     // 디버깅/튜닝용

            // 디버깅용 분해
            public double S10, S20, S30;

            // 10초 z 샘플 노출
            public double ZProg10, ZFore10, ZPrice10, ZDiff10, ZSum10;
            public double ZNasdaq10, ZK200_10, ZInst10, ZIndi10;
        }

        /// <summary>
        /// 지수(ETF)용 점수: marketName("Kospi"/"Kosdaq") + etfPost(분10/20/30 값들)
        /// Sigma는 UpdateSigmaHierarchical로 누적되고 있다고 가정.
        /// </summary>
        public static Result Compute(string marketName, dynamic etfPost, Config cfg = null)
        {
            if (cfg == null)
            {
                bool isKospi = string.Equals(marketName, "Kospi", StringComparison.OrdinalIgnoreCase);
                cfg = isKospi ? KospiConfig : KosdaqConfig;
            }

            // 10/20/30 각각 점수 (이미 window 내부에서 L1 정규화됨)
            double s10 = ScoreWindow(10_000, etfPost, cfg);
            double s20 = ScoreWindow(20_000, etfPost, cfg);
            double s30 = ScoreWindow(30_000, etfPost, cfg);

            // 최종 합성도 L1 정규화
            double raw =
                (cfg.w10 * s10) +
                (cfg.w20 * s20) +
                (cfg.w30 * s30);

            double wFinal =
                Math.Abs(cfg.w10) +
                Math.Abs(cfg.w20) +
                Math.Abs(cfg.w30);

            if (wFinal > 1e-12)
                raw /= wFinal;
            else
                raw = 0.0;

            // 안전 차원에서 한 번 더 clamp
            raw = Clamp(raw, -3.0, 3.0);

            return new Result
            {
                Time = DateTime.Now,
                RawScore = raw,
                HudScore = ToHud(raw),

                S10 = s10,
                S20 = s20,
                S30 = s30,

                // 10초 z 샘플 (디버깅용)
                ZProg10 = GetZ(10_000, SignalType.Program, ToDouble(etfPost.분10프로천)),
                ZFore10 = GetZ(10_000, SignalType.Foreign, ToDouble(etfPost.분10외인천)),
                ZPrice10 = GetZ(10_000, SignalType.Price, ToDouble(etfPost.분10가격차)),
                ZDiff10 = GetZ(10_000, SignalType.배수차, ToDouble(etfPost.분10배수차)),
                ZSum10 = GetZ(10_000, SignalType.배수합, ToDouble(etfPost.분10배수합)),
                ZNasdaq10 = GetZ(10_000, SignalType.Nasdaq, ToDouble(etfPost.분10나스닥)),
                ZK200_10 = GetZ(10_000, SignalType.K200, ToDouble(etfPost.분10K200)),
                ZInst10 = GetZ(10_000, SignalType.Institution, ToDouble(etfPost.분10기관천)),
                ZIndi10 = GetZ(10_000, SignalType.Individual, ToDouble(etfPost.분10개인천)),
            };
        }

        private static double Clamp(double x, double min, double max)
        {
            if (x < min) return min;
            if (x > max) return max;
            return x;
        }

        private static double ScoreWindow(int windowMs, dynamic etfPost, Config cfg)
        {
            // window별 입력값 선택
            double prog = windowMs == 10_000 ? ToDouble(etfPost.분10프로천) :
                          windowMs == 20_000 ? ToDouble(etfPost.분20프로천) :
                                               ToDouble(etfPost.분30프로천);

            double fore = windowMs == 10_000 ? ToDouble(etfPost.분10외인천) :
                          windowMs == 20_000 ? ToDouble(etfPost.분20외인천) :
                                               ToDouble(etfPost.분30외인천);

            double inst = windowMs == 10_000 ? ToDouble(etfPost.분10기관천) :
                          windowMs == 20_000 ? ToDouble(etfPost.분20기관천) :
                                               ToDouble(etfPost.분30기관천);

            double indi = windowMs == 10_000 ? ToDouble(etfPost.분10개인천) :
                          windowMs == 20_000 ? ToDouble(etfPost.분20개인천) :
                                               ToDouble(etfPost.분30개인천);

            double nasdaq = windowMs == 10_000 ? ToDouble(etfPost.분10나스닥) :
                            windowMs == 20_000 ? ToDouble(etfPost.분20나스닥) :
                                                 ToDouble(etfPost.분30나스닥);

            double k200 = windowMs == 10_000 ? ToDouble(etfPost.분10K200) :
                          windowMs == 20_000 ? ToDouble(etfPost.분20K200) :
                                               ToDouble(etfPost.분30K200);

            double price = windowMs == 10_000 ? ToDouble(etfPost.분10가격차) :
                           windowMs == 20_000 ? ToDouble(etfPost.분20가격차) :
                                                ToDouble(etfPost.분30가격차);

            double diff = windowMs == 10_000 ? ToDouble(etfPost.분10배수차) :
                          windowMs == 20_000 ? ToDouble(etfPost.분20배수차) :
                                               ToDouble(etfPost.분30배수차);

            double sum = windowMs == 10_000 ? ToDouble(etfPost.분10배수합) :
                         windowMs == 20_000 ? ToDouble(etfPost.분20배수합) :
                                              ToDouble(etfPost.분30배수합);

            // z 변환(σ는 SigmaManager가 관리)
            double zProg = ClampZ(GetZ(windowMs, SignalType.Program, prog));
            double zFore = ClampZ(GetZ(windowMs, SignalType.Foreign, fore));
            double zInst = ClampZ(GetZ(windowMs, SignalType.Institution, inst));
            double zIndi = ClampZ(GetZ(windowMs, SignalType.Individual, indi));
            double zNasdaq = ClampZ(GetZ(windowMs, SignalType.Nasdaq, nasdaq));
            double zK200 = ClampZ(GetZ(windowMs, SignalType.K200, k200));
            double zPrice = ClampZ(GetZ(windowMs, SignalType.Price, price));
            double zDiff = ClampZ(GetZ(windowMs, SignalType.배수차, diff));
            double zSum = ClampZ(GetZ(windowMs, SignalType.배수합, sum));

            // 배수합은 “질량” 성격이 강하니 abs를 기본 추천
            double sumTerm = cfg.SumUseAbs ? Math.Abs(zSum) : zSum;
            if (cfg.SumAsPenalty) sumTerm = -sumTerm;

            // 가중합
            double raw =
                cfg.wProg * zProg +
                cfg.wFore * zFore +
                cfg.wInst * zInst +
                cfg.wIndi * zIndi +
                cfg.wNasdaq * zNasdaq +
                cfg.wK200 * zK200 +
                cfg.wPrice * zPrice +
                cfg.wDiff * zDiff +
                cfg.wSum * sumTerm;

            // L1 정규화 (가중치 총량으로 나눔)
            double wSumAbs =
                Math.Abs(cfg.wProg) +
                Math.Abs(cfg.wFore) +
                Math.Abs(cfg.wInst) +
                Math.Abs(cfg.wIndi) +
                Math.Abs(cfg.wNasdaq) +
                Math.Abs(cfg.wK200) +
                Math.Abs(cfg.wPrice) +
                Math.Abs(cfg.wDiff) +
                Math.Abs(cfg.wSum);

            double s = (wSumAbs > 1e-12) ? (raw / wSumAbs) : 0.0;

            // 이론상 대체로 -3~+3 안쪽이지만 안전 차원에서 clamp
            return Clamp(s, -3.0, 3.0);
        }

        private static double GetZ(int windowMs, SignalType sig, double x)
        {
            return g.Sigma.GetZ(windowMs, sig, x);
        }

        private static double ToDouble(object x)
        {
            if (x == null) return 0.0;
            try { return Convert.ToDouble(x, CultureInfo.InvariantCulture); }
            catch { return 0.0; }
        }
    }
    public readonly struct IndexScoreResult
    {
        public readonly DateTime Time;   // 업데이트 시각(스냅샷 식별용)
        public readonly double NetScore;
        public readonly double MassScore;
        public readonly double S10, S20, S30;

        public IndexScoreResult(DateTime time, double net, double mass, double s10, double s20, double s30)
        {
            Time = time; NetScore = net; MassScore = mass; S10 = s10; S20 = s20; S30 = s30;
        }
    }

    // if (IndexScoreStore.TryGet("K200", out var k200)){} 사용법
    public static class IndexScoreStore
    {
        private static readonly ConcurrentDictionary<string, Result> _latest
            = new ConcurrentDictionary<string, Result>(StringComparer.Ordinal);

        public static void Update(string symbol, Result r)
            => _latest[symbol] = r;

        public static bool TryGet(string symbol, out Result r)
            => _latest.TryGetValue(symbol, out r);
    }









    // ===== EMA Helpers =====
    public static class EmaStore
    {
        // marketName -> (intervalMs -> (diff,sum))
        private static readonly Dictionary<string, Dictionary<int, (double diff, double sum)>> _state
            = new Dictionary<string, Dictionary<int, (double diff, double sum)>>(StringComparer.OrdinalIgnoreCase);

        public static (int diff, int sum) Update(string market, int intervalMs, int diff, int sum, double alpha)
        {
            if (!_state.TryGetValue(market, out var byInterval))
            {
                byInterval = new Dictionary<int, (double, double)>();
                _state[market] = byInterval;
            }

            if (!byInterval.TryGetValue(intervalMs, out var prev))
            {
                // 최초엔 현재값으로 시드
                prev = (diff, sum);
            }
            else
            {
                prev.diff = alpha * diff + (1.0 - alpha) * prev.diff;
                prev.sum = alpha * sum + (1.0 - alpha) * prev.sum;
            }
            byInterval[intervalMs] = prev;

            // 표시는 반올림 정수
            return ((int)Math.Round(prev.diff), (int)Math.Round(prev.sum));
        }

        // 구간별 권장 alpha (기간 N에 대해 alpha=2/(N+1))
        public static double AlphaForInterval(int intervalMs)
        {
            int sec = Math.Max(1, intervalMs / 1000);
            int period; // EMA 기간
            if (sec <= 10) period = 8;       // 10s
            else if (sec <= 20) period = 12;      // 20s
            else period = 16;      // 30s+
            return 2.0 / (period + 1.0);
        }

        public static void Reset(string market) => _state.Remove(market);
    }



    // ───────────────────────────────────────────────
    // Sigma infra (Rolling/EMA/Welford + Manager + Persistence)
    // ───────────────────────────────────────────────

    public enum SigmaMode { Rolling, Ema, Welford }

    public interface ISigmaAccumulator
    {
        void Update(double x);
        double Mean { get; }
        double Sigma { get; }
        long Count { get; }
        void Reset();
    }

    public sealed class RollingAccumulator : ISigmaAccumulator
    {
        private readonly int _capacity;
        private readonly Queue<double> _q;
        private double _sum, _sumSq;

        public RollingAccumulator(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _capacity = capacity;
            _q = new Queue<double>(capacity);
        }

        public void Update(double x)
        {
            if (_q.Count == _capacity)
            {
                var old = _q.Dequeue();
                _sum -= old;
                _sumSq -= old * old;
            }
            _q.Enqueue(x);
            _sum += x;
            _sumSq += x * x;
        }

        public double Mean => _q.Count > 0 ? _sum / _q.Count : 0.0;

        public double Sigma
        {
            get
            {
                int n = _q.Count;
                if (n <= 1) return 0.0;
                double mean = Mean;
                double var = (_sumSq / n) - (mean * mean);
                return var > 0 ? Math.Sqrt(var) : 0.0;
            }
        }

        public long Count => _q.Count;

        public void Reset()
        {
            _q.Clear();
            _sum = 0.0; _sumSq = 0.0;
        }
    }

    public sealed class EmaAccumulator : ISigmaAccumulator
    {
        private readonly double _alpha;
        private bool _init;
        private double _mean, _v; // EMA-variance

        public EmaAccumulator(int span)
        {
            if (span <= 0) throw new ArgumentOutOfRangeException(nameof(span));
            _alpha = 2.0 / (span + 1.0);
        }

        public void Update(double x)
        {
            if (!_init)
            {
                _mean = x; _v = 0.0; _init = true; Count++;
                return;
            }
            _mean = _alpha * x + (1 - _alpha) * _mean;
            double diff = x - _mean;
            _v = _alpha * (diff * diff) + (1 - _alpha) * _v;
            Count++;
        }

        public double Mean => _mean;
        public double Sigma => _v > 0 ? Math.Sqrt(_v) : 0.0;
        public long Count { get; private set; }

        public void Reset()
        {
            _init = false; _mean = 0.0; _v = 0.0; Count = 0;
        }
    }

    public struct SigmaKey : IEquatable<SigmaKey>
    {
        public int WindowMs;
        public SignalType Signal;
        public SigmaKey(int windowMs, SignalType signal) { WindowMs = windowMs; Signal = signal; }
        public bool Equals(SigmaKey other) => WindowMs == other.WindowMs && Signal == other.Signal;
        public override bool Equals(object obj) => obj is SigmaKey k && Equals(k);
        public override int GetHashCode() => (WindowMs * 397) ^ (int)Signal;
    }

    public sealed class WelfordAccumulator : ISigmaAccumulator
    {
        // 유효 샘플 수 (감쇠형) → double로 변경
        public double N { get; private set; }

        public double Mean { get; private set; }
        public double M2 { get; private set; }

        // readiness 판단용
        public long Count => (long)N;

        public double Sigma => N > 0 ? Math.Sqrt(Variance) : 0.0;

        public double Variance => (N > 0) ? (M2 / Math.Max(1.0, N)) : 0.0; // population

        public void Update(double x) => Update(x, 0.55);

        // 반감기 (현재 약 44시간)
        public double HalfLifeSec { get; set; } = 380 * 60 * 7;

        public double AlphaCap { get; set; } = 1.0;

        public void Update(double x, double dt)
        {
            // 1) 반감기 기반 알파
            double alpha = 1.0 - Math.Pow(2.0, -dt / HalfLifeSec);

            if (alpha < 1e-9) alpha = 1e-9;         // 안전 하한
            if (alpha > AlphaCap) alpha = AlphaCap; // 선택 상한

            // 2) 평균 / 분산 업데이트
            double delta = x - Mean;

            Mean += alpha * delta;

            M2 = (1.0 - alpha) * (M2 + alpha * delta * delta);

            // 3) 유효 샘플 수 (감쇠형)
            N = (1.0 - alpha) * N + 1.0;
        }

        public void Reset()
        {
            N = 0;
            Mean = 0;
            M2 = 0;
        }

        // 시드 로딩용
        public void Seed(long n, double mean, double m2)
        {
            N = n;
            Mean = mean;
            M2 = m2;
        }
    }



    public enum SignalType
    {
        Program,
        Foreign,
        Institution,
        Individual,
        Price,
        Nasdaq,
        K200,
        배수차,
        배수합
    }



    public sealed class SigmaManager
    {
        private readonly Dictionary<SigmaKey, ISigmaAccumulator> _map = new Dictionary<SigmaKey, ISigmaAccumulator>();
        private readonly SigmaMode _mode;
        private readonly Dictionary<int, int> _windowSpanByMs; // windowMs → N(롤링개수 or EMA span)
        public int MinSamples = 5; // 워밍업

        private readonly string _baseDir = @"C:\BJS\data work";
        private readonly string _filePrefix = "SigmaState_";
        private string _filePath;

        // Index Norm seed source
        private readonly string _indexNormRoot = Path.Combine(@"C:\BJS\data work", "Index Norm");
        public int IndexNormLookbackDays = 20;

        private static readonly object _lock = new object();
        public string FilePath => _filePath;

        void LogId(string where)
            => Debug.WriteLine($"[{where}] this={RuntimeHelpers.GetHashCode(this)}, mode={_mode}, mapCount={_map.Count}");

        // --- ctor ---------------------------------------------------------------
        public SigmaManager(SigmaMode mode, Dictionary<int, int> windowSpanByMs)
        {
            _mode = mode;
            _windowSpanByMs = windowSpanByMs ?? throw new ArgumentNullException(nameof(windowSpanByMs));
        }

        // --- 내부 유틸 ----------------------------------------------------------
        private static readonly SignalType[] _allSignals = (SignalType[])Enum.GetValues(typeof(SignalType));

        private IEnumerable<int> EnumerateWindows() =>
            _windowSpanByMs.Keys.OrderBy(x => x); // 정렬된 window 목록

        private void SeedWelford(int windowMs, SignalType sig, long n, double mean, double m2)
        {
            var acc = GetOrCreate(windowMs, sig);
            if (acc is WelfordAccumulator w) w.Seed(n, mean, m2);
        }

        // Accumulator 팩토리/캐시
        private ISigmaAccumulator GetOrCreate(int windowMs, SignalType sig)
        {
            var key = new SigmaKey(windowMs, sig);
            if (_map.TryGetValue(key, out var acc))
                return acc;

            if (!_windowSpanByMs.TryGetValue(windowMs, out var span))
                throw new KeyNotFoundException("windowMs not configured: " + windowMs);

            ISigmaAccumulator created;
            switch (_mode)
            {
                case SigmaMode.Rolling:
                    created = new RollingAccumulator(span);
                    break;
                case SigmaMode.Ema:
                    created = new EmaAccumulator(span);
                    break;
                case SigmaMode.Welford:
                    created = new WelfordAccumulator(); // span 무시
                    break;
                default:
                    throw new NotSupportedException($"Unknown SigmaMode: {_mode}");
            }

            _map[key] = created;
            return created;
        }

        // --- 퍼블릭 API ---------------------------------------------------------
        public void Update(int windowMs, SignalType sig, double value) => GetOrCreate(windowMs, sig).Update(value);
        public double GetMean(int windowMs, SignalType sig) => GetOrCreate(windowMs, sig).Mean;
        public double GetSigma(int windowMs, SignalType sig) => GetOrCreate(windowMs, sig).Sigma;

        public double GetZ(int windowMs, SignalType sig, double x)
        {
            var acc = GetOrCreate(windowMs, sig);
            if (acc.Count < MinSamples) return 0.0;

            var s = acc.Sigma;
            if (s < 1e-9) return 0.0;

            return (x - acc.Mean) / s;
        }

        public void ResetAll()
        {
            foreach (var kv in _map) kv.Value.Reset();
        }

        // --- 시작 시 로드 -------------------------------------------------------
        public void LoadAtStart()
        {
            EnsureBaseDir();
            EnsureIndexNormRoot();

            _filePath = Path.Combine(_baseDir, _filePrefix + "Current.txt");

            // 1) 최신 SigmaState 파일 우선
            if (TryLoadLatestSigmaState())
                return;

            // 2) SigmaState 없으면 Index Norm 최근 N일로 seed
            TrySeedFromIndexNorm(IndexNormLookbackDays);

            // 3) 아무것도 없어도 그냥 빈 상태로 시작
        }

        private void EnsureBaseDir()
        {
            if (string.IsNullOrEmpty(_baseDir))
                throw new InvalidOperationException("_baseDir is empty");

            if (!Directory.Exists(_baseDir))
                Directory.CreateDirectory(_baseDir);
        }

        private void EnsureIndexNormRoot()
        {
            if (string.IsNullOrEmpty(_indexNormRoot))
                throw new InvalidOperationException("_indexNormRoot is empty");

            if (!Directory.Exists(_indexNormRoot))
                Directory.CreateDirectory(_indexNormRoot);
        }

        private bool TryLoadLatestSigmaState()
        {
            var files = Directory.GetFiles(_baseDir, _filePrefix + "*.txt", SearchOption.TopDirectoryOnly);
            if (files.Length == 0)
                return false;

            var latest = files
                .Select(p => new FileInfo(p))
                .OrderByDescending(fi => fi.LastWriteTimeUtc)
                .FirstOrDefault();

            if (latest == null || !latest.Exists)
                return false;

            _filePath = latest.FullName;
            LoadFromFile(_filePath);
            return true;
        }

        // --- Index Norm seed ----------------------------------------------------
        private bool TrySeedFromIndexNorm(int lookbackDays)
        {
            try
            {
                var dayDirs = GetRecentIndexNormDayDirs(lookbackDays);
                if (dayDirs.Count == 0)
                    return false;

                bool anyLoaded = false;

                foreach (var dir in dayDirs)
                {
                    var normFiles = Directory.GetFiles(dir, "*_norm.txt", SearchOption.TopDirectoryOnly)
                        .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                        .ToArray();

                    foreach (var file in normFiles)
                    {
                        if (LoadOneNormFile(file))
                            anyLoaded = true;
                    }
                }

                return anyLoaded;
            }
            catch
            {
                return false;
            }
        }

        private List<string> GetRecentIndexNormDayDirs(int lookbackDays)
        {
            EnsureIndexNormRoot();

            return Directory.GetDirectories(_indexNormRoot, "*", SearchOption.TopDirectoryOnly)
                .Where(dir =>
                {
                    var name = Path.GetFileName(dir);
                    return !string.IsNullOrWhiteSpace(name)
                        && name.Length == 8
                        && name.All(char.IsDigit);
                })
                .OrderByDescending(dir => Path.GetFileName(dir))
                .Take(Math.Max(1, lookbackDays))
                .ToList();
        }

        private bool LoadOneNormFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return false;

                using (var sr = new StreamReader(filePath, Encoding.UTF8, true))
                {
                    string headerLine = sr.ReadLine();
                    if (string.IsNullOrWhiteSpace(headerLine))
                        return false;

                    var headers = headerLine.Split(',');

                    int idxPrice = FindHeader(headers, "Δ가격", "가격", "Price", "dPrice");
                    int idxNasdaq = FindHeader(headers, "Δ나스닥", "Δ나스닥실시간", "나스닥", "Nasdaq", "NQRT");
                    int idxK200 = FindHeader(headers, "Δk200", "ΔK200", "k200", "K200");
                    int idxProgram = FindHeader(headers, "Δ프", "Δ프로", "프", "프로", "Program");
                    int idxForeign = FindHeader(headers, "Δ외", "외", "Foreign");
                    int idxInstitution = FindHeader(headers, "Δ기", "기", "Institution");
                    int idxIndividual = FindHeader(headers, "Δ개", "개", "Individual");
                    int idxDiff = FindHeader(headers, "Δ배수차", "배수차", "Diff");
                    int idxSum = FindHeader(headers, "Δ배수합", "배수합", "Sum");

                    bool anyRow = false;
                    string line;

                    while ((line = sr.ReadLine()) != null)
                    {
                        if (string.IsNullOrWhiteSpace(line))
                            continue;

                        var cols = line.Split(',');
                        if (cols.Length == 0)
                            continue;

                        anyRow = true;

                        UpdateAllWindowsIfParsed(cols, idxPrice, SignalType.Price);
                        UpdateAllWindowsIfParsed(cols, idxNasdaq, SignalType.Nasdaq);
                        UpdateAllWindowsIfParsed(cols, idxK200, SignalType.K200);
                        UpdateAllWindowsIfParsed(cols, idxProgram, SignalType.Program);
                        UpdateAllWindowsIfParsed(cols, idxForeign, SignalType.Foreign);
                        UpdateAllWindowsIfParsed(cols, idxInstitution, SignalType.Institution);
                        UpdateAllWindowsIfParsed(cols, idxIndividual, SignalType.Individual);
                        UpdateAllWindowsIfParsed(cols, idxDiff, SignalType.배수차);
                        UpdateAllWindowsIfParsed(cols, idxSum, SignalType.배수합);
                    }

                    return anyRow;
                }
            }
            catch
            {
                return false;
            }
        }

        private int FindHeader(string[] headers, params string[] aliases)
        {
            if (headers == null || headers.Length == 0 || aliases == null || aliases.Length == 0)
                return -1;

            for (int i = 0; i < headers.Length; i++)
            {
                var h = (headers[i] ?? string.Empty).Trim();
                foreach (var alias in aliases)
                {
                    if (string.Equals(h, alias, StringComparison.OrdinalIgnoreCase))
                        return i;
                }
            }

            return -1;
        }

        private void UpdateAllWindowsIfParsed(string[] cols, int colIndex, SignalType sig)
        {
            if (colIndex < 0 || colIndex >= cols.Length)
                return;

            if (!TryParseDouble(cols[colIndex], out double value))
                return;

            foreach (var windowMs in EnumerateWindows())
                Update(windowMs, sig, value);
        }

        private bool TryParseDouble(string s, out double value)
        {
            var inv = CultureInfo.InvariantCulture;
            return double.TryParse((s ?? string.Empty).Trim(), NumberStyles.Float, inv, out value);
        }

        // --- 상태 저장/복원 (Welford 전용 CSV) ----------------------------------
        public void Save()
        {
            LogId("Save");

            if (_mode != SigmaMode.Welford)
                return;

            EnsureBaseDir();

            _filePath = Path.Combine(_baseDir, _filePrefix + "Current.txt");
            var inv = CultureInfo.InvariantCulture;
            var tmp = _filePath + ".tmp";

            lock (_lock)
                using (var sw = new StreamWriter(tmp, false, new UTF8Encoding(false)))
                {
                    sw.WriteLine("# SIGMA v1 (Welford)");
                    sw.WriteLine("windowMs,signal,n,mean,m2");

                    foreach (var kv in _map.OrderBy(k => k.Key.WindowMs).ThenBy(k => k.Key.Signal))
                    {
                        if (kv.Value is WelfordAccumulator wa && wa.N > 0)
                        {
                            sw.Write(kv.Key.WindowMs); sw.Write(",");
                            sw.Write(kv.Key.Signal); sw.Write(",");
                            sw.Write(wa.N.ToString(inv)); sw.Write(",");
                            sw.Write(wa.Mean.ToString("R", inv)); sw.Write(",");
                            sw.Write(wa.M2.ToString("R", inv));
                            sw.WriteLine();
                        }
                    }
                }

            File.Copy(tmp, _filePath, overwrite: true);
            File.Delete(tmp);
        }

        private void LoadFromFile(string path)
        {
            _map.Clear();

            if (_mode != SigmaMode.Welford)
                return; // 다른 모드 복원은 필요 시 확장

            var inv = CultureInfo.InvariantCulture;

            foreach (var line in File.ReadLines(path))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (line[0] == '#' || line.StartsWith("windowMs")) continue;

                var cols = line.Split(',');
                if (cols.Length < 5) continue;

                if (!int.TryParse(cols[0], NumberStyles.Integer, inv, out int windowMs)) continue;
                if (!Enum.TryParse(cols[1], ignoreCase: true, out SignalType sig)) continue;
                if (!long.TryParse(cols[2], NumberStyles.Integer, inv, out long n)) continue;
                if (!double.TryParse(cols[3], NumberStyles.Float, inv, out double mean)) continue;
                if (!double.TryParse(cols[4], NumberStyles.Float, inv, out double m2)) continue;

                if (!_windowSpanByMs.ContainsKey(windowMs)) continue;

                SeedWelford(windowMs, sig, n, mean, m2);
            }
        }
    }

}
