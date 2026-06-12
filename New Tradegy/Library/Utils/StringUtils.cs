using New_Tradegy.Library.Core;
using New_Tradegy.Library.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace New_Tradegy.Library.Utils
{
    internal class StringUtils
    {
        public static string TruncateLinesRemovePartial(string text, int maxCharsPerLine)
        {
            if (string.IsNullOrEmpty(text)) return text;

            var sb = new StringBuilder();
            string[] lines = text.Split('\n');

            foreach (var line in lines)
            {
                if (line.Length > maxCharsPerLine)
                {
                    string cut = line.Substring(0, maxCharsPerLine);

                    // If the cut ends in the middle of a "word" (alphanumeric or dot/slash)
                    if (!char.IsWhiteSpace(cut.Last()) && maxCharsPerLine < line.Length &&
                        !char.IsWhiteSpace(line[maxCharsPerLine]))
                    {
                        // remove last incomplete token
                        int lastSpace = cut.LastIndexOf(' ');
                        if (lastSpace > -1)
                            cut = cut.Substring(0, lastSpace);
                        else
                            cut = string.Empty;
                    }

                    sb.AppendLine(cut.TrimEnd());
                }
                else
                {
                    sb.AppendLine(line);
                }
            }

            // Remove the very last newline if you don’t want it
            if (sb.Length > 0 && sb[sb.Length - 1] == '\n')
                sb.Length--;

            return sb.ToString();
        }


        public static string WrapByCharCount(string text, int maxCharsPerLine)
        {
            if (string.IsNullOrEmpty(text) || maxCharsPerLine <= 0) return text;

            var sb = new StringBuilder(text.Length + text.Length / Math.Max(1, maxCharsPerLine));

            int count = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];

                // keep existing newlines
                if (ch == '\r') continue;
                if (ch == '\n')
                {
                    sb.Append('\n');
                    count = 0;
                    continue;
                }

                sb.Append(ch);
                count++;

                if (count >= maxCharsPerLine)
                {
                    sb.Append('\n');
                    count = 0;
                }
            }

            // trim trailing newline if any
            if (sb.Length > 0 && sb[sb.Length - 1] == '\n') sb.Length--;

            return sb.ToString();
        }

        public static float GetAnnotationPixelWidth(
    Chart chart, ChartArea area, RectangleF rect,
    bool rectIsPercentOfArea = true)   // set false if rect.Width is already pixels
        {
            // width of the ChartArea in pixels
            float areaPx = (float)(chart.Width * area.Position.Width / 100.0);

            if (rectIsPercentOfArea)
            {
                // rect.Width is 0..100 % of the ChartArea
                float wPct = Math.Max(0f, Math.Min(100f, rect.Width));
                return areaPx * (wPct / 100f);
            }
            else
            {
                // rect.Width is already pixels
                return rect.Width;
            }
        }

        public static string WrapTextByPixels(string text, Font font, float maxWidthPx, Control measureOn)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            var sb = new StringBuilder();

            using (var g = measureOn.CreateGraphics())
            using (var fmt = new StringFormat(StringFormat.GenericTypographic))
            {
                fmt.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;

                string[] lines = text.Replace("\r\n", "\n").Split('\n');

                for (int li = 0; li < lines.Length; li++)
                {
                    string line = lines[li];
                    int start = 0;

                    while (start < line.Length)
                    {
                        int end = start + 1;

                        // grow the slice until it would exceed the width
                        for (; end <= line.Length; end++)
                        {
                            string slice = line.Substring(start, end - start);
                            SizeF size = g.MeasureString(slice, font, int.MaxValue, fmt);

                            if (size.Width > maxWidthPx)
                            {
                                // back off one char if we overshot
                                end = Math.Max(start + 1, end - 1);
                                break;
                            }
                        }

                        if (end > line.Length) end = line.Length;

                        sb.Append(line.Substring(start, end - start));
                        start = end;

                        if (start < line.Length) sb.Append('\n'); // wrap to next visual line
                    }

                    if (li < lines.Length - 1) sb.Append('\n'); // preserve original newline
                }
            }

            // trim a trailing newline if present
            if (sb.Length > 0 && sb[sb.Length - 1] == '\n') sb.Length -= 1;

            return sb.ToString();
        }

        public static string[] CollectWordsFromString(string text)
        {
            return text.Split(new char[] { ' ', ',', '.', '!', '?', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        }

        public static string r3_display_lines_body(StockData t)
        {
            if (t?.Api?.x == null) return "";

            if (!ChartLayoutUtils.TryGetDrawRange(t, out int start, out int endEx))
                return "";

            var api = t.Api;
            var x = api.x;
            int nrow = api.nrow;

            endEx = Math.Min(endEx, nrow);
            if (endEx <= 0) return "";

            start = Math.Max(0, start);

            int desiredStart = endEx - g.v.q_advance_lines - 7;
            if (desiredStart > start)
                start = Math.Max(0, desiredStart);

            if (endEx <= start)
                return "";

            var sb = new StringBuilder(4096);

            bool isKodex = t.Stock != null && t.Stock.Contains("KODEX");

            if (isKodex)
            {
                for (int j = start; j < endEx; j++)
                {
                    int time = x[j, 0];
                    if (time == 0 || time > 152100) break;

                    for (int k = 0; k < 12; k++)
                    {
                        if (k == 7) continue;

                        if (endEx - j - 1 == g.v.q_advance_lines && k == 0)
                            sb.AppendFormat("{0, 7}", "* " + time);
                        else
                            sb.AppendFormat("{0, 7}", x[j, k]);
                    }
                    sb.AppendLine();
                }
            }
            else
            {
                double money_factor = api.전일종가 / g.천만원; // ✅ 루프 밖으로

                for (int j = start; j < endEx; j++)
                {
                    int time = x[j, 0];
                    if (time == 0 || time > 152100) break;

                    for (int k = 0; k < 13; k++)
                    {
                        if (k == 4 || k == 5 || k == 6 || k == 7 || k == 9 || k == 11)
                            continue;

                        string str_add;

                        if (k == 12)
                        {
                            double dealt = 0.0, prog = 0.0, foreign = 0.0, percentage = 0.0;

                            double acc_program = x[j, 4] * money_factor;

                            if (j - 1 >= 0)
                            {
                                prog = (x[j, 4] - x[j - 1, 4]) * money_factor;
                                foreign = (x[j, 5] - x[j - 1, 5]) * money_factor;
                                dealt = (x[j, 7] - x[j - 1, 7]) * money_factor;

                                if (MathUtils.IsSafeToDivide(dealt))
                                    percentage = prog / dealt * 100.0;
                            }

                            string tstring =
                                "     " + (int)dealt + "  " +
                                (int)prog + "  " +
                                (int)percentage + "%" + "  " +
                                (int)acc_program;

                            str_add = tstring.PadLeft(30);
                        }
                        else if (k == 8)
                        {
                            str_add = string.Format("{0,15}",
                                (x[j, k] - x[j, k + 1]) + " / " + (x[j, k] + x[j, k + 1]));
                        }
                        else if (k == 10)
                        {
                            str_add = string.Format("{0,12}", x[j, k] + " / " + x[j, k + 1]);
                        }
                        else
                        {
                            if (endEx - j - 1 == g.v.q_advance_lines && k == 0)
                                str_add = string.Format("{0, 10}", "*" + x[j, k]);
                            else if (k == 3)
                                str_add = string.Format("{0, 11}", x[j, k] / 100);
                            else
                                str_add = string.Format("{0, 11}", x[j, k]);
                        }

                        sb.Append(str_add);
                    }

                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }

        public static string r3_display_매수_매도(StockData t)
        {
            if (t?.Api?.x == null) return "";

            if (!ChartLayoutUtils.TryGetDrawRange(t, out int start, out int endEx))
                return "";

            var api = t.Api;
            var x = api.x;
            int nrow = api.nrow;

            int end_row = Math.Min(endEx, nrow);          // end-exclusive
            int start_row = Math.Max(Math.Max(0, end_row - 5), Math.Max(0, start));

            if (end_row <= start_row) return "";

            var sb = new StringBuilder(1024);
            bool isKodex = t.Stock != null && t.Stock.Contains("KODEX");

            if (isKodex)
            {
                for (int j = start_row; j < end_row; j++)
                {
                    int time = x[j, 0];
                    if (time == 0 || time > 152100) break;

                    for (int k = 1; k < 12; k++)
                    {
                        if (k == 7) continue;

                        // ✅ k==0은 불가능 → 첫 출력컬럼(k==1)에 별표
                        if (end_row - j - 1 == g.v.q_advance_lines && k == 1)
                            sb.AppendFormat("{0, 7}", "* " + x[j, k]);
                        else
                            sb.AppendFormat("{0, 7}", x[j, k]);
                    }
                    sb.AppendLine();
                }
            }
            else
            {
                double money_factor = api.전일종가 / g.천만원;

                for (int j = start_row; j < end_row; j++)
                {
                    int time = x[j, 0];
                    if (time == 0 || time > 152100) break;

                    for (int k = 1; k < 13; k++)
                    {
                        if (k == 4 || k == 5 || k == 6 || k == 7 || k == 9 || k == 11)
                            continue;

                        string str_add;

                        if (k == 12)
                        {
                            double dealt = 0.0, prog = 0.0, foreign = 0.0, pct = 0.0;
                            double acc_prog = x[j, 4] * money_factor;

                            if (j - 1 >= 0)
                            {
                                prog = (x[j, 4] - x[j - 1, 4]) * money_factor;
                                foreign = (x[j, 5] - x[j - 1, 5]) * money_factor;
                                dealt = (x[j, 7] - x[j - 1, 7]) * money_factor;

                                if (MathUtils.IsSafeToDivide(dealt))
                                    pct = (prog / dealt) * 100.0;
                            }

                            str_add = "     " + (int)dealt + "  " + (int)prog + "  " + (int)pct + "%" + "  " + (int)acc_prog;
                        }
                        else if (k == 8 || k == 10)
                        {
                            str_add = string.Format("{0,12}", x[j, k] + " / " + x[j, k + 1]);
                        }
                        else
                        {
                            if (end_row - j - 1 == g.v.q_advance_lines && k == 1) // ✅ k==0 불가 → k==1
                                str_add = "*" + string.Format("{0, 7}", x[j, k]);
                            else
                                str_add = string.Format("{0, 8}", x[j, k]);
                        }

                        sb.Append(str_add);
                    }

                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }


        public static string r3_display_lines_header(StockData o)
        {
            string str = "";

            if (o.Stock.Contains("KODEX"))
            {
                str = o.Stock + "\n\n";
            }
            else
            {
                str = o.Stock + "   " + Math.Round(o.Statistics.일간변동편차, 1) + "  " + (o.Post.종누천).ToString("F0") +
                    "   (일간변동편차, 종누천)" + "\n\n";

                // Corr 딕셔너리에서 상관계수 상위 5개만 출력
                int count = 0;
                foreach (var kv in o.Misc.Corr.OrderByDescending(x => x.Value).Take(5))
                {
                    string targetCode = kv.Key;    // 종목 코드
                    double rho = kv.Value;         // 상관계수
                    str += string.Format(" {0, -15} {1, -10:F2}", targetCode, rho) + "\n";
                    count++;
                    if (count >= 5) break;
                }
                str += "\n";

                // =============================
                // 1. 피분 ~ 배합
                // =============================
                str += string.Format(
                    "{0,-24}{1,-24}{2,-24}{3,-24}\n",
                    string.Format("푀분({0,5:F1}, {1,5:F1})", o.Statistics.푀분_avr, o.Statistics.푀분_dev),
                    string.Format("거분({0,5:F1}, {1,5:F1})", o.Statistics.거분_avr, o.Statistics.거분_dev),
                    string.Format("배차({0,5:F0}, {1,5:F0})", o.Statistics.배차_avr, o.Statistics.배차_dev),
                    string.Format("배합({0,5:F0}, {1,5:F0})", o.Statistics.배합_avr, o.Statistics.배합_dev)
                );

                

                // =============================
                // 2. 종누 ~ 기누
                // =============================
                str += string.Format(
                    "{0,-24}{1,-24}{2,-24}\n",
                    string.Format("종누({0,7:F0}, {1,7:F0})", o.Statistics.종누_avr, o.Statistics.종누_dev),
                    string.Format("푀누({0,7:F0}, {1,7:F0})", o.Statistics.푀누_avr, o.Statistics.푀누_dev),
                    string.Format("기누({0,7:F0}, {1,7:F0})", o.Statistics.기누_avr, o.Statistics.기누_dev)
                );
                str += "\n";

            }

            return str;
        }

        public static void r3_display_lines(Chart chart, string stock, int row_id, int col_id)
        {
            if (!g.StockRepo.Contains(stock))
                return;

            StockData data = g.StockRepo.TryGetDataOrNull(stock); // or .TryGetDataOrNull(stock)
            if (data == null)
                return;

            string str = "";

            str = r3_display_lines_header(data);
            str += r3_display_lines_body(data);

            string temp_file = @"C:\BJS\Z Temp\temp.txt";

            lock (g.lockObject)
            {
                if (File.Exists(temp_file))
                    File.Delete(temp_file);
            }

            File.WriteAllText(temp_file, str);
            Process.Start(temp_file);
        }

        public static string reacal_only(int[,] x, int 전일종가)
        {
            string str = "";

            for (int j = 0; j < x.Length / 12; j++) // bound exist not the size
            {
                if (j < 0)
                    continue;

                if (x[j, 0] == 0 || x[j, 0] > 152100) // if time is not set then stop writing
                    break;

                string str_add = "";

                for (int k = 0; k < 13; k++) // bound exist not the size
                {
                    // 분 디렉토리 내의 날짜별 종목별 일반 데이터 순서
                    // 0       1       2    3      4      5       6      7       8      9       10     11
                    // 시간 가격  수급  강도  프로  외인   기관  거량   양배  음배   수연  강연

                    // r3 후 컬럼 순서, 실제 4(외인누적매수량), 5(기관누적매수량) 컬럼은 잘 보지않음
                    // 0      1      2      3      4       5       6      7       8      9        10
                    // 시간 가격  수급  강도  외인   기관   양배  음배   수연  강연   거분/프외/%/누적프외
                    if (k == 4 || k == 5 || k == 6 || k == 7 || k == 9 || k == 11) // 프돈_천만원/분 / 프돈 %
                    {
                        continue;
                    }
                    if (k == 12)
                    {
                        double money_factor = 전일종가 / g.천만원; // 천만원
                        double dealt_money_per_minute = 0.0;
                        double program_money_per_minute = 0.0;
                        double foreign_money_per_minute = 0.0;
                        double percentage_program_per_minute = 0.0;

                        double accumulated_foreign_money = x[j, 5] * money_factor;
                        double accumulated_program_money = x[j, 4] * money_factor;
                        if (j - 1 >= 0)
                        {
                            program_money_per_minute = (x[j, 4] - x[j - 1, 4]) * money_factor;
                            foreign_money_per_minute = (x[j, 5] - x[j - 1, 5]) * money_factor;
                            dealt_money_per_minute = (x[j, 7] - x[j - 1, 7]) * money_factor;
                            if (MathUtils.IsSafeToDivide(dealt_money_per_minute))
                            {
                                //percentage_program_and_foreign_per_minute = (double)(program_money_per_minute + foreign_money_per_minute) /
                                //    dealt_money_per_minute * 100.0;
                                // 20230416 외돈 제외
                                percentage_program_per_minute = (double)(program_money_per_minute) /
                                    dealt_money_per_minute * 100.0;
                            }
                        }

                        //str_add = "     " + Math.Round(dealt_money_per_minute / 10.0, 1) + "  " +
                        //   Math.Round(program_money_per_minute / 10.0 + foreign_money_per_minute / 10.0, 1) + "  " +
                        //   (int)percentage_program_and_foreign_per_minute + "%" + "  " +
                        //   Math.Round(accumulated_program_money / 10.0 + accumulated_foreign_money / 10.0, 1);
                        // 20230416 외돈 제외
                        str_add = "     " + (int)dealt_money_per_minute + "  " +
                           (int)program_money_per_minute + "  " +
                           (int)percentage_program_per_minute + "%" + "  " +
                           (int)accumulated_program_money;

                        if (x.Length - j - 1 == g.v.q_advance_lines)
                            str_add += "   ***";
                    }
                    else if (k == 8)
                        str_add = String.Format("{0,12}", x[j, k] + " / " + x[j, k + 1]);
                    else if (k == 10)
                        str_add = String.Format("{0,12}", x[j, k] + " / " + x[j, k + 1]);
                    else
                    {
                        if (x.Length - j - 1 == g.v.q_advance_lines && k == 0)

                            str_add = "*" + String.Format("{0, 7}", x[j, k]); // FORMAT COLUMN CONTROL
                        else
                            str_add = String.Format("{0, 8}", x[j, k]);
                    }
                    str += str_add;
                }
                str += "\n";
            }
            return str;
        }

        public static string r3_display_lines_after_recalculation(StockData o)
        {
            string str = o.Stock + "   " + Math.Round(o.Statistics.일간변동편차, 1) + "  " + (o.Post.종누천 / 10).ToString("F0") + "\n\n";
            int end_row = 0;

            int count = 0;
            foreach (var kv in o.Misc.Corr.OrderByDescending(x => x.Value).Take(5))
            {
                string targetCode = kv.Key;    // 종목 코드
                double rho = kv.Value;         // 상관계수
                str += string.Format("  {0,-10} {1,6:F2}", targetCode, rho) + "\n";

                count++;
                if (count >= 5) break;
            }


            str += "  " + Math.Round(o.Statistics.푀분_avr * 10, 0) + "  " + Math.Round(o.Statistics.푀분_dev * 10, 0) + "    " +
                          Math.Round(o.Statistics.거분_avr * 10, 0) + "  " + Math.Round(o.Statistics.거분_dev * 10, 0) + "      푀분     거분\n" +
                   "  " + Math.Round(o.Statistics.배차_avr, 0) + "  " + Math.Round(o.Statistics.배차_dev, 0) + "    " +
                          Math.Round(o.Statistics.배합_avr, 0) + "  " + Math.Round(o.Statistics.배합_dev, 0) + "      배차     배합\n\n";

            if (!ChartLayoutUtils.TryGetDrawRange(o, out int start, out int end))
                return "";

            end_row = end;          // end-exclusive
            int start_row = start;      // test면 g.Npts[0], live면 0

            int desiredStart = end_row - g.v.q_advance_lines - 10;
            if (desiredStart > start_row)
                start_row = desiredStart;

            if (end_row <= start_row) return "";


            for (int j = start_row; j < end_row; j++)
            {
                if (j < 0) continue;
                if (o.Api.x[j, 0] == 0 || o.Api.x[j, 0] > 152100) break;

                string str_add = "";

                for (int k = 0; k < 13; k++)
                {
                    if (k == 4 || k == 5 || k == 6 || k == 7 || k == 9 || k == 11)
                        continue;

                    if (k == 12)
                    {
                        double money_factor = o.Api.전일종가 / g.천만원;
                        double dealt = 0.0, prog = 0.0, foreign = 0.0, percentage = 0.0;

                        double acc_foreign = o.Api.x[j, 5] * money_factor;
                        double acc_program = o.Api.x[j, 4] * money_factor;

                        if (j - 1 >= 0)
                        {
                            prog = (o.Api.x[j, 4] - o.Api.x[j - 1, 4]) * money_factor;
                            foreign = (o.Api.x[j, 5] - o.Api.x[j - 1, 5]) * money_factor;
                            dealt = (o.Api.x[j, 7] - o.Api.x[j - 1, 7]) * money_factor;

                            if (MathUtils.IsSafeToDivide(dealt))
                            {
                                percentage = prog / dealt * 100.0;
                            }
                        }

                        str_add = "     " + (int)dealt + "  " +
                                  (int)prog + "  " +
                                  (int)percentage + "%" + "  " +
                                  (int)acc_program;

                        if (end_row - j - 1 == g.v.q_advance_lines)
                            str_add += "   ***";
                    }
                    else if (k == 8)
                    {
                        str_add = string.Format("{0,12}", o.Api.x[j, k] + " / " + o.Api.x[j, k + 1]);
                    }
                    else if (k == 10)
                    {
                        str_add = string.Format("{0,12}", o.Api.x[j, k] + " / " + o.Api.x[j, k + 1]);
                    }
                    else
                    {
                        if (end_row - j - 1 == g.v.q_advance_lines && k == 0)
                            str_add = "*" + string.Format("{0, 7}", o.Api.x[j, k]);
                        else
                            str_add = string.Format("{0, 8}", o.Api.x[j, k]);
                    }

                    str += str_add;
                }

                str += "\n";
            }

            return str;
        }

        // Sensei 20250316
        public static int ExtractIntFromString(string input)
        {
            if (string.IsNullOrEmpty(input))
                return 0; // Return 0 for empty or null input

            long value = 0;
            bool isNegative = false;

            foreach (char c in input)
            {
                if (c == '-' && value == 0)
                {
                    isNegative = true; // Handle negative sign (only at start)
                }
                else if (c >= '0' && c <= '9')
                {
                    value = value * 10 + (c - '0');

                    // Check for int overflow
                    if (value > int.MaxValue)
                        return isNegative ? int.MinValue : int.MaxValue;
                }
            }

            return isNegative ? (int)-value : (int)value;
        }

        // Sensei 20250316
        public static void SwapValues<T>(ref T lhs, ref T rhs)
        {
            T temp = lhs;
            lhs = rhs;
            rhs = temp;
        }

        // Sensei 20250419
        public static string CycleStrings(string current, List<string> options)
        {
            if (options == null || options.Count == 0)
                return current; // or throw if you prefer strictness

            int index = options.IndexOf(current);
            return (index < 0 || index == options.Count - 1)
                ? options[0]
                : options[index + 1];
        }


        //public static string message(Form owner, string caption, string message, string defaultOption)
        //{
        //    var result = MessageBox.Show(owner, message, caption, MessageBoxButtons.YesNo,
        //                                 MessageBoxIcon.Question,
        //                                 defaultOption == "Yes" ? MessageBoxDefaultButton.Button1 : MessageBoxDefaultButton.Button2);
        //    return result == DialogResult.Yes ? "Yes" : "No";
        //}

        // replaced by the above method
        public static string message(string caption, string message, string button_selection)
        {
            DialogResult result;
            if (button_selection == "Yes")
                result = MessageBox.Show(message, caption, MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question, MessageBoxDefaultButton.Button1);
            else
                result = MessageBox.Show(message, caption, MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);


            if (result == System.Windows.Forms.DialogResult.No)
                return "No";
            else
                return "Yes";

        }

        public static void message(string message)
        {
            MessageBox.Show(message);
        }
    }
}
