namespace SLMIV.Utils.Forms
{
    partial class UrlForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(UrlForm));
            this.btn_ok = new System.Windows.Forms.Button();
            this.btn_cancel = new System.Windows.Forms.Button();
            this.txbImageUrl = new System.Windows.Forms.TextBox();
            this.btnView = new System.Windows.Forms.Button();
            this.lbLocalfile = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // btn_ok
            // 
            this.btn_ok.Location = new System.Drawing.Point(162, 110);
            this.btn_ok.Name = "btn_ok";
            this.btn_ok.Size = new System.Drawing.Size(75, 23);
            this.btn_ok.TabIndex = 0;
            this.btn_ok.Text = "OK";
            this.btn_ok.UseVisualStyleBackColor = true;
            this.btn_ok.Click += new System.EventHandler(this.btn_ok_Click);
            // 
            // btn_cancel
            // 
            this.btn_cancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btn_cancel.Location = new System.Drawing.Point(243, 110);
            this.btn_cancel.Name = "btn_cancel";
            this.btn_cancel.Size = new System.Drawing.Size(75, 23);
            this.btn_cancel.TabIndex = 1;
            this.btn_cancel.Text = "Cancel";
            this.btn_cancel.UseVisualStyleBackColor = true;
            this.btn_cancel.Click += new System.EventHandler(this.btn_cancel_Click);
            // 
            // txbImageUrl
            // 
            this.txbImageUrl.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txbImageUrl.Location = new System.Drawing.Point(13, 13);
            this.txbImageUrl.Name = "txbImageUrl";
            this.txbImageUrl.Size = new System.Drawing.Size(411, 26);
            this.txbImageUrl.TabIndex = 2;
            this.txbImageUrl.Text = "http://";
            // 
            // btnView
            // 
            this.btnView.Location = new System.Drawing.Point(425, 16);
            this.btnView.Name = "btnView";
            this.btnView.Size = new System.Drawing.Size(46, 23);
            this.btnView.TabIndex = 3;
            this.btnView.Text = "View...";
            this.btnView.UseVisualStyleBackColor = true;
            this.btnView.Click += new System.EventHandler(this.btnView_Click);
            // 
            // lbLocalfile
            // 
            this.lbLocalfile.AutoSize = true;
            this.lbLocalfile.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lbLocalfile.Location = new System.Drawing.Point(21, 51);
            this.lbLocalfile.Name = "lbLocalfile";
            this.lbLocalfile.Size = new System.Drawing.Size(214, 13);
            this.lbLocalfile.TabIndex = 4;
            this.lbLocalfile.Text = "no local file associated with inventory object";
            // 
            // UrlForm
            // 
            this.AcceptButton = this.btn_ok;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btn_cancel;
            this.ClientSize = new System.Drawing.Size(483, 145);
            this.Controls.Add(this.lbLocalfile);
            this.Controls.Add(this.btnView);
            this.Controls.Add(this.txbImageUrl);
            this.Controls.Add(this.btn_cancel);
            this.Controls.Add(this.btn_ok);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "UrlForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Set the inventory\'s image using URL";
            this.Load += new System.EventHandler(this.UrlForm_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btn_ok;
        private System.Windows.Forms.Button btn_cancel;
        private System.Windows.Forms.TextBox txbImageUrl;
        private System.Windows.Forms.Button btnView;
        private System.Windows.Forms.Label lbLocalfile;
    }
}