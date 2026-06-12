using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace New_Tradegy.Library.Utils
{
    internal class DealUtils
    {
        public static int GetTick(string symbol, int price)
        {
            if (symbol.StartsWith("KODEX ", StringComparison.OrdinalIgnoreCase))
                return 5;   // ETF 예외, 필요하면 TIGER 등 추가

            int p = price;
            if (p < 2000) return 1;
            if (p < 5000) return 5;
            if (p < 20000) return 10;
            if (p < 50000) return 50;
            if (p < 200000) return 100;
            if (p < 500000) return 500;
            return 1000;
        }
    }
}
