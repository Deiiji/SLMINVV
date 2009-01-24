// ImageDownloader Class
// by Seneca Taliaferro/Joseph P. Socoloski III (Minoa)
// Copyright 2009. All Rights Reserved.
// NOTE:   
//
//LICENSE
//BY DOWNLOADING AND USING, YOU AGREE TO THE FOLLOWING TERMS:
//If it is your intent to use this software for non-commercial purposes, 
//such as in academic research, this software is free and is covered under 
//the GNU GPL License, given here: <http://www.gnu.org/licenses/gpl.txt> 
//You agree with Linden Research, Inc's Terms Of Service
//given here: <http://secondlife.com/corporate/tos.php>
////////////////////////////////////////////////////////////////////////////
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Data;
using System.IO;
using System.Text;
using System.Windows.Forms;
using SLNetworkComm;
using libsecondlife;
using SLMIV.Utils;
using System.Threading;
namespace SLMIV
{
    class ImageDownloader : UserControl
    {
        public PictureBox pbxImage;
        private Panel pnlStatus;
        public Label lblStatus;
        private ProgressBar proActivity;
        public TextBox tBoxStatus = new TextBox();

        private SLNetCom netcom;
        private SecondLife client;
        private InventoryItem item;
        public ImageCache imagecache;
        public TreeNode ItemTreeNode = new TreeNode();
        private String FilePath;
        public bool Decoded;
        public int ImagesFound;
        public int ImagesTransferred;

        public ImageDownloader(SecondLife instance, SLNetCom netcoms, InventoryItem item, ImageCache imageCache, string savepath)
        {
            InitializeComponent();
            //SecondLife secondlife, SLNetCom netcoms,
            netcom = netcoms;
            client = instance;
            this.item = item;
            this.imagecache = imageCache;
            this.FilePath = savepath;

            if (imageCache.ContainsImage(item.AssetUUID))
                SetFinalImage(this.imagecache.GetImage(item.AssetUUID));
            else
            {
                this.Disposed += new EventHandler(InventoryImageConsole_Disposed);
                client.Assets.OnImageReceived += new AssetManager.ImageReceivedCallback(Assets_OnImageReceived);
                SetStatusBoxText("Requesting: " + item.Name);
                client.Assets.RequestImage(item.AssetUUID, ImageType.Normal, 125000.0f, 0);
                this.Decoded = false;
            }
        }

        private void InventoryImageConsole_Disposed(object sender, EventArgs e)
        {
            client.Assets.OnImageReceived -= new AssetManager.ImageReceivedCallback(Assets_OnImageReceived);
        }

        //comes in on separate thread
        private void Assets_OnImageReceived(ImageDownload image, AssetTexture texture)
        {
            if (image.ID != item.AssetUUID) return;

            BeginInvoke(new OnSetStatusText(SetStatusText), new object[] { "Image downloaded. Decoding " + item.Name + "..." });
            BeginInvoke(new OnSetStatusBoxText(SetStatusBoxText), new object[] { "Image downloaded. Decoding " + item.Name + "..." });

            System.Drawing.Image decodedImage = ImageHelper.Decode(image.AssetData);

            if (decodedImage == null)
            {
                BeginInvoke(new OnSetStatusText(SetStatusText), new object[] { "Error decoding image." });
                BeginInvoke(new OnSetStatusBoxText(SetStatusBoxText), new object[] { "Error decoding image." });
                BeginInvoke(new MethodInvoker(DoErrorState));
                return;
            }

            this.imagecache.AddImage(image.ID, decodedImage);
            BeginInvoke(new OnSetFinalImage(SetFinalImage), new object[] { decodedImage });
        }

        //called on GUI thread
        private delegate void OnSetFinalImage(System.Drawing.Image finalImage);
        private void SetFinalImage(System.Drawing.Image finalImage)
        {
            try
            {
                pbxImage.Image = finalImage;
                //pnlOptions.Visible = true;
                pnlStatus.Visible = false;
                this.Decoded = true;
                //if ((item.Permissions.OwnerMask & PermissionMask.Copy) == PermissionMask.Copy)
                //{
                // create the directory to put this in
                pbxImage.Image.Save(this.FilePath, ImageFormat.Jpeg);
                //}
                BeginInvoke(new OnSetStatusBoxText(SetStatusBoxText), new object[] { "Saved: " + this.FilePath });
            }
            catch (Exception ex)
            {
                MessageBox.Show("InventoryImageConsole Error: " + ex.Message, "Name error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

        }

        //called on GUI thread
        private delegate void OnSetStatusText(string text);
        private void SetStatusText(string text)
        {
            lblStatus.Text = text;
        }

        private delegate void OnSetStatusBoxText(string text);
        private void SetStatusBoxText(string text)
        {
            tBoxStatus.AppendText("[" + DateTime.Now.ToShortTimeString() + "] " + text + Environment.NewLine);
        }

        private void DoErrorState()
        {
            lblStatus.Visible = true;
            lblStatus.ForeColor = Color.Red;
            proActivity.Visible = false;

            pnlStatus.Visible = true;
            //pnlOptions.Visible = false;
        }

        private void InitializeComponent()
        {
            this.pnlStatus = new System.Windows.Forms.Panel();
            this.lblStatus = new System.Windows.Forms.Label();
            this.proActivity = new System.Windows.Forms.ProgressBar();
            this.pbxImage = new System.Windows.Forms.PictureBox();
            this.pnlStatus.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pbxImage)).BeginInit();
            this.SuspendLayout();
            // 
            // pnlStatus
            // 
            this.pnlStatus.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.pnlStatus.Controls.Add(this.lblStatus);
            this.pnlStatus.Controls.Add(this.proActivity);
            this.pnlStatus.Location = new System.Drawing.Point(3, 3);
            this.pnlStatus.Name = "pnlStatus";
            this.pnlStatus.Size = new System.Drawing.Size(214, 44);
            this.pnlStatus.TabIndex = 3;
            // 
            // lblStatus
            // 
            this.lblStatus.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblStatus.Location = new System.Drawing.Point(3, 0);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(188, 13);
            this.lblStatus.TabIndex = 0;
            this.lblStatus.Text = "Downloading image...";
            // 
            // proActivity
            // 
            this.proActivity.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.proActivity.Location = new System.Drawing.Point(3, 16);
            this.proActivity.MarqueeAnimationSpeed = 50;
            this.proActivity.Name = "proActivity";
            this.proActivity.Size = new System.Drawing.Size(208, 16);
            this.proActivity.Style = System.Windows.Forms.ProgressBarStyle.Marquee;
            this.proActivity.TabIndex = 1;
            // 
            // pbxImage
            // 
            this.pbxImage.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.pbxImage.Location = new System.Drawing.Point(3, 53);
            this.pbxImage.Name = "pbxImage";
            this.pbxImage.Size = new System.Drawing.Size(211, 124);
            this.pbxImage.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pbxImage.TabIndex = 1;
            this.pbxImage.TabStop = false;
            // 
            // ImageDownloader
            // 
            this.Controls.Add(this.pnlStatus);
            this.Controls.Add(this.pbxImage);
            this.Name = "ImageDownloader";
            this.Size = new System.Drawing.Size(220, 177);
            this.pnlStatus.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.pbxImage)).EndInit();
            this.ResumeLayout(false);

        }

    }

    public class QueuedDownloadInfo
    {
        public InventoryItem IItem;
        public string FileName;
        public DateTime WhenRequested;
        public bool IsRequested;

        public QueuedDownloadInfo(string file, InventoryItem iitem)
        {
            FileName = file;
            IItem = iitem;
            WhenRequested = DateTime.Now;
            IsRequested = false;
        }
    }

}
