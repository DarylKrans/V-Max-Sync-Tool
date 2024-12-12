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
        readonly int com = 20;

        byte[] Vorpal_Test(byte[] data, int trk, byte[] ID)
        {
            if (data != null && data.Length > 6000)
            {
                trk = trk > 42 ? (trk / 2) + 1 : trk + 1;
                int d = VPL_Density(data.Length);
                BitArray source = new BitArray(Flip_Endian(data));
                BitArray gap = NewBit(new byte[] { 0x55 }, 5);
                BitArray gap2 = NewBit(new byte[] { 0x55 }, 3);
                BitArray sync = NewBit(new byte[] { 0xff, 0xff, 0x55 });
                BitArray sync2 = NewBit(new byte[] { 0xff, 0xff, 0x55 });
                BitArray Sector0 = NewBit(new byte[] { 0xcc, 0xff, 0x54 }, 23); // 25
                BitArray Sectora = NewBit(new byte[] { 0xbf, 0xd5 }, 17); // 17
                int sectors = Get_VPL_Sectors(source);
                BitArray[] s_data = new BitArray[sectors];
                for (int i = 0; i < sectors; i++)
                {
                    s_data[i] = NewBit(Decode_Vorpal(source, i, false, 163), 1300);
                }
                BitArray outt = new BitArray((data.Length + 512) * 8);
                int pos = 0;
                for (int i = 0; i < sectors; i++)
                {
                    if (i == 0) BitCopy(sync);
                    if ((i + 1) % 4 == 0 && ((trk < 18 && trk % 2 == 1) || trk > 18)) BitCopy(sync2);
                    BitCopy(i > 0 ? Sectora : Sector0);
                    BitCopy(s_data[i]);
                    BitCopy(i == 0 ? gap : gap2);
                    if (i == 0 && ((trk < 18 && trk != 5 && trk % 2 == 1) || (trk > 18 && trk != 21)))
                    {
                        BitCopy(NewBit(FastArray.Init(5, 0xff)));
                        BitCopy(NewBit(Build_BlockHeader(trk, 0, ID)));
                    }

                }

                byte[] tmp = Bit2Byte(outt, 0, pos);
                byte[] output = FastArray.Init(tmp.Length < vpl_density[d] ? vpl_density[d] : tmp.Length, 0x55);
                Buffer.BlockCopy(tmp, 0, output, 0, tmp.Length);
                return output;

                void BitCopy(BitArray src)
                {
                    for (int k = 0; k < src.Length; k++)
                    {
                        outt[pos++] = src[k];
                    }
                }
            }
            return null;
        }

        BitArray NewBit(byte[] bytes, int length = 0)
        {
            if (bytes == null || bytes.Length == 0) return null;
            length = length > 0 ? length : bytes.Length << 3;
            BitArray newbit = new BitArray(length);
            BitArray temp = new BitArray(Flip_Endian(bytes));
            int blen = newbit.Length > temp.Length ? temp.Length : newbit.Length;
            for (int i = 0; i < blen; i++)
            {
                if (temp[i]) newbit.Set(i, true);
            }
            return newbit;
        }

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
                    if (NDS.cbm[t] == 5 || NDS.cbm[t] == 1)
                    {
                        if (Original.OT[t].Length > 6000)
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
            Clear_Out_Items();
            Process_Nib_Data(p, false, false, true); /// false flag instructs the routine NOT to process CBM tracks again
        }

        int Get_VPL_Sectors(BitArray source)
        {
            int snc_cnt = 0;
            int sectors = 0;
            List<int> secpos = new List<int>();
            int skip = 160 << 3; /// Sets the # of bits to skip when a sector sync is found
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

        byte[] Rebuild_Vorpal(byte[] data, int trk, int leadptn = 0)
        {
            int last_sector = 1312; /// # of bytes to read when last sector found
            byte[] output = new byte[0];
            int offset;
            int d = 0;
            int snc_cnt = 0;
            int cur_sec = 0;
            int tlen = data.Length;
            if (VPL_auto_adj.Checked || VPL_rb.Checked) d = VPL_Density(tlen);
            //d = VPL_Density(tlen);
            int tstart = 0;
            int tend = 0;
            BitArray source = new BitArray(Flip_Endian(data));
            BitArray comp = new BitArray(Flip_Endian(vpl_s0));
            BitArray cmp = new BitArray(vpl_s0.Length * 8);
            int sectors = Get_VPL_Sectors(source);
            byte[] ssp = { 0x33 };
            byte[] lead_in = new byte[0];
            byte endbyte = leadptn == 1 ? (byte)0x55 : (leadptn == 2 ? (byte)0xAA : (byte)0xb5);
            if (leadptn == 0) lead_in = new byte[] { 0xd5, 0x35, 0x4d, 0x53, 0x54 };
            if (leadptn == 1)
            {
                lead_in = FastArray.Init(5, 0x55);
            }
            if (leadptn == 2)
            {
                lead_in = FastArray.Init(5, 0xaa);
            }
            output = new byte[tlen];
            if (!VPL_only_sectors.Checked) Write_Lead(100);
            BitArray output_bits = new BitArray(Flip_Endian(output));
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
                            tend = k + 7 + (last_sector);
                            break;
                        }
                    }
                    snc_cnt = 0;
                }
            }
            offset = (((output_bits.Length - (tend - tstart)) >> 1) >> 3) << 3;
            var len = (tend - tstart) >> 3;
            if (VPL_lead.Checked) offset = Convert.ToInt32(Lead_In.Value) << 3;
            if (VPL_only_sectors.Checked)
            {
                int asize = (tend - tstart + 7) >> 3;
                int size = sectors > 44 ? 7728 : density[d];
                int tsize = size > density[d] ? size : density[d];
                int offset_adj = (tsize - asize) / 2;
                offset = offset_adj >= 60 ? 60 << 3 : offset_adj << 3;
                output = new byte[tsize];
                Write_Lead(offset + 5);
                if (VPL_presync.Checked) Add_Pre_Sync();
                output[output.Length - 1] = 0x55;
                output_bits = new BitArray(Flip_Endian(output));
            }
            if ((VPL_auto_adj.Checked || VPL_rb.Checked) && !(VPL_only_sectors.Checked || VPL_lead.Checked))
            {
                offset = ((((vpl_density[d] << 3) - (tend - tstart)) >> 1) >> 3) << 3;
                offset = (offset > 60 << 3) ? 60 << 3 : (offset < 15 << 3) ? 15 << 3 : offset;
            }
            if (VPL_auto_adj.Checked)
            {
                var r = offset >> 3;
                var e = len + (r << 1);
                if (e < vpl_density[d])
                {
                    if (vpl_density[d] - e > 125) len = vpl_density[d] - 70;
                    else len = vpl_density[d] - ((r << 1) + 1);
                }
                output = new byte[vpl_density[d]];
                Write_Lead(offset + 5);
                if (VPL_presync.Checked) Add_Pre_Sync();
                output_bits = new BitArray(Flip_Endian(output));
                int os = 0;
                int ts = 0;
                if (!output_bits[offset - 1] && !output_bits[offset - 2]) ts += 2;
                offset += os; tstart += ts;
            }
            try
            {
                for (int i = 0; i < tend - tstart; i++) output_bits[offset + i] = source[tstart + i];
            }
            catch { }
            int total_size = offset + (tend - tstart);
            //if (!VPL_only_sectors.Checked)
            //{
            int leadout_len = (((output.Length << 3) - total_size) >> 3);
            byte[] leadout = FastArray.Init(leadout_len, endbyte);
            if (leadptn == 0) leadout[leadout.Length - 1] = 0xbd;
            BitArray leadout_bits = new BitArray(Flip_Endian(leadout));
            for (int i = 0; i < leadout_bits.Length; i++)
            {
                output_bits[total_size + i] = leadout_bits[i];
            }
            //}
            output = Bit2Byte(output_bits);
            Check_Sync();
            return output;

            void Write_Lead(int li)
            {
                try
                {
                    for (int i = 0; i < li; i++) Buffer.BlockCopy(lead_in, 0, output, 0 + (i * lead_in.Length), lead_in.Length);
                }
                catch { }
            }

            //int Get_VPL_Sectors()
            //{
            //    snc_cnt = 0;
            //    sectors = 0;
            //    List<int> secpos = new List<int>();
            //    int skip = 160 << 3; /// Sets the # of bits to skip when a sector sync is found
            //    for (int k = 0; k < source.Length; k++)
            //    {
            //        if (source[k]) snc_cnt++;
            //        if (!source[k])
            //        {
            //            if (snc_cnt == 8)
            //            {
            //                secpos.Add(k + 7);
            //                if (k + skip < source.Count) k += skip;
            //                else break;
            //                sectors++;
            //            }
            //            snc_cnt = 0;
            //        }
            //    }
            //    return sectors;
            //}

            void Check_Sync()
            {
                if (leadptn > 0)
                {
                    output[(offset >> 3) - 4] = 0xff;
                    output[(offset >> 3) - 3] = 0xff;
                    output[(offset >> 3) - 2] = 0x55;
                    output[(offset >> 3) - 1] = 0x55;
                }
            }

            void Add_Pre_Sync()
            {
                output[(offset / 8) - 3] = 0xff;
                output[(offset / 8) - 2] = 0xff;
                output[(offset / 8) - 1] = 0x55;
                //output[0] = 0xff;
                //output[1] = 0xff;
                //output[2] = 0x55;
                //output[3] = 0x55;
            }
        }

        byte[] Decode_Vorpal(BitArray source, int sector = -1, bool dec = true, int bytes = -1)
        {
            int snc_cnt = 0, psec = 0, sub = dec ? 0 : 8 * 5;
            //BitArray sec_data = new BitArray(dec ? (160 * 8) : (165 * 8));
            BitArray sec_data = new BitArray(dec ? (160 * 8) : (bytes == -1 ? 162 * 8 : bytes * 8));

            byte[] decoded = new byte[0];

            for (int k = 0; k < source.Length; k++)
            {
                if (source[k])
                    snc_cnt++;
                if (!source[k])
                {
                    if (snc_cnt == 8)
                    {
                        int dep = k + 7;

                        if (psec == sector)
                        {
                            try
                            {
                                for (int i = 0; i < sec_data.Length; i++)
                                {
                                    sec_data[i] = source[dep + i];
                                }

                                return dec ? Decode_Vorpal_GCR(Bit2Byte(sec_data)) : Bit2Byte(sec_data);
                            }
                            catch { }
                        }

                        psec++;
                        k += sec_data.Length - sub;
                    }
                    snc_cnt = 0;
                }
            }

            return decoded;
        }

        (byte[], int, int, int, int, int, int[], string[]) Get_Vorpal_Track_Length(byte[] data, int trk = -1)
        {
            int d = (tracks > 42) ? (trk / 2) + 1 : trk;
            int sec_size = 0, lead_len = 0, sub = 1, compare_len = 16;
            int min_skip_len = vpl_density[density_map[d]] - 100;
            int max_track_size = 7900, data_start = 0, data_end = 0, track_len = 0, track_lead_in = 0, sectors = 0, snc_cnt = 0;
            string sz = string.Empty, sid = string.Empty;
            bool single_rotation = false, start_found = false, end_found = false, lead_in_Found = false, repeat = false;
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
                    if (snc_cnt == 8) // && k > 600)
                    {
                        if (sec_size > 160 << 3 || k > 100 << 3)
                        {
                            if (k - ((com << 3) + 8) > 0)
                            {
                                if (!lead_in_Found && (!source[k - 10] && !source[k - 9]))
                                {
                                    sz = " * sector 0";
                                    (lead_in_Found, track_lead_in) = Get_LeadIn_Position(k);
                                }
                                else sz = string.Empty;

                                byte[] sec_ID = Bit2Byte(source, k - ((com << 3) >> 1), com << 3);
                                sid = Hex_Val(sec_ID);

                                if (!sec_header.Any(x => x == sid))
                                {
                                    sec_header.Add(sid);
                                    sec_pos.Add(k >> 3);

                                    if (!batch && sec_size >> 3 > 0)
                                    {
                                        try { sec_hdr.Add($"pos ({k >> 3}) Header (kinda) {sid.Substring(15, 20)} Size ({sec_size >> 3}) ({sec_size}) {sz}"); }
                                        catch { }
                                    }

                                    sec_size = 0;

                                    if (!start_found)
                                    {
                                        data_start = k;
                                        start_found = true;
                                    }

                                    k += 1180; // Skip over the next (x) bits after finding a sector
                                    sec_size += 1180;
                                }
                                else
                                {
                                    if (!batch)
                                    {
                                        try
                                        {
                                            if (!repeat)
                                            {
                                                sec_hdr.Add($"* Repeat * pos {k >> 3} {Hex_Val(sec_ID).Substring(15, 20)}");
                                                repeat = true;
                                            }
                                        }
                                        catch { }
                                    }

                                    if (!end_found)
                                    {
                                        data_end = k - sub;
                                        end_found = true;
                                    }
                                }
                                sectors = sec_header.Count;

                                if (!single_rotation && end_found)
                                    break;
                            }
                        }
                        else sec_size = 0;
                    }
                    snc_cnt = 0;
                }
                sec_size++;
            }
            if (start_found && !end_found) data_end = data.Length << 3;
            track_len = (data_end - data_start) + 1;

            if (single_rotation)
                tdata = Bit2Byte(source, data_start, track_len);
            else
            {
                int spl = 0;
                BitArray temp = new BitArray(track_len);

                int pos = track_lead_in;

                for (int i = 0; i < track_len; i++)
                {
                    try
                    {
                        temp[i] = source[pos];
                        pos++;

                        if (pos > data_end)
                        {
                            spl = i;
                            pos = data_start;
                        }
                    }
                    catch { }
                }

                tdata = Bit2Byte(temp);
            }
            return (tdata, data_start, data_end, track_len, track_lead_in, sectors, sec_pos.ToArray(), sec_hdr.ToArray());

            (bool, int) Get_LeadIn_Position(int position)
            {
                BitArray isRealend = new BitArray(compare_len << 3);
                bool leadF = false;
                int leadin = 0, l = position - 18;
                byte[] pre_sync = Bit2Byte(source, position - 18, 8);

                if (pre_sync[0] == 0x33)
                {
                    bool equal = false;

                    while (!equal)
                    {
                        l -= leadIn_std.Length;
                        lead_len += leadIn_std.Length;

                        if (l < 0)
                        {
                            l = 0;
                            break;
                        }

                        bool a = false, b = false;
                        lead_in = new BitArray(leadIn_std.Length);

                        for (int i = 0; i < lead_in.Length; i++)
                            lead_in[i] = source[l + i];

                        a = ((BitArray)leadIn_std.Clone()).Xor(lead_in).OfType<bool>().All(e => !e);
                        b = ((BitArray)leadIn_alt.Clone()).Xor(lead_in).OfType<bool>().All(e => !e);
                        equal = !a && !b;
                    }

                    if (l != 0)
                    {
                        byte[] te;

                        var u = l;

                        for (int i = 0; i < 32; i++)
                        {
                            te = Flip_Endian(Bit2Byte(source, u - i, 8));

                            if (te?.Length > 0 && te[0] == 0xbd)
                            {
                                l = u - (i + 1);
                                break;
                            }
                        }
                    }

                    leadin = l + leadIn_std.Count - 1;
                    isRealend = BitCopy(source, l, 16 << 3);
                }

                if (leadin + (max_track_size << 3) < source.Length)
                {
                    data_start = leadin;
                    start_found = true;
                    int q = min_skip_len << 3;
                    BitArray rcompp = new BitArray(isRealend.Count);

                    while (q < source.Length - (rcompp.Length))
                    {
                        rcompp = BitCopy(source, q, isRealend.Length);

                        if (((BitArray)isRealend.Clone()).Xor(rcompp).OfType<bool>().All(e => !e))
                        {
                            end_found = true;
                            data_end = q - sub;
                            single_rotation = true;
                            break;
                        }

                        q++;
                    }
                }

                leadF = true;

                return (leadF, leadin);
            }
        }

        bool Check_Vorpal_Sectors(BitArray source, int pos)
        {
            pos += 1200;
            int snc = 0;
            while (pos < source.Length)
            {
                if (source[pos]) snc++;
                else
                {
                    if (snc == 8)
                    {
                        if (!source[pos] && source[pos + 1] && !source[pos + 2] && source[pos + 3] && !source[pos + 4] && source[pos + 5] && !source[pos + 6]) return true;
                    }
                    snc = 0;
                }
                pos++;
            }
            return false;
        }

        void Density_Reset()
        {
            VPL_density_reset.Visible = false;
            RunBusy(() =>
            {
                for (int i = 0; i < vpl_density.Length; i++)
                {
                    vpl_density[i] = vpl_defaults[i];
                    switch (i)
                    {
                        case 0: VD0.Value = vpl_density[i]; break;
                        case 1: VD1.Value = vpl_density[i]; break;
                        case 2: VD2.Value = vpl_density[i]; break;
                        case 3: VD3.Value = vpl_density[i]; break;
                    }
                }
            });
            Vorpal_Rebuild();
        }
    }
}