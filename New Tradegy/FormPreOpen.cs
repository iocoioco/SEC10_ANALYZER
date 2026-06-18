using New_Tradegy.Library;
using New_Tradegy.Library.Core;
using New_Tradegy.Library.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace New_Tradegy
{
    public partial class FormPreOpen : Form
    {
        private DataGridView dgvUp;
        private DataGridView dgvDown;
        private DataGridView dgvMoney;


        private readonly BindingList<PreOpenCandidate> _upList =
    new BindingList<PreOpenCandidate>();

        private readonly BindingList<PreOpenCandidate> _downList =
            new BindingList<PreOpenCandidate>();

        private readonly BindingList<PreOpenCandidate> _moneyList =
            new BindingList<PreOpenCandidate>();

        private StockRepository _stockRepo;

        public FormPreOpen()
        {
            InitializeComponent();
            _stockRepo = g.StockRepo;

            this.Text = "PreOpen Radar";
            this.StartPosition = FormStartPosition.CenterScreen;

            this.Width = 1500;
            this.Height = 700;

            BuildGrids();
        }



        private void BuildGrids()
        {
            dgvUp = CreateGrid("상승");
            dgvDown = CreateGrid("하락");
            dgvMoney = CreateGrid("호가");

            int margin = 10;
            int gap = 10;
            int w = (this.ClientSize.Width - margin * 2 - gap * 2) / 3;
            int h = this.ClientSize.Height - margin * 2;

            dgvUp.SetBounds(margin, margin, w, h);
            dgvDown.SetBounds(margin + w + gap, margin, w, h);
            dgvMoney.SetBounds(margin + (w + gap) * 2, margin, w, h);

            this.Controls.Add(dgvUp);
            this.Controls.Add(dgvDown);
            this.Controls.Add(dgvMoney);


            dgvUp.AutoGenerateColumns = true;
            dgvDown.AutoGenerateColumns = true;
            dgvMoney.AutoGenerateColumns = true;

            dgvUp.DataSource = _upList;
            dgvDown.DataSource = _downList;
            dgvMoney.DataSource = _moneyList;

            SetupGridColumns(dgvUp);
            SetupGridColumns(dgvDown);
            SetupGridColumns(dgvMoney);

            dgvUp.CellClick += dgv_CellClick;
            dgvDown.CellClick += dgv_CellClick;
            dgvMoney.CellClick += dgv_CellClick;
        }

        private void SetupGridColumns(DataGridView dgv)
        {
            dgv.Columns["Code"].Visible = false;
            dgv.Columns["ExpectedRate100"].Visible = false;
            dgv.Columns["ExpectedVsBidAsk100"].Visible = false;
            dgv.Columns["ExpectedMoney"].Visible = false;
            dgv.Columns["AskMoney1"].Visible = false;
            dgv.Columns["BidMoney1"].Visible = false;

            dgv.Columns["Name"].HeaderText = "종목명";
            dgv.Columns["등락"].HeaderText = "등락(%)";
            dgv.Columns["호가대비"].HeaderText = "호가대비(%)";
            dgv.Columns["예상체결액"].HeaderText = "예상체결액(억)";
            dgv.Columns["매도1호가액"].HeaderText = "매도1호가액(억)";
            dgv.Columns["매수1호가액"].HeaderText = "매수1호가액(억)";

            dgv.Columns["등락"].DefaultCellStyle.Format = "N2";
            dgv.Columns["호가대비"].DefaultCellStyle.Format = "N2";
            dgv.Columns["예상체결액"].DefaultCellStyle.Format = "N1";
            dgv.Columns["매도1호가액"].DefaultCellStyle.Format = "N1";
            dgv.Columns["매수1호가액"].DefaultCellStyle.Format = "N1";
        }

        private void dgv_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
                return;

            var dgv = sender as DataGridView;
            if (dgv == null)
                return;

            var item = dgv.Rows[e.RowIndex].DataBoundItem as PreOpenCandidate;
            if (item == null)
                return;

            ToggleInterested(item.Name);
        }

        private void ToggleInterested(string stock)
        {
            if (string.IsNullOrWhiteSpace(stock))
                return;

            var list = g.StockManager.InterestedWithBidList;

            if (list.Contains(stock))
                list.Remove(stock);
            else
                list.Add(stock);

            RefreshPreOpenGridColors();
        }

        private DataGridView CreateGrid(string title)
        {
            var dgv = new DataGridView();

            dgv.Dock = DockStyle.None;
            dgv.ReadOnly = true;
            dgv.AllowUserToAddRows = false;
            dgv.AllowUserToDeleteRows = false;
            dgv.RowHeadersVisible = false;
            dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgv.MultiSelect = false;
            dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            return dgv;
        }


        public void UpdatePreOpenGrids()
        {
            var all = new List<PreOpenCandidate>();

            foreach (var sd in _stockRepo.Stocks())// 여기 이름은 네 Repo 구조에 맞게 수정
            {
                if (sd == null || sd.PreOpen == null)
                    continue;

                var last = sd.PreOpen.Records.Count > 0
                    ? sd.PreOpen.Records[sd.PreOpen.Records.Count - 1]
                    : null;

                if (last == null)
                    continue;

                long expectedMoney = (long)last.ExpectedPrice * last.ExpectedVolume;

                long askMoney1 = (long)last.AskPrice1 * last.AskQty1;
                long bidMoney1 = (long)last.BidPrice1 * last.BidQty1;

                int midPrice = (last.AskPrice1 + last.BidPrice1) / 2;

                int expectedVsBidAsk100 = 0;

                if (midPrice > 0)
                {
                    expectedVsBidAsk100 =
                        (last.ExpectedPrice - midPrice) * 10000 / midPrice;
                }

                all.Add(new PreOpenCandidate
                {
                    Code = sd.Code,
                    Name = sd.Stock,

                    ExpectedRate100 = last.ExpectedRate100,
                    ExpectedVsBidAsk100 = expectedVsBidAsk100,

                    ExpectedMoney = expectedMoney,

                    AskMoney1 = askMoney1,
                    BidMoney1 = bidMoney1
                });
            }

            var up = all
                .OrderByDescending(x => x.ExpectedRate100)
                .Take(30)
                .ToList();

            var down = all
                .OrderBy(x => x.ExpectedRate100)
                .Take(30)
                .ToList();

            var money = all
                .OrderByDescending(x => x.ExpectedMoney)
                .Take(30)
                .ToList();

            ReplaceList(_upList, up);
            ReplaceList(_downList, down);
            ReplaceList(_moneyList, money);

            RefreshPreOpenGridColors();
        }


        private static void ReplaceList(
    BindingList<PreOpenCandidate> target,
    List<PreOpenCandidate> source)
        {
            target.Clear();

            foreach (var item in source)
                target.Add(item);
        }

        private void RefreshPreOpenGridColors()
        {
            PaintGrid(dgvUp);
            PaintGrid(dgvDown);
            PaintGrid(dgvMoney);
        }

        private void PaintGrid(DataGridView dgv)
        {
            foreach (DataGridViewRow row in dgv.Rows)
            {
                var item = row.DataBoundItem as PreOpenCandidate;
                if (item == null)
                    continue;

                bool selected =
                    g.StockManager.InterestedWithBidList.Contains(item.Code);

                row.DefaultCellStyle.BackColor =
                    selected ? Color.Gold : Color.White;
            }
        }

        public void SafeUpdatePreOpenGrids()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(SafeUpdatePreOpenGrids));
                return;
            }

            UpdatePreOpenGrids();
        }
    }
}
