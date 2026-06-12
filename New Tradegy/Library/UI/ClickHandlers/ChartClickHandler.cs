
using New_Tradegy.Library.Core;
using New_Tradegy.Library.Deals;
using New_Tradegy.Library.IO;
using New_Tradegy.Library.Listeners;
using New_Tradegy.Library.PostProcessing;
using New_Tradegy.Library.Trackers;
using New_Tradegy.Library.Trackers.Charting;
using New_Tradegy.Library.UI.KeyBindings;
using Newtonsoft.Json.Linq;
using OpenQA.Selenium.Interactions;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using static OpenQA.Selenium.BiDi.Modules.Script.RemoteValue;
using New_Tradegy.Library.UI.KeyBindings;
using DSCBO1Lib;
using static New_Tradegy.Library.Models.StockData;


namespace New_Tradegy.Library.UI.ChartClickHandlers
{
    internal class ChartClickHandler
    {
        private static CPUTILLib.CpCybos _cpcybos;



        private static int GetAskPriceFromGivenStock(string stockName)
        {
            var dgv = Utils.FormUtils.FindDataGridViewByName(Form1.Instance, stockName);
            if (dgv == null || dgv.Rows.Count <= 4 || dgv.Columns.Count <= 1)
                return -1;

            var cellValue = dgv.Rows[4].Cells[1].Value?.ToString()?.Trim();

            if (int.TryParse(cellValue?.Replace(",", ""), out int price))
                return price;
            else
                return -1;
        }


        public static void HandleControlClick(Chart chart, string selection, int row, int col)
        {
            var data = g.StockRepo.TryGetDataOrNull(g.clickedStock);
            if (data == null) return;

            switch (selection)
            {
                case "r4":
                    if (g.test)
                    {
                        g.Npts[1]--;
                        if (g.Npts[1] < 2)
                        {
                            g.Npts[0] = 0;
                            g.Npts[1] = 2;
                        }
                        ActionCode.New('B', false, eval: true, draw: 'B').Run();
                    }
                    break;

                case "l1":

                    break;

                case "l2": // 수급과장배수
                    {
                        if (data.Kind != InstrumentKind.Index)
                        {
                            data.Misc.수급과장배수 *= 1.33;
                            data.Misc.CreateNewChartArea = true;
                            ActionCode.New('B', post: false, eval: false, draw: 'B').Run();
                        }

                        else
                        {
                            if (g.StockManager.LeverageList[0] == data.Stock) // KOSPI 레버리지
                            {
                                g.KodexMagnifier[0, 1] *= 1.33;
                            }
                            else
                                g.KodexMagnifier[1, 1] *= 1.33;

                            FileIn.LoadOrSaveKodexMagnifier("write");
                            data.Misc.CreateNewChartArea = true;
                            ActionCode.New('m', post: false, eval: false, draw: 'm').Run();
                        }
                    }
                    break;

                case "l3": // 수급과장배수, Leverage magnifier * 1.33
                    {
                        if (data.Kind != InstrumentKind.Index)
                        {
                            data.Misc.수급과장배수 *= 1.33;
                            data.Misc.CreateNewChartArea = true;
                            ActionCode.New('B', post: false, eval: false, draw: 'B').Run();
                        }
                        else
                        {
                            if (g.StockManager.LeverageList[0] == data.Stock) // KOSPI 레버리지
                            {
                                g.KodexMagnifier[0, 2] *= 1.33;
                            }
                            else
                                g.KodexMagnifier[1, 2] *= 1.33;

                            FileIn.LoadOrSaveKodexMagnifier("write");
                            data.Misc.CreateNewChartArea = true;
                            ActionCode.New('m', post: false, eval: false, draw: 'm').Run();
                        }
                    }
                    break;

                case "l4":
                    if (g.test)
                    {
                        ActionHandlers.TimeLongMoveKey?.Invoke();
                    }
                    break;

                case "l8": // 수급과장배수, Leverage magnifier * 1.5
                    {
                        if (data.Kind != InstrumentKind.Index)
                        {
                            data.Misc.수급과장배수 *= 0.66;
                            data.Misc.CreateNewChartArea = true;
                            ActionCode.New('B', post: false, eval: false, draw: 'B').Run();
                        }
                        else
                        {
                            if (g.StockManager.LeverageList[0] == data.Stock) // KOSPI 레버리지
                            {
                                g.KodexMagnifier[0, 1] *= 0.66;
                            }
                            else
                                g.KodexMagnifier[1, 1] *= 0.66;

                            FileIn.LoadOrSaveKodexMagnifier("write");
                            data.Misc.CreateNewChartArea = true;
                            ActionCode.New('m', post: false, eval: false, draw: 'm').Run();
                        }
                    }
                    break;

                case "l9": // 수급과장배수, Leverage magnifier * 1.5
                    {
                        if (data.Kind != InstrumentKind.Index)
                        {
                            data.Misc.수급과장배수 *= 0.66;
                            data.Misc.CreateNewChartArea = true;
                            ActionCode.New('B', post: false, eval: false, draw: 'B').Run();
                        }
                        else
                        {
                            if (g.StockManager.LeverageList[0] == data.Stock) // KOSPI 레버리지
                            {
                                g.KodexMagnifier[0, 2] *= 0.66;
                            }
                            else
                                g.KodexMagnifier[1, 2] *= 0.66;

                            FileIn.LoadOrSaveKodexMagnifier("write");
                            data.Misc.CreateNewChartArea = true;
                            ActionCode.New('m', post: false, eval: false, draw: 'm').Run();
                        }

                    }
                    break;
            }
        }


        public static void HandleClick(Chart chart, string selection, int row_id, int col_id)
        {
            var data = g.StockRepo.TryGetDataOrNull(g.clickedStock);
            if (data == null) return;

            switch (selection)
            {
                case "l1": // Shrink Toggle
                    {
                        data.Misc.ShrinkDraw = !data.Misc.ShrinkDraw;
                        data.Misc.CreateNewChartArea = true;
                        ActionCode.New('B', post: false, eval: false, draw: 'B').Run();
                    }
                    break;

                case "l2": // 수급과장배수, Leverage magnifier * 1.5
                    {

                        if (data.Kind != InstrumentKind.Index)
                        {
                            data.Misc.가격과장배수 *= 1.33;
                            data.Misc.CreateNewChartArea = true;
                            ActionCode.New('B', post: false, eval: false, draw: 'B').Run();
                        }
                        else
                        {
                            if (g.StockManager.LeverageList[0] == data.Stock)
                            {
                                g.KodexMagnifier[0, 0] *= 1.33;
                            }
                            else
                                g.KodexMagnifier[1, 0] *= 1.33;

                            FileIn.LoadOrSaveKodexMagnifier("write");
                            data.Misc.CreateNewChartArea = true;
                            ActionCode.New('m', post: false, eval: false, draw: 'm').Run();
                        }
                        data.Misc.CreateNewChartArea = true;

                        ActionCode.New('B', post: false, eval: false, draw: 'B').Run();
                    }
                    break;

                case "l3": // Stock Memo
                    {
                        string dirPath = @"C:\BJS\data work\Stock Memo";
                        string filePath = Path.Combine(dirPath, g.clickedStock + ".txt");
                        if (!Directory.Exists(dirPath)) Directory.CreateDirectory(dirPath);
                        if (!File.Exists(filePath))
                            File.Create(filePath).Dispose();
                        Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
                    }
                    break;

                case "l4":
                    if (g.test)
                    {
                        ActionHandlers.TimeShortMoveKey?.Invoke();
                    }
                    break;

                case "l5":
                    {
                        if (!g.StockManager.HoldingList.Contains(g.clickedStock))
                        {
                            if (g.StockManager.InterestedWithBidList.Contains(g.clickedStock))
                            {
                                g.StockManager.InterestedWithBidList.Remove(g.clickedStock);
                            }
                            else
                            {
                                g.StockManager.InterestedWithBidList.Add(g.clickedStock);
                                if (g.StockManager.InterestedOnlyList.Contains(g.clickedStock))
                                    g.StockManager.InterestedOnlyList.Remove(g.clickedStock);
                            }
                        }
                        ActionCode.New('m', post: false, eval: false, draw: 'm').Run();
                    }
                    break;

                case "l6":
                    g.StockManager.InterestedOnlyList.Clear();
                    PostProcessor.ManageChart1Invoke();
                    break;

                case "l7":
                    wk.CallNaverChart(g.clickedStock, "main");
                    break;

                case "l8":
                    if (data.Kind != InstrumentKind.Index)
                    {
                        data.Misc.가격과장배수 *= 0.66;
                        data.Misc.CreateNewChartArea = true;
                        ActionCode.New('B', post: false, eval: false, draw: 'B').Run();
                    }
                    else
                    {
                        if (g.StockManager.LeverageList[0] == data.Stock)
                        {
                            g.KodexMagnifier[0, 0] *= 0.66;
                        }
                        else
                            g.KodexMagnifier[1, 0] *= 0.66;

                        FileIn.LoadOrSaveKodexMagnifier("write");
                        data.Misc.CreateNewChartArea = true;
                        ActionCode.New('m', post: false, eval: false, draw: 'm').Run();
                    }
                    break;

                case "l9":
                    if (g.test)
                    {
                        g.Npts[1]--;
                        if (g.Npts[1] < 2)
                        {
                            g.Npts[0] = 0;
                            g.Npts[1] = 2;
                        }
                        ActionCode.New('B', true, eval: true, draw: 'B').Run();
                    }
                    break;

                case "r1":
                    string keyword = $"{g.clickedStock} 뉴스 주식";
                    string encodedQuery = Uri.EscapeDataString(keyword);
                    string url = $"https://www.google.com/search?q={encodedQuery}&tbs=qdr:{g.PeoridNews}";
                    Process.Start("chrome.exe", $"--new-tab {url}");
                    break;

                case "r2":
                    wk.CallNaverChart(g.clickedStock, "fchart");
                    break;

                case "r3":
                    string query = g.clickedStock + " 기업정보";
                    encodedQuery = Uri.EscapeDataString(query);
                    url = $"https://www.google.com/search?q={encodedQuery}";
                    Process.Start("chrome.exe", $"--new-tab {url}");
                    break;

                case "r4":
                    ActionHandlers.TimeOneForwardsKey.Invoke();
                    break;

                case "r5":
                    break;

                case "r6":

                    if (g.v.MainChartDisplayMode != "Datewise")
                    {
                        g.v.PreviousMainChartDisplayMode = g.v.MainChartDisplayMode;
                        g.v.MainChartDisplayMode = "Datewise";

                        g.DataOffset = 0;
                        g.ChartMain.DisplayDatewiseStockHistory(g.clickedStock, g.DataOffset);
                    }
                    else
                    {
                        g.DataOffset = 0;
                        g.v.MainChartDisplayMode = g.v.PreviousMainChartDisplayMode;
                        ActionCode.New('m', post: true, eval: true, draw: 'm').Run();
                    }
                    break;

                case "r7":
                    Utils.StringUtils.r3_display_lines(chart, g.clickedStock, row_id, col_id);
                    break;

                case "r8":
                    wk.CallNaverChart(g.clickedStock, "frgn");
                    break;

                case "r9":
                    if (g.clickedStock == "KODEX 레버리지" || g.clickedStock == "KODEX 코스닥150레버리지")
                    {
                        return;
                    }
                    else
                    {
                        bool isSector = g.clickedStock.StartsWith("SECTOR:");

                        // 2️⃣ SECTOR 접두어 제거 (표시용)
                        if (isSector)
                        {
                            g.clickedTitle = g.clickedStock.Substring("SECTOR:".Length);
                            g.v.SubChartDisplayMode = "상관";
                        }
                        else
                        {
                            if (g.v.SubChartDisplayMode == "상관")
                            {
                                g.v.SubChartDisplayMode = "섹터";
                            }
                            else
                            {
                                var group = g.GroupManager.FindGroupByStock(g.clickedStock);
                                if (group != null)
                                {
                                    g.clickedTitle = group.Title;
                                }
                                g.v.SubChartDisplayMode = "상관";
                            }

                        }
                        ActionCode.New('s', false, eval: true, draw: 's').Run();
                    }
                    break;
            }
        }
    }
}
