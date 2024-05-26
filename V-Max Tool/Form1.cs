using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace V_Max_Tool
{
    public partial class Form1 : Form
    {
        private bool Auto_Adjust = false; // <- Sets the Auto Adjust feature for V-Max and Vorpal images (for best remastering results)
        private readonly bool debug = false;
        private readonly string ver = " v0.9.84 (beta)";
        private readonly string fix = "(sync_fixed)";
        private readonly string mod = "(modified)";
        private readonly string vorp = "(aligned)";
        private readonly int[] density = { 7672, 7122, 6646, 6230 }; // <- adjusted capacity to account for minor RPM variation higher than 300
        //private int[] vpl_density = { 7760, 7204, 6723, 6302 }; // <- adjusted capacity to account for minor RPM variation higher than 300
        private readonly int[] vpl_density = { 7750, 7106, 6635, 6230 }; // <- adjusted capacity to account for minor RPM variation higher than 300
        private bool error = false;
        private bool cancel = false;
        private bool busy = false;
        private bool nib_error = false;
        private bool g64_error = false;
        private bool batch = false;
        private string nib_err_msg;
        private string g64_err_msg;
        private byte[] v2ldrcbm = new byte[0];
        private byte[] v24e64pal = new byte[0];
        private byte[] v26446ntsc = new byte[0];
        private byte[] v2644entsc = new byte[0];
        private byte loader_padding = 0x55;
        private readonly int min_t_len = 6000;
        Thread w;

        public Form1()
        {
            InitializeComponent();
            this.Text = $"Re-Master V-Max/Vorpal Utility {ver}";
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
            //Process_New_Image(File_List[0]);
            if (File_List.Length > 1)
            {
                using (Message_Center center = new Message_Center(this)) // center message box
                {
                    string t = "Multiple Files Selected";
                    string s = "Only .NIB files will be processed\nand exported as .G64 with the\nAuto-Adjust options\n\nStart Batch-Processing?";
                    DialogResult uc = MessageBox.Show(s, t, MessageBoxButtons.OKCancel, MessageBoxIcon.Information);
                    if (uc.ToString() == "OK")
                    {
                        if (SaveFolder.ShowDialog() == DialogResult.OK)
                        {
                            string sel_path = SaveFolder.SelectedPath.ToString();
                            if (sel_path != "")
                            {
                                List<string> f = new List<string>();
                                for (int i = 0; i < File_List.Length; i++)
                                {
                                    string ext = Path.GetExtension(File_List[i]);
                                    if (ext.ToLower() == supported[0]) f.Add(File_List[i]); // || ext.ToLower() == supported[1]) f.Add(File_List[i]);
                                }
                                Process_Batch(f.ToArray(), sel_path);
                            }
                        }
                    }
                }
            }
            else
            {
                if (System.IO.File.Exists(File_List[0]))
                {
                    fname = Path.GetFileNameWithoutExtension(File_List[0]);
                    fext = Path.GetExtension(File_List[0]);
                }
                Process_New_Image(File_List[0]);
            }
        }

        void Process_Batch(string[] batch_list, string path)
        {
            bool temp = Auto_Adjust;
            busy = true;
            Auto_Adjust = true;
            Set_Auto_Opts();
            busy = false;
            Drag_pic.Visible = Adv_ctrl.Enabled = false;
            Batch_Box.Visible = true;
            batch = true;
            Batch_Bar.Value = 0;
            Batch_Bar.Maximum = 100;
            Batch_Bar.Maximum *= 100;
            Batch_Bar.Value = Batch_Bar.Maximum / 100;
            Task.Run(delegate
            {
                for (int i = 0; i < batch_list.Length; i++)
                {
                    loader_fixed = false;
                    NDG.L_Rot = false;
                    if (!cancel)
                    {
                        if (System.IO.File.Exists(batch_list[i]))
                        {
                            Invoke(new Action(() =>
                            {
                                label8.Text = $"Processing file {i + 1} of {batch_list.Length}";
                                label9.Text = $"{Path.GetFileName(batch_list[i])}";
                                Batch_Bar.Maximum = (int)((double)Batch_Bar.Value / (double)(i + 1) * batch_list.Length);
                            }));
                            fname = Path.GetFileNameWithoutExtension(batch_list[i]);
                            fext = Path.GetExtension(batch_list[0]);
                            if (fext.ToLower() == supported[0]) Batch_NIB(batch_list[i]);
                            //if (fext.ToLower() == supported[1]) Batch_G64(batch_list[i]);
                        }
                    }
                    else break;
                }
                Invoke(new Action(() =>
                {
                    Import_File.Visible = false;
                    Import_Progress_Bar.Value = 0;
                    Batch_Bar.Value = 0;
                    Batch_Box.Visible = false;
                    using (Message_Center center = new Message_Center(this)) // center message box
                    {
                        string t = "";
                        string s = "";
                        if (!cancel)
                        {
                            t = "Done!";
                            s = "Batch processing completed..";
                        }
                        else
                        {
                            t = "Canceled!";
                            s = "Batch processing canceled by user";
                        }
                        MessageBox.Show(s, t, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    cancel = false;
                    busy = true;
                    Auto_Adjust = temp;
                    Set_Auto_Opts();
                    busy = false;
                    Reset_to_Defaults();
                }));
                batch = false;
            });

            void Batch_NIB(string fn)
            {
                FileStream Stream = new FileStream(fn, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                long length = new System.IO.FileInfo(fn).Length;
                tracks = (int)(length - 256) / 8192;
                if ((tracks * 8192) + 256 == length)
                {
                    //Track_Info.Items.Clear();
                    //Set_ListBox_Items(true, false);
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
                    if (head == "MNIB-1541-RAW")
                    {
                        Parse_Nib_Data();
                        if (!error)
                        {
                            Process_Nib_Data(true, false, true);
                            Make_G64($@"{path}\{fname}{fnappend}.g64");
                        }
                        else error = false;
                    }
                }
                GC.Collect();
            }
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
                    Data_Box.Clear();
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
                    Data_Box.Clear();
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
                        var lab = $"Total Tracks {tracks}, G64 Track Size {tr_size:N0} bytes";
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
                loader_fixed = false;
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
                            //listBox1.Visible = true;
                            //byte[] g = new byte[256];
                            //for (int i = 0; i < 256; i++)
                            //{
                            //    g[i] = (byte)i;
                            //    listBox1.Items.Add($"{i} {Hex(g, i, 1)} {Encoding.ASCII.GetString(g, i, 1)}\n");
                            //}
                            linkLabel1.Visible = false;
                            if (Disk_Dir.Checked) Disk_Dir.Focus();
                            Out_Type = get;
                            Save_Disk.Visible = true;
                            Source.Visible = Output.Visible = true;
                            label1.Text = $"{fname}{fext}";
                            label2.Text = l2;
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
        }

        private void Drag_Enter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
        }

        private void Make(object sender, EventArgs e)
        {
            Export_File();
        }

        private void F_load_CheckedChanged(object sender, EventArgs e)
        {
            Fix_Loader_Option(!busy);
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

        private void Dir_View_CheckedChanged(object sender, EventArgs e)
        {
            Dir_screen.Visible = Disk_Dir.Checked;
        }

        private void Button1_Click(object sender, EventArgs e)
        {
            Create_Blank_Disk();
        }

        private void LinkLabel1_LinkClicked(object sender, System.Windows.Forms.LinkLabelLinkClickedEventArgs e)
        {
            this.linkLabel1.LinkVisited = true;
            System.Diagnostics.Process.Start("https://github.com/DarylKrans/V-Max-Sync-Tool");
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
            Data_Viewer();
        }

        private void Jump_ValueChanged(object sender, EventArgs e)
        {
            View_Jump();
        }

        private void DV_gcr_CheckedChanged(object sender, EventArgs e)
        {
            if (!busy)
            {
                if (((RadioButton)sender).Checked)
                {
                    Data_Viewer();
                }
            }
        }

        private void Data_Sep_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!busy)
            {
                Data_Viewer();
            }
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
                        Array.Copy(NDG.Track_Data[t], 0, Original.OT[t], 0, NDG.Track_Data[t].Length);
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
                            Array.Copy(Original.OT[t], 0, NDG.Track_Data[t], 0, Original.OT[t].Length);
                            Array.Copy(Original.OT[t], 0, NDA.Track_Data[t], 0, Original.OT[t].Length);
                            Array.Copy(Original.OT[t], 0, NDA.Track_Data[t], Original.OT[t].Length, 8192 - Original.OT[t].Length);
                        }
                        NDG.Track_Length[t] = NDG.Track_Data[t].Length;
                        NDA.Track_Length[t] = NDG.Track_Length[t] * 8;
                        NDG.L_Rot = false;
                    }
                    displayed = false;
                    drawn = false;
                    if (!busy && !batch)
                    {
                        Check_Before_Draw(false);
                        Data_Viewer();
                    }
                }
            }
        }
    }
}
