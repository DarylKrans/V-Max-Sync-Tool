using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace V_Max_Tool
{
    public partial class Form1 : Form
    {
        /*
        v2_info[0] = v2 header start byte
        v2_info[1] = v2 header end byte
        v2_info[2] = header length
        v2_info[3] = v2 version
        v2_info[4] = 0 (syncless track) 1 (track has sync)
        v2_info[5] = gap located before this sector
        */
        private readonly byte[] vm2_pos_sync = { 0x57, 0x5b, 0x5f, 0x7f, 0xff };
        //private readonly byte[] v2_sync_marker = { 0x5b, 0xff }; /// 0x5b, 0xff (known working)
        private readonly byte[] v2_sync_marker = { 0x7f, 0xff, 0xff }; /// 0x5b, 0xff (known working)
        private readonly string[][] vm2_ver = new string[2][];
        private readonly string[] v_check = { "A5-A3", "A9-A3", "AD-AB", "AD-A7" };
        private readonly byte[] VM2_Valid = { 0xa5, 0xa4, 0xa9, 0xaC, 0xad, 0xb4, 0xbc };
        private readonly byte[] vv2n = { 0x64, 0xa5, 0xa5, 0xa5 };
        private readonly byte[] vv2p = { 0x4e, 0xa5, 0xa5, 0xa5 };

        void GetNewHeaders()
        {

            NDG.newheader = new byte[2];
            switch (V2_swap.SelectedIndex)
            {
                case 0: { NDG.newheader[0] = 0x64; NDG.newheader[1] = 0x4e; } break;
                case 1: { NDG.newheader[0] = 0x64; NDG.newheader[1] = 0x46; } break;
                case 2: { NDG.newheader[0] = 0x4e; NDG.newheader[1] = 0x64; } break;
            }
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
                RunBusy(() => f_load.Checked = V2_Auto_Adj.Checked);
                busy = e;
                Clear_Out_Items();
                Process_Nib_Data(c, false, p, true); /// false flag instructs the routine NOT to process CBM tracks again
            }
        }

        (byte[], int, int, int) Rebuild_V2(byte[] data, int sectors, byte[] t_info, int trk, byte[] new_header, bool use_new_Headers = false)
        {
            /// t_info[0] = start byte, t_info[1] = end byte, t_info[2] = header length, t_info[3] = v-max version (for sector headers)
            bool error = false;
            byte end_byte;
            int error_sec = 0;
            int trk_len = 0;
            int pos = 0;
            int Sector_len = 320;
            byte gap_byte = 0x55;
            int trk_density = density[Get_Density(data.Length)];
            byte[][] sec_dat = new byte[23][];
            byte[][] header = new byte[23][];
            byte[] t_gap = new byte[] { 0x7f };
            int track_num = tracks > 42 ? (trk / 2) + 1 : trk + 1;
            if (track_num % 2 == 1 && !NDS.t18_ID.All(x => x == 0))
            {
                t_gap = ArrayConcat(v2_sync_marker, new byte[] { 0xff, 0xff }, Build_BlockHeader(track_num, 255, NDS.t18_ID));
            }
            byte start_byte = t_info[0];
            for (int i = 0; i < data.Length; i++)
            {
                if (data[i] == start_byte && data[i + 1] == 0xa5 && data[i + 2] == 0xa5)
                {
                    if (i >= 1) data = Rotate_Left(data, i - 1);
                    break;
                }
            }
            end_byte = t_info[1];
            byte header1 = use_new_Headers ? new_header[0] : start_byte;
            byte header2 = use_new_Headers ? new_header[1] : end_byte;
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
                //(found, pos) = Find_Data($"{Hex_Val(new byte[] { start_byte })}-{vm2_ver[vs][i]}", data);
                (found, pos) = Find_Data(ArrayConcat(new byte[] { start_byte }, Hex2Byte(vm2_ver[vs][i])), data);
                while (data[pos] != end_byte && (pos < data.Length - 1)) pos++;
                pos += 320;
                while (pos < data.Length)
                {
                    try
                    {
                        if (data[pos] == start_byte && VM2_Valid.Any(s => s == data[pos + 1])) break;
                        pos++;
                        if (pos > 5 && data[pos] != 0xf7) gap_pos++;
                    }
                    catch { }
                    if (gap_pos > 5) { gap_found = true; break; }
                }
                if (gap_found) break;
            }
            int slen = v2_sync_marker.Length;
            bool syncless = t_info[4] == 0x00;
            bool addSync = V2_Add_Sync.Checked;
            int totalSync = syncless && !addSync ? slen : slen * sectors;
            var sec = 1000;
            for (int i = 0; i < data.Length; i++)
            {
                while (data[i] != start_byte) i++;
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
                header[sec] = new byte[2];
                sec_dat[sec] = new byte[Sector_len];
                try
                {
                    (found, pos) = Find_Data(ArrayConcat(new byte[] { start_byte }, Hex2Byte(vm2_ver[vs][sec])), data, ls_pos);
                    /// ---------- remove this if errors occur --------------------
                    ls_pos = (ls_pos + 320 >= data.Length - 320) ? 0 : pos + 320;
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
                    for (int r = 0; r < sec_dat[sec].Length; r++) sec_dat[sec][r] = 0x00;
                    error = true;
                    error_sec = sec;
                }
                trk_len += sec_dat[sec].Length;
                sec++;
                if (sec == sectors) sec = 0;
            }
            trk_len += totalSync;
            int left = trk_density - trk_len - 15;
            int hlen = left / sectors / 2 - 1;
            var buffer = new MemoryStream();
            var write = new BinaryWriter(buffer);
            var st_sec = 0;
            for (int i = 0; i < sectors; i++)
            {
                if (sec_dat[st_sec].Length > 0)
                {
                    if ((i == 0 && syncless && !addSync) || !syncless || addSync) write.Write(v2_sync_marker);
                    write.Write(Build_Header(header[st_sec], hlen));
                    write.Write(sec_dat[st_sec++]);
                    st_sec = st_sec == sectors ? 0 : st_sec;
                }
            }
            if (t_gap.Length > 0) write.Write(t_gap);
            int remain = (trk_density - (int)buffer.Position);
            if (remain > 0) write.Write(FastArray.Init(remain, gap_byte));
            if (error && !batch)
            {
                var tk = track_num;
                error = false;
            }
            return (buffer.ToArray(), 0, (int)buffer.Length, sectors);

            byte[] Build_Header(byte[] ID, int len)
            {
                var buff = new MemoryStream();
                var wrt = new BinaryWriter(buff);
                wrt.Write((byte)header1);
                for (int i = 0; i < len; i++) wrt.Write(ID);
                wrt.Write((byte)header2);
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
            byte[] pattern = FastArray.Init(6, 0xa5);
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
            byte[] pattern = FastArray.Init(3, 0xa5);
            byte[] ignore = new byte[] { 0x7e, 0x7f, 0xff, 0x5f, 0xbf, 0x57, 0x5b }; /// possible sync markers to ignore when building track
            bool st = (t_info[4] == 0);
            int head_len = Convert.ToInt32(t_info[2]);
            int sec_zero;
            byte[] find = FastArray.Init(4, 0xa5);
            byte[] comp = new byte[4];
            find[0] = start_byte[0];
            int vs = Convert.ToInt32(t_info[3]);
            try { Buffer.BlockCopy(data, data_start, temp_data, 0, data_end - data_start); } catch { }
            for (int i = 0; i < temp_data.Length - 5; i++)
            {
                if (temp_data[i] == find[0])
                {
                    Buffer.BlockCopy(temp_data, i, comp, 0, comp.Length);
                    if (Match(comp, find))
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
                (found, sec_zero) = Find_Data(ArrayConcat(start_byte, Hex2Byte(vm2_ver[vs][0])), data, 3);
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