using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace V_Max_Tool
{
    public partial class Form1 : Form
    {
        private readonly byte[] RLok = new byte[] { 0xff, 0xff, 0x55, 0x7b }; /// Pattern used to detect if a track is RapidLok protected

        byte[] RapidLok_Key_Fix(byte[] data)
        {
            byte[] blank = new byte[] { 0x00, 0x11, 0x22, 0x44, 0x45, 0x14, 0x12, 0x51, 0x88 };
            byte[] newkey = IArray(7153, 0xff);
            byte[] key = new byte[256];
            for (int i = 0; i < data.Length; i++)
            {
                if ((data[i] == 0x6b && !blank.Any(x => x == data[i + 1])) && i + 256 < data.Length)
                {
                    Buffer.BlockCopy(data, i, key, 0, 256);
                    break;
                }
            }
            for (int i = 55; i < key.Length; i++) if (key[i] != 0xff) key[i] = 0x00;
            Buffer.BlockCopy(key, 0, newkey, newkey.Length - 286, 256);
            return newkey;
        }

        (byte[], int, int, int, int, string[]) RapidLok_Track_Info(byte[] data)
        {
            int d_start = 0;
            int d_end = 0;
            int sectors = 0;
            int pos = 0;
            int snc_cnt = 0;
            bool sync = false;
            bool start_found = false;
            bool end_found = false;
            List<string> headers = new List<string>();
            List<string> a_headers = new List<string>();
            List<int> sec_pos = new List<int>();
            BitArray source = new BitArray(Flip_Endian(data));
            byte[] c;
            Compare();
            while (pos < source.Length)
            {
                if (source[pos])
                {
                    snc_cnt++;
                    if (snc_cnt >= 24) sync = true;
                }
                if (!source[pos])
                {
                    if (sync) Compare();
                    snc_cnt = 0;
                    sync = false;
                }
                pos++;
            }
            int ds = 0;
            int de = 0;
            if (headers.Count != headers.Distinct().Count())
            {
                for (int i = 1; i < headers.Count; i++)
                {
                    if (headers[i - 1] == headers[i])
                    {
                        ds = sec_pos[i];
                        d_start = ds;
                        de = sec_pos[i - 1];
                        d_end = de;
                        break;
                    }
                }
            }
            else
            {
                ds = d_start;
                if (end_found) de = d_end; else if (sec_pos.Count > 0) de = sec_pos[sec_pos.Count - 1];
            }
            pos = 0;
            int spos = ds;
            byte[] adata = new byte[0];
            try
            {
                BitArray dest = new BitArray(d_end - d_start);
                while (pos < dest.Length)
                {
                    dest[pos] = source[spos];
                    spos++;
                    pos++;
                    if (spos == de) break;
                    if (spos == source.Count) spos = ds;
                }
                adata = Flip_Endian(Bit2Byte(dest, 0, pos));
            }
            catch { }
            a_headers.Add($"track length {adata.Length} start {ds / 8} end {de / 8}");

            return (adata, d_start, d_end, d_end - d_start, sectors, a_headers.ToArray());

            void Compare()
            {
                c = Flip_Endian(Bit2Byte(source, pos, 7 * 8));
                if (c[0] == 0x75)
                {
                    if (!start_found)
                    {
                        start_found = true;
                        d_start = pos;
                    }
                    string head = Hex_Val(c);
                    if (!headers.Any(x => x == head))
                    {
                        a_headers.Add($"pos ({pos / 8}) {head}");
                        sectors++;
                    }
                    else
                    {
                        if (!end_found)
                        {
                            end_found = true;
                            d_end = pos;
                        }
                    }
                    headers.Add(Hex_Val(c));
                    sec_pos.Add(pos);
                }
            }
        }
    }
}