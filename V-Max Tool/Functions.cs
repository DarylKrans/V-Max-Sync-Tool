using ReMaster_Utility.Properties;
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
        private Thread Draw;
        private Thread circ;  // Thread for drawing circle disk image
        private Thread flat;  // Thread for drawing flat tracks image
        private Thread check_alive;
        private int pan_defw;
        private int pan_defh;
        private bool manualRender;
        private readonly Gbox outbox = new Gbox();
        private readonly Gbox inbox = new Gbox();
        private readonly Color C64_screen = Color.FromArgb(69, 55, 176);   //(44, 41, 213);
        private readonly Color c64_text = Color.FromArgb(135, 122, 237);   //(114, 110, 255); 
        private string def_bg_text;
        private bool Out_Type = true;
        private readonly string dir_def = "0 \"DRAG NIB/G64 TO \"START\n664 BLOCKS FREE.";

        private readonly byte[] sector_gap_length = {
                10, 10, 10, 10, 10, 10, 10, 10, 10, 10,	/*  1 - 10 */
            	10, 10, 10, 10, 10, 10, 10, 14, 14, 14,	/* 11 - 20 */
            	14, 14, 14, 14, 11, 11, 11, 11, 11, 11,	/* 21 - 30 */
            	8, 8, 8, 8, 8,						/* 31 - 35 */
            	8, 8, 8, 8, 8, 8, 8				/* 36 - 42 (non-standard) */
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
                0xff, 0xff, 0xff, 0xff, 0xff, 0x06, 0x0f, 0xff,
                //0xff, 0xff, 0xff, 0xff, 0xff, 0x0e, 0x0f, 0xff,
                0xff, 0x00, 0x01, 0x02, 0x05, 0x03, 0x04, 0x05,
                0xff, 0xff, 0x06, 0x07, 0x0a, 0x08, 0x09, 0x0a,
                0xff, 0x0b, 0x0c, 0x0d, 0xff, 0x0e, 0x0f, 0xff,
            };

        private readonly byte[] VPL_decode_high =
            {
                0xff, 0xff, 0xff, 0xff, 0xff, 0x60, 0xf0, 0xff,
                //0xff, 0xff, 0xff, 0xff, 0xff, 0xe0, 0xf0, 0xff,
                0xff, 0x00, 0x10, 0x20, 0x50, 0x30, 0x40, 0x50,
                0xff, 0xff, 0x60, 0x70, 0xa0, 0x80, 0x90, 0xa0,
                0xff, 0xb0, 0xc0, 0xd0, 0xff, 0xe0, 0x70, 0xff,
                //0xff, 0xb0, 0xc0, 0xd0, 0xff, 0xe0, 0xf0, 0xff,
            };

        void Reset_to_Defaults()
        {
            busy = true;
            Img_Q.SelectedIndex = 2;
            Set_ListBox_Items(true, true);
            Import_File.Visible = f_load.Visible = false;
            Tabs.Controls.Remove(Adv_V3_Opts);
            Tabs.Controls.Remove(Adv_V2_Opts);
            Tabs.Controls.Remove(Vpl_adv);
            Img_style.Enabled = Img_View.Enabled = Img_opts.Enabled = Save_Circle_btn.Visible = M_render.Visible = Adv_ctrl.Enabled = false;
            VBS_info.Visible = Reg_info.Visible = false;
            Other_opts.Visible = false;
            Save_Disk.Visible = false;
            Adv_ctrl.SelectedIndex = 0;
            linkLabel1.Visible = true;
            Draw_Init_Img(def_bg_text);
            Data_Box.Clear();
            Default_Dir_Screen();
            busy = false;
        }

        void Default_Dir_Screen()
        {
            Dir_screen.Clear();
            Dir_screen.Text = dir_def;
            Dir_screen.Select(2, 23);
            Dir_screen.SelectionBackColor = c64_text;
            Dir_screen.SelectionColor = C64_screen;
        }

        void Set_Auto_Opts()
        {
            if (Auto_Adjust)
            {
                V3_Auto_Adj.Checked = V2_Auto_Adj.Checked = VPL_auto_adj.Checked = f_load.Checked = true;
            }
        }

        void Set_Arrays(int len)
        {
            // NDS is the input or source array
            NDS.Track_Data = new byte[len][];
            NDS.Sector_Zero = new int[len];
            NDS.Track_Length = new int[len];
            NDS.D_Start = new int[len];
            NDS.D_End = new int[len];
            NDS.cbm = new int[len];
            NDS.sectors = new int[len];
            NDS.sector_pos = new int[len][];
            NDS.Header_Len = new int[len];
            NDS.cbm_sector = new int[len][];
            NDS.v2info = new byte[len][];
            NDS.Loader = new byte[0];
            NDS.Total_Sync = new int[len];
            NDS.Disk_ID = new byte[len][];
            NDS.Gap_Sector = new int[len];
            // NDA is the destination or output array
            NDA.Track_Data = new byte[len][];
            NDA.Sector_Zero = new int[len];
            NDA.Track_Length = new int[len];
            NDA.D_Start = new int[len];
            NDA.D_End = new int[len];
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

        void Data_Viewer()
        {
            if (Adv_ctrl.Controls[2] == Adv_ctrl.SelectedTab)
            {
                if (!busy)
                {
                    w = new Thread(new ThreadStart(() => Display_Data()));
                    w.Start();
                    Disp_Data.Text = "Stop";
                }
                else
                {
                    w?.Abort();
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
            if (NDS.cbm.Any(s => s == 2))
            {
                if (!Tabs.TabPages.Contains(Adv_V2_Opts))
                {
                    Tabs.Controls.Add(Adv_V2_Opts);
                }
                Tabs.Controls.Remove(Vpl_adv);
                Tabs.Controls.Remove(Adv_V3_Opts);
            }
            else Tabs.Controls.Remove(Adv_V2_Opts);
            if (NDS.cbm.Any(s => s == 3))
            {
                if (!Tabs.TabPages.Contains(Adv_V3_Opts))
                {
                    Tabs.Controls.Add(Adv_V3_Opts);
                }
                Tabs.Controls.Remove(Adv_V2_Opts);
                Tabs.Controls.Remove(Vpl_adv);
            }
            else Tabs.Controls.Remove(Adv_V3_Opts);
            if (NDS.cbm.Any(s => s == 5))
            {
                if (!Tabs.TabPages.Contains(Vpl_adv))
                {
                    Tabs.Controls.Add(Vpl_adv);
                }
                Tabs.Controls.Remove(Adv_V2_Opts);
                Tabs.Controls.Remove(Adv_V3_Opts);
            }
            else Tabs.Controls.Remove(Vpl_adv);
            if (NDS.cbm.Any(s => s == 1)) Adj_cbm.Visible = true; else Adj_cbm.Visible = false;
            VBS_info.Visible = Reg_info.Visible = Other_opts.Visible = true;
        }

        (string[], string) Populate_File_List(string[] File_List)
        {
            List<string> files = new List<string>();
            for (int r = 0; r < File_List.Length; r++)
            {
                try
                {
                    if (!Directory.Exists(File_List[r]))
                    {
                        if (Path.GetExtension(File_List[r]).ToLower() == ".nib")
                            if (CheckFile(File_List[r]))
                            {
                                files.Add($@"{System.IO.Path.GetDirectoryName(File_List[r])}\{System.IO.Path.GetFileName(File_List[r])}");
                            }
                    }
                    else
                    {
                        var Folder_files = Get(File_List[r]).ToArray();
                        for (int s = 0; s < Folder_files.Length; s++)
                        {
                            if (!Directory.Exists(Folder_files[s])) if (CheckFile(Folder_files[s]))
                                {
                                    files.Add($@"{System.IO.Path.GetDirectoryName(Folder_files[s])}\{System.IO.Path.GetFileName(Folder_files[s])}");
                                }
                        }
                    }
                }
                catch { }
            }
            return (files.ToArray(), Directory.GetParent(File_List[0]).ToString());

            bool CheckFile(string file)
            {
                if (File.Exists(file))
                {
                    FileStream Stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    long length = new System.IO.FileInfo(file).Length;
                    int ttrks = (int)(length - 256) / 8192;
                    if ((ttrks * 8192) + 256 == length)
                    {
                        byte[] nhead = new byte[256];
                        Stream.Seek(0, SeekOrigin.Begin);
                        Stream.Read(nhead, 0, 256);
                        Stream.Close();
                        var head = Encoding.ASCII.GetString(nhead, 0, 13);
                        if (head == "MNIB-1541-RAW") return true;
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

        byte[] Rotate_Left(byte[] data, int s)
        {
            s -= 1;
            return data.Skip(s).Concat(data.Take(s)).ToArray();
        }

        byte[] Rotate_Right(byte[] data, int s)
        {
            s -= 1;
            return data.Skip(data.Length - s).Concat(data.Take(data.Length - s)).ToArray();
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

        byte[] Bit2Byte(BitArray bits, int start = 0, int length = -1)
        {
            BitArray temp = new BitArray(bits);
            if (length < 0) length = bits.Length - start;
            if (start >= 0 && length != -1 && start + length <= bits.Length)
            {
                temp = new BitArray(length);
                for (int i = 0; i < length; i++) temp[i] = bits[start + i];
            }
            byte[] ret = new byte[((temp.Count - 1) / 8) + 1];
            temp.CopyTo(ret, 0);
            return ret;
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
            string b = "";
            for (int counter = 0; counter < bits.Length; counter++)
            {
                b += (bits[counter] ? "1" : "0");
                if ((counter + 1) % 8 == 0)
                    b += " ";
            }
            return b;
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
            int i = 0;
            if (len >= 7500) i = 0;
            if (len >= 6700 && len < 7500) i = 1;
            if (len >= 6300 && len < 6700) i = 2;
            if (len >= 6000 && len < 6300) i = 3;
            return i;
        }

        int Get_Density(int len)
        {
            int i = 0;
            if (len >= 7500) i = 0;
            if (len >= 6850 && len < 7500) i = 1;
            if (len >= 6400 && len < 6850) i = 2;
            if (len >= 6000 && len < 6400) i = 3;
            return i;
        }

        BitArray Flip_Bits(BitArray bits)
        {
            BitArray f = new BitArray(bits);
            for (int i = 0; i < bits.Count; i++)
            {
                f[i] = bits[7 - i];
            }
            return f;
        }

        byte[] Flip_Endian(byte[] data)
        {
            byte[] n = new byte[data.Length];
            for (int i = 0; i < data.Length; i++)
            {
                byte[] l = new byte[1];
                l[0] = data[i];
                BitArray t = Flip_Bits(new BitArray(l));
                t.CopyTo(n, i);
            }
            return n;
        }

        //bool Find_vpl_sec(BitArray source, int pos)
        //{
        //    int snc = 0;
        //    bool sncc = false;
        //    for (int j = pos; j < (pos + 1600); j++)
        //    {
        //        if (source[j])
        //        {
        //            snc++;
        //            if (snc == 8) sncc = true;
        //        }
        //        else
        //        {
        //            if (sncc)
        //            {
        //                if (snc == 8)
        //                {
        //                    bool flip = false;
        //                    for (int k = j; k < (j + 7); k++)
        //                    {
        //                        if (!source[k] == flip) { return false; }
        //                        flip = !flip;
        //                    }
        //                    return true;
        //                }
        //            }
        //            sncc = false;
        //            snc = 0;
        //        }
        //    }
        //    return false;
        //}

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

        byte[] IArray(int size, byte value = 0)
        {
            byte[] temp = new byte[size];
            for (int i = 0; i < size; i++) temp[i] = value;
            return temp;
        }

        (bool, int) Find_Data(string find, byte[] data, int clen, int start_pos = -1)
        {
            if (start_pos < 0) start_pos = 0;
            for (int i = start_pos; i < data.Length - find.Length; i++)
            {
                if (Hex_Val(data, i, clen) == find) return (true, i);
            }
            return (false, 0);
        }

        int Get_Cores()
        {
            foreach (var item in new System.Management.ManagementObjectSearcher("Select NumberOfCores from Win32_Processor").Get())
            {
                int coreCount = int.Parse(item["NumberOfCores"].ToString());
                Cores += coreCount;
            }
            return Cores -= 1;
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

        byte[] Lengthen_Track(byte[] data) // For track 18 on Vorpal images
        {
            if (data.Length > 0)
            {
                byte[] temp = new byte[density[1]];
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

        byte[] Lengthen_Loader(byte[] data, int Density)
        {
            if (data.Length > 0)
            {
                byte[] temp = new byte[density[Density]];
                int current = 0;
                int longest = 0;
                int pos = 0;
                byte fill = 0x00;
                byte cur = 0x00;
                int a = temp.Length - data.Length;
                for (int i = 0; i < data.Length; i++)
                {
                    if (data[i] == cur) current++;
                    else
                    {
                        cur = data[i];
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

        void Shrink_Loader(int trk)
        {
            byte[] temp = Shrink_Track(NDG.Track_Data[trk], 1);
            Set_Dest_Arrays(temp, trk);
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

        byte[] Decode_CBM_GCR(byte[] gcr)
        {
            byte hnib;
            byte lnib;
            byte[] plain = new byte[(gcr.Length / 5) * 4];
            for (int i = 0; i < gcr.Length / 5; i++)
            {
                hnib = GCR_decode_high[gcr[(i * 5) + 0] >> 3];
                lnib = GCR_decode_low[((gcr[(i * 5) + 0] << 2) | (gcr[(i * 5) + 1] >> 6)) & 0x1f];
                if (!(hnib == 0xff || lnib == 0xff)) plain[(i * 4) + 0] = hnib |= lnib;
                else plain[(i * 4) + 0] = 0x00;

                hnib = GCR_decode_high[(gcr[(i * 5) + 1] >> 1) & 0x1f];
                lnib = GCR_decode_low[((gcr[(i * 5) + 1] << 4) | (gcr[(i * 5) + 2] >> 4)) & 0x1f];
                if (!(hnib == 0xff || lnib == 0xff)) plain[(i * 4) + 1] = hnib |= lnib;
                else plain[(i * 4) + 1] = 0x00;

                hnib = GCR_decode_high[((gcr[(i * 5) + 2] << 1) | (gcr[(i * 5) + 3] >> 7)) & 0x1f];
                lnib = GCR_decode_low[(gcr[(i * 5) + 3] >> 2) & 0x1f];
                if (!(hnib == 0xff || lnib == 0xff)) plain[(i * 4) + 2] = hnib |= lnib;
                else plain[(i * 4) + 2] = 0x00;

                hnib = GCR_decode_high[((gcr[(i * 5) + 3] << 3) | (gcr[(i * 5) + 4] >> 5)) & 0x1f];
                lnib = GCR_decode_low[gcr[(i * 5) + 4] & 0x1f];
                if (!(hnib == 0xff || lnib == 0xff)) plain[(i * 4) + 3] = hnib |= lnib;
                else plain[(i * 4) + 3] = 0x00;
            }
            return plain;
        }

        byte[] Encode_CBM_GCR(byte[] plain)
        {
            int l = plain.Length / 4;
            byte[] gcr = new byte[l * 5];

            for (int i = 0; i < l; i++)
            {
                gcr[0 + (i * 5)] = (byte)(GCR_encode[(plain[0 + (i * 4)]) >> 4] << 3);
                gcr[0 + (i * 5)] |= (byte)(GCR_encode[(plain[0 + (i * 4)]) & 0x0f] >> 2);

                gcr[1 + (i * 5)] = (byte)(GCR_encode[(plain[0 + (i * 4)]) & 0x0f] << 6);
                gcr[1 + (i * 5)] |= (byte)(GCR_encode[(plain[1 + (i * 4)]) >> 4] << 1);
                gcr[1 + (i * 5)] |= (byte)(GCR_encode[(plain[1 + (i * 4)]) & 0x0f] >> 4);

                gcr[2 + (i * 5)] = (byte)(GCR_encode[(plain[1 + (i * 4)]) & 0x0f] << 4);
                gcr[2 + (i * 5)] |= (byte)(GCR_encode[(plain[2 + (i * 4)]) >> 4] >> 1);

                gcr[3 + (i * 5)] = (byte)(GCR_encode[(plain[2 + (i * 4)]) >> 4] << 7);
                gcr[3 + (i * 5)] |= (byte)(GCR_encode[(plain[2 + (i * 4)]) & 0x0f] << 2);
                gcr[3 + (i * 5)] |= (byte)(GCR_encode[(plain[3 + (i * 4)]) >> 4] >> 3);

                gcr[4 + (i * 5)] = (byte)(GCR_encode[(plain[3 + (i * 4)]) >> 4] << 5);
                gcr[4 + (i * 5)] |= GCR_encode[(plain[3 + (i * 4)]) & 0x0f];
            }
            return gcr;
        }

        byte[] Decode_Vorpal_GCR(byte[] gcr)
        {
            byte hnib;
            byte lnib;
            byte[] plain = new byte[(gcr.Length / 5) * 4];
            for (int i = 0; i < gcr.Length / 5; i++)
            {
                hnib = VPL_decode_high[gcr[(i * 5) + 0] >> 3];
                lnib = VPL_decode_low[((gcr[(i * 5) + 0] << 2) | (gcr[(i * 5) + 1] >> 6)) & 0x1f];
                if (!(hnib == 0xff || lnib == 0xff)) plain[(i * 4) + 0] = hnib |= lnib;
                else plain[(i * 4) + 0] = 0x00;

                hnib = VPL_decode_high[(gcr[(i * 5) + 1] >> 1) & 0x1f];
                lnib = VPL_decode_low[((gcr[(i * 5) + 1] << 4) | (gcr[(i * 5) + 2] >> 4)) & 0x1f];
                if (!(hnib == 0xff || lnib == 0xff)) plain[(i * 4) + 1] = hnib |= lnib;
                else plain[(i * 4) + 1] = 0x00;

                hnib = VPL_decode_high[((gcr[(i * 5) + 2] << 1) | (gcr[(i * 5) + 3] >> 7)) & 0x1f];
                lnib = VPL_decode_low[(gcr[(i * 5) + 3] >> 2) & 0x1f];
                if (!(hnib == 0xff || lnib == 0xff)) plain[(i * 4) + 2] = hnib |= lnib;
                else plain[(i * 4) + 2] = 0x00;

                hnib = VPL_decode_high[((gcr[(i * 5) + 3] << 3) | (gcr[(i * 5) + 4] >> 5)) & 0x1f];
                lnib = VPL_decode_low[gcr[(i * 5) + 4] & 0x1f];
                if (!(hnib == 0xff || lnib == 0xff)) plain[(i * 4) + 3] = hnib |= lnib;
                else plain[(i * 4) + 3] = 0x00;
            }
            return plain;
        }

        byte[] Build_BlockHeader(int track, int sector, byte[] ID)
        {
            byte[] header = new byte[8];
            header[0] = 0x08;
            header[2] = (byte)sector;
            header[3] = (byte)track;
            Buffer.BlockCopy(ID, 0, header, 4, 4);
            for (int i = 2; i < 6; i++) header[1] ^= header[i];
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

        void Check_Before_Draw(bool dontDrawFlat, bool timeout = false)
        {
            if (Adv_ctrl.SelectedTab == Adv_ctrl.TabPages["tabPage2"])
            {
                this.Update();
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
                        flat = new Thread(new ThreadStart(() => Draw_Flat_Tracks(false, timeout)));
                        flat.Start();
                    }
                    circle?.Dispose();
                    circ = new Thread(new ThreadStart(() => Draw_Circular_Tracks(timeout)));
                    circ.Start();
                }
                catch { }
                drawn = true;
                GC.Collect();
                busy = false;
                Draw = new Thread(new ThreadStart(() => Progress_Thread_Check(timeout)));
                Draw.Start();
            }
        }

        void Init()
        {
            listBox1.Visible = false; // set to true for debugging that requires a listbox
            Debug_Button.Visible = debug;
            Other_opts.Visible = false;
            busy = true;
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
            Adv_ctrl.SelectedIndexChanged += new System.EventHandler(Adv_Ctrl_SelectedIndexChanged);
            Out_density.DrawItem += new DrawItemEventHandler(Out_Density_Color);
            Track_Info.DrawItem += new DrawItemEventHandler(Source_Info_Color);
            sf.DrawItem += new DrawItemEventHandler(Track_Format_Color);
            out_rpm.DrawItem += new DrawItemEventHandler(RPM_Color);
            Out_density.DrawMode = DrawMode.OwnerDrawFixed;
            Track_Info.DrawMode = DrawMode.OwnerDrawFixed;
            out_rpm.DrawMode = DrawMode.OwnerDrawFixed;
            sf.DrawMode = DrawMode.OwnerDrawFixed;
            Out_density.ItemHeight = out_rpm.ItemHeight = sf.ItemHeight = 13;
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
            Adj_pbar.Value = 0;
            Adj_pbar.Visible = false;
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
            Tabs.Controls.Remove(Vpl_adv);
            VD0.Visible = VD1.Visible = VD2.Visible = VD3.Visible = debug;
            VD0.Value = vpl_density[0];
            VD1.Value = vpl_density[1];
            VD2.Value = vpl_density[2];
            VD3.Value = vpl_density[3];
            /// ----------------- V-Max v3 Config -------------
            Tabs.Controls.Remove(Adv_V3_Opts);
            V3_hlen.Enabled = false;
            /// ----------------- V-Max v2 Config -------------
            Tabs.Controls.Remove(Adv_V2_Opts);
            V2_hlen.Enabled = false;
            v2exp.Text = v3exp.Text = $"\u2190 Experimental";
            v2adv.Text = v3adv.Text = $"\u2193        Advanced users ONLY!        \u2193";
            vm2_ver[0] = new string[] { "A5-A5", "A4-A5", "A5-A7", "A5-A6", "A9-AD", "AC-A9", "AD-AB", "A9-AE", "A5-AD", "AC-A5", "AD-A7", "A5-AE", "A5-A9",
            "A4-A9", "A5-AB", "A5-AA", "A5-B5", "B4-A5", "A5-B7", "A5-B6", "A9-BD", "BC-A9" };
            vm2_ver[1] = new string[vm2_ver[0].Length];
            Array.Copy(vm2_ver[0], 0, vm2_ver[1], 0, vm2_ver[0].Length);
            vm2_ver[1][6] = "A5-A3"; vm2_ver[1][10] = "A9-A3";
            /// Loads V-Max Loader track replacements into byte[] arrays
            v2ldrcbm = Decompress(XOR(Resources.v2cbmla, 0xcb)); // V-Max CBM sectors (DotC, Into the Eagles Nest, Paperboy, etc..)
            v24e64pal = Decompress(XOR(Resources.v24e64p, 0x64)); // V-Max Custom sectors (PAL Loader)
            v26446ntsc = Decompress(XOR(Resources.v26446n, 0x46)); // V-Max Custom sectors (NTSC Loader) Older version, headers have weak bits and may be incompatible with some 1541's
            v2644entsc = Decompress(XOR(Resources.v2644En, 0x4e)); // V-Max Custom sectors (NTSC Loader) Newer version, headers are compatible with all 1541 versions.
            /// these loaders are guaranteed to work and the loader code has not been modified from original. (these are not "cracked" loaders)
            Img_Q.DataSource = Img_Quality;
            Img_Q.SelectedIndex = 2;
            Width = PreferredSize.Width;
            Flat_Interp.Visible = Flat_View.Checked;
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
            Trk_Analysis.Checked = true;
            Dir_screen.Visible = Disk_Dir.Checked;
            Tabs.Controls.Remove(Import_File);
            this.Controls.Add(Import_File);
            Import_File.BringToFront();
            Import_File.Top = 57;
            Import_File.Left = 19;
            Set_Boxes();
            Draw_Init_Img(def_bg_text);
            Default_Dir_Screen();
            Set_Auto_Opts();
            manualRender = M_render.Visible = Get_Cores() < 2;
            if (Cores < 2) Img_Q.SelectedIndex = 0;

            //Check_CPU_Speed();
            //File.WriteAllBytes($@"c:\test\compressed\v2cbmla.bin", XOR(Compress(File.ReadAllBytes($@"c:\test\loaders\cbm")), 0xcb));
            //File.WriteAllBytes($@"c:\test\compressed\v24e64p.bin", XOR(Compress(File.ReadAllBytes($@"c:\test\loaders\4e64")), 0x64));
            //File.WriteAllBytes($@"c:\test\compressed\v26446n.bin", XOR(Compress(File.ReadAllBytes($@"c:\test\loaders\6446")), 0x46));
            //File.WriteAllBytes($@"c:\test\compressed\v2644en.bin", XOR(Compress(File.ReadAllBytes($@"c:\test\loaders\644e")), 0x4e));

            busy = false;

            void Set_Boxes()
            {
                outbox.BackColor = Color.Gainsboro;
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
                var w = 5;
                out_track.Location = new Point(w, 15); w += out_track.Width - 1;
                out_rpm.Location = new Point(w, 15); w += out_rpm.Width - 1;
                out_size.Location = new Point(w, 15); w += out_size.Width - 1;
                out_dif.Location = new Point(w, 15); w += out_dif.Width - 1;
                Out_density.Location = new Point(w, 15);
                outbox.FlatStyle = FlatStyle.Flat;
                outbox.ForeColor = Color.Indigo;
                outbox.Name = "outbox";
                outbox.Width = outbox.PreferredSize.Width;
                outbox.Height = outbox.PreferredSize.Height;
                outbox.Location = new Point(210, 13);
                outbox.TabIndex = 52;
                outbox.TabStop = false;
                outbox.Text = "Track/ RPM /    Size    /  Diff  / Density";
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
        }

        void Set_ListBox_Items(bool r, bool nofile)
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
            if (r)
            {
                Make_Visible();
                out_size.Items.Clear();
                out_dif.Items.Clear();
                ss.Items.Clear();
                sf.Items.Clear();
                sl.Items.Clear();
                sd.Items.Clear();
                strack.Items.Clear();
                out_rpm.Items.Clear();
                out_track.Items.Clear();
                Out_density.Items.Clear();

                out_track.Height = Out_density.Height = out_size.Height = out_dif.Height = ss.Height = sf.Height = out_rpm.Height = out_size.PreferredHeight;
                sl.Height = strack.Height = sl.Height = sd.Height = sl.PreferredHeight; // (items * 12);
            }
            Make_Visible();
            outbox.Visible = inbox.Visible = !r;
            out_track.Height = Out_density.Height = out_size.Height = out_dif.Height = ss.Height = sf.Height = out_rpm.Height = out_size.PreferredHeight;
            sl.Height = strack.Height = sl.Height = sd.Height = sl.PreferredHeight; // (items * 12);
            outbox.Height = outbox.PreferredSize.Height;
            inbox.Height = inbox.PreferredSize.Height;
            Drag_pic.Visible = (r && nofile);
            out_size.EndUpdate();
            out_dif.EndUpdate();
            Out_density.EndUpdate();
            ss.EndUpdate();
            sf.EndUpdate();
            out_rpm.EndUpdate();
            out_track.EndUpdate();
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
            }
        }
    }
}