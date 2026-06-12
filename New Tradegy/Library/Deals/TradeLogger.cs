using New_Tradegy.Library.Models;
using System;
using System.Globalization;
using System.IO;

namespace New_Tradegy.Library.Trackers
{


    public static class TradeLogger
    {
        private static readonly object _fileLock = new object();
        private static readonly string logDir = @"C:\BJS\Z Data\매매\";

        static TradeLogger()
        {
            if (!Directory.Exists(logDir))
                Directory.CreateDirectory(logDir);
        }

        public static void LogTrade(OrderItem item)
        {
            string filename = Path.Combine(logDir, DateTime.Now.ToString("yyyy-MM-dd") + ".txt");
            string timestamp = DateTime.Now.ToString("HH:mm:ss");

            string stock = item.stock.PadRight(15);
            string type = item.buyorSell.PadRight(4);
            string priceStr = item.m_nPrice.ToString("N0", CultureInfo.InvariantCulture).PadLeft(10); // comma formatted
            string amtStr = item.m_nContAmt.ToString().PadLeft(7);

            long totalMoney = (long)item.m_nPrice * item.m_nContAmt;
            string moneyStr = (totalMoney / 10000).ToString("N0", CultureInfo.InvariantCulture).PadLeft(7); // in 만원

            string line = $"{timestamp,-10}{stock}{type}{priceStr}{amtStr}{moneyStr}";

            lock (_fileLock)
            {
                File.AppendAllText(filename, line + Environment.NewLine);
            }
        }
    }

}
