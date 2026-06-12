using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using New_Tradegy.Library.Deals;
using New_Tradegy.Library.Listeners;
using New_Tradegy.Library.Models;
using New_Tradegy.Library.Utils;

namespace New_Tradegy.Library.Trackers
{
    public class PendingBookBid
    {
        public string Stock { get; }
        public DataGridView Dgv { get; }
        public bool IsSuspended { get; private set; }

        public PendingBookBid(string stock, DataGridView dgv)
        {
            Stock = stock;
            Dgv = dgv;
            IsSuspended = false;
        }

        public void Suspend() => IsSuspended = true;
        public void Resume() => IsSuspended = false;
    }

    public class BookBidManager
    {
        private readonly Dictionary<string, IBookBidGenerator> _jpMap = new Dictionary<string, IBookBidGenerator>();
        private readonly Dictionary<string, DataGridView> _gridMap = new Dictionary<string, DataGridView>();
        private static readonly object _bookBidLock = new object();

        private Timer _monitorTimer;

        public void StartMonitorLoop()
        {
            //Console.WriteLine("⚠️ StartMonitorLoop is temporarily suspended.");
            //return;

            if (_monitorTimer != null)
            {
                _monitorTimer.Stop();
                _monitorTimer.Dispose();
                _monitorTimer = null;
                //Console.WriteLine("🛑 MonitorLoop has been stopped.");
            }

            _monitorTimer = new Timer { Interval = 10000 };
            _monitorTimer.Tick += (s, e) =>
            {
                foreach (var kv in g.BookBidInstances.ToList())
                {
                    var stock = kv.Key;
                    var instance = kv.Value as IBookBidGenerator;
                    if (instance == null)
                        continue;
                    //if (kv.Value is not BookBidGenerator instance) 
                    //    continue;

                    if ((DateTime.Now - instance.LastReceivedTime).TotalSeconds > 60)
                    {
                        //Console.WriteLine($"\u26d4 BookBid 멈춤: {stock} \u2192 재구독 시도");

                        instance.Unsubscribe();

                        DataGridView dgv;
                        lock (_bookBidLock)
                        {
                            g.BookBidInstances.Remove(stock);

                            IBookBidGenerator generator;

                            if (IsIndex(stock))
                                generator = new BookBidGeneratorIndex(stock, StockExchange.Instance);
                            else
                                generator = new BookBidGeneratorStock(stock, StockExchange.Instance);

                            dgv = generator.GenerateBookBidView(stock);
                            if (dgv == null)
                                continue;

                            g.BookBidInstances[stock] = generator;
                        }

                        lock (g.PendingBookBids)
                        {
                            g.PendingBookBids.Add(new PendingBookBid(stock, dgv));
                        }
                    }
                }
            };
            _monitorTimer.Start();
        }

        public DataGridView GetOrCreate(string stock)
        {
            lock (_bookBidLock)
            {
                if (_gridMap.TryGetValue(stock, out var existingGrid))
                    return existingGrid;

                IBookBidGenerator generator;

                if (IsIndex(stock))
                    generator = new BookBidGeneratorIndex(stock, StockExchange.Instance);
                else
                    generator = new BookBidGeneratorStock(stock, StockExchange.Instance);

                var grid = generator.GenerateBookBidView(stock);
                if (grid == null) return null;

                _jpMap[stock] = generator;
                _gridMap[stock] = grid;

                return grid;
            }
        }

        public DataGridView FindGrid(string stock)
        {
            if (string.IsNullOrEmpty(stock))
                return null;

            lock (_bookBidLock)
            {
                _gridMap.TryGetValue(stock, out var grid);
                return grid;
            }
        }

        bool IsIndex(string stock)
        {
            return stock.Contains("KODEX 레버리지")
                || stock.Contains("KODEX 코스닥150레버리지");
        }

        public void Remove(string stock)
        {
            if (_gridMap.TryGetValue(stock, out var grid))
            {
                if (grid.Parent != null)
                    g.MainForm.Invoke((MethodInvoker)(() => g.MainForm.Controls.Remove(grid)));

                grid.Dispose();
                _gridMap.Remove(stock);
            }

            if (_jpMap.TryGetValue(stock, out var generator))
            {
                generator.Unsubscribe();
                _jpMap.Remove(stock);
            }
        }

        //public bool Exists(string stock) => _gridMap.ContainsKey(stock);



        //public void Clear()
        //{
        //    foreach (var grid in _gridMap.Values)
        //    {
        //        if (grid.Parent != null)
        //            g.MainForm.Invoke((MethodInvoker)(() => grid.Parent.Controls.Remove(grid)));

        //        grid.Dispose();
        //    }
        //    foreach (var generator in _jpMap.Values)
        //        generator.Unsubscribe();

        //    _gridMap.Clear();
        //    _jpMap.Clear();
        //}

        public void CleanupAllExcept(IEnumerable<string> keepStocks)
        {
            var keepSet = new HashSet<string>(keepStocks.Where(s => !string.IsNullOrWhiteSpace(s)));
            foreach (var stock in _gridMap.Keys.ToList())
            {
                if (string.IsNullOrWhiteSpace(stock) || !keepSet.Contains(stock))
                    Remove(stock);
            }
        }

        public void Relocate(string stock)
        {
            if (_gridMap.TryGetValue(stock, out var grid) &&
                g.ChartManager.Chart1.ChartAreas.IndexOf(stock) is int index && index >= 0)
            {
                var chartArea = g.ChartManager.Chart1.ChartAreas[index];
                var chartPos = chartArea.Position;

                float chartX = chartPos.X / 100f * g.ChartManager.Chart1.Width;
                float chartY = chartPos.Y / 100f * g.ChartManager.Chart1.Height;
                float chartW = chartPos.Width / 100f * g.ChartManager.Chart1.Width;

                int newX = (int)(chartX + chartW);
                int newY;

                if (stock == "KODEX 레버리지")
                    newY = 0;   // 또는 코스피용 원하는 y
                else if (stock == "KODEX 코스닥150레버리지")
                    newY = (int)(g.ChartManager.Chart1.Height / g.nRow * 2);
                else
                    newY = (int)chartY;   // 일반 종목은 기존 방식

                if (stock.Contains("KODEX"))
                    grid.Location = new Point(newX, newY); // was newX + 20
                else
                    grid.Location = new Point(newX + 10, newY); // was newX + 20
                grid.Visible = true;
            }
        }
    }
}
