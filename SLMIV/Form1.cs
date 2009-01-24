// SL My Inventory Viewer v2.10.16
// by Seneca Taliaferro/Joseph P. Socoloski III (Minoa)
// Copyright 2008-2009. All Rights Reserved.
// NOTE:   View and search your inventory offline and out-world.
// WHAT'S NEW: 	
//  - FIXED: Issue 3: Can only span out to 8 folders of inventory. [InsertUnderParent, TreeViewToXML, sendSearchToLogToolStripMenuItem_Click recursive]
//  - FIXED: Issue 5: Images do not open when right-clicking on an object
//  - FIXED: Issue 6: Download Notecards and scripts
//  - FIXED: Issue 7: 'Could not find Photo Album folder' exception
//  - FIXED: Issue 9: Doing two SL Connections in a row causes freezing
//  - FIXED: Stopped right-click menu from first draw in upperleft corner
//  - FIXED: OS Support=[Vista SP1 and Windows 7 (Beta Build 7000) support]
//  - NEW FEATURE: Backup Scripts, Notecards, Textures, and other images in your inventory as one job. (Auto-adds to your imagelog)
//  - NEW FEATURE: Backup Scripts, Notecards, Textures, and other images individually by right-click->Download on an item.(Auto-adds to your imagelog)
//  - NEW FEATURE: Issue 10: Connect to other grids (buggy because of other grid permissions and interfaces)
//  - NEW FEATURE: SL Connection Logging for troubleshooting
//  - NEW FEATURE: 'Create Word Hit List': Lists highest to lowest of word frequencies found in the current treeview inventory
//  - NEW FEATURE: Issue 12: Search & Filter by property: Transfer, Modify, Copy, Move, All (Special Thanks to sircaren)
//
// LIMITS:  -No in-world features (rezzing, etc.) enabled while connected to SL
//          -Can not download objects in a 3D format.
// TODO:    
//          - Issue 11: Save inventory to hard disk and be able to *restore* them to any SL grid.
//LICENSE
//BY DOWNLOADING AND USING, YOU AGREE TO THE FOLLOWING TERMS:
//If it is your intent to use this software for non-commercial purposes, 
//such as in academic research, this software is free and is covered under 
//the GNU GPL License, given here: <http://www.gnu.org/licenses/gpl.txt> 
////////////////////////////////////////////////////////////////////////////
//NOTE: Before compiling you may need to add the SLMIV directory in your My Documents because this
// is typically created by the Inno script setup
#region using
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using System.Windows.Forms;
using System.Collections.Specialized;
using System.IO;
using System.Threading;
using Wintellect.PowerCollections;
using SLMIV.Utils;
using MyInventory;
using libsecondlife;
using SLNetworkComm;
using System.Xml;
using System.Drawing.Printing;
#endregion using

namespace SLMIV
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();

            //Initialize SL communication library objects
            client = new SecondLife();
            client.Settings.ALWAYS_REQUEST_OBJECTS = true;
            client.Settings.ALWAYS_DECODE_OBJECTS = true;
            client.Settings.OBJECT_TRACKING = true;
            client.Settings.ENABLE_SIMSTATS = true;
            client.Settings.FETCH_MISSING_INVENTORY = true;
            client.Settings.MULTIPLE_SIMS = true;
            client.Settings.SEND_AGENT_THROTTLE = true;
            client.Settings.SEND_AGENT_UPDATES = true;

            netcom = new SLNetCom(client);
            imageCache = new ImageCache();
            netcom.NetcomSync = this;
            AddNetcomEvents();//Needed for SLNetCom
        }

        #region variables
        public StringCollection SColl = new StringCollection();
        public StringBuilder SBuild = new StringBuilder();
        public TreeView BackupTreeView = new TreeView();
        public ImageCache imageCache;
        private LLUUID transferID;
        private LLUUID PhotoAlbumUUID = new LLUUID();
        string notecardContent;

        /// <summary>
        /// Used for tabBackup image downloading
        /// Feed it a TextBox control to receive status output
        /// </summary>
        //private ImageDownloader iDownloader = new ImageDownloader();
        // all items here, fed by the inventory walking thread
        public Queue<QueuedDownloadInfo> PendingImageDownloads = new Queue<QueuedDownloadInfo>();

        /// <summary>
        /// Used for AddNode and ImportXML
        /// </summary>
        public SLMIV.Utils.Forms.ProgressbarForm progressbar = new SLMIV.Utils.Forms.ProgressbarForm();

        /// <summary>
        /// Holds imported xml filename
        /// </summary>
        public string xmlfilename;

        /// <summary>
        /// User's SL Avatar Key
        /// </summary>
        public string userkey;

        /// <summary>
        /// Connection state
        /// </summary>
        bool connecttosl = false;

        /// <summary>
        /// SLNetCom object
        /// </summary>
        private SLNetCom netcom;

        /// <summary>
        /// libsecondlife object
        /// </summary>
        private SecondLife client;

        /// <summary>
        /// For SLNetCon inventory treenode
        /// </summary>
        private Dictionary<LLUUID, TreeNode> treeLookup = new Dictionary<LLUUID, TreeNode>();

        /// <summary>
        /// Holds InventoryItems [no Folders] builted after logging in
        /// </summary>
        public Dictionary<LLUUID, InventoryItem> libsecItemDict = new Dictionary<LLUUID, InventoryItem>();

        /// <summary>
        /// Holds some counts to display
        /// </summary>
        Dictionary<InventoryType, int> IICountsDict = new Dictionary<InventoryType, int>();

        /// <summary>
        /// Needed for a tabBackup backup. Holds folder paths while iterating thru inventory.
        /// </summary>
        public List<string> CurrentPath = new List<string>();

        public TreeNode selectedTreeNode
        {
            get { return _selectedTreeNode; }
            set { _selectedTreeNode = value; }
        }
        TreeNode _selectedTreeNode = new TreeNode();

        //For logs
        public string fullpath_ofnode
        {
            get { return _fullpath_ofnode; }
            set { _fullpath_ofnode = value; }
        }
        string _fullpath_ofnode;

        /// <summary>
        /// Used for ReNaming of a node
        /// </summary>
        public string oldNodeName;

        private Font printFont;
        private StreamReader streamToPrint;
        #endregion

        #region ToolStrip
        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //MessageBox.Show("Second Life My Inventory Viewer " + Application.ProductVersion + "\r\nby Joseph P. Socoloski III\r\nhttp://", "About SLMIV", MessageBoxButtons.OK, MessageBoxIcon.Information);
            AboutBox1 AboutForm = new AboutBox1();
            AboutForm.ShowDialog();
        }

        /// <summary>
        /// Opens Official SLMIV url from Help Menu 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void onlineHelpToolStripMenuItem_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start(Common.urlSLMIV);
        }

        /// <summary>
        /// Browse to the SLMIV Folder (eg. C:\Users\USERNAME\Documents\SLMIV
        /// </summary>
        /// <param name="sender">object</param>
        /// <param name="e">EventArgs</param>
        private void openSLMIVFolderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start(Common.SLMIVPath);
        }

        /// <summary>
        /// Close application from clicking Close from the File menu
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void closeToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            //Make sure everything is closed
            if (netcom != null)
            {
                if (!netcom.IsLoggedIn)
                {
                    this.Close();
                }
                else
                {
                    netcom.Logout();
                    this.Close();
                }
            }
            else
                this.Close();
        }

        /// <summary>
        /// Browse to .inv from menu
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void invToolStripMenuItem_Click(object sender, EventArgs e)
        {
            tabControl1.SelectedTab = tabControl1.TabPages["tabImportExport"];
            btBrowse_Click(sender, e);
        }

        /// <summary>
        /// Browse to .xml from menu
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void xmlToolStripMenuItem_Click(object sender, EventArgs e)
        {
            tabControl1.SelectedTab = tabControl1.TabPages["tabImportExport"];
            btImportXML_Click(sender, e);
        }

        /// <summary>
        /// Import from SL connection from Menu
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void fromSLToolStripMenuItem_Click(object sender, EventArgs e)
        {
            tabControl1.SelectedTab = tabControl1.TabPages["tabSLCom"];
        }

        /// <summary>
        /// Link to Online FAQ
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void sLMIVFAQToolStripMenuItem_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start(Common.urlFaq);
        }

        /// <summary>
        /// Links to SE products on shop.onrez
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void visitSEProductsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start(Common.urlShop);
        }

        /// <summary>
        /// Iterates thru TreeNodeCollection and Matches the tbSearchbox.Text with the node's 'creator_id' field.
        /// </summary>
        /// <param name="treenodecoll">TreeNodeCollection</param>
        /// <param name="stringb">stringb</param>
        private void CreatorIdSearchRecursive(TreeNodeCollection treenodecoll, StringBuilder stringb)
        {
            MIUtils.INV_TYPE typeofobject;
            foreach (TreeNode tn0 in treenodecoll)
            {
                object objinfo = new object();
                objinfo = MIUtils.GetInvObj(tn0.Name, MIUtils.OD_treeindex, out typeofobject);
                if (typeofobject == MIUtils.INV_TYPE.ITEM)
                {
                    if (((Item)objinfo).permission.creator_id == tbSearchbox.Text)
                    {
                        //Copy the node selected...
                        stringb.AppendLine(tn0.FullPath);
                    }
                }
                CreatorIdSearchRecursive(tn0.Nodes, stringb);
            }
        }

        /// <summary>
        /// Iterates thru TreeNodeCollection and Matches the tbSearchbox.Text with the node's 'Name' field.
        /// </summary>
        /// <param name="treenodecoll">TreeNodeCollection</param>
        /// <param name="stringb">stringb</param>
        private void ItemNameSearchRecursive(TreeNodeCollection treenodecoll, StringBuilder stringb)
        {
            foreach (TreeNode tn0 in treenodecoll)
            {
                if (tn0.Text.IndexOf(tbSearchbox.Text, 0, StringComparison.OrdinalIgnoreCase) != -1)
                {
                    //Copy the node selected...
                    stringb.AppendLine(tn0.FullPath);
                }
                ItemNameSearchRecursive(tn0.Nodes, stringb);
            }
        }

        /// <summary>
        /// Send the current inventory to the mainlog
        /// </summary>
        /// <param name="sender">object</param>
        /// <param name="e">EventArgs</param>
        private void sendSearchToLogToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //Append all of the treeView1 to the log file
            StringBuilder sb_output = new StringBuilder();

            if (cbSearchbyCreatorkey.Checked)
            {
                //For UIDKey search in the inventory "Creator Id"
                CreatorIdSearchRecursive(treeView1.Nodes, sb_output);//recursive method
                Logs.LogEntry("Creator ID search result for '" + userkey + "'\r\n" + sb_output.ToString(), File.AppendText(Common.MainLog));
                MessageBox.Show("Appended Creator ID search results for\r\n'" + userkey + "'\r\nPress Inventory then 'Reload Original' menu button\r\nto restore the original inventory tree.", "Log entry complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                //For text search in the inventory item's Name
                ItemNameSearchRecursive(treeView1.Nodes, sb_output);
                Logs.LogEntry("Examine from search result for '" + tbSearchbox.Text + "'\r\n" + sb_output.ToString(), File.AppendText(Common.MainLog));
                MessageBox.Show("Appended search results for\r\n'" + tbSearchbox.Text + "'\r\nPress Inventory then 'Reload Original' menu button\r\nto restore the original inventory tree.", "Log entry complete", MessageBoxButtons.OK, MessageBoxIcon.Information);

                //Show the Log tab
                tabControl1.SelectedTab = tabControl1.TabPages["tabLogs"];
            }
        }

        /// <summary>
        /// Erase and start a new MainLog
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void clearToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //Prompt the user if they are sure they want to clear the MainLog
            DialogResult dResult = MessageBox.Show("Are you sure you want to clear your current Main Log?", "Erase Current Main Log", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (dResult == DialogResult.Yes)
            {
                Logs.CreateMainLogFile(this.tbLog);
                //Refresh the logs since we created a new log file
                UpdateLogTextboxes();
                //Show the Log tab
                tabControl1.SelectedTab = tabControl1.TabPages["tabLogs"];
            }
            else if (dResult == DialogResult.No)
            {

            }
            else if (dResult == DialogResult.Cancel)
            {

            }
        }

        /// <summary>
        /// Open the Mainlog file in editor
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void editToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            if (File.Exists(Common.MainLog))
            {
                System.Diagnostics.Process.Start(Common.MainLog);
            }
        }

        /// <summary>
        /// Clear the Image Log
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void clearToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            //Prompt the user if they are sure they want to clear the MainLog
            DialogResult dResult = MessageBox.Show("Are you sure you want to clear your current Image Log?\r\nThis will erase all of your current associations with inventory UUID objects.", "Erase Current Image and URL Attaches Log", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (dResult == DialogResult.Yes)
            {
                Logs.CreateImageLogFile();
                //Refresh the logs since we created a new log file
                UpdateLogTextboxes();
                //Show the Log tab
                tabControl1.SelectedTab = tabControl1.TabPages["tabLogs"];
                tabControlLogs.SelectedTab = tabControlLogs.TabPages["tabImageLog"];
            }
            else if (dResult == DialogResult.No)
            {

            }
            else if (dResult == DialogResult.Cancel)
            {

            }
        }

        /// <summary>
        /// Open the ImageLog file in editor
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void editToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (File.Exists(Common.ImageLog))
            {
                System.Diagnostics.Process.Start(Common.ImageLog);
                //System.Diagnostics.ProcessStartInfo start = new System.Diagnostics.ProcessStartInfo(Common.ImageLog);
                //start.UseShellExecute = true;
                //System.Diagnostics.Process.Start(start);
            }
        }
        /// <summary>
        /// Restart the application and start new
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void newRestartToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Restart();
        }

        /// <summary>
        /// Go to the SLMIV Support page
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void submitBugFeatureRequestToolStripMenuItem_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start(Common.urlSupport);
        }
        /// <summary>
        /// Create a list listing words found in the current treeView1 inventory and how many times that word is found.
        /// </summary>
        /// <param name="sender">object</param>
        /// <param name="e">EventArgs</param>
        private void createWordHitListToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Cursor.Current = Cursors.WaitCursor;
            //I gave up on having the results display as a treeview so displays in tbOutput
            tbOutput.Clear();
            tbOutput.Text = ("REPETITIVE WORDS IN YOUR INVENTORY HIT LIST:" + Environment.NewLine);
            tbOutput.Text += ("============================================" + Environment.NewLine);
            foreach (string result in MIUtils.CreateWordHitList(treeView1))
            {
                tbOutput.Text += (result + Environment.NewLine);
            }

            if (tabControl1.SelectedTab != tabControl1.TabPages["tabMyInv"])
            {
                //Go to the MyInventory tab
                tabControl1.SelectedTab = tabControl1.TabPages["tabMyInv"];
            }
            Cursor.Current = Cursors.Default;
        }

        #endregion ToolStrip

        #region Misc
        /// <summary>
        /// Before the Form is loaded for the first time
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Form1_Load(object sender, EventArgs e)
        {
            //Populate the textboxes using the settings file
            tbFirstName.Text = SLLoginSettings.Default.FirstName;
            tbLastName.Text = SLLoginSettings.Default.LastName;
            tbPassword.Text = SLLoginSettings.Default.Password;

            //Set Last Location selectionbox
            if (SLLoginSettings.Default.LastLocation.Trim() != "")
            {
                cbxLocation.Text = SLLoginSettings.Default.LastLocation;
            }
            else
                cbxLocation.SelectedIndex = 1;

            //Set Grid to main
            cbxGrid.SelectedIndex = 0;

            //Refresh all the SL values and their labels and buttons
            RefreshSLValues();

            //Create the log text file
            Logs.CreateMainLogFile(this.tbLog);

            //See if ImageLog exists, if not create an empty one
            if (!File.Exists(Common.ImageLog))
            {
                Logs.CreateImageLogFile();
            }
            //See if SLConnectLog exists, if not create an empty one
            if (!File.Exists(Common.SLConnectLog))
            {
                FileStream fs = File.Create(Common.SLConnectLog);
                fs.Close();
            }

            //Subscribe to any HookEvents
            client.Assets.OnAssetReceived += new AssetManager.AssetReceivedCallback(Assets_OnAssetReceived);
            client.OnLogMessage += new SecondLife.LogCallback(client_OnLogMessage);
        }

        /// <summary>
        /// Occurs before user closes the form
        /// Clean-up
        /// </summary>
        /// <param name="sender">object</param>
        /// <param name="e">FormClosingEventArgs</param>
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                //Check and make sure we are not connected to SL
                if (client.Network.Connected)
                {
                    client.Network.Logout();
                    Thread.Sleep(2000);
                }
            }
            catch (Exception)
            {
                this.Dispose();
            }
        }

        /// <summary>
        /// Creates a fullpath string starting at 'My Inventory' root folder and uses CurrentPath List
        /// </summary>
        /// <returns>string</returns>
        private string CurrentPathToString()
        {
            string Fullpath = "My Inventory";//Start at the RootFolder
            foreach (string var in CurrentPath)
            {
                Fullpath = Fullpath + "\\" + var;
            }
            return Fullpath;
        }

        /// <summary>
        /// Search by Creatorkey. If no key is in the searchbox it will use userkey
        /// </summary>
        private void cbSearchbyCreatorkey_CheckedChanged(object sender, EventArgs e)
        {
            if (cbSearchbyCreatorkey.Checked)
            {
                tbSearchbox.Text = userkey;
            }
            else
            {
                tbSearchbox.Text = "";
            }
        }

        /// <summary>
        /// Inserts a Folder or Item into the ViewTree
        /// </summary>
        /// <param name="parent_id">Its parent id #</param>
        /// <param name="idkey">Its own id #</param>
        /// <param name="title">Text name</param>
        /// <param name="imageindex">Desired image association</param>
        private void InsertUnderParent(string parent_id, string idkey, string title, int imageindex, string elementname, TreeNodeCollection treenodecoll)
        {
            try
            {
                foreach (TreeNode tn0 in treenodecoll)
                {
                    if (tn0.Name == parent_id)
                    {
                        tn0.Nodes.Insert(tn0.Nodes.Count + 1, idkey, title, imageindex);
                        //Add a tag for xml Element creation
                        tn0.Nodes[tn0.Nodes.Count - 1].Tag = elementname;
                    }
                    InsertUnderParent(parent_id, idkey, title, imageindex, elementname, tn0.Nodes);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("InsertUnderParent() throw: " + ex.Message);
            }

        }

        /// <summary>
        /// Calls ClearTreeView, then Create the TreeView like in 1.7.x
        /// </summary>
        private void CreateTreeView()
        {
            try
            {
                treeView1.BeginUpdate();
                ClearTreeView();
                treeView1.EndUpdate();

                treeView1.Refresh();
                //UPDATE THE TREEVIEW////////////////////////////////////////////////////////////////
                // Display a wait cursor while the TreeNodes are being created.
                Cursor.Current = Cursors.WaitCursor;

                // Suppress repainting the TreeView until all the objects have been created.
                treeView1.BeginUpdate();

                // Clear the TreeView each time the method is called.
                treeView1.Nodes.Clear();

                int countindex = 0;
                foreach (KeyValuePair<int, Object> pair in MIUtils.OD_MyInventory)
                {
                    //If it is a folder, then add to TreeView...
                    if (pair.Value.GetType().ToString() == "MyInventory.Folder")
                    {
                        Folder ftemp = (Folder)pair.Value;

                        //Add the node info to the search list
                        MIUtils.OD_treeindex.Add(ftemp.cat_id, countindex);

                        //What type of item is it?
                        int imageindex = MIUtils.Getinv_type(ftemp.type);
                        //if it is the "Trash" can, then reassign properly
                        if (ftemp.name == "Trash")
                            imageindex = MIUtils.Getinv_type(ftemp.pref_type);

                        //If the folder is a root then add it
                        if (ftemp.parent_id == "00000000-0000-0000-0000-000000000000")
                        {
                            //Add the node with text to treeview1
                            treeView1.Nodes.Add(ftemp.cat_id, ftemp.name, imageindex);
                            //Add a tag for xml Element creation
                            treeView1.Nodes[countindex].Tag = ftemp.type;
                        }
                        else//Then it must have a parent folder...
                        {
                            //Add the child node
                            InsertUnderParent(ftemp.parent_id, ftemp.cat_id, ftemp.name, imageindex, ftemp.type, treeView1.Nodes);
                            //treeView1.Nodes[treeView1.Nodes.Count-1].Tag = ftemp.type;
                        }
                    }
                    else//Must be an Item
                    {
                        Item Itemp = (Item)pair.Value;

                        //Add the node info to the search list
                        MIUtils.OD_treeindex.Add(Itemp.item_id, countindex);

                        //What type of item is it?
                        int imageindex = MIUtils.Getinv_type(Itemp.inv_type);

                        //If the Item is a root then add it
                        if (Itemp.parent_id == "00000000-0000-0000-0000-000000000000")
                        {
                            //Add the node with text to treeview1
                            treeView1.Nodes.Add(Itemp.item_id, Itemp.name, imageindex);
                            //Add a tag for xml Element creation
                            treeView1.Nodes[countindex].Tag = Itemp.inv_type;
                        }
                        else//Then it must have a parent...
                        {
                            //Add the child node
                            InsertUnderParent(Itemp.parent_id, Itemp.item_id, Itemp.name, imageindex, Itemp.inv_type, treeView1.Nodes);
                        }
                    }
                    countindex = countindex + 1;
                }

                MIUtils.INV_TYPE Invtype;

                //Let's get the Avatar's UUID, using 'Photo Album' 'My Inventory' is sometimes blank
                try
                {
                    //Let's see if online to get user id
                    if (client.Network.Connected)
                    {
                        userkey = client.Self.ID.UUID.ToString();
                    }
                    else
                        userkey = ((Folder)MIUtils.GetMyInventoryObjectByText("Photo Album", out Invtype)).owner_id;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("CreateTreeView throw: Could not find 'Photo Album' folder to get avatar's uuid.\r\nTry importing your inventory using SL Connect feature.\r\n" + ex.Message, "Could not find 'Photo Album' Folder", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                // Reset the cursor to the default for all controls.
                Cursor.Current = Cursors.Default;

                // Begin repainting the TreeView.
                treeView1.Sort();//Re-arange the treeView to look like SL's no matter how the xml or inv orders it
                treeView1.EndUpdate();
                treeView1.Update();

                //Make sure the search textbox is cleared
                tbSearchbox.Clear();
                label1.Text = "My Inventory:";

            }
            catch (Exception ex)
            {
                MessageBox.Show("CreateTreeView throw: " + ex.Message);
            }
        }

        /// <summary>
        /// Clear the treeView1 and all things associated with it.
        /// </summary>
        private void ClearTreeView()
        {
            tbOutput.Clear();
            MIUtils.OD_treeindex.Clear();
            SColl.Clear();
            treeLookup.Clear();
            this.treeView1.Nodes.Clear();
            this.treeView1.Refresh();
        }

        /// <summary>
        /// Update all label values using values from the current SL Connection 
        /// </summary>
        private void RefreshSLValues()
        {
            if (netcom != null)
            {
                if (netcom.IsLoggedIn)
                {
                    labelSLConnection.Text = "Connected";
                    labelSLConnection.ForeColor = Color.Green;
                    tssSLConn.Text = "Connected";
                    tssAvatar.Text = client.Self.Name;
                    //since we are logged in, let's make sure uuid is assigned correctly
                    userkey = client.Self.ID.ToStringHyphenated();
                    tssUUID.Text = userkey;

                    btConnect.Enabled = false;//Make sure it doesn't get enabled by events by accident
                    //Make sure Quit button is enabled
                    btQuit.Enabled = true;

                    //Disable the login txtboxes
                    tbFirstName.Enabled = false;
                    tbLastName.Enabled = false;
                    tbPassword.Enabled = false;
                    cbxLocation.Enabled = false;
                    chkbStayConnected.Enabled = false;
                    tbLoginUri.Enabled = false;
                    cbxGrid.Enabled = false;

                    connecttosl = true;

                    lbAvatarName.Text = "Avatar Name: " + netcom.LoginOptions.FullName;
                    lbCurrentSim.Text = "Current Sim: " + client.Network.CurrentSim.Name;
                    lbLindenDollars.Text = String.Format("{0:C}", client.Self.Balance).Replace("$", "L$: ");
                }
                else
                {
                    //labelSLConnection.Text = "Not Connected";
                    tssSLConn.Text = "Not Connected";

                    btConnect.Enabled = true;
                    //Make sure Quit button is enabled
                    btQuit.Enabled = false;

                    //Disable the login txtboxes
                    tbFirstName.Enabled = true;
                    tbLastName.Enabled = true;
                    tbPassword.Enabled = true;
                    cbxLocation.Enabled = true;
                    chkbStayConnected.Enabled = true;
                    tbLoginUri.Enabled = true;
                    cbxGrid.Enabled = true;
                }
            }
            else
            {
                //labelSLConnection.Text = "Not Connected";
                tssSLConn.Text = "Not Connected";

                btConnect.Enabled = true;
                //Make sure Quit button is enabled
                btQuit.Enabled = false;

                //Disable the login txtboxes
                tbFirstName.Enabled = true;
                tbLastName.Enabled = true;
                tbPassword.Enabled = true;
                cbxLocation.Enabled = true;
                chkbStayConnected.Enabled = true;
                tbLoginUri.Enabled = true;
                cbxGrid.Enabled = true;
            }

            if (treeView1.Nodes.Count > 0)
            {
                btSearch.Enabled = true;
                tbSearchbox.Enabled = true;
                cbSearchbyCreatorkey.Enabled = true;
                btExportXML.Enabled = true;
                toXToolStripMenuItem.Enabled = true;
                treeView1.Enabled = true;
                reloadOriginalToolStripMenuItem.Enabled = true;
                sendSearchToLogToolStripMenuItem.Enabled = true;
                createWordHitListToolStripMenuItem.Enabled = true;
                cbFilterCopy.Enabled = true;
                cbFilterModify.Enabled = true;
                cbFilterMove.Enabled = true;
                cbFilterTransfer.Enabled = true;
                btnShowFilter.Enabled = true;
            }
            else
            {
                btSearch.Enabled = false;
                tbSearchbox.Enabled = false;
                cbSearchbyCreatorkey.Enabled = false;
                btExportXML.Enabled = false;
                toXToolStripMenuItem.Enabled = false;
                treeView1.Enabled = false;
                reloadOriginalToolStripMenuItem.Enabled = false;
                sendSearchToLogToolStripMenuItem.Enabled = false;
                createWordHitListToolStripMenuItem.Enabled = false;
                cbFilterCopy.Enabled = false;
                cbFilterModify.Enabled = false;
                cbFilterMove.Enabled = false;
                cbFilterTransfer.Enabled = false;
                btnShowFilter.Enabled = false;
            }
        }

        /// <summary>
        /// Recursive method to add nodes to treeView1 using  XmlNode and TreeNode
        /// </summary>
        /// <param name="inXmlNode">XmlNode</param>
        /// <param name="inTreeNode">TreeNode</param>
        private void AddNode(XmlNode inXmlNode, TreeNode inTreeNode)
        {
            XmlNode xNode;
            TreeNode tNode;
            XmlNodeList nodeList;
            int i;

            // Loop through the XML nodes until the leaf is reached.
            // Add the nodes to the TreeView during the looping process.
            if (inXmlNode.HasChildNodes)
            {
                nodeList = inXmlNode.ChildNodes;
                for (i = 0; i <= nodeList.Count - 1; i++)
                {
                    xNode = inXmlNode.ChildNodes[i];
                    //inTreeNode.Nodes.Add(new TreeNode(xNode.Name));//creates just "category" as element

                    //create a treenode from name value in xml
                    string inv_name = xNode.Attributes["name"].Value;
                    inTreeNode.Nodes.Add(inv_name);
                    tNode = inTreeNode.Nodes[i];
                    AddNode(xNode, tNode);

                    //progressbar should have had maximum previously set
                    progressbar.Step("Loading " + inv_name + "\r\nfrom " + xmlfilename.Split('\\')[xmlfilename.Split('\\').Length - 1], 0);
                }
            }
            else
            {
                // We have reached the end of the xml path (last XmlNode child)
                ///inTreeNode.Text = (inXmlNode.OuterXml).Trim();
                inTreeNode.Text = inXmlNode.Attributes["name"].Value;
            }

        }

        /// <summary>
        /// Adds a Folder line to the temp .inv file
        /// </summary>
        /// <param name="cat_id"></param>
        /// <param name="parent_id"></param>
        /// <param name="type"></param>
        /// <param name="pref_type"></param>
        /// <param name="name"></param>
        /// <param name="owner_id"></param>
        /// <param name="version"></param>
        private void AddFolderNodetoSB(string cat_id, string parent_id, string type, string pref_type, string name, string owner_id, string version)
        {
            string line = "\tinv_category\t0\n\t{\n\t\tcat_id\t" + cat_id + "\n\t\tparent_id\t" + parent_id +
                "\n\t\ttype\t" + type + "\n\t\tpref_type\t" + pref_type + "\n\t\tname\t" + name + "|\n\t\towner_id\t" + owner_id +
                "\n\t\tversion\t" + version + "\n\t}\n";

            //Utils.MIUtils.SB_MyInventory.AppendLine(line);
            File.AppendAllText(Common.cur_invFile, line);
        }

        /// <summary>
        /// Adds an Item line to the temp .inv file
        /// </summary>
        /// <param name="item_id"></param>
        /// <param name="parent_id"></param>
        /// <param name="p_base_mask"></param>
        /// <param name="p_owner_mask"></param>
        /// <param name="p_group_mask"></param>
        /// <param name="p_everyone_mask"></param>
        /// <param name="p_next_owner_mask"></param>
        /// <param name="p_creator_id"></param>
        /// <param name="p_owner_id"></param>
        /// <param name="p_last_owner_id"></param>
        /// <param name="p_group_id"></param>
        /// <param name="shadow_id"></param>
        /// <param name="type"></param>
        /// <param name="inv_type"></param>
        /// <param name="flags"></param>
        /// <param name="s_sale_type"></param>
        /// <param name="s_sale_price"></param>
        /// <param name="name"></param>
        /// <param name="desc"></param>
        /// <param name="creation_date"></param>
        private void AddItemNodetoSB(string item_id, string parent_id, string p_base_mask, string p_owner_mask, string p_group_mask, string p_everyone_mask,
            string p_next_owner_mask, string p_creator_id, string p_owner_id, string p_last_owner_id, string p_group_id, string shadow_id, string type,
            string inv_type, string flags, string s_sale_type, string s_sale_price, string name, string desc, string creation_date)
        {
            string line = "\tinv_item\t0\n\t{\n\t\titem_id\t" + item_id +
                "\n\t\tparent_id\t" + parent_id +
                "\n\tpermissions 0\n\t{\n\t\tbase_mask\t" + p_base_mask +
                "\n\t\towner_mask\t" + p_owner_mask + "\n\t\tgroup_mask\t" + p_group_mask + "\n\t\teveryone_mask\t" + p_everyone_mask +
                "\n\t\tnext_owner_mask\t" + p_next_owner_mask +
                "\n\t\tcreator_id\t" + p_creator_id +
                "\n\t\towner_id\t" + p_owner_id +
                "\n\t\tlast_owner_id\t" + p_last_owner_id +
                "\n\t\tgroup_id\t" + p_group_id +
                "\n\t\tnext_owner_mask\t" + p_next_owner_mask + "\n\t}\n\t\tshadow_id\t" + shadow_id +
                "\n\t\ttype\t" + type.ToLower() +
                "\n\t\tinv_type\t" + inv_type.ToLower() +
                "\n\t\tflags\t" + flags +
                "\n\tsale_info\t0\n\t{\n\t\tsale_type\t" + s_sale_type +
                "\n\t\tsale_price\t" + s_sale_price +
                "\n\t}\n\t\tname\t" + name +
                "|\n\t\tdesc\t" + desc +
                "|\n\t\tcreation_date\t" + creation_date + "\n\t}\n";

            //Utils.MIUtils.SB_MyInventory.AppendLine(line);
            File.AppendAllText(Common.cur_invFile, line);
        }


        /// <summary>
        /// Quit button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btQuit_Click(object sender, EventArgs e)
        {
            this.labelSLConnection.Font = new System.Drawing.Font("Arial", 24F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            Disconnect();
        }

        /// <summary>
        /// Resfresh the textboxes for the logs
        /// </summary>
        private void UpdateLogTextboxes()
        {
            //Common.MainLog
            tbLog.Clear();
            tbLog.AppendText(File.ReadAllText(Common.MainLog));

            //Common.ImageLog
            tbImageLog.Clear();
            tbImageLog.AppendText(File.ReadAllText(Common.ImageLog));

            //Common.SLConnectLog
            tbSLConLog.Clear();
            tbSLConLog.AppendText(File.ReadAllText(Common.SLConnectLog));
            //tbSLConLog.SelectionStart = tbSLConLog.Text.Length - 1;
        }

        /// <summary>
        /// Disconnect from SL
        /// </summary>
        private void Disconnect()
        {
            //Disable the Connection button
            btConnect.Enabled = true;

            labelSLConnection.Text = "Logging out...";
            labelSLConnection.ForeColor = Color.Black;
            labelSLConnection.Update();
            client.Log("[SLMIV] " + DateTime.Now.ToShortTimeString() + ": Logging out of SL...", Helpers.LogLevel.Info);

            //Shor the progressbar
            pBarConnection.Visible = true;
            pBarConnection.Focus();
            this.Update();

            netcom.Logout();

            while (netcom.IsLoggedIn)
            {
                this.Update();
            }

            labelSLConnection.Text = "Not Connected";
            labelSLConnection.ForeColor = Color.Black;
            labelSLConnection.Update();

            pBarConnection.Visible = false;

            //Make sure Quit button is disabled
            btQuit.Enabled = false;

            //Refresh all the SL values and their labels and buttons
            RefreshSLValues();

            this.Update();

            connecttosl = false;
        }

        /// <summary>
        /// Get the avatar's key, Create all inventory variables and treeView1, RefreshSLValues
        /// </summary>
        private void DoSLInventory()
        {
            Cursor.Current = Cursors.WaitCursor;

            //Get the avatar's key
            userkey = client.Self.ID.ToStringHyphenated();

            //Get the SL Connection's root inventory
            GetSLInventoryTreeRoot();

            pBarConnection.Visible = true;

            //Expand all to fire the after_expand event method to populate all child nodes
            treeView1.Nodes[0].ExpandAll();

            pBarConnection.Visible = false;

            //Read in the .inv into a StringBuilder...
            Utils.MIUtils.SB_MyInventory.AppendLine(File.ReadAllText(Common.cur_invFile));

            //Take the SB and seperate it into a StringCollection...
            Utils.MIUtils.SplitintoSC(Utils.MIUtils.SB_MyInventory);

            //Convert each line in the SC in a Folder or Item object, and place into the main MultiDictionary
            Utils.MIUtils.ConvertToOD(Utils.MIUtils.SC_MyInvInBrackets);

            //The main contents Dictionaries are now created so lets create the treeView1 the SLMIV standard way
            CreateTreeView();//calls treeView1.EndUpdate();etc


            //Check to see if we are disconnecting
            if (!chkbStayConnected.Checked)
            {
                Disconnect();//Quit calls RefreshSLValues()
            }
            else
            {
                //Refresh all the SL values and their labels and buttons
                RefreshSLValues();
            }

            this.Focus();
            this.Update();

            //Ok, let's see what we got
            labelSLConnection.Text = "Counting Inventory...";
            client.Log("[SLMIV] " + DateTime.Now.ToShortTimeString() + ": Counting Inventory...", Helpers.LogLevel.Info);
            Thread.Sleep(2000);//Help show status

            //Let's add the Photo Album folder
            MIUtils.INV_TYPE type;//Should be Folder
            Object founditem = MIUtils.GetMyInventoryObjectByText("Photo Album", out type);
            if (type == MIUtils.INV_TYPE.FOLDER)
            {
                PhotoAlbumUUID = LLUUID.Parse(((Folder)founditem).cat_id);
            }
            IICountsDict.Clear();
            IICountsDict = SLMIV.Utils.MIUtils.CountInventoryItems(this.libsecItemDict, PhotoAlbumUUID);

            labelSLConnection.Text = "Finished Counting Inventory";
            client.Log("[SLMIV] " + DateTime.Now.ToShortTimeString() + ": Finished Counting Inventory", Helpers.LogLevel.Info);
            client.Log("[SLMIV] " + DateTime.Now.ToShortTimeString() + ": Creating SLMIV Inventory object tree...", Helpers.LogLevel.Info);
            treeView1.Nodes[0].Expand();//let's see the subfolders
            this.Update();
            Cursor.Current = Cursors.Default;
            client.Log("[SLMIV] " + DateTime.Now.ToShortTimeString() + ": Finished SLMIV Inventory object tree...", Helpers.LogLevel.Info);

            //Go to the MyInventory tab
            tabControl1.SelectedTab = tabControl1.TabPages["tabMyInv"];
        }


        /// <summary>
        /// Displays and Refreshes the InvCounts and EstimatedTime on the tabBackup tab
        /// </summary>
        private void DisplayInvCountsEstimatedTime()
        {
            //Display inventory counts
            if (this.IICountsDict.Count > 0)
            {
                int Notecardcount = 0;
                IICountsDict.TryGetValue(InventoryType.Notecard, out Notecardcount);
                chkNotecards.Text = "Notecards [QTY: " + Convert.ToString(Notecardcount) + "]";

                int PhotoAlbumcount = 0;
                IICountsDict.TryGetValue(InventoryType.Snapshot, out PhotoAlbumcount);
                chkPhotoAlbum.Text = "Photo Album [QTY: " + Convert.ToString(PhotoAlbumcount) + "]";

                int LSLcount = 0;
                IICountsDict.TryGetValue(InventoryType.LSL, out LSLcount);
                chkScripts.Text = "Scripts/LSL [QTY: " + Convert.ToString(LSLcount) + "]";

                int Texturecount = 0;
                IICountsDict.TryGetValue(InventoryType.Texture, out Texturecount);
                chkTextures.Text = "Textures [QTY: " + Convert.ToString(Texturecount) + "]";

                int Totalcount = 0;
                if (chkNotecards.Checked)
                    Totalcount = Notecardcount;
                if (chkPhotoAlbum.Checked)
                    Totalcount = Totalcount + PhotoAlbumcount;
                if (chkScripts.Checked)
                    Totalcount = Totalcount + LSLcount;
                if (chkTextures.Checked)
                    Totalcount = Totalcount + Texturecount;

                Totalcount = ((Totalcount * 2) / 60);//Each item needs a 2 sec Sleep, so convert est. secs to minutes
                //Images have an extra 2sec each so add those extra seconds
                int extra_sec = 0;
                if (chkPhotoAlbum.Checked)
                    extra_sec = ((PhotoAlbumcount * 3) / 60);
                if (chkTextures.Checked)
                    extra_sec = extra_sec + ((Texturecount * 3) / 60);

                Totalcount = Totalcount + extra_sec;

                if (Totalcount < 1)
                    lbEstTime.Text = "Estimated time for backup is less than 1 minute.";
                else
                    lbEstTime.Text = "Estimated time for backup is " + Convert.ToString(Totalcount) + " minutes.";
            }
        }
        #endregion Misc

        #region SLComm methods
        private void AddNetcomEvents()
        {
            netcom.ClientLoggingIn += new EventHandler<OverrideEventArgs>(netcom_ClientLoggingIn);
            netcom.ClientLoginStatus += new EventHandler<ClientLoginEventArgs>(netcom_ClientLoginStatus);
            netcom.ClientLoggedOut += new EventHandler(netcom_ClientLoggedOut);
            netcom.ClientDisconnected += new EventHandler<ClientDisconnectEventArgs>(netcom_ClientDisconnected);
        }

        /// <summary>
        /// Handle SL Connection success and failures
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void netcom_ClientLoginStatus(object sender, ClientLoginEventArgs e)
        {
            switch (e.Status)
            {
                case NetworkManager.LoginStatus.Success:
                    pBarConnection.Visible = false;
                    //Refresh all the SL values and their labels and buttons
                    RefreshSLValues();//Must refresh while logged in to update labels
                    DoSLInventory();
                    break;

                case NetworkManager.LoginStatus.Failed:
                    pBarConnection.Visible = false;
                    labelSLConnection.Text = e.Message;
                    client.Log("[SLMIV] " + DateTime.Now.ToShortTimeString() + e.Message, Helpers.LogLevel.Error);
                    this.labelSLConnection.Font = new System.Drawing.Font("Arial", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
                    labelSLConnection.ForeColor = Color.Red;
                    break;
            }

            //Refresh all the SL values and their labels and buttons
            RefreshSLValues();
        }

        private void netcom_ClientLoggingIn(object sender, OverrideEventArgs e)
        {
            //Show the progressbar
            this.Focus();
            this.Update();
            pBarConnection.Focus();
            pBarConnection.Visible = true;
            pBarConnection.Update();
        }

        private void netcom_ClientLoggedOut(object sender, EventArgs e)
        {
            labelSLConnection.Text = "Not Connected";
            labelSLConnection.ForeColor = Color.Black;
        }

        private void netcom_ClientDisconnected(object sender, ClientDisconnectEventArgs e)
        {
            if (e.Type == NetworkManager.DisconnectType.ClientInitiated) return;

            //(new SLMIV.Utils.Forms.frmDisconnected(e)).ShowDialog();
            MessageBox.Show(e.Message, "Lost Connection...", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        #endregion SLComm methods

        #region SLNetConn Inventory methods
        private void treeView1_AfterExpand(object sender, TreeViewEventArgs e)
        {
            try
            {
                this.Focus();
                this.pBarConnection.BringToFront();
                this.pBarConnection.Refresh();
                this.Update();
                if ((netcom != null))
                {
                    if (netcom.IsLoggedIn)
                    {
                        if (e.Node.Nodes[0].Tag == null)
                        {
                            //BUG:folder.UUID is null on a search
                            InventoryFolder folder = (InventoryFolder)e.Node.Tag;
                            if (folder.UUID != null)
                            {
                                client.Inventory.RequestFolderContents(folder.UUID, client.Self.ID, true, true, false, InventorySortOrder.ByName);
                                ProcessIncomingObject(folder);
                            }
                        }

                        e.Node.ImageKey = "OpenFolder";
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("treeView1_AfterExpand Error: " + ex.Message + Environment.NewLine + ex.StackTrace);//Debug
                //MessageBox.Show("treeView1_AfterExpand Error: " + ex.Message);//I added this try to this version but exceptions are happening but still displays search results
            }

        }

        /// <summary>
        /// Clears the current treeView1 then populates the treeview using SL connection
        /// </summary>
        private void GetSLInventoryTreeRoot()
        {
            ClearTreeView();

            InventoryFolder rootFolder = client.Inventory.Store.RootFolder;
            TreeNode rootNode = treeView1.Nodes.Add(rootFolder.UUID.ToString(), "My Inventory");
            rootNode.Tag = rootFolder;
            rootNode.ImageKey = "ClosedFolder";

            treeLookup.Add(rootFolder.UUID, rootNode);
            //Starting new Inventory so create temp file for SLMIVUtils
            //This file will be used in btConnect to create Utils.MIUtils.SB_MyInventory
            ///Common.cur_invFile = Common.invFolder + "\\" + DateTime.Now.Ticks.ToString() + ".inv";
            Common.cur_invFile = Common.invFolder + "\\" + userkey + ".inv";
            File.WriteAllText(Common.cur_invFile, "");

            //Add to the .inv file
            AddFolderNodetoSB(rootFolder.UUID.ToStringHyphenated(), rootFolder.ParentUUID.ToStringHyphenated(), "category", rootFolder.PreferredType.ToString(), "My Inventory", rootFolder.OwnerID.ToStringHyphenated(), Convert.ToString(rootFolder.Version));

            //Triggers treeView1's AfterExpand event, thus triggering the root content request
            rootNode.Nodes.Add("Requesting folder contents...");
            rootNode.Expand();
        }

        //Separate thread
        private void Inventory_OnInventoryFolderUpdated(LLUUID folderID)
        {
            BeginInvoke(
                new InventoryManager.InventoryFolderUpdated(FolderDownloadFinished),
                new object[] { folderID });
        }

        //UI thread
        private void FolderDownloadFinished(LLUUID folderID)
        {
            InventoryBase invObj = client.Inventory.Store[folderID];
            ProcessIncomingObject(invObj);
        }

        //Separate thread
        private bool Inventory_OnInventoryObjectReceived(LLUUID fromAgentID, string fromAgentName, uint parentEstateID, LLUUID regionID, LLVector3 position, DateTime timestamp, AssetType type, LLUUID objectID, bool fromTask)
        {
            BeginInvoke(
                new InventoryManager.InventoryObjectReceived(ReceivedInventoryObject),
                new object[] { fromAgentID, fromAgentName, parentEstateID, regionID, position, timestamp, type, objectID, fromTask });

            return true;
        }

        //UI thread
        private bool ReceivedInventoryObject(LLUUID fromAgentID, string fromAgentName, uint parentEstateID, LLUUID regionID, LLVector3 position, DateTime timestamp, AssetType type, LLUUID objectID, bool fromTask)
        {
            InventoryBase invObj = client.Inventory.Store[objectID];
            ProcessIncomingObject(invObj);

            return true;
        }

        /// <summary>
        /// Occurs when treeView1_AfterExpand is called.
        /// Processes an InventoryBase object into the treeView1
        /// </summary>
        /// <param name="io"></param>
        private void ProcessIncomingObject(InventoryBase io)
        {
            if (io is InventoryFolder)
            {
                InventoryFolder folder = (InventoryFolder)io;
                TreeNode node = treeLookup[folder.UUID];

                treeView1.BeginUpdate();
                node.Nodes.Clear();

                List<InventoryBase> folderContents = client.Inventory.Store.GetContents(folder);
                if (folderContents.Count > 0)
                {
                    ProcessInventoryItems(folderContents, node);
                    //treeView1.Sort();

                    if (!node.IsVisible)
                    {
                        node.LastNode.EnsureVisible();
                        node.EnsureVisible();
                    }
                }

                treeView1.EndUpdate();
            }
            else if (io is InventoryItem)
            {
                InventoryItem item = (InventoryItem)io;
                TreeNode node = treeLookup[item.ParentUUID];

                treeView1.BeginUpdate();

                TreeNode itemNode = AddTreeItem(item, node);
                //treeView1.Sort();

                if (!itemNode.IsVisible)
                {
                    if (node.IsExpanded)
                    {
                        node.LastNode.EnsureVisible();
                        itemNode.EnsureVisible();
                    }
                }

                treeView1.EndUpdate();
            }
        }

        private TreeNode AddTreeFolder(InventoryFolder folder, TreeNode node)
        {
            if (treeLookup.ContainsKey(folder.UUID))
                return treeLookup[folder.UUID];

            TreeNode folderNode = node.Nodes.Add(folder.UUID.ToString(), folder.Name);
            folderNode.Tag = folder;
            folderNode.ImageKey = "ClosedFolder";

            treeLookup.Add(folder.UUID, folderNode);
            return folderNode;
        }

        /// <summary>
        /// Searches treeLookup for InventoryItem.UUID and returns its <LLUUID, TreeNode> object
        /// if it is not found, it adds <UUID string, item.Name> to incoming TreeNode node
        /// then node to treeLookup
        /// </summary>
        /// <param name="item">InventoryItem</param>
        /// <param name="node">TreeNode</param>
        /// <returns>TreeNode with added <UUID string, item.Name> item</returns>
        private TreeNode AddTreeItem(InventoryItem item, TreeNode node)
        {
            if (treeLookup.ContainsKey(item.UUID))
                return treeLookup[item.UUID];

            TreeNode itemNode = node.Nodes.Add(item.UUID.ToString(), item.Name);
            itemNode.Tag = item;

            switch (item.InventoryType)
            {
                case InventoryType.Wearable:
                    itemNode.ImageKey = "Gear"; //TODO: use "clothing" key instead
                    break;

                case InventoryType.Notecard:
                    itemNode.ImageKey = "Notecard";
                    break;

                case InventoryType.LSL:
                    itemNode.ImageKey = "Script";
                    break;

                case InventoryType.Texture:
                    itemNode.ImageKey = "Gear"; //TODO: use "image" key instead
                    break;

                default:
                    itemNode.ImageKey = "Gear";
                    break;
            }

            treeLookup.Add(item.UUID, itemNode);
            return itemNode;
        }

        //Recursive!
        private void ProcessInventoryItems(List<InventoryBase> list, TreeNode node)
        {
            if (list == null) return;

            foreach (InventoryBase item in list)
            {
                if (item is InventoryFolder)
                {
                    InventoryFolder folder = (InventoryFolder)item;
                    TreeNode folderNode = AddTreeFolder(folder, node);

                    List<InventoryBase> contents = client.Inventory.Store.GetContents(folder);
                    if (contents.Count > 0)
                    {
                        ProcessInventoryItems(contents, folderNode);
                    }
                    else
                    {
                        folderNode.Nodes.Add("Requesting folder contents...");
                    }
                    AddFolderNodetoSB(folder.UUID.ToStringHyphenated(), folder.ParentUUID.ToStringHyphenated(), "category", folder.PreferredType.ToString(), folder.Name, folder.OwnerID.ToStringHyphenated(), Convert.ToString(folder.Version));
                }
                else if (item is InventoryItem)
                {
                    //In order to retreive items, we need the libsecondlife objects on-hand //eg 3e19c6b4-5a3d-7f48-8080-79f518953e3f
                    libsecItemDict.Add(((InventoryItem)item).UUID, ((InventoryItem)item));

                    AddTreeItem((InventoryItem)item, node);
                    {
                        AddItemNodetoSB(((InventoryItem)item).UUID.ToStringHyphenated(),// item_id
                        ((InventoryItem)item).ParentUUID.ToStringHyphenated(),// parent_id

                        ((InventoryItem)item).Permissions.BaseMask.ToString(),//base_mask	
                        ((InventoryItem)item).Permissions.OwnerMask.ToString(),//owner_mask	
                        ((InventoryItem)item).Permissions.GroupMask.ToString(),//group_mask	
                        ((InventoryItem)item).Permissions.EveryoneMask.ToString(),//everyone_mask	
                        ((InventoryItem)item).Permissions.NextOwnerMask.ToString(),//next_owner_mask	
                        ((InventoryItem)item).CreatorID.ToStringHyphenated(),//creator_id	
                        ((InventoryItem)item).OwnerID.ToStringHyphenated(),//owner_id	
                        "",//last_owner_id	
                        ((InventoryItem)item).GroupID.ToStringHyphenated(),//group_id
                        "unknown",
                        ((InventoryItem)item).AssetType.ToString(),
                        ((InventoryItem)item).InventoryType.ToString(),
                        ((InventoryItem)item).Flags.ToString(),
                        ((InventoryItem)item).SaleType.ToString(),
                        ((InventoryItem)item).SalePrice.ToString(),
                        ((InventoryItem)item).Name,
                        ((InventoryItem)item).Description,
                        ((InventoryItem)item).CreationDate.ToUniversalTime().ToString());
                    }
                }
            }
        }

        private void AddNewFolder(string folderName, TreeNode node)
        {
            if (node == null) return;

            InventoryFolder folder = null;
            TreeNode folderNode = null;

            if (node.Tag is InventoryFolder)
            {
                folder = (InventoryFolder)node.Tag;
                folderNode = node;
            }
            else if (node.Tag is InventoryItem)
            {
                folder = (InventoryFolder)node.Parent.Tag;
                folderNode = node.Parent;
            }

            treeView1.BeginUpdate();

            LLUUID newFolderID = client.Inventory.CreateFolder(folder.UUID, AssetType.Folder, folderName);
            InventoryFolder newFolder = (InventoryFolder)client.Inventory.Store[newFolderID];
            TreeNode newNode = AddTreeFolder(newFolder, folderNode);

            //treeView1.Sort();
            treeView1.EndUpdate();
        }

        /// <summary>
        /// Append string to local troubleshooting log for SL Connection
        /// </summary>
        /// <param name="entry">string</param>
        private void client_OnLogMessage(string entry, Helpers.LogLevel level)
        {
            //"No handler registered for CAPS event ParcelProperties"
            if (!entry.Contains("No handler registered for CAPS event EnableSimulator"))
            {
                if (!entry.Contains("No handler registered for packet event AvatarAppearance"))
                {
                    File.AppendAllText(Common.SLConnectLog, entry + Environment.NewLine);
                }
            }
        }

        /// <summary>
        /// txbStatusHis text was updated
        /// </summary>
        /// <param name="sender">object</param>
        /// <param name="e">EventArgs</param>
        private void txbStatusHis_TextChanged(object sender, EventArgs e)
        {
            //stop the freezing look
            this.Focus();
            this.txbStatusHis.BringToFront();
            this.txbStatusHis.Refresh();
            this.BringToFront();
            this.Refresh();
            this.Update();
        }
        #endregion SLNetConn Inventory methods

        #region Importing-Exporting Methods
        /// <summary>
        /// Browse to the .inv file, Create the TreeView like 1.7, then converts treeView1 to xml
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btBrowse_Click(object sender, EventArgs e)
        {
            try
            {
                Cursor.Current = Cursors.WaitCursor;

                int browseresult = SLMIV.Utils.MIUtils.BrowsetoInvFile(out userkey);

                if (browseresult != -1)
                {
                    Utils.Forms.ProgressbarForm pBar = new SLMIV.Utils.Forms.ProgressbarForm();
                    pBar.MaximumSteps = 2;
                    pBar.Show();
                    pBar.Step("Creating Inventory...", 0);

                    //Create the TreeView like 1.7
                    CreateTreeView();

                    pBar.Dispose();

                    //Refresh all the SL values and their labels and buttons
                    RefreshSLValues();

                    //Go to the MyInventory tab
                    tabControl1.SelectedTab = tabControl1.TabPages["tabMyInv"];
                }

                Cursor.Current = Cursors.Default;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        /// <summary>
        /// Import a well-formed SLMIV xml file
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btImportXML_Click(object sender, EventArgs e)
        {
            try
            {
                OpenFileDialog ofd = new OpenFileDialog();
                ofd.Filter = "xml files (*.xml)|*.xml|All files (*.*)|*.*";
                ofd.InitialDirectory = Common.SLMIVPath;
                ofd.ShowDialog();

                if (ofd.FileName != string.Empty)
                {
                    // Assign the global for AddNode progressbar
                    xmlfilename = ofd.FileName;

                    Utils.MIUtils.CreateIndexesFromXML(xmlfilename);

                    //The main contents Dictionaries are now created so lets create the treeView1 the SLMIV standard way
                    CreateTreeView();//calls treeView1.EndUpdate();etc

                    progressbar.Dispose();

                    // Reset the cursor to the default for all controls.
                    Cursor.Current = Cursors.Default;

                    //Refresh all the SL values and their labels and buttons
                    RefreshSLValues();

                    //Go to the MyInventory tab
                    tabControl1.SelectedTab = tabControl1.TabPages["tabMyInv"];
                }
            }
            catch (XmlException xmlEx)
            {
                MessageBox.Show(xmlEx.Message);
                progressbar.Dispose();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                progressbar.Dispose();
            }
        }

        private void BeginLogin()
        {
            ///Next do BeginLogin() stuff...
            //Figure out where to start from reading the start location
            switch (cbxLocation.SelectedIndex)
            {
                case -1: //Custom
                    netcom.LoginOptions.StartLocation = StartLocationType.Custom;
                    netcom.LoginOptions.StartLocationCustom = cbxLocation.Text;
                    break;

                case 0: //Home
                    netcom.LoginOptions.StartLocation = StartLocationType.Home;
                    break;

                case 1: //Last
                    netcom.LoginOptions.StartLocation = StartLocationType.Last;
                    break;
            }

            //Which Grid?
            if (cbxGrid.SelectedIndex == 0)
            {
                client.Settings.LOGIN_SERVER = Common.GridMainLogin;
            }
            else if (cbxGrid.SelectedIndex == 1)
            {
                client.Settings.LOGIN_SERVER = Common.GridBetaLogin;
            }
            else if (cbxGrid.SelectedIndex == 2)
            {
                //Custom Grid
                client.Settings.LOGIN_SERVER = tbLoginUri.Text.Trim();
            }

            //Begin login
            netcom.Login();
            ///END of BeginLogin() stuff
        }

        /// <summary>
        /// Connect to SL
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btConnect_Click(object sender, EventArgs e)
        {
            //Create the new slconnectlog.txt
            FileStream fstream = File.Create(Common.SLConnectLog);
            fstream.Close();

            this.labelSLConnection.Font = new System.Drawing.Font("Arial", 24F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));

            if (treeView1.Nodes.Count > 0)
            {
                //SLMIV can not currently do two connections in a row
                //Prompt the user if they are sure they want to Restart SLMIV
                DialogResult dResult = MessageBox.Show("You are trying to connect online when you already have inventory loaded. In order to proceed, SLMIV must restart.\r\n\r\nThis will erase all of your current logs and inventory tree in SLMIV only.\r\nAre you sure you want to Restart SLMIV?", "Restart SLMIV", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (dResult == DialogResult.Yes)
                {
                    Application.Restart();
                }
                else if (dResult == DialogResult.No)
                {

                }
                else if (dResult == DialogResult.Cancel)
                {

                }
            }
            else
            {
                Cursor.Current = Cursors.WaitCursor;
                //ClearTreeView the treeView1
                ClearTreeView();

                //Go to the MyInventory tab and back
                // for some reason the treeview1 will not get populated if not shown first.
                tabControl1.SelectedTab = tabControl1.TabPages["tabMyInv"];
                tabControl1.SelectedTab = tabControl1.TabPages["tabSLCom"];

                labelSLConnection.Text = "Logging in...";
                labelSLConnection.ForeColor = Color.Black;
                labelSLConnection.Update();

                client.Log("[SLMIV] " + DateTime.Now.ToShortTimeString() + ": Logging into SL...", Helpers.LogLevel.Info);

                //Disable the Connection button
                btConnect.Enabled = false;

                ///next does InitializeConfig() stuff...
                //Set the current login information from the settings file
                SLLoginSettings.Default.FirstName = tbFirstName.Text;
                SLLoginSettings.Default.LastName = tbLastName.Text;
                SLLoginSettings.Default.Password = tbPassword.Text;
                SLLoginSettings.Default.LastLocation = cbxLocation.SelectedText;
                SLLoginSettings.Default.Save();

                //Pass the login info to the SLNetCom object
                netcom.LoginOptions.FirstName = SLLoginSettings.Default.FirstName;
                netcom.LoginOptions.LastName = SLLoginSettings.Default.LastName;
                netcom.LoginOptions.Password = SLLoginSettings.Default.Password;
                netcom.LoginOptions.Grid = LoginGrid.MainGrid;//Always go to the Main Grid
                netcom.LoginOptions.IsPasswordMD5 = true;
                ///END InitializeConfig() stuff

                //Let's just make sure, and keep a record that libsecItemDict needs to be cleared. 
                libsecItemDict.Clear();

                BeginLogin();//must break here to allow SLNetCom events
                //netcom_ClientLoginStatus is then fired, which if succesfull, DoSLInventory() is called

                Cursor.Current = Cursors.Default;
            }

        }

        /// <summary>
        /// Export the current treeView to well formed SLMIV xml 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btExportXML_Click(object sender, EventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "xml files (*.xml)|*.xml|All files (*.*)|*.*";
            sfd.InitialDirectory = Common.SLMIVPath;
            sfd.ShowDialog();

            if (sfd.FileName != string.Empty)
            {
                if (treeView1.Nodes.Count > 0)
                {
                    //Lets convert treeView1 to xml
                    Utils.MIUtils.TreeViewToXML(treeView1, sfd.FileName);
                    MessageBox.Show("Export to xml successfull!");
                }
                else
                {
                    MessageBox.Show("Currently no inventory loaded in treeView box. Please load an inventory before exporting.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

        }

        /// <summary>
        /// Menu's ExportXML
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toXToolStripMenuItem_Click(object sender, EventArgs e)
        {
            btExportXML_Click(sender, e);
        }
        #endregion Importing-Exporting Methods

        #region treeView Methods
        /// <summary>
        /// Remove the specified TreeNode from treeView1.Nodes
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        public bool RemoveTreeViewNode(TreeNode node)
        {
            bool bRemoved = false;
            treeView1.BeginUpdate();
            try
            {
                bRemoved = doForwardSearchDelete(treeView1.Nodes, node.Name, bRemoved);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                bRemoved = doForwardSearchDelete(treeView1.Nodes, node.Name, bRemoved);
            }

            treeView1.EndUpdate();
            treeView1.Update();

            return bRemoved;
        }

        void doRecursionSearch(TreeNodeCollection t, string tofind)
        {
            foreach (TreeNode tn in t)
            {
                if (tn.Text.Contains(tofind))
                {
                    treeView1.Nodes.Add(tn);
                    doRecursionSearch(tn.Nodes, tofind);
                }
                else
                    doRecursionSearch(tn.Nodes, tofind);
            }
        }

        private void IterateTreeNodes(TreeNode originalNode, TreeNode rootNode)
        {
            foreach (TreeNode childNode in originalNode.Nodes)
            {
                TreeNode newNode = new TreeNode(childNode.Text);
                newNode.Tag = childNode.Tag;
                newNode.Name = childNode.Name;
                newNode.ImageIndex = childNode.ImageIndex;
                this.BackupTreeView.SelectedNode = rootNode;
                this.BackupTreeView.SelectedNode.Nodes.Add(newNode);
                IterateTreeNodes(childNode, newNode);
            }
        }

        /// <summary>
        /// Search and remove the node from treeView1.Nodes
        /// </summary>
        /// <param name="t"></param>
        /// <param name="tofind"></param>
        /// <param name="tnodecoll"></param>
        public bool doForwardSearchDelete(TreeNodeCollection t, string tofindkey, bool bresult)
        {
            foreach (TreeNode tn in t)
            {
                if (tn.Name == tofindkey)
                {
                    treeView1.Nodes.Remove(tn);
                    bresult = true;
                    break;
                }
                else
                {
                    if (doForwardSearchDelete(tn.Nodes, tofindkey, bresult))
                    {
                        //Since result is true, keep it as true to pass
                        bresult = true;
                        break;
                    }
                }
            }
            return bresult;
        }

        /// <summary>
        /// Creates a new treeView with only the desired nodes
        /// </summary>
        /// <param name="tofind">text string to find in Nodes.Text</param>
        public void doSelectedShow(string tofind)
        {
            int foundcount = 0;

            tofind = tofind.ToLower();
            //UPDATE THE TREEVIEW////////////////////////////////////////////////////////////////
            // Display a wait cursor while the TreeNodes are being created.
            Cursor.Current = Cursors.WaitCursor;

            // Suppress repainting the TreeView until all the objects have been created.
            treeView1.BeginUpdate();

            // Clear the TreeView each time the method is called.
            treeView1.Nodes.Clear();

            int countindex = 0;
            foreach (KeyValuePair<int, Object> pair in MIUtils.OD_MyInventory)
            {
                //If it is a folder, then add to TreeView...
                if (pair.Value.GetType().ToString() == "MyInventory.Folder")
                {
                    Folder ftemp = (Folder)pair.Value;

                    //What type of item is it?
                    int imageindex = Utils.MIUtils.Getinv_type(ftemp.type);

                    //If the folder is a root then add it
                    if (ftemp.parent_id == "00000000-0000-0000-0000-000000000000")
                    {
                        //Add the node with text to treeview1
                        treeView1.Nodes.Add(ftemp.cat_id, ftemp.name, imageindex);
                    }
                    else//Then it must have a parent folder...
                    {
                        //Add the child node
                        InsertUnderParent(ftemp.parent_id, ftemp.cat_id, ftemp.name, imageindex, ftemp.type, treeView1.Nodes);
                    }
                }
                else//Must be an Item
                {
                    Item Itemp = (Item)pair.Value;

                    //What type of item is it?
                    int imageindex = Utils.MIUtils.Getinv_type(Itemp.inv_type);

                    //If the Item is a root then add it
                    if (Itemp.parent_id == "00000000-0000-0000-0000-000000000000")
                    {
                        //Add the node with text to treeview1
                        treeView1.Nodes.Add(Itemp.item_id, Itemp.name, imageindex);
                    }
                    else//Then it must have a parent...
                    {
                        //There maybe a UIDKey in the searchbox, this will mean some special handling
                        if (cbSearchbyCreatorkey.Checked && Itemp.permission.creator_id.Contains(tofind))
                        {
                            //Add the child node
                            InsertUnderParent(Itemp.parent_id, Itemp.item_id, Itemp.name, imageindex, Itemp.inv_type, treeView1.Nodes);
                            foundcount = foundcount + 1;
                        }
                        else
                        {
                            if (Itemp.name.ToLower().Contains(tofind))//Case insensitive
                            {
                                //Add the child node
                                InsertUnderParent(Itemp.parent_id, Itemp.item_id, Itemp.name, imageindex, Itemp.inv_type, treeView1.Nodes);
                                foundcount = foundcount + 1;
                            }
                        }
                    }
                }
                countindex = countindex + 1;
            }
            // Reset the cursor to the default for all controls.
            Cursor.Current = Cursors.Default;
            treeView1.SelectedNode = treeView1.Nodes[0];//SelectectNode must not be null
            treeView1.ExpandAll();//This action triggers the event which creates the entire treeViewnode
            // Begin repainting the TreeView.
            //treeView1.Nodes[0].Expand();//Let's see My Inventory folder subfolders
            treeView1.EndUpdate();

            //UPDATE THE TREEVIEW - DONE////////////////////////////////////////////////////////////////
            this.label1.Text = "My Inventory: (" + Convert.ToString(foundcount) + " entries found)";
        }

        /// <summary>
        /// Creates a new treeView with only the desired item attributes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnShowFilter_Click(object sender, EventArgs e)
        {
            if (cbFilterCopy.Checked || cbFilterModify.Checked || cbFilterMove.Checked || cbFilterTransfer.Checked)
            {
                int foundcount = 0;

                //UPDATE THE TREEVIEW////////////////////////////////////////////////////////////////
                // Display a wait cursor while the TreeNodes are being created.
                Cursor.Current = Cursors.WaitCursor;

                // Suppress repainting the TreeView until all the objects have been created.
                treeView1.BeginUpdate();

                // Clear the TreeView each time the method is called.
                treeView1.Nodes.Clear();

                int countindex = 0;
                foreach (KeyValuePair<int, Object> pair in MIUtils.OD_MyInventory)
                {
                    //If it is a folder, then add to TreeView...
                    if (pair.Value.GetType().ToString() == "MyInventory.Folder")
                    {
                        Folder ftemp = (Folder)pair.Value;

                        //What type of item is it?
                        int imageindex = Utils.MIUtils.Getinv_type(ftemp.type);

                        //If the folder is a root then add it
                        if (ftemp.parent_id == "00000000-0000-0000-0000-000000000000")
                        {
                            //Add the node with text to treeview1
                            treeView1.Nodes.Add(ftemp.cat_id, ftemp.name, imageindex);
                        }
                        else//Then it must have a parent folder...
                        {
                            //Add the child node
                            InsertUnderParent(ftemp.parent_id, ftemp.cat_id, ftemp.name, imageindex, ftemp.type, treeView1.Nodes);
                        }
                    }
                    else//Must be an Item
                    {
                        Item Itemp = (Item)pair.Value;

                        //What type of item is it?
                        int imageindex = Utils.MIUtils.Getinv_type(Itemp.inv_type);

                        //If the Item is a root then add it
                        if (Itemp.parent_id == "00000000-0000-0000-0000-000000000000")
                        {
                            //Add the node with text to treeview1
                            treeView1.Nodes.Add(Itemp.item_id, Itemp.name, imageindex);
                        }
                        else//Then it must have a parent...
                        {
                            bool bContinue = cbFilterCopy.Checked;
                            if (!bContinue)
                            {
                                bContinue = cbFilterModify.Checked;
                                if (!bContinue)
                                {
                                    bContinue = cbFilterMove.Checked;
                                    if (!bContinue)
                                    {
                                        bContinue = cbFilterTransfer.Checked;
                                    }
                                }
                            }

                            if (!bContinue)
                            {
                            }
                            else
                            {
                                if (Itemp.permission.base_mask.Contains(cbFilterCopy.Text) && cbFilterCopy.Checked)
                                {
                                    //Add the child node
                                    InsertUnderParent(Itemp.parent_id, Itemp.item_id, Itemp.name, imageindex, Itemp.inv_type, treeView1.Nodes);
                                    foundcount = foundcount + 1;
                                }
                                else if (Itemp.permission.base_mask.Contains(cbFilterModify.Text) && cbFilterModify.Checked)
                                {
                                    //Add the child node
                                    InsertUnderParent(Itemp.parent_id, Itemp.item_id, Itemp.name, imageindex, Itemp.inv_type, treeView1.Nodes);
                                    foundcount = foundcount + 1;
                                }
                                else if (Itemp.permission.base_mask.Contains(cbFilterMove.Text) && cbFilterMove.Checked)
                                {
                                    //Add the child node
                                    InsertUnderParent(Itemp.parent_id, Itemp.item_id, Itemp.name, imageindex, Itemp.inv_type, treeView1.Nodes);
                                    foundcount = foundcount + 1;
                                }
                                else if (Itemp.permission.base_mask.Contains(cbFilterTransfer.Text) && cbFilterTransfer.Checked)
                                {
                                    //Add the child node
                                    InsertUnderParent(Itemp.parent_id, Itemp.item_id, Itemp.name, imageindex, Itemp.inv_type, treeView1.Nodes);
                                    foundcount = foundcount + 1;
                                }
                                else if (Itemp.permission.base_mask.Contains("All"))
                                {
                                    //Add the child node
                                    InsertUnderParent(Itemp.parent_id, Itemp.item_id, Itemp.name, imageindex, Itemp.inv_type, treeView1.Nodes);
                                    foundcount = foundcount + 1;
                                }
                            }
                        }
                    }
                    countindex = countindex + 1;
                }
                // Reset the cursor to the default for all controls.
                Cursor.Current = Cursors.Default;
                treeView1.SelectedNode = treeView1.Nodes[0];//SelectectNode must not be null
                treeView1.ExpandAll();//This action triggers the event which creates the entire treeViewnode
                // Begin repainting the TreeView.
                //treeView1.Nodes[0].Expand();//Let's see My Inventory folder subfolders
                treeView1.EndUpdate();

                //UPDATE THE TREEVIEW - DONE////////////////////////////////////////////////////////////////
                this.label1.Text = "My Inventory: (" + Convert.ToString(foundcount) + " entries found)";
            }
            else
            {
                CreateTreeView();//Nothing selected so just show everything
            }
        }
        /// <summary>
        /// Restore to the original treeView nodes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void reloadOriginalToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CreateTreeView();
        }

        #endregion treeView Methods

        #region Mouse Events
        /// <summary>
        /// Open the backup folder using the default Windows Explorer
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnOpenBUFolder_Click(object sender, EventArgs e)
        {
            if (Directory.Exists(Common.backupRootFolder))
                System.Diagnostics.Process.Start(Common.backupRootFolder);
            else
                MessageBox.Show(Common.backupRootFolder + "Does not exist.\r\nDownload an item or perform backup.", "Folder does not exist", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        /// <summary>
        /// This was created to fix a bug when using the keyboard to select nodes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            TreeNodeMouseClickEventArgs args = new TreeNodeMouseClickEventArgs(e.Node, MouseButtons.Left, 1, e.Node.Bounds.X, e.Node.Bounds.Y);
            treeView1_NodeMouseClick(sender, args);
        }
        private void chkNotecards_CheckedChanged(object sender, EventArgs e)
        {
            DisplayInvCountsEstimatedTime();//update tabBackup
        }
        private void chkPhotoAlbum_CheckedChanged(object sender, EventArgs e)
        {
            DisplayInvCountsEstimatedTime();//update tabBackup
        }

        private void chkScripts_CheckedChanged(object sender, EventArgs e)
        {
            DisplayInvCountsEstimatedTime();//update tabBackup
        }

        private void chkTextures_CheckedChanged(object sender, EventArgs e)
        {
            DisplayInvCountsEstimatedTime();//update tabBackup
        }

        /// <summary>
        /// Occurs after a tab control is selected
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tabControl1_Selected(object sender, TabControlEventArgs e)
        {
            if (e.TabPage == tabControl1.TabPages["tabLogs"])
            {
                //Refresh the logs incase something appended
                UpdateLogTextboxes();
            }
        }

        /// <summary>
        /// Event occurs when a mouse button is pressed down
        /// Right-Click events handled here with ContextMenuStrip
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void treeView1_MouseDown(object sender, MouseEventArgs e)
        {
            try
            {
                try
                {
                    //Copy the node selected...
                    selectedTreeNode = (TreeNode)(treeView1.GetNodeAt(e.X, e.Y).Clone());

                    //***DO SELECTION STUFF***
                    int startingImage = selectedTreeNode.ImageIndex;
                    selectedTreeNode.SelectedImageIndex = startingImage;

                    //Set Selection...
                    treeView1.SelectedNode = selectedTreeNode;
                    treeView1.Update();

                    //Get Folder or Item Object Info
                    tbOutput.Clear();
                    string ObjectsKey = selectedTreeNode.Name;
                    int ivalue = 0;
                    MIUtils.OD_treeindex.TryGetValue(ObjectsKey, out ivalue);
                    MIUtils.INV_TYPE objtype = new MIUtils.INV_TYPE();
                    Object objectinfo = new Object();
                    if (ivalue != -1)
                    {
                        objectinfo = MIUtils.GetMyInventoryObject(ivalue, out objtype);
                        if (objtype == MIUtils.INV_TYPE.FOLDER)
                        {
                            tbOutput.AppendText(((Folder)objectinfo).ToString());
                        }
                        else//must be item...
                        {
                            tbOutput.AppendText(((Item)objectinfo).ToString());
                        }
                    }

                    //If the starting image was a folder, then update...
                    if (startingImage == 0 || startingImage == 1)
                    {
                        if (selectedTreeNode.IsExpanded)
                        {
                            selectedTreeNode.ImageIndex = 1;//then open folder
                        }
                        else
                        {
                            selectedTreeNode.ImageIndex = 0;//if closed folder
                        }
                    }

                    #region If RIGHT-CLICK...
                    //If RIGHT-CLICK...
                    if (e.Button == MouseButtons.Right)
                    {
                        //***DISPLAY RIGHT-CLICK CONTEXTMENU***
                        try
                        {
                            //Copy the node selected...
                            selectedTreeNode = (TreeNode)(treeView1.GetNodeAt(e.X, e.Y).Clone());
                            fullpath_ofnode = treeView1.GetNodeAt(e.X, e.Y).FullPath;

                            //Let's select this node instead of the last selected node
                            //treeView1.SelectedNode = selectedTreeNode;
                            //treeView1.Update();

                            //Display the contextmenu if it is not the Trash can...
                            if (selectedTreeNode.Text == "Trash" || selectedTreeNode.Text == "TrashFolder")
                            {
                                DialogResult dResult = MessageBox.Show("Empty Trash?", "Title", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                                if (dResult == DialogResult.Yes)
                                {
                                    //Delete all of the nodes under Trash
                                    foreach (TreeNode originalNode in this.selectedTreeNode.Nodes)
                                    {
                                        RemoveTreeViewNode(originalNode);
                                    }

                                    //Log it
                                    Logs.LogEntry("Empty Trash", File.AppendText(Common.MainLog));
                                }
                                else if (dResult == DialogResult.No)
                                {

                                }
                                else if (dResult == DialogResult.Cancel)
                                {

                                }

                            }
                            else //not Trash can
                            {
                                //Do we enable 'Open' ??...
                                if (objtype == MIUtils.INV_TYPE.FOLDER)
                                {
                                    this.openToolStripMenuItem.Enabled = false;
                                }
                                else//must be item...
                                {
                                    //Let's enable the Open choice
                                    this.openToolStripMenuItem.Enabled = true;

                                    //Let's disable the open if there is no image attached
                                    if (((Item)objectinfo).type != "landmark")
                                    {
                                        //Create SLMIVImage which reads the ImageLog
                                        SLMIVImage NodeImage = new SLMIVImage(Common.ImageLog);
                                        if (NodeImage.sbImageLog.ToString().IndexOf(selectedTreeNode.Name) != -1)
                                        {
                                            this.openToolStripMenuItem.Enabled = true;
                                        }
                                        else
                                            this.openToolStripMenuItem.Enabled = false;
                                    }

                                    //Let's examine for Download feature for Notecards, LSL, and other
                                    if (((Item)objectinfo).type == "notecard" ||
                                        ((Item)objectinfo).type == "lsl" ||
                                        ((Item)objectinfo).type == "lsltext" ||
                                        ((Item)objectinfo).type == "texture" ||
                                        ((Item)objectinfo).type == "snapshot")
                                    {
                                        this.downloadToolStripMenuItem.Enabled = true;
                                    }
                                    else
                                    {
                                        this.downloadToolStripMenuItem.Enabled = false;
                                    }

                                }

                                //now show RIGHTCLICK MENU...
                                this.ContextMenuStrip = contextMenuStrip1;
                                //   this.ContextMenuStrip.Show();//FIX: stop first display somewhere else
                                //Need to refresh or ToolTips will be lost
                                treeView1.Refresh();
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message);
                        }
                    }
                    #endregion If RIGHT-CLICK...

                    //Scroll to the last line and right justified
                    tbOutput.AppendText("\r\n");
                    tbOutput.ScrollToCaret();
                }
                catch (Exception)
                {
                    //If clicking on nothing in treeview1, exception is thrown
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show("treeView1_MouseDown Error: " + ex.Message, "MouseDown Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Edit the nodes' images when clicked
        /// Fixes the incorrect drawing of the node's image
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void treeView1_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            try
            {
                int startingImage = e.Node.ImageIndex;
                e.Node.SelectedImageIndex = startingImage;

                #region Get Folder or Item Object Info
                tbOutput.Clear();
                string ObjectsKey = e.Node.Name;
                int ivalue = 0;
                MIUtils.OD_treeindex.TryGetValue(ObjectsKey, out ivalue);
                if (ivalue != -1)
                {
                    MIUtils.INV_TYPE objtype;
                    Object objectinfo = new Object();
                    objectinfo = MIUtils.GetMyInventoryObject(ivalue, out objtype);
                    if (objtype == MIUtils.INV_TYPE.FOLDER)
                    {
                        tbOutput.AppendText(((Folder)objectinfo).ToString());
                    }
                    else//must be item...
                    {
                        tbOutput.AppendText(((Item)objectinfo).ToString());
                    }

                    //Set the ToolTip
                    e.Node.ToolTipText = e.Node.FullPath;
                    toolTipSLMIV.Show(e.Node.Text, this);

                    //Update the tbOutput
                    tbOutput.AppendText("\r\nItem's Full Folder Path:\r\n" + e.Node.FullPath);
                }
                #endregion Get Folder or Item Object Info

                #region If the starting image was a folder, then update...
                if (startingImage == 0 || startingImage == 1)
                {
                    if (e.Node.IsExpanded)
                    {
                        e.Node.ImageIndex = 1;//then open folder
                    }
                    else
                    {
                        e.Node.ImageIndex = 0;//if closed folder
                    }
                }
                #endregion If the starting image was a folder, then update...

                //Scroll to the last line and right justified
                tbOutput.AppendText("\r\n");
                tbOutput.ScrollToCaret();
            }
            catch (Exception ex)
            {
                MessageBox.Show("treeView1_NodeMouseClick Error: " + ex.Message, "NodeMouseClick Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void cbxGrid_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cbxGrid.SelectedIndex == 0)
            {
                tbLoginUri.Clear();
                tbLoginUri.ReadOnly = true;
                client.Settings.LOGIN_SERVER = Common.GridMainLogin;
            }
            else if (cbxGrid.SelectedIndex == 1)
            {
                tbLoginUri.Clear();
                tbLoginUri.ReadOnly = true;
                client.Settings.LOGIN_SERVER = Common.GridBetaLogin;
            }
            else if (cbxGrid.SelectedIndex == 2)
            {
                tbLoginUri.ReadOnly = false;
            }
        }

        /// <summary>
        /// When entering into tabBackup
        /// </summary>
        /// <param name="sender">object</param>
        /// <param name="e">EventArgs</param>
        private void tabBackup_Enter(object sender, EventArgs e)
        {
            //Are we connected to SL?
            if (!this.client.Network.Connected)
            {
                this.btnBackup.Enabled = false;
                chkNotecards.Enabled = false;
                chkPhotoAlbum.Enabled = false;
                chkScripts.Enabled = false;
                chkTextures.Enabled = false;
                this.lbStatus.ForeColor = Color.Red;
                this.lbStatus.Text = "No SL Connection. You must be logged in to perform a backup.";
                //Display inventory counts
                if (this.IICountsDict.Count > 0)
                {
                    int count = 0;
                    IICountsDict.TryGetValue(InventoryType.Notecard, out count);
                    chkNotecards.Text = "Notecards [QTY: " + Convert.ToString(count) + "]";
                    IICountsDict.TryGetValue(InventoryType.Snapshot, out count);
                    chkPhotoAlbum.Text = "Photo Album [QTY: " + Convert.ToString(count) + "]";
                    IICountsDict.TryGetValue(InventoryType.LSL, out count);
                    chkScripts.Text = "Scripts/LSL [QTY: " + Convert.ToString(count) + "]";
                    IICountsDict.TryGetValue(InventoryType.Texture, out count);
                    chkTextures.Text = "Textures [QTY: " + Convert.ToString(count) + "]";
                }
            }
            else
            {
                this.btnBackup.Enabled = true;
                chkNotecards.Enabled = true;
                chkPhotoAlbum.Enabled = true;
                chkScripts.Enabled = true;
                chkTextures.Enabled = true;
                this.lbStatus.ForeColor = Color.Black;
                this.lbStatus.Text = "SL Connection. Select the items you wish backed-up, then click 'Backup These Items'";
                //Display inventory counts
                DisplayInvCountsEstimatedTime();//update tabBackup
            }

        }

        /// <summary>
        /// Search the current treeView
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btSearch_Click(object sender, EventArgs e)
        {
            //Reset the two main search collections
            //foundnodes = temptv.Nodes;
            //foundparentnodes = temptv.Nodes;
            string lookingfor = tbSearchbox.Text;
            //Create a new treeView with only the desired nodes
            doSelectedShow(lookingfor);
            treeView1.Update();
        }
        private void tabControlLogs_Enter(object sender, EventArgs e)
        {
            tbLog.ScrollToCaret();
            tbImageLog.ScrollToCaret();
            tbSLConLog.ScrollToCaret();
        }

        private void tabControlLogs_Click(object sender, EventArgs e)
        {
            tbLog.ScrollToCaret();
            tbImageLog.ScrollToCaret();
            tbSLConLog.ScrollToCaret();
        }
        #endregion Mouse Events

        #region ToolTip Events
        /// <summary>
        /// Set the size of the tooltip's area
        /// </summary>
        /// <param name="sender">object</param>
        /// <param name="e">PopupEventArgs</param>
        private void toolTipSLMIV_Popup(object sender, PopupEventArgs e)
        {
            int wid = e.ToolTipSize.Width + 150;
            int hgt = e.ToolTipSize.Height;
            if (hgt < 150)
                hgt = 150;

            e.ToolTipSize = new Size(wid, hgt);
        }

        /// <summary>
        /// Draws background, border, text, and the image of tooltip
        /// </summary>
        /// <param name="sender">object</param>
        /// <param name="e">DrawToolTipEventArgs</param>
        private void toolTipSLMIV_Draw(object sender, DrawToolTipEventArgs e)
        {
            try
            {
                TreeView tview = (TreeView)e.AssociatedWindow;
                if (tview.SelectedNode != null)
                {
                    TreeNode tnode = tview.SelectedNode;

                    //Create SLMIVImage which reads the ImageLog
                    SLMIVImage NodeImage = new SLMIVImage(Common.ImageLog);
                    NodeImage.Get(tnode.Name);

                    //Draw the background and border
                    e.DrawBackground();
                    e.DrawBorder();

                    //Draw the text
                    StringFormat sf = new StringFormat();
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    Rectangle rect = new Rectangle(125, 0, e.Bounds.Width - (150), e.Bounds.Height);
                    e.Graphics.DrawString(tnode.Text, e.Font, Brushes.Black, rect, sf);

                    if ((((string)tnode.Tag) != "notecard") && (((string)tnode.Tag) != "lsl"))
                    {
                        //Draw the image
                        e.Graphics.DrawImage(NodeImage.Get(tnode.Name), 9, 9);
                    }
                    else
                    {
                        //Does it have a local text file?
                        if (NodeImage.path_image != null && NodeImage.path_image.ToLower().Trim().EndsWith(".txt"))
                        {
                            System.Drawing.Image image = new Bitmap(SLMIV.Resources.Folder_open, 120, 120);
                            e.Graphics.DrawImage(image, 9, 9);
                        }
                        else if (NodeImage.path_image != null && NodeImage.path_image.ToLower().Trim().EndsWith(".lsl"))
                        {
                            System.Drawing.Image image = new Bitmap(SLMIV.Resources.Folder_open, 120, 120);
                            e.Graphics.DrawImage(image, 9, 9);
                            //Draw the no image
                            //e.Graphics.DrawImage(System.Drawing.Image.FromHbitmap(SLMIV.Resources.Nonecircle.GetHbitmap()), 9, 9);
                        }
                        else
                        {
                            //Draw the no image
                            System.Drawing.Image image = new Bitmap(SLMIV.Resources.Nonecircle, 120, 120);
                            e.Graphics.DrawImage(image, 9, 9);
                        }
                    }
                }
            }
            catch (Exception ex)
            {

            }
        }
        #endregion ToolTip Events

        #region ContextMenu Methods
        /// <summary>
        /// Copy the Folder or Item's UUID to clipboard
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void copyAssetUUIDToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Clipboard.Clear();
            Clipboard.SetDataObject(treeView1.SelectedNode.Name, true);

            //Let's show that we copied the UUID to the user
            tbOutput.SelectedText = treeView1.SelectedNode.Name;
            tbOutput.Copy();
        }

        /// <summary>
        /// Delete node
        /// </summary>
        /// <param name="sender">object</param>
        /// <param name="e">EventArgs</param>
        private void toolStripMenuItem1_Click(object sender, EventArgs e)
        {
            Logs.LogEntry("Delete '" + selectedTreeNode.Text + "' from location:\r\n" + fullpath_ofnode, File.AppendText(Common.MainLog));

            //Delete the treenode...
            if (!RemoveTreeViewNode(selectedTreeNode))
                MessageBox.Show("Could not delete " + selectedTreeNode.Text);

            //UpdateCollections(selectedTreeNode.Name);//not using Backuptreenode in version 2
        }

        /// <summary>
        /// Added an Examine entry into the log file 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void examineToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Logs.LogEntry("Examine '" + selectedTreeNode.Text + "' from location:\r\n" + fullpath_ofnode, File.AppendText(Common.MainLog));

            MessageBox.Show("Added to log file:\r\nExamine '" + selectedTreeNode.Text + "' from location:\r\n" + fullpath_ofnode);
        }

        /// <summary>
        /// Rename the Folder or Item and append to log
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void renameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            oldNodeName = treeView1.SelectedNode.FullPath;
            treeView1.LabelEdit = true;
            treeView1.SelectedNode.BeginEdit();
        }

        /// <summary>
        /// After a LabelEdit, turn off editing for other nodes, then append to the 
        /// MainLog
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void treeView1_AfterLabelEdit(object sender, NodeLabelEditEventArgs e)
        {
            treeView1.LabelEdit = false;
            treeView1.SelectedNode.EndEdit(e.CancelEdit);

            Logs.LogEntry("Rename " + oldNodeName + " to\r\n" + e.Node.Parent.FullPath + "\\" + e.Label, File.AppendText(Common.MainLog));
        }

        /// <summary>
        /// Occurs when user clicks the open menu option
        /// Opens the Item {}
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //If this is a landmark, then ask if you want to visit slr
            int iNodeindex = 0;
            MIUtils.OD_treeindex.TryGetValue(treeView1.SelectedNode.Name, out iNodeindex);
            if (iNodeindex != -1)
            {
                MIUtils.INV_TYPE nodeObjtype;
                Object objinfo = new Object();
                objinfo = MIUtils.GetMyInventoryObject(iNodeindex, out nodeObjtype);
                //Create SLMIVImage which reads the ImageLog
                SLMIVImage NodeImage = new SLMIVImage(Common.ImageLog);

                if (nodeObjtype == MIUtils.INV_TYPE.ITEM)
                {
                    if (((Item)objinfo).type == "landmark")
                    {
                        #region landmark
                        try
                        {
                            //MessageBox.Show(((Item)objinfo).name);
                            string temp = ((Item)objinfo).name;
                            temp = temp.Replace(")", "");
                            temp = temp.Replace("(", ",");
                            string[] location = temp.Split(',');
                            //first word after first ',' should be region
                            string region = location[1].Trim();
                            string x = location[2].Trim();
                            string y = location[3].Trim();
                            string z = location[3].Trim();

                            string slurl = "http://slurl.com/secondlife/" + region + "/" + x + "/" + y + "/" + z;

                            DialogResult dResult = MessageBox.Show("Do you want to visit?\r\n" + slurl, "Visit landmark at slurl.com", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                            if (dResult == DialogResult.Yes)
                            {
                                System.Diagnostics.Process.Start(slurl);
                            }
                            else if (dResult == DialogResult.No)
                            {

                            }
                            else if (dResult == DialogResult.Cancel)
                            {

                            }
                        }
                        catch (Exception ex)
                        {
                            if (ex.Message.StartsWith("Index"))
                            {
                                MessageBox.Show("Sorry, can not proceed with slurl visit.\r\nNot enough landmark information in the name of the landmark.\r\nPlease rename the landmark inworld with the correct sl default name.", "slurl visit error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                            else
                                MessageBox.Show(ex.Message);
                        }
                        #endregion landmark
                        //if it is a script or notecard
                    }
                    else if ((((Item)objinfo).type == "notecard") || (((Item)objinfo).type == "lsltext"))
                    {
                        #region notecard
                        try
                        {
                            //MessageBox.Show(((Item)objinfo).name);
                            string temp = ((Item)objinfo).name;
                            string uuid = (((Item)objinfo).item_id);
                            if (NodeImage.sbImageLog.ToString().IndexOf(uuid) != -1)
                            {
                                //Extract string from log
                                int commapoint = (NodeImage.sbImageLog.ToString().Trim().IndexOf(uuid) + uuid.Length + 1);
                                int endofpath = (NodeImage.sbImageLog.ToString().IndexOf("\r\n", commapoint + 1));//find the first return after the commapoint
                                string[] splitline = (NodeImage.sbImageLog.ToString().Trim().Substring(commapoint, endofpath - commapoint)).Split('#');//seperate out the comments
                                System.Diagnostics.Process.Start(splitline[0].Trim());
                            }
                        }
                        catch (Exception ex)
                        {
                            if (ex.Message.StartsWith("Index"))
                            {
                                MessageBox.Show("Sorry.\r\nNot enough information.", "browse to a file error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                            else
                                MessageBox.Show(ex.Message);
                        }

                        #endregion notecard
                    }
                    else
                    {
                        //This works for local paths and urls
                        if (NodeImage.sbImageLog.ToString().IndexOf(treeView1.SelectedNode.Name) != -1)
                        {
                            NodeImage.Display(treeView1.SelectedNode.Name);
                        }

                    }
                }
            }
        }

        /// <summary>
        /// Do the browseToToolStripMenuItem method
        /// </summary>
        public void notecEdfrm_HookEvent(Hooks message, object args)
        {
            browseToToolStripMenuItem_Click(message, EventArgs.Empty);
        }


        /// <summary>
        /// Attach Image submenu event
        /// Browse to the local image file to associate item with 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void browseToToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //if imagelog doesn't exist
            if (!File.Exists(Common.ImageLog))
            {
                //Logs.CreateImageLogFile();//creates an empty first line
                File.WriteAllText(Common.ImageLog, "");
            }

            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "All files (*.*)|*.*|bmp files (*.bmp)|*.bmp|lsl files (*.lsl)|*.lsl|txt files (*.txt)|*.txt|jpg files (*.jpg)|*.jpg|gif files (*.gif)|*.gif";
            ofd.InitialDirectory = Common.SLMIVPath;
            ofd.ShowDialog();
            try
            {
                if (ofd.FileName != string.Empty)
                {
                    //Does this key exist already in the log file??
                    if (File.ReadAllText(Common.ImageLog).ToString().IndexOf(selectedTreeNode.Name) != -1)
                    {
                        DialogResult dResult = MessageBox.Show("You already have an object with this item.\r\nOverwrite?", "Associate New Image/Object", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                        if (dResult == DialogResult.Yes)
                        {
                            StringBuilder sbImageLog = new StringBuilder();
                            if (File.Exists(Common.ImageLog))
                            {
                                sbImageLog.Append(File.ReadAllText(Common.ImageLog));
                            }
                            int commapoint = (sbImageLog.ToString().IndexOf(selectedTreeNode.Name) + selectedTreeNode.Name.Length + 1);
                            int endofpath = (sbImageLog.ToString().IndexOf("\r\n", commapoint + 1));//find the first return after the commapoint
                            string pathtoimage = sbImageLog.ToString().Substring(commapoint, endofpath - commapoint);

                            //reWrite item's image file path into log file...
                            sbImageLog.Replace(selectedTreeNode.Name + "," + pathtoimage, selectedTreeNode.Name + "," + ofd.FileName + " # " + selectedTreeNode.Text);

                            //write the change to the imagefile
                            File.WriteAllText(Common.ImageLog, sbImageLog.ToString());

                            //update the image link

                            //Format LinkImage
                            this.Update();

                        }
                        else if (dResult == DialogResult.No)
                        {

                        }
                        else if (dResult == DialogResult.Cancel)
                        {

                        }
                    }
                    else //no
                    {
                        //if file exists
                        //Write item's key and the image's file path into log file...
                        Logs.ImageLogEntry(selectedTreeNode.Name + "," + ofd.FileName + " # " + selectedTreeNode.Text, File.AppendText(Common.ImageLog));

                        //update the image link
                        //        pathtoimage = ofd.FileName;
                        //Format LinkImage
                        //       this.linkImage.Text = "Image Available!";
                        //        this.linkImage.Enabled = true;
                        this.Update();
                    }
                }
                else
                {

                }
            }
            catch (Exception ex)
            {

                MessageBox.Show(ex.Message);
            }
        }

        /// <summary>
        /// Attach Image submenu event
        /// Type URL of internet image file to associate item with 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void internetURLToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //Check to see if key exists in imagelog file
            //yes
            try
            {
                string pathtoimage = "";
                //If the file exists load it into sbImageLog
                StringBuilder sbImageLog = new StringBuilder();
                if (File.Exists(Common.ImageLog))
                {
                    sbImageLog.Append(File.ReadAllText(Common.ImageLog));

                    string temp = File.ReadAllText(Common.ImageLog);
                    if ((temp != "\r\n"))//http://www.flickr.com/photos/8695699@N05/538315923/
                    {
                        if ((temp != ""))
                        {
                            //Select the points in the sbImageLog
                            int commapoint = (sbImageLog.ToString().IndexOf(selectedTreeNode.Name) + selectedTreeNode.Name.Length + 1);
                            int endofpath = (sbImageLog.ToString().IndexOf("\r\n", commapoint + 1));//find the first return after the commapoint
                            pathtoimage = sbImageLog.ToString().Substring(commapoint, endofpath - commapoint);
                        }
                    }
                    else
                        File.WriteAllText(Common.ImageLog, "#key,pathoffile\r\n");//fix for Index error when nothing in a imagelog file

                    //Does this key exist already in the log file??
                    if (File.ReadAllText(Common.ImageLog).ToString().IndexOf(selectedTreeNode.Name) != -1)
                    {
                        //Check the image path to see if there is already a url or local file association
                        if (pathtoimage.StartsWith("http://"))
                        {
                            //if yes URL, show in txb
                            SLMIV.Utils.Forms.UrlForm urlForm = new SLMIV.Utils.Forms.UrlForm();
                            urlForm.url = pathtoimage;
                            urlForm.lbNotify = "no local file associated with inventory object";//clear the label

                            DialogResult dResult = urlForm.ShowDialog();
                            if (dResult == DialogResult.OK)
                            {
                                //See if user changed urlForm.url 
                                if (urlForm.url != pathtoimage)
                                {
                                    //User changed so update imagelog file
                                    //reWrite item's image file path into log file...
                                    sbImageLog.Replace(selectedTreeNode.Name + "," + urlForm.url, selectedTreeNode.Name + "," + pathtoimage);
                                    File.WriteAllText(Common.ImageLog, sbImageLog.ToString());

                                    //Format LinkImage
                                    //     this.linkImage.Text = "Image Available!";
                                    //    this.linkImage.Enabled = true;
                                    this.Update();
                                }
                            }
                        }
                        else
                        {
                            //no URL but local exists for key, display default URL
                            //and tell user there is a local image
                            SLMIV.Utils.Forms.UrlForm urlForm = new SLMIV.Utils.Forms.UrlForm();
                            urlForm.key = selectedTreeNode.Name;//pass key
                            urlForm.url = "http://";
                            urlForm.lbNotify = "Found currect local image at:\r\n" + pathtoimage;

                            //Update the pathtoimage if user clicked ok to update
                            DialogResult dResult = urlForm.ShowDialog();
                            if (dResult == DialogResult.OK)
                            {
                                pathtoimage = urlForm.url;

                                //Format LinkImage
                                //    this.linkImage.Text = "Image Available!";
                                //    this.linkImage.Enabled = true;
                                this.Update();
                            }
                        }
                    }
                    else //no key found in file
                    {
                        SLMIV.Utils.Forms.UrlForm urlForm = new SLMIV.Utils.Forms.UrlForm();
                        urlForm.key = selectedTreeNode.Name;//pass key
                        urlForm.url = "http://";
                        urlForm.lbNotify = "no local file associated with inventory object";//clear the label
                        //Update the pathtoimage if user clicked ok to update
                        DialogResult dResult = urlForm.ShowDialog();
                        if (dResult == DialogResult.OK)
                        {
                            //Write item's key and the image's file path into log file...
                            Logs.ImageLogEntry(selectedTreeNode.Name + "," + urlForm.url + " # " + selectedTreeNode.Text, File.AppendText(Common.ImageLog));

                            //update the image link
                            pathtoimage = urlForm.url;

                            //Format LinkImage
                            //    this.linkImage.Text = "Image Available!";
                            //    this.linkImage.Enabled = true;
                            this.Update();
                        }
                    }
                }
                else//The file does not exit, so create then apply imagelink
                {
                    SLMIV.Utils.Forms.UrlForm urlForm = new SLMIV.Utils.Forms.UrlForm();
                    urlForm.key = selectedTreeNode.Name;//pass key
                    urlForm.url = "http://";
                    urlForm.lbNotify = "no local file associated with inventory object";//clear the label
                    //Update the pathtoimage if user clicked ok to update
                    DialogResult dResult = urlForm.ShowDialog();
                    if (dResult == DialogResult.OK)
                    {
                        //Write item's key and the image's file path into log file...
                        Logs.ImageLogEntry(selectedTreeNode.Name + "," + urlForm.url + " # " + selectedTreeNode.Text, File.AppendText(Common.ImageLog));

                        //update the image link
                        pathtoimage = urlForm.url;

                        //Format LinkImage
                        //    this.linkImage.Text = "Image Available!";
                        //    this.linkImage.Enabled = true;
                        this.Update();
                    }
                }
            }
            catch (Exception ex)
            {

                MessageBox.Show(ex.Message);
            }

        }

        #endregion ContextMenu Methods

        #region Printing
        /// <summary>
        /// Print the MainLog
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void printToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                streamToPrint = new StreamReader(Common.MainLog);
                try
                {
                    printFont = new Font("Arial", 12);
                    PrintDocument pd = new PrintDocument();
                    pd.PrintPage += new PrintPageEventHandler(pd_PrintPage);
                    // Print the document.
                    pd.Print();
                }
                finally
                {
                    streamToPrint.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
        private void pd_PrintPage(object sender, PrintPageEventArgs ev)
        {
            float linesPerPage = 0;
            float yPos = 0;
            int count = 0;
            float leftMargin = ev.MarginBounds.Left;
            float topMargin = ev.MarginBounds.Top;
            String line = null;

            // Calculate the number of lines per page.
            linesPerPage = ev.MarginBounds.Height /
               printFont.GetHeight(ev.Graphics);

            // Iterate over the file, printing each line.
            while (count < linesPerPage &&
               ((line = streamToPrint.ReadLine()) != null))
            {
                yPos = topMargin + (count * printFont.GetHeight(ev.Graphics));
                ev.Graphics.DrawString(line, printFont, Brushes.Black,
                   leftMargin, yPos, new StringFormat());
                count++;
            }

            // If more lines exist, print another page.
            if (line != null)
                ev.HasMorePages = true;
            else
                ev.HasMorePages = false;
        }
        #endregion Printing

        #region Backup methods
        /// <summary>
        /// Occurs when user Right-Clicks an item and chooses Download
        /// Connects to SL to download a single InventoryItem
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void downloadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                if (!String.IsNullOrEmpty((String)treeView1.SelectedNode.Tag))
                {
                    if ((((String)treeView1.SelectedNode.Tag) == "notecard") ||
                        (((String)treeView1.SelectedNode.Tag) == "lsl") ||
                        (((String)treeView1.SelectedNode.Tag) == "snapshot") ||
                        (((String)treeView1.SelectedNode.Tag) == "texture") ||
                        (((String)treeView1.SelectedNode.Tag) == "rootcategory"))
                    {
                        //Get the SLMIV object for this item
                        Item SLMIVItem;
                        InventoryNotecard notecard;
                        string ObjectsKey = treeView1.SelectedNode.Name;
                        int ivalue = 0;
                        MIUtils.OD_treeindex.TryGetValue(ObjectsKey, out ivalue);
                        MIUtils.INV_TYPE objtype = new MIUtils.INV_TYPE();
                        Object objectinfo = new Object();
                        if (ivalue != -1)
                        {
                            objectinfo = MIUtils.GetMyInventoryObject(ivalue, out objtype);
                            if (objtype == MIUtils.INV_TYPE.ITEM)
                            {
                                SLMIVItem = ((Item)objectinfo);
                                Console.WriteLine("[SLMIV]: Retreiving " + treeView1.SelectedNode.Text + ", " + treeView1.SelectedNode.Name);
                                //Check to make sure currently online
                                if (client.Network.Connected)
                                {
                                    LLUUID uuid = new LLUUID();
                                    uuid = LLUUID.Parse(treeView1.SelectedNode.Name);
                                    InventoryItem item = new InventoryItem(uuid);
                                    if (libsecItemDict.TryGetValue(uuid, out item))
                                    {
                                        if ((item.AssetType == AssetType.Notecard) || (item.AssetType == AssetType.LSLText))
                                        {
                                            frmNotecardEditor notecEdfrm = new frmNotecardEditor(item, this.client, this.netcom, imageListTreeView);
                                            notecEdfrm.ItemTreeNode = treeView1.SelectedNode;//Pass the TreeNode so it has its fullpath
                                            notecEdfrm.Show();
                                        }
                                        else if (item.AssetType == AssetType.Texture)
                                        {
                                            //Show 
                                            ImageViewerForm iviewer = new ImageViewerForm(item, client, netcom, imageListTreeView, imageCache);
                                            iviewer.ItemTreeNode = treeView1.SelectedNode;//Pass the TreeNode so it has its fullpath
                                            iviewer.Show();
                                        }
                                    }
                                }
                                else
                                {
                                    MessageBox.Show("Can not download because you are not currently connected to the online grid.", "Can Not Download", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                }
                            }
                        }
                        else
                        {
                            //Exception ?? Really? Please tell me why
                            MessageBox.Show("ERROR: Can not download because the selected item in the inventory tree [" + treeView1.SelectedNode.Text + "] is not found in MIUtils.OD_treeindex.", "Can Not Download", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                    else
                    {
                        MessageBox.Show("Can not download because the selected item in the inventory tree [" + treeView1.SelectedNode.Text + "] is not a Notecard or LSL script.", "Can Not Download", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception)
            {

            }
        }

        /// <summary>
        /// User clicked Backup button on tabBackup.
        /// Downloads requested items and sends output to txbStatusHis
        /// </summary>
        /// <param name="sender">object</param>
        /// <param name="e">EventArgs</param>
        private void btnBackup_Click(object sender, EventArgs e)
        {
            try
            {
                //Disable the backup button, no dups
                btnBackup.Enabled = false;
                //post time
                lbEstTime.Text = lbEstTime.Text + Environment.NewLine + "(started:" + DateTime.Now.ToShortTimeString() + ")";
                lbEstTime.Update();//update right away before other threads start working!
                //Are there files in the directory?
                // If so, Let's delete these
                if (Directory.Exists(Common.backupRootFolder + "\\" + client.Self.Name))
                {
                    //Prompt user yes or know
                    DialogResult dResult = MessageBox.Show("Do you want to delete ALL current backup items for " + client.Self.Name + "?(click 'Yes')\r\nClick 'No' button to keep existing items and write over previous items.", "Friendly reminder from accidental deletion", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                    if (dResult == DialogResult.Yes)
                    {
                        //Ok then start fresh..
                        string delpath = (Common.backupRootFolder + "\\" + client.Self.Name);
                        Directory.Delete(delpath, true);
                        client.Log("[SLMIV] BACKUP Prep deleted " + delpath, Helpers.LogLevel.Info);
                        client.Log("[SLMIV] BACKUP STARTED...", Helpers.LogLevel.Info);
                    }
                    else if (dResult == DialogResult.No)
                    {
                        client.Log("[SLMIV] BACKUP STARTED...", Helpers.LogLevel.Info);
                    }
                    else if (dResult == DialogResult.Cancel)
                    {
                        client.Log("[SLMIV] BACKUP was cancelled by user.", Helpers.LogLevel.Info);
                        MessageBox.Show("Backup was cancelled.", "Backup Cancelled", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                }

                //libsecItemDict holds all items
                //Find out what items user wish to be backed up
                Cursor.Current = Cursors.WaitCursor;
                txbStatusHis.Clear();
                txbStatusHis.AppendText("BEGINNING BACKUP OF " + client.Self.Name.ToUpper() + "'S 'MY INVENTORY'" + Environment.NewLine);
                txbStatusHis.AppendText("BACKUP SETTINGS CHECKED: " + Environment.NewLine);
                if (chkNotecards.Checked)
                    txbStatusHis.AppendText("     " + InventoryType.Notecard.ToString().ToUpper() + Environment.NewLine);
                if (chkPhotoAlbum.Checked)
                    txbStatusHis.AppendText("     PHOTO ALBUM" + Environment.NewLine);
                if (chkScripts.Checked)
                    txbStatusHis.AppendText("     " + InventoryType.LSL.ToString().ToUpper() + Environment.NewLine);
                if (chkTextures.Checked)
                    txbStatusHis.AppendText("     " + InventoryType.Texture.ToString().ToUpper() + Environment.NewLine);
                txbStatusHis.AppendText("=========================" + Environment.NewLine);

                //Look at all the nodes
                foreach (InventoryNode i in client.Inventory.Store.RootNode.Nodes.Values)
                {
                    DownloadInventoryBase(i.Data);//also uses iDownloader to add images
                }
                //Image downloading only...
                if (chkPhotoAlbum.Checked || chkTextures.Checked)
                {
                    while (PendingImageDownloads.Count != 0)
                    {
                        QueuedDownloadInfo queinfo = PendingImageDownloads.Dequeue();
                        ImageDownloader iDownloader = new ImageDownloader(this.client, this.netcom, queinfo.IItem, imageCache, queinfo.FileName);
                        iDownloader.tBoxStatus = txbStatusHis;
                        iDownloader.Dock = DockStyle.Fill;
                        panelImage.Controls.Add(iDownloader);
                        iDownloader.BringToFront();
                        iDownloader.Visible = true;
                        iDownloader.Update();
                        iDownloader.Show();
                    }
                    //Only downloading the one photo then ending
                }

                Cursor.Current = Cursors.Default;//Allow clicking
                //Do not place anything after here because other threads are still downloading and saving images
                btnBackup.Enabled = true;//except this, lets allow the user to create problems or use good judgement
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
        /// <summary>
        /// Executes AssetManager.RequestInventoryAsset and assigns transferID while writing to lbStatus.Text and txbStatusHis
        /// </summary>
        private void RequestInventoryItem(InventoryItem i)
        {
            Console.WriteLine("[SLMIV]: AssetReceivedCallback=requesting:" + i.Name + " ;" + i.UUID.ToStringHyphenated());//Debug
            client.Log("[SLMIV] Requesting: " + i.Name + " ;UUID: " + i.UUID.ToStringHyphenated(), Helpers.LogLevel.Info);
            lbStatus.Text = "Requesting: " + i.Name + " ;UUID: " + i.UUID.ToStringHyphenated();
            AppendLineStatusBox(lbStatus.Text);
            lbStatus.Update();
            transferID = client.Assets.RequestInventoryAsset(i.AssetUUID, i.UUID, LLUUID.Zero, i.OwnerID, i.AssetType, false);
            System.Threading.Thread.Sleep(2000);//Sleep needed to wait for call backs
        }

        /// <summary>
        /// Appends a Line to txbStatusHis.Text with a time prefix
        /// </summary>
        /// <param name="text"></param>
        private void AppendLineStatusBox(String text)
        {
            txbStatusHis.AppendText("[" + DateTime.Now.ToShortTimeString() + "] " + text + Environment.NewLine);
        }

        //Separate thread
        private void Assets_OnAssetReceived(AssetDownload transfer, Asset asset)
        {
            if (transfer.ID != transferID)
            {
                Console.WriteLine("[SLMIV]: Assets_OnAssetReceived=transfer:" + transfer.AssetID.ToStringHyphenated() + " != transferID:" + transferID.ToString());//Debug
                return;
            }

            if (!transfer.Success)
            {
                notecardContent = "Unable to download item. You may not have the correct permissions.";
                client.Log("[SLMIV] Unable to download AssetID: " + transferID.ToString() + " You may not have the correct permissions.", Helpers.LogLevel.Info);
                return;
            }
            else
            {
                notecardContent = Helpers.FieldToUTF8String(transfer.AssetData);
                //client.Log("[SLMIV] Received AssetID: " + transfer.AssetID.UUID.ToString(), Helpers.LogLevel.Info);//NO:asset.AssetID.UUID.ToString(),asset.AssetID.ToStringHyphenated()NO:transfer.ID.ToStringHyphenated(), NO:transferID.ToString()
                Console.WriteLine("[SLMIV]: Received AssetID: " + transfer.AssetID.UUID.ToString());//Debug
            }
        }

        /// <summary>
        /// Take an object
        /// </summary>
        /// <param name="io">InventoryBase</param>
        private void DownloadInventoryBase(InventoryBase io)
        {
            if (io is InventoryFolder)
            {
                InventoryFolder folder = (InventoryFolder)io;
                List<InventoryBase> folderContents = client.Inventory.Store.GetContents(folder);

                if (folderContents.Count > 0)
                {
                    CurrentPath.Add(MIUtils.CleanForWriteandRead(folder.Name));//We found a new folder with something in it, keep a record of it
                    foreach (InventoryBase subfolderObj in folderContents)
                    {
                        if (client.Network.Connected)
                            DownloadInventoryBase(subfolderObj);
                        else
                        {
                            //Let's make sure the user does not get froozen if we have sl network problems
                            client.Log("[SLMIV] ERROR: Connection lost to grid; Aborting backup", Helpers.LogLevel.Error);
                            return;
                        }
                    }
                    //We are now out of this folder, so remove the last foldername from the treebranch
                    CurrentPath.RemoveAt(CurrentPath.Count - 1);
                }

                treeView1.EndUpdate();
            }
            else if (io is InventoryItem)
            {
                InventoryItem Iitem = (InventoryItem)io;//"lsl""snapshot""texture"

                //Now create the backup file in that dir
                if (Iitem.InventoryType == InventoryType.Notecard && chkNotecards.Checked == true)
                {
                    DirectoryInfo dinfo = Directory.CreateDirectory(Common.backupRootFolder + "\\" + client.Self.Name + "\\" + CurrentPathToString());
                    RequestInventoryItem(Iitem);
                    client.Log("[SLMIV] Received UUID: " + Iitem.UUID.ToStringHyphenated(), Helpers.LogLevel.Info);
                    FileStream fs = File.Create(dinfo.FullName + "\\" + MIUtils.CleanForWriteandRead(Iitem.Name) + ".txt"); fs.Close();
                    File.WriteAllText(dinfo.FullName + "\\" + MIUtils.CleanForWriteandRead(Iitem.Name) + ".txt", Convert.ToString(notecardContent));
                    lbStatus.Text = "Created: " + dinfo.FullName + "\\" + MIUtils.CleanForWriteandRead(Iitem.Name) + ".txt";
                    AppendLineStatusBox(lbStatus.Text);
                    //Write item's key and the image's file path into log file...
                    Logs.ImageLogEntry(Iitem.UUID.ToStringHyphenated() + "," + (dinfo.FullName + "\\" + MIUtils.CleanForWriteandRead(Iitem.Name) + ".txt") + " # " + Iitem.Name, File.AppendText(Common.ImageLog));
                }
                else if (Iitem.InventoryType == InventoryType.LSL && chkScripts.Checked == true)
                {
                    DirectoryInfo dinfo = Directory.CreateDirectory(Common.backupRootFolder + "\\" + client.Self.Name + "\\" + CurrentPathToString());
                    RequestInventoryItem(Iitem);
                    client.Log("[SLMIV] Received UUID: " + Iitem.UUID.ToStringHyphenated(), Helpers.LogLevel.Info);
                    FileStream fs = File.Create(dinfo.FullName + "\\" + MIUtils.CleanForWriteandRead(Iitem.Name) + ".lsl"); fs.Close();
                    File.WriteAllText(dinfo.FullName + "\\" + MIUtils.CleanForWriteandRead(Iitem.Name) + ".lsl", Convert.ToString(notecardContent));
                    lbStatus.Text = "Created: " + dinfo.FullName + "\\" + MIUtils.CleanForWriteandRead(Iitem.Name) + ".lsl";
                    AppendLineStatusBox(lbStatus.Text);
                    //Write item's key and the image's file path into log file...
                    Logs.ImageLogEntry(Iitem.UUID.ToStringHyphenated() + "," + (dinfo.FullName + "\\" + MIUtils.CleanForWriteandRead(Iitem.Name) + ".lsl") + " # " + Iitem.Name, File.AppendText(Common.ImageLog));
                }
                else if ((Iitem.InventoryType == InventoryType.Snapshot && chkPhotoAlbum.Checked == true) ||
          (Iitem.InventoryType == InventoryType.Texture && chkPhotoAlbum.Checked == true))
                {
                    if (Iitem.ParentUUID == PhotoAlbumUUID)
                    {
                        DirectoryInfo dinfo = Directory.CreateDirectory(Common.backupRootFolder + "\\" + client.Self.Name + "\\My Inventory\\Photo Album");
                        string newfilepath = dinfo.FullName + "\\" + MIUtils.CleanForWriteandRead(Iitem.Name) + ".jpg";
                        Console.WriteLine("[SLMIV]:DownloadInventoryBase: Fetching Snapshot {0}", Iitem.Name);//Debug
                        lbStatus.Text = ("Fetching Snapshot: " + Iitem.Name);
                        AppendLineStatusBox(lbStatus.Text);
                        PendingImageDownloads.Enqueue(new QueuedDownloadInfo(newfilepath, Iitem));
                        //Write item's key and the image's file path into log file...
                        Logs.ImageLogEntry(Iitem.UUID.ToStringHyphenated() + "," + (newfilepath) + " # " + Iitem.Name, File.AppendText(Common.ImageLog));

                    }
                }
                if (Iitem.InventoryType == InventoryType.Texture && chkTextures.Checked == true)
                {
                    //Do not duplicate Photo Album images
                    if (Iitem.ParentUUID != PhotoAlbumUUID)
                    {
                        DirectoryInfo dinfo = Directory.CreateDirectory(Common.backupRootFolder + "\\" + client.Self.Name + "\\" + CurrentPathToString());
                        string newfilepath = dinfo.FullName + "\\" + MIUtils.CleanForWriteandRead(Iitem.Name) + ".jpg";
                        Console.WriteLine("[SLMIV]:DownloadInventoryBase: Fetching Texture {0}", Iitem.Name);//Debug
                        lbStatus.Text = ("Fetching Texture: " + Iitem.Name);
                        AppendLineStatusBox(lbStatus.Text);
                        PendingImageDownloads.Enqueue(new QueuedDownloadInfo(newfilepath, Iitem));
                        //Write item's key and the image's file path into log file...
                        Logs.ImageLogEntry(Iitem.UUID.ToStringHyphenated() + "," + (newfilepath) + " # " + Iitem.Name, File.AppendText(Common.ImageLog));
                    }
                }
                lbStatus.Update();
            }
        }

        #endregion

    }
}