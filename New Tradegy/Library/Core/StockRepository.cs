using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using DSCBO1Lib;
using New_Tradegy.Library.Models;

namespace New_Tradegy.Library.Core
{
    public class StockRepository
    {
        private static StockRepository _instance = null;
        private static readonly object _lock = new object();

        private readonly Dictionary<string, StockData> _stockMap = new Dictionary<string, StockData>();

        private StockRepository() { }
        public IReadOnlyList<StockData> AllGeneralStocks { get; private set; }
        public IReadOnlyList<StockData> AllSectorStocks { get; private set; }
        public IReadOnlyList<string> AllGeneralStockNames { get; private set; }

        public void BuildAllGeneralCache()
        {
            AllGeneralStocks = _stockMap.Values
                .Where(d => d != null && d.Kind == StockData.InstrumentKind.Stock)
                .ToList();
        }

        public void BuildAllSectorCache()
        {
            AllSectorStocks = _stockMap.Values
                .Where(d => d != null && d.Kind == StockData.InstrumentKind.Sector)
                .ToList();
        }

        
        public void BuildAllGeneralNamesCache()
        {
            var list = _stockMap.Values
                .Where(d => d != null && d.Kind == StockData.InstrumentKind.Stock)
                .ToList();

            AllGeneralStocks = list;
            AllGeneralStockNames = list
                .Select(d => d.Stock)
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
        }
        public void ClearAll()
        {
            lock (_lock)
            {
                _stockMap.Clear(); // 네 실제 필드명(stockMap) 그대로
            }
        }
        public static StockRepository Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                            _instance = new StockRepository();
                    }
                }
                return _instance;
            }
        }

        // ✅ null 제외한 전체 데이터
        public IEnumerable<StockData> AllDatas
            => _stockMap.Values.Where(d => d != null);

        // ✅ Kind 기반 분리 (정답 라인)
        public IEnumerable<StockData> Stocks()
            => _stockMap.Values.Where(d => d != null && d.Kind == StockData.InstrumentKind.Stock);

        public IEnumerable<StockData> Sectors()
            => _stockMap.Values.Where(d => d != null && d.Kind == StockData.InstrumentKind.Sector);

        public IEnumerable<StockData> Indices()
            => _stockMap.Values.Where(d => d != null && d.Kind == StockData.InstrumentKind.Index);

        // ✅ 너가 원하는 "일반 종목만"
        //public IEnumerable<StockData> AllGeneralDatas
        //    => Stocks();

        public void AddOrUpdate(string stock, StockData data)
        {
            if (string.IsNullOrWhiteSpace(stock))
                return;

            if (data == null)
            {
                _stockMap[stock] = null;
                return;
            }

            data.Stock = stock;

            // ✅ Kind 자동 판별(최소 안전장치)
            if (stock.StartsWith("SECTOR:"))
                data.Kind = StockData.InstrumentKind.Sector;
            else if (stock.StartsWith("KODEX"))
                data.Kind = StockData.InstrumentKind.Index;
            else
                data.Kind = StockData.InstrumentKind.Stock;

            _stockMap[stock] = data;
        }

        public void RemoveStock(string key)
        {
            if (key == null) return;
            if (_stockMap.ContainsKey(key))
                _stockMap.Remove(key);
        }

        public bool TryGet(string stock, out StockData data)
        {
            return _stockMap.TryGetValue(stock, out data);
        }

        public StockData TryGetDataOrNull(string stock)
        {
            if (stock == null) return null;
            StockData data;
            if (_stockMap.TryGetValue(stock, out data))
                return data;
            return null;
        }

        public bool Contains(string stock)
        {
            return stock != null && _stockMap.ContainsKey(stock);
        }

        // 지금 Find는 "code"라는 이름인데 Stock(이름)으로 찾고 있음 → 명확히
        public StockData FindByStockName(string stockName)
        {
            if (string.IsNullOrWhiteSpace(stockName)) return null;
            return AllDatas.FirstOrDefault(d => d.Stock == stockName);
        }


        public void RemoveAllSectorStocks()
        {
            lock (_lock)
            {
                var keys = _stockMap
                    .Where(kv =>
                        kv.Value != null &&
                        (
                            kv.Value.Kind == StockData.InstrumentKind.Sector ||
                            kv.Key.StartsWith("SECTOR:")
                        ))
                    .Select(kv => kv.Key)
                    .ToList();

                foreach (var key in keys)
                    _stockMap.Remove(key);

                BuildAllSectorCache();
                BuildAllGeneralCache();
                BuildAllGeneralNamesCache();
            }
        }
    }


}
