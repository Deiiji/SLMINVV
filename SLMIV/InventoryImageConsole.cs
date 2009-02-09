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

namespace SLMIV
{
    public partial class InventoryImageConsole : UserControl
    {
        private SLNetCom netcom;
        private SecondLife client;
        private InventoryItem item;
        public ImageCache imagecache;
        public TreeNode ItemTreeNode = new TreeNode();

        public InventoryImageConsole(SecondLife instance, SLNetCom netcoms, InventoryItem item, ImageCache imageCache)
        {
            InitializeComponent();
            //SecondLife secondlife, SLNetCom netcoms,
            netcom = netcoms;
            client = instance;
            this.item = item;
            this.imagecache = imageCache;

            if (imageCache.ContainsImage(item.AssetUUID))
                SetFinalImage(this.imagecache.GetImage(item.AssetUUID));
            else
            {
                this.Disposed += new EventHandler(InventoryImageConsole_Disposed);
                client.Assets.OnImageReceived += new AssetManager.ImageReceivedCallback(Assets_OnImageReceived);
                client.Assets.RequestImage(item.AssetUUID, ImageType.Normal, 125000.0f, 0);
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

            BeginInvoke(new OnSetStatusText(SetStatusText), new object[] { "Image downloaded. Decoding..." });

            System.Drawing.Image decodedImage = ImageHelper.Decode(image.AssetData);

            if (decodedImage == null)
            {
                BeginInvoke(new OnSetStatusText(SetStatusText), new object[] { "D'oh! Error decoding image." });
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
                pnlOptions.Visible = true;
                pnlStatus.Visible = false;

                string itemdir = Common.backupRootFolder + "\\" + this.client.Self.Name + "\\" + this.ItemTreeNode.FullPath.Substring(0, ItemTreeNode.FullPath.LastIndexOf(this.ItemTreeNode.Text)); ;
                //Create the path
                if (!Directory.Exists(itemdir))
                    Directory.CreateDirectory(itemdir);
                sfdImage.InitialDirectory = itemdir;
                //Craft a valid filename
                string validName = MIUtils.CleanForWriteandRead(this.ItemTreeNode.Text);

                if ((item.Permissions.OwnerMask & PermissionMask.Copy) == PermissionMask.Copy)
                {
                    btnSave.Click += delegate(object sender, EventArgs e)
                    {
                        sfdImage.FileName = validName;
                        if (sfdImage.ShowDialog() == DialogResult.OK)
                        {
                            if (sfdImage.FileName != string.Empty)
                            {
                                switch (sfdImage.FilterIndex)
                                {
                                    case 1: //BMP
                                        pbxImage.Image.Save(sfdImage.FileName, ImageFormat.Bmp);
                                        break;

                                    case 2: //JPG
                                        pbxImage.Image.Save(sfdImage.FileName, ImageFormat.Jpeg);
                                        break;

                                    case 3: //PNG
                                        pbxImage.Image.Save(sfdImage.FileName, ImageFormat.Png);
                                        break;

                                    default:
                                        pbxImage.Image.Save(sfdImage.FileName, ImageFormat.Bmp);
                                        break;
                                }

                                //Auto Attach this item to treeView1 node
                                //Write item's key and the image's file path into log file...
                                Logs.ImageLogEntry(ItemTreeNode.Name + "," + sfdImage.FileName + " # " + ItemTreeNode.Text, File.AppendText(Common.ImageLog));

                            }
                        }
                    };

                    btnSave.Enabled = true;
                }
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

        private void DoErrorState()
        {
            lblStatus.Visible = true;
            lblStatus.ForeColor = Color.Red;
            proActivity.Visible = false;

            pnlStatus.Visible = true;
            pnlOptions.Visible = false;
        }
    }
}
