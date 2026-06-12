using New_Tradegy.Library.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace New_Tradegy.Library.PostProcessing
{


    public static class ScoreRankEngine
    {
        static readonly ScoreKey[] SCORE_KEYS =
        {
            ScoreKey.CumMoney_10M,
            ScoreKey.CumPro_10M,
            ScoreKey.CumFor_10M,
            ScoreKey.CumInt_10M,
            ScoreKey.MinMoney_10M,
            ScoreKey.MinPro_10M,
            ScoreKey.MinFor_10M,
            ScoreKey.MinDiff,
            ScoreKey.MinSum
        };

        public static void Build(IEnumerable<StockData> items, IReadOnlyList<ScoreKey> keys)
        {
            if (items == null) return;
            var list = items as List<StockData> ?? items.ToList();
            if (list.Count == 0) return;

            foreach (var sd in list)
                ScoreBuilder.FillRaw_FromPost(sd);

            foreach (var key in keys)
                RankUtils.BuildRankAndPctMax_PositiveOnly(list, key);
        }
    }

    public static class ScoreBuilder
    {
        public static void FillRaw_FromPost(StockData sd)
        {
            if (sd == null) return;

            var p = sd.Post;
            if (p == null) return;

            if (sd.Score == null)
                sd.Score = new ScoreData();

            var s = sd.Score;

            double avg20 = sd.Statistics?.AvgDailyTurnover_10M ?? 0;
            if (avg20 <= 0) avg20 = 1;   // 방어

            // ================================
            // 누적 (정규화)
            // ================================

            s.Raw[ScoreKey.CumMoney_10M] = (double)p.종누천 / avg20;
            s.Raw[ScoreKey.CumPro_10M] = p.프누천 / avg20;
            s.Raw[ScoreKey.CumFor_10M] = p.외누천 / avg20;
            s.Raw[ScoreKey.CumInt_10M] = p.기누천 / avg20;

            // ================================
            // 분30 돈계열 (정규화)
            // ================================

            s.Raw[ScoreKey.MinMoney_10M] = p.분30거래천 / avg20;
            s.Raw[ScoreKey.MinPro_10M] = p.분30프로천 / avg20;
            s.Raw[ScoreKey.MinFor_10M] = p.분30외인천 / avg20; 

            // ================================
            // 배수계열 (이미 상대값)
            // ================================

            s.Raw[ScoreKey.MinDiff] = p.분30배수차;
            s.Raw[ScoreKey.MinSum] = p.분30배수합;
        }
    }

    public static class RankUtils
    {
        public static void BuildRankAndPctMax_PositiveOnly(
            IReadOnlyList<StockData> items,
            ScoreKey key)
        {
            if (items == null || items.Count == 0) return;

            double max = 0.0;

            // 1) 양수 max 찾기
            for (int i = 0; i < items.Count; i++)
            {
                var s = items[i]?.Score;
                if (s == null) continue;

                double v = s.GetRaw(key);          // ✅ double
                if (v > max) max = v;
            }

            // 2) 정렬용 리스트
            var list = new List<(StockData sd, double v)>(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                var sd = items[i];
                var s = sd?.Score;
                if (s == null) continue;

                double v = s.GetRaw(key);
                list.Add((sd, v));
            }

            // 큰 값이 1등
            list.Sort((a, b) => b.v.CompareTo(a.v));

            // 3) Rank + PctMax
            int rank = 0;
            double prev = double.NaN;

            for (int i = 0; i < list.Count; i++)
            {
                var (sd, v) = list[i];
                var s = sd.Score;

                // PositiveOnly니까 0 이하는 전부 0 처리
                if (v <= 0)
                {
                    s.Rank[key] = 0;
                    s.PctMax[key] = 0;
                    continue;
                }

                // ✅ 동점 처리: 값이 같으면 같은 등수
                if (i == 0 || v != prev) rank = i + 1;
                prev = v;

                s.Rank[key] = rank;

                // max 대비 %
                double pct = (max > 0.0) ? (100.0 * v / max) : 0.0;
                s.PctMax[key] = pct;
            }
        }
    }




    /// <summary>
    /// 섹터의 Post(스냅샷)만 갱신한다.
    /// - 매 호출마다 0 초기화 후 재계산(덮어쓰기/누적 오염 방지)
    /// - api.x는 다음 단계(SectorApiChartUpdater)에서 처리
    /// </summary>
    public static class SectorPostBuilder
    {
        public static void UpdateSectorPosts()
        {
            if (g.GroupManager?.Groups == null || g.GroupManager.Groups.Count == 0)
                return;

            foreach (var grp in g.GroupManager.Groups)
            {
                if (grp == null) continue;
                if (string.IsNullOrWhiteSpace(grp.Title)) continue;
                if (grp.Stocks == null || grp.Stocks.Count < 2) continue;

                // 1) members 수집
                var members = CollectMembers(grp.Stocks);
                if (members.Count < 2) continue;

                // 2) sector 확보
                var sector = GetOrCreateSectorStock(grp.Title);
                if (sector == null) continue;

                // 3) Post 스냅샷 계산
                ComputeSectorPostSnapshot(members, sector);

                // 4) (선택) sector Statistics.AvgDailyTurnover_10M 업데이트(섹터 분모)
                UpdateSectorAvg20(members, sector);
            }
        }

        // ---------------------------
        // core
        // ---------------------------

        private static void ComputeSectorPostSnapshot(
            List<StockData> members,
            StockData sector
            )
        {
            if (sector.Post == null)
                sector.Post = new PostData(); // 너의 Post 타입에 맞게

            var sp = sector.Post;

            // ✅ 핵심: 매 호출마다 0으로 초기화 후 재계산 (스냅샷)
            ClearSectorPost(sp);

            double sumMinMoney = 0;   // Σ 분30거래천
            double sumMinPro = 0;     // Σ 분30프로천
            double sumMinFor = 0;     // Σ 분30외인천

            double sumW_trade = 0;    // Σ w (w=분30거래천)
            double sumPrice = 0;
            double sumDiffW = 0;      // Σ (분30배수차*w)
            double sumSumW = 0;       // Σ (분30배수합*w)
            double sumBuyW = 0;       // Σ (분30매수배*w)  (옵션)
            double sumSellW = 0;      // Σ (분30매도배*w)  (옵션)

            double sumW_avg20 = 0;    // Σ wa (wa=Avg20거래천)
            double sumPriceW = 0;     // Σ (price100*wa)
            double sumPriceDiffW = 0; // Σ (분30가격차*wa)

            foreach (var m in members)
            {
                var p = m.Post;
                if (p == null) continue;

                // --------------------
                // 누적(합)
                // --------------------
                sp.프누천 += p.프누천;
                sp.외누천 += p.외누천;
                sp.기누천 += p.기누천;
                sp.종누천 += p.종누천;

                // --------------------
                // 분30(합)
                // --------------------
                sumMinMoney += p.분30거래천;
                sumMinPro += p.분30프로천;
                sumMinFor += p.분30외인천;

                // --------------------
                // 분30 배수계열: 분30거래천 가중
                // --------------------
                double w = p.분30거래천;
                if (w > 0)
                {
                    sumDiffW += p.분30배수차 * w;
                    sumSumW += p.분30배수합 * w;

                    // 아래 2개는 나중에 안 쓰더라도 만들어두는 방향이면 포함
                    sumBuyW += p.분30매수배 * w;
                    sumSellW += p.분30매도배 * w;

                    sumW_trade += w;
                }

                // --------------------
                // 가격/가격차: Avg20 가중
                // --------------------
                double wa = m.Statistics?.AvgDailyTurnover_10M ?? 0; // 천만원
                if (wa > 0)
                {
                    sumPriceW += p.분30의가격 * wa;
                    sumPriceDiffW += p.분30가격차 * wa;
                    sumW_avg20 += wa;
                }
            }

            // --------------------
            // 합 결과 반영
            // --------------------
            sp.분30거래천 = sumMinMoney;
            sp.분30프로천 = sumMinPro;
            sp.분30외인천 = sumMinFor;

            // 프퍼(프로/거래)
            sp.분30프퍼 = sumMinMoney > 0
                ? 100.0 * sumMinPro / sumMinMoney
                : 0.0;

            // 배수계열(가중평균)
            if (sumW_trade > 0)
            {
                sp.분30배수차 = sumDiffW / sumW_trade;
                sp.분30배수합 = sumSumW / sumW_trade;
                sp.분30매수배 = sumBuyW / sumW_trade;
                sp.분30매도배 = sumSellW / sumW_trade;
            }
            else
            {
                sp.분30배수차 = 0;
                sp.분30배수합 = 0;
                sp.분30매수배 = 0;
                sp.분30매도배 = 0;
            }

            // 가격/가격차(가중평균)
            if (sumW_avg20 > 0)
            {
                sp.분30의가격 = sumPriceW / sumW_avg20;
                sp.분30가격차 = sumPriceDiffW / sumW_avg20;
            }
            else
            {
                sp.분30의가격 = 0;
                sp.분30가격차 = 0;
            }
        }

        // ---------------------------
        // helpers
        // ---------------------------

        private static List<StockData> CollectMembers(IEnumerable<string> stockNames)
        {
            var list = new List<StockData>();

            foreach (var name in stockNames)
            {
                if (string.IsNullOrWhiteSpace(name)) continue;

                var sd = g.StockRepo.TryGetDataOrNull(name);
                if (sd == null) continue;
                if (sd.Post == null) continue;          // ✅ Post 기반 계산이므로 Post 없는 애는 제외
                if (sd.Statistics == null) continue;    // Avg20 가중용
                list.Add(sd);
            }

            return list;
        }

        private static StockData GetOrCreateSectorStock(string title)
        {
            var sectorName = title.Trim();
            if (string.IsNullOrEmpty(sectorName))
                return null;

            string key = $"SECTOR:{sectorName}";
            var sector = g.StockRepo.TryGetDataOrNull(key);

            if (sector == null)
            {
                sector = new StockData
                {
                    Stock = key,
                    Api = new ApiData(),
                    Statistics = new StatisticsData(),
                    Post = new PostData(),
                };
                g.StockRepo.AddOrUpdate(key, sector);
            }

            if (sector.Api == null) sector.Api = new ApiData();
            if (sector.Statistics == null) sector.Statistics = new StatisticsData();
            if (sector.Post == null) sector.Post = new PostData();

            return sector;
        }

        private static void UpdateSectorAvg20(List<StockData> members, StockData sector)
        {
            double sum = 0;
            foreach (var m in members)
            {
                double a = m.Statistics?.AvgDailyTurnover_10M ?? 0;
                if (a > 0) sum += a;
            }
            if (sum <= 0) sum = 1;

            sector.Statistics.AvgDailyTurnover_10M = (int)Math.Round(sum);
        }

        /// <summary>
        /// 섹터 Post를 "스냅샷"으로 만들기 위해 매 호출마다 필드를 0으로 초기화한다.
        /// (여기서 필요한 필드만 초기화하면 됨)
        /// </summary>
        private static void ClearSectorPost(PostData sp)
        {
            // 누적
            sp.프누천 = 0;
            sp.외누천 = 0;
            sp.기누천 = 0;
            sp.종누천 = 0;

            // 분30
            sp.분30거래천 = 0;
            sp.분30프로천 = 0;
            sp.분30외인천 = 0;

            sp.분30프퍼 = 0;

            // 배수/강도
            sp.분30매수배 = 0;
            sp.분30매도배 = 0;
            sp.분30배수차 = 0;
            sp.분30배수합 = 0;

            // 가격계열
            sp.분30의가격 = 0;
            sp.분30가격차 = 0;

            // (추가 필드 있으면 여기서 더 0 처리)
        }

        // 멤버 price100 가져오기: 네 실제 구조에 맞춰 한 군데에서만 조정 가능하도록 helper로 뺐다.
        private static int GetMemberPrice100(StockData m)
        {
            // 예1) StockData에 price100 필드가 있으면:
            // return m.price100;

            // 예2) Api.x[lastRow,1]이 price100이면:
            var api = m.Api;
            if (api?.x == null || api.nrow <= 0) return 0;
            int lastRow = api.nrow - 1;
            if (lastRow < 0) return 0;
            return api.x[lastRow, 1];

            // 네 실제 기준으로 고쳐줘 friend.
        }

        private static void SetSectorPrice100(StockData sector, double px100)
        {
            // StockData에 price100 필드가 있으면:
            // sector.price100 = (int)Math.Round(px100);

            // 없으면 Post에 저장하는 방식이면:
            // sector.Post.가격100 = (int)Math.Round(px100);

            // 지금은 placeholder: Api / StockData 구조에 맞춰 한 줄만 바꿔서 쓰자.
            // (일단 아무 것도 안 하면 컴파일 에러 나니까, 임시로 Post에 넣는다고 가정)
            // sector.Post.가격100 = (int)Math.Round(px100);

            // ✅ 네 프로젝트에 맞는 1줄로 교체해줘.
        }
    }


    public static class SectorApiChartUpdater
    {
        // 차트용: api.x[row,*] 갱신 (append or replace)
        public static void UpdateSectorApiX(int hhmmss)
        {
            var sectors = g.StockRepo.AllSectorStocks;
            if (sectors == null || sectors.Count == 0) return;

            foreach (var sector in sectors)
            {
                if (sector?.Api?.x == null) continue;
                if (sector.Post == null) continue;
                if (sector.Statistics == null) continue;

                lock (sector) // sector.Api.x row 조작이므로 lock
                {
                    UpdateOne(sector, hhmmss);
                }
            }
        }

        private static void UpdateOne(StockData sector, int hhmmss)
        {
            var api = sector.Api;
            var sp = sector.Post;

            // 분모(섹터 20일 평균 거래대금 합, 천만원)
            double sectorAvg20_10M = sector.Statistics.AvgDailyTurnover_10M;
            if (sectorAvg20_10M <= 0) sectorAvg20_10M = 1;

            // append / replace 결정
            int lastRow = api.nrow - 1;

            int lastHHMM = api.x[lastRow, 0] / 100;
            int nowHHMM = hhmmss / 100;

            bool append = (lastRow < 0 || lastHHMM != nowHHMM);

            int row;
            if (append)
            {
                row = api.nrow;
                // 배열 상한 방어 (네 고정 ROWS 쓰면 그 상수로)
                int maxRow = api.x.GetLength(0);
                if (row >= maxRow)
                {
                    // 꽉 찼으면 마지막 row에 replace (혹은 shift 정책)
                    row = maxRow - 1;
                    api.nrow = maxRow;
                    append = false;
                }
                else
                {
                    api.nrow++;
                }
            }
            else
            {
                row = lastRow;
            }

            // ✅ 가격: 네가 SectorPostBuilder에서 만든 값을 쓰는 게 가장 안정적
            // (없으면 sector.Api 현재값/계산값으로 대체)
            int price100 = (int)sp.분30의가격;        // 없으면 필드명 맞춰서 수정
            if (price100 == 0) price100 = api.x[Math.Max(0, lastRow), 1];

            // ✅ 누적 멀티(스케일 100000)
            //int proMult100 = (int)(sp.프누천 / sectorAvg20_10M * 100000);
            //int forMult100 = (int)(sp.외누천 / sectorAvg20_10M * 100000);
            //int instMult100 = (int)(sp.기누천 / sectorAvg20_10M * 100000);
            //int moneyMult100 = (int)(sp.종누천 / sectorAvg20_10M * 100000);

            // ✅ 매수/매도배 (분30 기반이면 그걸 그대로)
            int buyMult = (int)sp.분30매수배;
            int sellMult = (int)sp.분30매도배;

            // ✅ WriteRow 포맷 그대로 기록
            api.x[row, 0] = hhmmss;
            api.x[row, 1] = price100;

            api.x[row, 4] = (int)sp.프누천;
            api.x[row, 5] = (int)sp.외누천;
            api.x[row, 6] = (int)sp.기누천;
            api.x[row, 7] = (int)sp.종누천;

            api.x[row, 8] = buyMult;
            api.x[row, 9] = sellMult;
        }
    }
}

