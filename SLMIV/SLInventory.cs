// SL My Inventory Viewer v2.10.11
// by Seneca Taliaferro/Joseph P. Socoloski III (Minoa)
// Copyright 2008-2009. All Rights Reserved.
// NOTE:   View your Inventory cache offline and out-world.
// WHAT'S NEW: 	
//  - FIXED: Issue 3: Can only span out to 8 folders of inventory. [InsertUnderParent, TreeViewToXML, sendSearchToLogToolStripMenuItem_Click recursive]
//  - FIXED: Issue 5: Images do not open when right-clicking on an object
//  - FIXED: Issue 6: Download Notecards and scripts
//  - FIXED: Issue 7: 'Could not find Photo Album folder' exception
//  - FIXED: Issue 9: Doing two SL Connections in a row causes freezing
//  - FIXED: Stopped right-click menu from first draw in upperleft corner
//  - NEW FEATURE: Backup Scripts, Notecards, Textures, and other images in your inventory as one job. (Auto-adds to your imagelog)
//  - NEW FEATURE: Backup Scripts, Notecards, Textures, and other images individually by right-click->Download on an item.(Auto-adds to your imagelog)
//  - NEW FEATURE: Issue 10: Connect to other grids (buggy because of other grid permissions and interfaces)
//  - NEW FEATURE: SL Connection Logging for troublehshooting
//  - NEW FEATURE: 'Create Word Hit List': Lists highest to lowest of word frequencies found in the current treeview inventory
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
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.Collections.Specialized;
using System.IO;
using Wintellect.PowerCollections;
using System.Drawing;
using System.Net;

namespace MyInventory
{
    /// <summary>
    /// Object class to hold all of the the details for each SalesInfo inventory object
    /// </summary>
    class SalesInfo
    {
        /// <summary>
        /// the avatar's id key of the owner
        /// </summary>
        public string sale_type
        {
            get { return _sale_type; }
            set { _sale_type = value; }
        }
        string _sale_type = null;

        public int sale_price
        {
            get { return _sale_price; }
            set { _sale_price = value; }
        }
        int _sale_price;

        public SalesInfo(string _sale_type, int _sale_price)
        {
            sale_type = _sale_type;
            sale_price = _sale_price;
        }

        //"sale_type\tnot\tsale_price\t10"
        public static SalesInfo Create(string sc_inv_line)
        {
            string[] seperators = new string[] { "sale_type\t", "\tsale_price\t" };
            string[] allpieces = sc_inv_line.ToString().Split(seperators, StringSplitOptions.None);

            string Sale_type = allpieces[1].Trim();
            int Sale_price = Convert.ToInt32(allpieces[2].Trim());

            return new SalesInfo(Sale_type, Sale_price);
        }

        public override string ToString()
        {
            string mainstring = "";
            mainstring += "Sale type:\t" + sale_type + "\r\n";              // The text of the object (shown in the inventory)
            mainstring += "Sale Price L$:\t" + Convert.ToString(sale_price) + "\r\n";
            return mainstring;
        }
    }

    /// <summary>
    /// Object class to hold all of the the details for each Folder inventory object
    /// </summary>
    class Folder
    {
        /// <summary>
        /// the id of the object
        /// </summary>
        public string cat_id
        {
            get { return _cat_id; }
            set { _cat_id = value; }
        }
        string _cat_id = null;

        /// <summary>
        /// the object it belongs to
        /// </summary>
        public string parent_id
        {
            get { return _parent_id; }
            set { _parent_id = value; }
        }
        string _parent_id = null;

        /// <summary>
        /// category, , etc
        /// </summary>
        public string type
        {
            get { return _type; }
            set { _type = value; }
        }
        string _type = null;

        /// <summary>
        /// category
        /// </summary>
        public string pref_type
        {
            get { return _pref_type; }
            set { _pref_type = value; }
        }
        string _pref_type = null;

        /// <summary>
        /// The text of the object (shown in the inventory)
        /// </summary>
        public string name
        {
            get { return _name; }
            set { _name = value; }
        }
        string _name = null;

        /// <summary>
        /// the avatar's id key of the owner
        /// </summary>
        public string owner_id
        {
            get { return _owner_id; }
            set { _owner_id = value; }
        }
        string _owner_id = null;

        public int version
        {
            get { return _version; }
            set { _version = value; }
        }
        int _version;

        //Holds its treenode index value
        public int tnodeindex
        {
            get { return _tnodeindex; }
            set { _tnodeindex = value; }
        }
        int _tnodeindex;

        public Folder(string _cat_id, string _parent_id, string _type, string _pref_type, string _name, string _owner_id, int _version)
        {
            cat_id = _cat_id;
            parent_id = _parent_id;//the object it belongs to
            type = _type; //category, , etc
            pref_type = _pref_type; // category
            name = _name; // The text of the object (shown in the inventory)
            owner_id = _owner_id; // the avatar's id key of the owner
            version = _version;

            //Not necessary for init creation:
            tnodeindex = _tnodeindex;
        }
        //"\tinv_category\t0\n\t{\n\t\tcat_id\t62385c1b-a37c-4168-8127-9b4b0ca28cc3\n\t\tparent_id\t00000000-0000-0000-0000-000000000000\n\t\ttype\tcategory\n\t\tpref_type\tcategory\n\t\tname\tMy Inventory|\n\t\towner_id\t2bbff43c-e25d-4514-9c28-81bceb372b9c\n\t\tversion\t296\n\t}\r\n"
        //"\tinv_category\t0{\tcat_id\t62385c1b-a37c-4168-8127-9b4b0ca28cc3\tparent_id\t00000000-0000-0000-0000-000000000000\ttype\tcategory\tpref_type\tcategory\tname\tMy Inventory|\towner_id\t2bbff43c-e25d-4514-9c28-81bceb372b9c\tversion\t296}\r\n"
        //"cat_id\t62385c1b-a37c-4168-8127-9b4b0ca28cc3\tparent_id\t00000000-0000-0000-0000-000000000000\ttype\tcategory\tpref_type\tcategory\tname\tMy Inventory|\towner_id\t2bbff43c-e25d-4514-9c28-81bceb372b9c\tversion\t296"
        public static Folder Create(string sc_inv_line)
        {
            //Remove the begining and brackets
            sc_inv_line = sc_inv_line.Replace("\n\t", "");
            sc_inv_line = sc_inv_line.Replace("\tinv_category\t0{\t", "");
            sc_inv_line = sc_inv_line.Replace("}\r\n", "");
            sc_inv_line = sc_inv_line.Replace("}", "");

            string[] seperators = new string[] { "cat_id\t", "\tparent_id\t", "\ttype\t", "\tpref_type\t", "\tname\t", "\towner_id\t", "\tversion\t" };
            string[] allpieces = sc_inv_line.ToString().Split(seperators, StringSplitOptions.None);

            string Cat_id = allpieces[1].Trim();
            string Parent_id = allpieces[2].Trim();    //the object it belongs to
            string fType = allpieces[3].Trim();        //category, , etc
            string Pref_type = allpieces[4].Trim();    // category
            string Name = "";
            if (allpieces[5].Trim().EndsWith("|"))
                Name = allpieces[5].Remove(allpieces[5].Length - 1, 1).Trim();
            else
                Name = allpieces[5].Trim();         // The text of the object (shown in the inventory)
            string Owner_id = allpieces[6].Trim();     // the avatar's id key of the owner
            int Version = Convert.ToInt32(allpieces[7].Trim());

            return new Folder(Cat_id, Parent_id, fType, Pref_type, Name, Owner_id, Version);
        }

        /// <summary>
        /// Returns all info in string format
        /// </summary>
        /// <returns>all info in string format</returns>
        public override string ToString()
        {
            string mainstring = "";
            mainstring += "Name:\t" + name + "\r\n";              // The text of the object (shown in the inventory)
            mainstring += "Id Key:\t" + cat_id + "\r\n";
            mainstring += "Owner Id Key:\t" + owner_id + "\r\n";  // the avatar's id key of the owner
            mainstring += "Parent Id:\t" + parent_id + "\r\n";    //the object it belongs to
            mainstring += "Type:\t" + type + "\r\n";              //category, , etc
            mainstring += "Pref Type:\t" + pref_type + "\r\n";
            mainstring += "Version:\t" + Convert.ToString(version) + "\r\n";
            return mainstring;
        }

        /// <summary>
        /// Returns all info in the inv stringcollection format
        /// </summary>
        /// <returns>all info in string format</returns>
        public static string ToSCLineFromString(string tostring)
        {
            tostring = tostring.Replace("\tinv_category\t0{\t", "");
            tostring = tostring.Replace("}\r\n", "");
            tostring = tostring.Replace("}", "");

            string[] seperators = new string[] { "Name:\t", "Id Key:\t", "Owner Id Key:\t", "Parent Id:\t", "Type:\t", "Pref Type:\t", "Version:\t" };
            string[] allpieces = tostring.ToString().Split(seperators, StringSplitOptions.None);

            string Name = allpieces[1].Trim();         // The text of the object (shown in the inventory)
            string Cat_id = allpieces[2].Trim();
            string Owner_id = allpieces[3].Trim();     // the avatar's id key of the owner
            string Parent_id = allpieces[4].Trim();    //the object it belongs to
            string fType = allpieces[5].Trim();        //category, , etc
            string Pref_type = allpieces[6].Trim();    // category
            string Version = allpieces[7].Trim();

            string mainstring = "\tinv_category\t0{\t";
            mainstring += "cat_id\t" + Cat_id;// The text of the object (shown in the inventory)
            mainstring += "\tparent_id\t" + Parent_id;//the object it belongs to
            mainstring += "\ttype\t" + fType;              //category, , etc
            mainstring += "\tpref_type\t" + Pref_type;
            mainstring += "\tname\t" + Name;
            mainstring += "\towner_id\t" + Owner_id;  // the avatar's id key of the owner
            mainstring += "\tversion\t" + Version;
            mainstring += "}";
            return mainstring;
        }
    }

    /// <summary>
    /// Object class to hold all of the the details for each Item inventory object (non-Folders)
    /// </summary>
    class Item
    {
        /// <summary>
        /// the id of the object
        /// </summary>
        public string item_id
        {
            get { return _item_id; }
            set { _item_id = value; }
        }
        string _item_id = null;

        /// <summary>
        /// the object it belongs to
        /// </summary>
        public string parent_id
        {
            get { return _parent_id; }
            set { _parent_id = value; }
        }
        string _parent_id = null;

        public string asset_id
        {
            get { return _asset_id; }
            set { _asset_id = value; }
        }
        string _asset_id;

        /// <summary>
        /// category, , etc
        /// </summary>
        public string type
        {
            get { return _type; }
            set { _type = value; }
        }
        string _type = null;

        /// <summary>
        /// category
        /// </summary>
        public string inv_type
        {
            get { return _inv_type; }
            set { _inv_type = value; }
        }
        string _inv_type = null;

        public string flags
        {
            get { return _flags; }
            set { _flags = value; }
        }
        string _flags = null;

        /// <summary>
        /// The text of the object (shown in the inventory)
        /// </summary>
        public string name
        {
            get { return _name; }
            set { _name = value; }
        }
        string _name = null;

        /// <summary>
        /// The text desc of the object (shown in the inventory)
        /// </summary>
        public string desc
        {
            get { return _desc; }
            set { _desc = value; }
        }
        string _desc = null;

        /// <summary>
        /// the avatar's id key of the owner
        /// </summary>
        public string creation_date
        {
            get { return _creation_date; }
            set { _creation_date = value; }
        }
        string _creation_date = null;

        /// <summary>
        /// 
        /// </summary>
        public permissions permission
        {
            get { return _permission; }
            set { _permission = value; }
        }
        permissions _permission = null;

        /// <summary>
        /// 
        /// </summary>
        public SalesInfo salesinfo
        {
            get { return _salesinfo; }
            set { _salesinfo = value; }
        }
        SalesInfo _salesinfo = null;

        public Item(string _item_id, string _parent_id, string _asset_id, string _type, string _inv_type, string _flags, string _name, string _desc, string _creation_date, permissions _permission, SalesInfo _salesinfo)
        {
            item_id = _item_id;
            parent_id = _parent_id;
            asset_id = _asset_id;
            type = _type;
            inv_type = _inv_type;
            flags = _flags;
            name = _name;
            desc = _desc;
            creation_date = _creation_date;
            permission = _permission;
            salesinfo = _salesinfo;
        }

        //"\tinv_item\t0\n\t{\n\t\titem_id\t064c58be-30e1-d5ff-ff8c-94b25bf17e8d\n\t\tparent_id\t12a86241-ab3d-49c0-ae75-c067cede6cb4\n\tpermissions 0\n\t{\n\t\tbase_mask\t7fffffff\n\t\towner_mask\t7fffffff\n\t\tgroup_mask\t00000000\n\t\teveryone_mask\t00000000\n\t\tnext_owner_mask\t7fffffff\n\t\tcreator_id\t2bbff43c-e25d-4514-9c28-81bceb372b9c\n\t\towner_id\t2bbff43c-e25d-4514-9c28-81bceb372b9c\n\t\tlast_owner_id\t00000000-0000-0000-0000-000000000000\n\t\tgroup_id\t00000000-0000-0000-0000-000000000000\n\t}\n\t\tasset_id\t94362507-033d-9680-d18b-1baf24d0da83\n\t\ttype\ttexture\n\t\tinv_type\tsnapshot\n\t\tflags\t00000000\n\tsale_info\t0\n\t{\n\t\tsale_type\tnot\n\t\tsale_price\t10\n\t}\n\t\tname\tSeneca's Buildings|\n\t\tdesc\tSeneca's buildings|\n\t\tcreation_date\t1161405411\n\t}\r\n"
        //"\tinv_item\t0{\titem_id\t064c58be-30e1-d5ff-ff8c-94b25bf17e8d\tparent_id\t12a86241-ab3d-49c0-ae75-c067cede6cb4permissions 0{\tbase_mask\t7fffffff\towner_mask\t7fffffff\tgroup_mask\t00000000\teveryone_mask\t00000000\tnext_owner_mask\t7fffffff\tcreator_id\t2bbff43c-e25d-4514-9c28-81bceb372b9c\towner_id\t2bbff43c-e25d-4514-9c28-81bceb372b9c\tlast_owner_id\t00000000-0000-0000-0000-000000000000\tgroup_id\t00000000-0000-0000-0000-000000000000}\tasset_id\t94362507-033d-9680-d18b-1baf24d0da83\ttype\ttexture\tinv_type\tsnapshot\tflags\t00000000sale_info\t0{\tsale_type\tnot\tsale_price\t10}\tname\tSeneca's Buildings|\tdesc\tSeneca's buildings|\tcreation_date\t1161405411}\r\n"
        //"item_id\t064c58be-30e1-d5ff-ff8c-94b25bf17e8d\tparent_id\t12a86241-ab3d-49c0-ae75-c067cede6cb4permissions 0{\tbase_mask\t7fffffff\towner_mask\t7fffffff\tgroup_mask\t00000000\teveryone_mask\t00000000\tnext_owner_mask\t7fffffff\tcreator_id\t2bbff43c-e25d-4514-9c28-81bceb372b9c\towner_id\t2bbff43c-e25d-4514-9c28-81bceb372b9c\tlast_owner_id\t00000000-0000-0000-0000-000000000000\tgroup_id\t00000000-0000-0000-0000-000000000000}\tasset_id\t94362507-033d-9680-d18b-1baf24d0da83\ttype\ttexture\tinv_type\tsnapshot\tflags\t00000000sale_info\t0{\tsale_type\tnot\tsale_price\t10}\tname\tSeneca's Buildings|\tdesc\tSeneca's buildings|\tcreation_date\t1161405411}\r\n"
        //"item_id\t064c58be-30e1-d5ff-ff8c-94b25bf17e8d\tparent_id\t12a86241-ab3d-49c0-ae75-c067cede6cb4permissions 0{\tbase_mask\t7fffffff\towner_mask\t7fffffff\tgroup_mask\t00000000\teveryone_mask\t00000000\tnext_owner_mask\t7fffffff\tcreator_id\t2bbff43c-e25d-4514-9c28-81bceb372b9c\towner_id\t2bbff43c-e25d-4514-9c28-81bceb372b9c\tlast_owner_id\t00000000-0000-0000-0000-000000000000\tgroup_id\t00000000-0000-0000-0000-000000000000}\tasset_id\t94362507-033d-9680-d18b-1baf24d0da83\ttype\ttexture\tinv_type\tsnapshot\tflags\t00000000sale_info\t0{\tsale_type\tnot\tsale_price\t10}\tname\tSeneca's Buildings|\tdesc\tSeneca's buildings|\tcreation_date\t1161405411"
        public static Item Create(string sc_inv_line)
        {
            //Remove the begining and brackets
            sc_inv_line = sc_inv_line.Replace("\n\t", "");
            sc_inv_line = sc_inv_line.Replace("\tinv_item\t0{\t", "");
            sc_inv_line = sc_inv_line.Replace("}\r\n", "");
            //sc_inv_line = sc_inv_line.Replace("}", "");

            string[] seperators = new string[] { "item_id\t", "\tparent_id\t", "permissions 0{\t", "}\tasset_id\t", "}\tshadow_id\t", "\ttype\t", "\tinv_type\t", "\tflags\t", "sale_info\t0{\t", "}\tname\t", "\tdesc\t", "\tcreation_date\t" };
            string[] allpieces = sc_inv_line.ToString().Split(seperators, StringSplitOptions.None);

            string Item_id = allpieces[1].Trim();
            string Parent_id = allpieces[2].Trim();    //the object it belongs to
            string Perms = allpieces[3].Trim();//"base_mask\t7fffffff\towner_mask\t7fffffff\tgroup_mask\t00000000\teveryone_mask\t00000000\tnext_owner_mask\t7fffffff\tcreator_id\t2bbff43c-e25d-4514-9c28-81bceb372b9c\towner_id\t2bbff43c-e25d-4514-9c28-81bceb372b9c\tlast_owner_id\t00000000-0000-0000-0000-000000000000\tgroup_id\t00000000-0000-0000-0000-000000000000"
            string Asset_id = allpieces[4].Trim();                                      //May also be shadow_id
            string sType = allpieces[5].Trim();
            string Inv_type = allpieces[6].Trim();
            string sFlags = allpieces[7].Trim();
            string sale_info = allpieces[8].Trim();                                        //"sale_type\tnot\tsale_price\t10"
            string Name = allpieces[9].Remove(allpieces[9].Length - 1, 1).Trim();             // The text of the object (shown in the inventory)
            string Desc = allpieces[10].Remove(allpieces[10].Length - 1, 1).Trim();           //Removes trailing '|'
            string Creation_date = allpieces[11].Remove(allpieces[11].Length - 2, 2).Trim();  //Removes trailing '}'

            return new Item(Item_id, Parent_id, Asset_id, sType, Inv_type, sFlags, Name, Desc, Creation_date,
                permissions.Create(Perms), SalesInfo.Create(sale_info));
        }

        /// <summary>
        /// Returns all info in string format
        /// </summary>
        /// <returns>all info in string format</returns>
        public override string ToString()
        {
            string mainstring = "";
            mainstring += "Name:\t" + name + "\r\n";              // The text of the object (shown in the inventory)
            mainstring += "Description:\t" + desc + "\r\n";
            mainstring += salesinfo.ToString();
            mainstring += "Id Key:\t" + item_id + "\r\n";
            mainstring += "Parent Id:\t" + parent_id + "\r\n";    //the object it belongs to
            mainstring += "Asset Id Key:\t" + asset_id + "\r\n";  // the avatar's id key of the owner
            mainstring += "Type:\t" + type + "\r\n";              //category, , etc
            mainstring += "Inv Type:\t" + inv_type + "\r\n";
            mainstring += "Flags:\t" + flags + "\r\n";
            mainstring += "Creation date:\t" + creation_date + "\r\n";
            mainstring += permission.ToString();
            return mainstring;
        }

        /// <summary>
        /// Returns all info in the inv stringcollection format
        /// </summary>
        /// <returns>all info in string format</returns>
        public static string ToSCLineFromString(string tostring)
        {
            //"\tinv_item\t0{\titem_id\t064c58be-30e1-d5ff-ff8c-94b25bf17e8d\tparent_id\t12a86241-ab3d-49c0-ae75-c067cede6cb4
            //permissions 0{\tbase_mask\t7fffffff\towner_mask\t7fffffff\tgroup_mask\t00000000\teveryone_mask\t00000000\tnext_owner_mask\t7fffffff
            //\tcreator_id\t2bbff43c-e25d-4514-9c28-81bceb372b9c\towner_id\t2bbff43c-e25d-4514-9c28-81bceb372b9c\tlast_owner_id
            //\t00000000-0000-0000-0000-000000000000\tgroup_id\t00000000-0000-0000-0000-000000000000}\tasset_id\t94362507-033d-9680-d18b-1baf24d0da83
            //\ttype\ttexture\tinv_type\tsnapshot\tflags\t00000000sale_info\t0{\tsale_type\tnot\tsale_price\t10}\tname\tSeneca's Buildings|
            //\tdesc\tSeneca's buildings|\tcreation_date\t1161405411}\r\n"

            string[] seperators = new string[] { "Name:", "Description:", "Sale type:", "Sale Price L$:", "Id Key:", "Parent Id:", "Asset Id Key:",
            "Type:", "Inv Type:", "Flags:", "Creation date:", "Creator Id:", "Owner Id:", "Last Owner Id:", "Group Id:", "base_mask:",
            "owner_mask:", "everyone_mask:", "next_owner_mask:"};
            string[] allpieces = tostring.ToString().Split(seperators, StringSplitOptions.None);

            string Name = allpieces[1].Trim();
            string Desc = allpieces[2].Trim();
            string Sale_type = allpieces[3].Trim();
            string Sale_price = allpieces[4].Trim();
            string Item_id = allpieces[5].Trim();
            string Parent_id = allpieces[6].Trim();    //the object it belongs to
            string Asset_id = allpieces[7].Trim();                                      //May also be shadow_id
            string sType = allpieces[8].Trim();
            string Inv_type = allpieces[9].Trim();
            string sFlags = allpieces[10].Trim();
            string Creation_date = allpieces[11].Trim();
            string Creator_id = allpieces[12].Trim();
            string Owner_id = allpieces[13].Trim();
            string LastOwner_id = allpieces[14].Trim();
            string Group_id = allpieces[15].Trim();
            string Base_mask = allpieces[16].Trim();
            string Owner_mask = allpieces[17].Trim();
            string Group_mask = "\t";
            string Everyone_mask = allpieces[18].Trim();
            string Next_owner_mask = allpieces[19].Trim();

            string mainstring = "\tinv_item\t0{";
            mainstring += "\titem_id\t" + Item_id;
            mainstring += "\tparent_id\t" + Parent_id;
            mainstring += "permissions 0{";
            mainstring += "\tbase_mask\t" + Base_mask;
            mainstring += "\towner_mask\t" + Owner_mask;
            mainstring += "\tgroup_mask\t" + Group_mask;
            mainstring += "\teveryone_mask\t" + Everyone_mask;
            mainstring += "\tnext_owner_mask\t" + Next_owner_mask;
            mainstring += "\tcreator_id\t" + Creator_id;
            mainstring += "\towner_id\t" + Owner_id;
            mainstring += "\tlast_owner_id\t" + LastOwner_id;
            mainstring += "\tgroup_id\t" + Group_id + "}";
            mainstring += "\tasset_id\t" + Asset_id;
            mainstring += "\ttype\t" + sType;
            mainstring += "\tinv_type\t" + Inv_type;
            mainstring += "\tflags\t" + sFlags;
            mainstring += "sale_info\t0{";
            mainstring += "\tsale_type\t" + Sale_type;
            mainstring += "\tsale_price\t" + Sale_price + "}";
            mainstring += "\tname\t" + Name + "|";
            mainstring += "\tdesc\t" + Desc + "|";
            mainstring += "\tcreation_date\t" + Creation_date + "}";
            return mainstring;
        }
    }

    /// <summary>
    /// Object class to hold all of the the details for all inventory object permission settings
    /// </summary>
    class permissions
    {
        /// <summary>
        /// the id of the creator
        /// </summary>
        public string base_mask
        {
            get { return _base_mask; }
            set { _base_mask = value; }
        }
        string _base_mask = null;

        /// <summary>
        /// the id of the creator
        /// </summary>
        public string owner_mask
        {
            get { return _owner_mask; }
            set { _owner_mask = value; }
        }
        string _owner_mask = null;

        /// <summary>
        /// the id of the creator
        /// </summary>
        public string group_mask
        {
            get { return _group_mask; }
            set { _group_mask = value; }
        }
        string _group_mask = null;

        /// <summary>
        /// the id of the creator
        /// </summary>
        public string everyone_mask
        {
            get { return _everyone_mask; }
            set { _everyone_mask = value; }
        }
        string _everyone_mask = null;

        /// <summary>
        /// the id of the creator
        /// </summary>
        public string next_owner_mask
        {
            get { return _next_owner_mask; }
            set { _next_owner_mask = value; }
        }
        string _next_owner_mask = null;

        /// <summary>
        /// the id of the creator
        /// </summary>
        public string creator_id
        {
            get { return _creator_id; }
            set { _creator_id = value; }
        }
        string _creator_id = null;

        /// <summary>
        /// the avatar's id key of the owner
        /// </summary>
        public string owner_id
        {
            get { return _owner_id; }
            set { _owner_id = value; }
        }
        string _owner_id = null;

        /// <summary>
        /// the id of the creator
        /// </summary>
        public string last_owner_id
        {
            get { return _last_owner_id; }
            set { _last_owner_id = value; }
        }
        string _last_owner_id = null;

        /// <summary>
        /// the id of the creator
        /// </summary>
        public string group_id
        {
            get { return _group_id; }
            set { _group_id = value; }
        }
        string _group_id = null;

        public permissions(string _base_mask, string _owner_mask, string _group_mask, string _everyone_mask, string _next_owner_mask, string _creator_id,
            string _owner_id, string _last_owner_id, string _group_id)
        {
            base_mask = _base_mask;
            owner_mask = _owner_mask;
            group_mask = _group_mask;
            everyone_mask = _everyone_mask;
            next_owner_mask = _next_owner_mask;
            creator_id = _creator_id;
            owner_id = _owner_id;
            last_owner_id = _last_owner_id;
            group_id = _group_id;
        }

        public override string ToString()
        {
            string mainstring = "";
            mainstring += "Creator Id:\t" + creator_id + "\r\n";
            mainstring += "Owner Id:\t" + owner_id + "\r\n";
            mainstring += "Last Owner Id:\t" + last_owner_id + "\r\n";
            mainstring += "Group Id:\t" + group_id + "\r\n";
            mainstring += "base_mask:\t" + base_mask + "\r\n";
            mainstring += "owner_mask:\t" + owner_mask + "\r\n";
            mainstring += "everyone_mask:\t" + everyone_mask + "\r\n";
            mainstring += "next_owner_mask:\t" + next_owner_mask + "\r\n";
            return mainstring;
        }

        //"base_mask\t7fffffff\towner_mask\t7fffffff\tgroup_mask\t00000000\teveryone_mask\t00000000\tnext_owner_mask\t7fffffff\tcreator_id\t2bbff43c-e25d-4514-9c28-81bceb372b9c\towner_id\t2bbff43c-e25d-4514-9c28-81bceb372b9c\tlast_owner_id\t00000000-0000-0000-0000-000000000000\tgroup_id\t00000000-0000-0000-0000-000000000000"
        public static permissions Create(String sc_inv_line)
        {
            string[] seperators = new string[] { "base_mask\t", "\towner_mask\t", "\tgroup_mask\t", "\teveryone_mask\t", "\tnext_owner_mask\t", "\tcreator_id\t", "\towner_id\t", "\tlast_owner_id\t", "\tgroup_id\t" };
            string[] allpieces = sc_inv_line.ToString().Split(seperators, StringSplitOptions.None);

            string Base_mask = allpieces[1].Trim();
            string Owner_mask = allpieces[2].Trim();
            string Group_mask = allpieces[3].Trim();
            string Everyone_mask = allpieces[4].Trim();
            string Next_owner_mask = allpieces[5].Trim();
            string Creator_id = allpieces[6].Trim();
            string Owner_id = allpieces[7].Trim();
            string Last_owner_id = allpieces[8].Trim();
            string Group_id = allpieces[9].Trim();

            return new permissions(Base_mask, Owner_mask, Group_mask, Everyone_mask, Next_owner_mask,
                Creator_id, Owner_id, Last_owner_id, Group_id);
        }
    }

    /// <summary>
    /// Custom ToolTip Class for the SL Inventory Items
    /// </summary>
    class SLMIVImage
    {
        /// <summary>
        /// ImageLog's path
        /// </summary>
        public string path_imagelog
        {
            get { return _path_imagelog; }
            set { _path_imagelog = value; }
        }
        string _path_imagelog = null;

        /// <summary>
        /// Image's Url
        /// </summary>
        public string path_image
        {
            get { return _path_image; }
            set { _path_image = value; }
        }
        string _path_image = null;


        /// <summary>
        /// Log file contents
        /// </summary>
        public StringBuilder sbImageLog = new StringBuilder();

        public SLMIVImage(string imagelogfile)
        {
            _path_imagelog = imagelogfile;
            ReadImageLog();
        }

        /// <summary>
        /// Reset the object
        /// </summary>
        public void Reset()
        {
            sbImageLog = new StringBuilder();
        }

        /// <summary>
        /// Reads the passed ImageLog
        /// </summary>
        /// <returns>true if file exists</returns>
        private bool ReadImageLog()
        {
            if (File.Exists(_path_imagelog))
            {
                sbImageLog.Append(File.ReadAllText(_path_imagelog));
                return true;
            }
            else
                return false;
        }

        /// <summary>
        /// Get the uuid's image if exists
        /// </summary>
        /// <param name="uuid">items' uuid</param>
        /// <returns>System.Drawing.Image</returns>
        public Image Get(string uuid)
        {
            Image image;
            ReadImageLog();

            if (sbImageLog.ToString().IndexOf(uuid) != -1)
            {
                //Image Available
                int commapoint = (sbImageLog.ToString().Trim().IndexOf(uuid) + uuid.Length + 1);
                int endofpath = (sbImageLog.ToString().IndexOf("\r\n", commapoint + 1));//find the first return after the commapoint
                string[] splitline = (sbImageLog.ToString().Trim().Substring(commapoint, endofpath - commapoint)).Split('#');//seperate out the comments
                _path_image = splitline[0].Trim();

                if (!_path_image.StartsWith("http:"))
                {
                    //or get from local path
                    if ((_path_image.EndsWith(".jpg")) || (_path_image.EndsWith(".gif")) || (_path_image.EndsWith(".bmp")))
                    {
                        image = new Bitmap(Image.FromFile(_path_image), 120, 120);
                    }
                    else
                    {
                        //the _path_image maybe a .txt or .lsl
                        image = null;
                        path_image = _path_image;
                        return image;
                    }
                }
                else
                {
                    string uuidbmplocal = SLMIV.Utils.Common.SLMIVPath + "\\images\\" + uuid + ".bmp";

                    //Get from website 
                    if (File.Exists(uuidbmplocal))
                    {
                        image = new Bitmap(Image.FromFile(uuidbmplocal), 120, 120);
                    }
                    else
                    {
                        //download the image
                        DownloadImage(_path_image, uuidbmplocal);

                        //load the image
                        image = new Bitmap(Image.FromFile(uuidbmplocal), 120, 120);
                    }
                }

                return image;
            }
            else
            {
                //"No Image";
                return new Bitmap(SLMIV.Resources.Nonecircle, 120, 120);
            }
        }

        /// <summary>
        /// Downloads the image from url and saves at new_path_filename
        /// </summary>
        /// <param name="url">url of image</param>
        /// <param name="new_path_filename">save path to local drive</param>
        private void DownloadImage(string url, string new_path_filename)
        {
            //Get the url and make sure it is not https
            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            {
                url = "http://" + url;
                //TxtUrl.Text = url;
            }

            //Create the HTTP Connection
            HttpWebRequest req;
            try
            {
                req = (HttpWebRequest)HttpWebRequest.Create(url);
            }
            catch (Exception err)
            {
                MessageBox.Show("Error: " + err.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                //TxtUrl.Focus();
                return;
            }

            //Get the WebResponse class
            HttpWebResponse resp;
            try
            {
                resp = (HttpWebResponse)req.GetResponse();
            }
            catch (WebException err)
            {
                MessageBox.Show(err.Status + " - " + err.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

                resp = (HttpWebResponse)err.Response;
                if (resp == null)
                {
                    //TxtUrl.Focus();
                    return;
                }
            }
            catch (Exception err)
            {
                MessageBox.Show("Error: " + err.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                //TxtUrl.Focus();
                return;
            }

            //Get stream 
            Stream rcvStream = resp.GetResponseStream();

            Image.FromStream(rcvStream).Save(new_path_filename);

            resp.Close();
        }

        /// <summary>
        /// Displays the object associated by calling System.Diagnostics.Process.Start
        /// </summary>
        /// <param name="uuid">items' uuid</param>
        /// <returns>false if can not be displayed</returns>
        public bool Display(string uuid)
        {
            bool successfull = false;

            try
            {
                //Get correct local path to file
                string uuidbmplocal = "";
                if (File.Exists(SLMIV.Utils.Common.SLMIVPath + "\\images\\" + uuid + ".bmp"))
                    uuidbmplocal = SLMIV.Utils.Common.SLMIVPath + "\\images\\" + uuid + ".bmp";
                else
                {
                    //Read the image log to get correct path
                    if (File.ReadAllText(SLMIV.Utils.Common.ImageLog).ToString().IndexOf(uuid) != -1)
                    {
                        StringBuilder sbImageLog = new StringBuilder();
                        if (File.Exists(SLMIV.Utils.Common.ImageLog))
                        {
                            sbImageLog.Append(File.ReadAllText(SLMIV.Utils.Common.ImageLog));
                        }
                        int commapoint = (sbImageLog.ToString().IndexOf(uuid) + uuid.Length + 1);
                        int endofpath = (sbImageLog.ToString().IndexOf("\r\n", commapoint + 1));//find the first return after the commapoint
                        //Trim and comments after line eg. "C:\\Program Files\\SL My Inventory Viewer v2\\images\\Light eyes.bmp # Light eyes"
                        endofpath = sbImageLog.ToString().IndexOf("#", commapoint + 1);
                        uuidbmplocal = sbImageLog.ToString().Substring(commapoint, endofpath - commapoint).Trim();
                    }
                }

                //Get from website 
                if (File.Exists(uuidbmplocal))
                {
                    System.Diagnostics.Process.Start(uuidbmplocal);
                    successfull = true;
                }
                else
                {
                    if (_path_image.StartsWith("http://"))
                    {
                        //download the image
                        DownloadImage(_path_image, uuidbmplocal);

                        //now display the file
                        System.Diagnostics.Process.Start(uuidbmplocal);
                        successfull = true;
                    }
                    else
                    {
                        //Must be a local file so try to display it...
                        System.Diagnostics.Process.Start(_path_image);
                        successfull = true;
                    }
                }
            }
            catch (Exception ex)
            {

            }
            return successfull;
        }
    }
}