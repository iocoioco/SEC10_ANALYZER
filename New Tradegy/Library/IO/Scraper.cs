using HtmlAgilityPack;
using New_Tradegy.Library.Models;
using New_Tradegy.Library.Trackers;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using HtmlDocument = HtmlAgilityPack.HtmlDocument;

namespace New_Tradegy.Library.IO
{
    internal class Scraper
    {
        // krw not resolved

        private static readonly HttpClient _client = new HttpClient();

        public static async Task task_major_indices()
        {
            // 첫 시작 딜레이
            await Task.Delay(12_000);

            while (true)
            {
                if (!wk.isWorkingHour())
                {
                    await Task.Delay(10_000);
                    continue;
                }

                HtmlDocument doc = null;
                const string url = "https://www.investing.com/indices/major-indices";

                try
                {
                    var html = await _client.GetStringAsync(url);
                    doc = new HtmlDocument();
                    doc.LoadHtml(html);
                }
                catch
                {
                    await Task.Delay(10_000);
                    continue;
                }

                if (doc == null)
                {
                    await Task.Delay(10_000);
                    continue;
                }

                // 테이블 노드 찾기 (필요하면 selector 더 정교하게)
                var tableNode = doc.DocumentNode.SelectSingleNode("//table");
                if (tableNode == null)
                {
                    await Task.Delay(10_000);
                    continue;
                }

                var table = tableNode
                    .Descendants("tr")
                    .Skip(1)
                    .Where(tr => tr.Elements("td").Count() > 1)
                    .Select(tr => tr.Elements("td").Select(td => td.InnerText.Trim()).ToList())
                    .ToList();

                // 행/열 개수 최소 확인 (32, 36, 29, 37 행, 6열까지 접근)
                if (table.Count <= 37 || table[0].Count <= 6)
                {
                    await Task.Delay(10_000);
                    continue;
                }

                try
                {
                    float ParsePercent(string s)
                    {
                        s = s.Replace("%", "").Trim();
                        float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v);
                        return v;
                    }

                    MajorIndex.Instance.ShanghaiIndex = ParsePercent(table[32][6]);
                    MajorIndex.Instance.HangSengIndex = ParsePercent(table[36][6]);
                    MajorIndex.Instance.NikkeiIndex = ParsePercent(table[29][6]);
                    float 대만가권 = ParsePercent(table[37][6]);

                    // UI 업데이트 (여기서 UI 스레드 보장 필요!)
                    g.controlPane.SetCellValue(2, 0, MajorIndex.Instance.ShanghaiIndex.ToString());
                    g.controlPane.SetCellValue(2, 1, MajorIndex.Instance.HangSengIndex.ToString());
                    g.controlPane.SetCellValue(2, 2, MajorIndex.Instance.NikkeiIndex.ToString());
                    g.controlPane.SetCellValue(2, 3, 대만가권.ToString());

                }
                catch
                {
                    // 파싱/인덱스 에러 시 조용히 넘김 (원하면 로그 추가)
                }

                await Task.Delay(30_000);
            }
        }


        public static async Task ScrapeUSDKRW()
        {
            string url = "https://kr.investing.com/currencies/usd-krw";

            try
            {
                // Set up ChromeDriver options for headless mode
                var options = new ChromeOptions();
                options.AddArgument("--headless"); // Run Chrome in headless mode
                options.AddArgument("--disable-gpu");
                options.AddArgument("--no-sandbox");
                options.AddArgument("--disable-dev-shm-usage");

                using (var driver = new ChromeDriver(options))
                {
                    // Navigate to the target URL
                    driver.Navigate().GoToUrl(url);

                    // Wait for the page to fully load
                    WebDriverWait wait = new WebDriverWait(driver, TimeSpan.FromSeconds(30));
                    wait.Until(d => ((IJavaScriptExecutor)d).ExecuteScript("return document.readyState").Equals("complete"));

                    // Wait for the specific element to become visible using its CSS selector
                    var element = wait.Until(ExpectedConditions.ElementIsVisible(By.CssSelector("div[data-test='instrument-price-last']")));

                    if (element != null)
                    {
                        // Extract the price string (e.g., "1,368.37")
                        string priceString = element.Text;

                        // Optionally remove any commas (e.g., "1,368.37" -> "1368.37")
                        priceString = priceString.Replace(",", "");

                        // Print or store the extracted value
                        //Console.WriteLine("Extracted USD/KRW exchange rate: " + priceString);

                        // Try to parse the value to a float or decimal
                        if (float.TryParse(priceString, out float usdKrw))
                        {
                            //Console.WriteLine($"Parsed USD/KRW exchange rate: {usdKrw}");
                        }
                        else
                        {
                            //Console.WriteLine("Failed to parse the USD/KRW exchange rate.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                //Console.WriteLine($"An error occurred: {ex.Message}");
            }

            await Task.Delay(1000); // Simulating an async operation
        }
        public static async Task task_usd_krw()
        {
            while (true)
            {
                if (!wk.isWorkingHour())
                {
                    await Task.Delay(10 * 1000); // Non-blocking delay
                    continue;
                }

                string url = "https://kr.investing.com/currencies/usd-krw";

                try
                {
                    var options = new ChromeOptions();
                    options.AddArgument("--headless");
                    options.AddArgument("--disable-gpu");
                    options.AddArgument("--no-sandbox");
                    options.AddArgument("--disable-dev-shm-usage");

                    using (var driver = new ChromeDriver(options))
                    {
                        driver.Navigate().GoToUrl(url);

                        // Wait for the page to fully load
                        new WebDriverWait(driver, TimeSpan.FromSeconds(30)).Until(
                            d => ((IJavaScriptExecutor)d).ExecuteScript("return document.readyState").Equals("complete")
                        );

                        // Use WebDriverWait to wait for the element to load
                        WebDriverWait wait = new WebDriverWait(driver, TimeSpan.FromSeconds(30));  // Increased timeout

                        // Updated to use WaitHelpers from SeleniumExtras
                        var element = wait.Until(
                            SeleniumExtras.WaitHelpers.ExpectedConditions.ElementIsVisible(By.CssSelector("div[data-test='instrument-price-last']"))
                        );

                        if (element != null)
                        {
                            string priceString = element.Text;
                            priceString = priceString.Replace(",", "");  // Handle commas

                            //Console.WriteLine("Extracted price string: " + priceString);

                            if (float.TryParse(priceString, out float usdKrw))
                            {
                           
                                if (g.controlPane.GetCellValue(1, 1) != usdKrw.ToString())
                                    g.controlPane.SetCellValue(1, 1, usdKrw.ToString());
                            }
                            else
                            {
                                //Console.WriteLine("Failed to parse the USD/KRW exchange rate.");
                            }
                        }
                        else
                        {
                            //Console.WriteLine("Exchange rate element not found.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    //Console.WriteLine($"An error occurred: {ex.Message}");
                }

                await Task.Delay(60 * 1000);  // Wait 60 seconds before the next iteration
            }
        }
        // updated on 20241020
        public static async Task task_usd_krw_new_new_old()
        {
            while (true)
            {
                if (!wk.isWorkingHour())
                {
                    await Task.Delay(10 * 1000); // Non-blocking delay
                    continue;
                }

                string url = "https://kr.investing.com/currencies/usd-krw";

                try
                {
                    var options = new ChromeOptions();
                    options.AddArgument("--headless");
                    options.AddArgument("--disable-gpu");
                    options.AddArgument("--no-sandbox");
                    options.AddArgument("--disable-dev-shm-usage");

                    using (var driver = new ChromeDriver(options))
                    {
                        driver.Navigate().GoToUrl(url);

                        // Wait for the page to fully load
                        new WebDriverWait(driver, TimeSpan.FromSeconds(60)).Until(
                            d => ((IJavaScriptExecutor)d).ExecuteScript("return document.readyState").Equals("complete")
                        );

                        // Check for iframe and switch to it (if needed)
                        // driver.SwitchTo().Frame(driver.FindElement(By.CssSelector("iframe-selector"))); // Uncomment if necessary

                        // Use WebDriverWait to wait for the element to load
                        WebDriverWait wait = new WebDriverWait(driver, TimeSpan.FromSeconds(60));  // Increased to 60 seconds

                        // Updated to use WaitHelpers from SeleniumExtras
                        var element = wait.Until(
                            SeleniumExtras.WaitHelpers.ExpectedConditions.ElementIsVisible(By.CssSelector("div[data-test='instrument-price-last']"))
                        );

                        if (element != null)
                        {
                            string priceString = element.Text;
                            priceString = priceString.Replace(",", "");  // Handle commas

                            //Console.WriteLine("Extracted price string: " + priceString);

                            if (float.TryParse(priceString, out float usdKrw))
                            {
 
                                if (g.controlPane.GetCellValue(1, 1) != usdKrw.ToString())
                                    g.controlPane.SetCellValue(1, 1, usdKrw.ToString());
                            }
                            else
                            {
                                //Console.WriteLine("Failed to parse the USD/KRW exchange rate.");
                            }
                        }
                        else
                        {
                            //Console.WriteLine("Exchange rate element not found.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    //Console.WriteLine($"An error occurred: {ex.Message}");
                }

                await Task.Delay(60 * 1000);  // Wait 60 seconds before the next iteration
            }
        }
        // row 2 kwd_usd, // row 3 kospi, kosdaq
        public static void task_usd_krw_old()
        {

            while (true)
            {
                if (!wk.isWorkingHour())
                {
                    Thread.Sleep(10 * 1000);
                    continue;
                }

                string url = "https://kr.investing.com/currencies/usd-krw";

                try
                {
                    // Set up ChromeDriver
                    var options = new ChromeOptions();
                    options.AddArgument("--headless"); // Run in headless mode
                    options.AddArgument("--disable-gpu"); // Disable GPU
                    options.AddArgument("--no-sandbox"); // Bypass OS security model
                    options.AddArgument("--disable-dev-shm-usage"); // Overcome limited resource problems

                    using (var driver = new ChromeDriver(options))
                    {
                        driver.Navigate().GoToUrl(url);
                        Thread.Sleep(5000); // Wait for the page to load

                        // Locate the element directly using Selenium
                        var element = driver.FindElement(By.CssSelector("div[data-test='instrument-price-last']"));

                        if (element != null)
                        {
                            string priceString = element.Text;
                            //Console.WriteLine("Extracted price string: " + priceString); // Log extracted price string
                            if (float.TryParse(priceString, out float usdKrw))
                            {

                         
                                if (g.controlPane.GetCellValue(1, 1) != usdKrw.ToString())
                                {
                                    g.controlPane.SetCellValue(1, 1, usdKrw.ToString());
                                }
                            }
                            else
                            {
                                // Log parsing error
                                //Console.WriteLine("Failed to parse the USD/KRW exchange rate.");
                            }
                        }
                        else
                        {
                            // Log element not found
                            //Console.WriteLine("Exchange rate element not found.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Log unexpected exceptions and continue
                    //Console.WriteLine($"An error occurred: {ex.Message}");
                }

                Thread.Sleep(60 * 1000);
            }
        }
        
        // updated on 20241020
        public static async Task task_major_indices_20251120()
        {
            // Initial delay before starting (12 seconds)
            await Task.Delay(1000 * 12);  // Replaced Thread.Sleep with await Task.Delay for non-blocking delay

            while (true)
            {
                // Check if it's working hours; if not, wait 10 seconds and continue
                if (!wk.isWorkingHour())
                {
                    await Task.Delay(1000 * 10);  // Replaced Thread.Sleep with await Task.Delay
                    continue;
                }

                string url = "https://www.investing.com/indices/major-indices";
                HtmlAgilityPack.HtmlWeb web = new HtmlAgilityPack.HtmlWeb();
                HtmlAgilityPack.HtmlDocument doc = null;

                try
                {
                    // Load the page asynchronously
                    // old doc = web.Load(url);  // HtmlAgilityPack does not support async loading directly, so this remains synchronous
                    using (var client = new HttpClient())
                    {
                        string html = await client.GetStringAsync(url);
                        doc = new HtmlDocument();
                        doc.LoadHtml(html);
                    }
                }
                catch (Exception e)
                {
                    // If loading fails, log the exception and continue (retry after 10 seconds)
                    //Console.WriteLine($"Exception occurred: {e.Message}");
                    await Task.Delay(1000 * 10);  // Non-blocking delay before retry
                    continue;
                }

                if (doc == null)
                {
                    await Task.Delay(1000 * 10);  // Non-blocking delay before retry
                    continue;
                }

                // Extract rows directly from the tabletable node
                List<List<string>> table = doc.DocumentNode.SelectSingleNode("//table")
                    .Descendants("tr")
                    .Skip(1)
                    .Where(tr => tr.Elements("td").Count() > 1)
                    .Select(tr => tr.Elements("td").Select(td => td.InnerText.Trim()).ToList())
                    .ToList();

                if (table.Count <= 37 || table[0].Count <= 6)
                {
                    await Task.Delay(10000);
                    continue;
                }

                // Parse and update indices
                try
                {
                    if (float.TryParse(table[32][6].Trim('%'), out float val1))
                        MajorIndex.Instance.ShanghaiIndex = val1;

                    if (float.TryParse(table[36][6].Trim('%'), out float val2))
                        MajorIndex.Instance.HangSengIndex = val2;

                    if (float.TryParse(table[29][6].Trim('%'), out float val3))
                        MajorIndex.Instance.NikkeiIndex = val3;

                    float 대만가권;
                    string t = table[37][6].Trim('%');
                    float.TryParse(t, out 대만가권);

                    // Update cells (you can wrap this part too)
                    if (g.controlPane.GetCellValue(2, 0) != MajorIndex.Instance.ShanghaiIndex.ToString())
                        g.controlPane.SetCellValue(2, 0, MajorIndex.Instance.ShanghaiIndex.ToString());

                    if (g.controlPane.GetCellValue(2, 1) != MajorIndex.Instance.HangSengIndex.ToString())
                        g.controlPane.SetCellValue(2, 1, MajorIndex.Instance.HangSengIndex.ToString());

                    if (g.controlPane.GetCellValue(2, 2) != MajorIndex.Instance.NikkeiIndex.ToString())
                        g.controlPane.SetCellValue(2, 2, MajorIndex.Instance.NikkeiIndex.ToString());

                    if (g.controlPane.GetCellValue(2, 3) != 대만가권.ToString())
                        g.controlPane.SetCellValue(2, 3, 대만가권.ToString());
                }
                catch (Exception ex)
                {
                    //Console.WriteLine($"⚠️ Parsing or table index error: {ex.Message}");
                }


                // Wait for 30 seconds before the next iteration
                await Task.Delay(1000 * 30);  // Non-blocking delay
            }
        }


        // updated on 20241020
        public static async Task task_bitcoin()
        {
            // Initial delay before starting (13 seconds)
            await Task.Delay(1000 * 13);  // Replaced Thread.Sleep with await Task.Delay for non-blocking delay

            while (true)
            {
                // Check if it's working hours; if not, wait for 120 seconds and continue
                if (!wk.isWorkingHour())
                {
                    await Task.Delay(1000 * 120);  // Replaced Thread.Sleep with await Task.Delay
                    continue;
                }

                string url = "https://www.investing.com/crypto/bitcoin";

                HtmlAgilityPack.HtmlWeb web = new HtmlAgilityPack.HtmlWeb();
                HtmlAgilityPack.HtmlDocument doc = null;

                try
                {
                    // Load the page synchronously (HtmlAgilityPack doesn't support async loading)
                    doc = web.Load(url);
                }
                catch (Exception e)
                {
                    //Console.WriteLine($"Exception occurred: {e.Message}");
                    await Task.Delay(12000);  // Non-blocking delay before retry
                    continue;
                }

                if (doc == null)
                {
                    await Task.Delay(12000);  // Non-blocking delay before retry
                    continue;
                }

                // Extract the Bitcoin price from the page
                float bitcoin;
                var t = doc.DocumentNode.SelectSingleNode(".//span[contains(@class, 'pid-1057391-last')]").InnerHtml;
                float.TryParse(t, out bitcoin);

                // Update global data table
               
                if(g.controlPane.GetCellValue(3, 1) != bitcoin.ToString())
                    g.controlPane.SetCellValue(3, 1, bitcoin.ToString());

                // Wait for 12 seconds before the next iteration
                await Task.Delay(12000);  // Non-blocking delay
            }
        }


        // updated on 20241020
        public static async Task task_commodities_futures()
        {
            // Initial delay before starting (14 seconds)
            await Task.Delay(1000 * 14);  // Replaced Thread.Sleep with await Task.Delay for non-blocking delay

            while (true)
            {
                // Check if it's working hours; if not, wait for 10 seconds and continue
                if (!wk.isWorkingHour())
                {
                    await Task.Delay(1000 * 10);  // Replaced Thread.Sleep with await Task.Delay
                    continue;
                }

                string url = "https://www.investing.com/commodities/real-time-futures";
                HtmlAgilityPack.HtmlWeb web = new HtmlAgilityPack.HtmlWeb();
                HtmlAgilityPack.HtmlDocument doc = null;

                try
                {
                    // Load the page synchronously (HtmlAgilityPack doesn't support async loading)
                    doc = web.Load(url);
                }
                catch (Exception e)
                {
                    //Console.WriteLine($"Exception occurred: {e.Message}");
                    await Task.Delay(10000);  // Non-blocking delay before retry
                    continue;
                }

                if (doc == null)
                {
                    await Task.Delay(10000);  // Non-blocking delay before retry
                    continue;
                }

                // Extract data from the table
                List<List<string>> table = doc.DocumentNode.SelectSingleNode("//table")
                    .Descendants("tr")
                    .Skip(1)
                    .Where(tr => tr.Elements("td").Count() > 1)
                    .Select(tr => tr.Elements("td").Select(td => td.InnerText.Trim()).ToList())
                    .ToList();

                // Parse the commodities data
                float gold;
                string t = table[0][3];
                float.TryParse(t, out gold);

                float copper;
                t = table[4][3];
                float.TryParse(t, out copper);

                float wti;
                t = table[7][3];
                float.TryParse(t, out wti);

                float gas;
                t = table[9][3];
                float.TryParse(t, out gas);

                float aluminum;
                t = table[13][3];
                float.TryParse(t, out aluminum);

                float wheat;
                t = table[17][3];
                float.TryParse(t, out wheat);

                float lumber;
                t = table[32][3];
                float.TryParse(t, out lumber);

                // Wait for a period before running the loop again (you can adjust this delay)
                await Task.Delay(10000);  // Non-blocking delay before the next loop iteration
            }
        }


        public static async Task task_jsb()
        {
            while (true)
            {
                // Get the current time in HHmm format
                int HHmm = Convert.ToInt32(DateTime.Now.ToString("HHmm"));

                // Check if the current time is at 59 or 29 minutes past the hour, and if it hasn't been alerted yet
                if ((HHmm % 100 == 59 || HHmm % 100 == 29) && g.AlarmedHHmm != HHmm)
                {
                    // Play sound and update the last alerted time
                    Utils.SoundUtils.Sound("일반", "to jsb");
                    g.AlarmedHHmm = HHmm;

                    // Wait asynchronously for 59 seconds before checking again
                    await Task.Delay(59 * 1000);
                }

                // Small delay to prevent excessive CPU usage in the loop
                await Task.Delay(1000); // Wait 1 second before checking the time again
            }
        }




    }
}
