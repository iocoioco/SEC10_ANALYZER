using New_Tradegy;
using New_Tradegy.Library.Core;
using New_Tradegy.Library.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace New_Tradegy.Library.Listeners
{
    public static class MarketEyeBatchSelector
    {
        private static int repositoryOffset = 0;

        static readonly HashSet<string> _indexSet =
        new HashSet<string>(StringComparer.Ordinal); // 앱 시작 시 indexList 넣어두기

        public static List<string> Select200Batch(
            List<string> indexList, List<string> holding,
            List<string> interestedWithBid, List<string> interestedOnly,
            List<string> rankedStockList, int batchSize = 200)
        {
            // 고정: 지수(4) + 보유(~3) + 관심(~5) + mixed(82) ≈ 94
            // 회전: repo 나머지 ~100종/0.7초 → repo 400 미만이면 3~4회(≈3초)에 1바퀴
            if (batchSize <= 0) return new List<string>(0);

            // ✅ 0-1) indexSet 항상 최신화 (이 메서드가 책임)
            _indexSet.Clear();
            if (indexList != null)
                foreach (var s in indexList)
                    if (!string.IsNullOrEmpty(s)) _indexSet.Add(s);

            var repoAll = g.StockRepo.AllGeneralStockNames;
            var repoSet = new HashSet<string>(repoAll, StringComparer.Ordinal);

            

            var selected = new List<string>(batchSize);
            var picked = new HashSet<string>(StringComparer.Ordinal);

            bool TryAddStock(string s)
            {
                if (string.IsNullOrEmpty(s) || picked.Contains(s) || !repoSet.Contains(s) || _indexSet.Contains(s))
                    return false;
                picked.Add(s); selected.Add(s); return true;
            }

            bool TryAddIndex(string s)
            {
                if (string.IsNullOrEmpty(s) || picked.Contains(s)) return false;
                picked.Add(s); selected.Add(s); return true;
            }

            // 0) 인덱스/ETF
            if (indexList != null)
                foreach (var s in indexList)
                    if (selected.Count < batchSize) TryAddIndex(s);

            // 1) chart area 우선 — holding → interestedWithBid (mixed보다 먼저)
            void FillUntilFull(IEnumerable<string> src)
            {
                if (src == null) return;
                foreach (var s in src)
                {
                    if (selected.Count >= batchSize) break;
                    TryAddStock(s);
                }
            }

            FillUntilFull(holding);
            FillUntilFull(interestedWithBid);

            // 2) 지수 산출용 mixed (82)
            if (g.kospi_mixed?.stocks != null)
                foreach (var s in g.kospi_mixed.stocks)
                    if (selected.Count < batchSize) TryAddStock(s);

            if (g.kosdaq_mixed?.stocks != null)
                foreach (var s in g.kosdaq_mixed.stocks)
                    if (selected.Count < batchSize) TryAddStock(s);

            // 3) chart withoutBookBid (소수 — interestedOnly + ranking 현재 페이지)
            FillUntilFull(interestedOnly);
            if (rankedStockList != null)
                FillUntilFull(rankedStockList.Skip(g.gid));

            // 4) repo 회전 — 목표 ~ROTATION_TARGET종, batchSize(200)까지 채움
            if (selected.Count < batchSize && repoAll.Count > 0)
            {
                int scanned = 0;
                for (int i = 0; i < repoAll.Count && selected.Count < batchSize; i++, scanned++)
                    TryAddStock(repoAll[(repositoryOffset + i) % repoAll.Count]);

                if (scanned > 0)
                    repositoryOffset = (repositoryOffset + scanned) % repoAll.Count;
            }

            return selected;
        }
    }
}

