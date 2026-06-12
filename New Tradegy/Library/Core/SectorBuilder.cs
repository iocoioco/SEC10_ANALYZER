using New_Tradegy.Library.Core;
using New_Tradegy.Library.IO;
using New_Tradegy.Library.Models;
using New_Tradegy.Library.Trackers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace New_Tradegy.Library.PostProcessing
{
    public static class SectorBuilder
    {
        static Dictionary<string, RingBuffer<RingData>> _sectorRings
            = new Dictionary<string, RingBuffer<RingData>>();

        private const int ROWS = 382;
        private const int COLS = 12;

        private const int SEED_TIME = 85900;      // 08:59:59
        private const int OPEN_MIN = 900;         // 09:00 (HHMM)
        private const int MULT_SCALE = 10000;     // 배수/체결강도 기준 스케일
        private const double WON_TO__10M = 10_000_000.0; // 1천만원
        static int ToMinuteKey(int hhmmss) => hhmmss / 100;

        private static int GetMaxTimeKeyForMinute(List<StockData> members, int tMin)
        {
            int maxTk = 0;

            foreach (var m in members)
            {
                var api = m?.Api;
                var x = api?.x;
                if (x == null || api.nrow <= 0) continue;

                int n = Math.Min(api.nrow, x.GetLength(0));
                if (n <= 0) continue;

                int row = FindLastRowOfMinute(x, n, tMin);
                if (row < 0) continue;

                int tk = x[row, 0];
                if (tk > maxTk) maxTk = tk;
            }

            return maxTk;
        }

        public static void BuildSectorsFromSavedMinuteData()
        {
            if (g.GroupManager?.Groups == null || g.GroupManager.Groups.Count == 0)
                return;

            foreach (var grp in g.GroupManager.Groups)
            {
                if (grp == null) continue;
                if (string.IsNullOrWhiteSpace(grp.Title)) continue;
                if (grp.Stocks == null || grp.Stocks.Count < 2) continue;

                // 1) 멤버 수집
                var members = new List<StockData>();
                foreach (var name in grp.Stocks)
                {
                    var sd = g.StockRepo.TryGetDataOrNull(name);
                    if (sd?.Api == null) continue;
                    if (sd.Api.x == null) continue;
                    if (sd.Api.nrow <= 0) continue;
                    if (sd.Api.전일종가 <= 0) continue;
                    if (sd.Statistics == null) continue;
                    members.Add(sd);
                }
                if (members.Count < 2) continue;

                // 2) 섹터 확보
                string sectorName = grp.Title.Trim();
                if (string.IsNullOrEmpty(sectorName)) continue;

                string sectorKey = $"SECTOR:{sectorName}";
                var sector = g.StockRepo.TryGetDataOrNull(sectorKey);
                if (sector == null)
                {
                    sector = new StockData
                    {
                        Stock = sectorKey,
                        Api = new ApiData(),
                        Statistics = new StatisticsData()
                    };
                    g.StockRepo.AddOrUpdate(sectorKey, sector);
                }
                if (sector.Api == null) sector.Api = new ApiData();
                if (sector.Statistics == null) sector.Statistics = new StatisticsData();

                // 3) 분모(20일 평균 거래대금 합, 천만원)
                double sectorAvg_10M = 0.0;
                foreach (var m in members)
                {
                    double avg = m.Statistics.AvgDailyTurnover_10M; // 천만원 단위
                    if (avg > 0) sectorAvg_10M += avg;
                }
                if (sectorAvg_10M <= 0) sectorAvg_10M = 1;
                sector.Statistics.AvgDailyTurnover_10M = (int)Math.Round(sectorAvg_10M);

                // 4) 분(minuteKey) 목록 만들기(합집합)
                var minutes = BuildMinuteSetFromMembers(members);

                lock (sector)
                {
                    EnsureSectorMatrix(sector);

                    // ✅ 시드(08:59:59) 기본값 생성
                    WriteSeedDefault(sector);

                    if (minutes.Count == 0)
                    {
                        sector.Api.nrow = Math.Max(sector.Api.nrow, 1);
                        ChartGeneral.UpdateFlowSnapshot(sector);
                        continue;
                    }

                    // ✅ 행0=시드, 행1부터 채움
                    int outRow = 1;
                    int maxOut = sector.Api.x.GetLength(0);

                    foreach (int tMin in minutes)
                    {
                        if (outRow >= maxOut) break;
                        if (tMin <= 859) continue;

                        if (!TryComputeSectorRow_AllMembers_WithBuySell(
                                members, sectorAvg_10M, tMin,
                                out int used,
                                out int price100,
                                out int proMult100, out int forMult100, out int instMult100, out int moneyMult100,
                                out int buyMult, out int sellMult,
                                out bool allMembersHaveThisMinute))
                        {
                            continue;
                        }

                        // ✅ Build는 “전원 데이터” 원칙 유지
                        if (!allMembersHaveThisMinute)
                            continue;

                        int hhmmss = GetMaxTimeKeyForMinute(members, tMin);
                        // 그래도 못 구하면 fallback으로 59초 추천
                        //if (hhmmss <= 0) hhmmss = tMin * 100 + 59;

                        WriteRow(sector, outRow, hhmmss,
                            price100, proMult100, forMult100, instMult100, moneyMult100,
                            buyMult, sellMult);

                        outRow++;
                    }

                    sector.Api.nrow = Math.Max(1, outRow);
                }

                ChartGeneral.UpdateFlowSnapshot(sector);
            }
        }

        public static void UpdateSectorsOnDownloadTick(int hhmmss)
        {
            if (g.GroupManager?.Groups == null || g.GroupManager.Groups.Count == 0)
                return;

            int tMin = ToMinuteKey(hhmmss);
            bool anyUpdated = false;

            foreach (var grp in g.GroupManager.Groups)
            {
                if (grp == null) continue;
                if (string.IsNullOrWhiteSpace(grp.Title)) continue;
                if (grp.Stocks == null || grp.Stocks.Count < 2) continue;

                var members = new List<StockData>();
                foreach (var name in grp.Stocks)
                {
                    var sd = g.StockRepo.TryGetDataOrNull(name);
                    if (sd?.Api == null) continue;
                    if (sd.Api.x == null) continue;
                    if (sd.Api.nrow <= 0) continue;
                    if (sd.Api.전일종가 <= 0) continue;
                    if (sd.Statistics == null) continue;
                    members.Add(sd);
                }
                if (members.Count < 2) continue;

                string sectorKey = $"SECTOR:{grp.Title.Trim()}";
                var sector = g.StockRepo.TryGetDataOrNull(sectorKey);
                if (sector == null)
                {
                    sector = new StockData
                    {
                        Stock = sectorKey,
                        Api = new ApiData(),
                        Statistics = new StatisticsData()
                    };
                    g.StockRepo.AddOrUpdate(sectorKey, sector);
                }
                if (sector.Api == null) sector.Api = new ApiData();
                if (sector.Statistics == null) sector.Statistics = new StatisticsData();

                double sectorAvg10M = sector.Statistics.AvgDailyTurnover_10M;
                if (sectorAvg10M <= 0)
                {
                    double sum = 0;
                    foreach (var m in members)
                        if (m.Statistics.AvgDailyTurnover_10M > 0)
                            sum += m.Statistics.AvgDailyTurnover_10M;

                    if (sum <= 0) sum = 1;
                    sectorAvg10M = sum;
                    sector.Statistics.AvgDailyTurnover_10M = (int)Math.Round(sum);
                }

                if (!TryComputeSectorRow_RealTimeUsed(
                        members, tMin,
                        out int used,
                        out double denomUsed10M,
                        out int price100,
                        out int proCum10M,
                        out int forCum10M,
                        out int instCum10M,
                        out int moneyCum10M,
                        out int buyDummy,
                        out int sellDummy))
                    continue;

                if (used < 2) continue;

                double cover = denomUsed10M / sectorAvg10M;
                if (cover < 0.80) continue;

                bool wrote = WriteSectorRowWithSeedAndReplaceAppend(
                    sector, hhmmss, tMin,
                    price100,
                    proCum10M, forCum10M, instCum10M, moneyCum10M,
                    buyDummy, sellDummy);


                if (!wrote) continue;

                ChartGeneral.UpdateFlowSnapshot(sector);

                var api = sector.Api;
                int row = api.nrow - 1;
                if (row < 0) continue;

                long buyAmtCumWon = 0;
                long sellAmtCumWon = 0;

                foreach (var m in members)
                {
                    var a = m.Api;
                    if (a?.틱수누량 == null || a.틱도누량 == null) continue;
                    if (a.틱수누량.Length == 0 || a.틱도누량.Length == 0) continue;

                    long buyQty = a.틱수누량[0];
                    long sellQty = a.틱도누량[0];
                    long price = a.전일종가;

                    buyAmtCumWon += buyQty * price;
                    sellAmtCumWon += sellQty * price;
                }

                long buyAmtCum10M = buyAmtCumWon / 10_000_000L;
                long sellAmtCum10M = sellAmtCumWon / 10_000_000L;

                if (!_sectorRings.TryGetValue(sectorKey, out var ring) || ring == null)
                {
                    ring = new RingBuffer<RingData>(96);
                    _sectorRings[sectorKey] = ring;
                }

                int nowMs = Environment.TickCount;

                ring.Append(new RingData
                {
                    TimeMs = nowMs,
                    Price100 = api.x[row, 1],
                    ProCum = api.x[row, 4],
                    ForCum = api.x[row, 5],
                    InstCum = api.x[row, 6],
                    MoneyCum = api.x[row, 7],
                    BuyAmtCum10M = buyAmtCum10M,
                    SellAmtCum10M = sellAmtCum10M
                });

                int p1m = 0, f1m = 0, i1m = 0, m1m = 0;
                int buyMult = 0, sellMult = 0;
                double pf = 0;

                if (RingCalc.TryGetDelayed30x2_Delta10M(
                        ring, nowMs,
                        out int dPro30_10M,
                        out int dFor30_10M,
                        out int dInst30_10M,
                        out int dMoney30_10M,
                        out long buy30_10M,
                        out long sell30_10M,
                        out double pf30))
                {
                    double avg = sectorAvg10M;
                    if (avg <= 0) avg = 1;

                    const int MULT_SCALE = 100;
                    m1m = (int)Math.Round(dMoney30_10M * 2.0 * 380.0 / avg * MULT_SCALE);
                    p1m = (int)Math.Round(dPro30_10M * 2.0 * 380.0 / avg * MULT_SCALE);
                    f1m = (int)Math.Round(dFor30_10M * 2.0 * 380.0 / avg * MULT_SCALE);
                    i1m = (int)Math.Round(dInst30_10M * 2.0 * 380.0 / avg * MULT_SCALE);

                    const int SCALE = 10;
                    buyMult = (int)Math.Round(buy30_10M * 2.0 * 380.0 / avg * SCALE);
                    sellMult = (int)Math.Round(sell30_10M * 2.0 * 380.0 / avg * SCALE);

                    pf = pf30;
                }

                grp.UpdateSectorMetrics(
                    minuteKey: tMin,
                    usedCount: used,
                    coverRatio: cover,
                    price100: api.x[row, 1],
                    moneyMult100: m1m,
                    proMult100: p1m,
                    forMult100: f1m,
                    instMult100: i1m,
                    buyMult: buyMult,
                    sellMult: sellMult,
                    pfOverMoneyPct: pf
                );

                anyUpdated = true;
            }

            if (anyUpdated)
                RebuildSectorRanks(g.GroupManager.Groups);
        }

        private static SortedSet<int> BuildMinuteSetFromMembers(List<StockData> members)
        {
            var set = new SortedSet<int>();

            foreach (var m in members)
            {
                int n = Math.Min(m.Api.nrow, m.Api.x.GetLength(0));
                for (int r = 0; r < n; r++)
                {
                    int t = m.Api.x[r, 0];
                    if (t <= 0) continue;

                    int tMin = t / 100;   // HHMM
                    if (tMin <= 0) continue;

                    set.Add(tMin);
                }
            }

            return set;
        }

        // ---- local helper: 특정 분(targetMin)의 마지막 row 찾기 ----
        private static int FindLastRowOfMinute(int[,] x, int n, int targetMin)
        {
            int last = -1;
            if (x == null || n <= 0) return -1;

            // n이 x 실제 row보다 클 수 있으니 방어
            int maxN = x.GetLength(0);
            if (n > maxN) n = maxN;

            for (int r = 0; r < n; r++)
            {
                int t = x[r, 0];
                if (t <= 0) continue;

                int hhmm = t / 100;
                if (hhmm < targetMin) continue;

                if (hhmm == targetMin) last = r;
                else break; // hhmm > targetMin (오름차순 가정)
            }
            return last;
        }


        private static bool TryComputeSectorRow_AllMembers_WithBuySell(
            List<StockData> members, double sectorAvg_10M, int tMin,
            out int used,
            out int price100,
            out int proMult100, out int forMult100, out int instMult100, out int moneyMult100,
            out int buyMult, out int sellMult,
            out bool allMembersHaveThisMinute)
        {
            used = 0;
            price100 = 0;

            // ✅ 이름은 Mult100 그대로 두되 "누적 돈(10M)"을 담음
            proMult100 = forMult100 = instMult100 = moneyMult100 = 0;

            buyMult = 0;
            sellMult = 0;
            allMembersHaveThisMinute = false;

            if (members == null || members.Count < 2) return false;

            // ✅ 분(증분돈) 가중평균용
            double sumW = 0.0;   // Σ minuteMoney_10M
            double sumWP = 0.0;  // Σ minuteMoney_10M * p100
            double sumMinute = 0.0;   // Σ minuteMoney_10M
            double sumBuy = 0.0; // Σ minuteMoney_10M * b
            double sumSell = 0.0;// Σ minuteMoney_10M * s

            // ✅ 누적 돈 합산용(오버플로 방지)
            long moneySum = 0;
            long proSum = 0;
            long forSum = 0;
            long instSum = 0;

            int eligibleCount = 0;
            int haveCount = 0;



            foreach (var m in members)
            {
                if (m?.Api?.x == null) continue;
                if (m.Api.nrow <= 0) continue;
                if (m.Api.전일종가 <= 0) continue;

                eligibleCount++;

                var api = m.Api;
                var x = api.x;

                int n = Math.Min(api.nrow, x.GetLength(0));
                if (n <= 0) continue;

                // ✅ 이번 분(tMin)과 이전 분(tMin-1)의 "마지막 row" 확보
                int rowNow = FindLastRowOfMinute(x, n, tMin);
                if (rowNow < 0) continue; // 이번 분 데이터 없음

                int rowPrev = FindLastRowOfMinute(x, n, tMin - 1); // 장 시작이면 -1일 수 있음

                haveCount++;

                int p100 = x[rowNow, 1];

                // ✅ 누적 수량(또는 누적 거래) 기반: x[row,7]
                int cumVolNow = x[rowNow, 7];
                if (cumVolNow < 0) cumVolNow = 0;

                int cumVolPrev = 0;
                if (rowPrev >= 0)
                {
                    cumVolPrev = x[rowPrev, 7];
                    if (cumVolPrev < 0) cumVolPrev = 0;
                }

                // ✅ 분 거래 수량(증분)
                int dVol = cumVolNow - cumVolPrev;
                if (dVol < 0) dVol = 0;

                // ✅ 분 매수배/매도배는 "이번 분의 값" 사용
                int b = x[rowNow, 8];
                int s = x[rowNow, 9];

                // ✅ pro/for/inst는 누적수량(기존 컬럼 의미 유지)
                int proQty = x[rowNow, 4];
                int forQty = x[rowNow, 5];
                int instQty = x[rowNow, 6];

                double px = api.전일종가;

                // ===== 1) 누적 돈(10M): 기존 유지 =====
                double money_10M = (cumVolNow * px) / WON_TO__10M;
                double pro_10M = (proQty * px) / WON_TO__10M;
                double for_10M = (forQty * px) / WON_TO__10M;
                double inst_10M = (instQty * px) / WON_TO__10M;

                moneySum += (long)Math.Round(money_10M);
                proSum += (long)Math.Round(pro_10M);
                forSum += (long)Math.Round(for_10M);
                instSum += (long)Math.Round(inst_10M);

                // ===== 2) 분 거래돈(10M): price/buy/sell 가중치 =====
                double minuteMoney_10M = (dVol * px) / WON_TO__10M;
                
                if (minuteMoney_10M > 0)
                {
                    sumW += m.Statistics.AvgDailyTurnover_10M;
                    sumWP += m.Statistics.AvgDailyTurnover_10M * p100;

                    sumMinute += minuteMoney_10M;
                    sumBuy += minuteMoney_10M * b;
                    sumSell += minuteMoney_10M * s;
                }

                used++;
            }

            // ✅ allMembersHaveThisMinute: "계산 자격 있는 멤버" 기준
            allMembersHaveThisMinute = (eligibleCount >= 2 && haveCount == eligibleCount);

            if (used < 2) return false;

            // ✅ out int 클램프
            moneyMult100 = (moneySum > int.MaxValue) ? int.MaxValue : (moneySum < int.MinValue ? int.MinValue : (int)moneySum);
            proMult100 = (proSum > int.MaxValue) ? int.MaxValue : (proSum < int.MinValue ? int.MinValue : (int)proSum);
            forMult100 = (forSum > int.MaxValue) ? int.MaxValue : (forSum < int.MinValue ? int.MinValue : (int)forSum);
            instMult100 = (instSum > int.MaxValue) ? int.MaxValue : (instSum < int.MinValue ? int.MinValue : (int)instSum);

            // ✅ price/buy/sell: 분거래돈 가중평균
            if (sumW > 0)
                price100 = (int)Math.Round(sumWP / sumW);

            if (sumMinute > 0)
            {
                buyMult = (int)Math.Round(sumBuy / sumMinute);
                sellMult = (int)Math.Round(sumSell / sumMinute);
            }
            else
            {
                buyMult = 0;
                sellMult = 0;
            }


            return true;
        }


        private static bool TryComputeSectorRow_RealTimeUsed(
    List<StockData> members,
    int tMin,
    out int used,
    out double denomUsed_10M,
    out int price100,
    out int proMult100,
    out int forMult100,
    out int instMult100,
    out int moneyMult100,
    out int buyMult,
    out int sellMult)
        {
            used = 0;
            denomUsed_10M = 0;
            price100 = 0;

            proMult100 = 0;
            forMult100 = 0;
            instMult100 = 0;
            moneyMult100 = 0;

            buyMult = 0;
            sellMult = 0;

            if (members == null || members.Count < 2)
                return false;

            // ✅ 누적 돈 합산(오버플로 방지)
            long moneySum = 0;
            long proSum = 0;
            long forSum = 0;
            long instSum = 0;

            // ✅ price 안정화: 20일 평균거래대금 가중
            double sumW_price = 0.0;   // Σ wAvg
            double sumWP_price = 0.0;  // Σ wAvg * p100

            // ✅ buy/sell 반응: 분30거래천 가중 (그대로 유지)
            double sumW_bs = 0.0;      // Σ minuteMoney
            double sumBuy = 0.0;       // Σ minuteMoney * b
            double sumSell = 0.0;      // Σ minuteMoney * s

            foreach (var m in members)
            {
                if (m?.Post == null) continue;
                if (m.Api?.x == null) continue;
                if (m.Api.nrow <= 0) continue;

                var api = m.Api;
                var post = m.Post;

                int n = Math.Min(api.nrow, api.x.GetLength(0));
                if (n <= 0) continue;

                // ✅ rowNow: 해당 minute의 마지막 row
                //    분 경계 직후 -1이 나오면 마지막 유효 row로 fallback
                int rowNow = FindLastRowOfMinute(api.x, n, tMin);
                if (rowNow < 0)
                {
                    int last = n - 1;
                    while (last >= 0 && api.x[last, 0] == 0) last--;
                    if (last < 0) continue;
                    rowNow = last;
                }

                int p100 = api.x[rowNow, 1];

                // ===== 1) 누적 돈: Post 누적(천만원 단위) 그대로 합산 =====
                moneySum += (long)post.종누천;
                proSum += (long)post.프누천;
                forSum += (long)post.외누천;
                instSum += (long)post.기누천;

                // ===== 2) price100: AvgDailyTurnover_10M 가중 (안정) =====
                double wAvg = m.Statistics?.AvgDailyTurnover_10M ?? 0;
                if (wAvg > 0)
                {
                    denomUsed_10M += wAvg;

                    sumW_price += wAvg;
                    sumWP_price += wAvg * p100;
                }

                // ===== 3) buy/sell: 분30거래천 가중 (반응) =====
                double minuteMoney = post.분30거래천;
                if (minuteMoney < 0) minuteMoney = 0;

                int b = (int)post.분30매수배;
                int s = (int)post.분30매도배;

                if (minuteMoney > 0)
                {
                    sumW_bs += minuteMoney;
                    sumBuy += minuteMoney * b;
                    sumSell += minuteMoney * s;
                }

                used++;
            }

            if (used < 2)
                return false;

            // ✅ price: 안정 가중
            if (sumW_price > 0)
                price100 = (int)Math.Round(sumWP_price / sumW_price);
            else
                price100 = 0;

            // ✅ buy/sell: 분30거래천 가중 (분모 방어!)
            if (sumW_bs > 0)
            {
                buyMult = (int)Math.Round(sumBuy / sumW_bs);
                sellMult = (int)Math.Round(sumSell / sumW_bs);
            }
            else
            {
                buyMult = 0;
                sellMult = 0;
            }

            // ✅ 누적 돈 out int 클램프
            moneyMult100 = moneySum > int.MaxValue ? int.MaxValue :
                           moneySum < int.MinValue ? int.MinValue :
                           (int)moneySum;

            proMult100 = proSum > int.MaxValue ? int.MaxValue :
                         proSum < int.MinValue ? int.MinValue :
                         (int)proSum;

            forMult100 = forSum > int.MaxValue ? int.MaxValue :
                         forSum < int.MinValue ? int.MinValue :
                         (int)forSum;

            instMult100 = instSum > int.MaxValue ? int.MaxValue :
                          instSum < int.MinValue ? int.MinValue :
                          (int)instSum;

            return true;
        }




        public static void RebuildSectorRanks(IEnumerable<GroupData> groups)
        {
            if (groups == null) return;

            var list = groups
                .Where(d =>
                    d != null &&
                    d.UsedCount >= 2 &&
                    d.CoverRatio >= 0.80)
                .ToList();

            if (list.Count == 0) return;

            foreach (var d in list)
                d.ClearRanks();

            ApplyRank(list, d => d.MoneyMult100, (d, r) => d.SetRankMoney(r));
            ApplyRank(list, d => d.ProMult100, (d, r) => d.SetRankPro(r));
            ApplyRank(list, d => d.ForMult100, (d, r) => d.SetRankFor(r));
            ApplyRank(list, d => d.InstMult100, (d, r) => d.SetRankInst(r));
        }

        private static void ApplyRank(
            List<GroupData> list,
            Func<GroupData, int> selector,
            Action<GroupData, int> assign)
        {
            var ordered = list
                .Where(d => selector(d) > 0)
                .OrderByDescending(selector)
                .ToList();

            int rank = 1;
            foreach (var d in ordered)
                assign(d, rank++);
        }






        //private static int FindRowByTimeKey(int[,] x, int nrow, int colTime, int hhmmss)
        //{
        //    if (x == null || nrow <= 0) return -1;

        //    int last = Math.Min(nrow - 1, x.GetLength(0) - 1);
        //    for (int r = 0; r <= last; r++)
        //    {
        //        if (x[r, colTime] == hhmmss)
        //            return r;
        //    }
        //    return -1;
        //}

        //private static int SafeGet(int[,] x, int r, int c)
        //{
        //    if (x == null) return 0;
        //    if (r < 0 || r >= x.GetLength(0)) return 0;
        //    if (c < 0 || c >= x.GetLength(1)) return 0;
        //    return x[r, c];
        //}

        //// 0/NaN/Infinity 방어: 랭킹에서 밀리게 아주 작은 값으로
        //private static double SafeRankValue(double v)
        //{
        //    if (double.IsNaN(v) || double.IsInfinity(v)) return double.NegativeInfinity;
        //    return v;
        //}

        //private static double SignedRankKey(double v)
        //{
        //    if (double.IsNaN(v) || double.IsInfinity(v)) return double.NegativeInfinity;

        //    // 양수는 그대로
        //    if (v > 0) return v;

        //    // 0은 양수보다 뒤로 보내기 위해 아주 작은 음수
        //    if (v == 0) return -1e-12;

        //    // 음수는 더 뒤로: 음수는 그대로 두면 (예: -0.1 > -1.0 이므로 -0.1이 더 앞)
        //    // 여기서는 "덜 음수인 것(-0.1)이 덜 약한" 것이니 앞에 와도 됨.
        //    // 만약 "음수는 전부 최하위"로 보내고 싶으면 return -1e9 + v 같은 방식으로 더 밀어도 됨.
        //    return v;
        //}

        //private static double GetSectorNorm(GroupData d, int col)
        //{
        //    if (d == null) return 0;

        //    // 1) GroupData에서 섹터 StockData 찾기
        //    //    (grp.Title로 SECTOR:{Title} 키 구성)
        //    string title = d.Title;
        //    if (string.IsNullOrWhiteSpace(title)) return 0;

        //    string sectorKey = $"SECTOR:{title.Trim()}";
        //    var sectorSd = g.StockRepo.TryGetDataOrNull(sectorKey);
        //    if (sectorSd?.Api?.x == null || sectorSd.Api.nrow <= 0) return 0;

        //    // 2) last row
        //    int last = sectorSd.Api.nrow - 1;
        //    if (last < 0) return 0;

        //    // 3) 누적돈(10M) 값
        //    double v = sectorSd.Api.x[last, col];

        //    // 4) 20일평균(10M)
        //    double avg = sectorSd.Statistics?.AvgDailyTurnover_10M ?? 0;
        //    if (avg <= 0) avg = 1;

        //    return v / avg;
        //}

        private static void EnsureSectorMatrix(StockData sector)
        {
            if (sector.Api.x == null ||
                sector.Api.x.GetLength(0) != ROWS ||
                sector.Api.x.GetLength(1) != COLS)
            {
                sector.Api.x = new int[ROWS, COLS];
            }
        }

        private static int ResolveTargetRowByMinute(StockData sector, int tMin)
        {
            var api = sector.Api;
            var x = api.x;
            int n = Math.Min(api.nrow, x.GetLength(0));

            if (n <= 0)
            {
                WriteSeedDefault(sector);
                n = Math.Min(sector.Api.nrow, x.GetLength(0));
            }

            if (n == 1)
            {
                if (1 >= x.GetLength(0)) return -1;
                return 1;
            }

            int lastRow = n - 1;
            int lastTime = x[lastRow, 0];
            int lastMin = lastTime / 100;

            if (lastMin == tMin)
                return lastRow;

            if (lastMin < tMin)
            {
                if (n >= x.GetLength(0)) return -1;
                return n;
            }

            for (int r = 1; r < n; r++)
            {
                int tm = x[r, 0] / 100;
                if (tm == tMin) return r;
            }

            return -1;
        }



        private static void WriteRow(
    StockData sector, int row, int hhmmss,
    int price100, int proMult100, int forMult100, int instMult100, int moneyMult100,
    int buyMult, int sellMult)
        {
            var api = sector.Api;

            api.x[row, 0] = hhmmss;
            api.x[row, 1] = price100;

            api.x[row, 4] = proMult100;
            api.x[row, 5] = forMult100;
            api.x[row, 6] = instMult100;
            api.x[row, 7] = moneyMult100;

            api.x[row, 8] = buyMult;
            api.x[row, 9] = sellMult;
        }

        private static bool WriteSectorRowWithSeedAndReplaceAppend(
     StockData sector, int hhmmss, int tMin,
     int price100,
     int proMult100, int forMult100, int instMult100, int moneyMult100,
     int buyMult, int sellMult)
        {
            if (sector?.Api == null) return false;

            bool wrote = false;

            lock (sector)
            {
                EnsureSectorMatrix(sector);

                // ✅ Seed 보장 (row0 = 08:59:59)
                int seedMinute = sector.Api.x[0, 0] / 100;
                bool seedOk = (seedMinute == 859);

                if (sector.Api.nrow <= 0 || !seedOk)
                    WriteSeedDefault(sector);

                // ✅ OPEN_MIN 특수 처리:
                //   - 기존 코드는 "%100==0"이라 seed가 85959인 구조와 충돌 가능
                //   - "seed가 아직 85900 같은 더미"로 잡혀있을 때만 보정
                if (tMin == OPEN_MIN
                    && sector.Api.x[0, 0] / 100 == 859
                    && sector.Api.x[0, 0] % 100 == 0)
                {
                    WriteRow(sector, 0, SEED_TIME,
                        price100, proMult100, forMult100, instMult100, moneyMult100,
                        buyMult, sellMult);

                    // (기존 로직 유지) 동일 X 방지용 1초 증가
                    sector.Api.x[0, 0] += 1;
                    wrote = true;
                }

                // ✅ 대상 row 결정은 minuteKey로 (Replace/Append 정책 유지)
                int targetRow = ResolveTargetRowByMinute(sector, tMin);
                if (targetRow >= 0)
                {
                    // ✅ 핵심: timeKey는 tMin*100이 아니라 "실제 hhmmss"를 찍는다.
                    int hhmmssToWrite = hhmmss;

                    // 방어: 혹시 hhmmss가 0이거나 분이 안 맞으면 fallback
                    if (hhmmssToWrite <= 0)
                        hhmmssToWrite = tMin * 100 + 59;
                    else if (hhmmssToWrite / 100 != tMin)
                        hhmmssToWrite = tMin * 100 + 59;

                    WriteRow(sector, targetRow, hhmmssToWrite,
                        price100, proMult100, forMult100, instMult100, moneyMult100,
                        buyMult, sellMult);

                    if (targetRow >= sector.Api.nrow)
                        sector.Api.nrow = targetRow + 1;

                    wrote = true;
                }
            }

            return wrote;
        }


        private static void WriteSeedDefault(StockData sector)
        {
            var x = sector.Api.x;

            x[0, 0] = SEED_TIME;
            x[0, 1] = 0;
            x[0, 2] = 100;
            x[0, 3] = MULT_SCALE;

            for (int c = 4; c < COLS; c++)
                x[0, c] = 0;

            if (sector.Api.nrow < 1) sector.Api.nrow = 1;
        }




    }
}
