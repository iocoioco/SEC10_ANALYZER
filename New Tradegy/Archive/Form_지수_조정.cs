using New_Tradegy.Library;
using New_Tradegy.Library.Trackers;
using System;
using System.Windows.Forms;

namespace New_Tradegy
{
    public partial class Form_지수_조정 : Form
    {
        double[] t = new double[6];
        // 지수내의 지수내합(3), 기관(4), 외인(5), 개인(6), 나스닥지수(10), 연기(11)
        // "기관", "외인", "개인", "연기" 를 하나의 변수로 처리
        // "지수"
        // "나스닥" 
        // "가격"
        string _stock = "";
        string[] s = new string[4] { "가격", "지수", "기타", "나스닥" };
        public Form_지수_조정(string kospiorKosdaq)
        {
            InitializeComponent();

            _stock = kospiorKosdaq;
        }

        private void Form_지수_조정_Load(object sender, EventArgs e)
        {
            this.Text = "Form_지수_조정" + " (" + _stock + ")";


            int w = g.ChartManager.Chart1.Bounds.Width / 4;
            int h = g.ChartManager.Chart1.Bounds.Height / 4;

            int id = 0;
            for (int i = 0; i < g.StockManager.IndexList.Count; i++)
            {
                if (g.clickedStock == g.StockManager.IndexList[i])
                {
                    id = i;
                    break;
                }
            }

            for (int i = 0; i < 4; i++)
            {
                System.Windows.Forms.TextBox t = new System.Windows.Forms.TextBox();
                t.Text = s[i];
                t.Location = new System.Drawing.Point(0, 10 + i * h / 5);
                t.Size = new System.Drawing.Size(40, h / 10);
                t.Enabled = false;
                this.Controls.Add(t);

                HScrollBar hScrollBar = new HScrollBar();
                hScrollBar.Minimum = 10;
                hScrollBar.Maximum = 200;
                hScrollBar.Value = 100;

                hScrollBar.Name = "Horizontal";
                hScrollBar.Text = "Horizontal" + " " + i.ToString();
                hScrollBar.Location = new System.Drawing.Point(50, i * h / 5);
                hScrollBar.Size = new System.Drawing.Size(w - 80, h / 10);
                hScrollBar.Scroll += HScrollBar_Scroll;
                //int a = hScrollBar.InnerScrollBar.Width;
                this.Controls.Add(hScrollBar);
            }
            this.TopMost = true;
        }

        private void HScrollBar_Scroll(object sender, ScrollEventArgs e)
        {
            if (e.Type == ScrollEventType.EndScroll)
            {
                double value = e.NewValue;

                HScrollBar c = (HScrollBar)sender;
                string Name = c.Text;

                int id = 0;
                for (int i = 0; i < g.StockManager.IndexList.Count; i++)
                {
                    if (g.clickedStock == g.StockManager.IndexList[i])
                    {
                        id = i;
                        break;
                    }
                }

                int jd = 0;
                for (int j = 0; j < 4; j++)
                {
                    if (Name.Contains(j.ToString()))
                    {
                        jd = j;
                        break;
                    }
                }
                g.KodexMagnifier[id, jd] *= (value / 100.0);

                var data = g.StockRepository.TryGetDataOrNull(g.clickedStock);

                if (data != null && data.Api != null && data.Api.nrow > 0)
                {
                    ChartIndex.UpdateSeries(g.ChartManager.Chart1, data);
                    ChartIndex.UpdateSeries(g.ChartManager.Chart2, data);
                }
            }
        }

 


        //SQUID size_location() 참조할 것
        //지수 지수 = (지수)Application.OpenForms["지수"];
        //Form_KODEX_.Location = new Point(0, 0);
        //Form_KODEX_.Size = new Size(1920 / 2, height);

        //this.components = new System.ComponentModel.Container();
        //this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        //this.ClientSize = new System.Drawing.Size(1300, 450);
    }
}
