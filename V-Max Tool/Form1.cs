using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace V_Max_Tool
{
    public partial class Form1 : Form
    {
        private readonly bool debug = false;
        private readonly string ver = " v0.9.72 (beta)";
        private readonly string fix = "(sync_fixed)";
        private readonly string mod = "(modified)";
        private readonly string vorp = "(aligned)";
        //private readonly int[] density = { 7692, 7142, 6666, 6250 }; // <- Actual capacity as defined by the manual
        private readonly int[] density = { 7672, 7122, 6646, 6230 }; // <- adjusted capacity to account for minor RPM variation higher than 300
        private bool error = false;
        private bool opt = false;
        private bool nib_error = false;
        private bool g64_error = false;
        private string nib_err_msg;
        private string g64_err_msg;
        private readonly int min_t_len = 6000;

        public Form1()
        {
            InitializeComponent();
            this.Text = $"Re-Master V-Max/Vorpal Utility {ver}";
            Init();
            Set_ListBox_Items(true, true);
        }

        void V2_Adv_Opts()
        {
            bool c = false;
            bool p = true;
            if (V2_Auto_Adj.Checked)
            {
                c = true;
                p = false;
                for (int t = 0; t < tracks; t++)
                {
                    if (NDS.cbm[t] == 4)
                    {
                        if (Original.OT[t].Length == 0)
                        {
                            Original.OT[t] = new byte[NDG.Track_Data[t].Length];
                            Array.Copy(NDG.Track_Data[t], 0, Original.OT[t], 0, NDG.Track_Data[t].Length);
                        }
                        int d = Get_Density(NDG.Track_Data[t].Length);
                        byte[] temp = Shrink_Track(NDG.Track_Data[t], d);
                        if (Re_Align.Checked && !NDG.L_Rot)
                        {
                            Rotate_Loader(temp);
                            NDG.L_Rot = true;
                        }
                        Set_Dest_Arrays(temp, t);
                    }
                }
            }
            else
            {
                for (int t = 0; t < tracks; t++)
                {
                    if (NDS.cbm[t] == 4 || NDS.cbm[t] == 1)
                    {
                        if (Original.OT[t].Length != 0)
                        {
                            NDG.Track_Data[t] = new byte[Original.OT[t].Length];
                            Array.Copy(Original.OT[t], 0, NDG.Track_Data[t], 0, Original.OT[t].Length);
                            Array.Copy(Original.OT[t], 0, NDA.Track_Data[t], 0, Original.OT[t].Length);
                            Array.Copy(Original.OT[t], 0, NDA.Track_Data[t], Original.OT[t].Length, 8192 - Original.OT[t].Length);
                        }
                        NDG.Track_Length[t] = NDG.Track_Data[t].Length;
                        NDA.Track_Length[t] = NDG.Track_Length[t] * 8;
                        c = true;

                    }
                }
            }
            int i = Convert.ToInt32(V2_hlen.Value);
            if (i >= V2_hlen.Minimum && i <= V2_hlen.Maximum)
            {
                out_track.Items.Clear();
                out_size.Items.Clear();
                out_dif.Items.Clear();
                Out_density.Items.Clear();
                out_rpm.Items.Clear();
                Process_Nib_Data(c, false, p); // false flag instructs the routine NOT to process CBM tracks again
            }
        }

        void V3_Auto_Adjust()
        {
            bool p = true;
            bool v = false;
            if (V3_Auto_Adj.Checked || Adj_cbm.Checked)
            {
                for (int t = 0; t < tracks; t++)
                {
                    if (NDG.Track_Data[t] != null)
                    {
                        if (NDS.cbm[t] == 1 || NDS.cbm[t] == 3)
                        {
                            if (Original.OT[t].Length == 0)
                            {
                                Original.OT[t] = new byte[NDG.Track_Data[t].Length];
                                Array.Copy(NDG.Track_Data[t], 0, Original.OT[t], 0, NDG.Track_Data[t].Length);
                            }
                        }
                        if (NDS.cbm[t] == 4) Shrink_Short_Sector(t);
                    }
                }
            }
            else
            {
                for (int t = 0; t < tracks; t++)
                {
                    if (NDG.Track_Data[t] != null)
                    {
                        if (NDS.cbm[t] == 4)
                        {
                            NDG.Track_Data[t] = new byte[Original.SG.Length];
                            NDA.Track_Data[t] = new byte[Original.SA.Length];
                            Array.Copy(Original.SG, 0, NDG.Track_Data[t], 0, Original.SG.Length);
                            Array.Copy(Original.SA, 0, NDA.Track_Data[t], 0, Original.SA.Length);
                            NDG.Track_Length[t] = NDG.Track_Data[t].Length;
                            NDA.Track_Length[t] = NDG.Track_Length[t] * 8;
                            NDG.L_Rot = false;
                        }
                        if (NDS.cbm[t] == 1 || (NDS.cbm[t] == 3)) // && NDS.sectors[t] < 16))
                        {
                            if (Original.OT[t].Length != 0)
                            {
                                NDG.Track_Data[t] = new byte[Original.OT[t].Length];
                                Array.Copy(Original.OT[t], 0, NDG.Track_Data[t], 0, Original.OT[t].Length);
                                Array.Copy(Original.OT[t], 0, NDA.Track_Data[t], 0, Original.OT[t].Length);
                                Array.Copy(Original.OT[t], 0, NDA.Track_Data[t], Original.OT[t].Length, NDA.Track_Data[t].Length - Original.OT[t].Length);
                                p = false;
                                v = true;
                            }
                            NDG.Track_Length[t] = NDG.Track_Data[t].Length;
                            NDA.Track_Length[t] = NDG.Track_Length[t] * 8;
                        }
                    }
                }
            }

            out_track.Items.Clear();
            out_size.Items.Clear();
            out_dif.Items.Clear();
            Out_density.Items.Clear();
            out_rpm.Items.Clear();
            if (Adj_cbm.Checked && !V3_Auto_Adj.Checked) p = false;
            Process_Nib_Data(true, p, v); // false flag instructs the routine NOT to process CBM tracks again -- p (true/false) process v-max v3 short tracks
        }

        void Vorpal_Rebuild()
        {
            if (VPL_rb.Checked)
            {
                for (int t = 0; t < tracks; t++)
                {
                    if (NDG.Track_Data[t] != null)
                    {
                        if (NDS.cbm[t] == 5)
                        {
                            if (Original.OT[t].Length == 0)
                            {
                                Original.OT[t] = new byte[NDG.Track_Data[t].Length];
                                Array.Copy(NDG.Track_Data[t], 0, Original.OT[t], 0, NDG.Track_Data[t].Length);
                            }
                        }
                    }
                }
            }
            else
            {
                for (int t = 0; t < tracks; t++)
                {
                    if (NDG.Track_Data[t] != null)
                    {
                        if (NDS.cbm[t] == 5) // && NDS.sectors[t] < 16))
                        {
                            if (Original.OT[t].Length != 0)
                            {
                                NDG.Track_Data[t] = new byte[Original.OT[t].Length];
                                Array.Copy(Original.OT[t], 0, NDG.Track_Data[t], 0, Original.OT[t].Length);
                                Array.Copy(Original.OT[t], 0, NDA.Track_Data[t], 0, Original.OT[t].Length);
                                Array.Copy(Original.OT[t], 0, NDA.Track_Data[t], Original.OT[t].Length, NDA.Track_Data[t].Length - Original.OT[t].Length);
                            }
                            NDG.Track_Length[t] = NDG.Track_Data[t].Length;
                            NDA.Track_Length[t] = NDG.Track_Length[t] * 8;
                        }
                    }
                }
            }

            out_track.Items.Clear();
            out_size.Items.Clear();
            out_dif.Items.Clear();
            Out_density.Items.Clear();
            out_rpm.Items.Clear();
            Process_Nib_Data(true, false, false); // false flag instructs the routine NOT to process CBM tracks again -- p (true/false) process v-max v3 short tracks

        }

        void Process_New_Image(string file)
        {
            string l = "Not ok";
            try
            {
                nib_header = new byte[0];
                g64_header = new byte[684];
                FileStream Stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                if (fext.ToLower() == supported[0])
                {
                    Show_Progress_Bar();
                    long length = new System.IO.FileInfo(file).Length;
                    tracks = (int)(length - 256) / 8192;
                    if ((tracks * 8192) + 256 == length) l = "File Size OK!";
                    Track_Info.Items.Clear();
                    Set_ListBox_Items(true, false);
                    nib_header = new byte[256];
                    Stream.Seek(0, SeekOrigin.Begin);
                    Stream.Read(nib_header, 0, 256);
                    Set_Arrays(tracks);
                    for (int i = 0; i < tracks; i++)
                    {
                        NDS.Track_Data[i] = new byte[8192];
                        Stream.Seek(256 + (8192 * i), SeekOrigin.Begin);
                        Stream.Read(NDS.Track_Data[i], 0, 8192);
                        Original.OT[i] = new byte[0];
                    }
                    Stream.Close();
                    var head = Encoding.ASCII.GetString(nib_header, 0, 13);
                    var hm = "Bad Header";
                    if (head == "MNIB-1541-RAW") hm = "Header Match!";
                    var lab = $"Total Tracks ({tracks}), {l}, {hm}";
                    Process(true, lab);
                }
                if (fext.ToLower() == supported[1])
                {
                    Show_Progress_Bar();
                    Track_Info.Items.Clear();
                    Set_ListBox_Items(true, false);
                    Stream.Seek(0, SeekOrigin.Begin);
                    Stream.Read(g64_header, 0, 684);
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
                            int pos = BitConverter.ToInt32(g64_header, 12 + (i * 4));
                            if (pos != 0)
                            {
                                Stream.Seek(pos, SeekOrigin.Begin);
                                Stream.Read(temp, 0, 2);
                                short ts = BitConverter.ToInt16(temp, 0);
                                NDS.Track_Data[i] = new byte[8192];
                                byte[] tdata = new byte[ts];
                                Stream.Seek(pos + 2, SeekOrigin.Begin);
                                Stream.Read(tdata, 0, ts);
                                NDG.s_len[i] = tdata.Length;
                                Array.Copy(tdata, 0, NDS.Track_Data[i], 0, ts);
                                Array.Copy(tdata, 0, NDS.Track_Data[i], ts, 8192 - ts);
                            }
                            else
                            {
                                NDS.Track_Data[i] = new byte[8192];
                                for (int j = 0; j < NDS.Track_Data[i].Length; j++)
                                {
                                    NDS.Track_Data[i][j] = 0;
                                }
                            }
                        }
                        Stream.Close();
                        var lab = $"Total Tracks ({tracks}), G64 Track Size ({tr_size:N0})";
                        Out_Type = false;
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
                Dir_screen.Clear();
                Dir_screen.Text = "LOAD\"$\",8\nSEARCHING FOR $\nLOADING";
                Task.Run(delegate
                {
                    Parse_Nib_Data();
                    if (!error)
                    {
                        Invoke(new Action(() =>
                        {

                            Process_Nib_Data(true, false, true);
                            Set_ListBox_Items(false, false);
                            Get_Disk_Directory();
                            if (Disk_Dir.Checked) Disk_Dir.Focus();
                            Out_Type = get;
                            Save_Disk.Visible = true;
                            Source.Visible = Output.Visible = true;
                            label1.Text = $"{fname}{fext}";
                            label2.Text = l2;
                            label1.Update();
                            label2.Update();
                            linkLabel1.Visible = false;
                            M_render.Enabled = true;
                            Import_File.Visible = false;
                            Adv_ctrl.Enabled = true;
                            if (Encoding.ASCII.GetString(g64_header, 0, 8) == "GCR-1541" && NDS.cbm.Any(s => s == 5))
                            {
                                using (Message_Center center = new Message_Center(this)) // center message box
                                {
                                    string t = "Output integrity warning!";

                                    string s = "Vorpal tracks detected.\n\nIt is advised to use NIB files when processing Vorpal images\nWhen processing G64's, the output file may not work correctly.";
                                    if (s.ToLower().Contains("source array")) s = "Image is corrupt and cannot be opened";
                                    MessageBox.Show(s, t, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                    error = true;
                                }
                            }
                        }));
                    }
                });
            }

            void Show_Progress_Bar()
            {
                Import_Progress_Bar.Value = 0;
                Import_File.Visible = true;
                Update();
            }

            void Set_Arrays(int len)
            {
                // NDS is the input or source array
                NDS.Track_Data = new byte[len][];
                NDS.Sector_Zero = new int[len];
                NDS.Track_Length = new int[len];
                NDS.D_Start = new int[len];
                NDS.D_End = new int[len];
                NDS.Sector_Zero = new int[len];
                NDS.cbm = new int[len];
                NDS.sectors = new int[len];
                NDS.Header_Len = new int[len];
                NDS.cbm_sector = new int[len][];
                NDS.v2info = new byte[len][];
                NDS.Loader = new byte[0];
                NDS.Total_Sync = new int[len];
                NDS.Disk_ID = new byte[len][];
                // NDA is the destination or output array
                NDA.Track_Data = new byte[len][];
                NDA.Sector_Zero = new int[len];
                NDA.Track_Length = new int[len];
                NDA.D_Start = new int[len];
                NDA.D_End = new int[len];
                NDA.Sector_Zero = new int[len];
                NDA.sectors = new int[len];
                NDA.Total_Sync = new int[len];
                // NDG is the G64 arrays
                NDG.Track_Length = new int[len];
                NDG.Track_Data = new byte[len][];
                NDG.L_Rot = false;
                NDG.s_len = new int[len];
                // Original is the arrays that keep the original track data for the Auto Adjust feature
                Original.A = new byte[0];
                Original.G = new byte[0];
                Original.SA = new byte[0];
                Original.SG = new byte[0];
                Original.OT = new byte[len][];
            }
        }

        private void Drag_Enter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
        }

        private void Drag_Drop(object sender, DragEventArgs e)
        {
            Source.Visible = Output.Visible = false;
            f_load.Text = "Fix Loader Sync";
            Save_Disk.Visible = false;
            sl.DataSource = null;
            out_size.DataSource = null;
            string[] File_List = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (File.Exists(File_List[0]))
            {
                fname = Path.GetFileNameWithoutExtension(File_List[0]);
                fext = Path.GetExtension(File_List[0]);
            }
            Process_New_Image(File_List[0]);
        }

        private void Make(object sender, EventArgs e)
        {
            Export_File();
        }

        private void F_load_CheckedChanged(object sender, EventArgs e)
        {
            int i = 100;
            if (f_load.Checked)
            {
                if (tracks > 0 && NDS.Track_Data.Length > 0)
                {
                    i = Array.FindIndex(NDS.cbm, s => s == 4);
                    if (i < 100 && i > -1)
                    {
                        Original.G = new byte[NDG.Track_Data[i].Length];
                        Original.A = new byte[NDA.Track_Data[i].Length];
                        Array.Copy(NDG.Track_Data[i], 0, Original.G, 0, NDG.Track_Data[i].Length);
                        Array.Copy(NDA.Track_Data[i], 0, Original.A, 0, NDA.Track_Data[i].Length);
                        NDG.Track_Data[i] = Fix_Loader(NDG.Track_Data[i]);
                        NDA.Track_Data[i] = new byte[8192];
                        if (NDG.Track_Data[i].Length < 8192)
                        {
                            try
                            {
                                Array.Copy(NDG.Track_Data[i], 0, NDA.Track_Data[i], 0, NDG.Track_Data[i].Length);
                                Array.Copy(NDG.Track_Data[i], 0, NDA.Track_Data[i], NDG.Track_Data[i].Length, 8192 - NDG.Track_Data[i].Length);
                            }
                            catch { }
                        }
                    }
                }
            }
            if (!f_load.Checked)
            {
                f_load.Text = "Fix Loader Sync";
                if (tracks > 0) i = Array.FindIndex(NDS.cbm, s => s == 4);
                if (i > -1 && i < 100)
                {
                    if (Original.A.Length > 0) { NDA.Track_Data[i] = Original.A; }
                    if (Original.G.Length > 0) { NDG.Track_Data[i] = Original.G; f_load.Text += " ( Restored )"; }
                }
            }
        }

        private void V2_Custom_CheckedChanged(object sender, EventArgs e)
        {
            if (!opt)
            {
                opt = true;
                V2_hlen.Enabled = V2_Custom.Checked;
                if (V2_Custom.Checked) V2_Auto_Adj.Checked = false;
                opt = false;
                V2_Adv_Opts();
            }
        }

        private void AutoAdj_CheckedChanged(object sender, EventArgs e)
        {
            if (!opt)
            {
                opt = true;
                if (V2_Auto_Adj.Checked) V2_Custom.Checked = V2_hlen.Enabled = V2_Add_Sync.Checked = false;
                opt = false;
                V2_Adv_Opts();
            }
        }

        private void V3_Auto_Adj_CheckedChanged(object sender, EventArgs e)
        {
            if (!opt)
            {
                opt = true;
                if (V3_Auto_Adj.Checked) V3_Custom.Checked = V3_hlen.Enabled = false;
                opt = false;
                V3_Auto_Adjust();
            }
        }

        private void V3_Custom_CheckedChanged(object sender, EventArgs e)
        {
            if (!opt)
            {
                opt = true;
                if (V3_Custom.Checked)
                {
                    V3_Auto_Adj.Checked = false;
                    V3_hlen.Enabled = true;
                }
                else V3_hlen.Enabled = false;
                opt = false;
                V3_Auto_Adjust();
            }
        }

        private void Adj_cbm_CheckedChanged(object sender, EventArgs e)
        {
            V3_Auto_Adjust();
        }

        private void V2_Add_Sync_CheckedChanged(object sender, EventArgs e)
        {
            if (!opt)
            {
                opt = true;
                if (V2_Add_Sync.Checked) V2_Auto_Adj.Checked = false;
                opt = false;
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
            Check_Before_Draw(false);
        }

        private void Dir_View_CheckedChanged(object sender, EventArgs e)
        {
            Dir_screen.Visible = Disk_Dir.Checked;
        }

        private void Button1_Click(object sender, EventArgs e)
        {
            Create_Blank_Disk();
        }//
        private void LinkLabel1_LinkClicked(object sender, System.Windows.Forms.LinkLabelLinkClickedEventArgs e)
        {
            this.linkLabel1.LinkVisited = true;
            System.Diagnostics.Process.Start("https://github.com/DarylKrans/V-Max-Sync-Tool");
        }

        private void VPL_lead_CheckedChanged(object sender, EventArgs e)
        {
            if (!opt)
            {
                opt = true;
                Lead_In.Enabled = VPL_lead.Checked;
                if (VPL_lead.Checked)
                {
                    VPL_rb.Checked = true;
                    VPL_only_sectors.Checked = VPL_shrink.Checked = false;
                }
                opt = false;
                Vorpal_Rebuild();
            }
        }

        private void VPL_rb_CheckedChanged(object sender, EventArgs e)
        {
            if (!opt)
            {
                opt = true;
                if (!VPL_rb.Checked) { VPL_lead.Checked = Lead_In.Enabled = VPL_only_sectors.Checked = VPL_shrink.Checked = false; }
                opt = false;
                Vorpal_Rebuild();
            }
        }

        private void Lead_In_ValueChanged(object sender, EventArgs e)
        {
            if (!opt)
            {
                Vorpal_Rebuild();
            }
        }

        private void VPL_only_sectors_CheckedChanged(object sender, EventArgs e)
        {
            if(!opt)
            {
                opt = true;
                if (VPL_only_sectors.Checked)
                {
                    Lead_In.Enabled = VPL_lead.Checked = VPL_shrink.Checked = false;
                    VPL_rb.Checked = true;
                }
                opt = false;
                Vorpal_Rebuild();
            }
        }

        private void VPL_shrink_CheckedChanged(object sender, EventArgs e)
        {
            if (!opt)
            {
                opt = true;
                if (!VPL_rb.Checked) VPL_rb.Checked = true;
                Lead_In.Enabled = VPL_lead.Checked = VPL_only_sectors.Checked = false;
                opt = false;
                Vorpal_Rebuild();
            }
            else VPL_shrink.Checked = false;
        }
    }
}