using System;
using System.Windows.Forms;

namespace New_Tradegy.Library.UI
{
    public partial class AlertForm : Form
    {
        // Alert when wifi trouble 
        public AlertForm()
        {
            // InitializeComponent(); ❌ REMOVE THIS LINE
            this.Text = "⚠️ Network Warning";
            this.Size = new System.Drawing.Size(400, 150);
            this.TopMost = true;

            label = new Label
            {
                AutoSize = true,
                Font = new System.Drawing.Font("Segoe UI", 12),
                Location = new System.Drawing.Point(20, 30),
                Text = "Network unstable..."
            };

            this.Controls.Add(label);
        }

        private Label label;

        public void UpdateText(string msg)
        {
            if (InvokeRequired)
            {
                this.Invoke(new Action(() => label.Text = msg));
            }
            else
            {
                label.Text = msg;
            }
        }
    }
}
