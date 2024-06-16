using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace V_Max_Tool
{
    public partial class Form1 : Form
    {
        private readonly byte[] vm2_pos_sync = { 0x57, 0x5b, 0x5f, 0x7f, 0xff };
        private readonly byte[] v2_sync_marker = { 0x5b, 0xff }; /// 0x5b, 0xff (known working)
        private readonly string[][] vm2_ver = new string[2][];
        private readonly string[] v_check = { "A5-A3", "A9-A3", "AD-AB", "AD-A7" };
        private readonly byte[] VM2_Valid = { 0xa5, 0xa4, 0xa9, 0xaC, 0xad, 0xb4, 0xbc };
        private readonly byte[] vv2n = { 0x64, 0xa5, 0xa5, 0xa5 };
        private readonly byte[] vv2p = { 0x4e, 0xa5, 0xa5, 0xa5 };

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
                            Buffer.BlockCopy(NDG.Track_Data[t], 0, Original.OT[t], 0, NDG.Track_Data[t].Length);
                        }
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
                            Buffer.BlockCopy(Original.OT[t], 0, NDG.Track_Data[t], 0, Original.OT[t].Length);
                            Buffer.BlockCopy(Original.OT[t], 0, NDA.Track_Data[t], 0, Original.OT[t].Length);
                            Buffer.BlockCopy(Original.OT[t], 0, NDA.Track_Data[t], Original.OT[t].Length, 8192 - Original.OT[t].Length);
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
                bool e = busy;
                busy = true;
                f_load.Checked = V2_Auto_Adj.Checked;
                busy = e;
                out_track.Items.Clear();
                out_size.Items.Clear();
                out_dif.Items.Clear();
                Out_density.Items.Clear();
                out_rpm.Items.Clear();
                Process_Nib_Data(c, false, p, true); /// false flag instructs the routine NOT to process CBM tracks again
            }
        }

        (byte[], int, int, int) Rebuild_V2(byte[] data, int sectors, byte[] t_info, int trk, byte[] nh)
        {
            /// t_info[0] = start byte, t_info[1] = end byte, t_info[2] = header length, t_info[3] = v-max version (for sector headers)
            bool error = false;
            byte end_byte;
            int error_sec = 0;
            int trk_len = 0;
            int gap_len;
            int pos = 0;
            int Sector_len = 320;
            byte gab_byte = 0x55;
            int trk_density = density[Get_Density(data.Length)];
            byte[] start_byte = new byte[1];
            byte se_byte = 0x7f;
            byte[][] hdr_dat = new byte[23][];
            byte[][] sec_dat = new byte[23][];
            byte[][] header = new byte[23][];
            byte[] t_gap = new byte[0];
            int[] sector_pos = new int[23];
            start_byte[0] = t_info[0];
            end_byte = t_info[1];
            int g_sec = Convert.ToInt32(t_info[5]);
            int vs = Convert.ToInt32(t_info[3]);
            int gap_pos = 0;
            string compare;
            bool gap_found = false;
            bool found = false;
            for (int i = 0; i < sectors; i++)
            {
                gap_pos = 0;
                var d = pos;
                if (d + 320 > data.Length) d = 0;
                (found, pos) = Find_Data($"{Hex_Val(start_byte)}-{vm2_ver[vs][i]}", data, 3, d);
                while (data[pos] != end_byte && (pos < data.Length - 1)) pos++;
                pos += 320;
                while (pos < data.Length)
                {
                    try
                    {
                        if (data[pos] == start_byte[0] && VM2_Valid.Any(s => s == data[pos + 1])) break;
                        pos++;
                        if (pos > 5 && data[pos] != 0xf7) gap_pos++;
                    }
                    catch { }
                    if (gap_pos > 5) { gap_found = true; break; }
                }
                if (gap_found)
                {
                    if (data[pos - 5] == 0x52)
                    {
                        t_gap = new byte[11];
                        Buffer.BlockCopy(data, pos - 5, t_gap, 1, 10);
                        t_gap[0] = 0xff;
                    }
                    if (i == vm2_ver[vs].Length - 1) gap_pos = 0; else gap_pos += pos;
                    break;
                }
            }
            if (gap_pos > 0) data = Rotate_Left(data, gap_pos);
            int slen;
            var sec = 1000;
            for (int i = 0; i < data.Length; i++)
            {
                while (data[i] != start_byte[0]) i++;
                compare = Hex_Val(data, i + 1, 2);
                if (vm2_ver[vs].Any(s => s == compare))
                {
                    sec = Array.IndexOf(vm2_ver[vs], compare);
                }
                if (sec > 0 && sec < 1000) break;
            }
            int ssec = sec;
            int ls_pos = 0;
            for (int i = 0; i < sectors; i++)
            {
                slen = v2_sync_marker.Length;
                header[sec] = new byte[2];
                sec_dat[sec] = new byte[Sector_len];
                try
                {
                    (found, pos) = Find_Data($"{Hex_Val(start_byte)}-{vm2_ver[vs][sec]}", data, 3, ls_pos);
                    /// ---------- remove this if errors occur --------------------
                    ls_pos = pos + 320;
                    if (ls_pos >= data.Length - 320) ls_pos = 0;
                    /// -----------------------------------------------------------
                }
                catch { }
                Buffer.BlockCopy(data, pos + 1, header[sec], 0, header[sec].Length);
                while (data[pos] != end_byte && (pos + Sector_len + 2) < data.Length) pos++;
                try
                {
                    Buffer.BlockCopy(data, pos + 1, sec_dat[sec], 0, Sector_len);
                }
                catch
                {
                    for (int r = 0; r < sec_dat[sec].Length; r++) sec_dat[sec][r] = 0xf7;
                    error = true;
                    error_sec = sec;
                }
                trk_len += (sec_dat[sec].Length + (slen + 1));
                sec++;
                if (sec == sectors) sec = 0;
            }
            int left = trk_density - trk_len - 15;
            int hlen = ((left / sectors) / 2) * 2;
            gap_len = trk_density - ((hlen * sectors) + trk_len) - t_gap.Length;
            var buffer = new MemoryStream();
            var write = new BinaryWriter(buffer);
            var st_sec = 0;
            for (int i = 0; i < sectors; i++)
            {
                if (sec_dat[st_sec].Length > 0)
                {
                    write.Write(v2_sync_marker);
                    write.Write(Build_Header(header[st_sec], hlen));
                    write.Write(sec_dat[st_sec]);
                    write.Write((byte)se_byte);
                    st_sec++; if (st_sec == sectors) st_sec = 0;
                }
            }
            if (t_gap.Length > 0) write.Write(t_gap);
            for (int j = 0; j < gap_len; j++) write.Write((byte)gab_byte);
            if (error && !batch)
            {
                var tk = 0;
                if (tracks > 42) tk = (trk / 2) + 1; else tk = trk + 1;
                error = false;
                //Invoke(new Action(() =>
                //{
                //    using (Message_Center centeringService = new Message_Center(this)) /// center message box
                //    {
                //        string m = $"Possible corrupt data on track {tk} sector {error_sec}";
                //        string t = "Error processing image!";
                //        MessageBox.Show(m, t, MessageBoxButtons.OK, MessageBoxIcon.Error);
                //    }
                //}));
            }
            return (buffer.ToArray(), 0, (int)buffer.Length, sectors);

            byte[] Build_Header(byte[] ID, int len)
            {
                var buff = new MemoryStream();
                var wrt = new BinaryWriter(buff);
                if (!V2_swap_headers.Checked) wrt.Write(start_byte); else wrt.Write(nh[0]);
                for (int i = 0; i < (len - 2) / 2; i++) wrt.Write(ID);
                if (!V2_swap_headers.Checked) wrt.Write((byte)end_byte); else wrt.Write(nh[1]);
                buff.Close();
                wrt.Close();
                return buff.ToArray();
            }
        }

        (byte[], int, int, int, int, string[], int, int, byte[]) Get_V2_Track_Info(byte[] data, int trk)
        {
            int data_start = 0;
            int data_end = 0;
            int sec_zero = 0;
            int sectors = 0;
            int gap_sec = 0;
            bool start_found = false;
            bool end_found = false;
            bool found = false;
            byte[] start_byte = new byte[1];
            byte[] end_byte = new byte[1];
            byte[] pattern = IArray(6, 0xa5);
            byte[] ignore = new byte[] { 0x7e, 0x7f, 0xff, 0x5f, 0xbf, 0x57 };
            string ptn = Hex_Val(pattern);
            string compare = string.Empty;
            byte[] m = new byte[6];
            List<string> all_headers = new List<string>();
            List<string> headers = new List<string>();
            for (int i = 0; i < data.Length - 4; i++)
            {
                try
                {
                    compare = Hex_Val(data, i, 6);
                }
                catch { }
                if (ptn == compare)
                {
                    start_byte[0] = data[i - 1];
                    m[0] = data[i - 1];
                    for (int j = 1; j < data.Length; j++)
                    {
                        if (data[j + i] != 0xa5)
                        {
                            end_byte[0] = data[j + i];
                            m[1] = end_byte[0];
                            found = true;
                            break;
                        }
                    }
                }
                if (found) break;
            }
            int tr;
            if (tracks > 42) tr = (trk / 2) + 1; else tr = (trk + 1);
            bool v = false;
            string ver = "";
            int vs = 0;
            for (int i = 0; i < v_check.Length; i++)
            {
                v = Check_Version($"{Hex_Val(start_byte)}-{v_check[i]}", data, 3);
                if (v)
                {
                    if (i < 2) { ver = "(older)"; vs = 1; } else { ver = "(newer)"; v = false; vs = 0; }
                    break;
                }
            }
            m[3] = (byte)vs;
            var co = 0;
            string snc = " *(Syncless)";
            for (int i = 0; i < data.Length; i++)
            {
                if (data[i] == start_byte[0] && i > 0)
                {
                    if (ignore.Any(s => s == data[i - 1])) co++;
                }
                if (co > 10) { m[4] = 1; snc = ""; break; }
            }
            if (!batch) all_headers.Add($"track {tr} Format : {secF[NDS.cbm[trk]]} {ver}");
            byte[] comp = new byte[2];
            byte[] rep = new byte[0];
            int dif = 0;
            for (int i = 0; i < data.Length - 5; i++)
            {
                List<byte> hd = new List<byte>();
                if (data[i] == start_byte[0])
                {
                    Buffer.BlockCopy(data, i + 1, comp, 0, comp.Length);
                    if (vm2_ver[vs].Any(s => s == Hex_Val(comp)))
                    {
                        var pos = i + 1;
                        while (data[pos] != end_byte[0] && data[pos] < data.Length)
                        {
                            hd.Add(data[pos]); pos++;
                        }
                        if (!Match(rep, comp))
                        {
                            var a = Array.FindIndex(vm2_ver[vs], s => s == Hex_Val(comp));
                            if (pos - dif > 370)
                            {
                                gap_sec = (hd[0] ^ hd[1]);
                                m[5] = (byte)a;
                                all_headers.Add($"<------------------- (Gap) ------------------->");
                            }

                            m[2] = (byte)hd.Count;
                            string sz = "";
                            if (a == 0) { sz = "*"; sec_zero = i; }
                            if (!batch) all_headers.Add($"Sector ({hd[0] ^ hd[1]}){sz} pos ({i}) {Hex_Val(start_byte, 0, 1)}-{Hex_Val(hd.ToArray())}-{Hex_Val(end_byte, 0, 1)}");
                            if (!start_found) { data_start = i; start_found = true; }
                            sectors++;
                            dif = pos;
                        }
                        else
                        {
                            data_end = i;
                            end_found = true;
                            if (!batch)
                            {
                                all_headers.Add($"pos {i} ** Repeat ** {Hex_Val(start_byte, 0, 1)}-{Hex_Val(hd.ToArray())}-{Hex_Val(end_byte, 0, 1)}");
                                all_headers.Add($"Track length ({data_end - data_start}){snc} Sectors ({sectors}) Sector 0 ({sec_zero}) Header length ({hd.Count + 2}) {Hex_Val(m)}");
                                all_headers.Add(" ");
                            }
                            break;
                        }
                        if (rep.Length == 0)
                        {
                            rep = new byte[2];
                            Buffer.BlockCopy(comp, 0, rep, 0, comp.Length);
                        }
                        if (!start_found) data_start = i;
                    }
                    if (end_found) break;
                }
            }
            if (data_end < data_start) data_end = data.Length;
            byte[] tdata = new byte[8192];
            try
            {
                Buffer.BlockCopy(data, data_start, tdata, 0, data_end - data_start);
                Buffer.BlockCopy(data, data_start, tdata, (data_end - data_start), 8192 - (data_end - data_start));
            }
            catch { }
            return (tdata, data_start, data_end, sec_zero, (data_end - data_start) << 3, all_headers.ToArray(), sectors, gap_sec, m);
        }

        byte[] Adjust_V2_Sync(byte[] data, int data_start, int data_end, byte[] t_info, bool Fix_Sync, int trk = -1)
        {
            if (trk < 0) trk = 0;
            byte[] temp_data = new byte[data_end - data_start];
            byte[] start_byte = { t_info[0] };
            byte[] end_byte = { t_info[1] };
            byte[] compare = new byte[4];
            byte[] pattern = IArray(3, 0xa5);
            byte[] ignore = new byte[] { 0x7e, 0x7f, 0xff, 0x5f, 0xbf, 0x57, 0x5b }; /// possible sync markers to ignore when building track
            bool st = (t_info[4] == 0);
            int head_len = Convert.ToInt32(t_info[2]);
            int sec_zero;
            byte[] find = IArray(4, 0xa5);
            find[0] = start_byte[0];
            int vs = Convert.ToInt32(t_info[3]);
            try { Buffer.BlockCopy(data, data_start, temp_data, 0, data_end - data_start); } catch { }
            for (int i = 0; i < temp_data.Length - 5; i++)
            {
                Buffer.BlockCopy(temp_data, i, compare, 0, compare.Length);
                if (Match(find, compare))
                {
                    if (i > 5)
                    {
                        sec_zero = i - 5;
                        temp_data = Rotate_Left(temp_data, i - 5);
                    }
                    else
                    {
                        temp_data = Rotate_Right(temp_data, i + 5);
                        sec_zero = i + 5;
                    }
                    break;
                }
            }
            if (Fix_Sync) /// <- if the "Fix_Sync" bool is true, otherwise just return track info without any adjustments
            {
                /// ---------------------- Build new track with adjusted sync markers -------------------------------------- //
                var buffer = new MemoryStream();
                var write = new BinaryWriter(buffer);
                var s_pos = 0;
                /// Set the length of the sector header in multiples of 2 including the start and end marker.  Minimum = 6
                var sector_header = Convert.ToInt32((V2_hlen.Value - 2) * 2) / 2;
                if (V2_Auto_Adj.Checked) sector_header = head_len;
                byte[] sec_header = new byte[0];
                byte[] secz = { 0xa5, 0xa5 };
                bool no_sync = false;
                compare = new byte[2];
                /// begin processing the track
                bool sf = false;
                byte[] chk = new byte[1];
                while (s_pos < temp_data.Length)
                {
                    try
                    {
                        if (s_pos + 2 < temp_data.Length && temp_data[s_pos] == start_byte[0] && VM2_Valid.Any(s => s == temp_data[s_pos + 1]))  // s_pos + 2 
                        {
                            sf = false;
                            var m = 0;
                            byte[] header_ID = new byte[2];
                            if (s_pos + 3 < temp_data.Length - 1) Buffer.BlockCopy(temp_data, s_pos + 2, header_ID, 0, 2); // s_pos + 4, s_pos + 3
                            while (temp_data[s_pos] != start_byte[0]) m++; // s_pos++;
                            s_pos += m + 1; /// sets source position 1 byte after the header start byte to get the header pattern data
                            Buffer.BlockCopy(temp_data, s_pos, compare, 0, compare.Length);

                            if (vm2_ver[vs].Any(s => s == Hex_Val(compare))) // <- checks to verify header pattern is in the list of valid headers
                            {
                                /// check that it's not sector 0 which needs sync, then check if a sync marker is before the header start byte.  If not, its a syncless track
                                if (!V2_Add_Sync.Checked)
                                {
                                    if (compare != secz && (!vm2_pos_sync.Any(s => s == temp_data[s_pos - 2])) && temp_data[s_pos - 1] == start_byte[0]) no_sync = true;
                                    else no_sync = false;
                                }
                                var header_length = 0;
                                while (s_pos < temp_data.Length && temp_data[s_pos] != end_byte[0]) /// <- getting the length of the header pattern
                                {
                                    s_pos++; header_length++;
                                }
                                s_pos++;
                                if (V2_Custom.Checked) header_length = sector_header;

                                if (!no_sync)
                                {
                                    buffer.Seek(buffer.Length, SeekOrigin.Begin);
                                    buffer.Read(chk, 0, 1);
                                    if (chk[0] != 0x7f) write.Write((byte)0x7f);
                                    write.Write(v2_sync_marker); /// <- Here's where we add the sync (unless its a syncless track)
                                }
                                write.Write(Build_Header(start_byte, end_byte, compare, ((header_length) / 2) * 2)); /// building new header and writing to buffer
                            }
                        }
                    }
                    catch { }
                    if (s_pos < temp_data.Length && !ignore.Any(s => s == temp_data[s_pos]))
                    {
                        if (!sf) write.Write(temp_data[s_pos]); /// <- loop writes sector data to the buffer until it hits another header
                        if (temp_data[s_pos] == 0x7f) sf = true;
                    }
                    s_pos++;
                }
                bool found = false;
                (found, sec_zero) = Find_Data($"{Hex_Val(start_byte)}-{vm2_ver[vs][0]}", data, 3);
                return buffer.ToArray(); /// <- Return new array with sync markers adjusted

                byte[] Build_Header(byte[] s, byte[] e, byte[] f, int len)
                {
                    var buff = new MemoryStream();
                    var wrt = new BinaryWriter(buff);
                    wrt.Write((byte)s[0]);
                    for (int i = 0; i < (len / 2); i++) wrt.Write(f);
                    wrt.Write((byte)e[0]);
                    return buff.ToArray();
                }
            }
            else return temp_data; /// <- Return array without any adjustments to sync
        }
    }
}