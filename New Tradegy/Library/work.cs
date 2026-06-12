using OpenQA.Selenium.Chrome;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using System.Collections.Concurrent;
using New_Tradegy.Library.Core;
using New_Tradegy.Library.Models;
using New_Tradegy.Library.IO;
using New_Tradegy.Library.UI.KeyBindings;
using New_Tradegy.Library.Trackers;
using System.Security.Policy;
using System.Drawing;
using New_Tradegy.Library.UI;

namespace New_Tradegy.Library
{
    internal class wk
    {
        static CPUTILLib.CpStockCode _cpstockcode;
        public static void deleteChartAreaAnnotation(Chart chartName, string stockName)
        {
            // Check if the ChartArea exists
            if (chartName.ChartAreas.IndexOf(stockName) >= 0)
            {
                // Get the ChartArea
                var chartArea = chartName.ChartAreas[stockName];

                var annotationsToRemove = chartName.Annotations.Where(a => a.Name == stockName).ToList();

                foreach (var annotation in annotationsToRemove)
                {
                    chartName.Annotations.Remove(annotation); // Remove the annotation from the chart
                }

                // Remove all series associated with this ChartArea
                var seriesToRemove = chartName.Series
                    .Where(s => s.ChartArea == stockName)
                    .ToList();

                foreach (var series in seriesToRemove)
                {
                    chartName.Series.Remove(series);
                    //Console.WriteLine($"Removed series: {series.Name}");
                }
                if (chartArea == null)
                    return;
                else
                    chartName.ChartAreas.Remove(chartArea);
            }

            else
            {
                //Console.WriteLine($"ChartArea with name {stockName} does not exist.");
            }
        }

        public static bool isWorkingHour()
        {

            DateTime now = DateTime.Now;

            // ⏰ Market date check
            int currentDate = Convert.ToInt32(now.ToString("yyyyMMdd"));
            if (g.date != currentDate)
                return false;

            // 📆 Skip weekends
            if (now.DayOfWeek == DayOfWeek.Saturday || now.DayOfWeek == DayOfWeek.Sunday)
                return false;

            // 🕘 Market hours (adjust as needed)
            int HHmm = now.Hour * 100 + now.Minute;
            if (HHmm < 900 || HHmm > 1530)
                return false;

            return true;
        }
        public static bool isStock(string stock)
        {
            _cpstockcode = new CPUTILLib.CpStockCode();
            if (stock == "")
                return false;


            string code = _cpstockcode.NameToCode(stock); // 코스피혼합, 코스닥혼합 code.Length = 0 제외될 것임
            if (code.Length == 7)
                return true;
            else
                return false;
        }

        public static void 거분순서(List<string> stocks)
        {
            stocks.Sort((a, b) =>
            {
                var sa = g.StockRepo.TryGetDataOrNull(a);
                var sb = g.StockRepo.TryGetDataOrNull(b);

                if (sa == null && sb == null) return 0;
                if (sa == null) return 1;
                if (sb == null) return -1;

                double va = sa.Api.분거래천?[0] ?? double.MinValue;
                double vb = sb.Api.분거래천?[0] ?? double.MinValue;

                return vb.CompareTo(va); // descending order
            });
        }
        public static bool 종목일중변동자료계산(
    string stock, int days,
    out double avr, out double dev,
    out int avr_dealt, out int min_dealt, out int max_dealt,
    out ulong 일평균거래량, out string 일간변동평균편차)
        {
            avr = 0;
            dev = 0;
            avr_dealt = 0;
            max_dealt = 0;
            min_dealt = 0;
            일평균거래량 = 0;
            일간변동평균편차 = "";

            string path = $@"C:\BJS\data work\일\{stock}.txt";
            if (!File.Exists(path)) return false;

            var lines = File.ReadLines(path).Reverse().Take(days).ToList();
            if (lines.Count < 1) return false;

            var dayList = new List<double>();
            long totalDealt = 0;
            int daysCount = 0;
            bool hasDealt = false;

            foreach (var line in lines)
            {
                var words = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (words.Length != 10)
                    continue;   // 한 줄 이상해도 전체 실패하지 않게

                if (!double.TryParse(words[1], out double startPrice) ||  // 시가
                    !double.TryParse(words[2], out double highPrice) ||   // 고가
                    !double.TryParse(words[3], out double lowPrice) ||    // 저가
                    !double.TryParse(words[4], out double closePrice) ||  // 종가
                    !ulong.TryParse(words[5], out ulong 거래량))
                    continue;

                if (거래량 == 0)
                    continue;

                long dayDealt = (long)((closePrice * 거래량) / g.천만원);
                totalDealt += dayDealt;

                if (!hasDealt)
                {
                    min_dealt = (int)Math.Min(dayDealt, int.MaxValue);
                    max_dealt = (int)Math.Min(dayDealt, int.MaxValue);
                    hasDealt = true;
                }
                else
                {
                    max_dealt = Math.Max(max_dealt, (int)Math.Min(dayDealt, int.MaxValue));
                    min_dealt = Math.Min(min_dealt, (int)Math.Min(dayDealt, int.MaxValue));
                }

                일평균거래량 += 거래량;

                if (startPrice > 0)
                    dayList.Add((highPrice - lowPrice) / startPrice * 100.0);

                daysCount++;
            }

            if (daysCount == 0 || dayList.Count == 0)
                return false;

            avr_dealt = (int)Math.Min(totalDealt / daysCount, int.MaxValue);
            일평균거래량 /= (ulong)daysCount;

            double sum = 0;
            foreach (var val in dayList)
                sum += val;

            avr = sum / dayList.Count;

            if (dayList.Count > 1)
            {
                double varianceSum = 0;
                foreach (var val in dayList)
                    varianceSum += (val - avr) * (val - avr);

                dev = Math.Sqrt(varianceSum / (dayList.Count - 1));
            }
            else
            {
                dev = 0;
            }

            일간변동평균편차 = $"{avr:0.#}/{dev:0.#}";
            return true;
        }
        public static double 누적거래액환산율(int hhmmss)
        {
            double value = 0;
            if (hhmmss > 10000) // if 6 digit is passed, make it 4 digit
                hhmmss /= 100;

            int hh = Convert.ToInt32(hhmmss) / 100;
            int mm = Convert.ToInt32(hhmmss) % 100;

            if (hh >= 15) // 시작시간 9시
                          //if (hh >= 16) // 시작시간 10시
            {
                if (mm > 20)
                {
                    mm = 20;
                }
            }
            value = (hh - 9) * 60 + mm + 1; // 시작시간 9시
                                            //value = (hh - 10) * 60 + mm + 1; // 시작시간 10시
            if (value <= 0 || value > 6 * 60 + 21) // value가 0 이하 또는 381보다 크면 value = 381
                value = 6 * 60 + 21;


            double return_value = 381.0 / value;
            return return_value;
        }

        /// <summary>
        /// given current directory date, -1, +1 forward 
        /// and backward directory date search
        /// </summary>
        /// <param name="date_int"></param>
        /// <param name="updn"></param>
        /// <returns></returns>
        /// 
        public static int GetAdjacentDateFolder(int dateInt, int direction)
        {
            // 1. Get subdirectories with only 8-digit folder names
            var subdirs = Directory.GetDirectories(@"C:\BJS\분")
                .Select(Path.GetFileName)
                .Where(name => name?.Length == 8 && name.All(char.IsDigit))
                .ToList();

            // 2. Sort subdirs just in case they’re not ordered
            subdirs.Sort();

            string currentDate = dateInt.ToString();
            int index = subdirs.IndexOf(currentDate);

            if (index == -1) return -1; // date not found

            int nextIndex = index + direction;

            // 3. Bounds check
            if (nextIndex < 0 || nextIndex >= subdirs.Count)
                return -1;

            return Convert.ToInt32(subdirs[nextIndex]);
        }
        public static void CallNaverChart(string stock, string selection)
        {
            CPUTILLib.CpStockCode _cd = new CPUTILLib.CpStockCode();

            if (stock == null)
                Process.Start("chrome.exe", "https://finance.naver.com/");
            //Process.Start("microsoft-edge:https://finance.naver.com/");
            else
            {
                string url;
                if (selection == "fchart") // fchart
                {
                    url = "https://finance.naver.com/item/fchart.nhn?code=";
                    //url = "microsoft-edge:https://finance.naver.com/item/fchart.nhn?code=";
                }
                else if (selection == "main") // main
                {
                    url = "https://finance.naver.com/item/main.naver?code="; // 투자자별 매매동향
                    //url = "microsoft-edge:https://finance.naver.com/item/main.nhn?code="; // 종합정보
                    //url = "microsoft-edge:https://finance.naver.com/item/main.nhn?code=";
                }
                else // foreign & institute buying history
                {
                    url = "https://finance.naver.com/item/frgn.naver?code=";
                }

                string code = _cd.NameToCode(stock);
                code = new String(code.Where(Char.IsDigit).ToArray());
                url += code;
                Process.Start("chrome.exe", url);

                //Process.Start("chrome.exe", $"--new-tab {url}{code}");
            }
        }
    }
}


