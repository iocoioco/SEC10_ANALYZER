using New_Tradegy.Library;
using New_Tradegy.Library.Trackers;
using New_Tradegy.Library.UI;
using New_Tradegy.Library.Utils;
using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

namespace New_Tradegy
{
    public partial class FormMonitor : Form
    {
        public bool BuyMode { get; set; }
        public bool SellMode { get; set; }
        public int AmountForDeal { get; set; }
        public int BuyInterval { get; set; }
        public double AggressiveFactor { get; set; }

        public TrackBar trackAmount;
        public TrackBar trackInterval;
        public TrackBar trackAggressive;

        public FormMonitor()
        {
            InitializeComponent();
            SetupLayout();
        }

       

        private void SetupLayout()
        {
            //groupPane(230px)

            //tradePane(230px)

            //towerPanel(200px)

            //NotifyBox(나머지 공간 채움)

            var workingArea = Screen.PrimaryScreen.WorkingArea;
            int width = workingArea.Width * 1 / 2;
            //width = 600;
            int height = workingArea.Height; // was * 0.365);
       
            int x = workingArea.Width * 5 / 10;
            int y = 0;

            this.StartPosition = FormStartPosition.Manual;
            this.Bounds = new Rectangle(x, y, width, height);
            this.Text = "Monitor Panel";


            // === Root Layout ===
            var rootLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
            };
            int topPanelHeight = (int)(workingArea.Height * 0.365);
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, topPanelHeight));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));




            // === Define topPanel ===
            var topPanel = new TableLayoutPanel


            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Font = new Font("Arial", 9, FontStyle.Bold),
            };

            // === Set Column Styles ===
            topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200)); // controlPane (left sliders)
            topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230)); // tradePane (narrow grid)
            topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230)); // groupPane (narrow grid)
            topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));  // multiTextBox (take all remaining)

            // === Add topPanel to the Form ===
            this.Controls.Add(topPanel); // or wherever you're anchoring this layout

            // === towerPanel using TableLayoutPanel ===
            var towerPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                ColumnCount = 1,
                RowCount = 0,
                BackColor = Color.White,
            };
            towerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));









            // === Buy Mode Row ===
            // === Buy Mode Row ===
            var buyMode = new CheckBox
            {
                Text = "수동",
                Appearance = Appearance.Button,
                Checked = true, // default to manual
                AutoSize = false,
                Width = 60,
                Height = 25,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Margin = new Padding(0)
            };

            var buyRow = new TableLayoutPanel
            {
                ColumnCount = 2,
                RowCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill,
                Margin = new Padding(10, 5, 10, 0)
            };

            buyRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 50)); // label width
            buyRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var lblBuy = new Label
            {
                Text = "매수",
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill,
                AutoSize = false,
                Margin = new Padding(0)
            };

            buyRow.Controls.Add(lblBuy, 0, 0);
            buyRow.Controls.Add(buyMode, 1, 0);

            towerPanel.RowCount++;
            towerPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            towerPanel.Controls.Add(buyRow, 0, towerPanel.RowCount - 1);

            // === Sell Mode Row ===
            var sellMode = new CheckBox
            {
                Text = "자동",
                Appearance = Appearance.Button,
                Checked = true, // default to auto
                AutoSize = false,
                Width = 60,
                Height = 25,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Margin = new Padding(0)
            };

            var sellRow = new TableLayoutPanel
            {
                ColumnCount = 2,
                RowCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill,
                Margin = new Padding(10, 5, 10, 0)
            };

            sellRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 50)); // label width
            sellRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var lblSell = new Label
            {
                Text = "매도",
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill,
                AutoSize = false,
                Margin = new Padding(0)
            };






            buyMode.CheckedChanged += (s, e) =>
            {
                buyMode.Text = buyMode.Checked ? "수동" : "자동";
                g.confirm_buy = buyMode.Checked; // Update global setting

                if (g.confirm_buy)
                    SoundUtils.Sound("Keys", "confirm buy");
                else
                    SoundUtils.Sound("Keys", "no confirm buy");

            };

            sellMode.CheckedChanged += (s, e) =>
            {
                sellMode.Text = sellMode.Checked ? "자동" : "수동";
                g.confirm_sell = !sellMode.Checked; // Update global setting

                if (g.confirm_sell)
                    SoundUtils.Sound("Keys", "confirm sell");
                else
                    SoundUtils.Sound("Keys", "no confirm sell");
            };


            sellRow.Controls.Add(lblSell, 0, 0);
            sellRow.Controls.Add(sellMode, 1, 0);

            towerPanel.RowCount++;
            towerPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            towerPanel.Controls.Add(sellRow, 0, towerPanel.RowCount - 1);



            var trackInterval = new TrackBar
            {
                Minimum = 1,
                Maximum = 10,
                TickFrequency = 1,
                Value = 5,
                Dock = DockStyle.Fill
            };

            var lblInterval = new Label
            {
                Text = $"간격(초): {trackInterval.Value}",
                TextAlign = ContentAlignment.BottomLeft
            };

            trackInterval.Scroll += (s, e) =>
            {
                BuyInterval = trackInterval.Value;
                lblInterval.Text = $"간격(초): {BuyInterval}";
                //Console.WriteLine($"[DEBUG] Interval: {BuyInterval}");
            };

            var trackAggressive = new TrackBar
            {
                Minimum = 70,
                Maximum = 130,
                TickFrequency = 5,
                Value = 100,
                Dock = DockStyle.Fill
            };

            var lblAggressive = new Label
            {
                Text = $"공격성 x{trackAggressive.Value / 100.0:0.00}",
                TextAlign = ContentAlignment.BottomLeft
            };

            trackAggressive.Scroll += (s, e) =>
            {
                AggressiveFactor = trackAggressive.Value / 100.0;
                lblAggressive.Text = $"공격성 x{AggressiveFactor:0.00}";
                //Console.WriteLine($"[DEBUG] Aggressive Factor: {AggressiveFactor:0.00}");
            };

            // === SliderBlock helper ===

            TableLayoutPanel MakeSliderBlock(Label label, TrackBar trackBar)
            {
                var panel = new TableLayoutPanel
                {
                    ColumnCount = 1,
                    RowCount = 2,
                    Dock = DockStyle.Fill,
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    Margin = new Padding(10, 5, 10, 0) // was 10, 5, 10, 0
                };
                panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));

                label.Dock = DockStyle.Fill;
                trackBar.Dock = DockStyle.Fill;

                panel.Controls.Add(label, 0, 0);
                panel.Controls.Add(trackBar, 0, 1);
                return panel;
            }

            // === Add slider blocks ===

            //var amountBlock = MakeSliderBlock(lblAmount, trackAmount);
            var intervalBlock = MakeSliderBlock(lblInterval, trackInterval);
            var aggressiveBlock = MakeSliderBlock(lblAggressive, trackAggressive);

            foreach (var block in new[] {  intervalBlock, aggressiveBlock }) // amountBlock,
            {
                towerPanel.RowCount++;
                towerPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                towerPanel.Controls.Add(block, 0, towerPanel.RowCount - 1);
            }

            // === Add ControlPane (3-row DataGridView) ===

            towerPanel.RowCount++;
            towerPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 90)); // ~94 height for 3 rows
            towerPanel.Controls.Add(g.controlPane.View, 0, towerPanel.RowCount - 1);

            topPanel.Controls.Add(towerPanel, 2, 0);
            topPanel.Controls.Add(g.tradePane.View, 1, 0); // column 1, row 0
            topPanel.Controls.Add(g.groupPane.View, 0, 0); // column 1, row 0

            var notifyBox = new NotifyBox();
            g.NotifyBox = notifyBox; // assign globally

            // Optional: limit column span or width if needed
     
            notifyBox.BackColor = Color.Beige;         // Optional visual cue

            topPanel.Controls.Add(notifyBox, column: 3, row: 0);

            // -------------------------------
            // Bottom Panel (KOSPI | KOSDAQ)
            // -------------------------------
            var bottomPanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 1, ColumnCount = 2 };
            bottomPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            bottomPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            var dgvKospi = new DataGridView { Dock = DockStyle.Fill, RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
            dgvKospi.Columns.Add("Stock", "종목");
            dgvKospi.Columns.Add("Score", "점수");

            var dgvKosdaq = new DataGridView { Dock = DockStyle.Fill, RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
            dgvKosdaq.Columns.Add("Stock", "종목");
            dgvKosdaq.Columns.Add("Score", "점수");

            bottomPanel.Controls.Add(dgvKospi, 0, 0);
            bottomPanel.Controls.Add(dgvKosdaq, 1, 0);

            // Final Composition
            rootLayout.Controls.Add(topPanel, 0, 0);
            rootLayout.Controls.Add(bottomPanel, 0, 1);
            this.Controls.Add(rootLayout);
        }
    }
}
