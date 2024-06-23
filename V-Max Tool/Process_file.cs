using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TrackBar;

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
        private bool loader_fixed = false;
        private byte[] nib_header = new byte[0];
        private byte[] g64_header = new byte[684];
        private readonly string[] supported = { ".nib", ".g64", ".nbz" }; // Supported file extensions list
        /// vsec = the CBM sector header values & against byte[] sz
        private readonly string[] valid_cbm = { "52-40-05-28", "52-40-05-2C", "52-40-05-48", "52-40-05-4C", "52-40-05-38", "52-40-05-3C", "52-40-05-58", "52-40-05-5C",
            "52-40-05-24", "52-40-05-64", "52-40-05-68", "52-40-05-6C", "52-40-05-34", "52-40-05-74", "52-40-05-78", "52-40-05-54", "52-40-05-A8",
            "52-40-05-AC", "52-40-05-C8", "52-40-05-CC", "52-40-05-B8" };
        /// vmax = the block header values of V-Max v2 sectors (non-CBM sectors)
        //private readonly string[] secF = { "NDOS", "CBM", "V-Max v2", "V-Max v3", "Loader", "Vorpal", "Unformatted" };
        private readonly string[] secF = { "NDOS", "CBM", "V-Max v2", "V-Max v3", "Loader", "Vorpal", "RapidLok", "RL-Key", "EA", "RA/MB", "Microprose", "Unformatted" };
        private readonly int[] invalid_char = { 0, 1, 2, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 95,
            128, 129, 130, 131, 132, 133, 134, 135, 136, 137, 138, 139 };
        private int[] jt = new int[42];

        void Batch_Get_File_List(string[] files)
        {
            string s = string.Empty;
            string t = string.Empty;
            var folder = Path.GetDirectoryName(files[0]);
            var SaveFolder = new FolderBrowserDialog()
            {
                Description = "Select a folder for output files.",
                SelectedPath = folder,

            };
            var ok = new DialogResult();
            Invoke(new Action(() => ok = SaveFolder.ShowDialog()));
            if (ok == DialogResult.OK)
            {
                string parent;// = "";
                string[] batch_list;
                if (Cores <= 3) Invoke(new Action(() => Batch_Box.Visible = true));
                (batch_list, parent) = Populate_File_List(files);

                if (batch_list?.Length != 0)
                {
                    string sel_path = SaveFolder.SelectedPath.ToString();
                    if (sel_path != "") Process_Batch(batch_list, sel_path, parent);
                }
                else
                {
                    Invoke(new Action(() =>
                    {
                        using (Message_Center centerr = new Message_Center(this)) // center message box
                        {
                            t = "Nothing to do!";
                            s = "No valid files to process";
                            MessageBox.Show(s, t, MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }));
                }
            }
        }

        void Process_Batch(string[] batch_list, string path, string basedir)
        {
            Stopwatch btime = new Stopwatch();
            btime.Start();
            bool temp = Auto_Adjust;
            Invoke(new Action(() =>
            {
                Reset_to_Defaults();
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
                Batch_List_Box.Items.Clear();
                Batch_List_Box.Visible = true;
                Disable_Core_Controls(true);
            }));
            Worker_Alt?.Abort();
            Worker_Alt = new Thread(new ThreadStart(() => Start_Work()));
            Worker_Alt?.Start();

            void Start_Work()
            {
                LB_File_List = new List<string>();
                for (int i = 0; i < batch_list.Length; i++)
                {
                    //testfile = Path.GetFileNameWithoutExtension(batch_list[i]); /// <-- for debugging
                    loader_fixed = false;
                    NDG.L_Rot = false;
                    if (!cancel)
                    {

                        if (System.IO.File.Exists(batch_list[i]))
                        {
                            Invoke(new Action(() =>
                            {
                                linkLabel1.Visible = false;
                                label8.Text = $"Processing file {i + 1} of {batch_list.Length}";
                                label9.Text = $"{Path.GetFileName(batch_list[i])}";
                                Batch_Bar.Maximum = (int)((double)Batch_Bar.Value / (double)(i + 1) * batch_list.Length);
                                if (Cores > 1) Import_File.Visible = false; else Import_File.Visible = true;
                            }));
                            string curfile = $@"{path}\{Path.GetDirectoryName(batch_list[i]).Replace(basedir, "")}\{Path.GetFileNameWithoutExtension(batch_list[i])}{fnappend}.g64";
                            fext = Path.GetExtension(batch_list[0]);
                            if (fext.ToLower() == supported[0]) Batch_NIB(batch_list[i], curfile);
                            Invoke(new Action(() =>
                            {
                                var status = "OK!";
                                if (error)
                                {
                                    if (File.Exists(curfile))
                                    {
                                        status = "Completed with errors";
                                    }
                                    else status = "Error, file not saved";
                                }
                                if (File.Exists(curfile))
                                {
                                    long sz = new FileInfo(curfile).Length / 1024;
                                    status = $"(OK!) {sz:N0}kb";
                                }
                                else status = "Error, file not saved";
                                error = false;
                                Batch_List_Box.Items.Add($@"{Path.GetDirectoryName(curfile).Replace(path, "")}\{Path.GetFileNameWithoutExtension(curfile).Replace($"{fnappend}", "")} ({status})");
                                Batch_List_Box.SelectedIndex = Batch_List_Box.Items.Count - 1;
                                Batch_List_Box.SelectedIndex = -1;
                                LB_File_List.Add(curfile);
                            }));
                        }
                    }
                    else break;
                }
                Invoke(new Action(() =>
                {
                    btime.Stop();
                    if (DB_timers.Checked) label2.Text = $"Total Batch-Process time {btime.Elapsed.TotalSeconds:F2} seconds";
                    using (Message_Center center = new Message_Center(this)) /// center message box
                    {
                        string t = "";
                        string s = "";
                        if (!cancel)
                        {
                            t = "Done!";
                            s = $"Batch processing completed..\n in {btime.Elapsed.TotalSeconds:F2} seconds";
                        }
                        else
                        {
                            t = "Canceled!";
                            s = "Batch processing canceled by user";
                        }
                        MessageBox.Show(s, t, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    Import_File.Visible = false;
                    Import_Progress_Bar.Value = 0;
                    Batch_Bar.Value = 0;
                    Batch_Box.Visible = false;
                    cancel = false;
                    busy = true;
                    Auto_Adjust = temp;
                    busy = false;
                    Set_Auto_Opts();
                    Reset_to_Defaults(false);
                    Disable_Core_Controls(false);
                }));
                batch = false;
            }

            void Batch_NIB(string fn, string output)
            {
                FileStream Stream = new FileStream(fn, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                long length = new System.IO.FileInfo(fn).Length;
                tracks = (int)(length - 256) / 8192;
                if ((tracks * 8192) + 256 == length)
                {
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
                        try
                        {
                            Stopwatch parse = Parse_Nib_Data();
                            if (!error)
                            {
                                Stopwatch proc = Process_Nib_Data(true, false, true);
                                if (debug) Invoke(new Action(() =>
                                {
                                    if (DB_timers.Checked) label2.Text = $"Parse time : {parse.Elapsed.TotalMilliseconds} ms, Process time : {proc.Elapsed.TotalMilliseconds} Total {parse.Elapsed.TotalMilliseconds + proc.Elapsed.TotalMilliseconds} ms";
                                }));
                                Make_G64(output, end_track, fat_trk);
                            }
                        }
                        catch (Exception e)
                        {
                            Invoke(new Action(() =>
                            {
                                Text = e.Message;
                            }));
                        }
                    }
                }
                GC.Collect();
            }
        }

        Stopwatch Parse_Nib_Data()
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            Invoke(new Action(() =>
            {
                Import_Progress_Bar.Value = 0;
                Import_Progress_Bar.Maximum = 100;
                Import_Progress_Bar.Maximum *= 100;
                Import_Progress_Bar.Value = Import_Progress_Bar.Maximum / 100;
                if (Cores < 8) Import_Progress_Bar.Visible = true;
            }));
            bool ldr = false;
            bool cyan = false;
            int cbm = 0; int vmx = 0; int vpl = 0; int rlk = 0; int mps = 0;
            double ht;
            bool halftracks = false;
            string[][] f = new string[tracks][];
            string tr = "Track";
            string le = "Length";
            string fm = "Format";
            string bl = "** Potentially bad loader! **";
            //bool fat = false;
            if (tracks > 42)
            {
                halftracks = true;
                ht = 0.5;
            }
            else ht = 0;
            /// ------------ Safe Threading Method, Starts as many threads as there are physical CPU cores available ----------------------
            /// ------------ CPU_Killer (true) Starts as many threads as there are jobs to do. (can overwhelm slower CPU's quickly!) ------
            Task = new Thread[tracks];
            for (int i = 0; i < tracks; i++)
            {
                int x = i;
                Task_Limit.WaitOne();
                Task[i] = new Thread(new ThreadStart(() => Analyze_Track(x)));
                Task[i].Start();
                Update_Progress_Bar(i);
                if (tracks > 42) i++;
            }
            foreach (var thread in Task) thread?.Join();
            Task = new Thread[0];
            int c_cyn = 8;
            int c_gcr = 62;
            int c_v1 = 78;
            if (tracks < 43) { c_cyn = 4; c_gcr = 31; c_v1 = 39; }
            if (NDS.cbm[c_cyn] == 1) cyan = Check_Cyan_Loader(NDS.Track_Data[c_cyn]);
            if (cyan && NDS.cbm[c_v1] != 1) NDS.Track_Data[c_gcr] = Cyan_t32_GCR_Fix(NDS.Track_Data[c_gcr], c_gcr);
            /// -- Checks for false positive of RapidLok Key track on non-RapidLok images
            if (NDS.cbm.Any(x => x == 7) && !NDS.cbm.Any(x => x == 6))
            {
                for (int i = 0; i < tracks; i++) if (NDS.cbm[i] == 7) NDS.cbm[i] = secF.Length - 1;
            }
            if (!batch)
            {
                bool fat = false;
                var color = Color.Black;
                Invoke(new Action(() =>
                {
                    if (NDS.cbm.Any(s => s == 4) || NDS.cbm.Any(s => s == 9)) f_load.Visible = true; else f_load.Visible = false;
                    Check_Adv_Opts();
                    if (ldr) Loader_Track.Text = "Loader Track : Yes"; else Loader_Track.Text = "Loader Track : No";
                    CBM_Tracks.Text = $"CBM tracks : {cbm}";
                    if (NDS.cbm.Any(x => x == 2) || NDS.cbm.Any(x => x == 3)) Protected_Tracks.Text = $"V-Max Tracks : {vmx}";
                    if (NDS.cbm.Any(x => x == 5)) Protected_Tracks.Text = $"Vorpal Tracks : {vpl}";
                    if (NDS.cbm.Any(x => x == 6)) Protected_Tracks.Text = $"RapidLok Tracks : {rlk}";
                    if (NDS.cbm.Any(x => x == 10)) Protected_Tracks.Text = $"MicroPros Tracks : {mps}";
                    Protected_Tracks.Visible = (vmx > 0 || vpl > 0 || rlk > 0 || mps > 0);
                    if (tracks > 42)
                    {
                        halftracks = true;
                        ht = 0.5;
                    }
                    else ht = 0;
                    bool cust_dens = false;
                    bool v2 = false;
                    bool v3 = false;
                    Track_Info.BeginUpdate();
                    int t;
                    for (int i = 0; i < tracks; i++)
                    {
                        int htk = 1;
                        if (tracks > 42) htk = 2;
                        string Fat = "";

                        if (tracks > 42) t = i / 2 + 1; else t = i + 1;
                        if (!NDS.cbm.Any(x => x == 10))
                        {
                            if (i > 2 && i + htk < NDS.cbm.Length && (NDS.cbm[i] == 1 && (NDS.cbm[i + htk] == 1 || NDS.cbm?[i - htk] == 1)))
                            {
                                if ((t != NDS.Track_ID[i] && t == NDS.Track_ID[i] + 1) || (i + htk < NDS.Track_ID.Length && t == NDS.Track_ID[i + htk])) Fat = " [Fat]";
                            }
                        }
                        if (halftracks) ht += .5; else ht += 1;
                        if (NDS.cbm[i] > 0 && NDS.cbm[i] < secF.Length - 1)
                        {
                            var d = Get_Density(NDS.Track_Length[i] >> 3);
                            if ((ht >= 0 && ht < 18 && d != 0) || (ht >= 18 && ht < 25 && d != 1) || (ht >= 25 && ht < 31 && d != 2) || (ht >= 31 && ht < 43 && d != 3)) cust_dens = true;
                            if (NDS.cbm[i] == 2) v2 = true;
                            if (NDS.cbm[i] == 3) v3 = true;
                        }
                        if (!batch && NDS.Track_Length[i] > 6000 && NDS.cbm[i] != secF.Length - 1 && NDS.cbm[i] > 0)
                        {
                            color = Color.Black;
                            var d = Get_Density(NDS.Track_Length[i] >> 3);
                            string e = "";
                            if ((ht >= 31 && d != 3) || (ht >= 25 && ht < 31 && d != 2) || (ht >= 18 && ht < 25 && d != 1) || (ht >= 0 && ht < 18 && d != 0)) e = " [!]";
                            if (NDS.cbm[i] == 0) color = Color.Black;
                            if (NDS.cbm[i] == 1) color = Color.Black;
                            if (NDS.cbm[i] == 2) color = Color.DarkMagenta;
                            if (NDS.cbm[i] == 3) color = Color.Green;
                            if (NDS.cbm[i] == 4) color = Color.Blue;
                            if (NDS.cbm[i] == 5) color = Color.DarkCyan;
                            if (NDS.cbm[i] == 6) color = Color.DarkOrange;
                            if (NDS.cbm[i] == 7 || NDS.cbm[i] == 8) color = Color.DarkGreen;
                            if (NDS.cbm[i] == 10) color = Color.Brown;
                            sf.Items.Add(new LineColor { Color = color, Text = $"{secF[NDS.cbm[i]]}{Fat}" });
                            sl.Items.Add((NDS.Track_Length[i] >> 3).ToString("N0"));
                            ss.Items.Add(NDS.sectors[i]);
                            strack.Items.Add(ht);
                            sd.Items.Add($"{3 - d}{e}");
                        }
                        if (NDS.cbm[i] == 1 || NDS.cbm[i] == 10)
                        {
                            htk = 1;
                            if (tracks > 42) htk = 2;
                            Fat = "";
                            if (!NDS.cbm.Any(x => x == 10))
                            {
                                if ((t != NDS.Track_ID[i] && t == NDS.Track_ID[i] + 1) || (i + htk < NDS.Track_ID.Length && t == NDS.Track_ID[i + htk]))
                                {
                                    Fat = " [ Fat-Track ]";
                                    fat = true;
                                }
                            }
                            Track_Info.Items.Add(new LineColor { Color = Color.Blue, Text = $"{tr} {t} {fm} : {secF[NDS.cbm[i]]}, Embeded Track ID # {NDS.Track_ID[i]} {Fat}" });
                            for (int j = 0; j < f[i].Length; j++)
                            {
                                if (j >= f[i].Length - 3) color = Color.Black; else color = Color.FromArgb(40, 40, 40);
                                if (f[i][j].ToLower().Contains("(0)*")) color = Color.FromArgb(255, 255, 255);
                                Track_Info.Items.Add(new LineColor { Color = color, Text = $"{f[i][j]}" });
                            }
                            Track_Info.Items.Add(" ");
                        }
                        if (NDS.cbm[i] == 2)
                        {
                            color = Color.Blue;
                            for (int j = 0; j < f[i].Length; j++)
                            {
                                if (j > 0) color = Color.FromArgb(110, 0, 110);
                                if (j >= f[i].Length - 3 || f[i][j].ToLower().Contains("gap")) color = Color.Black;
                                if (f[i][j].ToLower().Contains("(0)*")) color = Color.FromArgb(230, 0, 230);
                                Track_Info.Items.Add(new LineColor { Color = color, Text = f[i][j] });
                            }
                        }
                        if (NDS.cbm[i] == 3)
                        {
                            Track_Info.Items.Add(new LineColor { Color = Color.Blue, Text = $"{tr} {t} {fm} : {secF[NDS.cbm[i]]}" });
                            for (int j = 0; j < f[i].Length; j++)
                            {
                                if (j >= f[i].Length - 2) color = Color.Black; else color = Color.DarkGreen;
                                if (f[i][j].ToLower().Contains("(0)*")) color = Color.LightGreen;
                                Track_Info.Items.Add(new LineColor { Color = color, Text = $"{f[i][j]}" });
                            }
                            Track_Info.Items.Add(" ");
                        }
                        if (NDS.cbm[i] == 4)
                        {
                            Track_Info.Items.Add(new LineColor { Color = Color.DarkBlue, Text = $"{tr} {t} {fm} : {secF[NDS.cbm[i]]} {tr} {le} ({NDG.Track_Data[i].Length})" });
                            if (NDG.Track_Data[i].Length > 7400) Track_Info.Items.Add(bl);
                            Track_Info.Items.Add(" ");
                        }
                        if (NDS.cbm[i] == 5)
                        {
                            Track_Info.Items.Add(new LineColor { Color = Color.Blue, Text = $"{tr} {t} {fm} : {secF[NDS.cbm[i]]}" });
                            for (int j = 0; j < f[i].Length; j++)
                            {
                                Track_Info.Items.Add(new LineColor { Color = Color.DarkBlue, Text = f[i][j] });
                            }
                            Track_Info.Items.Add(new LineColor { Color = Color.Black, Text = $"Track Length : ({(NDS.D_End[i] - NDS.D_Start[i] >> 3)}) Sectors ({NDS.sectors[i]})" });
                            Track_Info.Items.Add(" ");
                        }
                        if (NDS.cbm[i] == 6)
                        {
                            Track_Info.Items.Add(new LineColor { Color = Color.Blue, Text = $"{tr} {t} {fm} : {secF[NDS.cbm[i]]} {tr} {le} ({NDG.Track_Data[i].Length})" });
                            for (int j = 0; j < f[i].Length; j++)
                            {
                                Track_Info.Items.Add(new LineColor { Color = Color.DarkMagenta, Text = f[i][j] });
                            }
                            Track_Info.Items.Add(new LineColor { Color = Color.Black, Text = $"Track Length : ({(NDS.D_End[i] - NDS.D_Start[i] >> 3)}) Sectors ({NDS.sectors[i]})" });
                            Track_Info.Items.Add(" ");
                        }
                        if (NDS.cbm[i] == 7)
                        {
                            Track_Info.Items.Add(new LineColor { Color = Color.DarkBlue, Text = $"{tr} {t} {fm} : {secF[NDS.cbm[i]]} {tr} {le} ({NDG.Track_Data[i].Length})" });
                        }
                        if (NDS.cbm[i] == 8)
                        {
                            Track_Info.Items.Add(new LineColor { Color = Color.DarkBlue, Text = $"{tr} {t} {fm} : PirateSlayer (EA) {tr} {le} ({NDG.Track_Data[i].Length})" });
                        }
                        if (NDS.cbm[i] == 9)
                        {
                            Track_Info.Items.Add(new LineColor { Color = Color.DarkBlue, Text = $"{tr} {t} {fm} : RainbowArts / MagicBytes {tr} {le} ({NDG.Track_Data[i].Length})" });
                        }
                        Track_Info.EndUpdate();
                    }
                    if (!cust_dens) Cust_Density.Text = "Track Densities : Standard"; else Cust_Density.Text = "Track Densities : Custom";
                    if (v2) VM_Ver.Text = "Protection : V-Max v2";
                    if (v3) VM_Ver.Text = "Protection : V-Max v3";
                    if (!v2 && !v3 && NDS.cbm.Any(x => x == 4)) VM_Ver.Text = "Protection : V-Max v2 CBM";
                    if (!v2 && !v3 && !NDS.cbm.Any(x => x == 4)) VM_Ver.Text = "No Protection or CBM exploit";
                    if (!v2 && !v3 && NDS.cbm.Any(x => x == 6)) VM_Ver.Text = "Protection : RapidLok";
                    if (NDS.cbm.Any(s => s == 5)) VM_Ver.Text = "Protection : Vorpal";
                    if (NDS.cbm.Any(x => x == 8)) VM_Ver.Text = "Protection: (EA) PirateSlayer";
                    if (NDS.cbm.Any(x => x == 9)) VM_Ver.Text = "Protection: Rainbow/Magic";
                    if (NDS.cbm.Any(x => x == 10)) VM_Ver.Text = "Protection: Microprose";
                    if (fat) VM_Ver.Text = "Protection: Fat-Tracks";
                    if (cyan) VM_Ver.Text = "Protection : Cyan Loader";
                }));
            }
            sw.Stop();
            return sw;

            void Get_Fmt(int trk)
            {
                NDS.cbm[trk] = Get_Data_Format(NDS.Track_Data[trk], trk);
            }

            void Update_Progress_Bar(int i)
            {
                Invoke(new Action(() =>
                {
                    if ((int)sw.Elapsed.TotalMilliseconds > 300) Import_File.Visible = true;
                    if (halftracks) ht = (i / 2) + 1; else ht = i + 1;
                    Import_Progress_Bar.Maximum = (int)((double)Import_Progress_Bar.Value / (double)(i + 1) * tracks);
                    if (tracks <= 42) label5.Text = $"Analyzing Disk : Track {(int)ht + 1}";
                    else if (i % 2 == 0) label5.Text = $"Analyzing Disk : Track {(int)ht + 1}";
                }));
            }

            void Get_Track_Info(int trk)
            {
                if (NDS.cbm[trk] == 1 || NDS.cbm[trk] == 10)
                {
                    bool cksm = !batch;
                    int[] junk;
                    if (NDS.cbm[trk] == 1) cbm++; else mps++;
                    (NDS.D_Start[trk],
                        NDS.D_End[trk],
                        NDS.Sector_Zero[trk],
                        NDS.Track_Length[trk],
                        f[trk],
                        NDS.sectors[trk],
                        NDS.cbm_sector[trk],
                        NDS.Total_Sync[trk],
                        NDS.Disk_ID[trk],
                        junk,
                        NDS.Track_ID[trk]) = CBM_Track_Info(NDS.Track_Data[trk], cksm, trk);
                    NDA.sectors[trk] = NDS.sectors[trk];
                    if (NDS.sectors[trk] == 1)
                    {
                        NDS.Track_Length[trk] = density[3] << 3;
                    }
                }
                if (NDS.cbm[trk] == 2)
                {
                    vmx++;
                    (
                        NDA.Track_Data[trk],
                        NDS.D_Start[trk],
                        NDS.D_End[trk],
                        NDS.Sector_Zero[trk],
                        NDS.Track_Length[trk],
                        f[trk],
                        NDS.sectors[trk],
                        NDS.Gap_Sector[trk],
                        NDS.v2info[trk]) = Get_V2_Track_Info(NDS.Track_Data[trk], trk);
                }
                if (NDS.cbm[trk] == 3)
                {
                    vmx++;
                    int len;
                    (f[trk],
                        NDS.D_Start[trk],
                        NDS.D_End[trk],
                        NDS.Sector_Zero[trk],
                        len, NDS.sectors[trk],
                        NDS.Header_Len[trk],
                        NDS.Gap_Sector[trk]) = Get_vmv3_track_length(NDS.Track_Data[trk], trk);
                    NDS.Track_Length[trk] = len * 8;
                    NDS.Sector_Zero[trk] *= 8;
                    NDA.sectors[trk] = NDS.sectors[trk];
                }
                if (NDS.cbm[trk] == 4)
                {
                    ldr = true;
                    int q = 0;
                    if (fext.ToLower() == ".g64") q = NDG.s_len[trk];
                    else (q, NDS.Track_Data[trk]) = (Get_Loader_Len(NDS.Track_Data[trk], 0, 80, 7000));
                    NDS.Track_Length[trk] = q * 8;
                    NDG.Track_Data[trk] = new byte[NDS.Track_Length[trk] / 8];
                    Buffer.BlockCopy(NDS.Track_Data[trk], 0, NDG.Track_Data[trk], 0, NDG.Track_Data[trk].Length);
                    NDG.Track_Length[trk] = NDG.Track_Data[trk].Length;
                    NDA.Track_Length[trk] = NDG.Track_Data[trk].Length * 8;
                    NDA.Track_Data[trk] = NDS.Track_Data[trk];
                }
                /// --------------------------------------------------------------------------------------------------------------------------------------------------------------- 

                if (NDS.cbm[trk] == 5)
                {
                    vpl++;
                    (NDG.Track_Data[trk],
                        NDS.D_Start[trk],
                        NDS.D_End[trk],
                        NDS.Track_Length[trk],
                        NDS.Header_Len[trk],
                        NDS.sectors[trk],
                        NDS.cbm_sector[trk],
                        f[trk]) = Get_Vorpal_Track_Length(NDS.Track_Data[trk], trk);
                    if (NDG.Track_Data[trk] != null)
                    {
                        if (NDS.cbm[trk] == 5)
                        {
                            if (Original.OT[trk].Length == 0)
                            {
                                Original.OT[trk] = new byte[NDG.Track_Data[trk].Length];
                                Buffer.BlockCopy(NDG.Track_Data[trk], 0, Original.OT[trk], 0, NDG.Track_Data[trk].Length);
                            }
                        }
                    }
                }
                if (NDS.cbm[trk] == 6)
                {
                    int tk = trk;
                    if (tracks > 42) tk = (trk / 2) + 1; else tk += 1;
                    rlk++;
                    int q = 0;
                    byte[] temp = new byte[q];
                    (temp,
                        NDS.D_Start[trk],
                        NDS.D_End[trk], q,
                        NDS.sectors[trk],
                        f[trk]) = RapidLok_Track_Info(NDS.Track_Data[trk]);
                    NDS.Track_Length[trk] = q;
                    Set_Dest_Arrays(temp, trk);
                }
                if (NDS.cbm[trk] == 7)
                {
                    rlk++;
                    byte[] newkey = RapidLok_Key_Fix(NDS.Track_Data[trk]);
                    NDS.Track_Length[trk] = newkey.Length << 3;
                    Set_Dest_Arrays(newkey, trk);
                }
                if (NDS.cbm[trk] == 8)
                {
                    byte[] EA = Pirate_Slayer(NDS.Track_Data[trk]);
                    NDS.Track_Length[trk] = EA.Length << 3;
                    Set_Dest_Arrays(EA, trk);
                }
                if (NDS.cbm[trk] == 9)
                {
                    byte[] EA = RainbowArts(NDS.Track_Data[trk], f_load.Checked);
                    NDS.Track_Length[trk] = EA.Length << 3;
                    Set_Dest_Arrays(EA, trk);
                }
            }

            void Analyze_Track(int track)
            {
                Get_Fmt(track);
                Get_Track_Info(track);
                Task_Limit.Release();
            }
        }
        /// ---------------------------------------------------------------------------------------------------------------------------------------------------

        Stopwatch Process_Nib_Data(bool cbm, bool short_sector, bool rb_vm, bool wait = false, bool new_disk = false)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            bool halftracks = false;
            bool v2a = false;
            bool v3a = false;
            bool vpa = false;
            bool fl = false;
            bool sl = false;
            bool cbmadj = false;
            bool v3adj = false;
            bool v2adj = false;
            bool vpadj = false;
            bool v2cust = false;
            bool v3cust = false;
            int vpl_lead = 0;
            bool cyan = false;
            Invoke(new Action(() =>
            {
                busy = true;
                end_track = tracks;
                fat_trk = -1;

                int c_cyn = 8;
                int c_gcr = 62;
                int c_v1 = 78;
                
                if (tracks < 43) { c_cyn = 4; c_gcr = 31; c_v1 = 39; }
                if (NDS.cbm[c_cyn] == 1 && NDS.cbm[c_v1] == 1) cyan = Check_Cyan_Loader(NDS.Track_Data[c_cyn]);
                //if (cyan && NDS.cbm[c_v1] != 1) NDS.Track_Data[c_gcr] = Cyan_t32_GCR_Fix(NDS.Track_Data[c_gcr], c_gcr);

                (v2a, v3a, vpa) = Check_Tabs();
                if (NDS.cbm.Any(x => x == 2))
                {
                    if (tracks > 42) end_track = 75; else end_track = 38;
                    for (int i = 0; i < tracks; i++)
                    {
                        if (NDS.cbm[i] == 2)
                        {
                            if (!V2_swap_headers.Checked && !batch)
                            {
                                V2_swap.DataSource = new string[] { "64-4E (newer)", "64-46 (weak bits)", "4E-64 (alt)" };
                                if (Hex_Val(NDS.v2info[i], 0, 2) == "64-4E") { Invoke(new Action(() => V2_swap.SelectedIndex = 0)); break; }
                                if (Hex_Val(NDS.v2info[i], 0, 2) == "64-46") { Invoke(new Action(() => V2_swap.SelectedIndex = 1)); break; }
                                if (Hex_Val(NDS.v2info[i], 0, 2) == "4E-64") { Invoke(new Action(() => V2_swap.SelectedIndex = 2)); break; }
                            }
                            else
                            {
                                if (V2_swap.SelectedIndex == 0) { NDG.newheader[0] = 0x64; NDG.newheader[1] = 0x4e; }
                                if (V2_swap.SelectedIndex == 1) { NDG.newheader[0] = 0x64; NDG.newheader[1] = 0x46; }
                                if (V2_swap.SelectedIndex == 2) { NDG.newheader[0] = 0x4e; NDG.newheader[1] = 0x64; }
                                loader_fixed = false;
                                NDG.L_Rot = false;
                                break;
                            }
                        }
                    }
                    if (V3_Auto_Adj.Checked || V3_Custom.Checked)
                    {
                        if (V3_Auto_Adj.Checked) v3aa = true; else v3aa = false;
                        if (V3_Custom.Checked) v3cc = true; else v3cc = false;
                        V3_Auto_Adj.Checked = V3_Custom.Checked = false;
                    }
                    V3_Auto_Adj.Checked = V3_Custom.Checked = false;
                    if (v2aa) V2_Auto_Adj.Checked = true;
                    if (v2cc) V2_Custom.Checked = true;
                    if (batch || V2_Auto_Adj.Checked) v2adj = true;
                    if (V2_Custom.Checked) v2cust = true;
                    if (v2adj || rb_vm) { fnappend = mod; cbmadj = true; }
                    else { fnappend = fix; cbmadj = Adj_cbm.Checked; }
                }
                if (NDS.cbm.Any(x => x == 3))
                {
                    if (tracks > 42) end_track = 75; else end_track = 38;
                    if (V2_Auto_Adj.Checked || V2_Custom.Checked)
                    {
                        if (V2_Auto_Adj.Checked) v2aa = true; else v2aa = false;
                        if (V2_Custom.Checked) v2cc = true; else v2cc = false;
                        V2_Auto_Adj.Checked = V2_Custom.Checked = false;
                    }
                    V2_Auto_Adj.Checked = V2_Custom.Checked = V2_Add_Sync.Checked = false;
                    if (v3aa) V3_Auto_Adj.Checked = true;
                    if (v3cc) V3_Custom.Checked = true;
                    if (batch || V3_Auto_Adj.Checked) v3adj = true;
                    if (V3_Custom.Checked) v3cust = true;
                    if (v3adj) { fnappend = mod; cbmadj = true; }
                    else { fnappend = fix; cbmadj = Adj_cbm.Checked; }
                }
                if (NDS.cbm.Any(x => x == 4))
                {
                    if ((f_load.Checked || batch || V2_swap_headers.Checked) && !loader_fixed) fl = true; else fl = false;
                    if (NDS.cbm.Any(x => x == 2) || NDS.cbm.Any(x => x == 3)) sl = true;
                }
                if (NDS.cbm.Any(ss => ss == 5))
                {
                    if (tracks > 42) end_track = 69; else end_track = 35;
                    if (VPL_auto_adj.Checked || batch || VPL_rb.Checked) vpadj = true;
                    if (VPL_rb.Checked || Adj_cbm.Checked || vpadj) fnappend = mod; else fnappend = vorp;
                    vpl_lead = Lead_ptn.SelectedIndex;
                }
                //if (NDS.cbm.Any(ss => ss == 9))
                //{ 
                //    f_load.Text = "Use larger Key Track"; 
                //}
                else f_load.Text = "Fix Loader";
                if (NDS.cbm.Any(ss => ss == 10))
                {
                    if (tracks > 42) end_track = 69; else end_track = 35;
                }
                //if (NDS.cbm.Any(x => x == 9)) if (tracks > 42) end_track = 73; else end_track = 36;
                if (Adj_cbm.Checked || v2a || v3a || vpa || batch)
                {
                    if (!DB_force.Checked) cbmadj = Check_tlen(); else cbmadj = true;
                }
                if (NDS.cbm.Any(ss => ss == 9))
                {
                    f_load.Text = "Use larger Key Track";
                    cbmadj = false;
                    if (batch) f_load.Checked = false;
                }
                if (new_disk) fnappend = string.Empty;
                busy = false;
            }));
            var ldt = 255;
            /// ------------ Safe Threading Method, Starts as many threads as there are physical threads available ------------------------

            Task = new Thread[tracks];
            for (int i = 0; i < tracks; i++)
            {

                int x = i;
                var y = vpl_lead;
                if (NDS.cbm[i] != 4)
                {
                    Task_Limit.WaitOne();
                    Task[i] = new Thread(new ThreadStart(() => Process(x, y, true)));
                    Task[i].Start();
                }
                else ldt = i;
                if (tracks > 42) i++;
            }
            foreach (var thread in Task) thread?.Join();
            if (ldt < tracks) Process(ldt, 0, false); /// (false) tells Process not to release the thread because it isn't in a Semaphore or a thread

            if (!batch)
            {
                double ht;
                if (tracks > 42)
                {
                    halftracks = true;
                    ht = 0.5;
                }
                else ht = 0;
                //Invoke(new Action(() => Text = $"{NDS.cbm[34]} {NDS.cbm[35]} {NDS.cbm[36]}"));
                Color color = new Color();
                for (int i = 0; i < tracks; i++)
                {
                    //Invoke(new Action(() => Text = $"{i}"));
                    if (halftracks) ht += .5; else ht += 1;
                    if (!batch && NDA.Track_Length[i] > 0 && NDS.cbm[i] < secF.Length - 1)
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
                        if (VPL_auto_adj.Checked && (tracks >= 43 && i < 33 || tracks <= 42 && i < 18) && NDS.cbm[i] == 5) r = 296.6;
                        if (VPL_auto_adj.Checked && (tracks >= 43 && i > 33 || tracks <= 42 && i > 17) && NDS.cbm[i] == 5) r = 299.0;
                        if (r == 300 && r < 301) color = Color.FromArgb(0, 30, 255);
                        if ((r >= 301 && r < 302) || (r < 300 && r >= 299)) color = Color.DarkGreen;
                        if (r > 302 || (r < 299 && r >= 297)) color = Color.Purple;
                        if (r < 297) color = Color.Brown;
                        out_rpm.Items.Add(new LineColor { Color = color, Text = $"{r:0.0}" });
                    }
                }
                if (!busy && Adv_ctrl.SelectedTab == Adv_ctrl.TabPages["tabPage2"] && !manualRender) Check_Before_Draw(false, wait);
                if (Adv_ctrl.Controls[2] != Adv_ctrl.SelectedTab) displayed = false;
                if (Adv_ctrl.Controls[0] != Adv_ctrl.SelectedTab) drawn = false;
                if (!busy) Data_Viewer();
                //Invoke(new Action(() => Text = $"fat {fat_trk} last {end_track}"));
            }
            sw.Stop();
            return sw;

            void Process_Track(int trk, bool acbm, bool av2, bool cv2, bool av3, bool cv3, bool avp, int vplead, bool fix, bool sol, bool rvb, bool cmb, bool s_sec, bool cyn) //, bool swp)
            {
                if (NDS.Track_Length[trk] > 0 && NDS.cbm[trk] > 0 && NDS.cbm[trk] < secF.Length)
                {
                    if (NDS.cbm[trk] == 1) Process_CBM(trk, acbm, cmb, cyn);
                    if (NDS.cbm[trk] == 2) Process_VMAX_V2(trk, av2, cv2, rvb);
                    if (NDS.cbm[trk] == 3) Process_VMAX_V3(trk, av3, cv3, rvb, s_sec);
                    if (NDS.cbm[trk] == 4) Process_Loader(trk, fix, sol);
                    if (NDS.cbm[trk] == 5) Process_Vorpal(trk, avp, vpl_lead);
                    if (NDS.cbm[trk] == 6) Process_RapidLok(trk);
                    if (NDS.cbm[trk] == 9) Process_Rainbow(trk);
                    if (NDS.cbm[trk] == 10) Process_MPS(trk, acbm);
                }
                else { NDA.Track_Data[trk] = NDS.Track_Data[trk]; }
            }

            (bool, bool, bool) Check_Tabs()
            {
                bool a = (V2_Auto_Adj.Checked && Tabs.TabPages.Contains(Adv_V2_Opts));
                bool b = (V3_Auto_Adj.Checked && Tabs.TabPages.Contains(Adv_V3_Opts));
                bool c = (VPL_auto_adj.Checked && Tabs.TabPages.Contains(Vpl_adv));
                return (a, b, c);
            }

            void Process(int track, int vorpal_lead = 0, bool release = true)
            {
                Process_Track(track, cbmadj, v2adj, v2cust, v3adj, v3cust, vpadj, vorpal_lead, fl, sl, rb_vm, cbm, short_sector, cyan);
                if (release) Task_Limit.Release();
            }

            void Process_MPS(int trk, bool acbm)
            {
                int d = Get_Density(NDS.Track_Length[trk] >> 3);
                byte[] temp = new byte[NDS.Track_Length[trk] >> 3];
                Buffer.BlockCopy(NDS.Track_Data[trk], NDS.D_Start[trk] >> 3, temp, 0, ((NDS.D_End[trk] >> 3) - (NDS.D_Start[trk] >> 3)));
                if (temp != null)
                {
                    if (Original.OT[trk]?.Length == 0)
                    {
                        Original.OT[trk] = new byte[temp.Length];
                        Buffer.BlockCopy(temp, 0, Original.OT[trk], 0, temp.Length);
                    }
                }
                BitArray source = new BitArray(Flip_Endian(temp));
                int pos = 0;
                bool sec = false;
                (sec, pos) = Find_Sector(source, 0);
                if (pos > 5)
                {
                    pos -= 1;
                    int fs = 0;
                    while (pos > 0 && fs < 60)
                    {
                        if (temp[pos] != 0xff) break;
                        pos--;
                        fs++;
                    }
                    temp = Rotate_Left(temp, pos + 1);
                }
                else
                {
                    int fs = 0;
                    pos = temp.Length - 1;
                    while (pos > 0 && fs < 60)
                    {
                        if (temp[pos] != 0xff) break;
                        pos--;
                        fs++;
                    }
                    temp = Rotate_Right(temp, fs + 1);
                }
                if (acbm && temp.Length > density[d]) temp = Shrink_Track(temp, d);
                Set_Dest_Arrays(temp, trk);
            }

            void Process_CBM(int trk, bool acbm, bool bmc, bool cyn_ldr)
            {
                bool ad = true;
                if (NDS.cbm.Any(x => x == 9)) ad = false;
                var track = trk;
                int htk = 1;
                if (tracks > 42) htk = 2;
                if (tracks > 42) { track = (trk / 2) + 1; htk = 2; } else track += 1;
                if ((track != NDS.Track_ID[trk] && track == NDS.Track_ID[trk] + 1) || (trk + htk < NDS.Track_ID.Length && track == NDS.Track_ID[trk + htk]))
                {
                    NDG.Fat_Track[trk] = true;
                    if (fat_trk < 0) fat_trk = track;
                    if (track != NDS.Track_ID[trk] && track >= 34) end_track = trk + htk;
                }
                byte[] temp = new byte[0];
                /// --- Handles a Protection found on Jordan vs Bird (EA) --------
                if (track > 33 && NDS.sectors[trk] == 1)
                {
                    temp = JvB(NDS.Track_Data[trk]);
                    Set_Dest_Arrays(temp, trk);
                }
                /// --------------------------------------------------------------
                else
                {
                    int exp_snc = 40;   /// expected sync length.  (sync will be adjusted to this value if it is >= minimum value (or) =< ignore value
                    int min_snc = 12;   /// minimum sync length to signal this is a sync marker that needs adjusting
                    int ign_snc = 80;   /// ignore sync if it is >= to value
                    var d = 0;
                    bool dont = false;
                    if (bmc || acbm)
                    {
                        try
                        {
                            //if (acbm && track < 18 && NDS.sectors[trk] != 21) exp_snc = 14;
                            if (acbm && ((track < 18 || track > 18) && NDS.sectors[trk] != Available_Sectors[track - 1])) { exp_snc = 14; dont = true; }
                            temp = Adjust_Sync_CBM(NDS.Track_Data[trk], exp_snc, min_snc, ign_snc, NDS.D_Start[trk], NDS.D_End[trk], NDS.Sector_Zero[trk], NDS.Track_Length[trk], trk, ad);
                            if (temp != null)
                            {
                                if (Original.OT[trk]?.Length == 0)
                                {
                                    Original.OT[trk] = new byte[temp.Length];
                                    Buffer.BlockCopy(temp, 0, Original.OT[trk], 0, temp.Length);
                                }
                            }
                            if (acbm)
                            {
                                //if (track == 18 && NDS.sectors[trk] != 19)
                                if ((track == 18 && NDS.cbm.Any(x => x == 5)) || (track == 40 && cyn_ldr))
                                {
                                    if (temp.Length < density[1]) temp = Lengthen_Track(temp);
                                    if (temp.Length > density[1]) temp = Shrink_Track(temp, 1);
                                }
                                else
                                {
                                    if (DB_force.Checked || !(NDS.cbm.Any(x => x == 4) && !(NDS.cbm.Any(x => x == 3) || NDS.cbm.Any(x => x == 2))))
                                    {
                                        if (!(track < 18 && NDS.sectors[trk] != 21) && !dont)
                                        {
                                            d = Get_Density(NDS.Track_Length[trk] >> 3);
                                            temp = Rebuild_CBM(NDS.Track_Data[trk], NDS.sectors[trk], NDS.Disk_ID[trk], d, NDS.Track_ID[trk]);
                                        }
                                    }
                                }
                            }
                            Set_Dest_Arrays(temp, trk);
                        }
                        catch
                        {
                            if (!error)
                            {
                                Invoke(new Action(() =>
                                {
                                    using (Message_Center center = new Message_Center(this)) /// center message box
                                    {
                                        string t = "This image is not compatible with this program!";
                                        string m = "Image data may be corrupt or unsupported format";
                                        MessageBox.Show(m, t, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                        error = true;
                                    }
                                }));
                            }
                        }
                    }
                }
            }

            void Process_VMAX_V2(int trk, bool av2a, bool cv2c, bool rbv)
            {
                if (rbv || cv2c)
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
                if (av2a && NDS.sectors[trk] > 12)
                {
                    if (Original.OT[trk].Length == 0)
                    {
                        Original.OT[trk] = new byte[NDG.Track_Data[trk].Length];
                        Buffer.BlockCopy(NDG.Track_Data[trk], 0, Original.OT[trk], 0, NDG.Track_Data[trk].Length);
                    }
                    byte[] tdata;
                    (tdata, NDA.D_Start[trk], NDA.D_End[trk], NDA.Sector_Zero[trk]) = Rebuild_V2(NDG.Track_Data[trk], NDS.sectors[trk], NDS.v2info[trk], trk, NDG.newheader);
                    Set_Dest_Arrays(tdata, trk);
                }
            }

            void Process_VMAX_V3(int trk, bool av3a, bool cv3c, bool rbv, bool short_sec)
            {
                if (rbv || cv3c)
                {
                    if (!(short_sec && NDS.sectors[trk] < 16))
                    {
                        (NDG.Track_Data[trk], NDA.Track_Length[trk], NDA.Sector_Zero[trk]) =
                            Adjust_Vmax_V3_Sync(NDS.Track_Data[trk], NDS.D_Start[trk], NDS.D_End[trk], NDS.Sector_Zero[trk]);
                    }
                    else Shrink_Short_Sector(trk);
                }
                NDG.Track_Length[trk] = NDG.Track_Data[trk].Length;
                if (av3a)
                {
                    if (Original.OT[trk].Length == 0)
                    {
                        Original.OT[trk] = new byte[NDG.Track_Data[trk].Length];
                        Buffer.BlockCopy(NDG.Track_Data[trk], 0, Original.OT[trk], 0, NDG.Track_Data[trk].Length);
                    }
                    byte[] temp = Rebuild_V3(NDG.Track_Data[trk], NDS.Gap_Sector[trk], trk);
                    Set_Dest_Arrays(temp, trk);
                }
                if (NDG.Track_Data[trk].Length > 0)
                {
                    try
                    {
                        NDA.Track_Data[trk] = new byte[8192];
                        Buffer.BlockCopy(NDG.Track_Data[trk], 0, NDA.Track_Data[trk], 0, NDG.Track_Data[trk].Length);
                        Buffer.BlockCopy(NDG.Track_Data[trk], 0, NDA.Track_Data[trk], NDG.Track_Data[trk].Length, 8192 - NDG.Track_Data[trk].Length);
                        Original.OT[trk] = new byte[NDG.Track_Data[trk].Length];
                        Buffer.BlockCopy(NDG.Track_Data[trk], 0, Original.OT[trk], 0, NDG.Track_Data[trk].Length);
                        NDA.sectors[trk] = NDS.sectors[trk];
                    }
                    catch { }
                }
            }

            void Process_Loader(int trk, bool fixl, bool soll)
            {
                if (Original.SG.Length == 0)
                {
                    Original.SG = new byte[NDG.Track_Data[trk].Length];
                    Original.SA = new byte[NDA.Track_Data[trk].Length];
                    Buffer.BlockCopy(NDG.Track_Data[trk], 0, Original.SG, 0, NDG.Track_Data[trk].Length);
                    Buffer.BlockCopy(NDA.Track_Data[trk], 0, Original.SA, 0, NDA.Track_Data[trk].Length);
                }
                if (fixl && !loader_fixed) Fix_Loader_Option(false, trk);
                var d = Get_Density(NDG.Track_Data[trk].Length);
                if (soll)
                {
                    if (NDG.Track_Length[trk] > density[d] + 50) Shrink_Loader(trk);
                    if (NDG.Track_Length[trk] < density[d]) NDG.Track_Data[trk] = Lengthen_Loader(NDG.Track_Data[trk], d);
                }
                Set_Dest_Arrays(NDG.Track_Data[trk], trk);
            }

            void Process_Vorpal(int trk, bool avp, int lead)
            {
                byte[] temp = new byte[NDG.Track_Data[trk].Length];
                Buffer.BlockCopy(NDG.Track_Data[trk], 0, temp, 0, NDG.Track_Data[trk].Length);
                if (avp) temp = Rebuild_Vorpal(temp, trk, lead);
                Set_Dest_Arrays(temp, trk);
            }

            void Process_RapidLok(int trk)
            {
                byte[] temp = new byte[0];
                int sync = 0;
                for (int i = 0; i < NDG.Track_Data[trk].Length; i++)
                {
                    if (NDG.Track_Data[trk][i] == 0xff) sync++;
                    else
                    {
                        try
                        {
                            if (sync > 12 && (NDG.Track_Data[trk][i] == 0x55 && NDG.Track_Data[trk][i + 1] == 0x7b))
                            {
                                temp = Rotate_Left(NDG.Track_Data[trk], i - (sync - 1) - 8);
                                Set_Dest_Arrays(temp, trk);
                                break;
                            }
                        }
                        catch { }
                        sync = 0;
                    }
                }
            }

            void Process_Rainbow(int trk)
            {
                byte[] temp = RainbowArts(NDS.Track_Data[trk], f_load.Checked);
                Set_Dest_Arrays(temp, trk);
            }

            bool Check_tlen()
            {
                List<int> tl = new List<int>();
                for (int i = 0; i < tracks; i++) if (NDS.cbm[i] == 1) tl.Add(NDS.Track_Length[i]);
                if (tl.Count > 0) if (tl.Max() >> 3 < 8000) return true;
                return false;
            }
        }

        int Get_Data_Format(byte[] data, int track)
        {
            bool blnk = false;
            int t = 0;
            int csec = 0;
            int trk = track;
            if (tracks > 42) trk = (track / 2) + 1; else trk += 1;
            if (trk > 35)
            {
                (t, blnk) = Check_Blank(data);
                if (!blnk && t == 9) return t;
                blnk = false;
                t = 0;
            }
            byte[] comp = new byte[4];
            if (Check_RapidLok(data)) return 6;
            if ((tracks <= 42 && track == 19) || tracks > 42 && track == 38) if (Check_Loader(data)) return 4;
            BitArray source = new BitArray(Flip_Endian(data));
            for (int i = 0; i < (data.Length) - comp.Length; i++)
            {
                Buffer.BlockCopy(data, i, comp, 0, comp.Length);
                t = Compare(comp, i);
                if (t == 3 && i + 20 < data.Length)
                {
                    for (int j = 0; j < 20; j++) if (data[i + j] == 0xee) return 3;
                    t = 0;
                }
                if (t != 0) break;
            }
            if (t == 1 || t == 0)
            {
                if (blnk) return t;
                byte[] temp = new byte[0];
                bool c;
                int mp = 0;
                int y = 0;
                int p = 0;
                int ps = 0;
                int seek = 32;
                for (int i = 0; i < 20; i++)
                {
                    (c, ps) = Find_Sector(source, i + 1);
                    /// ------------ Detect Microprose track --------------------------
                    if (c && (ps >> 3) + seek < data.Length)
                    {
                        int snc = 0;
                        int w = 0;
                        int pp = (ps);
                        while (data[pp + w] == 0xff) w++;
                        if (w < seek)
                        {
                            while (w < seek && pp + w < data.Length)
                            {
                                if ((data[pp + w] == 0xff)) snc++;
                                w++;
                            }
                            if (snc == 0) mp++;
                        }
                    }
                    /// --------------------------------------------------------------
                    if (c) { y++; p = ps + 320; if (p > data.Length) p = 0; }
                    if (y > 4 && mp < 1) return 1;  /// <-- This is a CBM formatted track
                    if (y > 4 && mp > 4) return 10; /// <-- This is a Microprose custom format track

                }
                byte[] ncomp = new byte[vpl_s0.Length];
                int pos = 0;
                BitArray scomp = new BitArray(vpl_s0.Length * 8);
                while (pos < source.Length - vpl_s0.Length * 8)
                {
                    for (int j = 0; j < scomp.Count; j++)
                    {
                        scomp[j] = source[pos + j];
                    }
                    scomp.CopyTo(ncomp, 0);
                    ncomp = Flip_Endian(ncomp);
                    if (Match(vpl_s0, ncomp) || Match(vpl_s1, ncomp))
                    {
                        if (Check_Vorpal_Sectors(source, pos)) return 5;
                    }
                    pos++;
                }
            }
            if (t == 0) (t, blnk) = Check_Blank(data);
            return t;

            int Compare(byte[] d, int p)
            {
                if (Match(d, vv2n) || Match(d, vv2p)) return 2;
                if (Match(d, v3a)) return 3;
                if (Match(d, RLok))
                {
                    bool c;
                    int y = 0;
                    p = 0;
                    int ps = 0;
                    for (int i = 0; i < 20; i++)
                    {
                        (c, ps) = Find_Sector(source, i + 1);
                        if (c) { y++; p = ps + 320; if (p > data.Length) p = 0; }
                        if (y > 4) return 1;
                    }
                    return 6;
                }
                if (d[0] == sz[0])
                {
                    //byte[] tmp = new byte[5];
                    //Buffer.BlockCopy(d, 0, tmp, 0, 4);
                    //tmp = Decode_CBM_GCR(tmp);
                    //if (tmp[2] < 22) { csec++; if (csec > 1) return 1; }
                    d[1] &= sz[1]; d[2] &= sz[2]; d[3] &= sz[3];
                    if (valid_cbm.Contains(Hex_Val(d))) { csec++; if (csec > 1) return 1; } /// change csec > 6 if there are issues
                }
                return 0;
            }

            (int, bool) Check_Blank(byte[] d)
            {
                int b = 0;
                int snc = 0;
                for (int i = 0; i < d.Length; i++)
                {
                    if (blank.Any(x => x == d[i])) b++;
                    if (d[i] == 0xff) snc++;
                    if (ps1.Any(x => x == d[i]) || ps2.Any(x => x == d[i]))
                    {
                        try
                        {
                            if (ps1[0] == d[i] || ps2[0] == d[i])
                            {
                                byte[] s1 = new byte[ps1.Length];
                                byte[] s2 = new byte[ps2.Length];
                                Buffer.BlockCopy(d, i, s1, 0, s1.Length);
                                Buffer.BlockCopy(d, i, s2, 0, s2.Length);
                                if (Match(ps1, s1) || Match(ps2, s2)) return (8, false);
                            }
                        }
                        catch { }
                    }
                    if (d[i] == ramb[0] || (i > 0 && (d[i] == 0xff && d[i - 1] != 0xff)))
                    {
                        if (d[i] == 0xff)
                        {
                            int ps = i;
                            int sc = 0;
                            while (ps < i + 150 && ps < d.Length)
                            {
                                if (d[ps] != 0xff) break;
                                ps++; sc++;
                                if (sc > 140) break;
                            }
                            //Invoke(new Action(() => Text = $"{track} {sc}"));
                            if (Enumerable.Range(108, 132).Contains(sc))  // return (9, false);
                            {
                                ps = 0;
                                sc = 0;
                                while (ps < data.Length)
                                {
                                    if (data[ps] == 0x55) sc++;
                                    if (sc > 4000) return (9, false);
                                    ps++;
                                }
                            }
                        }
                        else
                            try
                            {
                                byte[] cmp = new byte[ramb.Length];
                                Buffer.BlockCopy(d, i, cmp, 0, cmp.Length);
                                if (Match(ramb, cmp))
                                {
                                    int ptn = 0;
                                    int itt = 0;
                                    while (i + (itt * cmp.Length) < d.Length)
                                    {
                                        Buffer.BlockCopy(d, i + (ptn * cmp.Length), cmp, 0, cmp.Length);
                                        if (Match(cmp, ramb)) ptn++;
                                        if (ptn > 100) return (9, false);
                                        itt++;
                                    }
                                }
                                if (Match(ramb, cmp)) return (9, false);
                            }
                            catch { }
                    }
                    //if (b > 1000 && snc < 10) return (0, false);
                    //if (snc > 1000 && trk == 36) return (7, false);
                }
                if (b > 1000 && snc < 10) return (0, false);
                if (snc > 1000 && trk == 36) return (7, false);
                return (0, true);
            }

            bool Check_Loader(byte[] d)
            {
                byte[][] p = new byte[10][];
                /// byte[] p contains a list of commonly repeating patters in the V-Max track 20 loader
                /// the following (for) statement checks the track for these patters, if 30 matches are found, we assume its a loader track
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
                    Buffer.BlockCopy(d, i, cmp, 0, cmp.Length);
                    for (int j = 0; j < p.Length; j++)
                    {
                        if (Match(cmp, p[j]))
                        {
                            if (j < 7) i += 4; else i += 3;
                            l++;
                        }
                    }
                }
                if (l > 30) return true; else return false;
            }

            bool Check_RapidLok(byte[] d)
            {
                int sync = 0;
                for (int i = 0; i < d.Length; i++)
                {
                    if (d[i] == 0xff) sync++;
                    else
                    {
                        if (sync > 10 && (d[i] == 0x75 && d[i + 1] == 0x92))
                        {
                            return true;
                        }
                        sync = 0;
                    }
                }
                return false;
            }
        }

        void Display_Data()
        {
            Stopwatch sw = Stopwatch.StartNew();
            sw.Start();
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
            for (int i = 0; i < tracks; i++)
            {
                Invoke(new Action(() => DV_pbar.Maximum = (int)((double)DV_pbar.Value / (double)(i + 1) * tracks)));
                if (NDS.cbm[i] > 0 && NDS.cbm[i] < secF.Length - 1 && NDG.Track_Data?[i] != null)
                {
                    if (DV_gcr.Checked)
                    {
                        jmp++;
                        try
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
                        }
                        catch { }
                    }
                    if (DV_dec.Checked)
                    {
                        if (NDS.cbm[i] == 1) Disp_CBM(i, trk, false);
                        if (NDS.cbm[i] == 5) Disp_VPL(i, trk);
                        if (NDS.cbm[i] == 10) Disp_CBM(i, trk, true);
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
                sw.Stop();
                if (DB_timers.Checked) label2.Text = $"Display Disk Data {sw.Elapsed.TotalMilliseconds} ms";
                GC.Collect();
                View_Jump();
                Data_Box.Visible = true;
                //if (DV_dec.Checked) File.WriteAllBytes($@"c:\test\{fname}_Decoded.bin", buffer.ToArray());

            }));

            void Disp_CBM(int t, double track, bool mps)
            {
                byte[][] temp = new byte[NDS.sectors[t]][];
                bool[] nul = new bool[NDS.sectors[t]];
                if (DV_dec.Checked)
                {
                    jt[(int)trk] = db_Text.Length;
                    try
                    {
                        int total = 0;
                        byte[] tmp = new byte[NDG.Track_Data[t].Length];
                        Buffer.BlockCopy(NDG.Track_Data[t], 0, tmp, 0, tmp.Length);
                        BitArray tdata = new BitArray(Flip_Endian(tmp));
                        for (int i = 0; i < NDS.sectors[t]; i++)
                        {
                            if (mps)
                            {
                                int pos = 0;
                                (nul[i], pos) = Find_Sector(tdata, i);
                                byte[] sec_data = new byte[335];
                                Buffer.BlockCopy(NDG.Track_Data[t], pos, sec_data, 0, sec_data.Length);
                                temp[i] = Decode_CBM_GCR(sec_data);
                            }
                            else
                            {
                                (temp[i], nul[i]) = Decode_CBM_Sector(NDG.Track_Data[t], i, true, tdata);
                            }
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
                byte[][] temp = new byte[NDS.sectors[t]][];
                jt[(int)trk] = db_Text.Length;
                if (DV_dec.Checked)
                {
                    byte[] tmp = new byte[NDG.Track_Data[t].Length];
                    Buffer.BlockCopy(NDG.Track_Data[t], 0, tmp, 0, tmp.Length);
                    BitArray tdata = new BitArray(Flip_Endian(tmp));
                    int interleave = 3;
                    int current = 0;
                    int s = 0;
                    int total = 0;
                    for (int ii = 0; ii < NDS.sectors[t]; ii++)
                    {
                        temp[ii] = Decode_Vorpal(tdata, ii);
                        total += temp[ii].Length;
                    }
                    if (tr) db_Text += $"\n\nTrack ({track}) {secF[NDS.cbm[t]]} Sectors ({NDS.sectors[t]}) Length ({total}) bytes\n\n";
                    for (int ii = 0; ii < NDS.sectors[t]; ii++)
                    {
                        string temp2 = "";
                        if (se) db_Text += $"\n\nSector ({current}) Length ({temp[ii].Length}) bytes\n\n";
                        if (VS_dat.Checked) db_Text += Encoding.ASCII.GetString(Fix_Stops(temp[current]));
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
                temp += $"{Hex_Val(data, pos, length)}    ".Replace('-', ' ');
                byte[] temp2 = new byte[length];
                Buffer.BlockCopy(data, pos, temp2, 0, length);
                temp += $"{spc}{Encoding.ASCII.GetString(Fix_Stops(temp2))}\n";
                return temp;
            }

            string Append_Bin(byte[] data, int pos, int length, int expected_length = 0)
            {
                byte[] temp2 = new byte[length];
                Buffer.BlockCopy(data, pos, temp2, 0, length);
                string spc = "";
                if (expected_length > 0) for (int j = 0; j < expected_length - length; j++) spc += "         ";
                string temp = "";
                temp += $"{Byte_to_Binary(temp2)}     ";
                temp += $"{spc}{Encoding.ASCII.GetString(Fix_Stops(temp2))}\n";
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