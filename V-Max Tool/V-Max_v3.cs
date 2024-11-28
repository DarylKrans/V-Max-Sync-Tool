using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace V_Max_Tool
{
    public partial class Form1 : Form
    {
        /// V-Max v3 sync and header variables for "Rebuild tracks" options
        //private readonly byte[] v3_sector_sync = { 0x5b, 0xff };  // change the sync marker placed before sector headers (0x57, 0xff known working)
        private readonly byte[] v3_sector_sync = { 0x7f, 0xff };  // change the sync marker placed before sector headers (0x57, 0xff known working)
        private readonly int v3_min_header = 3;             // adjust the minimum length of the sector header (0x49) bytes
        private readonly int v3_max_header = 8; //12;            // adjust the maximum length of the sector header (0x49) bytes
        private readonly byte[] vm3_pos_sync = { 0x57, 0x5b, 0x5f, 0x7f, 0xff };
        private readonly byte[] v3a = { 0x49, 0x49, 0x49, 0xee };

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
                            if (Original.OT[t]?.Length == 0)
                            {
                                Original.OT[t] = new byte[NDG.Track_Data[t].Length];
                                Buffer.BlockCopy(NDG.Track_Data[t], 0, Original.OT[t], 0, NDG.Track_Data[t].Length);
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
                            Buffer.BlockCopy(Original.SG, 0, NDG.Track_Data[t], 0, Original.SG.Length);
                            Buffer.BlockCopy(Original.SA, 0, NDA.Track_Data[t], 0, Original.SA.Length);
                            NDG.Track_Length[t] = NDG.Track_Data[t].Length;
                            NDA.Track_Length[t] = NDG.Track_Length[t] * 8;
                            NDG.L_Rot = false;
                        }
                        if (NDS.cbm[t] == 1 || (NDS.cbm[t] == 3)) // && NDS.sectors[t] < 16))
                        {
                            if (Original.OT[t]?.Length != 0 || Original.OT[t] != null)
                            {
                                try
                                {
                                    NDG.Track_Data[t] = new byte[Original.OT[t].Length];
                                    Buffer.BlockCopy(Original.OT[t], 0, NDG.Track_Data[t], 0, Original.OT[t].Length);
                                    Buffer.BlockCopy(Original.OT[t], 0, NDA.Track_Data[t], 0, Original.OT[t].Length);
                                    Buffer.BlockCopy(Original.OT[t], 0, NDA.Track_Data[t], Original.OT[t].Length, NDA.Track_Data[t].Length - Original.OT[t].Length);
                                    p = false;
                                    v = true;
                                }
                                catch { }
                            }
                            NDG.Track_Length[t] = NDG.Track_Data[t].Length;
                            NDA.Track_Length[t] = NDG.Track_Length[t] * 8;
                        }
                    }
                }
            }
            bool e = busy;
            RunBusy(() => f_load.Checked = V3_Auto_Adj.Checked);
            busy = e;
            Clear_Out_Items();
            if (Adj_cbm.Checked && !V3_Auto_Adj.Checked) p = false;
            Process_Nib_Data(true, p, v, true); /// false flag instructs the routine NOT to process CBM tracks again -- p (true/false) process v-max v3 short tracks
        }

        byte[] Rebuild_V3(byte[] data, int gap_sector, byte[] Disk_ID, int trk)
        {
            trk = tracks > 42 ? (trk / 2) + 1 : trk + 1;
            int d = trk < 18 ? 0 : Get_Density(data.Length);
            int sectors = 0;
            int fill = 0;
            byte[] header = FastArray.Init(3, 0x49);
            byte[][] sec_data;
            byte sb = 0x49;
            byte eb = 0xee;
            byte filler = 0xff;
            int sync = v3_sector_sync.Length;
            byte gap = 0x55;
            List<int> s_st = new List<int>();
            List<string> hdr_ID = new List<string>();
            byte[] comp = new byte[3];
            byte[] track_ID = new byte[] { 0x7f };
            if (trk % 2 == 1 && !Disk_ID.All(x => x == 0))
            {
                track_ID = ArrayConcat(v3_sector_sync, new byte[] { 0xff, 0xff }, Build_BlockHeader(trk, 255, NDS.t18_ID));
            }
            int tlen = track_ID.Length;
            var a = 0;
            bool fnd = false;
            (fnd, a) = Find_Data(ArrayConcat(header, new byte[] { eb }), data);
            while (data[a] == 0x49)
            {
                a -= 1;
                if (a < 0) a = data.Length - 1;
            }
            data = Rotate_Left(data, a);
            for (int i = 0; i < data.Length - comp.Length; i++)
            {
                if (data[i] == sb) Buffer.BlockCopy(data, i, comp, 0, comp.Length);
                if (Match(comp, header))
                {
                    int b = 0;
                    while (data[i + b] == sb) b++;
                    if (b < 20 && data[i + b] == eb)
                    {
                        s_st.Add(i + b);
                    }
                    i += b;
                    try
                    {
                        Buffer.BlockCopy(data, i, comp, 0, comp.Length);
                    }
                    catch { }
                    if (comp[0] == eb)
                    {
                        sectors++;
                        hdr_ID.Add(Hex_Val(comp, 2, 1));
                    }
                }
            }

            sec_data = new byte[sectors][];
            for (int i = 0; i < sectors; i++)
            {
                int pos = 0;
                try
                {
                    while (s_st[i] + pos < data.Length)
                    {
                        if (vm3_pos_sync.Any(x => x == data[s_st[i] + pos]) || data[s_st[i] + pos] == sb) break;
                        pos++;
                    }
                    sec_data[i] = CopyFrom(data, s_st[i], pos);
                    tlen += sec_data[i].Length + sync;
                }
                catch { };
            }

            int header_len = (density[d] - (tlen) - 15) / sectors;
            header_len = header_len > v3_max_header ? v3_max_header : header_len < v3_min_header ? v3_min_header : header_len;
            int track_len = tlen + (header_len * sectors);
            if (sectors < 16)
            {
                fill = (density[d] - track_len);
                fill = track_ID.Length == 0 ? fill - 1 : fill;
                int start = 0;
                int longest = 0;
                int count = 0;
                for (int i = 1; i < data.Length; i++)
                {
                    if (data[i] != data[i - 1] && (data[i] != 0xaa && data[i] != 0x55)) count = 0;
                    count++;
                    if (count > longest)
                    {
                        start = i - count;
                        longest = count;
                        filler = data[start + 2];
                    }
                }
            }
            int index = hdr_ID.FindIndex(x => x.StartsWith("F3"));
            byte[] sec_header = FastArray.Init(header_len + sync, 0x49);
            Buffer.BlockCopy(v3_sector_sync, 0, sec_header, 0, sync);

            /// Start rebuilding the track
            var buff = new MemoryStream();
            var wrt = new BinaryWriter(buff);
            for (int i = 0; i < sectors; i++)
            {
                wrt.Write(sec_header);
                wrt.Write(sec_data?[index++]);
                index = index == sectors ? 0 : index;
            }
            wrt.Write(track_ID);
            if (fill > 0) wrt.Write(FastArray.Init(fill, filler));
            int remaining = (density[d] - (int)buff.Position);
            if (remaining > 0) wrt.Write(FastArray.Init(remaining, gap));
            return buff.ToArray();
        }

        (string[], int, int, int, int, int, int, int) Get_vmv3_track_length(byte[] data, int trk)
        {
            string msg = "";
            int data_start = 0;
            int data_end = 0;
            int sector_zero = 0;
            int header_total = 0;
            int header_avg = 0;
            int gap_sector = 0;
            int last_sector = 0;
            bool start_found = false;
            bool end_found = false;
            bool s_zero = false;
            byte sec_0_ID = 0xf3; /// V-Max v3 sector 0 ID marker
            byte head_end = 0xee; /// V-Max v3 header end byte located directly following the 49-49-49 pattern
            byte[] comp = new byte[2];
            byte[] head = new byte[18];
            List<string> s = new List<string>();
            List<string> ss = new List<string>();
            List<int> spos = new List<int>();
            List<byte> hb = new List<byte>();
            List<int> hl = new List<int>();
            string stats = string.Empty;
            for (int i = 0; i < data.Length - comp.Length; i++)
            {
                if (data[i] == 0x49 && data[i + 1] == 0x49)
                {
                    var a = 0;
                    while (data[i + a] == 0x49) a++;
                    i += a;
                    if (data[i] == head_end)
                    {
                        if (i + head.Length < data.Length) Buffer.BlockCopy(data, i, head, 0, head.Length);
                        if (!ss.Any(b => b == Hex_Val(head)))
                        {
                            if (head[2] == sec_0_ID) { sector_zero = i - a; s_zero = true; }
                            if (!start_found)
                            {
                                data_start = i - a;
                                start_found = true;
                                if (last_sector != 0) gap_sector = last_sector;
                            }
                            if (gap_sector == 0) gap_sector = last_sector;
                            last_sector = i;
                            ss.Add(Hex_Val(head));
                            hb.Add(head[2]);
                            spos.Add(i - a);
                            hl.Add(a);
                            header_total += a;
                        }
                        else
                        {
                            end_found = true;
                            data_end = i - a;
                            if (!batch)
                            {
                                build_list();
                                s.Add($"Pos {i - a} **repeat** {Hex_Val(head).Remove(8, Hex_Val(head).Length - 8)}");
                                stats = $"Track Length ({data_end - data_start}) Sectors ({ss.Count})";
                            }
                            if (!s_zero)
                            {
                                if (hb.Count < 10 && !s_zero)
                                {
                                    int p = Array.FindIndex(hb.ToArray(), se => se == sec_0_ID);
                                    if (p != -1)
                                    {
                                        s_zero = true;
                                        sector_zero = spos[p];
                                    }
                                }
                            }
                            if (!batch)
                            {
                                stats += $" sector 0 ({sector_zero})  Header Length ({a + 3})";
                                s.Add(stats);
                            }
                        }
                    }
                }
                if (end_found) break;
            }
            try
            {
                if (end_found && hb.Count < 10) sector_zero = spos[Array.FindIndex(hb.ToArray(), se => se == sec_0_ID)];
            }
            catch
            {
                if (!batch)
                {
                    Invoke(new Action(() =>
                    {
                        using (Message_Center centeringService = new Message_Center(this)) /// center message box
                        {
                            string m = "Output image may not work!";
                            string t = $"Error processing track {(trk / 2) + 1}";
                            MessageBox.Show(m, t, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    }));
                }
            }
            if (header_avg > 0 && header_total > 0) header_avg = header_total / ss.Count;

            void build_list()
            {
                int h;
                int p = Array.FindIndex(hb.ToArray(), se => se == sec_0_ID);
                for (int j = 0; j < ss.Count; j++)
                {
                    string hdr = "";
                    string sz = "";
                    if (j - p >= 0) h = j - p; else h = (hb.Count - p) + j;
                    if (h == 0) sz = "*";
                    for (int u = 0; u < hl[j]; u++) hdr += "49-";
                    s.Add($"Sector ({h}){sz} Pos ({spos[j]}) {hdr}{ss[j].Remove(8, ss[j].Length - 8)}");
                }
                if (!end_found) s.Add($"{msg}");
            }

            if (ss.Count < 16)
            {
                int de = density[Get_Density(data_end - data_start)];
                if ((tracks > 42 && trk == 36) || (tracks <= 42 && trk == 18)) de = density[1];
                if (start_found && !end_found)
                {
                    if (data_start > 500) data_start = 0;
                    data_end = de + 200;
                }
                if (start_found && end_found && (data_end - data_start) < 7000)
                {
                    var a = de - (data_end - data_start);
                    if (data_end + a < 8192) data_end += a;
                }
                msg = $"Track Length [est] (7400) Sectors ({hb.Count})";
            }
            if (fext.ToLower() == ".g64")
            {
                data_start = 0; data_end = NDG.s_len[trk];
                int p = Array.FindIndex(hb.ToArray(), se => se == sec_0_ID);
                msg = $"Track Length ({NDG.s_len[trk]}) Sectors ({hb.Count})";
            }
            if (!end_found)
            {
                build_list();
            }
            return (s.ToArray(), data_start, data_end, sector_zero, (data_end - data_start), ss.Count, header_avg, gap_sector);
        }

        (byte[], int, int) Adjust_Vmax_V3_Sync(byte[] data, int data_start, int data_end, int sector_zero, int sectors = 0)
        {
            if (data == null) { return (null, 0, 0); }
            byte[] bdata = new byte[data_end - data_start];
            Buffer.BlockCopy(data, data_start, bdata, 0, data_end - data_start);
            bdata = Rotate_Left(bdata, ((sector_zero >> 3) - (data_start >> 3)) - 2);
            var buffer = new MemoryStream();
            var write = new BinaryWriter(buffer);
            int spos = 0;
            byte[] hd = FastArray.Init(2, 0x49);
            byte[] comp = new byte[2];
            int cust = (int)V3_hlen.Value;
            int cur_sec = 0;
            while (spos < bdata.Length)
            {
                if (spos + 2 < bdata.Length && bdata[spos + 2] == hd[0])
                {
                    try
                    {
                        Buffer.BlockCopy(bdata, spos + 2, comp, 0, comp.Length);
                        if (Match(comp, hd))
                        {
                            var a = 0;
                            while (bdata[spos + a] != hd[0])
                            {
                                if (!vm3_pos_sync.Any(s => s == bdata[spos + a])) write.Write(bdata[spos + a]);
                                a++;
                            }
                            var b = 0;
                            while (spos + (a + b) < bdata.Length && bdata[spos + (a + b)] == hd[0]) b++;
                            spos += (a + b);
                            if (b < 15 && V3_Custom.Checked) b = cust;
                            if (cur_sec < sectors) write.Write(v3_sector_sync);
                            cur_sec++;
                            for (int i = 0; i < b; i++) write.Write((byte)hd[0]);
                        }
                    }
                    catch { }
                }
                if (spos < bdata.Length) write.Write(bdata[spos]);
                spos++;
            }
            var temp = buffer.ToArray();
            int pos = 0;
            while (pos < temp.Length - 1)
            {
                if (temp[pos] == 0x49 && temp[pos + 1] == 0x49)
                {
                    pos -= 2; break;
                }
                pos++;
            }
            temp = Rotate_Left(temp, pos);
            return (temp, (int)buffer.Length << 3, 0);
        }
    }
}