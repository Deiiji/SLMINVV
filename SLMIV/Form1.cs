// SL My Inventory Viewer v2.7.0
// by Seneca Taliaferro/Joseph P. Socoloski III (Minoa)
// Copyright 2007. All Rights Reserved.
// NOTE:   View your Inventory cache offline and out-world.
// WHAT'S NEW: 	
//  - Fixed SL Connection progressbar bug.
//  - Better SL Connection error handling.
//  - Added SL Grid Status browser window on SL Connection tab.
//  - Added .gz compression support for opening .inv within .gz cache files
//      (Special Thanks to Eugenio).
//
// LIMITS:  -No in-world features enabled while connected to SL
// TODO:    -When connected to SL, download notecard and script objects
//LICENSE
//BY DOWNLOADING AND USING, YOU AGREE TO THE FOLLOWING TERMS:
//If it is your intent to use this software for non-commercial purposes, 
//such as in academic research, this software is free and is covered under 
//the GNU GPL License, given here: <http://www.gnu.org/licenses/gpl.txt> 
////////////////////////////////////////////////////////////////////////////
#region using
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
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
            netcom.NetcomSync = this;
            AddNetcomEvents();//Needed for SLNetCom
        }

        #region variables
        public StringCollection SColl = new StringCollection();
        public StringBuilder SBuild = new StringBuilder();
        public TreeView BackupTreeView = new TreeView();

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
        /// Send the current inventory to the mainlog
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void sendSearchToLogToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //Append all of the treeView1 to the log file
            StringBuilder sb_output = new StringBuilder();
            MIUtils.INV_TYPE typeofobject;
            //(Item)curitemobj = MyInventory.Item.Create("");

            if (cbSearchbyCreatorkey.Checked)
            {
                #region For UIDKey search in the inventory "Creator Id"
                foreach (TreeNode childNode in treeView1.Nodes)
                {
                    foreach (TreeNode tn0 in treeView1.Nodes)
                    {
                        object objinfo = new object();
                        objinfo = MIUtils.GetInvObj(tn0.Name, MIUtils.OD_treeindex, out typeofobject);
                        if (typeofobject == MIUtils.INV_TYPE.ITEM)
                        {
                            if (((Item)objinfo).permission.creator_id == tbSearchbox.Text)
                            {
                                //Copy the node selected...
                                sb_output.AppendLine(tn0.FullPath);
                            }
                        }
                        foreach (TreeNode tn1 in tn0.Nodes)
                        {
                            objinfo = MIUtils.GetInvObj(tn1.Name, MIUtils.OD_treeindex, out typeofobject);
                            if (typeofobject == MIUtils.INV_TYPE.ITEM)
                            {
                                if (((Item)objinfo).permission.creator_id == tbSearchbox.Text)
                                {
                                    //Copy the node selected...
                                    sb_output.AppendLine(tn1.FullPath);
                                }
                            }
                            foreach (TreeNode tn2 in tn1.Nodes)
                            {
                                objinfo = MIUtils.GetInvObj(tn2.Name, MIUtils.OD_treeindex, out typeofobject);
                                if (typeofobject == MIUtils.INV_TYPE.ITEM)
                                {
                                    if (((Item)objinfo).permission.creator_id == tbSearchbox.Text)
                                    {
                                        //Copy the node selected...
                                        sb_output.AppendLine(tn2.FullPath);
                                    }
                                }
                                foreach (TreeNode tn3 in tn2.Nodes)
                                {
                                    objinfo = MIUtils.GetInvObj(tn3.Name, MIUtils.OD_treeindex, out typeofobject);
                                    if (typeofobject == MIUtils.INV_TYPE.ITEM)
                                    {
                                        if (((Item)objinfo).permission.creator_id == tbSearchbox.Text)
                                        {
                                            //Copy the node selected...
                                            sb_output.AppendLine(tn3.FullPath);
                                        }
                                    }
                                    foreach (TreeNode tn4 in tn3.Nodes)
                                    {
                                        objinfo = MIUtils.GetInvObj(tn4.Name, MIUtils.OD_treeindex, out typeofobject);
                                        if (typeofobject == MIUtils.INV_TYPE.ITEM)
                                        {
                                            if (((Item)objinfo).permission.creator_id == tbSearchbox.Text)
                                            {
                                                //Copy the node selected...
                                                sb_output.AppendLine(tn4.FullPath);
                                            }
                                        }
                                        foreach (TreeNode tn5 in tn4.Nodes)
                                        {
                                            objinfo = MIUtils.GetInvObj(tn5.Name, MIUtils.OD_treeindex, out typeofobject);
                                            if (typeofobject == MIUtils.INV_TYPE.ITEM)
                                            {
                                                if (((Item)objinfo).permission.creator_id == tbSearchbox.Text)
                                                {
                                                    //Copy the node selected...
                                                    sb_output.AppendLine(tn5.FullPath);
                                                }
                                            }
                                            foreach (TreeNode tn6 in tn5.Nodes)
                                            {
                                                objinfo = MIUtils.GetInvObj(tn6.Name, MIUtils.OD_treeindex, out typeofobject);
                                                if (typeofobject == MIUtils.INV_TYPE.ITEM)
                                                {
                                                    if (((Item)objinfo).permission.creator_id == tbSearchbox.Text)
                                                    {
                                                        //Copy the node selected...
                                                        sb_output.AppendLine(tn6.FullPath);
                                                    }
                                                }
                                                foreach (TreeNode tn7 in tn6.Nodes)
                                                {
                                                    objinfo = MIUtils.GetInvObj(tn7.Name, MIUtils.OD_treeindex, out typeofobject);
                                                    if (typeofobject == MIUtils.INV_TYPE.ITEM)
                                                    {
                                                        if (((Item)objinfo).permission.creator_id == tbSearchbox.Text)
                                                        {
                                                            //Copy the node selected...
                                                            sb_output.AppendLine(tn7.FullPath);
                                                        }
                                                    }
                                                    foreach (TreeNode tn8 in tn7.Nodes)
                                                    {
                                                        objinfo = MIUtils.GetInvObj(tn8.Name, MIUtils.OD_treeindex, out typeofobject);
                                                        if (typeofobject == MIUtils.INV_TYPE.ITEM)
                                                        {
                                                            if (((Item)objinfo).permission.creator_id == tbSearchbox.Text)
                                                            {
                                                                //Copy the node selected...
                                                                sb_output.AppendLine(tn8.FullPath);
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    Logs.LogEntry("Creator ID search result for' " + userkey + "'\r\n" + sb_output.ToString(), File.AppendText(Common.MainLog));
                    MessageBox.Show("Appended Creator ID search results for\r\n'" + userkey + "'\r\nPress Inventory then 'Reload Original' menu button\r\nto restore the original inventory tree.", "Log entry complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                #endregion For UIDKey search in the inventory "Creator Id"
            }
            else
            {
                #region For text search in the inventory item's Name
                foreach (TreeNode childNode in treeView1.Nodes)
                {
                    foreach (TreeNode tn0 in treeView1.Nodes)
                    {
                        if (tn0.Text.IndexOf(tbSearchbox.Text, 0, StringComparison.OrdinalIgnoreCase) != -1)
                        {
                            //Copy the node selected...
                            sb_output.AppendLine(tn0.FullPath);
                        }
                        foreach (TreeNode tn1 in tn0.Nodes)
                        {
                            if (tn1.Text.IndexOf(tbSearchbox.Text, 0, StringComparison.OrdinalIgnoreCase) != -1)
                            {
                                //Copy the node selected...
                                sb_output.AppendLine(tn1.FullPath);
                            }
                            foreach (TreeNode tn2 in tn1.Nodes)
                            {
                                if (tn2.Text.IndexOf(tbSearchbox.Text, 0, StringComparison.OrdinalIgnoreCase) != -1)
                                {
                                    //Copy the node selected...
                                    sb_output.AppendLine(tn2.FullPath);
                                }
                                foreach (TreeNode tn3 in tn2.Nodes)
                                {
                                    if (tn3.Text.IndexOf(tbSearchbox.Text, 0, StringComparison.OrdinalIgnoreCase) != -1)
                                    {
                                        //Copy the node selected...
                                        sb_output.AppendLine(tn3.FullPath);
                                    }
                                    foreach (TreeNode tn4 in tn3.Nodes)
                                    {
                                        if (tn4.Text.IndexOf(tbSearchbox.Text, 0, StringComparison.OrdinalIgnoreCase) != -1)
                                        {
                                            //Copy the node selected...
                                            sb_output.AppendLine(tn4.FullPath);
                                        }
                                        foreach (TreeNode tn5 in tn4.Nodes)
                                        {
                                            if (tn5.Text.IndexOf(tbSearchbox.Text, 0, StringComparison.OrdinalIgnoreCase) != -1)
                                            {
                                                //Copy the node selected...
                                                sb_output.AppendLine(tn5.FullPath);
                                            }
                                            foreach (TreeNode tn6 in tn5.Nodes)
                                            {
                                                if (tn6.Text.IndexOf(tbSearchbox.Text, 0, StringComparison.OrdinalIgnoreCase) != -1)
                                                {
                                                    //Copy the node selected...
                                                    sb_output.AppendLine(tn6.FullPath);
                                                }
                                                foreach (TreeNode tn7 in tn6.Nodes)
                                                {
                                                    if (tn7.Text.IndexOf(tbSearchbox.Text, 0, StringComparison.OrdinalIgnoreCase) != -1)
                                                    {
                                                        //Copy the node selected...
                                                        sb_output.AppendLine(tn7.FullPath);
                                                    }
                                                    foreach (TreeNode tn8 in tn7.Nodes)
                                                    {
                                                        if (tn8.Text.IndexOf(tbSearchbox.Text, 0, StringComparison.OrdinalIgnoreCase) != -1)
                                                        {
                                                            //Copy the node selected...
                                                            sb_output.AppendLine(tn8.FullPath);
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    Logs.LogEntry("Examine from search result' " + tbSearchbox.Text + "'\r\n" + sb_output.ToString(), File.AppendText(Common.MainLog));
                    MessageBox.Show("Appended search results for\r\n'" + tbSearchbox.Text + "'\r\nPress Inventory then 'Reload Original' menu button\r\nto restore the original inventory tree.", "Log entry complete", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    //Show the Log tab
                    tabControl1.SelectedTab = tabControl1.TabPages["tabLogs"];
                }
                #endregion For text search in the inventory item's Name
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

            //Refresh all the SL values and their labels and buttons
            RefreshSLValues();

            //Create the log text file
            Logs.CreateMainLogFile(this.tbLog);

            //See if ImageLog exists, if not create an empty one
            if (!File.Exists(Common.ImageLog))
            {
                Logs.CreateImageLogFile();
            }
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
        private void InsertUnderParent(string parent_id, string idkey, string title, int imageindex, string elementname)
        {
            try
            {
                foreach (TreeNode tn0 in treeView1.Nodes)
                {
                    if (tn0.Name == parent_id)
                    {
                        tn0.Nodes.Insert(tn0.Nodes.Count + 1, idkey, title, imageindex);
                        //Add a tag for xml Element creation
                        tn0.Nodes[tn0.Nodes.Count - 1].Tag = elementname;

                    }
                    foreach (TreeNode tn1 in tn0.Nodes)
                    {
                        if (tn1.Name == parent_id)
                        {
                            tn1.Nodes.Insert(tn1.Nodes.Count + 1, idkey, title, imageindex);
                            //Add a tag for xml Element creation
                            tn1.Nodes[tn1.Nodes.Count - 1].Tag = elementname;
                            break;
                        }
                        foreach (TreeNode tn2 in tn1.Nodes)
                        {
                            if (tn2.Name == parent_id)
                            {
                                tn2.Nodes.Insert(tn2.Nodes.Count + 1, idkey, title, imageindex);
                                //Add a tag for xml Element creation
                                tn2.Nodes[tn2.Nodes.Count - 1].Tag = elementname;
                                break;
                            }
                            foreach (TreeNode tn3 in tn2.Nodes)
                            {
                                if (tn3.Name == parent_id)
                                {
                                    tn3.Nodes.Insert(tn3.Nodes.Count + 1, idkey, title, imageindex);
                                    //Add a tag for xml Element creation
                                    tn3.Nodes[tn3.Nodes.Count - 1].Tag = elementname;
                                    break;
                                }
                                foreach (TreeNode tn4 in tn3.Nodes)
                                {
                                    if (tn4.Name == parent_id)
                                    {
                                        tn4.Nodes.Insert(tn4.Nodes.Count + 1, idkey, title, imageindex);
                                        //Add a tag for xml Element creation
                                        tn4.Nodes[tn4.Nodes.Count - 1].Tag = elementname;
                                        break;
                                    }
                                    foreach (TreeNode tn5 in tn4.Nodes)
                                    {
                                        if (tn5.Name == parent_id)
                                        {
                                            tn5.Nodes.Insert(tn5.Nodes.Count + 1, idkey, title, imageindex);
                                            //Add a tag for xml Element creation
                                            tn5.Nodes[tn5.Nodes.Count - 1].Tag = elementname;
                                            break;
                                        }
                                        foreach (TreeNode tn6 in tn5.Nodes)
                                        {
                                            if (tn6.Name == parent_id)
                                            {
                                                tn6.Nodes.Insert(tn6.Nodes.Count + 1, idkey, title, imageindex);
                                                //Add a tag for xml Element creation
                                                tn6.Nodes[tn6.Nodes.Count - 1].Tag = elementname;
                                                break;
                                            }
                                            foreach (TreeNode tn7 in tn6.Nodes)
                                            {
                                                if (tn7.Name == parent_id)
                                                {
                                                    tn7.Nodes.Insert(tn7.Nodes.Count + 1, idkey, title, imageindex);
                                                    //Add a tag for xml Element creation
                                                    tn7.Nodes[tn7.Nodes.Count - 1].Tag = elementname;
                                                    break;
                                                }
                                                foreach (TreeNode tn8 in tn7.Nodes)
                                                {
                                                    if (tn8.Name == parent_id)
                                                    {
                                                        tn8.Nodes.Insert(tn8.Nodes.Count + 1, idkey, title, imageindex);
                                                        //Add a tag for xml Element creation
                                                        tn8.Nodes[tn8.Nodes.Count - 1].Tag = elementname;
                                                        break;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
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
                            InsertUnderParent(ftemp.parent_id, ftemp.cat_id, ftemp.name, imageindex, ftemp.type);
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
                            InsertUnderParent(Itemp.parent_id, Itemp.item_id, Itemp.name, imageindex, Itemp.inv_type);
                        }
                    }
                    countindex = countindex + 1;
                }

                MIUtils.INV_TYPE Invtype;

                //Let's get the Avatar's UUID, using 'Photo Album' 'My Inventory' is sometimes blank
                try
                {
                    userkey = ((Folder)MIUtils.GetMyInventoryObjectByText("Photo Album", out Invtype)).owner_id;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("CreateTreeView throw: Could not find 'Photo Album' folder to get avatar's uuid.\r\nTry logging into SL using the SL Viewer to recreate your cache file. Make sure you open your inventory while you are in-world.\r\n" + ex.Message, "Could not find 'Photo Album' Folder", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

                    //Make sure Quit button is enabled
                    btQuit.Enabled = true;

                    //Disable the login txtboxes
                    tbFirstName.Enabled = false;
                    tbLastName.Enabled = false;
                    tbPassword.Enabled = false;
                    cbxLocation.Enabled = false;
                    chkbStayConnected.Enabled = false;

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
            //Don't do anything until we are logged in
      //      while (!netcom.IsLoggedIn)
      //      {
      //          pBarConnection.Update();
      //          this.Focus();
      //          this.Update();
                //Thread.Sleep(1);
      //      }

            //Get the avatar's key
            //LLUUID id = new LLUUID();
            userkey = client.Self.ID.ToStringHyphenated();
            //userkey = id.ToStringHyphenated();

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

            Cursor.Current = Cursors.Default;

            //Go to the MyInventory tab
            tabControl1.SelectedTab = tabControl1.TabPages["tabMyInv"];
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

            (new SLMIV.Utils.Forms.frmDisconnected(e)).ShowDialog();
        }

        #endregion SLComm methods

        #region SLNetConn Inventory methods
        private void treeView1_AfterExpand(object sender, TreeViewEventArgs e)
        {
            if (netcom != null)
            {
                if (netcom.IsLoggedIn)
                {
                    if (e.Node.Nodes[0].Tag == null)
                    {
                        InventoryFolder folder = (InventoryFolder)e.Node.Tag;
                        client.Inventory.RequestFolderContents(folder.UUID, client.Self.ID, true, true, false, InventorySortOrder.ByName);

                        ProcessIncomingObject(folder);
                    }

                    e.Node.ImageKey = "OpenFolder";
                }
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
                ofd.InitialDirectory = Application.StartupPath;
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

            //move to loginin event

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

            BeginLogin();//must break here to allow SLNetCom events
            //netcom_ClientLoginStatus is then fired, which if succesfull, DoSLInventory() is called

            Cursor.Current = Cursors.Default;
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
            sfd.InitialDirectory = Application.StartupPath;
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
                        InsertUnderParent(ftemp.parent_id, ftemp.cat_id, ftemp.name, imageindex, ftemp.type);
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
                            InsertUnderParent(Itemp.parent_id, Itemp.item_id, Itemp.name, imageindex, Itemp.inv_type);
                            foundcount = foundcount + 1;
                        }
                        else
                        {
                            if (Itemp.name.ToLower().Contains(tofind))//Case insensitive
                            {
                                //Add the child node
                                InsertUnderParent(Itemp.parent_id, Itemp.item_id, Itemp.name, imageindex, Itemp.inv_type);
                                foundcount = foundcount + 1;
                            }
                        }
                    }
                }
                countindex = countindex + 1;
            }
            // Reset the cursor to the default for all controls.
            Cursor.Current = Cursors.Default;

            treeView1.ExpandAll();
            // Begin repainting the TreeView.
            treeView1.EndUpdate();

            //UPDATE THE TREEVIEW - DONE////////////////////////////////////////////////////////////////
            this.label1.Text = "My Inventory: (" + Convert.ToString(foundcount) + " entries found)";
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
                                }

                                //now show RIGHTCLICK MENU...
                                this.ContextMenuStrip = contextMenuStrip1;
                                this.ContextMenuStrip.Show();
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
            //ofd.InitialDirectory = Application.StartupPath + "\\images";
            ofd.InitialDirectory = Application.StartupPath;
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



    }
}