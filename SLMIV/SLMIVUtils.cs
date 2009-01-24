using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.IO.Compression;
using System.Xml;
using Wintellect.PowerCollections;
using MyInventory;
using libsecondlife;

namespace SLMIV.Utils
{
    #region Common
    public class Common
    {
        public static string SLMIVPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\SLMIV";
        public static string MainLog = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\SLMIV\\mainlog.txt";
        public static string ImageLog = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\SLMIV\\imagelog.txt";
        public static string SLConnectLog = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\SLMIV\\slconnectlog.txt";
        public static string TempInv = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\SLMIV\\tempinv.inv";
        public static string invFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\SLMIV\\inv";
        public static string urlSLMIV = @"http://slmiv.googlecode.com";
        public static string urlFaq = @"http://www.joeswammi.com/sl/se/slmyinventory/faq.html";
        public static string urlShop = @"http://xstreetsl.com/modules.php?name=Marketplace&MerchantID=240644";//http://shop.onrez.com/Seneca_Taliaferro
        public static string urlSupport = @"http://code.google.com/p/slmiv/issues/list";
        public static string backupRootFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\SLMIV\\backup";
        public static string cur_invFile = "";
        public static string GridMainLogin = @"https://login.agni.lindenlab.com/cgi-bin/login.cgi";
        public static string GridBetaLogin = @"https://login.aditi.lindenlab.com/cgi-bin/login.cgi";
        //http://osgrid.org:8002 opensim
        //http://logingrid.net:8002 openlife [not LLSD enabled]
    }
    #endregion Common

    #region Global Events
    public enum Hooks
    {
        AttachLocalFile,
        BackupItem
    };
    #endregion Global Events

    #region Logs
    public class Logs
    {
        public static bool CreateMainLogFile(TextBox tb)
        {
            try
            {
                //clear old text in old log
                File.WriteAllText(Common.MainLog, "");

                using (StreamWriter w = File.AppendText(Common.MainLog))
                {
                    LogEntry(DateTime.Now.ToLongTimeString() + " " + DateTime.Now.ToShortDateString() + "\r\nCreated main log file successfully!", w);
                }
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return false;
            }
        }

        public static void LogEntry(String logMessage, TextWriter w)
        {
            w.Write("Log Entry :\r\n");
            //w.WriteLine("{0} {1}", DateTime.Now.ToLongTimeString(), DateTime.Now.ToShortDateString());
            w.WriteLine(logMessage);
            w.WriteLine("---------------------------------------");
            // Update the underlying file.
            w.Flush();
            // Close the writer and underlying file.
            w.Close();
            //Update the tbLog
            //tb.AppendText(File.ReadAllText(Common.MainLog));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public static bool CreateImageLogFile()
        {
            try
            {
                //clear old text in old log
                File.WriteAllText(Common.ImageLog, "");

                using (StreamWriter w = File.AppendText(Common.ImageLog))
                {
                    ImageLogEntry("", w);
                }
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="logMessage"></param>
        /// <param name="w"></param>
        public static void ImageLogEntry(String logMessage, TextWriter w)
        {
            w.WriteLine(logMessage);
            // Update the underlying file.
            w.Flush();
            // Close the writer and underlying file.
            w.Close();
            //Update the tbLog
            //tb.AppendText(File.ReadAllText(Common.MainLog));
        }

        /// <summary>
        /// Searches the StringCollection for the item's imagepath
        /// </summary>
        /// <param name="key">SL created key representing the Folder or Item</param>
        /// <param name="stringc">StringCollection of the current ImageLog file</param>
        /// <returns></returns>
        public static string Findfilepath(string key, StringCollection stringc)
        {
            string path = "";
            //Look for the entire line
            StringEnumerator SCE = stringc.GetEnumerator();
            while (SCE.MoveNext())
            {
                if (SCE.Current.Contains(key))
                    path = SCE.Current;
            }

            path = path.Substring(path.IndexOf(","), path.Length).Trim();

            return path;
        }
    }

    #endregion Logs

    public class MIUtils
    {
        #region Variables
        /// <summary>
        /// StringBuilder that holds a .inv file
        /// </summary>
        public static StringBuilder SB_MyInventory = new StringBuilder();

        /// <summary>
        /// StringCollection of each folder or item taken from SB_MyInventory
        /// </summary>
        public static StringCollection SC_MyInvInBrackets = new StringCollection();

        /// <summary>
        /// Master index object which contains each Folder or Item object for the inventory.
        /// OrderedDictionary<int, Object> created from SC_MyInvInBrackets
        /// </summary>
        public static Wintellect.PowerCollections.OrderedDictionary<int, Object> OD_MyInventory = new OrderedDictionary<int, Object>();

        /// <summary>
        /// For reverse searching. Searching by UUID
        /// </summary>
        public static Wintellect.PowerCollections.OrderedDictionary<String, int> OD_treeindex = new OrderedDictionary<String, int>();

        /// <summary>
        /// PowerCollections.Bag used to store each word hit
        /// </summary>
        public static Wintellect.PowerCollections.Bag<string> WordBag = new Bag<string>();

        /// <summary>
        /// PowerCollections.OrderedMultiDictionary stores [word,hits] for CreateWordHitList()
        /// </summary>
        public static Wintellect.PowerCollections.OrderedMultiDictionary<int, String> WordDictCount = new OrderedMultiDictionary<int, String>(true);

        public enum INV_TYPE
        {
            FOLDER,
            ITEM,
            NULL
        }
        #endregion Variables

        public MIUtils()
        {
        }

        #region Index Creation Methods

        /// <summary>
        /// Takes the local copy of SB_MyInventory=inv_file
        /// then extracts all of the bracketchunks into SC_MyInvInBrackets
        /// </summary>
        /// <param name="inv_file"></param>
        public static void SplitintoSC(StringBuilder inv_file)
        {
            SC_MyInvInBrackets.Clear();

            inv_file = inv_file.Replace("\tinv_category", "!SEP!\tinv_category");
            inv_file = inv_file.Replace("\tinv_item", "!SEP!\tinv_item");

            string[] stringSeparators = new string[] { "!SEP!" };
            //Add all of the entries in the SC...
            SC_MyInvInBrackets.AddRange(inv_file.ToString().Trim().Split(stringSeparators, StringSplitOptions.RemoveEmptyEntries));
        }

        /// <summary>
        /// Appends a .inv formatted StringCollection to the OrderedDictionary OD_MyInventory
        /// </summary>
        /// <param name="SC_inv">.inv formatted StringCollection</param>
        public static void ConvertToOD(StringCollection SC_inv)
        {
            //Reset the OD_MyInventory
            OD_MyInventory.Clear();
            int icount = 0;

            // Enumerates the elements in the StringCollection.
            StringEnumerator myEnumerator = SC_inv.GetEnumerator();
            while (myEnumerator.MoveNext())
            {
                if (myEnumerator.Current != "")
                {
                    if (GetInvType(myEnumerator.Current) == INV_TYPE.FOLDER)
                    {
                        ///Folder tempf = Folder.Create(myEnumerator.Current);
                        ///Convert to Folder and assign in MD
                        ///OD_MyInventory.Add(tempf.cat_id, Folder.Create(myEnumerator.Current));
                        OD_MyInventory.Add(icount, Folder.Create(myEnumerator.Current));
                    }
                    else if (GetInvType(myEnumerator.Current) == INV_TYPE.ITEM)
                    {
                        ///Item tempi = Item.Create(myEnumerator.Current);
                        if (icount == 16)
                            icount = icount + 0;
                        OD_MyInventory.Add(icount, Item.Create(myEnumerator.Current));
                    }
                    else
                    {
                        MessageBox.Show("ConvertToOD exception: " + myEnumerator.Current + " is INV_TYPE.NULL");
                    }
                }
                icount = icount + 1;
            }
        }

        #endregion Index Creation Methods

        #region Get from Index Methods
        /// <summary>
        /// Returns a MyInventory.Item object.
        /// </summary>
        /// <param name="uidkey">Name UUID Key</param>
        /// <param name="invtype">INV_TYPE object</param>
        /// <returns></returns>
        public static Object GetInvObj(string uidkey, OrderedDictionary<String, int> objectdict, out INV_TYPE invtype)
        {
            invtype = MIUtils.INV_TYPE.NULL;
            Object objinfo = new Object();
            int iNodeindex = 0;
            objectdict.TryGetValue(uidkey, out iNodeindex);
            if (iNodeindex != -1)
            {
                MIUtils.INV_TYPE nodeObjtype;
                objinfo = MIUtils.GetMyInventoryObject(iNodeindex, out nodeObjtype);
                if (nodeObjtype == MIUtils.INV_TYPE.ITEM)
                {
                    invtype = INV_TYPE.ITEM;
                }
                else
                    invtype = INV_TYPE.FOLDER;
            }

            return objinfo;
        }

        /// <summary>
        /// Search the OrderedDictionary index OD_MyInventory by index number to return the SLMIV Folder or Item object.
        /// </summary>
        /// <param name="index">index of the object in OD_MyInventory</param>
        /// <param name="invtype">INV_TYPE of the object found</param>
        /// <returns>Returns a FOLDER or ITEM object from inventory and item's INV_TYPE</returns>
        public static Object GetMyInventoryObject(int index, out INV_TYPE invtype)
        {
            Object objectfound = new Object();
            INV_TYPE temp = INV_TYPE.NULL;

            OD_MyInventory.TryGetValue(index, out objectfound);

            if (objectfound.GetType().ToString() == "MyInventory.Folder")
            {
                temp = INV_TYPE.FOLDER;
            }
            else
            {
                temp = INV_TYPE.ITEM;
            }

            invtype = temp;
            return objectfound;
        }

        /// <summary>
        /// Search the OrderedDictionary index OD_MyInventory by Name string to return the SLMIV Folder or Item object.
        /// </summary>
        /// <param name="text_title">name string of inventory folder or item</param>
        /// <param name="invtype">INV_TYPE of the object found</param>
        /// <returns>Returns a FOLDER or ITEM object from inventory and item's INV_TYPE</returns>
        public static Object GetMyInventoryObjectByText(string text_title, out INV_TYPE invtype)
        {
            Object objectfound = new Object();
            INV_TYPE temp = INV_TYPE.NULL;

            foreach (KeyValuePair<int, Object> pair in OD_MyInventory)
            {
                if (pair.Value.GetType().ToString() == "MyInventory.Folder")
                {
                    temp = INV_TYPE.FOLDER;
                    Folder ftemp = (Folder)pair.Value;
                    if (ftemp.name == text_title)
                    {
                        objectfound = pair.Value;
                        break;
                    }
                }
                else
                {
                    temp = INV_TYPE.ITEM;
                    Item Itemp = (Item)pair.Value;
                    if (Itemp.name == text_title)
                    {
                        objectfound = pair.Value;
                        break;
                    }
                }
            }

            invtype = temp;
            return objectfound;
        }

        /// <summary>
        /// Reads a .inv formatted line to return its INV_TYPE 
        /// </summary>
        /// <param name="inv_line">a .inv formatted line</param>
        /// <returns>INV_TYPE</returns>
        public static INV_TYPE GetInvType(string inv_line)
        {
            if (inv_line.IndexOf("\tinv_category") != -1)
                return INV_TYPE.FOLDER;
            else if (inv_line.IndexOf("\tinv_item") != -1)
                return INV_TYPE.ITEM;
            else
                return INV_TYPE.NULL;
        }

        /// <summary>
        /// Evaluates the Item's inv_type tag, or Folder's pref_type to return an imagelist value
        /// </summary>
        /// <param name="invtype">Item's inv_type tag</param>
        /// <returns>imagelist value</returns>
        public static int Getinv_type(string invtype)
        {
            invtype = invtype.ToLower();

            int imageindex = 0;
            //What type of item is it?
            switch (invtype)
            {
                case "-1":
                    imageindex = 0;
                    break;
                case "category":
                    imageindex = 0;
                    break;
                case "categoryopen":
                    imageindex = 1;
                    break;
                case "trash":
                    imageindex = 2;
                    break;
                case "trashfolder":
                    imageindex = 2;
                    break;
                case "texture":
                    imageindex = 3;
                    break;
                case "rootcategory":
                    imageindex = 3;
                    break;
                case "gesture":
                    imageindex = 4;
                    break;
                case "object":
                    imageindex = 5;
                    break;
                case "wearable":
                    imageindex = 6;
                    break;
                case "script":
                    imageindex = 7;
                    break;
                case "lsl":
                    imageindex = 7;
                    break;
                case "snapshot":
                    imageindex = 8;
                    break;
                case "sound":
                    imageindex = 9;
                    break;
                case "notecard":
                    imageindex = 10;
                    break;
                case "landmark":
                    imageindex = 11;
                    break;
                default:
                    imageindex = 5;//"callingcard","attachment"
                    break;
            }

            return imageindex;
        }

        #endregion Get from Index Methods

        /// <summary>
        /// Read a .gz inventory cache and read its .inv into SLMIV
        /// (posted by Eugenio) modified by Joseph P. Socoloski III
        /// </summary>
        /// <param name="fileName">full path and filename</param>
        /// <returns>properly formatted .inv string</returns>
        private static string ReadGZInventory(String fileName)
        {
            if (!fileName.EndsWith(".gz"))
                return File.ReadAllText(fileName);

            FileStream fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
            GZipStream gzip = new GZipStream(fileStream, CompressionMode.Decompress);
            StreamReader reader = new StreamReader(gzip);
            StringBuilder sb = new StringBuilder();
            while (!reader.EndOfStream)
            {
                sb.Append(reader.ReadToEnd());
            }
            return sb.ToString();
        }

        /// <summary>
        /// Browse to the extracted .inv file and set the StringBuilder
        /// </summary>
        /// <returns>Returns -1 if failed</returns>
        public static int BrowsetoInvFile(out string filename)
        {
            filename = "";
            int result = 0;
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "gz files (*.gz)|*.gz|inv files (*.inv)|*.inv|All files (*.*)|*.*";
            //ofd.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            ofd.InitialDirectory = Common.SLMIVPath;
            ofd.ShowDialog();

            try
            {
                if (ofd.FileName != string.Empty)
                {
                    //Seperate all of the text in the path
                    string[] str = ofd.FileName.Split('\\');
                    str = str[str.Length - 1].Split('.');

                    //See if the filename is a key, if so it is probably user's
                    string[] uidstr = str[0].Split('-');

                    if (uidstr.Length == 5)
                    {
                        filename = str[0];
                    }
                    else
                        filename = "";

                    //Clear SB_MyInventory
                    SB_MyInventory.Remove(0, SB_MyInventory.Length);

                    if (ofd.FileName.Trim().EndsWith(".gz"))
                    {
                        //Read in the .gz into a StringBuilder...
                        SB_MyInventory.AppendLine(ReadGZInventory(ofd.FileName));

                        //Take the SB and seperate it into a StringCollection...
                        SplitintoSC(SB_MyInventory);

                        //Convert each line in the SC in a Folder or Item object, and place into the main MultiDictionary
                        ConvertToOD(SC_MyInvInBrackets);
                    }
                    else if (ofd.FileName.Trim().EndsWith(".inv"))
                    {
                        //Read in the .inv into a StringBuilder...
                        SB_MyInventory.AppendLine(File.ReadAllText(ofd.FileName));

                        //Take the SB and seperate it into a StringCollection...
                        SplitintoSC(SB_MyInventory);

                        //Convert each line in the SC in a Folder or Item object, and place into the main MultiDictionary
                        ConvertToOD(SC_MyInvInBrackets);
                    }
                    else
                    {
                        //not valid choice
                        result = -1;
                    }

                }
                else
                {
                    result = -1;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

            return result;
        }

        /// <summary>
        /// Write and Element to the XmlTextWriter from the treenodecollection
        /// </summary>
        /// <param name="xmltwriter">XmlTextWriter</param>
        /// <param name="treenodecoll">TreeNodeCollection</param>
        public static void TreeViewXMLWriteRecursive(XmlTextWriter xmltwriter, TreeNodeCollection treenodecoll)
        {
            INV_TYPE itype = INV_TYPE.NULL;
            foreach (TreeNode tn0 in treenodecoll)
            {
                xmltwriter.WriteStartElement(Convert.ToString(tn0.Tag));
                xmltwriter.WriteAttributeString("name", tn0.Text);
                xmltwriter.WriteAttributeString("uuid", tn0.Name);
                ImageWriteAttributeString(xmltwriter, tn0);
                //xmlWriter.WriteValue(Utils.MIUtils.GetMyInventoryObjectByText(tn0.Text, out itype).ToString());
                xmltwriter.WriteValue(Utils.MIUtils.GetInvObj(tn0.Name, MIUtils.OD_treeindex, out itype).ToString());

                if (tn0.Nodes.Count > 0)
                    TreeViewXMLWriteRecursive(xmltwriter, tn0.Nodes);
            }
        }

        /// <summary>
        /// Take the current treeView and convert it into a readable xml file
        /// </summary>
        /// <param name="treeViewOne">Populated treeView with Tag value set</param>
        /// <param name="filename">desired xml path and filename</param>
        /// <returns>true if successful</returns>
        public static bool TreeViewToXML(TreeView treeViewOne, string filename)
        {//http://www.devhood.com/tutorials/tutorial_details.aspx?tutorial_id=773
            bool successfull = false;

            try
            {
                XmlDocument xmlDoc = new XmlDocument();
                XmlNode root = xmlDoc.DocumentElement;
                XmlTextWriter xmlWriter = new XmlTextWriter(filename, System.Text.Encoding.UTF8);
                xmlWriter.Formatting = Formatting.Indented;
                xmlWriter.WriteProcessingInstruction("xml", "version='1.0' encoding='UTF-8'");
                //xmlWriter.WriteStartElement("inventory");

                TreeViewXMLWriteRecursive(xmlWriter, treeViewOne.Nodes);

                //  xmlWriter.WriteEndElement();
                xmlWriter.Close();
                successfull = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error thrown in TreeViewToXML(): " + ex.Message);
                successfull = false;
            }
            return successfull;
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
            foreach (TreeNode tn0 in treenodecoll)
            {
                if (tn0.Name == parent_id)
                {
                    tn0.Nodes.Insert(tn0.Nodes.Count + 1, idkey, title, imageindex);
                    //Add a tag for xml Element creation
                    tn0.Nodes[tn0.Nodes.Count + 1].Tag = elementname;
                }
                this.InsertUnderParent(parent_id, idkey, title, imageindex, elementname, tn0.Nodes);
            }
        }

        /// <summary>
        /// Helper to TreeViewToXML
        /// </summary>
        /// <param name="xmlw"></param>
        /// <param name="node"></param>
        public static void ImageWriteAttributeString(XmlTextWriter xmlw, TreeNode node)
        {
            INV_TYPE invtype = INV_TYPE.NULL;
            Object invobject = Utils.MIUtils.GetMyInventoryObjectByText(node.Text, out invtype);
            if (invtype == INV_TYPE.FOLDER)
            {
                //if it is the "Trash" can, then reassign properly
                if (node.Text == "Trash")
                    xmlw.WriteAttributeString("image", Convert.ToString(Getinv_type(((Folder)invobject).pref_type)));
                else
                    xmlw.WriteAttributeString("image", Convert.ToString(Getinv_type(((Folder)invobject).type)));
            }
            else
            {
                xmlw.WriteAttributeString("image", Convert.ToString(Getinv_type(((Item)invobject).type)));
            }
        }

        /// <summary>
        /// Takes an SLMIV xml file and converts it into SLMIV index variables to create Folder and Item objects for treeView1
        /// </summary>
        /// <param name="filename">desired xml path and filename</param>
        public static void CreateIndexesFromXML(string filename)
        {
            try
            {
                if (File.Exists(filename))
                {
                    //Clear SB_MyInventory, SC_MyInvInBrackets, OD_MyInventory
                    SB_MyInventory.Remove(0, SB_MyInventory.Length);
                    SC_MyInvInBrackets.Clear();
                    OD_MyInventory.Clear();

                    //Read the xml
                    //append each element into the 

                    System.Xml.XmlReader xmlr = XmlReader.Create(File.OpenRead(filename));
                    //xmlr.MoveToContent();
                    string line = "";
                    bool bCategory = false;
                    while (xmlr.Read())
                    {
                        if (xmlr.NodeType == XmlNodeType.Element)
                        {
                            if (xmlr.Name == "category")
                            {
                                //inv_category
                                bCategory = true;
                            }
                            else
                            {
                                //inv_item
                                bCategory = false;
                            }
                        }

                        if (xmlr.NodeType == XmlNodeType.Text)
                        {
                            if (bCategory)
                            {
                                line = Folder.ToSCLineFromString(xmlr.Value);
                                //Add the correctly formatted line to the StringCollection index
                                SC_MyInvInBrackets.Add(line);
                            }
                            else
                            {
                                line = Item.ToSCLineFromString(xmlr.Value);
                                //Add the correctly formatted line to the StringCollection index
                                SC_MyInvInBrackets.Add(line);
                            }
                        }

                    }
                    //Create the OD_MyInventory
                    ConvertToOD(SC_MyInvInBrackets);

                }
                else
                    MessageBox.Show(filename + "\r\ndoes not exist");
            }
            catch (Exception ex)
            {
                MessageBox.Show("CreateIndexesFromXML error: " + ex.Message);
            }
        }

        /// <summary>
        /// Converts an image into an icon.
        /// </summary>
        /// <param name="img">The image that shall become an icon</param>
        /// <param name="size">The width and height of the icon. Standard
        /// sizes are 16x16, 32x32, 48x48, 64x64.</param>
        /// <param name="keepAspectRatio">Whether the image should be squashed into a
        /// square or whether whitespace should be put around it.</param>
        /// <returns>An icon!!</returns>
        public static Icon MakeIcon(System.Drawing.Image img, int size, bool keepAspectRatio)
        {
            Bitmap square = new Bitmap(size, size); // create new bitmap
            Graphics g = Graphics.FromImage(square); // allow drawing to it

            int x, y, w, h; // dimensions for new image

            if (!keepAspectRatio || img.Height == img.Width)
            {
                // just fill the square
                x = y = 0; // set x and y to 0
                w = h = size; // set width and height to size
            }
            else
            {
                // work out the aspect ratio
                float r = (float)img.Width / (float)img.Height;

                // set dimensions accordingly to fit inside size^2 square
                if (r > 1)
                { // w is bigger, so divide h by r
                    w = size;
                    h = (int)((float)size / r);
                    x = 0; y = (size - h) / 2; // center the image
                }
                else
                { // h is bigger, so multiply w by r
                    w = (int)((float)size * r);
                    h = size;
                    y = 0; x = (size - w) / 2; // center the image
                }
            }

            // make the image shrink nicely by using HighQualityBicubic mode
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.DrawImage(img, x, y, w, h); // draw image with specified dimensions
            g.Flush(); // make sure all drawing operations complete before we get the icon

            // following line would work directly on any image, but then
            // it wouldn't look as nice.
            return Icon.FromHandle(square.GetHicon());
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="inventoryDict">Dictionary(LLUUID, InventoryItem)</param>
        /// <returns>Dictionary(InventoryType, int)</returns>
        public static Dictionary<InventoryType, int> CountInventoryItems(Dictionary<LLUUID, InventoryItem> inventoryDict, LLUUID palbumuuid)
        {
            Dictionary<InventoryType, int> countedDict = new Dictionary<InventoryType, int>();
            //Add the keys we will be counting
            countedDict.Add(InventoryType.Notecard, 0);
            countedDict.Add(InventoryType.LSL, 0);
            countedDict.Add(InventoryType.Texture, 0);
            countedDict.Add(InventoryType.Snapshot, 0);

            foreach (InventoryItem ii in inventoryDict.Values)
            {
                if (ii.InventoryType == InventoryType.Notecard)
                {
                    int pastcount = 0;
                    countedDict.TryGetValue(InventoryType.Notecard, out pastcount);
                    countedDict.Remove(InventoryType.Notecard);
                    countedDict.Add(InventoryType.Notecard, pastcount + 1);
                }
                if (ii.InventoryType == InventoryType.LSL)
                {
                    int pastcount = 0;
                    countedDict.TryGetValue(InventoryType.LSL, out pastcount);
                    countedDict.Remove(InventoryType.LSL);
                    countedDict.Add(InventoryType.LSL, pastcount + 1);
                }
                if (ii.InventoryType == InventoryType.Texture)
                {
                    int pastcount = 0;
                    countedDict.TryGetValue(InventoryType.Texture, out pastcount);
                    countedDict.Remove(InventoryType.Texture);
                    countedDict.Add(InventoryType.Texture, pastcount + 1);
                }
                if (ii.ParentUUID == palbumuuid)
                {
                    int pastcount = 0;
                    countedDict.TryGetValue(InventoryType.Snapshot, out pastcount);
                    countedDict.Remove(InventoryType.Snapshot);
                    countedDict.Add(InventoryType.Snapshot, pastcount + 1);
                }

            }

            return countedDict;
        }

        /// <summary>
        /// Removes Invalid chars necessary to create folders and filenames
        /// \,/,:,*,?,",arrow-left,>,|
        /// </summary>
        /// <param name="str">String</param>
        /// <returns>String</returns>
        public static String CleanForWriteandRead(String str)
        {
            //Craft a valid filename
            String validName = str;
            validName = validName.Replace("\\", "");
            validName = validName.Replace("/", "");
            validName = validName.Replace(":", "");
            validName = validName.Replace("*", "");
            validName = validName.Replace("?", "");
            validName = validName.Replace("\"", "");
            validName = validName.Replace("<", "");
            validName = validName.Replace(">", "");
            validName = validName.Replace("|", "");

            return validName;
        }

        /// <summary>
        /// Adds words to WordBag, strips the following chars out of TreeNode.Text
        /// seperators=" ", ".", "\\", "/", ":", "*", "?", ">", "arrow-left", "|", "[", "]", ";", "(", ")", "+", "-", "="
        /// </summary>
        /// <param name="treenodecoll"></param>
        public static void TreeViewWordRecursive(TreeNodeCollection treenodecoll)
        {
            foreach (TreeNode tn0 in treenodecoll)
            {
                //tn0.Tag, name:tn0.Text, uuid:tn0.Name
                string Name = tn0.Text;
                string[] seperators = new string[] { " ", ",", "@", ".", "\\", "/", ":", "*", "?", ">", "<", "|", "[", "]", ";", "(", ")", "+", "-", "=" };

                //Remove all odd characters from the name
                //Split the item's name into its words
                WordBag.AddMany(Name.Split(seperators, StringSplitOptions.RemoveEmptyEntries));

                if (tn0.Nodes.Count > 0)
                    TreeViewWordRecursive(tn0.Nodes);
            }
        }

        /// <summary>
        /// Searches the TreeView node Text for repeating word
        /// </summary>
        /// <param name="treeViewOne">TreeView</param>
        /// <returns>List Tstring></returns>
        public static List<string> CreateWordHitList(TreeView treeViewOne)
        {
            //New search so refresh WordBag
            WordBag.Clear();

            TreeViewWordRecursive(treeViewOne.Nodes);//Create the word list

            List<string> ResultsList = new List<string>();

            foreach (string var in WordBag)
            {
                //if(!WordDictCount.ContainsKey(var))
                WordDictCount.Add(WordBag.NumberOfCopies(var), var);
            }

            foreach (KeyValuePair<int, string> pair in WordDictCount.Reversed().KeyValuePairs)
            {
                string text = pair.Value + " (" + Convert.ToString(pair.Key) + ")";
                if (!ResultsList.Contains(text))
                {
                    ResultsList.Add(text);
                }
            }

            return ResultsList;
        }

        #region not being used
        /// <summary>
        /// Gets the first bracket chunk (inv_category or inv_item), 
        /// Adds it to the main SC, then removes it from 
        /// </summary>
        /// <param name="inv_file"></param>
        /// <returns></returns>
        public StringBuilder GetFirstBracketChunk(StringBuilder inv_file)
        {
            StringBuilder returningSB = new StringBuilder();
            int startpos = 0;
            int endpos = 0;

            if (inv_file.ToString().IndexOf("inv_category") != -1)
            {   //We found a Folder
                startpos = inv_file.ToString().IndexOf("inv_category");
                endpos = FoundLastBracket(inv_file);

                //Add the chunk to the SC
                SC_MyInvInBrackets.Add(inv_file.ToString().Substring(0, endpos));

                //Remove the chunk from the SB (leave the tab);
                inv_file = inv_file.Remove(0, endpos - 1);

                returningSB = inv_file;

            }
            else if (inv_file.ToString().IndexOf("inv_item") != -1)
            {   //We Found an Item
                startpos = inv_file.ToString().IndexOf("inv_item");
                endpos = FoundLastBracket(inv_file);

                //Add the chunk to the SC
                SC_MyInvInBrackets.Add(inv_file.ToString().Substring(0, endpos));

                //Remove the chunk from the SB (leave the tab);
                inv_file = inv_file.Remove(0, endpos - 1);

                returningSB = inv_file;
            }
            else
            {
                //We can not find neither a Folder or item next in the inventory SB
                returningSB = null;
            }
            return returningSB;
        }

        /// <summary>
        /// Gets the last nv_category or inv_item bracket position by
        /// seeing the nv_category or inv_item after itself
        /// </summary>
        /// <param name="inv_file"></param>
        /// <returns></returns>
        private int FoundLastBracket(StringBuilder inv_file)
        {
            int endpos = 0;
            string maintemp = inv_file.ToString();
            //Remove the first two characters so it will not return a false positive find
            maintemp = maintemp.Remove(0, 4);

            //Find the first { openingbracket

            if (maintemp.IndexOf("nv_category") != -1)//If there is another bracket
            {
                endpos = maintemp.IndexOf("nv_category");
                endpos = endpos + 4; //Add the two that we removed from the begining
                endpos = endpos - 1; //Subtract one to get before the indexof
            }
            else if (maintemp.IndexOf("inv_item") != -1)
            {
                endpos = maintemp.IndexOf("inv_item");
                endpos = endpos + 4; //Add the two that we removed from the begining
                endpos = endpos - 1; //Subtract one to get before the indexof
            }
            else
                endpos = -1;

            return endpos;
        }
        #endregion not being used

    }
}