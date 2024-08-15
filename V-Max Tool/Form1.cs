using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;


namespace V_Max_Tool
{
    public partial class Form1 : Form
    {
        private bool Auto_Adjust = true; // <- Sets the Auto Adjust feature for V-Max and Vorpal images (for best remastering results)
        private readonly bool Replace_RapidLok_Key = false;
        private readonly string ver = " v1.0.0.1";
        private readonly string fix = "_ReMaster";
        private readonly string mod = "_ReMaster"; // _(modified)";
        private readonly string vorp = "_ReMaster"; //(aligned)";
        private readonly byte loader_padding = 0x55;
        private readonly int[] density = { 7672, 7122, 6646, 6230 }; // <- adjusted capacity to account for minor RPM variation higher than 300
        private readonly int[] vpl_density = { 7750, 7106, 6635, 6230 }; // <- adjusted capacity to account for minor RPM variation higher than 300
        private bool error = false;
        private bool cancel = false;
        private bool busy = false;
        private bool nib_error = false;
        private bool g64_error = false;
        private bool batch = false;
        private string nib_err_msg;
        private string g64_err_msg;
        private byte[] rak1 = new byte[0];
        private byte[] cldr_id = new byte[0];
        private byte[] v2ldrcbm = new byte[0];
        private byte[] v24e64pal = new byte[0];
        private byte[] v26446ntsc = new byte[0];
        private byte[] v2644entsc = new byte[0];
        private readonly int min_t_len = 6000;
        private int end_track = -1;
        private int fat_trk = -1;
        private int Cores;
        private int Default_Cores;
        private readonly ToolTip tips = new System.Windows.Forms.ToolTip();
        private List<string> LB_File_List = new List<string>();
        private Semaphore Task_Limit = new Semaphore(3, 3);
        //private const int EIGHT_KB = 8192;
        Thread Worker_Main;
        Thread Worker_Alt;
        Thread[] Job;

        public Form1()
        {
            InitializeComponent();
            this.Text = $"Re-Master {ver}";
            Init();
            Set_ListBox_Items(true, true);
        }

        private void Drag_Drop(object sender, DragEventArgs e)
        {
            Source.Visible = Output.Visible = false;
            f_load.Text = "Fix Loader";
            Save_Disk.Visible = false;
            sl.DataSource = null;
            out_size.DataSource = null;
            string[] File_List = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (File_List.Length > 1 || Directory.Exists(File_List[0]))
            {
                using (Message_Center center = new Message_Center(this)) // center message box
                {
                    string t = "Multiple Files Selected";
                    string s = "Only .NIB/NBZ files will be processed\nand exported as .G64 with the\nAuto-Adjust options\n\nStart Batch-Processing?";
                    DialogResult uc = MessageBox.Show(s, t, MessageBoxButtons.OKCancel, MessageBoxIcon.Information);
                    if (uc.ToString() == "OK")
                    {
                        Worker_Main = new Thread(new ThreadStart(() => Batch_Get_File_List(File_List)));
                        Worker_Main.Start();
                    }
                }
            }
            else
            {
                if (System.IO.File.Exists(File_List[0]))
                {
                    fname = Path.GetFileNameWithoutExtension(File_List[0]).Replace("_ReMaster", "");
                    fext = Path.GetExtension(File_List[0]);
                    Process_New_Image(File_List[0]);
                }
            }
        }

        unsafe void Process_New_Image(string file)
        {
            Disable_Core_Controls(true);
            string l = "Not ok";
            try
            {
                g64_header = new byte[684];
                byte[] source = File.ReadAllBytes(file);
                int length = source.Length;
                if (fext.ToLower() == supported[0])
                {
                    Data_Box.Clear();
                    tracks = (int)(length - 256) >> 13;
                    if ((tracks << 13) + 256 == length) l = "File Size OK!";
                    Track_Info.Items.Clear();
                    Set_ListBox_Items(true, false);
                    Buffer.BlockCopy(source, 0, nib_header, 0, 256);
                    Set_Arrays(tracks);
                    for (int i = 0; i < tracks; i++)
                    {
                        NDS.Track_Data[i] = new byte[MAX_TRACK_SIZE];
                        Buffer.BlockCopy(source, 256 + (MAX_TRACK_SIZE * i), NDS.Track_Data[i], 0, MAX_TRACK_SIZE);
                        Original.OT[i] = new byte[0];
                    }
                    var head = Encoding.ASCII.GetString(nib_header, 0, 13);
                    var hm = "Bad Header";
                    if (head == "MNIB-1541-RAW") hm = "Header Match!";
                    var lab = $"Total Tracks ({tracks}), {l}, {hm}";
                    Process(true, lab);
                }
                if (fext.ToLower() == supported[1] || fext.ToLower() == supported[4])
                {
                    Data_Box.Clear();
                    Track_Info.Items.Clear();
                    Set_ListBox_Items(true, false);
                    byte[] decomp;
                    if (fext.ToLower() == supported[4])
                    {
                        decomp = LZdecompress(source);
                    }
                    else
                    {
                        decomp = new byte[length];
                        Buffer.BlockCopy(source, 0, decomp, 0, length);
                    }

                    if (decomp.Length > 0)
                    {
                        Buffer.BlockCopy(decomp, 0, g64_header, 0, 684);
                        var head = Encoding.ASCII.GetString(g64_header, 0, 8);
                        tracks = Convert.ToInt32(g64_header[9]);
                        Set_Arrays(tracks);
                        int tr_size = BitConverter.ToInt16(g64_header, 10);
                        var hm = "Bad Header";
                        if (head == "GCR-1541")
                        {
                            hm = "Header Match!";
                            byte[] temp = new byte[2];
                            for (int i = 0; i < tracks; i++)
                            {
                                Original.OT[i] = new byte[0];
                                NDS.Track_Data[i] = FastArray.Init(MAX_TRACK_SIZE, 0x00);
                                int pos = BitConverter.ToInt32(g64_header, 12 + (i * 4));
                                if (pos != 0)
                                {
                                    try
                                    {
                                        Buffer.BlockCopy(decomp, pos, temp, 0, 2);
                                        short ts = BitConverter.ToInt16(temp, 0);
                                        byte[] tdata = new byte[ts];
                                        Buffer.BlockCopy(decomp, pos + 2, tdata, 0, ts);
                                        NDG.s_len[i] = tdata.Length;
                                        Buffer.BlockCopy(tdata, 0, NDS.Track_Data[i], 0, ts);
                                        Buffer.BlockCopy(tdata, 0, NDS.Track_Data[i], ts, MAX_TRACK_SIZE - ts);
                                    }
                                    catch { }
                                }
                            }
                            var lab = $"Total Tracks {tracks}, G64 Track Size {tr_size:N0} bytes";
                            Process(false, lab);
                        }
                        else
                        {
                            label1.Text = $"{hm}";
                            label2.Text = "";
                        }
                        if (hm == "Bad Header")
                        {
                            using (Message_Center center = new Message_Center(this)) // center message box
                            {
                                string t = "Bad Header!";
                                string s = "Image is corrupt and cannot be opened";
                                MessageBox.Show(s, t, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                error = true;
                            }
                        }
                    }

                }
                if (fext.ToLower() == supported[2])
                {
                    Data_Box.Clear();
                    int sectors = length >> 8;
                    byte[][] secdata = new byte[sectors][];
                    int trk_counter = 0;
                    tracks = 0;
                    for (int i = 0; i < sectors; i++)
                    {
                        try
                        {
                            secdata[i] = new byte[256];
                            Buffer.BlockCopy(source, i << 8, secdata[i], 0, 256);
                            trk_counter++;
                            if (trk_counter == Available_Sectors[tracks])
                            {
                                trk_counter = 0;
                                tracks++;
                            }
                        }
                        catch { }
                    }
                    Track_Info.Items.Clear();
                    Set_ListBox_Items(true, false);
                    Set_Arrays(tracks);
                    byte[] ID = FastArray.Init(4, 0x0f);
                    ID[1] = secdata[357][163];
                    ID[0] = secdata[357][162];
                    int psec = 0;
                    byte[] sync = FastArray.Init(5, 0xff);
                    for (int i = 0; i < tracks; i++)
                    {
                        MemoryStream buffer = new MemoryStream();
                        BinaryWriter write = new BinaryWriter(buffer);
                        int tsec = Available_Sectors[i];
                        int len = density[density_map[i]];
                        NDS.Track_Data[i] = FastArray.Init(MAX_TRACK_SIZE, 0x00);
                        for (int j = 0; j < tsec; j++)
                        {
                            byte[] gap = FastArray.Init(sector_gap_length[i], 0x55);
                            byte[] sec_header = Build_BlockHeader(i + 1, j, ID);
                            byte[] sec_data = Build_Sector(secdata[psec]);
                            write.Write(sync);
                            write.Write(sec_header);
                            write.Write(gap);
                            write.Write(sync);
                            write.Write(sec_data);
                            write.Write(gap);
                            psec++;
                        }
                        int dif = len - (int)buffer.Length;
                        if (dif > 0) write.Write(FastArray.Init(dif, 0x55));
                        byte[] temp = buffer.ToArray();
                        Buffer.BlockCopy(temp, 0, NDS.Track_Data[i], 0, temp.Length);
                        Buffer.BlockCopy(temp, 0, NDS.Track_Data[i], temp.Length, MAX_TRACK_SIZE - temp.Length);
                    }
                    var lab = $"Total Tracks ({tracks}), {l}";
                    //Invoke(new Action(() => Text = $"sectors {sectors} tracks {tracks} {secdata.Length}"));
                    Process(true, lab);
                }
                if (fext.ToLower() == supported[3])
                {
                    Data_Box.Clear();
                    byte[] decomp = LZdecompress(source);
                    if (decomp != null)
                    {
                        tracks = (decomp.Length - 256) >> 13;
                        if ((tracks << 13) + 256 == decomp.Length) l = "File Size OK!";
                        Track_Info.Items.Clear();
                        Set_ListBox_Items(true, false);
                        Set_Arrays(tracks);
                        nib_header = new byte[256];
                        Buffer.BlockCopy(decomp, 0, nib_header, 0, nib_header.Length);
                        for (int i = 0; i < tracks; i++)
                        {
                            NDS.Track_Data[i] = FastArray.Init(MAX_TRACK_SIZE, 0x00);
                            Buffer.BlockCopy(decomp, (i << 13) + 256, NDS.Track_Data[i], 0, MAX_TRACK_SIZE);
                            Original.OT[i] = new byte[0];
                        }
                    }
                    var head = Encoding.ASCII.GetString(nib_header, 0, 13);
                    var hm = "Bad Header";
                    if (head == "MNIB-1541-RAW") hm = "Header Match!";
                    var lab = $"Total Tracks ({tracks}), {l}, {hm}";
                    Process(true, lab);
                }
            }

            catch (Exception ex)
            {
                using (Message_Center center = new Message_Center(this)) // center message box
                {
                    string t = "Something went wrong!";

                    string s = ex.Message;
                    if (s.ToLower().Contains("source array")) s = "Image is corrupt and cannot be opened";
                    MessageBox.Show(s, t, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    error = true;
                }
            }
            GC.Collect();
            if (!error && !supported.Any(s => s == fext.ToLower()))
            {
                Reset_to_Defaults();
                label1.Text = "File not Valid!";
                label2.Text = string.Empty;
            }
            if (error)
            {
                Reset_to_Defaults();
                label1.Text = "";
                label2.Text = string.Empty;
                error = false;
            }

            void Process(bool get, string l2)
            {
                Batch_List_Box.Visible = false;
                Dir_screen.Clear();
                Dir_screen.Text = "LOAD\"$\",8\nSEARCHING FOR $\nLOADING";
                loader_fixed = false;
                Worker_Main?.Abort();
                Worker_Main = new Thread(new ThreadStart(() => Do_work()));
                Worker_Main.Start();
            }
        }

        void Do_work()
        {
            Stopwatch parse = new Stopwatch();
            Stopwatch proc = new Stopwatch();
            try
            {
                parse = Parse_Nib_Data();
            }
            catch { }
            if (!error)
            {
                Invoke(new Action(() =>
                {
                    try
                    {
                        proc = Process_Nib_Data(true, false, true);
                        if (DB_timers.Checked) Invoke(new Action(() => label2.Text = $"Parse time : {parse.Elapsed.TotalMilliseconds} ms, Process time : {proc.Elapsed.TotalMilliseconds} ms, Total {parse.Elapsed.TotalMilliseconds + proc.Elapsed.TotalMilliseconds} ms"));
                        Set_ListBox_Items(false, false);
                        Get_Disk_Directory();
                        linkLabel1.Visible = false;
                        Save_Disk.Visible = true;
                        Source.Visible = Output.Visible = true;
                        label1.Text = $"{fname}{fext}";
                        M_render.Enabled = true;
                        Import_File.Visible = false;
                        Adv_ctrl.Enabled = true;
                        Disable_Core_Controls(false);
                    }
                    catch (Exception ex)
                    {
                        if (!batch)
                        {
                            using (Message_Center center = new Message_Center(this)) // center message box
                            {
                                string t = "Something went wrong!";

                                string s = ex.Message;
                                //s = "Image is corrupt and cannot be opened";
                                MessageBox.Show(s, t, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            }
                            Reset_to_Defaults();
                        }
                    }
                }));
            }

        }

        private void Drag_Enter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
        }

        private void Make(object sender, EventArgs e)
        {
            Export_File(end_track);
        }

        private void F_load_CheckedChanged(object sender, EventArgs e)
        {
            int i = 100;
            if (tracks > 0 && NDS.Track_Data.Length > 0)
            {
                i = Array.FindIndex(NDS.cbm, s => s == 4);
                if (i < 100 && i > -1)
                {
                    Fix_Loader_Option(!busy, i);
                }
            }
        }

        private void V2_Custom_CheckedChanged(object sender, EventArgs e)
        {
            if (!busy)
            {
                busy = true;
                V2_hlen.Enabled = V2_Custom.Checked;
                if (V2_Custom.Checked) V2_Auto_Adj.Checked = false;
                busy = false;
                V2_Adv_Opts();
            }
        }

        private void AutoAdj_CheckedChanged(object sender, EventArgs e)
        {
            if (!busy)
            {
                busy = true;
                if (V2_Auto_Adj.Checked) V2_Custom.Checked = V2_hlen.Enabled = V2_Add_Sync.Checked = false;
                if (!V2_Auto_Adj.Checked) { v2aa = false; }
                busy = false;
                V2_Adv_Opts();
            }
        }

        private void V3_Auto_Adj_CheckedChanged(object sender, EventArgs e)
        {
            if (!busy)
            {
                busy = true;
                if (V3_Auto_Adj.Checked) V3_Custom.Checked = V3_hlen.Enabled = false;
                if (!V3_Auto_Adj.Checked) { v3aa = false; }
                busy = false;
                V3_Auto_Adjust();
            }
        }

        private void V3_Custom_CheckedChanged(object sender, EventArgs e)
        {
            if (!busy)
            {
                busy = true;
                if (V3_Custom.Checked)
                {
                    V3_Auto_Adj.Checked = false;
                    V3_hlen.Enabled = true;
                }
                else V3_hlen.Enabled = false;
                busy = false;
                V3_Auto_Adjust();
            }
        }

        private void Adj_cbm_CheckedChanged(object sender, EventArgs e)
        {
            if (!busy)
            {
                busy = true;
                V3_Auto_Adjust();
                busy = false;
            }
        }

        private void V2_Add_Sync_CheckedChanged(object sender, EventArgs e)
        {
            if (!busy)
            {
                busy = true;
                if (V2_Add_Sync.Checked) V2_Auto_Adj.Checked = false;
                busy = false;
                V2_Adv_Opts();
            }
        }

        private void V2_hlen_ValueChanged(object sender, EventArgs e)
        {
            V2_Adv_Opts();
        }

        private void V3_hlen_ValueChanged(object sender, EventArgs e)
        {
            V3_Auto_Adjust();
        }

        private void Manual_render_Click(object sender, EventArgs e)
        {
            drawn = false;
            Check_Before_Draw(false);
        }

        private void LinkLabel1_LinkClicked(object sender, System.Windows.Forms.LinkLabelLinkClickedEventArgs e)
        {
            this.linkLabel1.LinkVisited = true;
            Process.Start("https://github.com/DarylKrans/V-Max-Sync-Tool");
        }

        private void VPL_lead_CheckedChanged(object sender, EventArgs e)
        {
            if (!busy)
            {
                busy = true;
                Lead_In.Enabled = VPL_lead.Checked;
                if (VPL_lead.Checked)
                {
                    VPL_rb.Checked = true;
                    VPL_only_sectors.Checked = VPL_auto_adj.Checked = false;
                    VPL_presync.Enabled = VPL_auto_adj.Checked;
                }
                busy = false;
                Vorpal_Rebuild();
            }
        }

        private void VPL_rb_CheckedChanged(object sender, EventArgs e)
        {
            if (!busy)
            {
                busy = true;
                if (!VPL_rb.Checked) { VPL_lead.Checked = Lead_In.Enabled = VPL_only_sectors.Checked = VPL_auto_adj.Checked = Adj_cbm.Checked = false; }
                VPL_presync.Enabled = VPL_auto_adj.Checked;
                Lead_ptn.Enabled = VPL_rb.Checked;
                if (VPL_rb.Checked) VPL_auto_adj.Checked = false;
                VPL_presync.Enabled = VPL_auto_adj.Checked;
                busy = false;
                Vorpal_Rebuild();
            }
        }

        private void Lead_In_ValueChanged(object sender, EventArgs e)
        {
            if (!busy)
            {
                Vorpal_Rebuild();
            }
        }

        private void VPL_only_sectors_CheckedChanged(object sender, EventArgs e)
        {
            if (!busy)
            {
                busy = true;
                if (VPL_only_sectors.Checked)
                {
                    Lead_In.Enabled = VPL_lead.Checked = VPL_auto_adj.Checked = false;
                    VPL_rb.Checked = true;
                }
                busy = false;
                Vorpal_Rebuild();
            }
        }

        private void VPL_Auto_CheckedChanged(object sender, EventArgs e)
        {
            if (!busy)
            {
                busy = true;
                Lead_In.Enabled = VPL_lead.Checked = VPL_only_sectors.Checked = VPL_rb.Checked = false;
                VPL_presync.Enabled = VPL_auto_adj.Checked;
                busy = false;
                Vorpal_Rebuild();
            }
        }

        private void Disp_Data_Click(object sender, EventArgs e)
        {
            if (busy) Data_Viewer(true);
            else Data_Viewer();
        }

        private void Jump_ValueChanged(object sender, EventArgs e)
        {
            View_Jump();
        }

        private void DV_gcr_CheckedChanged(object sender, EventArgs e)
        {
            if (((RadioButton)sender).Checked)
            {
                Data_Viewer();
            }
        }

        private void Data_Sep_SelectedIndexChanged(object sender, EventArgs e)
        {
            Data_Viewer();
        }

        private void Show_sec_CheckedChanged(object sender, EventArgs e)
        {
            if (((CheckBox)sender).Checked) drawn = false;
            if (!busy)
            {
                Check_Before_Draw(false);
            }
        }

        private void Img_Q_SelectedIndexChanged(object sender, EventArgs e)
        {
            drawn = false;
            if (!busy)
            {
                Check_Before_Draw(false);
            }
        }

        private void VD0_ValueChanged(object sender, EventArgs e)
        {
            if (!busy)
            {
                vpl_density[0] = Convert.ToInt32(VD0.Value);
                vpl_density[1] = Convert.ToInt32(VD1.Value);
                vpl_density[2] = Convert.ToInt32(VD2.Value);
                vpl_density[3] = Convert.ToInt32(VD3.Value);
                Vorpal_Rebuild();
            }
        }

        private void B_cancel_Click(object sender, EventArgs e)
        {
            cancel = true;
        }

        private void Re_Align_CheckedChanged(object sender, EventArgs e)
        {
            for (int t = 0; t < tracks; t++)
            {
                if (NDS.cbm[t] == 4)
                {
                    if (Original.OT[t].Length == 0)
                    {
                        Original.OT[t] = new byte[NDG.Track_Data[t].Length];
                        Buffer.BlockCopy(NDG.Track_Data[t], 0, Original.OT[t], 0, NDG.Track_Data[t].Length);
                    }
                    if (!NDG.L_Rot)
                    {
                        Set_Dest_Arrays(Rotate_Loader(NDG.Track_Data[t]), t);
                        NDG.L_Rot = true;
                    }
                    else
                    {
                        if (Original.OT[t].Length != 0)
                        {
                            NDG.Track_Data[t] = new byte[Original.OT[t].Length];
                            Buffer.BlockCopy(Original.OT[t], 0, NDG.Track_Data[t], 0, Original.OT[t].Length);
                            Buffer.BlockCopy(Original.OT[t], 0, NDA.Track_Data[t], 0, Original.OT[t].Length);
                            Buffer.BlockCopy(Original.OT[t], 0, NDA.Track_Data[t], Original.OT[t].Length, MAX_TRACK_SIZE - Original.OT[t].Length);
                        }
                        NDG.Track_Length[t] = NDG.Track_Data[t].Length;
                        NDA.Track_Length[t] = NDG.Track_Length[t] * 8;
                        NDG.L_Rot = false;
                    }
                    displayed = false;
                    drawn = false;
                    if (!busy && !batch)
                    {
                        Check_Before_Draw(false, true);
                        Data_Viewer();
                    }
                }
            }
        }

        private void DB_vpl_CheckedChanged(object sender, EventArgs e)
        {
            //VD0.Visible = VD1.Visible = VD2.Visible = VD3.Visible = DB_vpl.Checked;
            usecpp = !CPP_tog.Checked;
            this.Text = $"Re-Master {ver}";
            if (!CPP_tog.Checked)
            {
                Text += " (C++ Extensions Enabled)";
            }
        }

        private void Debug_Button_Click(object sender, EventArgs e)
        {
            Create_Blank_Disk();
        }

        private void V2_Swap_Headers_CheckedChanged(object sender, EventArgs e)
        {
            V2_swap.Enabled = V2_swap_headers.Checked;
        }

        private void V2_swap_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!busy)
            {
                busy = true;
                V2_Auto_Adj.Checked = true;
                V2_Custom.Checked = V2_Add_Sync.Checked = false;
                if (V2_swap.SelectedIndex == 0) { NDG.newheader[0] = 0x64; NDG.newheader[1] = 0x4e; }
                if (V2_swap.SelectedIndex == 1) { NDG.newheader[0] = 0x64; NDG.newheader[1] = 0x46; }
                if (V2_swap.SelectedIndex == 2) { NDG.newheader[0] = 0x4e; NDG.newheader[1] = 0x64; }
                busy = false;
                V2_Adv_Opts();
            }
        }

        private void DB_cores_ValueChanged(object sender, EventArgs e)
        {
            Cores = Convert.ToInt32(DB_cores.Value);
            Set_Cores(false);
        }

        private void DB_core_override_CheckedChanged(object sender, EventArgs e)
        {
            DB_cores.Enabled = DB_core_override.Checked;
            if (DB_core_override.Checked)
            {
                Cores = Convert.ToInt32(DB_cores.Value);
            }
            else
            {
                Cores = Default_Cores;
            }
            Set_Cores();
        }

        private void ListBox1_DoubleClick(object sender, EventArgs e)
        {
            var a = Batch_List_Box.SelectedIndex;
            if (a >= 0 && a < Batch_List_Box.Items.Count)
            {
                string argument = "/select, \"" + LB_File_List[a].Replace(@"\\", @"\") + "\"";
                if (File.Exists(LB_File_List[a])) Process.Start("explorer.exe", argument);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            try
            {
                cancel = true;
                this.Text = "Closing..";
                Application.Exit();
                Environment.Exit(0);
            }
            catch { }
            this.Close();
        }

        private void RL_Fix_CheckedChanged(object sender, EventArgs e)
        {
            if (!busy && RL_Fix.Checked)
            {
                if (NDS.cbm.Any(x => x == 6)) RL_Remove_Protection();
                else
                {
                    Check_Cyan_Loader(true);
                    Clear_Out_Items();
                    Process_Nib_Data(true, false, false, true);
                }
            }
        }
    }
}