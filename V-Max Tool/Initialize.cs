using ReMaster_Utility.Properties;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;


namespace V_Max_Tool
{
    public partial class Form1 : Form
    {
        private Thread Draw;
        private Thread circ;  // Thread for drawing circle disk image
        private Thread flat;  // Thread for drawing flat tracks image
        private Thread check_alive;
        private Thread Worker_Main;
        private Thread Worker_Alt;
        private Thread[] Job;
        private Semaphore Task_Limit = new Semaphore(3, 3);
        private readonly System.Windows.Forms.ToolTip tips = new System.Windows.Forms.ToolTip();
        private List<string> LB_File_List = new List<string>();
        private int Cores;
        private int Default_Cores;
        private int pan_defw;
        private int pan_defh;
        private bool manualRender;
        private bool DontThread = false;
        private const bool Set = false;
        private const bool Free = true;
        private readonly Gbox outbox = new Gbox();
        private readonly Gbox inbox = new Gbox();
        private readonly Color C64_screen = Color.FromArgb(69, 55, 176);   //(44, 41, 213);
        private readonly Color c64_text = Color.FromArgb(135, 122, 237);   //(114, 110, 255); 
        private bool usecpp = true;
        private bool CPP_LZ = true;
        private string def_bg_text;
        private static readonly PrivateFontCollection DirFont = new PrivateFontCollection();
        private Label[] BlkMap_track = new Label[41];
        private Label[] BlkMap_sector = new Label[21];
        //private Button[][] BlkMap_bam = new Button[41][];
        private Panel[][] BlkMap_bam = new Panel[41][];

        private readonly byte[] sector_gap_length =
            {
                //10, 10, 10, 10, 10, 10, 10, 10, 10, 10,	/*  1 - 10 */
                10, 10, 10, 10, 10, 10, 10, 10, 10, 10,	/*  1 - 10 */
            	10, 10, 10, 10, 10, 10, 10, 14, 14, 14,	/* 11 - 20 */
            	14, 14, 14, 14, 11, 11, 11, 11, 11, 11,	/* 21 - 30 */
            	8, 8, 8, 8, 8,					    	/* 31 - 35 */
            	8, 8, 8, 8, 8, 8, 8		        		/* 36 - 42 (non-standard) */
            };

        private readonly byte[] sector_gap_density =
        {
            10, 14, 11, 8
        };

        private readonly byte[] Available_Sectors =
            {
                21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21,
                19, 19, 19, 19, 19, 19, 19,
                18, 18, 18, 18, 18, 18,
                17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17,

            };

        private readonly byte[] density_map = {
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0,	/*  1 - 10 */
            	0, 0, 0, 0, 0, 0, 0, 1, 1, 1,	/* 11 - 20 */
            	1, 1, 1, 1, 2, 2, 2, 2, 2, 2,	/* 21 - 30 */
            	3, 3, 3, 3, 3,					/* 31 - 35 */
            	3, 3, 3, 3, 3, 3, 3				/* 36 - 42 (non-standard) */
            };

        private readonly int[] Sectors_by_density = { 21, 19, 18, 17 };

        private readonly byte[] GCR_encode = {
                0x0a, 0x0b, 0x12, 0x13,
                0x0e, 0x0f, 0x16, 0x17,
                0x09, 0x19, 0x1a, 0x1b,
                0x0d, 0x1d, 0x1e, 0x15
            };

        private readonly byte[] GCR_decode_high =
            {
                0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff,
                0xff, 0x80, 0x00, 0x10, 0xff, 0xc0, 0x40, 0x50,
                0xff, 0xff, 0x20, 0x30, 0xff, 0xf0, 0x60, 0x70,
                0xff, 0x90, 0xa0, 0xb0, 0xff, 0xd0, 0xe0, 0xff
            };

        private readonly byte[] GCR_decode_low =
            {
                0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff,
                0xff, 0x08, 0x00, 0x01, 0xff, 0x0c, 0x04, 0x05,
                0xff, 0xff, 0x02, 0x03, 0xff, 0x0f, 0x06, 0x07,
                0xff, 0x09, 0x0a, 0x0b, 0xff, 0x0d, 0x0e, 0xff
            };

        // 0    1     2     3     4     5    6     7
        // 8    9     0a    0b    0c    0d   0e    0f
        // 10   11    12    13    14    15   16    17
        // 18   19    1a    1b    1c    1d   1e    1f
        private readonly byte[] VPL_decode_low =
           {
                //0xff, 0xff, 0xff, 0xff, 0xff, 0x06, 0x0f, 0xff,
                0xff, 0xff, 0xff, 0xff, 0xff, 0x0e, 0x0f, 0xff,
                
                0xff, 0x00, 0x01, 0x02, 0x05, 0x03, 0x04, 0x05,
                0xff, 0xff, 0x06, 0x07, 0x0a, 0x08, 0x09, 0x0a,
                0xff, 0x0b, 0x0c, 0x0d, 0xff, 0x0e, 0x0f, 0xff,
            };

        private readonly byte[] VPL_decode_high =
            {
                //0xff, 0xff, 0xff, 0xff, 0xff, 0x60, 0xf0, 0xff,
                0xff, 0xff, 0xff, 0xff, 0xff, 0xe0, 0xf0, 0xff,

                0xff, 0x00, 0x10, 0x20, 0x50, 0x30, 0x40, 0x50,
                0xff, 0xff, 0x60, 0x70, 0xa0, 0x80, 0x90, 0xa0,
                
                //0xff, 0xb0, 0xc0, 0xd0, 0xff, 0xe0, 0x70, 0xff,
                0xff, 0xb0, 0xc0, 0xd0, 0xff, 0xe0, 0xf0, 0xff,
            };

        private readonly string[] ErrorCodes =
            {
                "null",
                "Sector OK",            // 01
                "Header not Found",     // 02
                "Sync not found",       // 03
                "Data not found",       // 04
                "Bad data checksum",    // 05
                "Bad GCR",              // 06
                "Bad header checksum",  // 09
                "ID mismatch"           // 0b (11)
            };

        private readonly string[] c1541error =
        {
            "",
            "0, Sector OK",
            "20, Block header not found",
            "21, Sync not found",
            "22, Data block not found",
            "23, Checksum error in data",
            "24, Byte decoding error",
            "",
            "",
            "27, Checksum error in header",
            "",
            "29, Disk ID mismatch"
        };

        //private readonly int[] sectorInterleave =
        //{
        //    10, 7, 5
        //};
        private readonly int[] sectorInterleave =
       {
            1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12
        };

        void Reset_to_Defaults(bool clear_batch_list = true)
        {
            busy = true;
            Img_Q.SelectedIndex = 2;
            Set_ListBox_Items(true, true, clear_batch_list);
            Import_File.Visible = f_load.Visible = false;
            Tabs.Controls.Remove(Advanced_Opts);
            if (clear_batch_list)
            {
                Batch_List_Box.Visible = false;
                Batch_List_Box.Items.Clear();
            }
            end_track = -1;
            fat_trk = -1;
            Img_style.Enabled = Img_View.Enabled = Img_opts.Enabled = Save_Circle_btn.Visible = M_render.Visible = Adv_ctrl.Enabled = false;
            VBS_info.Visible = Reg_info.Visible = false;
            Other_opts.Visible = false;
            Save_Disk.Visible = false;
            Adv_ctrl.SelectedIndex = 0;
            linkLabel1.Visible = true;
            Draw_Init_Img(def_bg_text);
            Data_Box.Clear();
            Default_Dir_Screen();
            label2.Text = string.Empty;
            Dir_Box.Items.Clear();
            busy = false;
        }

        void Set_Arrays(int len)
        {
            /// NDS is the input or source array
            NDS.Track_Data = new byte[len][];
            NDS.Sector_Zero = new int[len];
            NDS.Track_Length = new int[len];
            NDS.D_Start = new int[len];
            NDS.D_End = new int[len];
            NDS.cbm = new int[len];
            NDS.sectors = new int[len];
            NDS.Header_Len = new int[len];
            NDS.cbm_sector = new int[len][];
            NDS.v2info = new byte[len][];
            NDS.Loader = new byte[0];
            NDS.Total_Sync = new int[len];
            NDS.Disk_ID = new byte[len][];
            NDS.Gap_Sector = new int[len];
            NDS.Track_ID = new int[len];
            NDS.Prot_Method = string.Empty;
            NDS.t18_ID = new byte[4];
            NDS.Adjust = new bool[len];
            /// NDA is the destination or output array
            NDA.Track_Data = new byte[len][];
            NDA.Sector_Zero = new int[len];
            NDA.Track_Length = new int[len];
            NDA.D_Start = new int[len];
            NDA.D_End = new int[len];
            NDA.sectors = new int[len];
            NDA.Total_Sync = new int[len];
            /// NDG is the G64 arrays
            NDG.Track_Length = new int[len];
            NDG.Track_Data = new byte[len][];
            NDG.L_Rot = false;
            NDG.s_len = new int[len];
            NDG.newheader = new byte[2];
            NDG.Fat_Track = new bool[len];
            /// Original is the arrays that keep the original track data for the Auto Adjust feature
            Original.A = new byte[0];
            Original.G = new byte[0];
            Original.SA = new byte[0];
            Original.SG = new byte[0];
            Original.OT = new byte[len][];
            /// DiskDir is the arrays that handle directoy entries
            DiskDir.Entries = 0;
            DiskDir.Sectors = new byte[0][];
            DiskDir.Entry = new byte[0][];
            DiskDir.FileName = new string[0];
            Dir_Box.Items.Clear();
        }

        public static Font GetCustomFont(float fontSize, FontStyle fontStyle)
        {
            return new Font(DirFont.Families[0], fontSize, fontStyle);
        }
        int Get_Cores()
        {
            foreach (var item in new System.Management.ManagementObjectSearcher("Select NumberOfCores from Win32_Processor").Get())
            {
                int coreCount = int.Parse(item["NumberOfCores"].ToString());
                Cores += coreCount;
            }
            if (Cores == 1) manualRender = M_render.Visible = true;
            Default_Cores = Cores;
            return Cores;
        }

        void Set_Cores(bool Min_Cores_3 = true)
        {
            if (Min_Cores_3 && Cores < 3) Cores = 3;
            else DB_cores.Value = Cores;
            Task_Limit = new Semaphore(Cores, Cores);
        }

        void Set_Auto_Opts()
        {
            if (Auto_Adjust) V3_Auto_Adj.Checked = V2_Auto_Adj.Checked = VPL_auto_adj.Checked = f_load.Checked = true;
            else V3_Auto_Adj.Checked = V2_Auto_Adj.Checked = VPL_auto_adj.Checked = f_load.Checked = false;
            V2_Add_Sync.Checked = V2_Auto_Adj.Checked;
        }

        void Default_Dir_Screen()
        {
            Dir_screen.Clear();
            Dir_screen.Text = dir_def;
            Dir_screen.Select(2, 23);
            Dir_screen.SelectionBackColor = c64_text;
            Dir_screen.SelectionColor = C64_screen;
            DiskDir.Entries = 0;
            DiskDir.Sectors = new byte[0][];
            DiskDir.Entry = new byte[0][];
            DiskDir.FileName = new string[0];
            Dir_Box.Items.Clear();
        }

        void Init()
        {
            /// Remove Create Blank Disk and Directory Editor functions
            CBD_box.Visible = false;
            Dir_Edit.Visible = false;
            label19.Visible = label18.Visible = Sec_Interleave.Visible = CBD_box.Visible;
            Dir_screen.Top = CBD_box.Visible ? Dir_screen.Top : 0;
            Dir_screen.Height = tabPage3.Height;
            /// Don't allow files to be added to disk images (comment out the following lines)
            //Dir_screen.AllowDrop = true;
            //Dir_screen.DragEnter += new DragEventHandler(Dir_Screen_DragEnter);
            //Dir_screen.DragDrop += new DragEventHandler(Dir_Screen_DragDrop);


            byte[] fontData = Resources.C64_Pro_Mono_STYLE;
            FontFamily customFontFamily = LoadFontFromResource(fontData);
            Font customFont = GetCustomFont(12.0f, FontStyle.Regular);
            usecpp = Load_Dll();
            if (!usecpp) CPP_tog.Enabled = false;
            Batch_List_Box.Visible = false; // set to true for debugging that requires a listbox
            Batch_List_Box.HorizontalScrollbar = true;
            Other_opts.Visible = false;
            Advanced_Opts.Controls.Add(V2_Advanced);
            Advanced_Opts.Controls.Add(V3_Advanced);
            Advanced_Opts.Controls.Add(VPL_Advanced);
            Advanced_Opts.Controls.Add(RPL_Advanced);
            V2_Advanced.Top = V3_Advanced.Top = VPL_Advanced.Top = RPL_Advanced.Top = 0;
            V2_Advanced.Left = V3_Advanced.Left = VPL_Advanced.Left = RPL_Advanced.Left = 0;
            V2_Advanced.Visible = V3_Advanced.Visible = VPL_Advanced.Visible = RPL_Advanced.Visible = false;
            /// Directory editing box values
            groupBox3.Controls.Add(Dir_Box);
            Dir_Box.DrawMode = DrawMode.OwnerDrawFixed;
            Dir_Box.CheckOnClick = true;
            Dir_Box.UseCompatibleTextRendering = true;
            Dir_Box.Font = Dir_screen.Font = customFont;
            Dir_Box.BackColor = C64_screen;
            Dir_Box.ForeColor = c64_text;
            Dir_Box.Location = new System.Drawing.Point(1, 12);
            Dir_Box.Size = new System.Drawing.Size(groupBox3.Width - 12, 31 * 19);
            Dir_Box.FormattingEnabled = true;
            Dir_Box.MouseDown += new MouseEventHandler(Dir_Box_MouseDown);
            Dir_Box.MouseMove += new MouseEventHandler(Dir_Box_MouseMove);
            Dir_Box.MouseUp += new MouseEventHandler(Dir_Box_MouseUp);
            Dir_Box.DragOver += new DragEventHandler(Dir_Box_DragOver);
            Dir_Box.DragDrop += new DragEventHandler(Dir_Box_DragDrop);
            Dir_Box.AllowDrop = true;
            Dir_All.LinkBehavior = LinkBehavior.NeverUnderline;
            Dir_None.LinkBehavior = LinkBehavior.NeverUnderline;
            Dir_Rev.LinkBehavior = LinkBehavior.NeverUnderline;
            Dir_Edit.LinkBehavior = LinkBehavior.NeverUnderline;
            scrollTimer = new System.Windows.Forms.Timer { Interval = 20 };
            scrollTimer.Tick += new EventHandler(ScrollTimer_Tick);
            groupBox3.BringToFront();
            groupBox3.Visible = false;
            GB_NewDisk.BringToFront();
            GB_NewDisk.Visible = false;
            GB_NewDisk.Top = ((this.Height - GB_NewDisk.Height) / 2) - 100;
            GB_NewDisk.Left = ((this.Width - GB_NewDisk.Width) / 2) - 100;
            Dir_Ftype.Enabled = Dir_ChgType.Checked;
            Dir_Ftype.DataSource = new string[] { "PRG", "SEQ", "USR", "REL", "DEL" };
            /// ----------------------------
            this.linkLabel1.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.LinkLabel1_LinkClicked);
            Tabs.Controls.Remove(Import_File);
            this.Controls.Add(Import_File);
            Import_File.BringToFront();
            panel1.Controls.Add(outbox);
            panel1.Controls.Add(inbox);
            Height = PreferredSize.Height;
            pan_defw = panPic.Width;
            pan_defh = panPic.Height;
            panPic.Controls.Add(Disk_Image);
            Out_view.Select();
            Circle_View.Select();
            Disk_Image.Image = new Bitmap(8192, 42 * 15);
            panPic.AutoScroll = false;
            panPic.SetBounds(0, 0, Disk_Image.Width, Disk_Image.Height);
            Disk_Image.SizeMode = PictureBoxSizeMode.AutoSize;
            flat_large = new Bitmap(8192, panPic.Height - 16);
            out_weak.Width = 64;
            Adv_ctrl.SelectedIndexChanged += new System.EventHandler(Adv_Ctrl_SelectedIndexChanged);
            Out_density.DrawItem += new DrawItemEventHandler(Out_Density_Color);
            out_track.DrawItem += new DrawItemEventHandler(Out_Track_Color);
            Track_Info.DrawItem += new DrawItemEventHandler(Source_Info_Color);
            sf.DrawItem += new DrawItemEventHandler(Track_Format_Color);
            out_rpm.DrawItem += new DrawItemEventHandler(RPM_Color);
            Out_density.DrawMode = DrawMode.OwnerDrawFixed;
            out_track.DrawMode = DrawMode.OwnerDrawFixed;
            Track_Info.DrawMode = DrawMode.OwnerDrawFixed;
            out_rpm.DrawMode = DrawMode.OwnerDrawFixed;
            sf.DrawMode = DrawMode.OwnerDrawFixed;
            Out_density.ItemHeight = out_rpm.ItemHeight = sf.ItemHeight = 13;
            out_track.ItemHeight = out_rpm.ItemHeight = sf.ItemHeight = 13;
            Track_Info.ItemHeight = 15;
            Track_Info.HorizontalScrollbar = true;
            Adj_cbm.Visible = false;
            Tabs.Visible = true;
            Data_Box.DetectUrls = false;
            Data_Sep.DataSource = new string[] { "None", "Tracks", "Sectors" }; //d;
            Data_Sep.SelectedIndex = 1;
            VS_hex.Checked = true;
            T_jump.Visible = Jump.Visible = false;
            DV_pbar.Value = 0;
            DV_gcr.Checked = true;
            fnappend = fix;
            label1.Text = label2.Text = coords.Text = "";
            Source.Visible = Output.Visible = label4.Visible = Img_Q.Visible = Save_Circle_btn.Visible = false;
            Save_Disk.Visible = false;
            AllowDrop = true;
            DragEnter += new DragEventHandler(Drag_Enter);
            DragDrop += new DragEventHandler(Drag_Drop);
            f_load.Visible = false;
            /// ------------------ Vorpal Config --------------
            Lead_In.Enabled = VPL_lead.Checked;
            Lead_In.Value = 50;
            leadIn_std[9] = true;
            bool flip = false;
            for (int i = 0; i < leadIn_std.Length; i++)
            {
                if (i < 7) leadIn_std[i] = !flip;
                leadIn_alt[i] = flip;
                flip = !flip;
            }
            Lead_ptn.DataSource = new string[] { "Default", "0x55", "0xAA" };//pt;
            Lead_ptn.SelectedIndex = 0;
            Lead_ptn.Enabled = VPL_rb.Checked;
            VD0.Enabled = VD1.Enabled = VD2.Enabled = VD3.Enabled = VPL_density_reset.Visible = false;
            VD0.Value = vpl_density[0];
            VD1.Value = vpl_density[1];
            VD2.Value = vpl_density[2];
            VD3.Value = vpl_density[3];
            Lead_ptn.BringToFront();
            /// ----------------- V-Max v3 Config -------------
            V3_hlen.Enabled = false;
            /// ----------------- V-Max v2 Config -------------
            Tabs.Controls.Remove(Advanced_Opts);
            V2_hlen.Enabled = false;
            v2exp.Text = v3exp.Text = string.Empty;
            v2adv.Text = v3adv.Text = $"\u2193        Advanced users ONLY!        \u2193";
            vm2_ver[0] = new string[] { "A5-A5", "A4-A5", "A5-A7", "A5-A6", "A9-AD", "AC-A9", "AD-AB", "A9-AE", "A5-AD", "AC-A5", "AD-A7", "A5-AE", "A5-A9",
            "A4-A9", "A5-AB", "A5-AA", "A5-B5", "B4-A5", "A5-B7", "A5-B6", "A9-BD", "BC-A9" };
            vm2_ver[1] = new string[vm2_ver[0].Length];
            Array.Copy(vm2_ver[0], 0, vm2_ver[1], 0, vm2_ver[0].Length);
            vm2_ver[1][6] = "A5-A3"; vm2_ver[1][10] = "A9-A3";
            V2_swap.DataSource = new string[] { "64-4E (newer)", "64-46 (weak bits)", "4E-64 (alt)" };
            V2_swap.Enabled = V2_swap_headers.Checked;
            string[] interleave_select = new string[] { "1", "2", "3", "4", "5", "6", "7 JiffyDos 1571", "8 Fastloader", "9", "10 Standard", "11", "12" };
            Sec_Interleave.DataSource = interleave_select; // new string[] { "Standard (10)", "JiffyDos 1571 (7)", "Custom (5)" };
            S_Interleave.DataSource = interleave_select; //new string[] { "Standard (10)", "JiffyDos 1571 (7)", "Custom (5)" };
            /// Loads V-Max Loader track replacements into byte[] arrays
            v2ldrcbm = Decompress(XOR(Resources.v2cbmla, 0xcb)); // V-Max CBM sectors (DotC, Into the Eagles Nest, Paperboy, etc..)
            v24e64pal = Decompress(XOR(Resources.v24e64p, 0x64)); // V-Max Custom sectors (PAL Loader)
            v26446ntsc = Decompress(XOR(Resources.v26446n, 0x46)); // V-Max Custom sectors (NTSC Loader) Older version, headers have weak bits and may be incompatible with some 1541's
            v2644entsc = Decompress(XOR(Resources.v2644En, 0x4e)); // V-Max Custom sectors (NTSC Loader) Newer version, headers are compatible with all 1541 versions.
            /// these loaders are guaranteed to work and the loader code has not been modified from original. (these are not "cracked" loaders)
            rak1 = Decompress(XOR(Resources.rak1, 0xab));
            cldr_id = Decompress(XOR(Resources.cyan, 0xc1));
            fastloader = Decompress(XOR(Resources.fload, 0xf1));
            byte[] rlnk = Decompress(XOR(Resources.rlnk, 0x7b));
            Buffer.BlockCopy(rlnk, 0, rl_nkey, 0, rl_nkey.Length);
            for (int i = 54; i < rlnk.Length; i++) rl_7b[i - 54] = Convert.ToInt32(rlnk[i]);
            /// RapidLok Patches
            rl6_t18s3[0, 0] = new byte[] { 0x60, 0x9b, 0x2a, 0x7d };
            rl6_t18s3[1, 0] = new byte[] { 0xea, 0xf4, 0xb7, 0xb3, 0xba };
            rl6_t18s3[2, 0] = new byte[] { 0x75, 0x73, 0xdc, 0x3d, 0x27, 0xd6 };
            rl6_t18s3[3, 0] = new byte[] { 0x18, 0x28, 0x9b, 0x95, 0x64, 0x00, 0xa5, 0xc7, 0xc2, 0x27 };
            rl6_t18s3[4, 0] = new byte[] { 0xf9, 0xeb, 0x63 };
            rl6_t18s3[0, 1] = new byte[] { 0x68, 0x95, 0x56, 0xcb };
            rl6_t18s3[1, 1] = new byte[] { 0x82, 0xae, 0xeb, 0x93, 0x46 };
            rl6_t18s3[2, 1] = new byte[] { 0xa5, 0xf3, 0xb3, 0x8a, 0xc3, 0xd6 };
            rl6_t18s3[3, 1] = new byte[] { 0x1b, 0x12, 0x68, 0xb5, 0xb4, 0x01, 0xa5, 0x83, 0x83, 0xdc };
            rl6_t18s3[4, 1] = new byte[] { 0xf9, 0x3c, 0x63 };
            rl6_t18s6[0, 0] = new byte[] { 0x91, 0xb3, 0x7f, 0x92, 0xbe, 0xe9, 0x0c, 0x92, 0xfd, 0xcc, 0x24, 0x38, 0x02, 0x5a, 0xf1, 0x3e, 0x27, 0x51,
                                           0x52, 0x43, 0x9c, 0xd3, 0x93, 0x23, 0xca, 0x5d, 0x24, 0x7d, 0x31};
            rl6_t18s6[0, 1] = new byte[] { 0x32, 0x36, 0xe8, 0x0e, 0x33, 0x71, 0x70, 0x3a, 0xe0, 0xdf, 0x3d, 0x57, 0xd7, 0xb3, 0xcc, 0x2d, 0x0a, 0x4d,
                                           0x87, 0x06, 0x97, 0x74, 0xb2, 0x7a, 0x75, 0x83, 0x56, 0x9f, 0x33};
            rl2_t18s9[0, 0] = new byte[] { 0x75, 0xf3, 0x7f, 0x9b, 0xb9 };
            rl2_t18s9[1, 0] = new byte[] { 0xa9, 0xd5, 0x95, 0xfa, 0x9a };
            rl2_t18s9[0, 1] = new byte[] { 0x75, 0xf3, 0xef, 0x5b, 0xa9 };
            rl2_t18s9[1, 1] = new byte[] { 0xa9, 0xd5, 0xdc, 0xdb, 0xea };
            rl1_t18s9[0, 0] = new byte[] { 0xd5, 0x5e, 0xeb, 0xb7, 0xdb };
            rl1_t18s9[1, 0] = new byte[] { 0x3c, 0xcf, 0x3e, 0xd6, 0x96 };
            rl1_t18s9[0, 1] = new byte[] { 0xd5, 0x5e, 0x7b, 0xb6, 0xdb };
            rl1_t18s9[1, 1] = new byte[] { 0x3c, 0xcd, 0x5a, 0xdd, 0x56 };
            RL_Fix.Visible = false;
            RL_success.Text = string.Empty;
            p[0] = new byte[] { 0xd2, 0x4b, 0xff, 0x64 };
            p[1] = new byte[] { 0x4d, 0x6d, 0x5b, 0xff };
            p[2] = new byte[] { 0x92, 0x49, 0x24, 0x92 };
            p[3] = new byte[] { 0x6b, 0xff, 0x65, 0x53 };
            p[4] = new byte[] { 0x93, 0xff, 0x69, 0x25 };
            p[5] = new byte[] { 0x33, 0x33, 0x33, 0x33 };
            p[6] = new byte[] { 0x52, 0x52, 0x52, 0x52 };
            p[7] = new byte[] { 0x5a, 0x5a, 0x5a, 0x5a };
            p[8] = new byte[] { 0x69, 0x69, 0x69, 0x69 };
            p[9] = new byte[] { 0x4b, 0x4b, 0x4b, 0x4b };
            RM_cyan.Visible = false;
            RM_cyan.Left = 8;
            Img_Q.DataSource = Img_Quality;
            Img_Q.SelectedIndex = 2;
            Width = PreferredSize.Width;
            Flat_Interp.Visible = Flat_View.Checked;
            Circle_View.Checked = Out_view.Checked = true;
            label4.Visible = Img_Q.Visible = Circle_View.Checked;
            Circle_Render.Visible = Flat_Render.Visible = label3.Visible = false;
            Img_opts.Enabled = Img_style.Enabled = Img_View.Enabled = false;
            Import_File.Visible = false;
            Batch_Box.Visible = false;
            for (int i = 0; i < 8000; i++) { def_bg_text += "10"; }
            M_render.Enabled = false;
            Adv_ctrl.Enabled = false;
            VBS_info.Visible = Reg_info.Visible = false;
            Dir_screen.BackColor = C64_screen;
            Dir_screen.ForeColor = c64_text;
            Dir_screen.ReadOnly = true;
            Tabs.Controls.Remove(Import_File);
            this.Controls.Add(Import_File);
            Import_File.BringToFront();
            Import_File.Top = 57;
            Import_File.Left = 19;
            DB_cores.Enabled = DB_core_override.Checked;
            SwapDensities();
            Set_Boxes();
            Draw_Init_Img(def_bg_text);
            Default_Dir_Screen();
            Set_Auto_Opts();
            BlockMap_Setup();
            Cores = Get_Cores();
            Set_Cores();
            Set_Tool_Tips();
            //out_size.BringToFront();
            //Output.Height = 12;
            tips.ShowAlways = true;
            manualRender = M_render.Visible = Cores <= 3;
            if (Cores < 2) Img_Q.SelectedIndex = 0;
            AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(
            (s, a) =>
            {
                if (a.Name.Substring(0, a.Name.IndexOf(",")) == "msvcrt")
                {
                    return Assembly.Load(Decompress(XOR(Resources.msvcrt, 0x24)));
                }
                return null;
            });
            Build_BitReverseTable();
            try
            {
                //File.WriteAllBytes($@"c:\test\compressed\fload.bin", XOR(Compress(File.ReadAllBytes($@"c:\test\loaders\fload")), 0xf1));
                //File.WriteAllBytes($@"c:\test\compressed\cpp_extf.bin", XOR(Compress(File.ReadAllBytes($@"c:\test\loaders\DrawArc.dll")), 0xda));
                //File.WriteAllBytes($@"c:\test\compressed\msvcrt.bin", XOR(Compress(File.ReadAllBytes($@"c:\test\loaders\msvcrt")), 0x24));
                //File.WriteAllBytes($@"c:\test\compressed\rlnk.bin", XOR(Compress(File.ReadAllBytes($@"c:\test\loaders\rlnk")), 0x7b));
                //File.WriteAllBytes($@"c:\test\compressed\cyan.bin", XOR(Compress(File.ReadAllBytes($@"c:\test\loaders\cyan")), 0xc1));
                //File.WriteAllBytes($@"c:\test\compressed\rak1.bin", XOR(Compress(File.ReadAllBytes($@"c:\test\loaders\rak2")), 0xab));
                //File.WriteAllBytes($@"c:\test\compressed\v2cbmla.bin", XOR(Compress(File.ReadAllBytes($@"c:\test\loaders\cbm")), 0xcb));
                //File.WriteAllBytes($@"c:\test\compressed\v24e64p.bin", XOR(Compress(File.ReadAllBytes($@"c:\test\loaders\4e64")), 0x64));
                //File.WriteAllBytes($@"c:\test\compressed\v26446n.bin", XOR(Compress(File.ReadAllBytes($@"c:\test\loaders\6446")), 0x46));
                //File.WriteAllBytes($@"c:\test\compressed\v2644en.bin", XOR(Compress(File.ReadAllBytes($@"c:\test\loaders\644e")), 0x4e));
            }
            catch { }
            DB_g64.Checked = true;

            void Set_Boxes()
            {
                outbox.BackColor = Color.Gainsboro;
                outbox.BringToFront();
                panel1.Controls.Remove(this.out_weak);
                panel1.Controls.Remove(this.Out_density);
                panel1.Controls.Remove(this.out_rpm);
                panel1.Controls.Remove(this.out_track);
                panel1.Controls.Remove(this.out_dif);
                panel1.Controls.Remove(this.out_size);
                outbox.Controls.Add(this.Out_density);
                outbox.Controls.Add(this.out_rpm);
                outbox.Controls.Add(this.out_track);
                outbox.Controls.Add(this.out_dif);
                outbox.Controls.Add(this.out_size);
                outbox.Controls.Add(this.out_weak);
                var w = 5;
                out_track.Location = new Point(w, 15); w += out_track.Width - 1;
                out_rpm.Location = new Point(w, 15); w += out_rpm.Width - 1;
                out_size.Location = new Point(w, 15); w += out_size.Width - 1;
                out_dif.Location = new Point(w, 15); w += out_dif.Width - 1;
                Out_density.Location = new Point(w, 15); w += out_dif.Width - 1;
                out_weak.Location = new Point(w, 15); //w += out_dif.Width - 1;
                outbox.FlatStyle = FlatStyle.Flat;
                outbox.ForeColor = Color.Indigo;
                outbox.Name = "outbox";
                outbox.Width = outbox.PreferredSize.Width;
                outbox.Height = outbox.PreferredSize.Height;
                outbox.Location = new Point(225, 13);
                outbox.TabIndex = 52;
                outbox.TabStop = false;
                outbox.Text = "Track/ RPM /     Size     / Diff     / Density  / Weak";
                inbox.BackColor = Color.Gainsboro;
                panel1.Controls.Remove(this.sd);
                panel1.Controls.Remove(this.strack);
                panel1.Controls.Remove(this.sf);
                panel1.Controls.Remove(this.ss);
                panel1.Controls.Remove(this.sl);
                inbox.Controls.Add(this.sd);
                inbox.Controls.Add(this.strack);
                inbox.Controls.Add(this.sf);
                inbox.Controls.Add(this.ss);
                inbox.Controls.Add(this.sl);
                inbox.BringToFront();
                w = 5;
                strack.Location = new Point(w, 15); w += strack.Width - 1;
                sl.Location = new Point(w, 15); w += sl.Width - 1;
                sf.Location = new Point(w, 15); w += sf.Width - 1;
                ss.Location = new Point(w, 15); w += ss.Width - 1;
                sd.Location = new Point(w, 15);
                inbox.FlatStyle = FlatStyle.Popup;
                inbox.ForeColor = Color.Indigo;
                inbox.Location = new Point(8, 13);
                inbox.Name = "inbox";
                inbox.Width = inbox.PreferredSize.Width;
                inbox.Height = inbox.PreferredSize.Height;
                inbox.TabIndex = 55;
                inbox.TabStop = false;
                inbox.Text = "Trk / Size / Format / Sectors / Dens";
            }

            FontFamily LoadFontFromResource(byte[] fontdata)
            {
                // Pin the font data array in memory
                IntPtr fontPtr = Marshal.AllocCoTaskMem(fontdata.Length);
                Marshal.Copy(fontdata, 0, fontPtr, fontdata.Length);

                // Add the font to the PrivateFontCollection
                DirFont.AddMemoryFont(fontPtr, fontdata.Length);

                // Free the memory
                Marshal.FreeCoTaskMem(fontPtr);

                // Return the first font family in the collection
                return DirFont.Families[0];
            }

            void Set_Tool_Tips()
            {
                tips.SetToolTip(Adj_cbm, "Adjust standard tracks to fit a 300rpm rotation cycle\n" +
                    "Allows for writing images without slowing down the disk drive\n\n" +
                    "Option may not be available on certain Protection types that rely on the extra data");
                tips.SetToolTip(f_load, "Attempt to (fix) a V-Max loader track\nif a V-Max image gets stuck on track 20, try this option");
                tips.SetToolTip(Save_Disk, "Export ReMastered file as G64 or NIB");
                tips.SetToolTip(Circle_View, "Show image of track data representation as it would be on a disk");
                tips.SetToolTip(Flat_View, "Show image of track data representation in a linear view");
                tips.SetToolTip(Out_view, "Show the processed (output) image data representation");
                tips.SetToolTip(Src_view, "Show the source file's (input) image data representation");
                tips.SetToolTip(Show_sec, "Highlight areas where a new sector starts (V-Max/Vorpal)");
                tips.SetToolTip(Cap_margins, "Show's where the data exceeds the track's capacity limit at 300rpm");
                tips.SetToolTip(Flat_Interp, "Blurs the image a little (can help better define where the sectors are)");
                tips.SetToolTip(Rev_View, "Shows the tracks in different colors to differentiate between formats");
                tips.SetToolTip(Save_Circle_btn, "Save currently displayed image as BMP or JPG");
                tips.SetToolTip(label4, "Change Disk-View image resolution\nLow = 1000 x 1000, Insanity = 7000 x 7000");
                tips.SetToolTip(Img_Q, "Change Disk-View image resolution\nLow = 1000 x 1000, Insanity = 7000 x 7000");
                tips.SetToolTip(Re_Align, "Attempt to center the V-Max loader data in the track to prevent the track gap from being placed within the data");
                tips.SetToolTip(V2_Auto_Adj, "Adjust all tracks to fit on a disk without slowing down the drive motor");
                tips.SetToolTip(V2_Custom, "Manually set the sector header length (applies to all tracks)\n" +
                    "this isn't very useful, but it could be fun! or dangerous. Who knows?");
                tips.SetToolTip(V3_Auto_Adj, "Adjust all tracks to fit on a disk without slowing down the drive motor");
                tips.SetToolTip(V3_Custom, "Manually set the sector header length (applies to all tracks)\n" +
                    "this isn't very useful, but it could be fun! or dangerous. Who knows?");
                tips.SetToolTip(V2_swap_headers, "Changes the sector headers (must use the same headers on all sides)\n" +
                    "64-46 contains weak-bits that might not work on older 1541 drives.\n" +
                    "Change headers to 64-4E if your drive has any issues with loading\n" +
                    "*4E-64 is only found on European versions of V-Max, but they also work");
                tips.SetToolTip(V2_Add_Sync, "Adds 10 bits of sync before each sector on syncless tracks\n" +
                    "This doesn't have any affect on loading and the protection doesn't check for it");
                tips.SetToolTip(VPL_auto_adj, "Adjust all tracks for best success on write");
                tips.SetToolTip(VPL_rb, "Adjust all (Vorpal) tracks for best success on write, leaves standard tracks un-altered");
                tips.SetToolTip(VPL_lead, "Adjust sector data placement (in bytes) from the start of the track");
                tips.SetToolTip(VPL_only_sectors, "Vorpal tracks will ONLY contain the sector data, no lead-in or lead-out\n" +
                    "this is for educational purposes only and is unlikely to produce a working image");
                tips.SetToolTip(VPL_presync, "Adds 16 bits of sync to the start of the track (in the lead-in)\n" +
                    "this is for experimentation and may help or hinder successful disk-writes");
                tips.SetToolTip(label7, "Change the Lead-in/out sequence of Vorpal tracks\n" +
                    "0x55 and 0xAA are essentially the same (01010101 or 10101010");
                tips.SetToolTip(RL_ChangeKey, "Check this option if output image fails the Track-36 key check");
                tips.SetToolTip(RL_Fix, "Remove all advanced RapidLok checks.\nDisables the following items...\n\nTrack 36 key-check\nHeader integrity checks\n" +
                    "Sync checks\nSpecial sector checks\nTrack alignment checks\n\nCurrently only works on RapidLok versions 1,2, and 4-7");
                tips.SetToolTip(DB_force, "Will perform auto-adjust on standard CBM-Formatted tracks, ignoring special\n"
                    + "conditions that some copy protections rely on to pass the protection.\n\n Sometimes these conditions are falsely identified."
                    + " select this option to FORCE adjusting of these tracks");
            }

            void BlockMap_Setup()
            {
                track_label.Text = "";
                track_label.AutoSize = false;
                track_label.NewText = "Track";
                track_label.ForeColor = Color.White;
                track_label.RotateAngle = -90;

                FreeBlk.Text = "";
                FreeBlk.AutoSize = false;
                FreeBlk.NewText = "Free Block";
                FreeBlk.ForeColor = Color.FromArgb(30, 125, 30);
                FreeBlk.RotateAngle = -90;

                AllocBlk.Text = "";
                AllocBlk.AutoSize = false;
                AllocBlk.NewText = "Allocated Block";
                AllocBlk.ForeColor = Color.FromArgb(30, 200, 30);
                AllocBlk.RotateAngle = -90;

                ErrorBlk.Text = "";
                ErrorBlk.AutoSize = false;
                ErrorBlk.NewText = "Block Error";
                ErrorBlk.ForeColor = Color.Red;
                ErrorBlk.RotateAngle = -90;

                CSTfmt.Text = "";
                CSTfmt.AutoSize = false;
                CSTfmt.NewText = "Custom Format";
                CSTfmt.ForeColor = Color.MediumOrchid;
                CSTfmt.RotateAngle = -90;

                // set Track # labels in Block Map
                int left = 25;
                int top = 27;
                int inc = 15;
                for (int i = 0; i < 41; i++)
                {
                    string spc = i < 9 ? " " : string.Empty;
                    BlkMap_track[i] = new Label();
                    BlkMap_Panel.Controls.Add(this.BlkMap_track[i]);
                    BlkMap_track[i].AutoSize = true;
                    BlkMap_track[i].Font = new Font("Courier New", 9.5F, FontStyle.Regular, GraphicsUnit.Point, 0);
                    BlkMap_track[i].ForeColor = Color.DarkGray;
                    BlkMap_track[i].Location = new Point(left, top + (inc * i));
                    BlkMap_track[i].Size = new Size(26, 27);
                    BlkMap_track[i].TabIndex = 4;
                    BlkMap_track[i].Text = $"{spc}{i + 1}";
                    BlkMap_track[i].Visible = true;
                    BlkMap_track[i].BringToFront();
                    Blk_pan.Height = 3 + inc + (inc * i);
                }
                // set Sector # labels in Block Map
                left = 50;
                top = 6;
                int spacing = 24;
                for (int i = 0; i < 21; i++)
                {
                    string spc = i < 9 ? " " : string.Empty;
                    BlkMap_sector[i] = new Label();
                    this.BlkMap_Panel.Controls.Add(BlkMap_sector[i]);
                    BlkMap_sector[i].AutoSize = true;
                    BlkMap_sector[i].Font = new Font("Courier New", 9.5F, FontStyle.Regular, GraphicsUnit.Point, 0);
                    BlkMap_sector[i].ForeColor = Color.DarkGray;
                    BlkMap_sector[i].Location = new Point(left - (i < 9 ? 5 : 0) + (spacing * i), top);
                    BlkMap_sector[i].Size = new Size(26, 27);
                    BlkMap_sector[i].TabIndex = 4;
                    BlkMap_sector[i].Text = $"{spc}{i + 1}";
                    BlkMap_sector[i].BringToFront();
                    Blk_pan.Width = spacing + (spacing * i) + 2;
                }
                // set BAM buttons in Block Map
                int w_spc = 24;
                int h_spc = 15;
                top = 3;
                left = 2;
                int active = 200;
                int inactive = 40;
                for (int i = 0; i < 41; i++)
                {
                    BlkMap_bam[i] = new Panel[21];
                    for (int j = 0; j < 21; j++)
                    {
                        bool valid = (j < Available_Sectors[i] && i < 35);
                        BlkMap_bam[i][j] = new Panel();
                        Blk_pan.Controls.Add(BlkMap_bam[i][j]);
                        BlkMap_bam[i][j].Location = new Point(left + (w_spc * j), top + (h_spc * i));
                        BlkMap_bam[i][j].Size = new Size(20, 12);
                        BlkMap_bam[i][j].TabIndex = 0;
                        BlkMap_bam[i][j].BackColor = Color.FromArgb(valid ? active : inactive, 30, 200, 30);
                        BlkMap_bam[i][j].BringToFront();
                        BlkMap_bam[i][j].Visible = false;
                        BlkMap_bam[i][j].MouseEnter += Panel_MouseEnter;
                        BlkMap_bam[i][j].MouseLeave += Button_MouseLeave;
                        BlkMap_bam[i][j].MouseClick += Panel_MouseClick;
                        BlkMap_bam[i][j].Tag = new { Track = i, Sector = j };
                    }
                }
            }
        }

        void Set_ListBox_Items(bool r, bool nofile, bool clear_batch_list = true)
        {
            strack.BeginUpdate();
            ss.BeginUpdate();
            sf.BeginUpdate();
            sl.BeginUpdate();
            sd.BeginUpdate();
            out_size.BeginUpdate();
            out_dif.BeginUpdate();
            out_rpm.BeginUpdate();
            out_track.BeginUpdate();
            Out_density.BeginUpdate();
            out_weak.BeginUpdate();
            if (r)
            {
                Make_Visible();
                Clear_Out_Items();
                Clear_In_Items();

                out_track.Height = Out_density.Height = out_size.Height = out_dif.Height = out_rpm.Height = out_weak.Height = out_size.PreferredHeight;
                sl.Height = strack.Height = sl.Height = sd.Height = ss.Height = sf.Height = sl.PreferredHeight; // (items * 12);
            }
            Make_Visible();
            outbox.Visible = inbox.Visible = !r;
            out_track.Height = Out_density.Height = out_size.Height = out_dif.Height = out_rpm.Height = out_weak.Height = out_size.PreferredHeight;
            sl.Height = strack.Height = sl.Height = sd.Height = ss.Height = sf.Height = sl.PreferredHeight; // (items * 12);
            outbox.Height = outbox.PreferredSize.Height;
            inbox.Height = inbox.PreferredSize.Height;
            if (clear_batch_list) Drag_pic.Visible = (r && nofile);
            out_size.EndUpdate();
            out_dif.EndUpdate();
            Out_density.EndUpdate();
            ss.EndUpdate();
            sf.EndUpdate();
            out_rpm.EndUpdate();
            out_track.EndUpdate();
            out_weak.EndUpdate();
            strack.EndUpdate();
            sl.EndUpdate();
            sd.EndUpdate();

            void Make_Visible()
            {
                out_size.Visible = !r;
                out_dif.Visible = !r;
                ss.Visible = !r;
                sf.Visible = !r;
                sl.Visible = !r;
                sd.Visible = !r;
                strack.Visible = !r;
                out_rpm.Visible = !r;
                out_track.Visible = !r;
                Out_density.Visible = !r;
                out_weak.Visible = !r;
            }
        }

        void Clear_Out_Items()
        {
            out_track.Items.Clear();
            out_size.Items.Clear();
            out_dif.Items.Clear();
            Out_density.Items.Clear();
            out_rpm.Items.Clear();
            out_weak.Items.Clear();
        }

        void Clear_In_Items()
        {
            ss.Items.Clear();
            sf.Items.Clear();
            sl.Items.Clear();
            sd.Items.Clear();
            strack.Items.Clear();
        }

        bool Load_Dll()
        {
            try
            {
                byte[] cpp = Decompress(XOR(Resources.cpp_extf, 0xda));

                // Check if the file exists and needs to be overwritten
                if (File.Exists(DLL.path))
                {
                    try
                    {
                        byte[] verify = File.ReadAllBytes(DLL.path);

                        // If the file contents differ, overwrite it
                        if (cpp.Length != verify.Length || !cpp.SequenceEqual(verify))
                        {
                            try
                            {
                                OverwriteFile(DLL.path, cpp);
                            }
                            catch { return false; }
                        }
                    }
                    catch { return false; }
                }
                else
                {
                    // If the file doesn't exist, create it
                    WriteNewFile(DLL.path, cpp);
                }

                // Hide the file after writing
                try
                {
                    File.SetAttributes(DLL.path, FileAttributes.Hidden);
                }
                catch { }

                // Attempt to load and test the DLL
                int test = 0;
                try
                {
                    test = NativeMethods.TestLoaded();
                }
                catch { }

                return (test == 6);

                void OverwriteFile(string path, byte[] content)
                {
                    try
                    {
                        // Remove hidden attribute if necessary before overwriting
                        File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.Hidden);
                    }
                    catch { }

                    try
                    {
                        File.WriteAllBytes(path, content);
                    }
                    catch { }
                }

                void WriteNewFile(string path, byte[] content)
                {
                    try
                    {
                        File.WriteAllBytes(path, content);
                    }
                    catch { }
                }
            }
            catch { }
            return false;

        }

        void Build_BitReverseTable()
        {
            for (int i = 0; i < 256; i++)
            {
                Reverse_Endian_Table[i] = ReverseBits((byte)i);
            }
            byte ReverseBits(byte b)
            {
                b = (byte)((b * 0x0202020202 & 0x010884422010) % 1023);
                return b;
            }
        }
    }
}