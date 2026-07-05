using MathNet.Numerics.RootFinding;
using New_Tradegy.Library;
using New_Tradegy.Library.Core;
using New_Tradegy.Library.Deals;
using New_Tradegy.Library.IO;
using New_Tradegy.Library.PostProcessing;
using New_Tradegy.Library.Trackers;
using New_Tradegy.Library.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using static New_Tradegy.Library.Models.DisplayMode;

namespace New_Tradegy.Library.UI.KeyBindings
{
    using New_Tradegy; // WeightForm 네임스페이스 맞춰주세요
    using New_Tradegy.Library.Models;
    // somewhere (e.g., Global static helper)
    using System;
    using System.Threading;
    using System.Windows.Forms;
    using static System.Net.Mime.MediaTypeNames;

    public static class WeightFormHost
    {
        private static WeightForm _inst;

        public static void ShowOrActivate()
        {
            Action open = () =>
            {
                if (_inst == null || _inst.IsDisposed)
                {
                    _inst = new WeightForm();
                    _inst.FormClosed += (s, e) => _inst = null;
                }

                if (!_inst.Visible) _inst.Show();
                // 앞으로 가져오기
                _inst.TopMost = true;   // 임시로 TopMost
                _inst.Activate();
                _inst.BringToFront();
                _inst.TopMost = false;  // 다시 해제(원하면 유지)
            };

            // UI 스레드에서 실행
            if (System.Windows.Forms.Application.MessageLoop) open();
            else System.Windows.Forms.Application.OpenForms[0].BeginInvoke(open);

        }
    }


    public static class ActionHandlers
    {
        // Function

        

        #region
        // Escape
        public static readonly Action DealCancel = () =>
        {
            if (!g.test)
            {
                SoundUtils.Sound("Keys", "cancel");
                for (int i = OrderItemTracker.OrderMap.Count - 1; i >= 0; i--)
                {
                    var data = OrderItemTracker.GetOrderByRowIndex(i);
                    if (data != null)
                        DealManager.DealCancelOrder(data);
                }
            }
            else
            {
                g.Npts[0] = 0;
                g.Npts[1] = 2;
            }

        };

        public static readonly Action DealHalf = () =>
        {
            if (g.test)
            {
                g.Npts[0] = 0;
                g.Npts[1] = g.TestMaximumRow;
            }
            else
            {
                g.일회거래액 = KeyHelper.DealMoney(g.일회거래액, '-');

                if (g.controlPane.GetCellValue(0, 2) != g.일회거래액.ToString())
                {
                    g.controlPane.SetCellValue(0, 2, g.일회거래액);
                }
                Task.Run(() =>
                {
                    try
                    {
                        SoundUtils.Sound("돈", g.일회거래액.ToString());
                    }
                    catch { /* log or ignore */ }
                });
            }
        };

        public static readonly Action DealDoubleKey = () =>
        {
            if (g.test)
            {
                return;
            }
            else
            {
                g.일회거래액 = KeyHelper.DealMoney(g.일회거래액, '+');

                if (g.controlPane.GetCellValue(0, 2) != g.일회거래액.ToString())
                {
                    g.controlPane.SetCellValue(0, 2, g.일회거래액);
                }
                Task.Run(() =>
                {
                    try
                    {
                        SoundUtils.Sound("돈", g.일회거래액.ToString());
                    }
                    catch { /* log or ignore */ }
                });
            }
        };


        // F3
        public static readonly Action DealConfirmSellKey = () =>
        {
            if (!g.test)
            {
                g.confirm_sell = !g.confirm_sell;

                if (g.confirm_sell)
                    SoundUtils.Sound("Keys", "confirm sell");
                else
                    SoundUtils.Sound("Keys", "no confirm sell");
            }
        };


        // F5
        public static readonly Action ConfirmSellToggle = () =>
        {
            if (!g.test)
            {
                g.confirm_sell = !g.confirm_sell;

                if (g.confirm_sell)
                    SoundUtils.Sound("Keys", "confirm sell");
                else
                    SoundUtils.Sound("Keys", "no confirm sell");
            }
        };
        #endregion

        // Number


        public static readonly Action AddTopRankToInterestedOnlyList = () =>
        {
            AddRankSectorToInterestedList(0);
        };

        public static readonly Action AddSecondRankToInterestedOnlyList = () =>
        {
            AddRankSectorToInterestedList(1);
        };

        public static readonly Action ClearInterestedOnlyList = () =>
        {
            g.StockManager.InterestedOnlyList.Clear();
            ActionCode.New('m', post: false, eval: false, draw: 'm').Run();
        };

        private static void AddRankSectorToInterestedList(int rank)
        {
            if (rank < 0)
                return;

            if (rank >= g.GroupManager.GroupRankingList.Count)
                return;

             var stocks = g.GroupManager.GroupRankingList[rank].Stocks;
        
            foreach (string stock in stocks)
            {
                if (string.IsNullOrWhiteSpace(stock))
                    continue;

                if (!g.StockManager.InterestedOnlyList.Contains(stock))
                    g.StockManager.InterestedOnlyList.Add(stock);
            }

            ActionCode.New('m', post: false, eval: false, draw: 'm').Run();
        }


        #region
        public static void SetMode(DisplayMode mode)
        {
            g.v.MainChartDisplayMode = mode.ToString();

            Form se = (Form)System.Windows.Forms.Application.OpenForms["Form1"];
            if (se == null) return;

            se.Text = g.v.MainChartDisplayMode;

            ActionCode.New('B', post: false, eval: true, draw: 'B').Run();
        }
        #endregion

        // Top
        #region
        public static readonly Action TimeOneForwardsKey = () =>
        {
            //if (g.ImpulseRunner != null)
            //    g.ImpulseRunner.TestImpulseHud("삼성전자");

            if (g.test) // 시간 앞으로 (테스트)
            {
                g.Npts[1]++;
                if (g.Npts[1] > g.TestMaximumRow)
                {
                    g.Npts[0] = 0;
                    g.Npts[1] = 2;
                }

                ActionCode.New('B', post: true, eval: true, draw: 'B').Run();
            }

            else
            {
                for (int i = 0; i < 2; i++)
                {
                    string inv = g.StockManager.InverseList[i];

                    if (g.StockManager.InterestedWithBidList.Contains(inv))
                    {
                        g.StockManager.InterestedWithBidList.Remove(inv);
                    }
                    else
                    {
                        g.StockManager.InterestedWithBidList.Add(inv);
                    }
                }
                ActionCode.New('m', post: false, eval: false, draw: 'm').Run();
            }
        };

        public static readonly Action TimeOneBackwardsKey = () =>
        {
            if (g.test) // 시간 뒤로 (테스트)
            {
                g.Npts[1]--;
                if (g.Npts[1] <= g.Npts[0] + 1) // time difference more than 2
                    g.Npts[1]++;

                ActionCode.New('B', post: true, eval: true, draw: 'B').Run();
            }

        };

        public static readonly Action TimeShortMoveKey = () =>
        {
            if (g.test) // 짧은 시간 앞으로 in draw
            {
                if (g.EndNptsBeforeExtend == 0) // time extensionw
                {
                    TimeUtils.MinuteAdvanceRetreat(g.v.q_advance_lines);    // forward eval = false;
                    ActionCode.New('B', post: true, eval: false, draw: 'B').Run();
                }

                else
                {
                    TimeUtils.MinuteAdvanceRetreat(0);                      // backward eval = true;
                    ActionCode.New('B', post: true, eval: true, draw: 'B').Run();
                }


            }
        };

        public static readonly Action TimeLongMoveKey = () =>
        {
            if (g.test) // 긴 시간 앞으로 in draw
            {
                if (g.EndNptsBeforeExtend == 0) // time extension
                {
                    TimeUtils.MinuteAdvanceRetreat(g.v.Q_advance_lines);
                    ActionCode.New('B', post: true, eval: false, draw: 'B').Run();
                }

                else
                {
                    TimeUtils.MinuteAdvanceRetreat(0);
                    ActionCode.New('B', post: true, eval: false, draw: 'B').Run();
                }
            }
        };

        public static readonly Action WeightControlKey = () =>
        {
            WeightFormHost.ShowOrActivate();
        };


        public static readonly Action TimeTenForwardsKey = () =>
        {
            if (g.test)
            {
                if (g.draw_selection == 1)
                {
                    g.Npts[1] += 10;
                    if (g.Npts[1] > g.RealMaximumRow)
                    {
                        g.Npts[1] = g.RealMaximumRow;
                    }
                }
                else
                    g.npts_fi_dwm += 10;

                ActionCode.New('B', post: true, eval: true, draw: 'B').Run();
            }

        };

        public static readonly Action TimeTenBackwardsKey = () =>
        {
            if (g.test)
            {
                if (g.draw_selection == 1)
                {
                    g.Npts[1] -= 10;
                    if (g.Npts[1] <= g.Npts[0] + 1)
                    {
                        g.Npts[1] = g.Npts[0] + 1;
                    }
                }
                else
                    g.npts_fi_dwm -= 10;

                ActionCode.New('B', post: true, eval: true, draw: 'B').Run();
            }

        };

        public static readonly Action TimeThirtyForwardsKey = () =>
        {
            if (g.test)
            {
                g.Npts[1] += 30;

                if (g.Npts[1] > g.TestMaximumRow)
                {
                    g.Npts[1] = g.TestMaximumRow;
                }

                ActionCode.New('B', post: true, eval: true, draw: 'B').Run();
            }
        };

        public static readonly Action TimeThirtyBackwardsKey = () =>
        {
            if (g.test)
            {
                g.Npts[1] -= 30;

                if (g.Npts[1] <= g.Npts[0] + 1)
                {
                    g.Npts[1] = g.Npts[0] + 1;
                }

                ActionCode.New('B', post: true, eval: true, draw: 'B').Run();
            }
        };

        public static readonly Action ToggleChart1Focus = () =>
        {
           g.chart1Focus = !g.chart1Focus;
            if (g.chart1Focus)
                SoundUtils.Sound("", "focus on");
            else
                SoundUtils.Sound("", "focus off");
        };

        public static void IncreasePassControlPercentage()
        {
            HardUniverseFilter.AdjustPassPercentage(increase: true);
        }

        public static void DecreasePassControlPercentage()
        {
            HardUniverseFilter.AdjustPassPercentage(increase: false);
        }

        

        public static readonly Action OpenFilesKey = () => // 제어, 상관, 관심 등
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.InitialDirectory = @"C:\BJS\data work\";
                dialog.Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*";
                dialog.Multiselect = true;

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    foreach (string file in dialog.FileNames)
                    {
                        // Open the file with the default editor
                        Process.Start("notepad.exe", file);
                    }
                }
            }
        };

        public static readonly Action OpenMemoKey = () => // 제어, 상관, 관심 등
        {

        };

        public static readonly Action NewsPeoridKey = () =>
        {
            if (g.PeoridNews == 'd')
            {
                g.PeoridNews = 'w';
                SoundUtils.Sound("일반", "news week");
            }

            else if (g.PeoridNews == 'w')
            {
                g.PeoridNews = 'm';
                SoundUtils.Sound("일반", "news month");
            }
            else
            {
                g.PeoridNews = 'd';
                SoundUtils.Sound("일반", "news day");
            }
        };

        public static readonly Action DrawBollingerKey = () =>
        {
            g.draw_selection = 3;
        };


        public static readonly Action DrawForeignAndInstituteKey = () =>
        {
            g.draw_selection = 2;
        };

        public static readonly Action DrawNormaStockKey = () =>
        {
            g.draw_selection = 1;
        };
        #endregion

        // Home
        #region
        public static readonly Action ShrinkOrNotTenPlusKey = () =>
        {
            g.NptsForShrinkDraw += 10;
            ActionCode.New('B', false, eval: false, draw: 'B').Run();
        };

        public static readonly Action ShrinkOrNotTenMinusKey = () =>
        {
            g.NptsForShrinkDraw -= 10;
            if (g.NptsForShrinkDraw <= 10)
            {
                g.NptsForShrinkDraw = 10;
            }
            ActionCode.New('B', false, eval: false, draw: 'B').Run();
        };

        public static readonly Action AddInterestToggle = () =>
        {
            if (g.test)
                return;

            g.add_interest = !g.add_interest;
            if (g.add_interest)
                SoundUtils.Sound("일반", "add interest");
            else
                SoundUtils.Sound("일반", "no add interest");
        };


        public static readonly Action SectorDraw = async () =>
        {
            g.v.SubChartDisplayMode = "섹터";
            g.q = "o&s";
            g.gid = 0;
            //ActionCode.New('s', post: false, eval: false, draw: 's').Run();


            ActionCode.New('s', post: true, eval: true, draw: 's').Run();
        };
        

        public static readonly Func<Task> SaveAllStocksAsync = async () =>
        {
            if (!g.test)
            {
                string caption = "Save all stocks ?";
                string message = "모든 파일 현재 시간 기준 저장";
                string default_option = "No";


                //Form1.Instance.BringToFront();
                //Form1.Instance.Activate();
                //Form1.Instance.Focus();

                // string result = StringUtils.message(Form1.Instance, caption, message, default_option);

                string result = StringUtils.message(caption, message, default_option);


                if (result == "Yes")
                    await  FileOut.SaveAllStocksAsync();
            }
        };








        public static readonly Func<Task> nRowDecrease = async () =>
        {
            if (g.nRow > 2)
                g.nRow--;
            ActionCode.New('m', false, eval: false, draw: 'm').Run();
        };

        public static readonly Func<Task> nRowIncrease = async () =>
        {
            g.nRow++;
            ActionCode.New('m', false, eval: false, draw: 'm').Run();
        };

        public static readonly Func<Task> nColDecrease = async () =>
        {
            if (g.nCol > 2)
                g.nCol--;
            ActionCode.New('m', false, eval: false, draw: 'm').Run();
        };

        public static readonly Func<Task> nColIncrease = async () =>
        {
            g.nCol++;
            ActionCode.New('m', false, eval: false, draw: 'm').Run();
        };

        #endregion


        // Bottom
        #region

        public static readonly Action MainChartRotator = () =>
        {
            var list = new List<string> { "푀분", "등랍", "푀누", "종누" };
            g.v.MainChartDisplayMode = StringUtils.CycleStrings(g.v.MainChartDisplayMode, list);
            g.q = "o&s";
            g.gid = 0;
            var se = (Form)System.Windows.Forms.Application.OpenForms["Form1"];
            if (se == null)
            {
                //Console.WriteLine("Form1 is not open. 푀누 종누.");
                return;
            }
            se.Text = g.v.MainChartDisplayMode;

            ActionCode.New('m', post: true, eval: true, draw: 'm').Run();
        };

        public static readonly Action SubChartRotator = () =>
        {
            List<string> list = new List<string> { "섹터", "피올", "닥올" };
            g.v.SubChartDisplayMode = StringUtils.CycleStrings(g.v.SubChartDisplayMode, list);

            ActionCode.New('s', post: true, eval: true, draw: 's').Run();
        };

        public static readonly Action OptimalTradingToggleKey = () =>
        {
            g.optimumTrading = !g.optimumTrading;
            if (g.optimumTrading)
                SoundUtils.Sound("돈", "optimum");
            else
                SoundUtils.Sound("돈", "non optimum");
        };

        //public static readonly Action RemoveInterestedOnlyListKey = () =>
        //{
        //    for (int i = g.StockManager.InterestedOnlyList.Count - 1; i >= 0; i--)
        //    {
        //        // rd.read_관심제거추가(g.호가종목[i]); // this does nothing
        //        g.StockManager.InterestedOnlyList.Remove(g.StockManager.InterestedOnlyList[i]);
        //    }
        //};

        public static readonly Action RemoveInterestedWithBidListKey = () =>
        {
            for (int i = g.StockManager.InterestedWithBidList.Count - 1; i >= 0; i--)
            {
                // rd.read_관심제거추가(g.호가종목[i]); // this does nothing
                g.StockManager.InterestedWithBidList.Remove(g.StockManager.InterestedWithBidList[i]);
            }
        };

        public static readonly Action KillWebTxtFormKey = () =>
        {
            Process[] AllBrowsers = Process.GetProcesses();
            foreach (var process in AllBrowsers)
            {
                if (process.MainWindowTitle != "")
                {
                    string s = process.ProcessName.ToLower();
                    if (s == "iexplore" || s == "iexplorer" || s == "chrome" || s == "firefox" ||
                        s == "notepad" || s == "microsoftedgecp" || s == "microsoftedge" || s.Contains("microsoft edge"))
                        process.Kill();
                }
            }
           
        };

        public static readonly Action PreviousPage = () =>
        {
            int tradeplangridview = 1;
            switch (g.q)
            {
                case "o&s":
                    //case "e&s":
                    int count = g.StockManager.HoldingList.Count + g.StockManager.InterestedWithBidList.Count; // 지수종목
                    if (g.gid - ((g.nCol - 2) * g.nRow - count - tradeplangridview) >= 0)
                    {
                        g.gid -= (g.nCol - 2) * g.nRow - count - tradeplangridview;
                    }
                    else
                    {
                        g.gid = 0;
                    }
                    ActionCode.New('m', false, eval: false, draw: 'm').Run();
                    break;

                case "h&s":
                    for (int jndex = 1; jndex < (g.nCol - 2) * g.nRow - tradeplangridview; jndex++)
                    {
                        int return_date = wk.GetAdjacentDateFolder(g.moving_reference_date, 1); // 거래익일
                        if (return_date == -1)
                        {
                            return;
                        }
                        else
                        {
                            g.moving_reference_date = return_date;
                        }
                    }
                    break;

                default:
                    break;
            }
        };

        public static readonly Action NextPage = () =>
        {
            int tradeplangridview = 1;
            switch (g.q)
            {
                case "o&s":
                    
                    int count = g.StockManager.HoldingList.Count + g.StockManager.InterestedWithBidList.Count;
                    if (g.gid + ((g.nCol - 2) * g.nRow - count - tradeplangridview) < g.StockRepo.AllGeneralStocks.Count())
                    {
                        g.gid += (g.nCol - 2) * g.nRow - count - tradeplangridview;
                    }
                    else
                    {
                        g.gid = 0;
                    }
                    ActionCode.New('m', false, eval: false, draw: 'm').Run();
                    break;

                case "h&s":
                    for (int jndex = 1; jndex < (g.nCol - 2) * g.nRow - tradeplangridview; jndex++)
                    {
                        int return_date;
                        if (g.draw_history_forwards)
                            return_date = wk.GetAdjacentDateFolder(g.moving_reference_date, +1); // 거래익일
                        else
                            return_date = wk.GetAdjacentDateFolder(g.moving_reference_date, -1); // 거래전일
                        if (return_date == -1)
                        {
                            return;
                        }
                        else
                        {
                            g.moving_reference_date = return_date;
                        }
                    }
                    break;

                default:
                    break;
            }
        };






        public static void ActiveClearKey()
        {
            var active = g.StockManager.Active;
            if (string.IsNullOrEmpty(active))
                return;

            g.StockManager.Active = "";

            g.StockManager.InterestedWithBidList.Remove(active);  // 네 차트 제거 함수
        }
        public static void ActiveToInterestedKey()
        {
            var active = g.StockManager.Active;
            if (string.IsNullOrEmpty(active))
                return;

            if (!g.StockManager.InterestedWithBidList.Contains(active))
                g.StockManager.InterestedWithBidList.Add(active);

            g.StockManager.Active = "";
        }


        public static readonly Action 통과종목수 = () =>
        {
            
        };










        public static void GoPrevDay()
        {
            DateNavigator.GoPrevDay();

            Utils.SoundUtils.Sound("time", "date backwards");

            RebuildForChangedDate();

            ActionCode.New('m', true, eval: true, draw: 'B').Run();
        }

        public static void GoNextDay()
        {
            DateNavigator.GoNextDay();

            Utils.SoundUtils.Sound("time", "date forwards");

            RebuildForChangedDate();

            ActionCode.New('m', true, eval: true, draw: 'B').Run();
        }

        private static void RebuildForChangedDate()
        {
            var minuteDir = $@"C:\BJS\분\{g.date}";

            // 1) 기존 sector 제거 권장
            g.StockRepo.RemoveAllSectorStocks(); // 없으면 직접 구현

            // 2) minute reload
            MinuteFileLoader.LoadUniverseMinuteData(minuteDir);






            // 3) 파생 rebuild
            g.GroupManager = new GroupManager();

            GroupRepository.LoadGroups();

            FileIn.read_삼성_코스피_코스닥_전체종목(masterOnly: false);
            FileIn.read_파일관심종목();

            SectorBuilder.BuildSectorsFromSavedMinuteData();






            // 4) cache rebuild
            g.StockRepo.BuildAllGeneralCache();
            g.StockRepo.BuildAllSectorCache();
            g.StockRepo.BuildAllGeneralNamesCache();
        }

        public static void GoBackToOrigin()
        {
            DateNavigator.GoBackToOrigin();
            Utils.SoundUtils.Sound("time", "date to present");
            ActionCode.New('m', true, eval: true, draw: 'B').Run();
        }

        //KeyBindingRegistrar.Register('<', () => DateNavigator.GoPrevDay());
        //KeyBindingRegistrar.Register('>', () => DateNavigator.GoNextDay());
        //KeyBindingRegistrar.Register('=', () => DateNavigator.GoBackToOrigin()); // 복귀
    };
    #endregion


    public static class UiPost
    {
        public static void Post(Action a)
        {
            var ui = System.Windows.Forms.Application.OpenForms.Count > 0
                ? System.Windows.Forms.Application.OpenForms[0]
                : null;

            if (ui == null) return;

            if (ui.InvokeRequired) ui.BeginInvoke(a);
            else a();
        }
    }

}

