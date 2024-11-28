using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Permissions;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace V_Max_Tool
{
    public partial class Form1 : Form
    {

        void RunBusy(Action action)
        {
            busy = true;
            action();
            busy = false;
        }

        string Get_DirectoryFileType(byte b)
        {
            string fileType = " ";
            if ((b | 0x3f) == 0x3f || (b | 0x3f) == 0x7f) fileType = "*";

            switch (b | 0xf0)
            {
                case 0xf0: fileType += "DEL"; break;
                case 0xf1: fileType += "SEQ"; break;
                case 0xf2: fileType += "PRG"; break;
                case 0xf3: fileType += "USR"; break;
                case 0xf4: fileType += "REL"; break;
                case 0xf8: fileType += "DEL"; break;
                default: fileType += "???"; break;
            }

            if ((b | 0x3f) == 0xff || (b | 0x3f) == 0x7f) fileType += "<";

            return fileType;
        }

        string Get_DirectoryFileName(byte[] file, bool onlyname = false)
        {
            bool eof = false;
            string fName = "\"";
            for (int k = 5; k < 21; k++)
            {
                if (file[k] != 0xa0)
                {
                    if (file[k] != 0x00) fName += Encoding.ASCII.GetString(file, k, 1);
                    else fName += "@";
                }
                else
                {
                    if (!eof) fName += "\"";
                    else fName += " ";
                    eof = true;
                }
            }

            if (!eof) fName += "\"";
            else fName += " ";
            return fName.Replace('?', '-');
        }

        byte[] LZcompress(byte[] inputData)
        {
            if (inputData == null || inputData.Length == 0)
                throw new ArgumentException("Input data cannot be null or empty.");

            byte[] outputData = new byte[(int)(inputData.Length * 1.04f) + 1];
            CPP_Compress();

            return outputData;

            unsafe void CPP_Compress()
            {
                fixed (byte* inPtr = inputData)
                fixed (byte* outPtr = outputData)
                {
                    int compressedSize = 0;
                    if (usecpp && CPP_LZ)
                    {
                        try
                        {
                            compressedSize = NativeMethods.LZ_CompressFast((IntPtr)inPtr, (IntPtr)outPtr, (uint)inputData.Length);
                        }
                        catch { CPP_LZ = false; }
                    }
                    if (!usecpp || !CPP_LZ) compressedSize = LZ_CompressFast(inPtr, outPtr, (uint)inputData.Length);
                    Array.Resize(ref outputData, compressedSize);
                }
            }
        }

        unsafe byte[] LZdecompress(byte[] compressed)
        {
            if (compressed == null || compressed.Length == 0)
                return null;
            uint size = (uint)compressed.Length;
            fixed (byte* inPtr = compressed)
            {
                int outsize = 0;
                if (usecpp && CPP_LZ)
                {
                    try
                    {
                        outsize = NativeMethods.LZ_GetUncompressedSize((IntPtr)inPtr, size);
                    }
                    catch { CPP_LZ = false; }
                }
                if (!usecpp || !CPP_LZ) outsize = GetDecompressedSize(compressed);
                if (outsize > 0)
                {
                    byte[] outputData = new byte[outsize];
                    if (usecpp && CPP_LZ)
                    {
                        try
                        {
                            fixed (byte* outPtr = outputData)
                            {
                                NativeMethods.LZ_Uncompress((IntPtr)inPtr, (IntPtr)outPtr, size);
                            }
                        }
                        catch { CPP_LZ = false; }
                    }
                    if (!usecpp || !CPP_LZ) outputData = LZ_Uncompress(compressed, outsize);
                    return outputData;

                }
                return null;
            }
        }

        void Query_Track_Formats()
        {
            int ldr = 0, vpl = 0, rlk = 0, mps = 0, vmx = 0, cb = 0;
            for (int i = 0; i < tracks; i++) { }
            foreach (var format in NDS.cbm)
            {
                switch (format)
                {
                    case 1: cb++; break;
                    case 2: vmx++; break;
                    case 3: vmx++; break;
                    case 4: ldr++; break;
                    case 5: vpl++; break;
                    case 6: rlk++; break;
                    case 7: rlk++; break;
                    case 10: mps++; break;
                }
            }

            if (ldr > 0) Loader_Track.Text = "Loader Track : Yes"; else Loader_Track.Text = "Loader Track : No";
            CBM_Tracks.Text = $"CBM tracks : {cb}";
            if (NDS.cbm.Any(x => x == 2) || NDS.cbm.Any(x => x == 3)) Protected_Tracks.Text = $"V-Max Tracks : {vmx}";
            if (NDS.cbm.Any(x => x == 5)) Protected_Tracks.Text = $"Vorpal Tracks : {vpl}";
            if (NDS.cbm.Any(x => x == 6)) Protected_Tracks.Text = $"RapidLok Tracks : {rlk}";
            if (NDS.cbm.Any(x => x == 10)) Protected_Tracks.Text = $"MicroPros Tracks : {mps}";
            Protected_Tracks.Visible = (vmx > 0 || vpl > 0 || rlk > 0 || mps > 0);
        }

        void ResetAllBlocks()
        {
            foreach (var row in BlkMap_bam)
            {
                foreach (var button in row)
                {
                    {
                        tips.SetToolTip(button, string.Empty);
                        button.BackColor = Color.FromArgb(30, 100, 100, 100);
                    }
                }
            }
        }

        (bool, bool, bool, bool, bool, bool, bool, bool, bool, bool, bool, bool, int) Set_Adjust_Options(bool rb_vm, bool cynldr = false)
        {
            bool v2a = false, v3a = false, vpa = false, v2adj = false, v2cust = false, v3adj = false, v3cust = false, cbmadj = false;
            bool sl = false, fl = false, vpadj = false;
            int vpl_lead = 0;
            (v2a, v3a, vpa) = Check_Tabs();
            if (NDS.cbm.Any(x => x == 2))
            {
                V2_Auto_Adj.Checked = (v2aa || V2_Auto_Adj.Checked);
                V2_Custom.Checked = (v2cc || V2_Custom.Checked);
                end_track = tracks > 42 ? 75 : 38;
                Invoke(new Action(() =>
                {
                    for (int i = 0; i < tracks; i++)
                    {
                        if (NDS.v2info[i] != null)
                        {
                            if (!V2_swap_headers.Checked && !batch)
                            {
                                V2_swap.DataSource = new string[] { "64-4E (newer)", "64-46 (weak bits)", "4E-64 (alt)" };
                                if (Hex_Val(NDS.v2info[i], 0, 2) == "64-4E") { V2_swap.SelectedIndex = 0; break; }
                                if (Hex_Val(NDS.v2info[i], 0, 2) == "64-46") { V2_swap.SelectedIndex = 1; break; }
                                if (Hex_Val(NDS.v2info[i], 0, 2) == "4E-64") { V2_swap.SelectedIndex = 2; break; }
                            }
                            else
                            {
                                GetNewHeaders();
                                loader_fixed = false;
                                NDG.L_Rot = false;
                                break;
                            }
                        }
                    }
                }));

                if (V3_Auto_Adj.Checked || V3_Custom.Checked)
                {
                    v3aa = V3_Auto_Adj.Checked;
                    v3cc = V3_Custom.Checked;
                }
                //this.Text = $"v3aa {v3aa} v3cc {v3cc} v2aa {v2aa} v2cc {v2cc} b {busy}";
                V3_Auto_Adj.Checked = V3_Custom.Checked = false;
                v2aa = V2_Auto_Adj.Checked;
                v2cc = V2_Custom.Checked;
                v2adj = batch || V2_Auto_Adj.Checked;
                v2cust = V2_Custom.Checked;
                if (v2adj || rb_vm) { fnappend = mod; cbmadj = true; }
                else { fnappend = fix; cbmadj = Adj_cbm.Checked; }
            }
            if (NDS.cbm.Any(x => x == 3))
            {
                V3_Auto_Adj.Checked = (v3aa || V3_Auto_Adj.Checked);
                V3_Custom.Checked = (v3cc || V3_Custom.Checked);
                end_track = tracks > 42 ? 75 : 38;
                //this.Text = $"v3aa {v3aa} v3cc {v3cc} v2aa {v2aa} v2cc {v2cc} b {busy}";
                if (V2_Auto_Adj.Checked || V2_Custom.Checked)
                {
                    v2aa = V2_Auto_Adj.Checked;
                    v2cc = V2_Custom.Checked;
                }
                V2_Auto_Adj.Checked = V2_Custom.Checked = false; // = V2_Add_Sync.Checked = false;
                v3aa = V3_Auto_Adj.Checked;
                v3cc = V3_Custom.Checked;
                v3adj = batch || V3_Auto_Adj.Checked;
                v3cust = V3_Custom.Checked;
                if (v3adj) { fnappend = mod; cbmadj = true; }
                else { fnappend = fix; cbmadj = Adj_cbm.Checked; }
            }
            if (NDS.cbm.Any(x => x == 4))
            {
                fl = ((f_load.Checked || batch || V2_swap_headers.Checked) && !loader_fixed);
                sl = NDS.cbm.Any(x => x == 2) || NDS.cbm.Any(x => x == 3);
            }
            if (NDS.cbm.Any(ss => ss == 5))
            {
                end_track = tracks > 42 ? 69 : 35;
                vpadj = VPL_auto_adj.Checked || batch || VPL_rb.Checked;
                fnappend = (VPL_rb.Checked || Adj_cbm.Checked || vpadj) ? mod : vorp;
                vpl_lead = Lead_ptn.SelectedIndex;
            }
            RL_Fix.Visible = NDS.cbm.Any(x => x == 6) || cynldr;
            if (NDS.cbm.Any(ss => ss == 10))
            {
                end_track = tracks > 42 ? 69 : 35;
            }
            if (Adj_cbm.Checked || v2a || v3a || vpa || batch)
            {
                if (!DB_force.Checked) cbmadj = Check_tlen(); else cbmadj = true;
            }
            return (v2a, v3a, vpa, v2adj, v2cust, v3adj, v3cust, cbmadj, sl, fl, vpadj, rb_vm, vpl_lead);

            (bool, bool, bool) Check_Tabs()
            {
                bool a = (V2_Auto_Adj.Checked && Tabs.TabPages.Contains(Advanced_Opts));
                bool b = (V3_Auto_Adj.Checked && Tabs.TabPages.Contains(Advanced_Opts));
                bool c = (VPL_auto_adj.Checked && Tabs.TabPages.Contains(Advanced_Opts));
                return (a, b, c);
            }

            bool Check_tlen()
            {
                List<int> tl = new List<int>();
                int tr = 0;
                for (int i = 0; i < tracks; i++)
                {
                    tr = tracks > 42 ? i / 2 : i;
                    if (NDS.cbm[i] == 1 && NDS.sectors[i] >= Available_Sectors[tr])
                    {
                        tl.Add(NDS.Track_Length[i]);
                    }
                }
                if (tl.Count > 0) return tl.Max() >> 3 < 8000;
                return false;
            }
        }

        void SwapDensities(bool update = false)
        {
            bool chkd = Density_Range.Checked;
            for (int i = 0; i < 4; i++) density[i] = chkd ? CBM_Standard_Density[i] : ReMaster_Adjusted_Density[i];
            if (tracks > 0 && update)
            {
                Clear_Out_Items();
                Process_Nib_Data(true, false, false, true);
            }
        }
        public static byte[] Compress(byte[] data)
        {
            MemoryStream output = new MemoryStream();
            //using (DeflateStream dstream = new DeflateStream(output, CompressionMode.Compress)) // .net 3.5
            using (DeflateStream dstream = new DeflateStream(output, CompressionLevel.Optimal)) // .net 4.x
            {
                dstream.Write(data, 0, data.Length);
            }
            return output.ToArray();
        }

        public static byte[] Decompress(byte[] data)
        {
            MemoryStream input = new MemoryStream(data);
            MemoryStream output = new MemoryStream();
            using (DeflateStream dstream = new DeflateStream(input, CompressionMode.Decompress))
            {
                dstream.CopyTo(output);
            }
            return output.ToArray();
        }

        (int, int) Longest_Run(byte[] data, byte[] of = null)
        {
            int count = 0;
            int longest = 0;
            int pos = 0;
            byte prev = 0x00;
            if (data != null && data.Length > 0)
            {
                for (int i = 0; i < data.Length; i++)
                {
                    if (of != null)
                    {
                        if (of.Any(x => x == data[i])) count++;
                        else
                        {
                            if (count > longest)
                            {
                                longest = count;
                                pos = i - count;
                            }
                            count = 0;
                        }
                    }
                    else
                    {
                        if (data[i] == prev) count++;
                        else
                        {
                            prev = data[i];
                            if (count > longest)
                            {
                                longest = count;
                                pos = i - count;
                            }
                            count = 0;
                        }
                    }
                }
                return (pos, longest);
            }
            else return (-1, -1);

            //void Set_Counters(int p, int l, int c)
            //{
            //    if (c > l) longest = c;
            //}
        }

        void Data_Viewer(bool stop = false)
        {
            if (Adv_ctrl.Controls[2] == Adv_ctrl.SelectedTab)
            {
                if (!stop)
                {
                    Worker_Alt?.Abort();
                    Worker_Alt = new Thread(new ThreadStart(() => Display_Data()));
                    Worker_Alt.Start();
                    Disp_Data.Text = "Stop";
                }
                else
                {
                    Worker_Alt?.Abort();
                    Disp_Data.Text = "Refresh";
                    busy = false;
                }
            }
        }

        void View_Jump()
        {
            if (Data_Box.Text.Length >= 0)
            {
                Data_Box.Visible = false;
                Data_Box.Select(jt[Convert.ToInt32(T_jump.Value)], 0);
                Data_Box.ScrollToCaret();
                Data_Box.Visible = true;
            }
        }

        void Check_Adv_Opts()
        {
            int[] pgs = new int[] { 2, 3, 5, 6 };
            if (NDS.cbm.Any(s => pgs.Contains(s)))
            {
                ManageTabPage(Advanced_Opts);
                int? visibleControl = NDS.cbm.Contains(2) ? 2 :
                      NDS.cbm.Contains(3) ? 3 :
                      NDS.cbm.Contains(5) ? 5 :
                      NDS.cbm.Contains(6) ? 6 : (int?)null;

                V2_Advanced.Visible = visibleControl == 2;
                V3_Advanced.Visible = visibleControl == 3;
                VPL_Advanced.Visible = visibleControl == 5;
                RPL_Advanced.Visible = visibleControl == 6;

            }
            else Tabs.TabPages.Remove(Advanced_Opts);
            Adj_cbm.Visible = NDS.cbm.Any(s => s == 1);
            VBS_info.Visible = true;
            Reg_info.Visible = true;
            Other_opts.Visible = true;

            void ManageTabPage(TabPage tabPageToAdd, params TabPage[] tabsToRemove)
            {
                if (!Tabs.TabPages.Contains(tabPageToAdd)) Tabs.TabPages.Add(tabPageToAdd);
                foreach (var tab in tabsToRemove) Tabs.TabPages.Remove(tab);
            }
        }

        (string[], string) Populate_File_List(string[] File_List)
        {
            List<string> files = new List<string>();
            foreach (var item in File_List)
            {
                try
                {
                    if (Directory.Exists(item))
                    {
                        var folderFiles = Get(item).Where(file => !Directory.Exists(file) && (Path.GetExtension(file).ToLower() == ".nib" ||
                        Path.GetExtension(file).ToLower() == ".nbz") && (CheckNib(file) || CheckNbz(file)));
                        files.AddRange(folderFiles.Select(file => $@"{Path.GetDirectoryName(file)}\{Path.GetFileName(file)}"));
                    }
                    else
                    {
                        var ext = Path.GetExtension(item).ToLower();
                        if (ext == ".nib")
                        {
                            if (CheckNib(item))
                            {
                                files.Add($@"{Path.GetDirectoryName(item)}\{Path.GetFileName(item)}");
                            }
                        }
                        if (ext == ".nbz")
                        {
                            if (CheckNbz(item))
                            {
                                files.Add($@"{Path.GetDirectoryName(item)}\{Path.GetFileName(item)}");
                            }
                        }
                    }
                }
                catch { }
            }
            busy = false;
            return (files.ToArray(), Directory.GetParent(File_List[0]).ToString());

            bool CheckNib(string file)
            {
                if (File.Exists(file))
                {
                    using (FileStream stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        long length = new FileInfo(file).Length;
                        int ttrks = (int)(length - 256) / 8192;
                        if ((ttrks * 8192) + 256 == length)
                        {
                            byte[] nhead = new byte[256];
                            stream.Seek(0, SeekOrigin.Begin);
                            stream.Read(nhead, 0, 256);
                            string head = Encoding.ASCII.GetString(nhead, 0, 13);
                            return head == "MNIB-1541-RAW";
                        }
                    }
                }
                return false;
            }

            bool CheckNbz(string file)
            {
                if (File.Exists(file))
                {
                    using (FileStream stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        long length = new FileInfo(file).Length;
                        byte[] compressed = new byte[length];
                        stream.Seek(0, SeekOrigin.Begin);
                        stream.Read(compressed, 0, (int)length);
                        stream.Close();
                        byte[] decomp = LZdecompress(compressed);
                        if (decomp != null)
                        {
                            int d_size = decomp.Length;
                            int ttrks = (d_size - 256) / 8192;
                            if ((ttrks * 8192) + 256 == d_size)
                            {
                                byte[] nhead = new byte[256];
                                Buffer.BlockCopy(decomp, 0, nhead, 0, 256);
                                string head = Encoding.ASCII.GetString(nhead, 0, 13);
                                return head == "MNIB-1541-RAW";
                            }
                        }
                    }
                }
                return false;
            }
        }

        public IEnumerable<string> Get(string path)
        {
            IEnumerable<string> files = Enumerable.Empty<string>();
            IEnumerable<string> directories = Enumerable.Empty<string>();
            try
            {
                var permission = new FileIOPermission(FileIOPermissionAccess.Read, path);
                permission.Demand();
                files = Directory.GetFiles(path);
                directories = Directory.GetDirectories(path);
            }
            catch
            {
                path = null;
            }

            if (path != null)
            {
                yield return path;
            }

            foreach (var file in files)
            {
                yield return file;
            }
            var subdirectoryItems = directories.SelectMany(Get);
            foreach (var result in subdirectoryItems)
            {
                yield return result;
            }
        }

        void Out_Density_Color(object sender, DrawItemEventArgs e)
        {
            try
            {
                if (Out_density.Items[e.Index] is LineColor item)
                {
                    e.Graphics.DrawString(
                        item.Text,
                        e.Font,
                        new SolidBrush(item.Color),
                    e.Bounds);
                }
            }
            catch { }
        }

        void Out_Track_Color(object sender, DrawItemEventArgs e)
        {
            try
            {
                if (out_track.Items[e.Index] is LineColor item)
                {
                    e.Graphics.DrawString(
                        item.Text,
                        e.Font,
                        new SolidBrush(item.Color),
                    e.Bounds);
                }
            }
            catch { }
        }

        void Source_Info_Color(object sender, DrawItemEventArgs e)
        {
            try
            {
                if (Track_Info.Items[e.Index] is LineColor item)
                {
                    e.Graphics.DrawString(
                        item.Text,
                        e.Font,
                        new SolidBrush(item.Color),
                    e.Bounds);
                }
            }
            catch { }
        }

        void Track_Format_Color(object sender, DrawItemEventArgs e)
        {
            try
            {
                if (sf.Items[e.Index] is LineColor item)
                {
                    e.Graphics.DrawString(
                        item.Text,
                        e.Font,
                        new SolidBrush(item.Color),
                    e.Bounds);
                }
            }
            catch { }
        }

        void RPM_Color(object sender, DrawItemEventArgs e)
        {
            try
            {
                if (out_rpm.Items[e.Index] is LineColor item)
                {
                    e.Graphics.DrawString(
                        item.Text,
                        e.Font,
                        new SolidBrush(item.Color),
                    e.Bounds);
                }
            }
            catch { }
        }

        byte[] XOR(byte[] data, byte value)
        {
            for (int i = 0; i < data.Length; i++) data[i] ^= value;
            return data;
        }

        byte[] Rotate_Right(byte[] data, int pos)
        {
            if (pos > 0 && pos < data.Length)
            {
                pos -= 1;
                int length = data.Length;
                pos %= length; // Ensure pos is within array length
                byte[] temp = new byte[pos];
                Array.Copy(data, length - pos, temp, 0, pos); // Copy last 'pos' elements to temp
                Buffer.BlockCopy(data, 0, data, pos, length - pos); // Shift remaining elements to right
                Buffer.BlockCopy(temp, 0, data, 0, pos); // Copy temp to start of array
            }
            return data;
        }

        byte[] Rotate_Left(byte[] data, int pos)
        {
            if (pos > 0 && pos < data.Length)
            {
                int length = data.Length;
                pos %= length; // Ensure pos is within array length
                byte[] temp = new byte[pos];
                Array.Copy(data, temp, pos); // Copy first 'pos' elements to temp
                Buffer.BlockCopy(data, pos, data, 0, length - pos); // Shift remaining elements to left
                Buffer.BlockCopy(temp, 0, data, length - pos, pos); // Copy temp to end of array
            }
            return data;
        }

        byte[] CopyFrom(byte[] Source, int Pos = 0, int Length = 0)
        {
            if (Source == null || Source.Length == 0 || Pos > Source.Length - 1) return null;
            Length = (Length > 0 && Pos + Length <= Source.Length - 1) ? Length : Source.Length - Pos - 1;
            byte[] dest = new byte[Length];
            try
            {
                Buffer.BlockCopy(Source, Pos, dest, 0, Length);
            }
            catch { return null; }
            return dest;
        }

        public static byte[] ArrayConcat(params byte[][] arrays)
        {
            int totalLength = arrays.Sum(a => a.Length);
            byte[] result = new byte[totalLength];

            int offset = 0;
            foreach (byte[] array in arrays)
            {
                Buffer.BlockCopy(array, 0, result, offset, array.Length);
                offset += array.Length;
            }

            return result;
        }

        string Hex_Val(byte[] data, int start = 0, int end = -1)
        {
            if (data != null)
            {
                if (end == -1) end = data.Length;
                return BitConverter.ToString(data, start, end);
            }
            else return string.Empty;
        }

        int Get_Weak_Bytes(byte[] data)
        {
            if (data == null) return -1;
            int weak = 0;
            for (int i = 0; i < data.Length; i++)
            {
                if (ToBinary(Encoding.ASCII.GetString(data, i, 1)).Contains("000")) weak++;
            }
            return weak;
        }

        (byte[], int, int) GetSectorWithErrorCode(byte[] data, int sector, bool decode, byte[] ID = null, BitArray source = null, int position = 0)
        {
            source = source ?? new BitArray(Flip_Endian(data));
            ID = ID ?? GetDiskID(true);
            var error = 1;
            (bool valid, int pos, byte[] id, bool hdr_cksm) = Find_Sector(source, sector, position, true);
            if (valid)
            {
                (byte[] sec_data, bool chksum) = Decode_CBM_Sector(data, sector, true, source, pos);
                error = !chksum ? 5 : error;
                error = (sec_data == null || sec_data?.Length != 256) ? 4 : error;
                if (ID != null) error = (!Match(ID, id)) ? 11 : error;
                /* if Decode is set to true, Send back the un-altered sector data from the track */
                if (!decode) (sec_data, _) = Decode_CBM_Sector(data, sector, false, source, pos);
                return (sec_data, error, pos);
            }
            else error = 2;
            error = (!hdr_cksm) ? 9 : error;
            return (null, error, -1);
        }

        byte[] GetDiskID(bool reverse = false)
        {
            int dirtrack = tracks > 42 ? 34 : 17;
            if (NDS.cbm[dirtrack] == 1 && NDS.Track_Data[dirtrack] != null)
            {
                var temp = new BitArray(Flip_Endian(NDS.Track_Data[dirtrack]));
                (_, _, var dID, _) = Find_Sector(temp, 0);
                if (dID != null)
                {
                    var ID = new byte[2];
                    ID[0] = reverse ? dID[0] : dID[1];
                    ID[1] = reverse ? dID[1] : dID[0];
                    return ID;
                }
            }
            return null;
        }

        byte[] Remove_Weak_Bits(byte[] data, bool aggressive = false)
        {
            if (data == null) return null;
            HashSet<byte> blankSet = new HashSet<byte>(blank);
            for (int i = 0; i < data.Length; i++)
            {
                if (blankSet.Contains(data[i]))
                {
                    if (aggressive && i + 5 <= data.Length)
                    {
                        data[i] = 0x00;
                        data[i + 1] = 0x00;
                        data[i + 2] = 0x00;
                        data[i + 3] = 0x00;
                        data[i + 4] = 0x00;
                        i += 4;
                    }
                    else data[i] = 0x00; // Zero out the byte
                }
            }
            return data;
        }
        /// -------------------   Bit Operation functions   ---------------------------------------------------------------------------------

        byte[] Bit2Byte(BitArray bits, int start = 0, int length = -1)
        {
            if (length < 0) length = bits.Length - start;

            if (start >= 0 && length > 0 && start + length <= bits.Length)
            {
                int byteLength = (length - 1) / 8 + 1;
                byte[] ret = new byte[byteLength];
                for (int i = 0; i < length; i++)
                {
                    if (bits[start + i])
                    {
                        int index = i / 8;
                        int bitOffset = i % 8;
                        ret[index] |= (byte)(1 << (7 - bitOffset));
                    }
                }
                return (ret);
            }
            else return new byte[0];
        }

        BitArray BitCopy(BitArray bits, int start = 0, int length = -1)
        {
            if (length < 0) length = bits.Length - start;
            if (start >= 0 && length != -1 && start + length <= bits.Length)
            {
                BitArray temp = new BitArray(length);
                for (int i = 0; i < length; i++) temp[i] = bits[start + i];
                return temp;
            }
            else return bits;
        }

        public static string ToBinary(string data)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char c in data.ToCharArray()) sb.Append(Convert.ToString(c, 2).PadLeft(8, '0'));
            return sb.ToString();
        }

        public String Byte_to_Binary(Byte[] data)  // (use this for .NET 3.5 build) Note: only 100ms longer
        {
            BitArray bits = new BitArray(Flip_Endian(data));
            StringBuilder b = new StringBuilder();
            for (int counter = 0; counter < bits.Length; counter++)
            {
                b.Append((bits[counter] ? "1" : "0"));
                if ((counter + 1) % 8 == 0)
                    b.Append(" ");
            }
            return b.ToString();
        }

        byte SetBit(byte data, int bitPosition)
        {
            return (byte)(data | (1 << bitPosition));
        }

        byte ClearBit(byte data, int bitPosition)
        {
            return (byte)(data & ~(1 << bitPosition));
        }

        byte ToggleBit(byte data, int bitPosition)
        {
            return (byte)(data ^ (1 << bitPosition));
        }

        bool GetBitStatus(byte value, int bitPosition)
        {
            byte mask = (byte)(1 << bitPosition);
            return (value & mask) != 0;
        }

        void Pad_Bits(int position, int count, BitArray bitarray)
        {
            bool flip = !bitarray[position];
            for (int i = position; i < position + count; i++)
            {
                flip = !flip;
                bitarray[i] = flip;
            }
        }

        int VPL_Density(int len)
        {
            if (len >= 7500) return 0;
            if (len >= 6700) return 1;
            if (len >= 6300) return 2;
            return 3;
        }

        int Get_Density(int len)
        {
            if (len >= 7500) return 0;
            if (len >= 6850) return 1;
            if (len >= 6400) return 2;
            return 3;
        }

        byte[] Flip_Endian(byte[] data)
        {
            if (data == null) return null;
            int length = data.Length;
            byte[] temp = new byte[length];
            for (int i = 0; i < length; i++) temp[i] = Reverse_Endian_Table[data[i]];
            return temp;
        }

        int Find_Most_Frequent_Format(int[] array)
        {
            if (array.Length == 0) return -1;// throw new ArgumentException("Array is empty");
            Dictionary<int, int> counts = new Dictionary<int, int>();
            foreach (int num in array)
            {
                if (num > 1 && counts.ContainsKey(num)) counts[num]++;
                else counts[num] = 1;
            }
            int mostFrequent = array[0];
            int maxCount = counts[mostFrequent];
            foreach (var pair in counts)
            {
                if (pair.Value > maxCount)
                {
                    mostFrequent = pair.Key;
                    maxCount = pair.Value;
                }
            }
            return mostFrequent;
        }

        bool Check_Version(string find, byte[] sdat, int clen)
        {
            int i;
            for (i = 0; i < sdat.Length - find.Length; i++)
            {
                if (Hex_Val(sdat, i, clen) == find) return (true);
            }
            return (false);
        }

        static bool Match(byte[] expecting, byte[] have)
        {
            return expecting.SequenceEqual(have);
        }

        (bool, int) Find_Data(byte[] find, byte[] data, int start_pos = -1)
        {
            try
            {
                start_pos = start_pos < 0 ? 0 : start_pos;
                for (int i = start_pos; i <= data.Length - find.Length; i++)
                {
                    bool Match = true;
                    for (int j = 0; j < find.Length; j++)
                    {
                        if (data[i + j] != find[j])
                        {
                            Match = false;
                            break;
                        }
                    }
                    if (Match) return (true, i);
                }
            }
            catch { }
            return (false, 0);
        }

        byte[] Hex2Byte(string hex)
        {
            hex = hex.Replace("-", "");
            if (hex.Length % 2 != 1)
            {
                byte[] tmp = new byte[hex.Length >> 1];
                for (int i = 0; i < hex.Length >> 1; ++i) tmp[i] = (byte)((Val(hex[i << 1]) << 4) + (Val(hex[(i << 1) + 1])));
                return tmp;
            }
            return new byte[0];

            int Val(char current)
            {
                int val = (int)current;
                return val - (val < 58 ? 48 : 55);
            }
        }

        void Disable_Core_Controls(bool disable)
        {
            DB_core_override.Enabled = !disable;
            if (DB_cores.Enabled && disable) DB_cores.Enabled = !disable;
            if (!disable) DB_cores.Enabled = DB_core_override.Checked;
        }

        byte[] Shrink_Track(byte[] data, int trk_density)
        {
            byte[] temp;
            if (data.Length > density[trk_density])
            {
                int start = 0;
                int longest = 0;
                int count = 0;
                for (int i = 1; i < data.Length; i++)
                {
                    if (data[i] != data[i - 1]) count = 0;
                    count++;
                    if (count > longest)
                    {
                        start = (i + 1) - count;
                        longest = count;
                    }
                }
                temp = new byte[density[trk_density]];
                int shrink = data.Length - temp.Length;
                try
                {
                    Buffer.BlockCopy(data, 0, temp, 0, start);
                    Buffer.BlockCopy(data, start + shrink, temp, start, temp.Length - start);
                }
                catch { return data; }
            }
            else temp = data;
            return temp;
        }

        byte[] Lengthen_Track(byte[] data, int Density = 1) // For track 18 on Vorpal images
        {
            if (data.Length > 0)
            {
                byte[] temp = new byte[density[Density]];
                int current = 0;
                int longest = 0;
                int pos = 0;
                byte fill = 0x55;
                int a = temp.Length - data.Length;
                for (int i = 0; i < data.Length; i++)
                {
                    if (data[i] == 0x55 || data[i] == 0xaa) current++;
                    else
                    {
                        if (current > longest)
                        {
                            pos = i - 1;
                            longest = current;
                            fill = data[i - 1];
                        }
                        current = 0;
                    }
                }
                Buffer.BlockCopy(data, 0, temp, 0, pos);
                for (int i = pos; i < pos + a; i++) temp[i] = fill;
                Buffer.BlockCopy(data, pos, temp, pos + a, data.Length - pos);
                return temp;
            }
            else return data;
        }

        void Set_Dest_Arrays(byte[] data, int trk)
        {
            try
            {
                NDG.Track_Data[trk] = new byte[data.Length];
                NDA.Track_Data[trk] = new byte[8192];
                Buffer.BlockCopy(data, 0, NDG.Track_Data[trk], 0, data.Length);
                Buffer.BlockCopy(data, 0, NDA.Track_Data[trk], 0, data.Length);
                Buffer.BlockCopy(data, 0, NDA.Track_Data[trk], data.Length, 8192 - data.Length);
                NDA.Track_Length[trk] = data.Length << 3;
                NDG.Track_Length[trk] = data.Length;
            }
            catch { }
        }

        byte[] Create_Empty_Sector(byte fill = 0x01)
        {
            int chksum = 0;
            var buff = new MemoryStream();
            var wrt = new BinaryWriter(buff);
            wrt.Write((byte)0x07);
            wrt.Write((byte)0x4b);
            chksum ^= 0x4b;
            while (buff.Length < 260)
            {
                if (buff.Length < 257)
                {
                    wrt.Write((byte)fill);
                    chksum ^= 1;
                }
                if (buff.Length == 257) wrt.Write((byte)chksum);
                if (buff.Length > 257) wrt.Write(((byte)0x00));
            }
            return buff.ToArray();
        }

        byte[] SetSectorGap(int len)
        {
            byte[] gap = FastArray.Init(len, cbm_gap);
            if (cbm_gap == 0x55) gap[gap.Length - 1] = 0x56;
            return gap;
            //byte[] gap = new byte[len];
            //gap = Encode_CBM_GCR(gap);
            //return gap;
        }

        byte[] Decode_CBM_GCR(byte[] gcr)
        {
            byte[] plain = new byte[(gcr.Length / 5) << 2];
            for (int i = 0; i < gcr.Length / 5; i++)
            {
                int baseIndex = i * 5;
                byte b1 = gcr[baseIndex];
                byte b2 = gcr[baseIndex + 1];
                plain[(i << 2) + 0] = CombineNibbles((byte)(b1 >> 3), (byte)(((b1 << 2) | (b2 >> 6)) & 0x1f));
                b1 = gcr[baseIndex + 1];
                b2 = gcr[baseIndex + 2];
                plain[(i << 2) + 1] = CombineNibbles((byte)((b1 >> 1) & 0x1f), (byte)(((b1 << 4) | (b2 >> 4)) & 0x1f));
                b1 = gcr[baseIndex + 2];
                b2 = gcr[baseIndex + 3];
                plain[(i << 2) + 2] = CombineNibbles((byte)(((b1 << 1) | (b2 >> 7)) & 0x1f), (byte)((b2 >> 2) & 0x1f));
                b1 = gcr[baseIndex + 3];
                b2 = gcr[baseIndex + 4];
                plain[(i << 2) + 3] = CombineNibbles((byte)(((b1 << 3) | (b2 >> 5)) & 0x1f), (byte)(b2 & 0x1f));
            }
            return plain;

            byte CombineNibbles(byte hnib, byte lnib)
            {
                hnib = GCR_decode_high[hnib];
                lnib = GCR_decode_low[lnib];
                if (hnib == 0xff || lnib == 0xff) return 0x00;
                else return (byte)(hnib | lnib);
            }
        }

        byte[] Encode_CBM_GCR(byte[] plain)
        {
            int l = plain.Length >> 2;
            byte[] gcr = new byte[l * 5];
            for (int i = 0; i < l; i++)
            {
                int baseIndex = i << 2;
                byte p1 = plain[baseIndex];
                byte p2 = plain[baseIndex + 1];
                byte p3 = plain[baseIndex + 2];
                byte p4 = plain[baseIndex + 3];
                gcr[0 + (i * 5)] = (byte)((GCR_encode[p1 >> 4] << 3) | (GCR_encode[p1 & 0x0f] >> 2));
                gcr[1 + (i * 5)] = (byte)((GCR_encode[p1 & 0x0f] << 6) | (GCR_encode[p2 >> 4] << 1) | (GCR_encode[p2 & 0x0f] >> 4));
                gcr[2 + (i * 5)] = (byte)((GCR_encode[p2 & 0x0f] << 4) | (GCR_encode[p3 >> 4] >> 1));
                gcr[3 + (i * 5)] = (byte)((GCR_encode[p3 >> 4] << 7) | (GCR_encode[p3 & 0x0f] << 2) | (GCR_encode[p4 >> 4] >> 3));
                gcr[4 + (i * 5)] = (byte)((GCR_encode[p4 >> 4] << 5) | GCR_encode[p4 & 0x0f]);
            }
            return gcr;
        }

        byte[] Decode_Vorpal_GCR(byte[] gcr)
        {
            byte[] plain = new byte[(gcr.Length / 5) << 2];
            for (int i = 0; i < gcr.Length / 5; i++)
            {
                int baseIndex = i * 5;
                byte b1 = gcr[baseIndex];
                byte b2 = gcr[baseIndex + 1];
                plain[(i << 2) + 0] = CombineNibbles(VPL_decode_high[b1 >> 3], VPL_decode_low[((b1 << 2) | (b2 >> 6)) & 0x1f]);
                b1 = gcr[baseIndex + 1];
                b2 = gcr[baseIndex + 2];
                plain[(i << 2) + 1] = CombineNibbles(VPL_decode_high[(b1 >> 1) & 0x1f], VPL_decode_low[((b1 << 4) | (b2 >> 4)) & 0x1f]);
                b1 = gcr[baseIndex + 2];
                b2 = gcr[baseIndex + 3];
                plain[(i << 2) + 2] = CombineNibbles(VPL_decode_high[((b1 << 1) | (b2 >> 7)) & 0x1f], VPL_decode_low[(b2 >> 2) & 0x1f]);
                b1 = gcr[baseIndex + 3];
                b2 = gcr[baseIndex + 4];
                plain[(i << 2) + 3] = CombineNibbles(VPL_decode_high[((b1 << 3) | (b2 >> 5)) & 0x1f], VPL_decode_low[b2 & 0x1f]);
            }
            return plain;

            byte CombineNibbles(byte highNibble, byte lowNibble)
            {
                if (highNibble == 0xff || lowNibble == 0xff) return 0x00;
                else return (byte)(highNibble | lowNibble);
            }
        }

        byte[] Build_BlockHeader(int track, int sector, byte[] ID, bool badChecksum = false, bool ID_Mismatch = false)
        {
            byte[] header = new byte[8];
            header[0] = 0x08;
            header[2] = (byte)sector;
            header[3] = (byte)track;
            Buffer.BlockCopy(ID, 0, header, 4, 4);
            if (ID_Mismatch)
            {
                byte h4 = 0;
                byte h5 = 0;
                for (int i = 0; i < 8; i++)
                {
                    h4 = ToggleBit(header[4], i);
                    h5 = ToggleBit(header[5], i);
                }
                header[4] = h4;
                header[5] = h5;
            }
            for (int i = 2; i < 6; i++) header[1] ^= header[i];
            if (badChecksum) header[1] = Flip_Endian(new byte[] { header[1] })[0];
            return Encode_CBM_GCR(header);
        }

        void Shrink_Short_Sector(int trk)
        {
            if (Original.OT[trk].Length == 0)
            {
                Original.OT[trk] = new byte[NDG.Track_Data[trk].Length];
                Buffer.BlockCopy(NDG.Track_Data[trk], 0, Original.OT[trk], 0, NDG.Track_Data[trk].Length);
            }
            int d = Get_Density(NDG.Track_Data[trk].Length);
            byte[] temp = Shrink_Track(NDG.Track_Data[trk], d);
            if (temp.Length > density[d])
            {
                string pattern;
                string current = "";
                int start = 0;
                int run = 0;
                int cur = 0;
                for (int i = 0; i < NDG.Track_Data[trk].Length - 1; i++)
                {
                    pattern = Hex_Val(NDG.Track_Data[trk], i, 2);
                    if (pattern == current)
                    {
                        cur++;
                        if (cur > run)
                        {
                            run = cur;
                            start = (i - (run * 2));
                        }
                    }
                    else
                    {
                        cur = 0;
                        current = pattern;
                    }
                    i++;
                }
                temp = new byte[density[d]];
                int skip = NDG.Track_Data[trk].Length - density[d];
                Buffer.BlockCopy(NDG.Track_Data[trk], 0, temp, 0, start);
                Buffer.BlockCopy(NDG.Track_Data[trk], start + skip, temp, start, (temp.Length - 1) - start);
            }
            Set_Dest_Arrays(temp, trk);
        }

        Color ApplyDivisionModifier(Color color, float modifier)
        {
            int newR = (int)(color.R / modifier);
            int newG = (int)(color.G / modifier);
            int newB = (int)(color.B / modifier);
            int newA = (int)(color.A); // Include this if you want to modify the alpha channel as well

            // Ensure the new color components are within the valid range (0-255)
            newR = Math.Max(0, Math.Min(255, newR));
            newG = Math.Max(0, Math.Min(255, newG));
            newB = Math.Max(0, Math.Min(255, newB));
            newA = Math.Max(0, Math.Min(255, newA));

            return Color.FromArgb(newA, newR, newG, newB); // Use newA for alpha, or 255 if you don't modify alpha
        }

        void Check_Before_Draw(bool dontDrawFlat, bool timeout = false)
        {
            if (Adv_ctrl.SelectedTab == Adv_ctrl.TabPages["tabPage2"])
            {
                if (!batch)
                {
                    RunBusy(() =>
                    {
                        Draw?.Abort();
                        circ?.Abort();
                        flat?.Abort();
                        check_alive?.Abort();
                        flat?.Join();
                        try
                        {
                            if (!dontDrawFlat)
                            {
                                flat_large?.Dispose();
                                flat = new Thread(new ThreadStart(() => Draw_Flat_Tracks(false, (Cores < 3))));
                                flat.Start();
                            }
                            circle?.Dispose();
                            circ = new Thread(new ThreadStart(() => Draw_Circular_Tracks((Cores < 3))));
                            circ.Start();
                        }
                        catch { }
                        drawn = true;
                        GC.Collect();
                        Draw = new Thread(new ThreadStart(() => Progress_Thread_Check((Cores < 3))));
                        Draw.Start();
                    });
                }
            }
        }
    }
}