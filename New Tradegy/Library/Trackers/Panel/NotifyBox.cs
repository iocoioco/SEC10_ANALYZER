using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace New_Tradegy.Library.UI
{


    public class NotifyBox : RichTextBox
    {
        public NotifyBox()
        {
            this.Multiline = true;
            this.ReadOnly = true;
            this.WordWrap = true;
            this.ScrollBars = RichTextBoxScrollBars.Vertical;
            this.Dock = DockStyle.Fill;
            this.Font = new Font("Segoe UI", 9);
            this.BackColor = Color.White;
            this.BorderStyle = BorderStyle.None;

            this.KeyDown += OnKeyDown;
            this.MouseDown += OnMouseDown;
        }

        public void HandleKey(char key)
        {
            //switch (char.ToLower(key))
            //{
            //    case 'n':
            //        AppendTextWithStyle("[News] Market opens strong", Color.DarkBlue, FontStyle.Bold);
            //        break;

            //    case 'a':
            //        AppendTextWithStyle("[Alert] Large volume detected", Color.Red, FontStyle.Bold);
            //        break;

            //    case 's':
            //        AppendSuccess("Trade executed: +3.2%");
            //        break;

            //    case 'e':
            //        AppendError("Order failed: insufficient balance");
            //        break;

            //    case 'c':
            //        Clear();
            //        break;

            //    default:
            //        AppendTextWithStyle($"[Unknown key '{key}']", Color.Gray, FontStyle.Italic);
            //        break;
            //}
        }


        // 🔑 Key handling
        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                Clear();
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.C)
            {
                Copy();
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.A)
            {
                SelectAll();
                e.Handled = true;
            }
        }

        // 🖱 Mouse handling
        private void OnMouseDown(object sender, MouseEventArgs e)
        {
            string msg = "Mouse: ";
            if (e.Button == MouseButtons.Left) msg += "Left";
            if (e.Button == MouseButtons.Right) msg += "Right";
            if (ModifierKeys.HasFlag(Keys.Control)) msg += " + Ctrl";
            if (ModifierKeys.HasFlag(Keys.Shift)) msg += " + Shift";

            AppendTextWithStyle($"[{DateTime.Now:HH:mm:ss}] {msg} click", Color.Gray, FontStyle.Italic);
        }

        // ✍ Append styled text
        public void AppendTextWithStyle(string text, Color color, FontStyle style = FontStyle.Regular, bool addNewLine = true)
        {
            this.SelectionStart = this.TextLength;
            this.SelectionLength = 0;

            this.SelectionColor = color;
            this.SelectionFont = new Font(this.Font, style);
            this.AppendText(addNewLine ? text + Environment.NewLine : text);

            this.SelectionColor = this.ForeColor;
            this.SelectionFont = this.Font;

            this.ScrollToCaret();
        }

        // 🟨 Highlight background
        public void AppendHighlighted(string text, Color foreColor, Color backColor, bool addNewLine = true)
        {
            this.SelectionStart = this.TextLength;
            this.SelectionLength = 0;

            this.SelectionColor = foreColor;
            this.SelectionBackColor = backColor;
            this.AppendText(addNewLine ? text + Environment.NewLine : text);

            this.SelectionColor = this.ForeColor;
            this.SelectionBackColor = this.BackColor;

            this.ScrollToCaret();
        }

        // 🔁 Prepend instead of append
        public void PrependText(string text, bool addNewLine = true)
        {
            this.Text = (addNewLine ? text + Environment.NewLine : text) + this.Text;
        }

        // 📁 Save contents to file
        public void SaveToFile(string path)
        {
            File.WriteAllText(path, this.Text);
        }

        // 📂 Load contents from file
        public void LoadFromFile(string path)
        {
            if (File.Exists(path))
                this.Text = File.ReadAllText(path);
        }

        // 🔍 Search keyword and select
        public bool FindAndSelect(string keyword)
        {
            int index = this.Text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                this.SelectionStart = index;
                this.SelectionLength = keyword.Length;
                this.ScrollToCaret();
                return true;
            }
            return false;
        }

        // 🧹 Clear all with optional confirmation
        public void ClearWithConfirm()
        {
            if (MessageBox.Show("Clear all messages?", "Confirm", MessageBoxButtons.YesNo) == DialogResult.Yes)
                this.Clear();
        }

        // 🔝 Scroll to top
        public void ScrollToTop()
        {
            this.SelectionStart = 0;
            this.ScrollToCaret();
        }

        // ⏱ Add timestamped message
        public void AppendTimestamped(string text, Color? color = null)
        {
            string msg = $"[{DateTime.Now:HH:mm:ss}] {text}";
            AppendTextWithStyle(msg, color ?? Color.Black);
        }

        // 🚫 Append error (red)
        public void AppendError(string error)
        {
            AppendTextWithStyle($"[ERROR] {error}", Color.Red, FontStyle.Bold);
        }

        // ✅ Append success (green)
        public void AppendSuccess(string message)
        {
            AppendTextWithStyle($"[OK] {message}", Color.Green);
        }
    }
}
