using New_Tradegy.Library.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace New_Tradegy.Library.Listeners
{
    internal class PreMarketEyeBatchDownloader
    {
        private static readonly TimeSpan Period = TimeSpan.FromMilliseconds(1400);
        private static readonly TimeSpan MinPause = TimeSpan.FromMilliseconds(50);
        private static readonly SemaphoreSlim _once = new SemaphoreSlim(1, 1);

        private static CPSYSDIBLib.MarketEye _marketeye;
        private static readonly CPUTILLib.CpStockCode _cpstockcode = new CPUTILLib.CpStockCode();

        private static int _offset = 0;
        private static int _preOpenDownloadCount = 0;

   
        private static int SafeInt(object value)
        {
            if (value == null) return 0;

            try
            {
                return Convert.ToInt32(value);
            }
            catch
            {
                return 0;
            }
        }

        private static long SafeLong(object value)
        {
            if (value == null) return 0;

            try
            {
                return Convert.ToInt64(value);
            }
            catch
            {
                return 0;
            }
        }

    }
}
