using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace V_Max_Tool
{
    public partial class Form1 : Form
    {

        bool v2cc = false;
        bool v2aa = false;
        bool v3cc = false;
        bool v3aa = false;
        private string fname = "";
        private string fext = "";
        private string fnappend = "";
        private int tracks = 0;
        private bool displayed = false;
        private byte[] nib_header = new byte[0];
        private byte[] g64_header = new byte[684];
        private readonly string[] supported = { ".nib", ".g64" }; // Supported file extensions list
        // vsec = the CBM sector header values & against byte[] sz
        private readonly string[] valid_cbm = { "52-40-05-28", "52-40-05-2C", "52-40-05-48", "52-40-05-4C", "52-40-05-38", "52-40-05-3C", "52-40-05-58", "52-40-05-5C",
            "52-40-05-24", "52-40-05-64", "52-40-05-68", "52-40-05-6C", "52-40-05-34", "52-40-05-74", "52-40-05-78", "52-40-05-54", "52-40-05-A8",
            "52-40-05-AC", "52-40-05-C8", "52-40-05-CC", "52-40-05-B8" };
        // vmax = the block header values of V-Max v2 sectors (non-CBM sectors)
        private readonly string[] secF = { "NDOS", "CBM", "V-Max v2", "V-Max v3", "Loader", "Vorpal", "Unformatted" };
        private readonly int[] invalid_char = { 0, 1, 2, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 95,
            128, 129, 130, 131, 132, 133, 134, 135, 136, 137, 138, 139 };
        private int[] jt = new int[42];

        void Parse_Nib_Data()
        {
            Invoke(new Action(() =>
            {
                Import_Progress_Bar.Value = 0;
                Import_Progress_Bar.Maximum = 100;
                Import_Progress_Bar.Maximum *= 100;
                Import_Progress_Bar.Value = Import_Progress_Bar.Maximum / 100;
                Import_File.Visible = true;
                Track_Info.BeginUpdate();
            }));
            bool ldr = false; int cbm = 0; int vmx = 0; int vpl = 0;
            double ht;
            bool halftracks = false;
            string[] f;
            string[] headers;
            string tr = "Track";
            string le = "Length";
            string fm = "Format";
            string bl = "** Potentially bad loader! **";
            var a = tracks; // <- used to show analyse progress on progress bar when processing multi-threaded
            if (tracks > 42)
            {
                halftracks = true;
                ht = 0.5;
            }
            else ht = 0;
            if (!manualRender) // <- if CPU is determined to be fast, Checking track format is multi-threaded to improve speed 
            {
                Thread[] tt = new Thread[tracks];
                Invoke(new Action(() => label5.Text = "Analyzing Tracks.."));
                for (int i = 0; i < tracks; i++)
                {
                    int x = i;
                    tt[i] = new Thread(new ThreadStart(() => Get_Fmt(x)));
                    tt[i].Start();
                }
                for (int i = 0; i < tracks; i++) tt[i]?.Join();
            }
            //manualRender = true;
            int t;
            var color = Color.Black;
            for (int i = 0; i < tracks; i++)
            {
                if (halftracks) ht += .5; else ht += 1;
                if (manualRender) Get_Fmt(i); // NDS.cbm[track] = Get_Data_Format(NDS.Track_Data[track]);
                if (NDS.cbm[i] == 0)
                {
                    int l = Get_Track_Len(NDS.Track_Data[i]);
                    Invoke(new Action(() =>
                    {
                        if (l > 6000 && l < 8192)
                        {
                            if (tracks > 42) t = i / 2 + 1; else t = i + 1;
                            NDS.Track_Length[i] = l << 3;
                            NDS.D_Start[i] = 0;
                            NDS.D_End[i] = l;
                            NDS.sectors[i] = 0;
                            NDS.Sector_Zero[i] = 0;
                            Track_Info.Items.Add(new LineColor { Color = Color.Blue, Text = $"{tr} {t} {fm} : {secF[NDS.cbm[i]]}" });
                            Track_Info.Items.Add(new LineColor { Color = Color.Black, Text = $"Track Length {l}" });
                        }
                    }));
                }
                Invoke(new Action(() =>
                {
                    Import_Progress_Bar.Maximum = (int)((double)Import_Progress_Bar.Value / (double)(i + 1) * tracks);
                    if (tracks <= 42) label5.Text = $"Processing Track {(int)ht + 1} : {secF[NDS.cbm[i]]}";
                    else if (i % 2 == 0) label5.Text = $"Processing Track {(int)ht + 1} : {secF[NDS.cbm[i]]}";
                    Update();
                }));
                if (NDS.cbm[i] == 1)
                {
                    int[] junk;
                    cbm++;
                    if (tracks > 42) t = i / 2 + 1; else t = i + 1;
                    Invoke(new Action(() =>
                    {
                        Track_Info.Items.Add(new LineColor { Color = Color.Blue, Text = $"{tr} {t} {fm} : {secF[NDS.cbm[i]]}" });
                    }));
                    (NDS.D_Start[i], NDS.D_End[i], NDS.Sector_Zero[i], NDS.Track_Length[i], f, NDS.sectors[i], NDS.cbm_sector[i], NDS.Total_Sync[i], NDS.Disk_ID[i], junk) = Find_Sector_Zero(NDS.Track_Data[i], true);
                    Invoke(new Action(() =>
                    {
                        for (int j = 0; j < f.Length; j++)
                        {
                            if (j >= f.Length - 3) color = Color.Black; else color = Color.FromArgb(40, 40, 40);
                            if (f[j].ToLower().Contains("(0)*")) color = Color.FromArgb(255, 255, 255);
                            Track_Info.Items.Add(new LineColor { Color = color, Text = $"{f[j]}" });
                        }
                        Track_Info.Items.Add(" ");
                    }));
                    NDA.sectors[i] = NDS.sectors[i];
                }
                if (NDS.cbm[i] == 2)
                {
                    vmx++;
                    (NDA.Track_Data[i], NDS.D_Start[i], NDS.D_End[i], NDS.Sector_Zero[i], NDS.Track_Length[i], headers, NDS.sectors[i], NDS.v2info[i]) = Get_V2_Track_Info(NDS.Track_Data[i], i);
                    color = Color.Blue;
                    for (int j = 0; j < headers.Length; j++)
                    {
                        if (j > 0) color = Color.FromArgb(110, 0, 110);
                        if (j >= headers.Length - 3 || headers[j].ToLower().Contains("gap")) color = Color.Black;
                        if (headers[j].ToLower().Contains("(0)*")) color = Color.FromArgb(230, 0, 230);
                        Invoke(new Action(() => Track_Info.Items.Add(new LineColor { Color = color, Text = headers[j] })));
                    }
                }
                if (NDS.cbm[i] == 3)
                {
                    vmx++;
                    if (tracks > 42) t = i / 2 + 1; else t = i + 1;
                    Invoke(new Action(() => Track_Info.Items.Add(new LineColor { Color = Color.Blue, Text = $"{tr} {t} {fm} : {secF[NDS.cbm[i]]}" })));
                    int len;
                    (f, NDS.D_Start[i], NDS.D_End[i], NDS.Sector_Zero[i], len, NDS.sectors[i], NDS.Header_Len[i]) = Get_vmv3_track_length(NDS.Track_Data[i], i);
                    NDS.Track_Length[i] = len * 8;
                    NDS.Sector_Zero[i] *= 8;
                    NDA.sectors[i] = NDS.sectors[i];
                    Invoke(new Action(() =>
                    {
                        for (int j = 0; j < f.Length; j++)
                        {
                            if (j >= f.Length - 2) color = Color.Black; else color = Color.DarkGreen;
                            if (f[j].ToLower().Contains("(0)*")) color = Color.LightGreen;
                            Track_Info.Items.Add(new LineColor { Color = color, Text = $"{f[j]}" });
                        }
                        Track_Info.Items.Add(" ");
                    }));
                }
                if (NDS.cbm[i] == 4)
                {
                    ldr = true;
                    if (tracks > 42) t = i / 2 + 1; else t = i + 1;
                    int q = 0;
                    if (fext.ToLower() == ".g64") q = NDG.s_len[i];
                    else (q, NDS.Track_Data[i]) = (Get_Loader_Len(NDS.Track_Data[i], 0, 80, 7000));
                    NDS.Track_Length[i] = q * 8;
                    NDG.Track_Data[i] = new byte[NDS.Track_Length[i] / 8];
                    Array.Copy(NDS.Track_Data[i], 0, NDG.Track_Data[i], 0, NDG.Track_Data[i].Length);
                    NDG.Track_Length[i] = NDG.Track_Data[i].Length;
                    NDA.Track_Length[i] = NDG.Track_Data[i].Length * 8;
                    NDA.Track_Data[i] = NDS.Track_Data[i];
                    Invoke(new Action(() =>
                    {
                        Track_Info.Items.Add(new LineColor { Color = Color.DarkBlue, Text = $"{tr} {t} {fm} : {secF[NDS.cbm[i]]} {tr} {le} ({NDG.Track_Data[i].Length})" });
                        if (NDG.Track_Data[i].Length > 7400) Track_Info.Items.Add(bl);
                        Track_Info.Items.Add(" ");
                    }));
                }
                /// --------------------------------------------------------------------------------------------------------------------------------------------------------------- 

                if (NDS.cbm[i] == 5)
                {
                    vpl++;
                    f = new string[0];
                    (NDG.Track_Data[i], NDS.D_Start[i], NDS.D_End[i], NDS.Track_Length[i], NDS.Header_Len[i], NDS.sectors[i], NDS.cbm_sector[i], f) = Get_Vorpal_Track_Length(NDS.Track_Data[i], i);
                    if (tracks > 42) t = i / 2 + 1; else t = i + 1;
                    Invoke(new Action(() =>
                    {
                        Track_Info.Items.Add(new LineColor { Color = Color.Blue, Text = $"{tr} {t} {fm} : {secF[NDS.cbm[i]]}" });
                        for (int j = 0; j < f.Length; j++)
                        {
                            Track_Info.Items.Add(new LineColor { Color = Color.DarkBlue, Text = f[j] });
                        }
                        Track_Info.Items.Add(new LineColor { Color = Color.Black, Text = $"Track Length : ({(NDS.D_End[i] - NDS.D_Start[i] >> 3)}) Sectors ({NDS.sectors[i]})" });
                        Track_Info.Items.Add(" ");
                    }));
                    if (NDG.Track_Data[i] != null)
                    {
                        if (NDS.cbm[i] == 5)
                        {
                            if (Original.OT[i].Length == 0)
                            {
                                Original.OT[i] = new byte[NDG.Track_Data[i].Length];
                                Array.Copy(NDG.Track_Data[i], 0, Original.OT[i], 0, NDG.Track_Data[i].Length);
                            }
                        }
                    }
                }

                if (NDS.D_Start[i] == 0 && NDS.D_End[i] == 0 && NDS.Track_Length[i] == 0)
                {
                    NDS.Track_Length[i] = Get_Track_Len(NDS.Track_Data[i]);
                    if (NDS.Track_Length[i] > 32 && NDS.Track_Length[i] < 8192)
                    {
                        NDA.Track_Data[i] = new byte[8192];
                        NDG.Track_Data[i] = new byte[NDS.Track_Length[i]];
                        Array.Copy(NDS.Track_Data[i], NDG.Track_Data[i], NDS.Track_Length[i]);
                        Array.Copy(NDS.Track_Data[i], NDA.Track_Data[i], NDS.Track_Data[i].Length);
                        NDA.Track_Length[i] = NDG.Track_Length[i] = NDG.Track_Data[i].Length;
                        NDS.Track_Length[i] *= 8; NDS.D_Start[i] = 0; NDS.D_End[i] = NDS.Track_Length[i];
                    }
                    else { NDS.Track_Length[i] = 0; }
                }
                color = Color.Black;
                Invoke(new Action(() =>
                {
                    if (NDS.Track_Length[i] > 6000 && NDS.cbm[i] != 6 && NDS.cbm[i] != 0)
                    {
                        var d = Get_Density(NDS.Track_Length[i] >> 3);
                        string e = "";
                        if ((ht >= 31 && d != 3) || (ht >= 25 && ht < 31 && d != 2) || (ht >= 18 && ht < 25 && d != 1) || (ht >= 0 && ht < 18 && d != 0)) e = " [!]";
                        if (NDS.cbm[i] == 1) color = Color.Black;
                        if (NDS.cbm[i] == 2) color = Color.DarkMagenta;
                        if (NDS.cbm[i] == 3) color = Color.Green;
                        if (NDS.cbm[i] == 4) color = Color.Blue;
                        if (NDS.cbm[i] == 5) color = Color.DarkCyan;
                        sf.Items.Add(new LineColor { Color = color, Text = $"{secF[NDS.cbm[i]]}" });
                        sl.Items.Add((NDS.Track_Length[i] >> 3).ToString("N0"));
                        ss.Items.Add(NDS.sectors[i]);
                        strack.Items.Add(ht);
                        sd.Items.Add($"{3 - d}{e}");
                    }
                }));
            }
            Invoke(new Action(() =>
            {
                Track_Info.EndUpdate();
                if (NDS.cbm.Any(s => s == 4)) f_load.Visible = true; else f_load.Visible = false;
                Check_Adv_Opts();
                if (ldr) Loader_Track.Text = "Loader Track : Yes"; else Loader_Track.Text = "Loader Track : No";
                CBM_Tracks.Text = $"CBM tracks : {cbm}";
                if (vmx > 0) Protected_Tracks.Text = $"V-Max tracks : {vmx}";
                if (vpl > 0) Protected_Tracks.Text = $"Vorpal tracks : {vpl}";
                Protected_Tracks.Visible = (vmx > 0 || vpl > 0);
                if (tracks > 42)
                {
                    halftracks = true;
                    ht = 0.5;
                }
                else ht = 0;
                bool cust_dens = false;
                bool v2 = false;
                bool v3 = false;
                for (int i = 0; i < tracks; i++)
                {
                    if (halftracks) ht += .5; else ht += 1;
                    if (NDS.cbm[i] > 0 && NDS.cbm[i] < 5)
                    {
                        var d = Get_Density(NDS.Track_Length[i] >> 3);
                        if ((ht >= 0 && ht < 18 && d != 0) || (ht >= 18 && ht < 25 && d != 1) || (ht >= 25 && ht < 31 && d != 2) || (ht >= 31 && ht < 43 && d != 3)) cust_dens = true;
                        if (NDS.cbm[i] == 2) v2 = true;
                        if (NDS.cbm[i] == 3) v3 = true;
                    }
                }
                if (!cust_dens) Cust_Density.Text = "Track Densities : Standard"; else Cust_Density.Text = "Track Densities : Custom";
                if (v2) VM_Ver.Text = "Protection : V-Max v2";
                if (v3) VM_Ver.Text = "Protection : V-Max v3";
                if (!v2 && !v3) VM_Ver.Text = "Protection : V-Max (CBM)";
                if (NDS.cbm.Any(s => s == 5)) VM_Ver.Text = "Protection : Vorpal";
                
            }));

            void Get_Fmt(int trk)
            {
                NDS.cbm[trk] = Get_Data_Format(NDS.Track_Data[trk]);
                a--;
                if (!manualRender)
                {
                    Invoke(new Action(() =>
                    {
                        Import_Progress_Bar.Maximum = (int)((double)Import_Progress_Bar.Value / (double)((tracks - a) + 1) * tracks);
                        Update();
                    }));
                }
            }
        }

        void Process_Nib_Data(bool cbm, bool short_sector, bool rb_vm)
        {
            double ht;
            bool halftracks = false;
            string[] f;
            bool v2a = false;
            bool v3a = false;
            bool vpa = false;
            if (tracks > 42)
            {
                halftracks = true;
                ht = 0.5;
            }
            else ht = 0;
            Color color = new Color(); // = Color.Green;
            Invoke(new Action(() =>
            {
                Adj_pbar.Value = 0;
                Adj_pbar.Maximum = 100;
                Adj_pbar.Maximum *= 100;
                Adj_pbar.Value = Adj_pbar.Maximum / 100;
                Adj_pbar.Visible = true;
            }));
            for (int i = 0; i < tracks; i++)
            {
                if (halftracks) ht += .5; else ht += 1;
                if (NDS.Track_Length[i] > 0 && NDS.cbm[i] != 0)
                {
                    if (i - 1 > 0) Invoke(new Action(() => Adj_pbar.Maximum = (int)((double)Adj_pbar.Value / (double)(i + 1) * tracks)));
                    if (NDS.cbm[i] == 0) Process_Ndos(i);
                    if (NDS.cbm[i] == 1) Process_CBM(i);
                    if (NDS.cbm[i] == 2) Process_VMAX_V2(i);
                    if (NDS.cbm[i] == 3) Process_VMAX_V3(i);
                    if (NDS.cbm[i] == 4) Process_Loader(i);
                    if (NDS.cbm[i] == 5) Process_Vorpal(i);
                    if (NDA.Track_Length[i] > 0 && NDS.cbm[i] != 6)
                    {
                        out_size.Items.Add((NDA.Track_Length[i] / 8).ToString("N0"));
                        out_dif.Items.Add((NDA.Track_Length[i] - NDS.Track_Length[i] >> 3).ToString("+#;-#;0"));
                        string o = "";
                        var d = Get_Density(NDG.Track_Data[i].Length);
                        string e = "";
                        if ((ht >= 31 && d != 3) || (ht >= 25 && ht < 31 && d != 2) || (ht >= 18 && ht < 25 && d != 1) || (ht >= 0 && ht < 18 && d != 0)) e = " [!]";
                        if (NDG.Track_Data[i].Length > density[d])
                        {
                            if (NDG.Track_Data[i].Length > density[d] + 3) color = Color.Red;
                            if (NDG.Track_Data[i].Length > density[d] && NDG.Track_Data[i].Length < density[d] + 5) color = Color.Goldenrod;
                            o = $" + {NDG.Track_Data[i].Length - density[d]}";
                        }
                        else color = Color.Green;
                        if (NDG.Track_Data[i].Length < density[d]) o = $" - {density[d] - NDG.Track_Data[i].Length}";
                        Out_density.Items.Add(new LineColor { Color = color, Text = $"{3 - d}{e}{o}" });
                        out_track.Items.Add(ht);
                        double r = Math.Round(((double)density[Get_Density(NDA.Track_Length[i] >> 3)] / (double)(NDA.Track_Length[i] >> 3) * 300), 1);
                        if (r > 300) r = Math.Floor(r);
                        if (r == 300 && r < 301) color = Color.FromArgb(0, 30, 255);
                        if ((r >= 301 && r < 302) || (r < 300 && r >= 299)) color = Color.DarkGreen;
                        if (r > 302 || (r < 299 && r >= 297)) color = Color.Purple;
                        if (r < 297) color = Color.Brown;
                        out_rpm.Items.Add(new LineColor { Color = color, Text = $"{r:0.0}" });
                    }
                }
                else { NDA.Track_Data[i] = NDS.Track_Data[i]; }
            }
            Invoke(new Action(()=> Adj_pbar.Visible = false));
            if (!busy && Adv_ctrl.SelectedTab == Adv_ctrl.TabPages["tabPage2"] && !manualRender) Check_Before_Draw(false);
            if (Adv_ctrl.Controls[2] != Adv_ctrl.SelectedTab) displayed = false;
            if (Adv_ctrl.Controls[0] != Adv_ctrl.SelectedTab) drawn = false;

            if (!busy) Data_Viewer();

            (bool, bool, bool) Check_Tabs()
            {
                bool a = (V2_Auto_Adj.Checked && Tabs.TabPages.Contains(Adv_V2_Opts));
                bool b = (V3_Auto_Adj.Checked && Tabs.TabPages.Contains(Adv_V3_Opts));
                bool c = (VPL_auto_adj.Checked && Tabs.TabPages.Contains(Vpl_adv));
                return (a, b, c);
            }

            void Process_Ndos(int trk)
            {
                NDA.Track_Data[trk] = NDS.Track_Data[trk];
                NDG.Track_Data[trk] = new byte[NDS.Track_Length[trk >> 3]];
                Array.Copy(NDS.Track_Data[trk], 0, NDG.Track_Data[trk], 0, NDS.D_End[trk] - NDS.D_Start[trk]);
                NDA.Track_Length[trk] = NDG.Track_Length[trk];
            }

            void Process_CBM(int trk)
            {
                var track = trk;
                if (tracks > 42) track = (trk / 2) + 1; else track += 1;
                int exp_snc = 40;   // expected sync length.  (sync will be adjusted to this value if it is >= minimum value (or) =< ignore value
                int min_snc = 16;   // minimum sync length to signal this is a sync marker that needs adjusting
                int ign_snc = 80;   // ignore sync if it is >= to value
                var d = 0;
                byte[] temp = new byte[0];
                if (cbm)
                {
                    bool rebuild = true;
                    try
                    {
                        temp = Adjust_Sync_CBM(NDS.Track_Data[trk], exp_snc, min_snc, ign_snc, NDS.D_Start[trk], NDS.D_End[trk], NDS.Sector_Zero[trk], NDS.Track_Length[trk], trk);
                        if (temp != null)
                        {
                            if (Original.OT[trk].Length == 0)
                            {
                                Original.OT[trk] = new byte[temp.Length];
                                Array.Copy(temp, 0, Original.OT[trk], 0, temp.Length);
                            }
                        }
                        (v2a, v3a, vpa) = Check_Tabs();
                        if (v2a || v3a || vpa || Adj_cbm.Checked || (NDS.cbm.Any(s => s == 2) && v2aa) || (NDS.cbm.Any(s => s == 3) && v3aa))
                        {
                            if (track == 18 && NDS.sectors[trk] != 19) rebuild = false;
                            if (rebuild)
                            {
                                d = Get_Density(NDS.Track_Length[trk] >> 3);
                                temp = Rebuild_CBM(NDS.Track_Data[trk], NDS.sectors[trk], NDS.Disk_ID[trk], d, trk);
                                Set_Dest_Arrays(temp, trk);
                            }
                            else rebuild = true;

                        }
                        Set_Dest_Arrays(temp, trk);
                        (NDA.D_Start[trk], NDA.D_End[trk], NDA.Sector_Zero[trk], NDA.Track_Length[trk], f, NDA.sectors[trk], NDS.cbm_sector[trk], NDA.Total_Sync[trk], NDS.Disk_ID[trk], NDS.sector_pos[trk]) = Find_Sector_Zero(NDA.Track_Data[trk], false);
                        f[0] = "";
                    }
                    catch
                    {
                        if (!error)
                        {
                            using (Message_Center center = new Message_Center(this)) // center message box
                            {
                                string t = "This image is not compatible with this program!";
                                string m = "Image data may be corrupt or unsupported format";
                                MessageBox.Show(m, t, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                error = true;
                            }
                        }
                    }
                }
                if (!(V3_Auto_Adj.Checked || V3_Custom.Checked || VPL_rb.Checked))
                {
                    if (Adj_cbm.Checked) fnappend = mod;
                    else fnappend = fix;
                }
            }

            void Process_VMAX_V2(int trk)
            {
                busy = true;
                if (V3_Auto_Adj.Checked || V3_Custom.Checked)
                {
                    if (V3_Auto_Adj.Checked) v3aa = true; else v3aa = false;
                    if (V3_Custom.Checked) v3cc = true; else v3cc = false;
                    V3_Auto_Adj.Checked = V3_Custom.Checked = false;
                }
                V3_Auto_Adj.Checked = V3_Custom.Checked = false;
                if (rb_vm)
                {

                    byte[] temp = Adjust_V2_Sync(NDS.Track_Data[trk], NDS.D_Start[trk], NDS.D_End[trk], NDS.v2info[trk], true, trk);
                    if (NDS.v2info[trk].Length > 0 && NDS.Loader.Length == 0)
                    {
                        NDS.Loader = new byte[3];
                        NDS.Loader[0] = NDS.v2info[trk][0];
                        NDS.Loader[1] = NDS.v2info[trk][1];
                        NDS.Loader[2] = NDS.v2info[trk][3];
                    }
                    NDA.sectors[trk] = NDS.sectors[trk];
                    Set_Dest_Arrays(temp, trk);
                }
                if (v2aa) V2_Auto_Adj.Checked = true;
                if (v2cc) V2_Custom.Checked = true;
                if (V2_Auto_Adj.Checked && NDS.sectors[trk] > 12)
                {
                    if (Original.OT[trk].Length == 0)
                    {
                        Original.OT[trk] = new byte[NDG.Track_Data[trk].Length];
                        Array.Copy(NDG.Track_Data[trk], 0, Original.OT[trk], 0, NDG.Track_Data[trk].Length);
                    }
                    byte[] tdata;
                    (tdata, NDA.D_Start[trk], NDA.D_End[trk], NDA.Sector_Zero[trk]) = Rebuild_V2(NDG.Track_Data[trk], NDS.sectors[trk], NDS.v2info[trk], trk);
                    Set_Dest_Arrays(tdata, trk);
                }
                if (V2_Auto_Adj.Checked || V2_Custom.Checked || V2_Add_Sync.Checked) fnappend = mod; // "(sync_fixed)(modified)";
                else fnappend = fix;
                busy = false;
            }

            void Process_VMAX_V3(int trk)
            {
                busy = true;
                if (V2_Auto_Adj.Checked || V2_Custom.Checked)
                {
                    if (V2_Auto_Adj.Checked) v2aa = true; else v2aa = false;
                    if (V2_Custom.Checked) v2cc = true; else v2cc = false;
                    V2_Auto_Adj.Checked = V2_Custom.Checked = false;
                }
                V2_Auto_Adj.Checked = V2_Custom.Checked = V2_Add_Sync.Checked = false;
                if (V3_Auto_Adj.Checked || V3_Custom.Checked) fnappend = mod;
                else fnappend = fix;
                if (rb_vm)
                {
                    if (!(short_sector && NDS.sectors[trk] < 16))
                    {
                        (NDG.Track_Data[trk], NDA.Track_Length[trk], NDA.Sector_Zero[trk]) =
                            Adjust_Vmax_V3_Sync(NDS.Track_Data[trk], NDS.D_Start[trk], NDS.D_End[trk], NDS.Sector_Zero[trk]);
                    }
                    else Shrink_Short_Sector(trk);
                }
                NDG.Track_Length[trk] = NDG.Track_Data[trk].Length;
                if (v3aa) V3_Auto_Adj.Checked = true;
                if (v3cc) V3_Custom.Checked = true;
                if (V3_Auto_Adj.Checked)
                {
                    if (Original.OT[trk].Length == 0)
                    {
                        Original.OT[trk] = new byte[NDG.Track_Data[trk].Length];
                        Array.Copy(NDG.Track_Data[trk], 0, Original.OT[trk], 0, NDG.Track_Data[trk].Length);
                    }
                    byte[] temp = Rebuild_V3(NDG.Track_Data[trk], trk);
                    Set_Dest_Arrays(temp, trk);
                }
                if (NDG.Track_Data[trk].Length > 0)
                {
                    try
                    {
                        NDA.Track_Data[trk] = new byte[8192];
                        Array.Copy(NDG.Track_Data[trk], 0, NDA.Track_Data[trk], 0, NDG.Track_Data[trk].Length);
                        Array.Copy(NDG.Track_Data[trk], 0, NDA.Track_Data[trk], NDG.Track_Data[trk].Length, 8192 - NDG.Track_Data[trk].Length);
                        Original.OT[trk] = new byte[NDG.Track_Data[trk].Length];
                        Array.Copy(NDG.Track_Data[trk], 0, Original.OT[trk], 0, NDG.Track_Data[trk].Length);
                        NDA.sectors[trk] = NDS.sectors[trk];
                    }
                    catch { }
                }
                busy = false;
            }

            void Process_Loader(int trk)
            {
                if (Original.SG.Length == 0)
                {
                    Original.SG = new byte[NDG.Track_Data[trk].Length];
                    Original.SA = new byte[NDA.Track_Data[trk].Length];
                    Array.Copy(NDG.Track_Data[trk], 0, Original.SG, 0, NDG.Track_Data[trk].Length);
                    Array.Copy(NDA.Track_Data[trk], 0, Original.SA, 0, NDA.Track_Data[trk].Length);
                }
                if (NDG.Track_Length[trk] > 7600) Shrink_Loader(trk);
                if (f_load.Checked) (NDG.Track_Data[trk]) = Fix_Loader(NDG.Track_Data[trk]);
                if ((NDS.cbm.Any(s => s == 2) || NDS.cbm.Any(s => s == 3)))
                {
                    if (V2_Auto_Adj.Checked || V3_Auto_Adj.Checked) Shrink_Loader(trk);
                    if (Re_Align.Checked || V3_Auto_Adj.Checked || V2_Auto_Adj.Checked)
                    {
                        if (!NDG.L_Rot) NDG.Track_Data[trk] = Rotate_Loader(NDG.Track_Data[trk]);
                        NDG.L_Rot = true;
                    }
                }
                NDA.Track_Data[trk] = new byte[8192];
                if (NDG.Track_Data[trk].Length < 8192)
                {
                    try
                    {
                        Array.Copy(NDG.Track_Data[trk], 0, NDA.Track_Data[trk], 0, NDG.Track_Data[trk].Length);
                        Array.Copy(NDG.Track_Data[trk], 0, NDA.Track_Data[trk], NDG.Track_Data[trk].Length, 8192 - NDG.Track_Data[trk].Length);
                    }
                    catch { }
                }
            }

            void Process_Vorpal(int trk)
            {
                byte[] temp = new byte[NDG.Track_Data[trk].Length]; // + s - 1];
                Array.Copy(NDG.Track_Data[trk], 0, temp, 0, NDG.Track_Data[trk].Length);
                if (VPL_rb.Checked || VPL_auto_adj.Checked) temp = Rebuild_Vorpal(temp, trk);
                Set_Dest_Arrays(temp, trk);
                if (NDS.cbm.Any(ss => ss == 5))
                {
                    if (VPL_rb.Checked || Adj_cbm.Checked) fnappend = mod; else fnappend = vorp;
                }
            }

            void Shrink_Loader(int trk)
            {
                byte[] temp = Shrink_Track(NDG.Track_Data[trk], 1);
                Set_Dest_Arrays(temp, trk);
            }
        }

        int Get_Track_Len(byte[] data)
        {
            int p = 0;
            if (data != null)
            {
                byte[] star = new byte[16];
                Array.Copy(data, 0, star, 0, 16);
                byte[] comp = new byte[8176];
                Array.Copy(data, 16, comp, 0, 8176);

                for (p = 16; p < comp.Length; p++)
                {
                    if (comp.Skip(p).Take(star.Length).SequenceEqual(star))
                    {
                        break;
                    }
                }
            }
            return p + 16;
        }

        int Get_Data_Format(byte[] data)
        {
            int t = 0;
            int csec = 0;
            byte[] comp = new byte[4];
            if (Check_Loader(data)) return 4;
            for (int i = 0; i < data.Length - comp.Length; i++)
            {
                Array.Copy(data, i, comp, 0, comp.Length);
                t = Compare(comp);
                if (t == 3 && i + 20 < data.Length)
                {
                    for (int j = 0; j < 20; j++) if (data[i + j] == 0xee) return 3;
                    t = 0;
                }

                if (t != 0) break;
            }
            if (t == 1 || t == 0 && !data.All(ss => ss == 0x00))
            {
                byte[] temp = new byte[0];
                bool c;
                int y = 0;
                for (int i = 0; i < 16; i++)
                {
                    c = Find_Sector(data, i + 1);
                    if (c) y++;
                }
                if (y > 4) return 1;
                else
                {
                    byte[] ncomp = new byte[vpl_s0.Length];
                    int pos = 0;
                    byte[] tdata = new byte[data.Length];
                    Array.Copy(data, 0, tdata, 0, data.Length);
                    BitArray source = new BitArray(Flip_Endian(tdata));
                    BitArray dest = new BitArray(source.Count);
                    BitArray scomp = new BitArray(vpl_s0.Length * 8);
                    while (pos < source.Length - vpl_s0.Length * 8)
                    {
                        for (int j = 0; j < scomp.Count; j++)
                        {
                            scomp[j] = source[pos + j];
                        }
                        scomp.CopyTo(ncomp, 0);
                        ncomp = Flip_Endian(ncomp);
                        if (Hex_Val(ncomp) == Hex_Val(vpl_s0) || Hex_Val(ncomp) == Hex_Val(vpl_s1)) return 5;
                        pos++;
                    }
                }
            }
            if (t == 0) t = Check_Blank(data);
            return t;

            int Compare(byte[] d)
            {
                if (Hex_Val(d).Contains(v2))
                {
                    if ((d[0] == 0x64 || d[0] == 0x4e))
                    {
                        /// --------------------------      Remove this if errors occur ------------------------------------
                        if (NDS.cbm.Any(s => s == 5)) return 5;
                        /// ------------------------------------------------------------------------------------------------
                        return 2;
                    }
                }
                if ((Hex_Val(d)).Contains(v3)) return 3; // { vm3s++; if (vm3s > 1) return 3; }  //return 3;
                if (d[0] == sz[0])
                {
                    d[1] &= sz[1]; d[2] &= sz[2]; d[3] &= sz[3];
                    if (valid_cbm.Contains(Hex_Val(d))) { csec++; if (csec > 1) return 1; } // change csec > 6 if there are issues
                }
                return 0;
            }

            int Check_Blank(byte[] d)
            {
                int b = 0;
                int snc = 0;
                byte[] blank = new byte[] { 0x00, 0x11, 0x22, 0x44, 0x45, 0x14, 0x12, 0x51, 0x88 };
                for (int i = 0; i < d.Length; i++)
                {
                    if (blank.Any(s => s == d[i])) b++;
                    if (d[i] == 0xff) snc++;
                }
                if (snc > 10) return 0;
                if (b > 4000) return 6;
                else return 0;
            }

            bool Check_Loader(byte[] d)
            {
                byte[][] p = new byte[10][];
                // byte[] p contains a list of commonly repeating patters in the V-Max track 20 loader
                // the following (for) statement checks the track for these patters, if 30 matches are found, we assume its a loader track
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
                int l = 0;
                byte[] cmp = new byte[4];
                for (int i = 0; i < d.Length - cmp.Length; i++)
                {
                    Array.Copy(d, i, cmp, 0, cmp.Length);
                    for (int j = 0; j < p.Length; j++)
                    {
                        if (Hex_Val(cmp) == Hex_Val(p[j]))
                        {
                            if (j < 7) i += 4; else i += 3;
                            l++;
                        }
                    }
                }
                if (l > 30) return true; else return false;
            }
        }

        void Display_Data()
        {
            jt = new int[42];
            int jmp = 0;
            var buffer = new MemoryStream();
            var write = new BinaryWriter(buffer);
            var ds = 0;
            bool tr = false;
            bool se = false;
            string db_Text = "";
            int bin = 8;
            int hex = 16;
            Invoke(new Action(() =>
            {
                busy = true;
                if (VS_bin.Checked) Data_Box.Font = new Font("Lucida Console", 7.5f); else Data_Box.Font = new Font("Lucida Console", 10, FontStyle.Regular);
                Data_Box.Visible = false;
                Data_Box.Clear();
                DV_pbar.Value = 0;
                DV_pbar.Maximum = 100;
                DV_pbar.Maximum *= 100;
                DV_pbar.Value = DV_pbar.Maximum / 100;
                ds = Data_Sep.SelectedIndex;
            }));
            if (ds >= 1) tr = true;
            if (ds == 2) se = true;
            double trk = 1;
            bool ht = false;
            if (tracks > 42) ht = true;
            Stopwatch watch = new Stopwatch();
            watch.Start();
            for (int i = 0; i < tracks; i++)
            {
                Invoke(new Action(() => DV_pbar.Maximum = (int)((double)DV_pbar.Value / (double)(i + 1) * tracks)));
                if (NDS.cbm[i] > 0 && NDS.cbm[i] < 6)
                {
                    if (DV_gcr.Checked)
                    {
                        jmp++;
                        try
                        {
                            Invoke(new Action(() =>
                            {
                                jt[(int)trk] = db_Text.Length;
                                if (tr) db_Text += $"\n\nTrack ({trk})  Data Format: {secF[NDS.cbm[i]]} {NDG.Track_Data[i].Length} Bytes\n\n";
                                if (VS_dat.Checked) db_Text += $"{Encoding.ASCII.GetString(Fix_Stops(NDG.Track_Data[i]))}";
                                if (VS_hex.Checked)
                                {
                                    string temp = "";
                                    for (int j = 0; j < NDG.Track_Data[i].Length / hex; j++)
                                    {
                                        temp += Append_Hex(NDG.Track_Data[i], j * hex, hex);
                                    }
                                    var y = (NDG.Track_Data[i].Length / hex) * hex;
                                    if (y < NDG.Track_Data[i].Length)
                                    {
                                        temp += Append_Hex(NDG.Track_Data[i], y, NDG.Track_Data[i].Length - y, hex);
                                    }
                                    db_Text += temp;
                                }
                                if (VS_bin.Checked)
                                {
                                    string temp = "";
                                    for (int j = 0; j < NDG.Track_Data[i].Length / bin; j++)
                                    {
                                        temp += Append_Bin(NDG.Track_Data[i], j * bin, bin);
                                    }
                                    var y = (NDG.Track_Data[i].Length / bin) * bin;
                                    if (y < NDG.Track_Data[i].Length)
                                    {
                                        temp += Append_Bin(NDG.Track_Data[i], y, NDG.Track_Data[i].Length - y, bin);
                                    }
                                    db_Text += temp;
                                }
                            }));
                        }
                        catch { }
                    }
                    if (DV_dec.Checked)
                    {
                        if (NDS.cbm[i] == 1) Disp_CBM(i, trk);
                        if (NDS.cbm[i] == 5) Disp_VPL(i, trk);
                        jmp++;
                    }
                }
                if (ht) trk += .5; else trk += 1;
            }
            Invoke(new Action(() =>
            {
                T_jump.Maximum = jmp;
                if (ds >= 1 && jmp > 0) { T_jump.Visible = Jump.Visible = true; } else { T_jump.Visible = Jump.Visible = false; }
                Data_Box.Text = db_Text;
                Disp_Data.Text = "Refresh";
                DV_pbar.Value = 0;
                displayed = true;
                busy = false;
                watch.Stop();
                GC.Collect();
                //Text = watch.Elapsed.TotalMilliseconds.ToString();
                View_Jump();
                Data_Box.Visible = true;
            }));
            if (DV_dec.Checked) File.WriteAllBytes($@"c:\test\{fname}_Decoded.bin", buffer.ToArray());

            void Disp_CBM(int t, double track)
            {
                byte[][] temp = new byte[NDS.sectors[t]][]; // = new byte[0];
                bool[] nul = new bool[NDS.sectors[t]];
                if (DV_dec.Checked)
                {
                    jt[(int)trk] = db_Text.Length;
                    try
                    {
                        int total = 0;
                        byte[] tmp = new byte[NDG.Track_Data[t].Length];
                        Array.Copy(NDG.Track_Data[t], 0, tmp, 0, tmp.Length);
                        BitArray tdata = new BitArray(Flip_Endian(tmp));
                        for (int i = 0; i < NDS.sectors[t]; i++)
                        {
                            (temp[i], nul[i]) = Decode_CBM_GCR(NDG.Track_Data[t], i, true, tdata);
                            total += temp[i].Length;
                        }
                        if (tr) db_Text += $"\n\nTrack ({track})  Data Format: {secF[NDS.cbm[t]]} Length ({total}) bytes\n\n";
                        for (int i = 0; i < NDS.sectors[t]; i++)
                        {
                            string temp2 = "";
                            string ck = "";
                            if (nul[i]) ck = "Checksum OK"; else ck = "Checksum Failed!";
                            if (se) db_Text += $"\n\nSector ({i + 1}) Length {temp[i].Length} {ck}\n\n";
                            if (VS_dat.Checked) db_Text += Encoding.ASCII.GetString(Fix_Stops(temp[i]));
                            if (VS_hex.Checked)
                            {
                                for (int j = 0; j < temp[i].Length / hex; j++)
                                {
                                    temp2 += Append_Hex(temp[i], j * hex, hex);
                                }
                                var y = (temp[i].Length / hex) * hex;
                                if (y < temp[i].Length)
                                {
                                    temp2 += Append_Hex(temp[i], y, temp[i].Length - y, hex);
                                }
                                db_Text += temp2;
                            }
                            if (VS_bin.Checked)
                            {
                                for (int j = 0; j < temp[i].Length / bin; j++)
                                {
                                    temp2 += Append_Bin(temp[i], j * bin, bin);
                                }
                                var y = (temp[i].Length / bin) * bin;
                                if (y < temp[i].Length)
                                {
                                    temp2 += Append_Bin(temp[i], y, temp[i].Length - y, bin);
                                }
                                db_Text += temp2;
                            }
                            write.Write(temp[i]);
                        }
                    }
                    catch { }
                }
            }

            void Disp_VPL(int t, double track)
            {
                byte[][] temp = new byte[NDS.sectors[t]][]; // = new byte[0];
                jt[(int)trk] = db_Text.Length;
                if (DV_dec.Checked)
                {
                    byte[] tmp = new byte[NDG.Track_Data[t].Length];
                    Array.Copy(NDG.Track_Data[t], 0, tmp, 0, tmp.Length);
                    BitArray tdata = new BitArray(Flip_Endian(tmp));
                    int interleave = 3;
                    int current = 0;
                    int s = 0;
                    int total = 0;
                    for (int ii = 0; ii < NDS.sectors[t]; ii++)
                    {
                        temp[ii] = D_Vorpal(tdata, ii);
                        total += temp[ii].Length;
                    }
                    if (tr) db_Text += $"\n\nTrack ({track}) {secF[NDS.cbm[t]]} Sectors ({NDS.sectors[t]}) Length ({total}) bytes\n\n";
                    for (int ii = 0; ii < NDS.sectors[t]; ii++)
                    {
                        string temp2 = "";
                        if (se) db_Text += $"\n\nSector ({current}) Length ({temp[ii].Length}) bytes\n\n";
                        if (VS_dat.Checked) db_Text += Encoding.ASCII.GetString(Fix_Stops(temp[current])); //.Replace('?', '.');
                        if (VS_hex.Checked)
                        {
                            for (int j = 0; j < temp[current].Length / hex; j++)
                            {
                                temp2 += Append_Hex(temp[current], j * hex, hex);
                            }
                            var y = (temp[current].Length / hex) * hex;
                            if (y < temp[current].Length)
                            {
                                temp2 += Append_Hex(temp[current], y, temp[current].Length - y, hex);
                            }
                            db_Text += temp2;
                        }
                        if (VS_bin.Checked)
                        {
                            for (int j = 0; j < temp[current].Length / bin; j++)
                            {
                                temp2 += Append_Bin(temp[current], j * bin, bin);
                            }
                            var y = (temp[current].Length / bin) * bin;
                            if (y < temp[current].Length)
                            {
                                temp2 += Append_Bin(temp[current], y, temp[current].Length - y, bin);
                            }
                            db_Text += temp2;
                        }
                        write.Write(temp[current]);
                        current += interleave;
                        if (current > NDS.sectors[t] - 1)
                        {
                            s++;
                            current = 0 + s;
                        }
                    }
                }
            }

            string Append_Hex(byte[] data, int pos, int length, int expected_length = 0)
            {
                string spc = "";
                if (expected_length > 0) for (int j = 0; j < expected_length - length; j++) spc += "   ";
                string temp = "";
                temp += $"{Hex(data, pos, length)}    ".Replace('-', ' ');
                byte[] temp2 = new byte[length];
                Array.Copy(data, pos, temp2, 0, length); // Encoding.ASCII.GetString(NDG.Track_Data[i], j * 16, 16);
                temp += $"{spc}{Encoding.ASCII.GetString(Fix_Stops(temp2))}\n"; //.Replace('?', '.');
                return temp;
            }

            string Append_Bin(byte[] data, int pos, int length, int expected_length = 0)
            {
                byte[] temp2 = new byte[length];
                Array.Copy(data, pos, temp2, 0, length); // Encoding.ASCII.GetString(NDG.Track_Data[i], j * 16, 16);
                string spc = "";
                if (expected_length > 0) for (int j = 0; j < expected_length - length; j++) spc += "         ";
                string temp = "";
                temp += $"{Byte_to_Binary(temp2)}     "; //.Replace('-', ' ');
                temp += $"{spc}{Encoding.ASCII.GetString(Fix_Stops(temp2))}\n"; //.Replace('?','.');
                return temp;
            }

            byte[] Fix_Stops(byte[] data)
            {
                byte[] fix = new byte[data.Length];
                for (int i = 0; i < data.Length; i++)
                {
                    var g = Convert.ToInt32(data[i]);
                    if (invalid_char.Any(s => s == g) || g > 127) fix[i] = 0x2e; else fix[i] = data[i];
                }
                return fix;
            }
        }
    }
}