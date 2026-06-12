using CPTRADELib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using New_Tradegy.Library;
using New_Tradegy.Library.Core;
using New_Tradegy.Library.Listeners;
using New_Tradegy.Library.Deals;
using System.Collections;

namespace New_Tradegy.Library.Deals
{
    public class Order
    {
        public string Stock { get; set; }
        public int Price { get; set; }
        public int Quantity { get; set; }
        public int UrgencyLevel { get; set; }

        public Order(string stock, int price, int quantity, int urgencyLevel)
        {
            Stock = stock;
            Price = price;
            Quantity = quantity;
            UrgencyLevel = urgencyLevel;
        }
    }

    public class StockExchange
    {
        public static List<Order> buyOrders = new List<Order>();
        public static List<Order> sellOrders = new List<Order>();
        public static readonly object _bookBidLock = new object(); // Make sure it's initialized

        private static StockExchange _instance;
        public static StockExchange Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new StockExchange();
                return _instance;
            }
        }

        private Dictionary<string, IBookBidGenerator> _generators
    = new Dictionary<string, IBookBidGenerator>();

        public IBookBidGenerator GetOrCreateGenerator(string stock)
        {
            if (!_generators.TryGetValue(stock, out var generator))
            {
                generator = new BookBidGeneratorStock(stock, this);  // ✅ Correct use of `this` as StockExchange
                _generators[stock] = generator;
            }
            return generator;
        }



        private StockExchange() { }

        
        public void MonitorPrices(string stock, int sellHogaVolume, int sellHogaPrice, int buyHogaVolume, int buyHogaPrice)
        {
            foreach (var order in new List<Order>(buyOrders))
            {
                if (order.Stock == stock && order.Price == sellHogaPrice)
                {
                    var data = g.StockRepo.TryGetDataOrNull(stock);
                    if (data == null)
                        return;

                    string str = order.Stock + " : " + order.Price + " X " + order.Quantity +
                                 " = " + (order.Price * order.Quantity / 10000) + "만원";
                    str += "\n" + Utils.StringUtils.r3_display_매수_매도(data);

                    RemoveOrder(buyOrders, order);

                    lock (_bookBidLock)
                    {
                        if (!g.BookBidInstances.ContainsKey(stock))
                        {
                            var generator = new BookBidGeneratorStock(stock, StockExchange.Instance);  // or whatever logic you use
                            g.BookBidInstances.Add(stock, generator);
                            generator.OpenOrUpdateConfirmationForm(false, order.Stock, order.Quantity, order.Price, order.UrgencyLevel, str);
                        }
                   
                    }
                    
                }
            }

            foreach (var order in new List<Order>(sellOrders))
            {
                if (order.Stock == stock && order.Price == buyHogaPrice)
                {
                    var data = g.StockRepo.TryGetDataOrNull(stock);
                    if (data == null)
                        return;

                    string str = order.Stock + " : " + order.Price + " X " + order.Quantity +
                                 " = " + (order.Price * order.Quantity / 10000) + "만원";
                    str += "\n" + Utils.StringUtils.r3_display_매수_매도(data);

                    RemoveOrder(sellOrders, order);

                    var a = new BookBidGeneratorStock(stock, StockExchange.Instance); // ✅ Correct
                    a.OpenOrUpdateConfirmationForm(true, order.Stock, order.Quantity, order.Price, order.UrgencyLevel, str);
                }
            }
        }

        public void AddBuyOrder(string stock, int price, int quantity, int urgencyLevel)
        {
            if (DealManager.CheckPreviousLoss(stock))
                return;

            var existingOrder = buyOrders.Find(order => order.Stock == stock && order.Price == price);
            if (existingOrder != null)
                buyOrders.Remove(existingOrder);

            buyOrders.Add(new Order(stock, price, quantity, urgencyLevel));
        }

        public void AddSellOrder(string stock, int price, int quantity, int urgencyLevel)
        {
            var existingOrder = sellOrders.Find(order => order.Stock == stock && order.Price == price);
            if (existingOrder != null)
                sellOrders.Remove(existingOrder);

            sellOrders.Add(new Order(stock, price, quantity, urgencyLevel));
        }

        private bool IsSuddenDrop(int sellHogaPrice, int buyHogaPrice)
        {
            return (sellHogaPrice - buyHogaPrice) > TickDifference(sellHogaPrice);
        }

        private int TickDifference(double x)
        {
            if (x < 2000) return 1;
            if (x < 5000) return 5;
            if (x < 20000) return 10;
            if (x < 50000) return 50;
            if (x < 200000) return 100;
            if (x < 500000) return 500;
            return 1000;
        }

        private bool IsHogaGapAcceptable(int sellHogaPrice, int buyHogaPrice)
        {
            double priceDifference = Math.Abs(sellHogaPrice - buyHogaPrice);
            int allowedTickDifference = TickDifference(Math.Min(sellHogaPrice, buyHogaPrice));
            return priceDifference <= allowedTickDifference;
        }

        private void RemoveOrder(List<Order> orderList, Order order)
        {
            orderList.Remove(order);
        }
    }
}
