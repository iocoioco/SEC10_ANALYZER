using New_Tradegy.Library.UI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace New_Tradegy.Library.Trackers.Panel
{
    internal class NotifyAction
    {
        public static void AddSelectedStocksToChartAsync(NotifyBox box)
        {
            string selectedText = box.SelectedText?.Trim();
            if (string.IsNullOrEmpty(selectedText))
            {
                box.AppendTextWithStyle("[Warn] No stock selected", Color.OrangeRed, FontStyle.Italic);
                return;
            }

            box.AppendTextWithStyle("[Processing] Adding selected stocks...", Color.Gray, FontStyle.Italic);

            Task.Run(() =>
            {
                string normalized = selectedText
                    .Replace(",", " ")
                    .Replace("\n", " ")
                    .Replace("\r", " ")
                    .Replace("\t", " ");

                string[] stocks = normalized
                    .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .ToArray();


                int added = 0;
                foreach (var stock in stocks)
                {
                    bool result = false;

                    g.MainForm.Invoke((MethodInvoker)(() =>
                    {

                        g.StockManager.InterestedWithBidList.Add(stock);
                        box.AppendTextWithStyle($"[Added] {stock}", Color.DarkGreen);
                    }));

                    if (result)
                        added++;
                }

                g.MainForm.Invoke((MethodInvoker)(() =>
                {
                    box.AppendTextWithStyle($"[Done] {added} stocks added to chart", Color.Blue, FontStyle.Bold);
                }));
            });
        }
    }
}
