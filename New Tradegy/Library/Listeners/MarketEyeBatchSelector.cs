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
            const int EXTRA_FROM_PRIORITY = 30;
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

            // 0) 인덱스/ETF 먼저 강제 포함
            if (indexList != null)
                foreach (var s in indexList)
                    if (selected.Count < batchSize) TryAddIndex(s);

            // 1) mixed 포함(종목만)
            if (g.kospi_mixed?.stocks != null)
                foreach (var s in g.kospi_mixed.stocks)
                    if (selected.Count < batchSize) TryAddStock(s);

            if (g.kosdaq_mixed?.stocks != null)
                foreach (var s in g.kosdaq_mixed.stocks)
                    if (selected.Count < batchSize) TryAddStock(s);

            // 2) mixed 이후 “종목만” +30 확장
            int baseCount = selected.Count;
            int target = Math.Min(batchSize, baseCount + EXTRA_FROM_PRIORITY);

            void Fill(IEnumerable<string> src)
            {
                if (src == null) return;
                foreach (var s in src)
                {
                    if (selected.Count >= target) break;
                    TryAddStock(s);
                }
            }

            Fill(holding);
            Fill(interestedWithBid);
            Fill(interestedOnly);
            Fill(rankedStockList);

            // 3) 남는 자리는 repo 회전(종목만)  ✅ offset은 "실제 추가된 수"로 갱신
            if (selected.Count < batchSize && repoAll.Count > 0)
            {
                int startCount = selected.Count;

                for (int i = 0; i < repoAll.Count && selected.Count < batchSize; i++)
                    TryAddStock(repoAll[(repositoryOffset + i) % repoAll.Count]);

                int addedCount = selected.Count - startCount;
                if (addedCount > 0)
                    repositoryOffset = (repositoryOffset + addedCount) % repoAll.Count;
            }

            return selected;
        }
    }
}

