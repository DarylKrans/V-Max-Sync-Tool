using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.IO;
using System.Reflection;
using System.Threading;
using System;
namespace V_Max_Tool
{
    public partial class Form1 : Form
    {

        //readonly byte[] trk_start_reg = new byte[] { 0x35, 0x4d, 0x53, 0x54, 0xd5 };
        //readonly byte[] trk_start_alt = new byte[] { 0x55, 0x55, 0x55, 0x55, 0x55 };
        readonly byte[] vpl_s0 = new byte[] { 0x33, 0x3F, 0xD5 };
        readonly byte[] vpl_s1 = new byte[] { 0x35, 0x4d, 0x53 };
        //readonly byte[] trk_end_0 = new byte[] { 0xb5, 0xb5, 0xbd };
        readonly BitArray leadIn_std = new BitArray(10);
        readonly BitArray leadIn_alt = new BitArray(10);
        readonly int com = 16;

        byte[] Decode_Vorpal_Track(byte[] data, int trk = -1)
        {
            int snc_cnt = 0;
            int interleve = 3;
            int s = 0;
            int current = 0;
            int sectors = 0;
            int psec = 0;
            var buff = new MemoryStream();
            var wrt = new BinaryWriter(buff);
            BitArray sec_data = new BitArray(160 * 8);
            BitArray source = new BitArray(Flip_Endian(data));
            for (int k = 0; k < source.Length; k++)
            {
                if (source[k]) snc_cnt++;
                if (!source[k])
                {
                    if (snc_cnt == 8)
                    {
                        if (k + sec_data.Count < source.Count) k += sec_data.Count;
                        else break;
                        sectors++;
                    }
                    snc_cnt = 0;
                }
            }
            byte[][] sec_dat = new byte[sectors][];
            for (int k = 0; k < source.Length; k++)
            {
                if (source[k]) snc_cnt++;
                if (!source[k])
                {
                    if (snc_cnt == 8)
                    {
                        var dep = k + 7;
                        if (psec <= sectors)
                        {
                            try
                            {
                                for (int i = 0; i < sec_data.Count; i++)
                                {
                                    sec_data[i] = source[dep + i];
                                }
                                sec_dat[psec] = Decode_VPL(Flip_Endian(Bit2Byte(sec_data)));
                                k += sec_data.Count;
                                psec++;
                            }
                            catch { }
                        }
                    }
                    snc_cnt = 0;
                }
            }
            for (int i = 0; i < sec_dat.Length; i++)
            {
                wrt.Write(sec_dat[current]);
                current += interleve;
                if (current > sectors - 1)
                {
                    s++;
                    current = 0 + s;
                }
            }
            return buff.ToArray();
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
            List<string> hdr = new List<string>();
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
                        if (Hex_Val(rcomp) == Hex_Val(isRealend))
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