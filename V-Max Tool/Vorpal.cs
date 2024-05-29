using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace V_Max_Tool
{
    public partial class Form1 : Form
    {

        readonly byte[] vpl_s0 = new byte[] { 0x33, 0x3F, 0xD5 };
        readonly byte[] vpl_s1 = new byte[] { 0x35, 0x4d, 0x53 };
        readonly BitArray leadIn_std = new BitArray(10);
        readonly BitArray leadIn_alt = new BitArray(10);
        readonly int com = 16;

        void Vorpal_Rebuild()
        {
            bool p = false;
            if (VPL_rb.Checked || VPL_auto_adj.Checked)
            {
                for (int t = 0; t < tracks; t++)
                {
                    if (NDG.Track_Data[t] != null)
                    {
                        if (NDS.cbm[t] == 1)
                        {
                            if (Original.OT[t].Length == 0)
                            {
                                Original.OT[t] = new byte[NDG.Track_Data[t].Length];
                                Buffer.BlockCopy(NDG.Track_Data[t], 0, Original.OT[t], 0, NDG.Track_Data[t].Length);
                            }
                        }
                    }
                }
            }
            if (VPL_auto_adj.Checked) p = true;
            for (int t = 0; t < tracks; t++)
            {
                if (NDG.Track_Data[t] != null)
                {
                    if (NDS.cbm[t] == 5 || NDS.cbm[t] == 1) // && NDS.sectors[t] < 16))
                    {
                        if (Original.OT[t].Length != 0)
                        {
                            NDG.Track_Data[t] = new byte[Original.OT[t].Length];
                            Buffer.BlockCopy(Original.OT[t], 0, NDG.Track_Data[t], 0, Original.OT[t].Length);
                            Buffer.BlockCopy(Original.OT[t], 0, NDA.Track_Data[t], 0, Original.OT[t].Length);
                            Buffer.BlockCopy(Original.OT[t], 0, NDA.Track_Data[t], Original.OT[t].Length, NDA.Track_Data[t].Length - Original.OT[t].Length);
                        }
                        NDG.Track_Length[t] = NDG.Track_Data[t].Length;
                        NDA.Track_Length[t] = NDG.Track_Length[t] * 8;
                    }
                }
            }
            out_track.Items.Clear();
            out_size.Items.Clear();
            out_dif.Items.Clear();
            Out_density.Items.Clear();
            out_rpm.Items.Clear();
            Process_Nib_Data(p, false, false); // false flag instructs the routine NOT to process CBM tracks again -- p (true/false) process v-max v3 short tracks
        }

        byte[] Rebuild_Vorpal(byte[] data, int trk = -1)
        {
            int esb = 164; // # of bytes to read when last sector found
            if (trk == -1) trk -= 0;
            byte[] temp = new byte[0];
            int offset;
            int d = 0;
            int snc_cnt = 0;
            int cur_sec = 0;
            int tlen = data.Length;
            if (VPL_auto_adj.Checked || VPL_rb.Checked) d = VPL_Density(tlen);
            int tstart = 0;
            int tend = 0;
            BitArray source = new BitArray(Flip_Endian(data));
            BitArray comp = new BitArray(Flip_Endian(vpl_s0));
            BitArray cmp = new BitArray(vpl_s0.Length * 8);
            int sectors = Get_VPL_Sectors();
            byte[] ssp = { 0x33 };
            byte[] lead_in = new byte[0];
            byte lead_out = 0xb5;
            byte stop = 0xbd;
            //if (Lead_ptn.SelectedIndex == 0) lead_in = new byte[] { 0xaa, 0x6a, 0x9a, 0xa6, 0xa9 };
            if (Lead_ptn.SelectedIndex == 0) lead_in = new byte[] { 0xd5, 0x35, 0x4d, 0x53, 0x54 };
            if (Lead_ptn.SelectedIndex == 1)
            {
                lead_in = IArray(5, 0x55); // new byte[] { 0x55, 0x55, 0x55, 0x55, 0x55 };
                lead_out = 0x55;
                stop = 0x55;
            }
            if (Lead_ptn.SelectedIndex == 2)
            {
                lead_in = IArray(5, 0xaa); // new byte[] { 0xaa, 0xaa, 0xaa, 0xaa, 0xaa };
                lead_out = 0xaa;
                stop = 0xaa;

            }
            temp = new byte[tlen];
            if (!VPL_only_sectors.Checked) Write_Lead(100, 400);
            BitArray otmp = new BitArray(Flip_Endian(temp));
            for (int k = 0; k < source.Length - comp.Count; k++)
            {
                for (int j = 0; j < cmp.Count; j++) cmp[j] = source[k + j];
                if (((BitArray)cmp.Clone()).Xor(comp).OfType<bool>().All(e => !e)) { tstart = k; break; }
            }
            for (int k = tstart; k < source.Length; k++)
            {
                if (source[k]) snc_cnt++;
                if (!source[k])
                {
                    if (snc_cnt == 8)
                    {
                        cur_sec++;
                        if (cur_sec == sectors)
                        {
                            tend = k + 7 + (esb << 3);
                            break;
                        }
                    }
                    snc_cnt = 0;
                }
            }
            offset = (((otmp.Length - (tend - tstart)) >> 1) >> 3) << 3;
            var len = (tend - tstart) / 8;
            if (VPL_lead.Checked) offset = Convert.ToInt32(Lead_In.Value) << 3;
            if (VPL_only_sectors.Checked)
            {
                offset = 0;
                temp = new byte[len + 1];
                otmp = new BitArray(Flip_Endian(temp));
            }
            if (VPL_auto_adj.Checked) // || VPL_rb.Checked)
            {
                offset = ((((vpl_density[d] << 3) - (tend - tstart)) >> 1) >> 3) << 3;
                if (offset > 60 << 3) offset = 60 << 3;
                if (offset < 15 << 3) offset = 15 << 3;
                var r = offset >> 3;
                var e = len + (r << 1);
                if (e < vpl_density[d])
                {
                    if (vpl_density[d] - e > 125) len = vpl_density[d] - 70;
                    else len = vpl_density[d] - ((r << 1) + 1);
                }
                temp = new byte[vpl_density[d]];
                Write_Lead(100, 400);
                if (VPL_presync.Checked) Add_Pre_Sync();
                otmp = new BitArray(Flip_Endian(temp));
                int os = 0;
                int ts = 0;
                if (!otmp[offset - 1] && !otmp[offset - 2]) ts += 2;
                offset += os; tstart += ts;
            }
            try
            {
                for (int i = 0; i < tend - tstart; i++) otmp[offset + i] = source[tstart + i];
            }
            catch { }
            temp = Flip_Endian(Bit2Byte(otmp));
            Check_Sync();
            return temp;

            void Write_Lead(int li, int lo)
            {
                for (int i = temp.Length - lo; i < temp.Length; i++) temp[i] = lead_out;
                for (int i = 0; i < li; i++) Buffer.BlockCopy(lead_in, 0, temp, 0 + (i * lead_in.Length), lead_in.Length);
                temp[temp.Length - 1] = stop;
            }

            int Get_VPL_Sectors()
            {
                snc_cnt = 0;
                sectors = 0;
                List<int> secpos = new List<int>();
                int skip = 160 << 3; // Sets the # of bits to skip when a sector sync is found
                for (int k = 0; k < source.Length; k++)
                {
                    if (source[k]) snc_cnt++;
                    if (!source[k])
                    {
                        if (snc_cnt == 8)
                        {
                            secpos.Add(k + 7);
                            if (k + skip < source.Count) k += skip;
                            else break;
                            sectors++;
                        }
                        snc_cnt = 0;
                    }
                }
                return sectors;
            }
            void Check_Sync()
            {
                if (Lead_ptn.SelectedIndex > 0)
                {
                    temp[(offset / 8) - 4] = 0xff;
                    temp[(offset / 8) - 3] = 0xff;
                    temp[(offset / 8) - 2] = 0x55;
                    temp[(offset / 8) - 1] = 0x55;
                }
            }

            void Add_Pre_Sync()
            {
                temp[0] = 0xff;
                temp[1] = 0xff;
                temp[2] = 0x55;
                temp[3] = 0x55;
            }
        }

        byte[] Decode_Vorpal(BitArray source, int sector = -1, bool dec = true)
        {
            int snc_cnt = 0;
            int psec = 0;
            int sub = 0;
            BitArray sec_data = new BitArray(160 * 8);
            if (!dec)
            {
                sec_data = new BitArray(165 * 8);
                sub = 8 * 5;
            }
            byte[] decoded = new byte[0];
            for (int k = 0; k < source.Length; k++)
            {
                if (source[k]) snc_cnt++;
                if (!source[k])
                {
                    if (snc_cnt == 8)
                    {
                        var dep = k + 7;
                        if (psec == sector)
                        {
                            try
                            {
                                for (int i = 0; i < sec_data.Count; i++)
                                {
                                    sec_data[i] = source[dep + i];
                                }
                                if (dec) return Decode_Vorpal_GCR(Flip_Endian(Bit2Byte(sec_data)));
                                else return (Flip_Endian(Bit2Byte(sec_data)));
                            }
                            catch { }
                        }
                        psec++;
                        k += sec_data.Count - sub;
                        //if (!dec) k -= (8 * 5);
                    }
                    snc_cnt = 0;
                }
            }
            return decoded;
        }

        (byte[], int, int, int, int, int, int[], string[]) Get_Vorpal_Track_Length(byte[] data, int trk = -1)
        {
            int sec_size = 0;
            if (trk < 0) trk = 0;
            int lead_len = 0;
            int sub = 1; // <- # of bits to subtract from 'data_end' position marker
            int compare_len = 16; // <- sets the number of bytes to compare with for finding the end of the track
            int min_skip_len = 6000; // <- sets the # of bytes to skip when searching for the repeat of data
            int max_track_size = 7900;
            int data_start = 0;
            int data_end = 0;
            int track_len = 0;
            bool single_rotation = false;
            bool start_found = false;
            bool end_found = false;
            bool lead_in_Found = false;
            bool repeat = false;
            int track_lead_in = 0;
            int sectors = 0;
            int snc_cnt = 0;
            List<string> sec_header = new List<string>();
            List<string> sec_hdr = new List<string>();
            List<int> sec_pos = new List<int>();
            byte[] tdata = new byte[0];
            BitArray source = new BitArray(Flip_Endian(data));
            BitArray lead_in = new BitArray(leadIn_std.Length);
            for (int k = 0; k < source.Length; k++)
            {
                if (source[k]) snc_cnt++;
                if (!source[k])
                {
                    if (snc_cnt == 8)
                    {
                        if (k - ((com * 8) + 8) > 0)
                        {
                            // checking for [00110011 00 11111111] sector 0 marker
                            if (!lead_in_Found && (source[k - 10] == false && source[k - 9] == false)) (lead_in_Found, track_lead_in) = Get_LeadIn_Position(k);
                            byte[] sec_ID = Flip_Endian(Bit2Byte(source, k - ((com * 8) / 2), com * 8));
                            if (!sec_header.Any(x => x == Hex_Val(sec_ID)))
                            {
                                sec_header.Add(Hex_Val(sec_ID));
                                sec_pos.Add(k >> 3);
                                sec_hdr.Add($"pos {k >> 3} {Hex_Val(sec_ID)} ({sec_size / 8}) ({sec_size})");
                                sec_size = 0;
                                if (!start_found)
                                {
                                    data_start = k;
                                    start_found = true;
                                }
                            }
                            else
                            {
                                if (!repeat) { sec_hdr.Add($"* Repeat * pos {k >> 3} {Hex_Val(sec_ID)}"); repeat = true; }
                                if (!end_found)
                                {
                                    data_end = k - sub;
                                    end_found = true;
                                }
                            }
                            sectors = sec_header.Count;
                            if (!single_rotation && end_found) break;
                        }
                    }
                    snc_cnt = 0;
                }
                sec_size++;
            }
            int spl = 0;
            track_len = (data_end - data_start) + 1;
            if (single_rotation) tdata = Flip_Endian(Bit2Byte(source, data_start, track_len));
            else
            {
                BitArray temp = new BitArray(track_len);
                int pos = track_lead_in;
                for (int i = 0; i < track_len; i++)
                {
                    temp[i] = source[pos];
                    pos++;
                    if (pos > data_end) { spl = i; pos = data_start; }
                }
                tdata = Flip_Endian(Bit2Byte(temp));
            }
            return (tdata, data_start, data_end, track_len, track_lead_in, sectors, sec_pos.ToArray(), sec_hdr.ToArray());

            (bool, int) Get_LeadIn_Position(int position)
            {
                byte[] isRealend = new byte[16];
                bool leadF = false;
                int leadin = 0;
                int l = position - 18;
                byte[] pre_sync = Flip_Endian(Bit2Byte(source, position - 18, 8));
                if (pre_sync[0] == 0x33)
                {
                    bool equal = false;
                    while (!equal)
                    {
                        l -= leadIn_std.Length;
                        lead_len += leadIn_std.Length;
                        if (l < 0) { l = 0; break; }
                        bool a = false; bool b = false;
                        for (int i = 0; i < lead_in.Length; i++) lead_in[i] = source[l + i];
                        a = ((BitArray)leadIn_std.Clone()).Xor(lead_in).OfType<bool>().All(e => !e);
                        b = ((BitArray)leadIn_alt.Clone()).Xor(lead_in).OfType<bool>().All(e => !e);
                        equal = !a && !b;
                    }
                    if (l != 0)
                    {
                        var u = l;
                        for (int i = 0; i < 32; i++)
                        {
                            byte[] te = Bit2Byte(source, u - i, 8);
                            if (te[0] == 0xbd) { l = u - (i + 1); break; }
                        }
                    }
                    leadin = l + leadIn_std.Count - 1;
                    isRealend = Flip_Endian(Bit2Byte(source, l, 16 * 8));
                }

                if (leadin + (max_track_size * 8) < source.Length)
                {
                    data_start = leadin;
                    start_found = true;
                    int q = min_skip_len * 8;
                    while (q < source.Length - (compare_len * 8))
                    {
                        byte[] rcomp = Flip_Endian(Bit2Byte(source, q, isRealend.Length * 8));
                        if (Match(isRealend, rcomp)) // == Hex_Val(isRealend))
                        {
                            end_found = true;
                            data_end = q - sub;
                            single_rotation = true; // <- entire track contained within the start and end point of the source array
                            break;
                        }
                        q++;
                    }
                }
                leadF = true;
                return (leadF, leadin);
            }
        }
    }
}