using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using SLNetworkComm;
using libsecondlife;
using SLMIV.Utils;

namespace SLMIV
{
    public partial class ImageViewerForm : Form
    {
        private SLNetCom netcom;
        private SecondLife client;
        private InventoryItem item;
        private ImageCache imagecache;
        private bool closePending = false;
        private bool saving = false;
        public TreeNode ItemTreeNode = new TreeNode();

        public ImageViewerForm(InventoryItem item, SecondLife secondlife, SLNetCom netcoms, ImageList imageIconList, ImageCache imageCache)
        {
            InitializeComponent();

            this.netcom = netcoms;
            this.client = secondlife;
            this.item = item;
            this.imagecache = imageCache;

            //Let's display the associated icon with the AssetType
            switch (item.AssetType)
	        {
                case AssetType.Texture:
                case AssetType.TextureTGA:
                case AssetType.ImageJPEG:
                case AssetType.ImageTGA:
                    this.Icon = Utils.MIUtils.MakeIcon(imageIconList.Images["snapshot.ico"], 16, true);
                    break;
		        default:
                    this.Icon = Utils.MIUtils.MakeIcon(imageIconList.Images["texture.ico"], 16, true);
                    break;
            }

        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void ImageViewerForm_Load(object sender, EventArgs e)
        {
            this.Text = item.Name + " (" + item.AssetType.ToString() + ")";

            InventoryImageConsole imageConsole = new InventoryImageConsole(this.client, this.netcom, this.item, this.imagecache);
            imageConsole.Dock = DockStyle.Fill;
            imageConsole.Location = new System.Drawing.Point(3, 8);
            imageConsole.ItemTreeNode = this.ItemTreeNode;//Pass the treeView1 node to InventoryImageConsole
            this.Controls.Add(imageConsole);
            this.Controls["InventoryImageConsole"].Show();
        }

        private void btnOpen_Click(object sender, EventArgs e)
        {
            //Make sure doesn't already exist or write over
            //Let's display the associated icon with the AssetType
            //NEEDS CORRECT PATH
            string tempfile = GetFullFileName();
            //((InventoryImageConsole)this.Controls["InventoryImageConsole"]).ima
            System.Diagnostics.Process.Start(tempfile);
        }

        /// <summary>
        /// Returns the ItemTreeNode.FullPath of this.item without its .ext
        /// </summary>
        /// <returns>string</returns>
        private string GetFullFileName()
        {
            string tempfile = Common.backupRootFolder + "\\" + this.client.Self.Name + "\\" + this.ItemTreeNode.FullPath.Substring(0, ItemTreeNode.FullPath.LastIndexOf(this.ItemTreeNode.Text));
            //Create the path
            Directory.CreateDirectory(tempfile);
            tempfile = tempfile + this.ItemTreeNode.Text;

            return tempfile;
        }
    }
}