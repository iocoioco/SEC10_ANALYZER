using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace New_Tradegy.Library
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Diagnostics;
    using System.IO;

    class ProgramLossTrade
    {
        private decimal lastBestBid;
        private decimal lastBestAsk;
        private int lastBestBidSize;
       
        private bool orderPlaced = false;
        private Dictionary<string, decimal> programNetBuy = new Dictionary<string, decimal>(); // Program losses per stock
        private Dictionary<string, decimal> avgDailyDealt = new Dictionary<string, decimal>(); // 20-day average daily dealt amount
        private List<int> recentBidSizes = new List<int>(); // Track recent bid sizes
        private decimal largeBidThresholdFactor = 3.0m; // Buy wall must be 3x recent bid size

        public void OnBookUpdate(string stock, decimal bestBid, decimal bestAsk, int bidSize, int askSize)
        {
            lastBestBid = bestBid;
            lastBestAsk = bestAsk;
            lastBestBidSize = bidSize;

            //Console.WriteLine($"[{stock}] Market Update - Best Bid: {bestBid} (Size: {bidSize}), Best Ask: {bestAsk}");

            if (!recentBidSizes.Contains(bidSize)) recentBidSizes.Add(bidSize);
            if (recentBidSizes.Count > 10) recentBidSizes.RemoveAt(0); // Keep last 10 bid sizes

            DetectLargeBuyWall(stock);
        }

        public void OnProgramTradeUpdate(string stock, decimal programBuyAmount, decimal programSellAmount)
        {
            if (!programNetBuy.ContainsKey(stock)) programNetBuy[stock] = 0;
            programNetBuy[stock] += (programBuyAmount - programSellAmount);

            //Console.WriteLine($"[{stock}] Program Net Buy Today: {programNetBuy[stock]} KRW");

            RankStocksByLoss();
        }

        private void RankStocksByLoss()
        {
            var rankedLosses = programNetBuy
                .Where(stock => avgDailyDealt.ContainsKey(stock.Key)) // Ensure we have data
                .Select(stock => new
                {
                    Stock = stock.Key,
                    RelativeLoss = stock.Value / avgDailyDealt[stock.Key]
                })
                .OrderBy(stock => stock.RelativeLoss) // Most negative loss first
                .Take(5) // Watch top 5 losing stocks
                .ToList();

            //Console.WriteLine("📉 Top 5 Biggest Program Losers (Relative Loss):");
            foreach (var stock in rankedLosses)
            {
                //Console.WriteLine($"[{stock.Stock}] Loss Ratio: {stock.RelativeLoss:P2}");
            }
        }

        private void DetectLargeBuyWall(string stock)
        {
            if (!programNetBuy.ContainsKey(stock) || !avgDailyDealt.ContainsKey(stock)) return;
            var rankedLosses = programNetBuy
                .Where(s => avgDailyDealt.ContainsKey(s.Key))
                .Select(s => new
                {
                    Stock = s.Key,
                    RelativeLoss = s.Value / avgDailyDealt[s.Key]
                })
                .OrderBy(s => s.RelativeLoss)
                .Take(5)
                .Select(s => s.Stock)
                .ToHashSet();

            if (!rankedLosses.Contains(stock)) return; // Ignore if not in top 5 losers

            decimal avgRecentBidSize = recentBidSizes.Count > 0 ? (decimal)recentBidSizes.Sum() / recentBidSizes.Count : 0;

            if (lastBestBidSize >= avgRecentBidSize * largeBidThresholdFactor)
            {
                //Console.WriteLine($"🚀 [{stock}] Large Buy Wall Detected at {lastBestBid} (Size: {lastBestBidSize})");
                PlaceDefensiveBuyOrder(stock);
            }
        }

        private void PlaceDefensiveBuyOrder(string stock)
        {
            if (orderPlaced) return;

            decimal orderPrice = lastBestBid; // Buy at defended price

            //Console.WriteLine($"✅ [{stock}] Placing Limit Buy Order at: {orderPrice}");

            orderPlaced = true;

            File.AppendAllText("trade_log.txt", $"{DateTime.Now}: Buy {stock} at {orderPrice}\n");

            // Simulate order execution (Replace with actual API call)
        }
    }

}
