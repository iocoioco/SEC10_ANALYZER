namespace New_Tradegy
{
    partial class Form_매수_매도
    {
        // <summary>
        // Required designer variable.
        // </summary>
        private System.ComponentModel.IContainer components = null;

        // <summary>
        // Clean up any resources being used.
        // </summary>
        // <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        // <summary>
        // Required method for Designer support - do not modify
        // the contents of this method with the code editor.
        // </summary>
        private void InitializeComponent()
        {
            this.richTextBox1 = new System.Windows.Forms.RichTextBox();
            this.취소 = new System.Windows.Forms.Button();
            this.지정 = new System.Windows.Forms.Button();
            this.시장 = new System.Windows.Forms.Button();
            this.저장 = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // richTextBox1
            // 
            this.richTextBox1.Location = new System.Drawing.Point(145, 179);
            this.richTextBox1.Name = "richTextBox1";
            this.richTextBox1.Size = new System.Drawing.Size(915, 703);
            this.richTextBox1.TabIndex = 0;
            this.richTextBox1.Text = "";
            // 
            // 취소
            // 
            this.취소.Location = new System.Drawing.Point(763, 660);
            this.취소.Name = "취소";
            this.취소.Size = new System.Drawing.Size(200, 100);
            this.취소.TabIndex = 1;
            this.취소.Text = "취소";
            this.취소.UseVisualStyleBackColor = true;
            this.취소.Click += new System.EventHandler(this.취소_Click);
            // 
            // 지정
            // 
            this.지정.Location = new System.Drawing.Point(401, 686);
            this.지정.Name = "지정";
            this.지정.Size = new System.Drawing.Size(200, 100);
            this.지정.TabIndex = 2;
            this.지정.Text = "지정";
            this.지정.UseVisualStyleBackColor = true;
            this.지정.Click += new System.EventHandler(this.지정_Click);
            // 
            // 시장
            // 
            this.시장.Location = new System.Drawing.Point(195, 686);
            this.시장.Name = "시장";
            this.시장.Size = new System.Drawing.Size(200, 100);
            this.시장.TabIndex = 3;
            this.시장.Text = "시장";
            this.시장.UseVisualStyleBackColor = true;
            this.시장.Click += new System.EventHandler(this.시장_Click);
            // 
            // 저장
            // 
            this.저장.Location = new System.Drawing.Point(519, 470);
            this.저장.Name = "저장";
            this.저장.Size = new System.Drawing.Size(200, 100);
            this.저장.TabIndex = 4;
            this.저장.Text = "저장";
            this.저장.UseVisualStyleBackColor = true;
            this.저장.Click += new System.EventHandler(this.저장_Click);
            // 
            // Form_매수_매도
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(13F, 24F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1238, 1040);
            this.Controls.Add(this.저장);
            this.Controls.Add(this.시장);
            this.Controls.Add(this.지정);
            this.Controls.Add(this.취소);
            this.Controls.Add(this.richTextBox1);
            this.Name = "Form_매수_매도";
            this.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
            this.Text = "Form_매수_매도";
            this.Load += new System.EventHandler(this.Form_매수_매도_Load);
            this.ResumeLayout(false);

        }

        private System.Windows.Forms.RichTextBox richTextBox1;
        private System.Windows.Forms.Button 취소;
        private System.Windows.Forms.Button 지정;
        private System.Windows.Forms.Button 시장; // Declare the Yes button
        private System.Windows.Forms.Button 저장;
    }
}