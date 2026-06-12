using New_Tradegy.Library;
using System;
using System.Drawing;
using System.Windows.Forms;
using New_Tradegy.Library.Deals;
using New_Tradegy.Library.Utils;

namespace New_Tradegy
{
    // 1st  x 809 y 271 w 359
    // 2nd x 809 y 509 w 359
    // 3rd x 809 y 750 w 359 h 405

    // 종목
    // 매수 또는 매도
    
    public partial class Form_매수_매도 : Form
    {
        public static string _str;
        public string _stock { get; private set; }
        public bool _isSell { get; private set; }   // True if Sell, False if Buy
        public int _urgency;
        public int _amount;
        public int _price;

        private Timer _flashTimer; // 🔶 Add this line
        private bool _isFlashing = false;

        //private System.Windows.Forms.Button Yes;

        public Form_매수_매도(bool isSell, string ReceivedStock, int Amount, int Price, int Urgency, string ReceivedString)
        {
            InitializeComponent();
            _isSell = isSell;
            _stock = ReceivedStock;
            _str = ReceivedString;
            _urgency = Urgency;
            _amount = Amount;
            _price = Price;

            if (isSell)
            {
                this.Text = "매도"; // 매수 또는 매도
            }
            else
            {
                this.Text = "매수"; // 매수 또는 매도
            }

           
            int blockStart = 1; //arbitrary numbers to test
            int blockLength = 15;
            richTextBox1.SelectionStart = blockStart;
            richTextBox1.SelectionLength = blockLength;
            richTextBox1.SelectionBackColor = Color.FromArgb(235, 255, 255);

            Form se = (Form)System.Windows.Forms.Application.OpenForms["Form1"];
            if (se == null)
            {
                //Console.WriteLine("Form1 is not open. Form_매수_매도.");
                return;
            }
            DataGridView dgv = FormUtils.FindDataGridViewByName(se, _stock);
            this.Size = new Size(g.screenWidth / 6 + 40, g.screenHeight / 3 - 8);
            this.Location = new Point(dgv.Location.X + g.screenWidth / g.nCol - 25, dgv.Location.Y + 22);

            richTextBox1.Location = new Point(0, 0);
            richTextBox1.Size = this.Size;

            _urgency = Urgency;
        }



        private void Form_매수_매도_Load(object sender, EventArgs e)
        {
            appendMemoRichTextBox(_str, Color.Black, true);

            지정.TabIndex = 0;
            시장.TabIndex = 1;
            저장.TabIndex = 2;
            취소.TabIndex = 3;

            // Adjust Ok and Cancel button positions
            시장.Size = new Size(60, 25);
            지정.Size = new Size(60, 25);
            저장.Size = new Size(60, 25);
            취소.Size = new Size(60, 25);

            시장.Location = new Point(this.Width / 10, (int)(this.Height * 0.750));
            지정.Location = new Point(this.Width / 10 * 3, (int)(this.Height * 0.750));
            저장.Location = new Point(this.Width / 10 * 5, (int)(this.Height * 0.750));
            취소.Location = new Point(this.Width / 10 * 7, (int)(this.Height * 0.750));

            // Bring buttons to front
            //시장.BringToFront();
            //지정.BringToFront();
            //저장.BringToFront();
            //취소.BringToFront();

           
            // Urgency
            HScrollBar hUrgencyScrollBar = new HScrollBar();
            hUrgencyScrollBar.Minimum = 0;
            hUrgencyScrollBar.Maximum = 110;
            hUrgencyScrollBar.Value = (int)(_urgency * 1.1);
            hUrgencyScrollBar.Name = "";
            hUrgencyScrollBar.Text = "";
            hUrgencyScrollBar.Location = new Point(this.Width / 10, (int)(this.Height * 0.6));
            int w = 취소.Location.X + 취소.Width - 시장.Location.X;
            hUrgencyScrollBar.Size = new System.Drawing.Size(w, (int)(지정.Size.Height * 1.0));
            hUrgencyScrollBar.Scroll += HUrgencyscrollBar_Scroll;
            hUrgencyScrollBar.BringToFront();

            richTextBox1.Controls.Add(hUrgencyScrollBar);
            
            this.TopMost = true;


            this.ActiveControl = 지정;
            // this.지정.DialogResult = DialogResult.OK;
            this.AcceptButton = 지정;

            // StartFlashingButton(지정);
        }

        public void UpdateForm(bool isSell, string stockName, int Amount, int price, int Urgency, string str)
        {
            this.Text = isSell ? "매도 ?" : "매수 ?";
            _stock = stockName;
            _price = price;
            _amount = Amount;
            _isSell = isSell;
            _str = str;
            _urgency = Urgency;
  


            UpdateUI();
            //PositionForm();
        }
        private void UpdateUI()
        {
            richTextBox1.Text = _str;
        }
       

        public int The_urgency
        {
            get { return this._urgency; }
        }



        private void HUrgencyscrollBar_Scroll(object sender, ScrollEventArgs e)
        {
            if (e.Type == ScrollEventType.EndScroll)
            {
                _urgency = (int)e.NewValue;
                if (_urgency < 0)
                {
                    _urgency = 10;
                }
                if (_urgency > 100)
                {
                    _urgency = 100;
                }
            }
        }



        // addNewLine : if true add new line at the end, else ...
        public void appendMemoRichTextBox(string text, Color color, bool addNewLine = false)
        {
           
            richTextBox1.SuspendLayout();
            richTextBox1.SelectionColor = color;
            if(color ==  Color.Black)
            {
                richTextBox1.SelectionFont = new Font("Arial", 9, FontStyle.Regular, GraphicsUnit.Point, ((byte)(0)));
            }
            else
            {
                richTextBox1.SelectionFont = new Font("Arial", 15, FontStyle.Regular, GraphicsUnit.Point, ((byte)(0)));
            }
            //richTextBox1.SelectionFont = new Font("Arial", 15, FontStyle.Regular, GraphicsUnit.Point, ((byte)(0)));
           // richTextBox1.Font = new Font("Arial", 15, FontStyle.Regular, GraphicsUnit.Point, ((byte)(0)));
            richTextBox1.AppendText(addNewLine
                ? $"{text}{Environment.NewLine}"
                : text);
            richTextBox1.ScrollToCaret();
            richTextBox1.ResumeLayout();


            //int startIndex = richTextBox1.SelectionStart;
            //richTextBox1.Text = richTextBox1.Text.Insert(richTextBox1.SelectionStart, text);
        }



        





 

        
        private void 시장_Click(object sender, EventArgs e)
        {
            ExecuteDeal("03");
        }

        private void 지정_Click(object sender, EventArgs e)
        {
            ExecuteDeal("01");
        }

        private void 저장_Click(object sender, EventArgs e)
        {
            if (_isSell)
            {
                StockExchange.Instance.AddSellOrder(_stock, _price, _amount, _urgency);
            }
            else
            {
                StockExchange.Instance.AddBuyOrder(_stock, _price, _amount, _urgency);
            }
            this.BeginInvoke(new Action(() =>
            {
                this.Close();  // Ensures form closes properly before reopening
            }));

        }

        private void 취소_Click(object sender, EventArgs e)
        {
            this.BeginInvoke(new Action(() =>
            {
                this.Close();  // Ensures form closes properly before reopening
            }));
        }

        private void StartFlashingButton(Button button)
        {
            _flashTimer = new Timer();
            _flashTimer.Interval = 500; // Toggle every 0.5s
            _flashTimer.Tick += (s, e) =>
            {
                button.BackColor = _isFlashing ? SystemColors.Control : Color.Yellow;
                _isFlashing = !_isFlashing;
            };
            _flashTimer.Start();
        }

        private void StopFlashingButton(Button button)
        {
            _flashTimer?.Stop();
            button.BackColor = SystemColors.Control;
            _isFlashing = false;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                this.Close();
                return true;
            }

            if (keyData == Keys.F5) // or your custom key
            {
                ExecuteDeal("01"); // 지정
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }
        
        private void ExecuteDeal(string orderTypeCode)
        {
            string orderType = _isSell ? "매도" : "매수";
            DealManager.DealExec(orderType, _stock, _price, _amount, orderTypeCode);

            this.BeginInvoke(new Action(() =>
            {
                this.Close(); // clean close
            }));
        }

    }
}
