// SL My Inventory Viewer v1.7.1
// by Seneca Taliaferro/Joseph P. Socoloski III (Minoa)
// Copyright 2007. All Rights Reserved.
// NOTE:   View your Inventory cache offline and out-world.
// WHAT'S NEW: 	
//          -Window GrowandShrink autoresizing
//          -Send Search results to Log (Special Thanks to nuad01)
//          -Creator ID search result to Log (Special Thanks to dedric_mauriac)
//          -Added 'View Image Log' file button.
//          -Enabled comments for image log (format:  UIDKey, filepath # item name and free text)
// LIMITS:  -You must extract your .inv file yourself
//          -Can only read what cache you have, not your entire inventory.
// TODO:    -btRestore_Click needs to add back to Collections
//          -Get Drag and Drop working
//          -Add automatic reading of yourid.inv.zip file(no manual extraction).
// class UrlForm: Form class to handle image links to urls                 
//LICENSE
//BY DOWNLOADING AND USING, YOU AGREE TO THE FOLLOWING TERMS:
//If it is your intent to use this software for non-commercial purposes, 
//such as in academic research, this software is free and is covered under 
//the GNU GPL License, given here: <http://www.gnu.org/licenses/gpl.txt> 
////////////////////////////////////////////////////////////////////////////
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Collections.Specialized;
using System.IO;
using Wintellect.PowerCollections;
using MyInventory;

namespace SLMIV.Utils.Forms
{
    public partial class UrlForm : Form
    {
        public UrlForm()
        {
            InitializeComponent();
        }

        public string url;
        public string key;
        public string lbNotify;

        private void UrlForm_Load(object sender, EventArgs e)
        {
            this.txbImageUrl.Text = this.url;
            this.lbLocalfile.Text = lbNotify;

            //Set the accept and cancel buttons on the form 
            this.btn_ok.DialogResult = DialogResult.OK;
            this.btn_cancel.DialogResult = DialogResult.Cancel;
        }

        private void btn_ok_Click(object sender, EventArgs e)
        {
            if (this.txbImageUrl.Text.Trim() != "http://")
            {
                //Write the txb text next to the key in the imagelog file
                this.url = this.txbImageUrl.Text;
                this.lbLocalfile.Text = this.lbNotify;

                //Does user want to overwite a local file with a url
                if (this.lbNotify.StartsWith("Found currect local image at:\r\n"))
                {
                    DialogResult dResult = MessageBox.Show("You already have an image with this item.\r\nOverwrite?", "Associate New Image", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                    if (dResult == DialogResult.Yes)
                    {
                        StringBuilder sbImageLog = new StringBuilder();
                        if (File.Exists(Common.ImageLog))
                        {
                            sbImageLog.Append(File.ReadAllText(Common.ImageLog));
                        }
                        int commapoint = (sbImageLog.ToString().IndexOf(this.key) + this.key.Length + 1);
                        int endofpath = (sbImageLog.ToString().IndexOf("\r\n", commapoint + 1));//find the first return after the commapoint
                        string pathtoimage = sbImageLog.ToString().Substring(commapoint, endofpath - commapoint);

                        //reWrite item's image file path into log file...
                        sbImageLog.Replace(this.key + "," + pathtoimage, this.key + "," + this.txbImageUrl.Text);

                        File.WriteAllText(Common.ImageLog, sbImageLog.ToString());

                        this.DialogResult = DialogResult.OK;
                    }
                    else if (dResult == DialogResult.No)
                    {
                        this.DialogResult = DialogResult.No;
                    }
                    else if (dResult == DialogResult.Cancel)
                    {
                        this.DialogResult = DialogResult.Cancel;
                    }

                    this.Dispose();
                }
            }
            else
            {
                this.DialogResult = DialogResult.No;
                MessageBox.Show("URL is blank. Not going to save new url.", "Associate New Image", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void btn_cancel_Click(object sender, EventArgs e)
        {
            this.Dispose();
        }

        private void btnView_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start(txbImageUrl.Text);
        }
    }
}