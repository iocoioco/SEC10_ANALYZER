using New_Tradegy.Library;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using New_Tradegy.Library.IO;
using New_Tradegy.Library.Utils;
using New_Tradegy.Library.UI.KeyBindings;
using New_Tradegy.Library.PostProcessing;
namespace New_Tradegy
{
    public partial class Memo : Form
    {
        // AppendText("test\n", Color.Blue, false); last bool addNewLine or not
        // richTextBox1.Select(richTextBox1.Text.Length - 1, 0); // cursor to the end
        // string temp_text = richTextBox1.SelectedText; : slected text read


        // int blockStart = 1; //arbitrary numbers to test
        // int blockLength = 15;
        // richTextBox1.SelectionStart = blockStart;
        // richTextBox1.SelectionLength = blockLength;
        // richTextBox1.SelectionBackColor = Color.Yellow;

        string text;
        string directory = @"C:\BJS\data work\";
        string lastOpendFileName = "";
        int startIndex = 0;

        public Memo()
        {
            InitializeComponent();
        }

        private void Memo_Load(object sender, EventArgs e)
        {
            this.Size = new Size(359, 405);
            this.Location = new Point(809, 509);
            this.TopMost = true;

            string str = "";
            str = "open(^o)" + Environment.NewLine +
            "save(^s)" + Environment.NewLine +
            "find(^f)" + Environment.NewLine +
            "blank(^b)" + Environment.NewLine +
            "selected text -> 관심(^q)" + Environment.NewLine +
            "테마리스트(^t)" + Environment.NewLine +
            "상관리스트(^y)" + Environment.NewLine +
            "테마, 상관 paste and right click";
            richTextBox1.Text = str;
            richTextBox1.Font = new Font("Georgia", 10);
            //this.AcceptButton = Ok;

            richTextBox1.Size = new Size(this.Width, this.Height);
            richTextBox1.Location = new Point(0, 0);
            //richTextBox1.KeyPress += richTextBox1_KeyPress;

            textBox1.Hide();

            //richTextBox1.BringToFront();
        }
        //private void richTextBox1_KeyPress(object sender, KeyPressEventArgs e)
        //{
        //   ky.chart_keypress(e);

        //    SetFocusAndReturn();
        //}

        private void Memo_ResizeEnd(object sender, EventArgs e)
        {
            richTextBox1.Size = new Size((int)this.Width, this.Height);
            richTextBox1.Location = new Point(0, 0);
            richTextBox1.BringToFront();
        }

        private void richTextBox1_KeyDown(object sender, KeyEventArgs e)
        {
            // open(^o)
            // save(^s)
            // find(^f)
            // blank(^b)
            // selected text -> 관심(^q)
            // 테마리스트(^t)
            // 상관리스트(^y)

            // open(^o)
            if (e.Control == true && e.KeyCode == Keys.O)
            {
                text = richTextBox1.Text;
                txtFilesDisplayInDirectory();

            }
            //  save(^s)
            else if (e.Control == true && e.KeyCode == Keys.S)
            {
                text = richTextBox1.Text;
                FileOut.overWrite(directory + lastOpendFileName, text);
            }
            // find(^f)
            else if (e.Control == true && e.KeyCode == Keys.F)
            {
                if (textBox1.Visible == true)
                {
                    textBox1.Visible = false;
                }
                else
                {
                    textBox1.Size = new Size(150, 30);
                    textBox1.Location = new Point(richTextBox1.Location.X +
                                              richTextBox1.Width - textBox1.Width, 2);

                    textBox1.BringToFront();
                    textBox1.Visible = true;
                    textBox1.Focus();
                }
            }
            // blank(^b)
            else if (e.Control == true && e.KeyCode == Keys.B)
            {
                richTextBox1.Text = "";
            }
            // selected text -> 관심(^q)
            else if (e.Control == true && e.KeyCode == Keys.Q)
            {
                string selected_text = richTextBox1.SelectedText;
                string[] words = selected_text.Split(' ', '\t', '\n', ',', '(');
                if (words.Length <= 0)
                {
                    return;
                }
                foreach (var item in words)
                {
                    string stock = item.Replace("_", " ");
                    if (wk.isStock(stock) && g.StockManager.TotalStockList.Contains(stock))
                    {
                        if (!g.StockManager.InterestedOnlyList.Contains(stock))
                            g.StockManager.InterestedOnlyList.Add(stock);
                    }
                }
                //dr.draw_chart();
            }

            // 관심종목 제거
            else if (e.Control == true && e.KeyCode == Keys.W)
            {
                g.StockManager.InterestedOnlyList.Clear();
                
            }

            // 테마 순서 리스트 네이버로부터(^t)
            else if (e.Control == true && e.KeyCode == Keys.T)
            {
                richTextBox1.Text = "";
                string base_url = "https://finance.naver.com/sise/theme.naver";

                for (int i = 1; i <= 8; i++)
                    richTextBox1.Text += Memo_네이버_테마_순위(base_url + "?&page=" + i.ToString());
            }

            // 상관 제목 리스트 보이기(^y)
            else if (e.Control == true && e.KeyCode == Keys.Y)
            {
                richTextBox1.Text = "";
                text = "";

   
                var allGroups = g.GroupManager.GetAll();
                foreach (var item in allGroups)
                {
                    text += "// " + item.Title + "\n";
                }
                richTextBox1.Text = text;
                richTextBox1.Select(0, 0);
            }
            // 호가종목 제거
            else if (e.Control == true && e.KeyCode == Keys.M)
            {
                g.StockManager.InterestedWithBidList.Clear();
                ActionCode.New('B', false, eval: true, draw: 'B').Run();
            }
        }

        private void richTextBox1_MouseDown(object sender, MouseEventArgs e)
        {
            // var words = substring.Split(new string[] { " ", "\r\n" }, StringSplitOptions.None);

            if (e.Button == MouseButtons.Right)
            {
                #region
                int line = -1;
                int p = -1;
                p = richTextBox1.GetCharIndexFromPosition(e.Location);
                line = richTextBox1.GetLineFromCharIndex(p);

                //input = input.Substring(0, input.LastIndexOf("/") + 1); // delete from '/' to the end
                //string word = getWordAtIndex(richTextBox1, richTextBox1.SelectionStart); // single word selection unstable to use
                text = richTextBox1.Text;
                if (text.Length == 0) { return; }
                if (line < 0 || p < 0 || richTextBox1.Lines[line] == null) { return; }

                string temp = getWordAtIndex(richTextBox1, richTextBox1.SelectionStart);
                string wordsInLine = richTextBox1.Lines[line];
                string[] words = wordsInLine.Split(' ');
                // lastOpendFile
                // 상관(그룹, 개별), 테마(테마그룹), 일반(right or paste)
                if (wordsInLine.Length <= 0)
                    return;

                //Collectg words from the string
                //List<string> words = CollectWordsFromString(text);
                #endregion


                //int line = -1;
                //int p = -1;
                //p = richTextBox1.GetCharIndexFromPosition(e.Location);
                //line = richTextBox1.GetLineFromCharIndex(p);

                //text = richTextBox1.Text;
                //if (text.Length == 0) { return; }
                //if (line < 0 || p < 0 || line >= richTextBox1.Lines.Length) { return; }

                //string temp = getWordAtIndex(richTextBox1, richTextBox1.SelectionStart);
                //string wordsInLine = richTextBox1.Lines[line];
                //string[] words = sr.CollectWordsFromString(wordsInLine);
                if (words[0] == "//")
                {
                    var groupManager = g.GroupManager;
                    var allGroups = groupManager.GetAll();
                    for (int i = 0; i < allGroups.Count; i++)
                    {
                        if (allGroups[i].Title.Contains(words[1]))
                        {
                            foreach (string stockName in g.StockManager.InterestedOnlyList)
                            {
                                wk.deleteChartAreaAnnotation(g.ChartManager.Chart1, stockName);
                            }
                            g.StockManager.InterestedOnlyList.Clear();
                            foreach (var stock1 in allGroups[i].Stocks)
                            {
                                if (!g.StockManager.InterestedOnlyList.Contains(stock1) && g.StockManager.TotalStockList.Contains(stock1))
                                {
                                    g.StockManager.InterestedOnlyList.Add(stock1);
                                }
                            }
                            Form se = FormUtils.FindFormByName("Form1");
                            if (se == null)
                            {
                                //Console.WriteLine("Form1 is not open. Memo.");
                                return;
                            }
                            se.Text = words[1];
                            ActionCode.New('B', false, eval: true, draw: 'B').Run();
                        }
                       
                    }

                }
                else if (words[0].Contains(".txt"))
                {
                    richTextBox1.Text = File.ReadAllText(directory + words[0], Encoding.Default);
                    //string myString = File.ReadAllText(directory + words[0], Encoding.Default);
                    //byte[] bytes = Encoding.Default.GetBytes(myString);
                    //myString = Encoding.UTF8.GetString(bytes); 


                    lastOpendFileName = wordsInLine;
                    richTextBox1.Select(0, 0);
                    this.Text = words[0];
                }
                else if (wordsInLine.Contains("%"))
                {
                    words = wordsInLine.Split('\t');
                    List<string> tsl = new List<string>();
                    List<string> tsl_그룹_상관 = new List<string>();
                    List<string> GL_title = new List<string>();
                    List<List<string>> GL = new List<List<string>>();
                    FileIn.read_그룹_네이버_테마(tsl, tsl_그룹_상관, GL_title, GL);

                    for (int i = 0; i < GL_title.Count; i++)
                    {
                        if (GL_title[i] == words[0])
                        {
                            foreach (string stockName in g.StockManager.InterestedOnlyList)
                            {
                                wk.deleteChartAreaAnnotation(g.ChartManager.Chart1, stockName);
                            }
                            g.StockManager.InterestedOnlyList.Clear();
                            foreach (var stock2 in GL[i])
                            {
                                if (!g.StockManager.InterestedOnlyList.Contains(stock2) &&       g.StockManager.TotalStockList.Contains(stock2))
                                    g.StockManager.InterestedOnlyList.Add(stock2);
                            }
                            this.Text = GL_title[i];
                            PostProcessor.ManageChart1Invoke(); // Memo, 관심종목 제거 및 추가
                            return;
                        }
                    }
                    richTextBox1.Select(0, 0);
                }
                else
                {
                    string word = getWordAtIndex(richTextBox1, richTextBox1.SelectionStart);
                    if (word.Contains(',')) word = word.Split(',')[0];
                    if (word.Contains('(')) word = word.Split('(')[0];
                    word.Trim(new Char[] { ' ', '*', '.', '\t', '\n' });
                    word = word.Replace("_", " ");
                    if (wk.isStock(word))
                    {
                        if (!g.StockManager.InterestedOnlyList.Contains(word))
                            g.StockManager.InterestedOnlyList.Add(word);
                    }
                }
                ActionCode.New('B', false, eval: true, draw: 'B').Run();
            }
        }

        private void textBox1_KeyDown(object sender, KeyEventArgs e)
        {
            // Detect if Ctrl + V is pressed
            if (e.Control && e.KeyCode == Keys.V)
            {
                // Get the text from the clipboard
                if (Clipboard.ContainsText())
                {
                    string pastedText = Clipboard.GetText();

                    // Call method to collect words
                    string[] words = StringUtils.CollectWordsFromString(pastedText);

                    // Do something with the words (e.g., display or process them)
                    foreach (string word in words)
                    {
                        //Console.WriteLine(word); // Example output
                    }

                    e.Handled = true; // Optional: prevent the default paste operation if needed
                }
            }

            if (e.KeyCode == Keys.Enter)
            {
                int returned_startIndex = FindMyText(textBox1.Text, startIndex);
                if (returned_startIndex == -1) { return; } // not the string in the opened file

                int line = richTextBox1.GetLineFromCharIndex(returned_startIndex);

                ScrollToLine(line - 3); // 3
                startIndex = returned_startIndex + textBox1.Text.Length;

                // no sound
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }
        void ScrollToLine(int lineNumber)
        {
            if (lineNumber > richTextBox1.Lines.Count()) return;
            if (lineNumber < 0) lineNumber = 0;

            int index = richTextBox1.GetFirstCharIndexFromLine(lineNumber);
            richTextBox1.Select(index, 0);
            richTextBox1.ScrollToCaret();
        }

        public int FindMyText(string text, int start)
        {
            // Initialize the return value to false by default.
            int returnValue = -1;

            // Ensure that a search string has been specified and a valid start point.
            if (text.Length > 0 && start >= 0)
            {
                // Obtain the location of the search string in richTextBox1.
                returnValue = richTextBox1.Find(text, start, RichTextBoxFinds.MatchCase);
                if (returnValue < 0) // not found, start from beginning
                    returnValue = richTextBox1.Find(text, 0, RichTextBoxFinds.MatchCase);
            }
            return returnValue;
        }

        public void appendMemoRichTextBox(string text)
        {
            // after adding text the position move to the top
            bool addNewLine = false;
            richTextBox1.SuspendLayout();
            //richTextBox1.SelectionColor = color;
            richTextBox1.AppendText(addNewLine
                ? $"{text}{Environment.NewLine}"
                : text);
            richTextBox1.ScrollToCaret();
            richTextBox1.ResumeLayout();
        }

        // do not delete, will be used when writing
        public void appendMemoRichTextBox(string text, Color color, bool addNewLine = false)
        {
            richTextBox1.SuspendLayout();
            richTextBox1.SelectionColor = color;
            richTextBox1.AppendText(addNewLine
                ? $"{text}{Environment.NewLine}"
                : text);
            richTextBox1.ScrollToCaret();
            richTextBox1.ResumeLayout();
        }

        private string getWordAtIndex(RichTextBox RTB, int index)
        {
            string wordSeparators = " .,;-!?\r\n\"";
            int cp0 = index;
            int cp2 = RTB.Find(wordSeparators.ToCharArray(), index);
            for (int c = index; c > 0; c--)
            {
                if (c >= RTB.Text.Length)
                    return "";
                if (wordSeparators.Contains(RTB.Text[c]))
                {
                    cp0 = c + 1;
                    break;
                }
            }
            int l = cp2 - cp0;
            if (l > 0) return RTB.Text.Substring(cp0, l); else return "";
        }

        // find word and coloring 
        public static int HighlightText(RichTextBox myRtb, ref int startIndex, string word, Color color)
        {
            if (word == string.Empty)
                return -1;

            //int s_start = myRtb.SelectionStart, startIndex = 0, index;
            int foundIndex = -1;
            foundIndex = myRtb.Text.IndexOf(word, startIndex);

            startIndex = foundIndex + word.Length;

            return foundIndex;
        }

        public void insertMemoRichTextBox(string text)
        {
            // after adding text the position move to the top
            startIndex = richTextBox1.SelectionStart;
            richTextBox1.Text = richTextBox1.Text.Insert(richTextBox1.SelectionStart, text);
            richTextBox1.SelectionStart = startIndex + text.Length;
        }
        
        private void txtFilesDisplayInDirectory()
        {
            List<string> txtFiles = FileIn.txtFilesInGivenDirectory(directory);

            text = richTextBox1.Text;
            richTextBox1.Text = "";
            foreach (var file in txtFiles)
            {
                if (file.Length > 6) continue;
                if (file == "통계.txt" || file == "누적.txt" || file == "변수.txt") continue;
                FileInfo FileVol = new FileInfo(directory + file);
                int SizeinKB = (int)(FileVol).Length / 1024;
                if (SizeinKB < 500)
                    appendMemoRichTextBox(file + "\n");
            }
        }
        
        public string Memo_네이버_테마_순위(string url)
        {
            string 테마별시세 = "";


            HtmlAgilityPack.HtmlWeb web = new HtmlAgilityPack.HtmlWeb();

            HtmlAgilityPack.HtmlDocument doc = new HtmlAgilityPack.HtmlDocument();

            doc = web.Load(url);

            // ".//table[contains@class, 'type2')]")
            //table [@class='tb_type1 tb_type1_b']"

            // the same name tables : find all
            //            tables = soup.find_all('table', class_ = 'table table-striped table-bordered table-hover table-condensed table-list')
            //hisoric_population = tables[0]
            //forecast_population = tables[1]


            List<List<string>> table =
                doc.DocumentNode.SelectSingleNode(".//table[@class='type_1 theme']")
                .Descendants("tr")
                .Skip(1)
                .Where(tr => tr.Elements("td").Count() > 1)
                .Select(tr => tr.Elements("td").Select(td => td.InnerText.Trim()).ToList())
                .ToList();

            string sectorName = "";
            string increasePercentage = "";

            for (int i = 0; i < table.Count; i++)
            {
                sectorName = Encoding.GetEncoding("EUC-KR").GetString(Encoding.GetEncoding("EUC-KR").GetBytes(table[i][0]));
                increasePercentage = Encoding.GetEncoding("EUC-KR").GetString(Encoding.GetEncoding("EUC-KR").GetBytes(table[i][1]));
                테마별시세 += sectorName + "\t" + increasePercentage + "\n";
            }
            return 테마별시세;
        }




        // Import the necessary functions from user32.dll
        [DllImport("user32.dll")]
        private static extern IntPtr SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool SetFocus(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);


        private void SetFocusAndReturn()
        {
            IntPtr handle = this.Handle;

            // Delay for a short time to allow the browser to open
            System.Threading.Thread.Sleep(100);

            // Check if the form is minimized
            if (IsIconic(handle))
            {
                // Restore the window if it's minimized
                ShowWindow(handle, SW_RESTORE);
            }

            // Set the focus back to the form
            SetForegroundWindow(handle);
            SetFocus(handle);
            // Keep focus on the TextBox
            richTextBox1.Focus();
        }
        // Additional necessary API calls
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        private const int SW_RESTORE = 9;
    }
}
