using New_Tradegy.Library.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace New_Tradegy.Library.Utils
{
    public static class HardUniverseFilter
    {
        private static bool PassBuyDirection(StockData d)
        {
            if(g.test)
            {
                var a = d?.Api;
                if (a == null) return false;
                if (a.분배수차[0] < 0) return false;
                if (a.분프로천[0] < 0) return false;
            }
            else
            {
                var p = d?.Post;
                if (p == null) return false;
                if (p.분30배수차 < 0) return false;
                if (p.분30프로천 < 0) return false;
            }
            
            return true;
        }

        public static List<StockData> BuildPassed(
     IEnumerable<StockData> datas,
     bool useHoga)
        {
            if (datas == null)
                return new List<StockData>();

            var pre = datas
                .Where(d =>
                    d != null &&
                    d.Api != null &&
                    d.Statistics != null &&
                    d.Post != null &&
                    PassBuyDirection(d)
                )
                .ToList();

            if (pre.Count <= 50)
                return pre;

            if (useHoga)
            {
                return pre
                    .OrderByDescending(d => d.Post.Sec30Top1BookAvgValue)
                    .Take(50)
                    .ToList();
            }

            return pre.Take(50).ToList();
        }

        public static void UpdatePassedUniverseAndScores(bool useHoga)
        {
            g.PassedUniverse = HardUniverseFilter.BuildPassed(
                g.StockRepo.AllGeneralStocks,
                useHoga
            );
        }

        public static void AdjustPassPercentage(bool increase)
        {
            double step = 0.02; // 2%

            if (increase)
                g.PassControlDelta += step;
            else
                g.PassControlDelta -= step;

            // 안전 클램프
            g.PassControlDelta = Math.Max(-0.10, Math.Min(0.10, g.PassControlDelta));

            g.PassPct_Bung = g.PassControlBasePct + g.PassControlDelta;
            g.PassPct_Hoga = g.PassPct_Bung * g.PassControlHogaRatio;
        }
    }
}
