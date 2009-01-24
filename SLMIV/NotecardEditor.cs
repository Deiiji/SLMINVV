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
    public partial class frmNotecardEditor : Form
    {
        private SLNetCom netcom;
        private SecondLife client;
        private InventoryItem item;
        private LLUUID uploadID;
        private LLUUID transferID;

        private bool closePending = false;
        private bool saving = false;
        public TreeNode ItemTreeNode = new TreeNode();

        public frmNotecardEditor(InventoryItem item, SecondLife secondlife, SLNetCom netcoms, ImageList imageIconList)
        {
            InitializeComponent();

            netcom = netcoms;
            client = secondlife;
            this.item = item;
            AddNetcomEvents();

            //Let's display the associated icon with the AssetType
            if (item.AssetType == AssetType.Notecard)
                this.Icon = Utils.MIUtils.MakeIcon(imageIconList.Images["textdoc.ico"], 16, true);
            else if (item.AssetType == AssetType.LSLText)
                this.Icon = Utils.MIUtils.MakeIcon(imageIconList.Images["script.ico"], 16, true);

            this.Text = item.Name + " (" + item.AssetType.ToString() + ")";

            client.Assets.OnAssetReceived += new AssetManager.AssetReceivedCallback(Assets_OnAssetReceived);
            transferID = client.Assets.RequestInventoryAsset(item.AssetUUID, item.UUID, LLUUID.Zero, item.OwnerID, item.AssetType, false);
        }

        //Separate thread
        private void Assets_OnAssetReceived(AssetDownload transfer, Asset asset)
        {
            if (transfer.ID != transferID) return;

            string notecardContent;

            if (!transfer.Success)
            {
                notecardContent = "Unable to download item. You may not have the correct permissions.";
                BeginInvoke(new OnSetNotecardText(SetNotecardText), new object[] { notecardContent, true });
                return;
            }

            notecardContent = Helpers.FieldToUTF8String(transfer.AssetData);
            BeginInvoke(new OnSetNotecardText(SetNotecardText), new object[] { notecardContent, false });
        }

        //UI thread
        private delegate void OnSetNotecardText(string text, bool readOnly);
        private void SetNotecardText(string text, bool readOnly)
        {
            rtbNotecard.Clear();
            rtbNotecard.Text = text;

            if (readOnly)
            {
                rtbNotecard.ReadOnly = true;
                rtbNotecard.BackColor = Color.FromKnownColor(KnownColor.Control);
            }
            else
            {
                rtbNotecard.ReadOnly = false;
                rtbNotecard.BackColor = Color.White;
            }

            btnClose.Enabled = true;
            this.btnOpen.Enabled = true;
        }

        private void AddNetcomEvents()
        {
            netcom.ClientLoggedOut += new EventHandler(netcom_ClientLoggedOut);
        }

        private void netcom_ClientLoggedOut(object sender, EventArgs e)
        {
            closePending = false;
            this.Close();
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void rtbNotecard_TextChanged(object sender, EventArgs e)
        {
            if (!rtbNotecard.ReadOnly)
                btnSave.Enabled = true;
        }

        private DialogResult AskForSave()
        {
            return MessageBox.Show(
                "Your changes have not been saved. Save the notecard?",
                "SLMIV",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question);
        }

        private void SaveNotecard()
        {
            rtbNotecard.ReadOnly = true;
            rtbNotecard.BackColor = Color.FromKnownColor(KnownColor.Control);

            lblSaveStatus.Text = "Saving notecard...";

            string tempfile = GetFullFileName();
            FileStream fs = File.Create(tempfile);
            fs.Close();
            File.WriteAllText(tempfile, rtbNotecard.Text);

            lblSaveStatus.Visible = true;
            btnSave.Enabled = false;
            btnClose.Enabled = false;
        }


        //Separate thread

        private void Assets_OnAssetUploaded(AssetUpload upload)
        {
            if (upload.ID != uploadID) return;

            saving = false;
            if (closePending)
            {
                closePending = false;
                this.Close();
                return;
            }

            BeginInvoke(new MethodInvoker(SaveComplete));
        }

        private void SaveComplete()
        {
            rtbNotecard.ReadOnly = false;
            rtbNotecard.BackColor = Color.White;
            btnClose.Enabled = true;

            lblSaveStatus.Text = "Save completed.";
            lblSaveStatus.Visible = true;
            btnSave.Enabled = false;
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            //Fire the BackupItem Event
            string tempfile = GetFullFileName();
            FileStream fs = File.Create(tempfile);
            fs.Close();
            File.WriteAllText(tempfile, rtbNotecard.Text);
            //Auto Attach this item to treeView1 node
            //Write item's key and the image's file path into log file...
            Logs.ImageLogEntry(ItemTreeNode.Name + "," + tempfile + " # " + ItemTreeNode.Text, File.AppendText(Common.ImageLog));

            SaveComplete();
        }

        private void frmNotecardEditor_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (closePending || saving)
                e.Cancel = true;
            else if (btnSave.Enabled)
            {
                DialogResult result = AskForSave();

                switch (result)
                {
                    case DialogResult.Yes:
                        e.Cancel = true;
                        closePending = true;
                        SaveNotecard();
                        break;

                    case DialogResult.No:
                        e.Cancel = false;
                        break;

                    case DialogResult.Cancel:
                        e.Cancel = true;
                        break;
                }
            }
        }

        private void btnOpen_Click(object sender, EventArgs e)
        {
            //Make sure doesn't already exist or write over
            //Let's display the associated icon with the AssetType
            //NEEDS CORRECT PATH
            string tempfile = GetFullFileName();
            FileStream fs = File.Create(tempfile);
            fs.Close();
            File.WriteAllText(tempfile, rtbNotecard.Text);
            System.Diagnostics.Process.Start(tempfile);
        }

        /// <summary>
        /// Returns the ItemTreeNode.FullPath of this.item plus its .ext
        /// </summary>
        /// <returns>string</returns>
        private string GetFullFileName()
        {
            string tempfile = Common.backupRootFolder + "\\" + this.client.Self.Name + "\\" + this.ItemTreeNode.FullPath.Substring(0, ItemTreeNode.FullPath.LastIndexOf(this.ItemTreeNode.Text));
            //Create the path
            Directory.CreateDirectory(tempfile);
            if (item.AssetType == AssetType.Notecard)
                tempfile = tempfile + MIUtils.CleanForWriteandRead(this.ItemTreeNode.Text) + ".txt";
            else if (item.AssetType == AssetType.LSLText)
                tempfile = tempfile + MIUtils.CleanForWriteandRead(this.ItemTreeNode.Text) + ".lsl";

            return tempfile;
        }
    }
}