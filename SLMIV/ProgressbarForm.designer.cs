namespace SLMIV.Utils
{
    namespace Forms
    {
        partial class ProgressbarForm
        {
            /// <summary>
            /// Required designer variable.
            /// </summary>
            private System.ComponentModel.IContainer components = null;

            /// <summary>
            /// Clean up any resources being used.
            /// </summary>
            /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
            protected override void Dispose(bool disposing)
            {
                if (disposing && (components != null))
                {
                    components.Dispose();
                }
                base.Dispose(disposing);
            }

            #region Windows Form Designer generated code

            /// <summary>
            /// Required method for Designer support - do not modify
            /// the contents of this method with the code editor.
            /// </summary>
            private void InitializeComponent()
            {
                System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ProgressbarForm));
                this.pBar1 = new System.Windows.Forms.ProgressBar();
                this.lbprogress = new System.Windows.Forms.Label();
                this.SuspendLayout();
                // 
                // pBar1
                // 
                this.pBar1.Location = new System.Drawing.Point(77, 46);
                this.pBar1.Name = "pBar1";
                this.pBar1.Size = new System.Drawing.Size(302, 38);
                this.pBar1.TabIndex = 0;
                // 
                // lbprogress
                // 
                this.lbprogress.Font = new System.Drawing.Font("Verdana", 9.75F);
                this.lbprogress.Location = new System.Drawing.Point(22, 3);
                this.lbprogress.Name = "lbprogress";
                this.lbprogress.Size = new System.Drawing.Size(413, 40);
                this.lbprogress.TabIndex = 12;
                // 
                // ProgressbarForm
                // 
                this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
                this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
                this.ClientSize = new System.Drawing.Size(457, 106);
                this.Controls.Add(this.lbprogress);
                this.Controls.Add(this.pBar1);
                this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
                this.Name = "ProgressbarForm";
                this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
                this.Text = "Progress...";
                this.Load += new System.EventHandler(this.ProgressbarForm_Load);
                this.ResumeLayout(false);

            }

            #endregion

            public System.Windows.Forms.ProgressBar pBar1;
            private System.Windows.Forms.Label lbprogress;
        }
    }
}