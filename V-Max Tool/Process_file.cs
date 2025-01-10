using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
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
        private bool v2cc = false;
        private bool v2aa = false;
        private bool v3cc = false;
        private bool v3aa = false;
        private bool rad = false;
        private int radsec = 0;
        private string fname = "";
        private string fext = "";
        private string fnappend = "";
        private int tracks = 0;
        private bool displayed = false;
        private bool loader_fixed = false;
        private byte[] nib_header = new byte[256];
        private byte[] g64_header = new byte[684];
        private readonly byte[][] p = new byte[10][];
        private readonly string[] supported = { ".nib", ".g64", ".d64", ".nbz", ".z64" }; // Supported file extensions list
        /// vsec = the CBM sector header values & against byte[] sz
        private readonly string[] valid_cbm = { "52-40-05-28", "52-40-05-2C", "52-40-05-48", "52-40-05-4C", "52-40-05-38", "52-40-05-3C", "52-40-05-58", "52-40-05-5C",
            "52-40-05-24", "52-40-05-64", "52-40-05-68", "52-40-05-6C", "52-40-05-34", "52-40-05-74", "52-40-05-78", "52-40-05-54", "52-40-05-A8",
            "52-40-05-AC", "52-40-05-C8", "52-40-05-CC", "52-40-05-B8" };
        /// vmax = the block header values of V-Max v2 sectors (non-CBM sectors)
        private readonly string[] secF = { "Non-DOS", "CBM", "V-Max v2", "V-Max v3", "Loader", "Vorpal", "RapidLok", "RL-Key", "EA", "RA/MB", "Microprose", "GMA", "Unformatted" };
        private int[] jt = new int[42];
        const int MAX_TRACK_SIZE = 8192;
        const int SAMPLE_SIZE = 1024;

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
                string parent;
                string[] batch_list;
                //if (Cores <= 3) 
                Invoke(new Action(() => { Batch_Box.Visible = true; label8.Text = "Gathering files..."; label9.Text = ""; }));
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
                RunBusy(() =>
                {
                    Auto_Adjust = true;
                    Set_Auto_Opts();
                });
                Drag_pic.Visible = Adv_ctrl.Enabled = false;
                Batch_Box.Visible = true;
                batch = true;
                CBD_box.Enabled = false;
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
                            string curfile = $@"{path}\{Path.GetDirectoryName(batch_list[i]).Replace(basedir, "")}\{Path.GetFileNameWithoutExtension(batch_list[i]).Replace("_ReMaster", "")}{fnappend}.g64";
                            fext = Path.GetExtension(batch_list[0]);
                            if (fext.ToLower() == supported[0] || fext.ToLower() == supported[3]) Batch_NIB(batch_list[i], curfile);
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
                            s = $"Batch processing completed..\n {batch_list.Length} files processed in {btime.Elapsed.TotalSeconds:F2} seconds\nAverage" +
                            $" {(btime.Elapsed.TotalMilliseconds / batch_list.Length):F2}ms per file";
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
                    RunBusy(() =>
                    {
                        Auto_Adjust = temp;
                        CBD_box.Enabled = true;
                    });
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
                var ext = Path.GetExtension(fn);
                if (ext.ToLower() == ".nib")
                {
                    tracks = (int)(length - 256) / MAX_TRACK_SIZE;
                    nib_header = new byte[256];
                    Stream.Seek(0, SeekOrigin.Begin);
                    Stream.Read(nib_header, 0, 256);
                    Set_Arrays(tracks);
                    for (int i = 0; i < tracks; i++)
                    {
                        NDS.Track_Data[i] = new byte[MAX_TRACK_SIZE];
                        Stream.Seek(256 + (MAX_TRACK_SIZE * i), SeekOrigin.Begin);
                        Stream.Read(NDS.Track_Data[i], 0, MAX_TRACK_SIZE);
                        Original.OT[i] = new byte[0];
                    }
                    Stream.Close();
                }
                if (ext.ToLower() == ".nbz")
                {
                    byte[] compressed = new byte[length];
                    Stream.Seek(0, SeekOrigin.Begin);
                    Stream.Read(compressed, 0, (int)length);
                    byte[] decomp = LZdecompress(compressed);
                    nib_header = new byte[256];
                    length = decomp.Length;
                    tracks = ((int)length - 256) / MAX_TRACK_SIZE;
                    Set_Arrays(tracks);
                    Buffer.BlockCopy(decomp, 0, nib_header, 0, 256);
                    for (int i = 0; i < tracks; i++)
                    {
                        NDS.Track_Data[i] = new byte[MAX_TRACK_SIZE];
                        Buffer.BlockCopy(decomp, 256 + (MAX_TRACK_SIZE * i), NDS.Track_Data[i], 0, MAX_TRACK_SIZE);
                        Original.OT[i] = new byte[0];
                    }
                }
                if ((tracks * MAX_TRACK_SIZE) + 256 == length)
                {
                    var head = Encoding.ASCII.GetString(nib_header, 0, 13);
                    if (head == "MNIB-1541-RAW")
                    {
                        try
                        {
                            //ErrorList = new ConcurrentBag<string>();
                            Stopwatch parse = Parse_Nib_Data();
                            if (!error)
                            {
                                Stopwatch proc = Process_Nib_Data(true, false, true);
                                if (DB_timers.Checked) Invoke(new Action(() =>
                                {
                                    label2.Text = $"Parse time : {parse.Elapsed.TotalMilliseconds} ms, Process time : {proc.Elapsed.TotalMilliseconds} Total {parse.Elapsed.TotalMilliseconds + proc.Elapsed.TotalMilliseconds} ms";
                                }));
                                Make_G64(output, end_track);
                            }
                        }
                        catch { }
                    }
                }
            }
        }

        Stopwatch Parse_Nib_Data()
        {
            ErrorList = new ConcurrentBag<string>();
            Stopwatch sw = new Stopwatch();
            sw.Start();
            Invoke(new Action(() =>
            {
                RL_Fix.Checked = false;
                Import_Progress_Bar.Value = 0;
                Import_Progress_Bar.Maximum = 100;
                Import_Progress_Bar.Maximum *= 100;
                Import_Progress_Bar.Value = Import_Progress_Bar.Maximum / 100;
                if (Cores < 8) Import_Progress_Bar.Visible = true;
            }));
            int cbm = 0; int vmx = 0; int vpl = 0; int rlk = 0; int mps = 0;
            double ht;
            bool halftracks = false;
            string[][] f = new string[tracks][];
            string tr = "Track";
            string le = "Length";
            string fm = "Format";
            string bl = "** Potentially bad loader! **";
            if (tracks > 42)
            {
                halftracks = true;
                ht = 0.5;
            }
            else ht = 0;
            /// ------------ Safe Threading Method, Starts as many threads as there are physical CPU cores available ----------------------
            /// ------------ CPU_Killer (true) Starts as many threads as there are jobs to do. (can overwhelm slower CPU's quickly!) ------
            Job = new Thread[tracks];
            for (int i = 0; i < tracks; i++)
            {
                int x = i;
                Task_Limit.WaitOne();
                Job[i] = new Thread(new ThreadStart(() => Analyze_Track(x)));
                Job[i].Start();
                Update_Progress_Bar(i);
                if (tracks > 42) i++;
            }
            foreach (var thread in Job) thread?.Join();
            Check_Formats(); /// <- Checks and corrects falsly identified track formats

            Job = new Thread[0];
            for (int i = 0; i < tracks; ++i)
            {
                if (NDS.cbm[i] == 1 && NDS.sectors[i] == 0)
                {
                    NDS.cbm[i] = 0;
                    Get_Track_Info(i);
                }
            }
            /// -- Checks for false positive of RapidLok Key track on non-RapidLok images
            if (NDS.cbm.Any(x => x == 7) && !NDS.cbm.Any(x => x == 6))
            {
                for (int i = 0; i < tracks; i++) if (NDS.cbm[i] == 7) NDS.cbm[i] = secF.Length - 1;
            }
            bool cust_dens = false;
            bool v2 = false;
            bool v3 = false;
            bool fat = false;
            if (!batch)
            {
                var color = Color.Black;
                Invoke(new Action(() =>
                {
                    if (tracks > 42)
                    {
                        halftracks = true;
                        ht = 0.5;
                    }
                    else ht = 0;
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
                        //if (NDS.cbm[i] > 0 && NDS.cbm[i] < secF.Length - 1)
                        if (NDS.cbm[i] >= 0 && NDS.cbm[i] < secF.Length - 1)
                        {
                            if (ht > 17 && ht < 35)
                            {
                                var d = Get_Density(NDS.Track_Length[i] >> 3);
                                if ((ht >= 0 && ht < 18 && d != 0) || (ht >= 18 && ht < 25 && d != 1) || (ht >= 25 && ht < 31 && d != 2) || (ht >= 31 && ht < 43 && d != 3)) cust_dens = true;
                            }
                            if (NDS.cbm[i] == 2) v2 = true;
                            if (NDS.cbm[i] == 3) v3 = true;
                        }
                        //if (!batch && (NDS.Track_Length[i] > 6000 && NDS.Track_Length[i] >> 3 < 8100) && NDS.cbm[i] != secF.Length - 1 && NDS.cbm[i] > 0)
                        if (!batch && (NDS.Track_Length[i] > 6000 && NDS.Track_Length[i] >> 3 < 8100) && NDS.cbm[i] != secF.Length - 1 && NDS.cbm[i] >= 0)
                        {
                            color = Color.Black;
                            var d = Get_Density(NDS.Track_Length[i] >> 3);
                            string e = "";
                            if ((ht >= 31 && d != 3) || (ht >= 25 && ht < 31 && d != 2) || (ht >= 18 && ht < 25 && d != 1) || (ht >= 0 && ht < 18 && d != 0)) e = " [!]";
                            if (NDS.cbm[i] == 0) color = Color.FromArgb(110, 70, 173);
                            if (NDS.cbm[i] == 1) color = Color.Black;
                            if (NDS.cbm[i] == 2) color = Color.DarkMagenta;
                            if (NDS.cbm[i] == 3) color = Color.Green;
                            if (NDS.cbm[i] == 4) color = Color.Blue;
                            if (NDS.cbm[i] == 5) color = Color.DarkCyan;
                            if (NDS.cbm[i] == 6) color = Color.DarkOrange;
                            if ((NDS.cbm[i] >= 7 && NDS.cbm[i] <= 9) || NDS.cbm[i] == 11) color = Color.Blue;
                            if (NDS.cbm[i] == 10) color = Color.Brown;
                            sf.Items.Add(new LineColor { Color = color, Text = $"{secF[NDS.cbm[i]]}{Fat}" });
                            sl.Items.Add((NDS.Track_Length[i] >> 3).ToString("N0"));
                            string sec = (NDS.sectors[i] > 0 ? NDS.sectors[i].ToString() : "n/a");
                            //ss.Items.Add($"{NDS.sectors[i]}");
                            ss.Items.Add(sec);
                            strack.Items.Add(ht);
                            sd.Items.Add($"{3 - d}{e}");
                        }
                        if (NDS.cbm[i] == 0)
                        {
                            if (NDS.Track_Length[i] > 6000 << 3)
                            {
                                Track_Info.Items.Add(new LineColor { Color = Color.Blue, Text = $"{tr} {t} {fm} : {secF[NDS.cbm[i]]}" });
                                Track_Info.Items.Add(new LineColor { Color = color, Text = $"Track length : ({NDS.Track_Length[i] >> 3}) Custom Format" });
                                Track_Info.Items.Add(" ");
                            }
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
                                color = f[i][j].Contains("(Failed!)") ? Color.FromArgb(190, 0, 0) : color;
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
                                Color color1 = f[i][j].Contains("(Failed!)") ? Color.FromArgb(190, 0, 0) : Color.DarkBlue;
                                Track_Info.Items.Add(new LineColor { Color = color1, Text = f[i][j] });
                            }
                            Track_Info.Items.Add(new LineColor { Color = Color.Black, Text = $"Track Length : ({(NDS.D_End[i] - NDS.D_Start[i] >> 3)}) Sectors ({NDS.sectors[i]})" });
                            Track_Info.Items.Add(" ");
                        }
                        if (NDS.cbm[i] == 6)
                        {
                            Track_Info.Items.Add(new LineColor { Color = Color.Blue, Text = $"{tr} {t} {fm} : {secF[NDS.cbm[i]]}" });
                            try
                            {
                                if (f[i] != null)
                                {
                                    for (int j = 0; j < f[i].Length; j++)
                                    {
                                        Color color1 = f[i][j].Contains("(Failed!)") ? Color.FromArgb(190, 0, 0) : f[i][j].Contains("(Empty") ? Color.Black : Color.DarkMagenta;
                                        Track_Info.Items.Add(new LineColor { Color = color1, Text = f[i][j] });
                                    }
                                }
                            }
                            catch { }
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
                        if (NDS.cbm[i] == 11)
                        {
                            Track_Info.Items.Add(new LineColor { Color = Color.DarkBlue, Text = $"{tr} {t} {fm} : GMA/Securispeed {tr} {le} ({NDG.Track_Data[i].Length})" });
                        }
                        Track_Info.EndUpdate();
                    }
                    if (!cust_dens) Cust_Density.Text = "Track Densities : Standard"; else Cust_Density.Text = "Track Densities : Custom";
                    if (v2) NDS.Prot_Method = "Protection : V-Max v2";
                    if (v3) NDS.Prot_Method = "Protection : V-Max v3";
                    if (!v2 && !v3 && NDS.cbm.Any(x => x == 4)) NDS.Prot_Method = "Protection: V-Max v2 CBM";
                    if (!v2 && !v3 && !NDS.cbm.Any(x => x == 4)) NDS.Prot_Method = "Protection: None or CBM exploit";
                    if (fat) NDS.Prot_Method = "Protection: Fat-Tracks";
                    if (!v2 && !v3 && NDS.cbm.Any(x => x == 6)) NDS.Prot_Method = "Protection: RapidLok";
                    if (NDS.cbm.Any(s => s == 5)) NDS.Prot_Method = "Protection: Vorpal";
                    if (NDS.cbm.Any(x => x == 8)) NDS.Prot_Method = "Protection: (EA) PirateSlayer / Buster";
                    if (NDS.cbm.Any(x => x == 9)) NDS.Prot_Method = "Protection: Rainbow Arts / Magic Bytes";
                    if (NDS.cbm.Any(x => x == 10)) NDS.Prot_Method = "Protection: Micro Prose";
                    if (NDS.cbm.Any(x => x == 11)) NDS.Prot_Method = "Protection: GMA / Securispeed";
                    Update();
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
                if (NDS.cbm[trk] == 0)
                {
                    byte[] temp;
                    int snc = 0;
                    for (int i = 0; i < NDS.Track_Data[trk].Length; i++)
                    {
                        if (NDS.Track_Data[trk][i] == 0xff) snc++;
                    }
                    if (snc > NDS.Track_Data[trk].Length - 5) temp = FastArray.Init(density[3], 0xff);
                    else
                    {
                        temp = Custom_Format(NDS.Track_Data[trk]);
                        int consecutive = 0;
                        int pad = 0;
                        bool breakLoop = false;
                        int tempLength = temp.Length;

                        // Avoid using LINQ's .Any() inside a loop, and cache the blank array length
                        int blankLength = blank.Length;
                        for (int i = 0; i < tempLength; i++)
                        {
                            if (temp[i] == 0x55) pad++;

                            bool isBlank = false;
                            for (int j = 0; j < blankLength; j++)
                            {
                                if (temp[i] == blank[j])
                                {
                                    isBlank = true;
                                    break;
                                }
                            }

                            if (!isBlank) consecutive++;
                            else
                            {
                                if (consecutive > 50)
                                {
                                    breakLoop = true;
                                    break;
                                }
                                consecutive = 0;
                            }
                        }

                        if (!breakLoop && consecutive < 50) temp = new byte[0];

                        if (pad > 3000 && temp.Length >= density[3])
                        {
                            byte[] tmp = new byte[density[3]];
                            Buffer.BlockCopy(temp, 0, tmp, 0, tmp.Length);
                            temp = tmp;
                        }
                    }
                    if (temp.Length <= 8000)
                    {
                        int tempLengthBits = temp.Length << 3;
                        NDS.D_Start[trk] = 0;
                        NDS.D_End[trk] = tempLengthBits;
                        NDS.Track_Length[trk] = tempLengthBits;
                        Set_Dest_Arrays(temp, trk);
                    }
                    else
                    {
                        NDS.cbm[trk] = secF.Length - 1;
                        NDS.D_Start[trk] = NDS.D_End[trk] = NDS.Track_Length[trk] = 0;
                    }
                }

                if (NDS.cbm[trk] == 1 || NDS.cbm[trk] == 10)
                {
                    try
                    {
                        bool cksm = !batch;
                        int[] junk;
                        if (NDS.cbm[trk] == 1) cbm++; else mps++;
                        int it = 0;
                        while (NDS.Track_Length[trk] < 6200 << 3 && it < 10)
                        {
                            if (it > 0) NDS.Track_Data[trk] = Rotate_Left(NDS.Track_Data[trk], 10);
                            try
                            {
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
                                    NDS.Track_ID[trk],
                                    NDS.Adjust[trk]) = CBM_Track_Info(NDS.Track_Data[trk], cksm, trk);
                                if (NDS.Track_Length[trk] > 8000 << 3) break;
                                it++;
                            }
                            catch { }
                        }
                        NDA.sectors[trk] = NDS.sectors[trk];
                        if (NDS.sectors[trk] == 1)
                        {
                            NDS.Track_Length[trk] = density[3] << 3;
                        }
                    }
                    catch { }
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
                    if (NDS.sectors[trk] == 0)
                    {
                        NDS.cbm[trk] = secF.Length - 1;
                        NDS.Track_Data[trk] = FastArray.Init(8192, 0x00);
                    }
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
                    int tk = tracks > 42 ? (trk / 2) + 1 : trk + 1;
                    rlk++;
                    int q = 0;
                    byte[] temp = new byte[q];
                    (temp,
                        NDS.D_Start[trk],
                        NDS.D_End[trk], q,
                        NDS.sectors[trk],
                        NDS.Header_Len[trk],
                        f[trk]) = RapidLok_Track_Info(NDS.Track_Data[trk], trk, false, new byte[] { 0x00 });
                    if (q < (8000 << 3) && tk < 36) NDS.Track_Length[trk] = q;
                    else
                    {
                        NDS.cbm[trk] = secF.Length - 1;
                    }
                }

                if (NDS.cbm[trk] == 7)
                {
                    rlk++;
                    byte[] newkey; // = new byte[0];
                    (newkey, NDS.Loader) = RapidLok_Key_Fix(NDS.Track_Data[trk], !Replace_RapidLok_Key ? null : rl_nkey);
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
                    byte[] RA = RainbowArts(NDS.Track_Data[trk]);
                    NDS.Track_Length[trk] = RA.Length << 3;
                    Set_Dest_Arrays(RA, trk);
                }

                if (NDS.cbm[trk] == 11)
                {
                    byte[] GMA = Securispeed(NDS.Track_Data[trk]);
                    NDS.Track_Length[trk] = GMA.Length << 3;
                    Set_Dest_Arrays(GMA, trk);
                }
            }

            void Analyze_Track(int track)
            {
                try
                {
                    Get_Fmt(track);
                }
                catch { }
                try
                {
                    Get_Track_Info(track);
                }
                catch { }
                Task_Limit.Release();
            }

            void Check_Formats()
            {
                int m = Find_Most_Frequent_Format(NDS.cbm);
                int[] skip = new int[] { 0, 1, 4, 7, 8, 9, 11, secF.Length - 1 };
                if (!(skip.Any(x => x == m)))
                {
                    HashSet<int> ignore = new HashSet<int>();
                    if (m == 2 || m == 3) ignore.UnionWith(new int[] { 0, 1, 4 });
                    if (m == 5 || m == 10) ignore.UnionWith(new int[] { 0, 1 });
                    if (m == 6) ignore.UnionWith(new int[] { 0, 1, 7 });
                    Change_Fmt(ignore, m);
                }

                void Change_Fmt(HashSet<int> ign, int format)
                {
                    List<int> indicesToUpdate = new List<int>();

                    for (int i = 0; i < NDS.cbm.Length; i++)
                    {
                        if (i > 1 && !ign.Contains(NDS.cbm[i]) && NDS.cbm[i] != format)
                        {
                            indicesToUpdate.Add(i);
                        }
                    }

                    foreach (var index in indicesToUpdate)
                    {
                        NDS.cbm[index] = format;
                        Get_Track_Info(index);
                    }
                }
            }
        }
        /// ---------------------------------------------------------------------------------------------------------------------------------------------------

        Stopwatch Process_Nib_Data(bool cbm, bool short_sector, bool rb_vm, bool wait = false, bool new_disk = false)
        {
            VM_Ver.Text = "No Protection or CBM exploit";
            rad = false;
            int radtrk = 0;
            Stopwatch sw = new Stopwatch();
            sw.Start();
            bool halftracks = false, v2a = false, v3a = false, vpa = false, fl = false, sl = false, cbmadj = false;
            bool v3adj = false, v2adj = false, vpadj = false, v2cust = false, v3cust = false, cyan = false;
            int vpl_lead = 0;
            string rem = string.Empty;
            int ctrk = -1;
            Invoke(new Action(() =>
            {
                RunBusy(() =>
                {
                    end_track = tracks;
                    fat_trk = -1;
                    if (!new_disk) (cyan, ctrk) = Check_Cyan_Loader(false);
                    if (cyan)
                    {
                        int tk = ctrk == -1 ? 40 : tracks > 42 ? (ctrk / 2) + 1 : ctrk + 1;
                        NDS.Prot_Method = $"Protection: Cyan Loader [ track {tk} ]";
                    }
                    RM_cyan.Visible = cyan;
                    f_load.Visible = NDS.cbm.Any(s => s == 4);
                    Check_Adv_Opts();
                    Query_Track_Formats();
                    (v2a, v3a, vpa, v2adj, v2cust, v3adj, v3cust, cbmadj, sl, fl, vpadj, rb_vm, vpl_lead) = Set_Adjust_Options(rb_vm, cyan);
                    if (new_disk) fnappend = string.Empty;
                });
            }));
            /// ------------ Safe Threading Method, Starts as many threads as there are physical threads available ------------------------
            var ldt = 255;
            List<Task> tasks = new List<Task>();
            for (int i = 0; i < tracks; i++)
            {
                int x = i;
                var y = vpl_lead;
                if (NDS.cbm[i] != 4) tasks.Add(Task.Run(() => Process(x, y, ctrk, false)));
                else ldt = i;
                if (tracks > 42) i++;
            }
            Task.WhenAll(tasks).Wait();
            if (ldt < tracks) Process(ldt, 0, ctrk, false); /// (false) tells Process not to release the thread because it isn'format in a Semaphore or a thread
            if ((VM_Ver.Text == "No Protection or CBM exploit" || VM_Ver.Text.Contains("Radwar")) && rad)
            {
                byte[] temp = new byte[0];
                (rad, temp, radsec) = Radwar(NDG.Track_Data[radtrk], true, radsec);
                NDS.Prot_Method = $"Protection: Radwar [ track 18 sector {radsec + 1} ]";
                Set_Dest_Arrays(temp, radtrk);
            }
            if (cyan && RM_cyan.Checked)
            {
                int tk = tracks > 42 ? 8 : 4;
                byte[] temp = Cyan_Loader_Patch(NDG.Track_Data[tk]);
                Set_Dest_Arrays(temp, tk);
            }

            if (!batch)
            {
                VM_Ver.Text = $"{NDS.Prot_Method}{rem}";
                double ht;
                if (tracks > 42)
                {
                    halftracks = true;
                    ht = 0.5;
                }
                else ht = 0;
                Color color = new Color();
                Color tcolor = new Color();
                for (int i = 0; i < tracks; i++)
                {
                    if (halftracks) ht += .5; else ht += 1;
                    if (!batch && (NDS.cbm[i] < secF.Length - 1 && NDS.cbm[i] >= 0) && (NDS.Track_Length[i] > 6000 && NDS.Track_Length[i] >> 3 < 8100))
                    {
                        out_size.Items.Add((NDA.Track_Length[i] / 8).ToString("N0"));
                        tcolor = NDA.Track_Length[i] == 0 ? Color.Red : Color.Blue;
                        int weak = Get_Weak_Bytes(NDG.Track_Data[i]);
                        string weakbits = weak >= 0 ? $"{weak}" : "n/a";
                        out_weak.Items.Add($"       {weakbits}");
                        out_dif.Items.Add((NDA.Track_Length[i] - NDS.Track_Length[i] >> 3).ToString("+#;-#;0"));
                        string o = "";
                        var d = 0;
                        if (NDG.Track_Data?[i] != null) d = Get_Density(NDG.Track_Data[i].Length);
                        string e = "";
                        if ((ht >= 31 && d != 3) || (ht >= 25 && ht < 31 && d != 2) || (ht >= 18 && ht < 25 && d != 1) || (ht >= 0 && ht < 18 && d != 0)) e = " [!]";
                        if (NDG.Track_Data?[i] != null && NDG.Track_Data[i].Length > density[d])
                        {
                            if (NDG.Track_Data[i].Length > density[d] + 3) color = Color.Red;
                            if (NDG.Track_Data[i].Length > density[d] && NDG.Track_Data[i].Length < density[d] + 5) color = Color.Goldenrod;
                            o = $" + {NDG.Track_Data[i].Length - density[d]}";
                        }
                        else color = Color.Green;
                        if (NDG.Track_Data?[i] != null && NDG.Track_Data[i].Length < density[d]) o = $" - {density[d] - NDG.Track_Data[i].Length}";
                        Out_density.Items.Add(new LineColor { Color = color, Text = $"{3 - d}{e}{o}" });
                        out_track.Items.Add(new LineColor { Color = tcolor, Text = $"{ht}" });
                        double r = Math.Round(((double)density[Get_Density(NDA.Track_Length[i] >> 3)] / (double)(NDA.Track_Length[i] >> 3) * 300), 1);
                        if (r > 300) r = Math.Floor(r);
                        if (NDS.cbm[i] == 7) r = 300.0;
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
            }
            sw.Stop();
            //File.WriteAllBytes($@"c:\Replace_RapidLok_Key\rl_key.bin", NDS.Loader);
            //byte[] rltrks = new byte[tracks];
            //for (int i = 0; i < tracks; i++) rltrks[i] = (byte)NDS.Header_Len[i];
            //File.WriteAllBytes($@"c:\Replace_RapidLok_Key\7b_sec.bin", rltrks);
            return sw;

            void Process_Track(int trk, bool acbm, bool av2, bool cv2, bool av3, bool cv3, bool avp, int vplead, bool fix, bool sol, bool rvb, bool cmb, bool s_sec, bool cyn, int c_trk) //, bool swp)
            {
                if (NDS.Track_Length[trk] > 0 && NDS.cbm[trk] >= 0 && NDS.cbm[trk] < secF.Length)
                {
                    try
                    {
                        if (NDS.cbm[trk] == 0) Process_NDOS(trk);
                        if (NDS.cbm[trk] == 1) Process_CBM(trk, acbm, cmb, cyn, c_trk);
                        if (NDS.cbm[trk] == 2) Process_VMAX_V2(trk, av2, cv2, rvb);
                        if (NDS.cbm[trk] == 3) Process_VMAX_V3(trk, av3, cv3, rvb, s_sec);
                        if (NDS.cbm[trk] == 4) Process_Loader(trk, fix, sol);
                        if (NDS.cbm[trk] == 5) Process_Vorpal(trk, avp, vpl_lead);
                        if (NDS.cbm[trk] == 6) Process_RapidLok(trk);
                        if (NDS.cbm[trk] == 7) Process_RapidLokKey(trk);
                        if (NDS.cbm[trk] == 9) Process_Rainbow(trk);
                        if (NDS.cbm[trk] == 10) Process_MPS(trk, acbm);
                    }
                    catch { }
                }
                else { NDA.Track_Data[trk] = NDS.Track_Data[trk]; }
            }

            void Process(int track, int vorpal_lead = 0, int cyan_track = -1, bool release = true)
            {
                if (!release) try { Do_Work(); } catch { }
                else
                {
                    try
                    {
                        Task_Limit.WaitOne();
                        Do_Work();
                    }
                    finally
                    {
                        Task_Limit.Release();
                    }
                }

                void Do_Work()
                {

                    Process_Track(track, cbmadj, v2adj, v2cust, v3adj, v3cust, vpadj, vorpal_lead, fl, sl, rb_vm, cbm, short_sector, cyan, cyan_track);
                }
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
                (sec, pos, _, _) = Find_Sector(source, 0);
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

            void Process_CBM(int trk, bool acbm, bool bmc, bool cyn_ldr, int ctrack)
            {
                bool ad = NDS.Adjust[trk] && !NDS.cbm.Any(x => x == 9);
                //ad = false;
                int htk = tracks > 42 ? 2 : 1;
                int track = tracks > 42 ? (trk / 2) + 1 : trk + 1;
                if ((track != NDS.Track_ID[trk] && track == NDS.Track_ID[trk] + 1) || (trk + htk < NDS.Track_ID.Length && track == NDS.Track_ID[trk + htk]))
                {
                    NDG.Fat_Track[trk] = true;
                    if (fat_trk < 0) fat_trk = track;
                    //if (track != NDS.Track_ID[trk] && track >= 34 && !NDS.cbm.Any(x => x == 11)) end_track = trk + htk;
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
                    int min_snc = 35;   /// minimum sync length to signal this is a sync marker that needs adjusting (* original value : 16)
                    int ign_snc = 80;   /// ignore sync if it is >= to value
                    var d = 0;
                    if (track == 18 && NDS.cbm.Any(x => x == 6)) acbm = true;

                    if (bmc || acbm)
                    {
                        try
                        {
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
                                var sectors = NDS.sectors[trk];
                                bool condition1 = (track == 18 && (NDS.cbm.Any(x => x == 5) || NDS.cbm.Any(x => x == 6)));
                                bool condition2 = (track == 40 && cyn_ldr);
                                bool condition3 = (track > 34 && (sectors < 17));
                                bool condition4 = cyn_ldr && track == 32;
                                if (condition1 || condition2) // || condition3)
                                {
                                    int den = condition3 ? Get_Density(temp.Length) : density_map[track];
                                    int len = temp.Length;
                                    temp = len < density[den] ? Lengthen_Track(temp, den) : Shrink_Track(temp, den);
                                }
                                else
                                {
                                    bool condition5 = (track < 18 && NDS.sectors[trk] != 21 && NDS.Track_Length[trk] < 8000);
                                    if (DB_force.Checked || !(NDS.cbm.Any(x => x == 4) && !(NDS.cbm.Any(x => x == 3) || NDS.cbm.Any(x => x == 2))))
                                    {
                                        if (!(condition5) || DB_force.Checked)
                                        {
                                            d = Get_Density(NDS.Track_Length[trk] >> 3);
                                            try
                                            {
                                                temp = Rebuild_CBM(NDS.Track_Data[trk], NDS.sectors[trk], NDS.t18_ID, d, NDS.Track_ID[trk], NDS.D_Start[trk], condition4);
                                            }
                                            catch { }
                                        }
                                    }
                                }
                            }
                            if (track == 18)
                            {
                                int[] cbmRange = { 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };
                                if (cbmRange.All(x => !NDS.cbm.Any(y => y == x)) && NDS.sectors[trk] == 19)
                                {
                                    int sec = 0;
                                    (rad, temp, sec) = Radwar(temp);
                                    if (rad)
                                    {
                                        radtrk = trk;
                                        radsec = sec;
                                    }
                                }
                            }
                            if ((track == 40 && NDS.sectors[trk] < 17)) temp = Remove_Weak_Bits(temp);
                            bool nul = false;
                            if (ctrack > 0 && (trk == ctrack)) (temp, nul) = Cyan_t32_GCR_Fix(temp);
                            Set_Dest_Arrays(temp, trk);
                        }
                        catch { error = true; }
                    }
                }
            }

            void Process_VMAX_V2(int trk, bool av2a, bool cv2c, bool rbv)
            {
                bool replace_headers = V2_swap_headers.Checked;
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
                    (tdata, NDA.D_Start[trk], NDA.D_End[trk], NDA.Sector_Zero[trk]) = Rebuild_V2(Original.OT[trk], NDS.sectors[trk], NDS.v2info[trk], trk, NDG.newheader, replace_headers);
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
                            Adjust_Vmax_V3_Sync(NDS.Track_Data[trk], NDS.D_Start[trk], NDS.D_End[trk], NDS.Sector_Zero[trk], NDS.sectors[trk]);
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
                    byte[] temp = Rebuild_V3(NDG.Track_Data[trk], NDS.Gap_Sector[trk], NDS.t18_ID, trk);
                    Set_Dest_Arrays(temp, trk);
                }
                if (NDG.Track_Data[trk].Length > 0)
                {
                    try
                    {
                        NDA.Track_Data[trk] = new byte[MAX_TRACK_SIZE];
                        Buffer.BlockCopy(NDG.Track_Data[trk], 0, NDA.Track_Data[trk], 0, NDG.Track_Data[trk].Length);
                        Buffer.BlockCopy(NDG.Track_Data[trk], 0, NDA.Track_Data[trk], NDG.Track_Data[trk].Length, MAX_TRACK_SIZE - NDG.Track_Data[trk].Length);
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
                if (Original.OT[trk].Length == 0)
                {
                    Original.OT[trk] = new byte[NDG.Track_Data[trk].Length];
                    Buffer.BlockCopy(NDG.Track_Data[trk], 0, Original.OT[trk], 0, NDG.Track_Data[trk].Length);
                }
                byte[] temp = new byte[0];
                if (avp)
                {
                    temp = Rebuild_Vorpal(Original.OT[trk], trk, lead);
                }
                else
                {
                    //temp = NDG.Track_Data[trk];
                    BitArray source = new BitArray(Flip_Endian(NDS.Track_Data[trk]));
                    BitArray dest = new BitArray(NDS.Track_Length[trk] + 1);
                    int pos = NDS.Header_Len[trk];
                    for (int i = 0; i < NDS.Track_Length[trk] + 1; i++)
                    {
                        dest[i] = source[pos++];
                        if (pos == NDS.D_End[trk] + 1) pos = NDS.D_Start[trk];
                    }
                    temp = Bit2Byte(dest);
                }
                Set_Dest_Arrays(temp, trk);
            }

            void Process_RapidLok(int trk)
            {
                int track = trk;
                if (tracks > 42) track = (trk / 2);
                int sbl = Replace_RapidLok_Key ? rl_7b[track] : 0;
                byte[] temp;
                int q; int s; int e; int b; int h;
                string[] f;
                (temp, s, e, q, b, h, f) = RapidLok_Track_Info(NDS.Track_Data[trk], trk, true, NDS.t18_ID, sbl);
                Set_Dest_Arrays(temp, trk);
            }

            void Process_RapidLokKey(int trk)
            {
                byte[] newkey;
                (newkey, NDS.Loader) = RapidLok_Key_Fix(NDS.Track_Data[trk], !Replace_RapidLok_Key ? null : rl_nkey);
                NDS.Track_Length[trk] = newkey.Length << 3;
                Set_Dest_Arrays(newkey, trk);
            }

            void Process_Rainbow(int trk)
            {
                byte[] temp = RainbowArts(NDS.Track_Data[trk]);
                Set_Dest_Arrays(temp, trk);
            }

            void Process_NDOS(int trk)
            {
                byte[] temp1 = new byte[NDG.Track_Data[trk].Length];
                byte[] temp = new byte[0];
                Buffer.BlockCopy(NDG.Track_Data[trk], 0, temp1, 0, temp1.Length);
                (int pos, int longest) = Longest_Run(temp1, new byte[] { 0x55, 0xaa });
                //if (longest > 5)
                //{
                //    temp1 = Rotate_Left(temp1, pos + longest);
                //}
                temp1 = longest > 5 ? Rotate_Left(temp1, pos + longest) : temp1;
                int d = Get_Density(temp1.Length);
                if (temp1.Length > density[d])
                {
                    temp = new byte[density[d]];
                    Buffer.BlockCopy(temp1, 0, temp, 0, temp.Length);
                    Set_Dest_Arrays(temp, trk);
                }
                else Set_Dest_Arrays(temp1, trk);
                // nothing to do
            }
        }

        int Get_Data_Format(byte[] data, int track)
        {
            bool blnk = false;
            int t = 0;
            int csec = 0;
            int trk = tracks > 42 ? (track / 2) + 1 : track + 1;
            if (trk > 35)
            {
                (t, blnk) = Check_Blank(data);
                if (!blnk && t == 9) return t;
                blnk = false;
                t = 0;
            }
            byte[] comp = new byte[4];
            if (Check_RapidLok(data))
            {
                return 6;
            }
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
                    (c, ps, _, _) = Find_Sector(source, i + 1);
                    /// ------------ Detect Microprose track --------------------------
                    if (c && ps + seek < data.Length)
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
                if (trk < 36)
                {
                    while (pos < source.Length - vpl_s0.Length * 8)
                    {
                        ncomp = Bit2Byte(source, pos, vpl_s0.Length << 3);
                        if (Match(vpl_s0, ncomp) || Match(vpl_s1, ncomp))
                        {
                            if (Get_VPL_Sectors(source) > 30) return 5;
                        }
                        pos++;
                    }
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
                        (c, ps, _, _) = Find_Sector(source, i + 1);
                        if (c) { y++; p = ps + 320; if (p > data.Length) p = 0; }
                        if (y > 4) return 1;
                    }
                    return 6;
                }
                if (d[0] == sz[0])
                {
                    d[1] &= sz[1]; d[2] &= sz[2]; d[3] &= sz[3];
                    if (valid_cbm.Contains(Hex_Val(d))) { csec++; if (csec > 1) return 1; } /// change csec > 6 if there are issues
                }
                return 0;
            }

            (int, bool) Check_Blank(byte[] d)
            {
                HashSet<byte> blankSet = new HashSet<byte>(blank);
                int b = 0;
                int snc = 0;
                if (d.All(x => x == 0xff)) return (0, false);
                for (int i = 0; i < d.Length; i++)
                {
                    if (blankSet.Contains(d[i])) b++;
                    if (d[i] == 0xff) snc++;

                    try
                    {
                        if (d[i] == 0xff && d[i + 1] == ssp[1])
                        {
                            if (CheckPattern(ssp, i) && CheckPadding(0)) return (11, false);
                        }

                        if (d[i] == gmt[0])
                        {
                            byte[] gm = new byte[gmt.Length];
                            try
                            {
                                Buffer.BlockCopy(d, i, gm, 0, gmt.Length);
                                for (int j = 1; j < gmt.Length; j++) gm[j] &= gmt[j];
                                if (Match(gm, gmt) && CheckPadding(0)) return (11, false);
                            }
                            catch { }
                        }

                        if (ps1.Contains(d[i]) || ps2.Contains(d[i]))
                        {
                            if ((d[i] == ps1[0] && CheckPattern(ps1, i)) || (d[i] == ps2[0] && CheckPattern(ps2, i))) return (8, false);
                        }

                        if ((trk > 35 && trk < 41) && d[i] == ramb[0] || (i > 0 && d[i] == 0xff && d[i - 1] != 0xff))
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
                                if (sc >= 108 && sc <= 132 && CheckPadding(0)) return (9, false);
                            }
                            else if (CheckPattern(ramb, i))
                            {
                                int ptn = 0;
                                int itt = 0;
                                while (i + (itt * ramb.Length) < d.Length)
                                {
                                    if (!CheckPattern(ramb, i + (itt * ramb.Length))) break;
                                    ptn++;
                                    if (ptn > 100) return (9, false);
                                    itt++;
                                }
                                return (9, false);
                            }
                        }
                    }
                    catch { }
                }

                if (b > 1000 && snc < 10) return (0, false);
                if (snc > 1000 && trk == 36) return (7, false);
                return (0, true);
                //return (0, false);

                bool CheckPattern(byte[] pattern, int pos)
                {
                    if (pos + pattern.Length >= d.Length - 1) return false;
                    byte[] compare = new byte[pattern.Length];
                    Buffer.BlockCopy(d, pos, compare, 0, pattern.Length);
                    return Match(pattern, compare);
                }

                bool CheckPadding(int start)
                {
                    int pad = 0;
                    for (int j = start; j < d.Length; j++)
                    {
                        if (d[j] == 0x55 || d[j] == 0xaa) pad++;
                        if (pad > 3000) return true;
                    }
                    return false;
                }
            }

            bool Check_Loader(byte[] d)
            {
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
                if (compare(0)) return true;
                int sync = 0;
                int sec = 0;
                for (int i = 0; i < 2800; i++)
                {
                    if (d[i] == 0xff) sync++;
                    else
                    {
                        if (sync > 3 && d[i] == 0x75) if (compare(i)) sec++;
                        if (sec == 2) return true;
                        sync = 0;
                    }
                }
                return false;

                bool compare(int p)
                {
                    byte[] cmp = new byte[6];
                    Buffer.BlockCopy(d, p, cmp, 0, cmp.Length);
                    cmp[1] &= RLok1[1]; cmp[2] &= RLok1[2];
                    if (cmp[0] == RLok1[0] && cmp[1] == 0x90 && cmp[2] == 0x09 && cmp[4] == 0xd6 && cmp[5] == 0xed) return true;
                    return false;
                }
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
            StringBuilder db_Text = new StringBuilder();
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
                if (NDS.cbm[i] >= 0 && NDS.cbm[i] < secF.Length - 1 && NDG.Track_Data?[i] != null && NDG.Track_Data?[i].Length > 6000)
                {
                    if (DV_gcr.Checked)
                    {
                        jmp++;
                        try
                        {
                            jt[(int)trk] = db_Text.Length;
                            if (tr) db_Text.Append($"\n\nTrack ({trk})  Data Format: {secF[NDS.cbm[i]]} {NDG.Track_Data[i].Length} Bytes\n\n");
                            if (VS_dat.Checked) db_Text.Append($"{Encoding.ASCII.GetString(Fix_Stops(NDG.Track_Data[i]))}");
                            if (VS_hex.Checked)
                            {
                                StringBuilder temp = new StringBuilder();
                                for (int j = 0; j < NDG.Track_Data[i].Length / hex; j++)
                                {
                                    temp.Append(Append_Hex(NDG.Track_Data[i], j * hex, hex));
                                }
                                var y = (NDG.Track_Data[i].Length / hex) * hex;
                                if (y < NDG.Track_Data[i].Length)
                                {
                                    temp.Append(Append_Hex(NDG.Track_Data[i], y, NDG.Track_Data[i].Length - y, hex));
                                }
                                db_Text.Append(temp);
                            }
                            if (VS_bin.Checked)
                            {
                                StringBuilder temp = new StringBuilder();
                                for (int j = 0; j < NDG.Track_Data[i].Length / bin; j++)
                                {
                                    temp.Append(Append_Bin(NDG.Track_Data[i], j * bin, bin));
                                }
                                var y = (NDG.Track_Data[i].Length / bin) * bin;
                                if (y < NDG.Track_Data[i].Length)
                                {
                                    temp.Append(Append_Bin(NDG.Track_Data[i], y, NDG.Track_Data[i].Length - y, bin));
                                }
                                db_Text.Append(temp);
                            }
                        }
                        catch { }
                    }
                    if (DV_dec.Checked)
                    {
                        int[] known_formats = new int[] { 1, 5, 6, 10 };
                        if (NDS.cbm[i] == 1) Disp_CBM(i, trk, false);
                        //if (NDS.cbm[i] == 1) Disp_STD_GCR(i, trk);
                        if (NDS.cbm[i] == 5) Disp_VPL(i, trk);
                        if (NDS.cbm[i] == 6) Disp_RLK(i, trk);
                        if (NDS.cbm[i] == 10) Disp_CBM(i, trk, true);
                        if (!known_formats.Any(x => x == NDS.cbm[i]) && NDS.Track_Length[i] > 6000) Disp_STD_GCR(i, trk);
                        jmp++;
                    }
                }
                if (ht) trk += .5; else trk += 1;
            }
            Invoke(new Action(() =>
            {
                T_jump.Maximum = jmp;
                if (ds >= 1 && jmp > 0) { T_jump.Visible = Jump.Visible = true; } else { T_jump.Visible = Jump.Visible = false; }
                Data_Box.Text = db_Text.ToString();
                Disp_Data.Text = "Refresh";
                DV_pbar.Value = 0;
                displayed = true;
                busy = false;
                sw.Stop();
                if (DB_timers.Checked) label2.Text = $"Display Disk Data {sw.Elapsed.TotalMilliseconds} ms";
                GC.Collect();
                View_Jump();
                Data_Box.Visible = true;
                //if (DV_dec.Checked) File.WriteAllBytes($@"c:\Replace_RapidLok_Key\{fname}_Decoded.bin", buffer.ToArray());

            }));

            void Disp_STD_GCR(int t, double track)
            {
                byte[] dec = new byte[0];
                int tlen = 0;
                BitArray s = new BitArray(Flip_Endian(NDG.Track_Data[t]));
                int snc = 0;
                int sec = 0;
                for (int i = 0; i < s.Length; i++)
                {
                    if (s[i]) snc++;
                    else
                    {
                        if (snc > 16)
                        {
                            sec++;
                        }
                        snc = 0;
                    }
                }
                byte[][] sectors = new byte[sec][];
                sec = 0;
                for (int i = 0; i < s.Length; i++)
                {
                    if (s[i]) snc++;
                    else
                    {
                        if (snc > 16)
                        {
                            try
                            {
                                if (sectors.Length > 1)
                                {
                                    int ssnc = 0;
                                    for (int j = i; j < s.Length; j++)
                                    {
                                        if (s[j]) ssnc++;
                                        else
                                        {
                                            if (ssnc > 16 || j == s.Length - 1)
                                            {
                                                if (sec >= sectors.Length) break;
                                                int ppos = (j) - i;
                                                sectors[sec] = Decode_CBM_GCR(Bit2Byte(s, i, ppos));
                                                tlen += sectors[sec].Length;
                                                sec++;
                                                i += ppos;
                                            }
                                            ssnc = 0;
                                        }
                                    }
                                }
                                else
                                {
                                    sectors[sec] = Decode_CBM_GCR(Bit2Byte(s, i));
                                    tlen += sectors[sec].Length;
                                    break;
                                }
                            }
                            catch { }
                        }
                        snc = 0;
                    }
                }
                if (sectors.Length == 0)
                {
                    sectors = new byte[1][];
                    sectors[0] = Decode_CBM_GCR(NDG.Track_Data[t]);
                    tlen += sectors[0].Length;
                }

                if (sectors.Length > 0)
                {
                    jt[(int)trk] = db_Text.Length;
                    if (tr) db_Text.Append($"\n\nTrack ({track})  Data Format: {secF[NDS.cbm[t]]} Length ({tlen}) bytes, Sectors ({sectors.Length})\n\n");

                    for (int i = 0; i < sectors.Length; i++)
                    {
                        if (sectors?[i] != null && sectors?[i].Length > 16)
                        {
                            StringBuilder temp2 = new StringBuilder();
                            if (se) db_Text.Append($"\n\nSector ({i + 1}) Length {sectors[i].Length}\n\n");
                            if (VS_dat.Checked) db_Text.Append(Encoding.ASCII.GetString(Fix_Stops(sectors[i])));
                            if (VS_hex.Checked)
                            {
                                for (int j = 0; j < sectors[i].Length / hex; j++)
                                {
                                    temp2.Append(Append_Hex(sectors[i], j * hex, hex));
                                }
                                var y = (sectors[i].Length / hex) * hex;
                                if (y < sectors[i].Length)
                                {
                                    temp2.Append(Append_Hex(sectors[i], y, sectors[i].Length - y, hex));
                                }
                                db_Text.Append(temp2);
                            }
                            if (VS_bin.Checked)
                            {
                                for (int j = 0; j < sectors[i].Length / bin; j++)
                                {
                                    temp2.Append(Append_Bin(sectors[i], j * bin, bin));
                                }
                                var y = (sectors[i].Length / bin) * bin;
                                if (y < sectors[i].Length)
                                {
                                    temp2.Append(Append_Bin(sectors[i], y, sectors[i].Length - y, bin));
                                }
                                db_Text.Append(temp2);
                            }

                        }
                    }
                }
            }

            void Disp_RLK(int t, double track)
            {
                byte[] dec = new byte[0];
                int tlen = 0;
                string contents = string.Empty;
                BitArray s = new BitArray(Flip_Endian(NDG.Track_Data[t]));
                int snc = 0;
                int sec = 0;
                for (int i = 0; i < s.Length; i++)
                {
                    if (s[i]) snc++;
                    else
                    {
                        if (snc > 16)
                        {
                            byte[] a = Bit2Byte(s, i, 16);
                            if (a[0] == 0x6b || (a[0] == 0x55 && a[1] != 0x7b)) sec++;
                        }
                        snc = 0;
                    }
                }
                byte[][] sectors = new byte[sec][];
                bool[] cksm = new bool[sec];
                sec = 0;
                for (int i = 0; i < s.Length; i++)
                {
                    if (s[i]) snc++;
                    else
                    {
                        if (snc > 16)
                        {
                            try
                            {
                                byte[] a = Bit2Byte(s, i, 16);
                                if (a[0] == 0x6b)
                                {
                                    (sectors[sec], cksm[sec]) = Decode_Rapidlok_GCR(Bit2Byte(s, i, 583 << 3), false);
                                    tlen += sectors[sec++].Length;
                                }
                                else if (a[0] == 0x55 && a[1] == 0x55)
                                {
                                    sectors[sec] = FastArray.Init(376, 0x00);
                                    tlen += sectors[sec++].Length;
                                }
                            }
                            catch { }
                        }
                        snc = 0;
                    }
                }
                
                if (sectors.Length > 0)
                {
                    jt[(int)trk] = db_Text.Length;
                    
                    if (tr) db_Text.Append($"\n\nTrack ({track})  Data Format: {secF[NDS.cbm[t]]} Length ({tlen}) bytes, Sectors ({sectors.Length})\n\n");

                    for (int i = 0; i < sectors.Length; i++)
                    {
                        if (sectors?[i] != null && sectors?[i].Length > 16)
                        {
                            StringBuilder temp2 = new StringBuilder();
                            contents = sectors[i].All(x => x == 0x00) ? " (Empty, No Data!)" : string.Empty;
                            string checksumStatus = contents != string.Empty ? "N/A" : (cksm[i] ? "OK" : "Failed!");
                            if (se) db_Text.Append($"\n\nSector ({i + 1}) Length {sectors[i].Length}{contents} Checksum ({checksumStatus})\n\n");
                            if (VS_dat.Checked) db_Text.Append(Encoding.ASCII.GetString(Fix_Stops(sectors[i])));
                            if (VS_hex.Checked)
                            {
                                for (int j = 0; j < sectors[i].Length / hex; j++)
                                {
                                    temp2.Append(Append_Hex(sectors[i], j * hex, hex));
                                }
                                var y = (sectors[i].Length / hex) * hex;
                                if (y < sectors[i].Length)
                                {
                                    temp2.Append(Append_Hex(sectors[i], y, sectors[i].Length - y, hex));
                                }
                                db_Text.Append(temp2);
                            }
                            if (VS_bin.Checked)
                            {
                                for (int j = 0; j < sectors[i].Length / bin; j++)
                                {
                                    temp2.Append(Append_Bin(sectors[i], j * bin, bin));
                                }
                                var y = (sectors[i].Length / bin) * bin;
                                if (y < sectors[i].Length)
                                {
                                    temp2.Append(Append_Bin(sectors[i], y, sectors[i].Length - y, bin));
                                }
                                db_Text.Append(temp2);
                            }

                        }
                    }
                }
            }

            void Disp_CBM(int t, double track, bool mps)
            {
                byte[][] temp = new byte[NDS.sectors[t]][];
                bool[] valid_checksum = new bool[NDS.sectors[t]];
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
                                (valid_checksum[i], pos, _, _) = Find_Sector(tdata, i);
                                byte[] sec_data = new byte[335];
                                Buffer.BlockCopy(NDG.Track_Data[t], pos, sec_data, 0, sec_data.Length);
                                temp[i] = Decode_CBM_GCR(sec_data);
                            }
                            else
                            {
                                (temp[i], valid_checksum[i]) = Decode_CBM_Sector(NDG.Track_Data[t], i, true, tdata);
                            }
                            total += temp[i].Length;
                        }
                        if (tr) db_Text.Append($"\n\nTrack ({track})  Data Format: {secF[NDS.cbm[t]]} Length ({total}) bytes\n\n");
                        for (int i = 0; i < NDS.sectors[t]; i++)
                        {
                            StringBuilder temp2 = new StringBuilder();
                            string ck = "";
                            if (valid_checksum[i]) ck = "Checksum OK"; else ck = "Checksum Failed!";
                            if (se) db_Text.Append($"\n\nSector ({i + 1}) Length {temp[i].Length} {ck}\n\n");
                            if (VS_dat.Checked) db_Text.Append(Encoding.ASCII.GetString(Fix_Stops(temp[i])));
                            if (VS_hex.Checked)
                            {
                                for (int j = 0; j < temp[i].Length / hex; j++)
                                {
                                    temp2.Append(Append_Hex(temp[i], j * hex, hex));
                                }
                                var y = (temp[i].Length / hex) * hex;
                                if (y < temp[i].Length)
                                {
                                    temp2.Append(Append_Hex(temp[i], y, temp[i].Length - y, hex));
                                }
                                db_Text.Append(temp2);
                            }
                            if (VS_bin.Checked)
                            {
                                for (int j = 0; j < temp[i].Length / bin; j++)
                                {
                                    temp2.Append(Append_Bin(temp[i], j * bin, bin));
                                }
                                var y = (temp[i].Length / bin) * bin;
                                if (y < temp[i].Length)
                                {
                                    temp2.Append(Append_Bin(temp[i], y, temp[i].Length - y, bin));
                                }
                                db_Text.Append(temp2);
                            }
                            write.Write(temp[i]);
                        }
                    }
                    catch { }
                }
            }

            void Disp_VPL(int t, double track)
            {
                string ok = "(OK)";
                string fail = "(Failed!)";
                byte[][] temp = new byte[NDS.sectors[t]][];
                bool[] cksm = new bool[NDS.sectors[t]];
                byte[] ID = new byte[NDS.sectors[t]];
                int pos = 0;
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
                        (temp[ii], cksm[ii], _, pos) = Decode_Vorpal(tdata, ii);
                        //byte[] id = Bit2Byte(tdata, pos + 1292, 8);
                        //ID[ii] = id[0];
                        //ID[ii] = (byte)(((id[0] & 0xc0) >> 2) | ((id[0] & 0x18) >> 1) | (id[0] & 0x03));
                        //ID[ii] = 
                        total += temp[ii].Length;
                    }
                    //List<int> l = new List<int>();
                    //foreach (byte b in ID) { if (!l.Contains(b)) l.Add(b); }
                    //if (ID.Length == 47) File.WriteAllBytes($@"c:\vorpal sectors.bin", ID.ToArray());
                    if (tr) db_Text.Append($"\n\nTrack ({track}) {secF[NDS.cbm[t]]} Sectors ({NDS.sectors[t]}) Length ({total}) bytes\n\n");
                    for (int ii = 0; ii < NDS.sectors[t]; ii++)
                    {
                        string ck = cksm[current] ? ok : fail;
                        StringBuilder temp2 = new StringBuilder();
                        if (se) db_Text.Append($"\n\nSector ({current}) Length ({temp[current].Length}) bytes. Checksum {ck}\n\n");
                        //if (se) db_Text.Append($"\n\nSector ({ID[ii]} {current}) {Convert.ToString(ID[ii], 2).PadLeft(8, '0')} Length ({temp[current].Length}) bytes. Checksum {ck}\n\n");
                        if (VS_dat.Checked) db_Text.Append(Encoding.ASCII.GetString(Fix_Stops(temp[current])));
                        if (VS_hex.Checked)
                        {
                            for (int j = 0; j < temp[current].Length / hex; j++)
                            {
                                temp2.Append(Append_Hex(temp[current], j * hex, hex));
                            }
                            var y = (temp[current].Length / hex) * hex;
                            if (y < temp[current].Length)
                            {
                                temp2.Append(Append_Hex(temp[current], y, temp[current].Length - y, hex));
                            }
                            db_Text.Append(temp2);
                        }
                        if (VS_bin.Checked)
                        {
                            for (int j = 0; j < temp[current].Length / bin; j++)
                            {
                                temp2.Append(Append_Bin(temp[current], j * bin, bin));
                            }
                            var y = (temp[current].Length / bin) * bin;
                            if (y < temp[current].Length)
                            {
                                temp2.Append(Append_Bin(temp[current], y, temp[current].Length - y, bin));
                            }
                            db_Text.Append(temp2);
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
        }

        string Append_Hex(byte[] data, int pos, int length, int expected_length = 0)
        {
            string spc = "";
            if (expected_length > 0) for (int j = 0; j < expected_length - length; j++) spc += "   ";
            StringBuilder temp = new StringBuilder();
            temp.Append($"{Hex_Val(data, pos, length)}    ".Replace('-', ' '));
            byte[] temp2 = new byte[length];
            Buffer.BlockCopy(data, pos, temp2, 0, length);
            temp.Append($"{spc}{Encoding.ASCII.GetString(Fix_Stops(temp2))}\n");
            return temp.ToString();
        }

        string Append_Bin(byte[] data, int pos, int length, int expected_length = 0)
        {
            byte[] temp2 = new byte[length];
            Buffer.BlockCopy(data, pos, temp2, 0, length);
            string spc = "";
            if (expected_length > 0) for (int j = 0; j < expected_length - length; j++) spc += "         ";
            StringBuilder temp = new StringBuilder();
            temp.Append($"{Byte_to_Binary(temp2)}     ");
            temp.Append($"{spc}{Encoding.ASCII.GetString(Fix_Stops(temp2))}\n");
            return temp.ToString();
        }

        byte[] Fix_Stops(byte[] data)
        {
            for (int i = 0; i < data.Length; i++) if ((data[i] >= 0 && data[i] <= 31) || data[i] == 95 || data[i] >= 128) data[i] = 0x2e;
            return data;
        }
    }
}