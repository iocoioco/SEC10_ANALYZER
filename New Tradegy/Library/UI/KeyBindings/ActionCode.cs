
using New_Tradegy.Library.Core;

using New_Tradegy.Library.Models;
using New_Tradegy.Library.PostProcessing;
using New_Tradegy.Library.Trackers;
using New_Tradegy.Library.Utils;
using OpenQA.Selenium.BiDi.Modules.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace New_Tradegy.Library.UI.KeyBindings
{
    public class ActionCode
    {
        public char ClearTarget { get; }
        public bool Post { get; }
        public bool Eval { get; }
        public char DrawTarget { get; }

        public ActionCode(char clear, bool post, bool eval, char draw)
        {
            ClearTarget = clear;
            Post = post;
            Eval = eval;
            DrawTarget = draw;
        }

        public static ActionCode New(char clear = ' ', bool post = false, bool eval = false, char draw = ' ')
        {
            return new ActionCode(clear, post, eval, draw);
        }

        public void Run()
        {
            // no need to used Clear 
            if (ClearTarget == 'm')
                g.ChartManager.ClearChart1();
            else if (ClearTarget == 's')
                g.ChartManager.ClearChart2();
            else if (ClearTarget == 'B')
                g.ChartManager.ClearAll();

            // Post = true (테스트일 경우, real이나 workinghour 아닌 경우)
            bool post = false;
            if (Post && g.test) post = true;
            if (Post && !g.test && !wk.isWorkingHour()) post = true;
            if (post)
                PostProcessor.post_test();

            if (Eval)
            {
                // 1) 스코어 빌드
                ScoreRankEngine.Build(g.StockRepo.AllGeneralStocks, ScoreKeySets.GeneralActive);
                ScoreRankEngine.Build(g.StockRepo.AllSectorStocks, ScoreKeySets.SectorActive);

                // 2) EvalInclusion 기반 Universe 생성
                g.PassedUniverse = g.StockRepo.AllGeneralStocks
                    .Where(x => RankLogic.EvalInclusion(x))
                    .ToList();

                // 3) 표시
                string t = g.PassedUniverse.Count
                    + "/"
                    + g.StockRepo.AllGeneralStocks.Count;

                g.controlPane.SetCellValue(1, 0, t);

                // 4) Snapshot + Rank
                g.PassedSnapshotStocks = g.PassedUniverse != null
                    ? new List<StockData>(g.PassedUniverse)
                    : new List<StockData>();

                RankLogic.RankGeneral(g.PassedSnapshotStocks);
                RankLogic.RankSector(g.StockRepo.AllSectorStocks);
            }

            if (DrawTarget == 'm' || DrawTarget == 'B')
                PostProcessor.ManageChart1Invoke();


            // 20260411
            if (DrawTarget == 's' || DrawTarget == 'B')
                PostProcessor.ManageChart2Invoke();
        }

    }
}

