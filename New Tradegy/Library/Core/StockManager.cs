using System;
using System.Collections.Generic;
using System.Linq;
using New_Tradegy.Library.Core;
using New_Tradegy.Library.Models;

namespace New_Tradegy.Library.Core
{
    // C# 7.3 호환 (target-typed new() 제거 + expression-bodied property로 즉시 계산)

    public class StockManager
    {
        private readonly StockRepository _repository;
        public StockRepository Repository { get { return _repository; } }

        public List<string> StockRankingList { get; private set; } = new List<string>();
        public List<string> HoldingList { get; private set; } = new List<string>();


        public List<string> InterestedWithBidList { get; private set; } = new List<string>();
       public List<string> InterestedOnlyList { get; private set; } = new List<string>();
        public List<string> InterestedInFile { get; private set; } = new List<string>();

        public string Active { get; set; } = "";
        public string Next { get; set; } = "";

        // (선택) 인덱스 내부 분류용 고정 리스트
        public IReadOnlyList<string> LeverageList { get; }
        public IReadOnlyList<string> InverseList { get; }



        // ✅ 즉시 계산되는 프로퍼티(캐시 없음)
        public IEnumerable<string> IndexList
        {
            get
            {
                return _repository.Indices()
                    .Select(d => d.Stock)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct();
            }
        }

        public IEnumerable<string> SectorList
        {
            get
            {
                return _repository.Sectors()
                    .Select(d => d.Stock)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct();
            }
        }


        // (이거 지금 0 reference라면 제거해도 됨)
        public Dictionary<string, List<string>> Groups { get; private set; } = new Dictionary<string, List<string>>();

        public StockManager(StockRepository repository)
        {
            _repository = repository;

            LeverageList = new List<string> { "KODEX 레버리지", "KODEX 코스닥150레버리지" };
            InverseList = new List<string> { "KODEX 200선물인버스2X", "KODEX 코스닥150선물인버스" };
        }









        // (선택) 지수창을 4개로 고정해서 쓰고 싶을 때
        public IEnumerable<string> FixedIndexList4 =>
            LeverageList.Concat(InverseList);







        /// <summary>
        /// 자동 신호/큐 처리용: InterestedWithBid에 반드시 포함되도록 보장(Add-if-missing).
        /// Toggle 금지.
        /// </summary>
        public bool EnsureInterestedWithBid(string stock)
        {
            if (string.IsNullOrWhiteSpace(stock)) return false;

            // 이미 있으면 아무 것도 하지 않음
            if (g.StockManager.InterestedWithBidList.Contains(stock))
                return false;

            // 없으면 추가
            g.StockManager.InterestedWithBidList.Add(stock);
            return true; // 변경 발생
        }

        /// <summary>
        /// 자동 신호/큐 처리용: 필요 시 제거(Remove-if-exists).
        /// </summary>
        public bool RemoveInterestedWithBid(string stock)
        {
            if (string.IsNullOrWhiteSpace(stock)) return false;

            if (!g.StockManager.InterestedWithBidList.Contains(stock))
                return false;

            g.StockManager.InterestedWithBidList.Remove(stock);
            return true;
        }





        
    }
}
