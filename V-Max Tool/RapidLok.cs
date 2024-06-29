using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Security.Cryptography;
using System.Windows.Forms;
using System.Xml.Linq;

namespace V_Max_Tool
{
    public partial class Form1 : Form
    {
        private readonly byte[] RLok = new byte[] { 0xff, 0xff, 0x55, 0x7b }; /// Pattern used to detect if a track is RapidLok protected
        private readonly byte[] RLok1 = new byte[] { 0x75, 0x90, 0x09 }; /// Pattern used to detect if a track is RapidLok protected
        private readonly byte[] RLok_7b = new byte[] { 0x55, 0x7b, 0x7b, 0x7b, 0x7b, 0x7b, 0x7b, 0x7b, 0x7b, 0x7b };
        private readonly byte[,][] rl6_t18s3 = new byte[5, 2][];
        private readonly byte[,][] rl6_t18s6 = new byte[1, 2][];
        private readonly byte[,][] rl2_t18s9 = new byte[2, 2][];
        private readonly byte[,][] rl1_t18s9 = new byte[2, 2][];


        byte[] Patch_RapidLok(byte[] data, int sectors)
        {
            bool patched = false;
            bool exist = false;
            bool cksm = false;
            int pos = 0;
            string p = "Still protected.";
            bool[] rl6 = new bool[5];
            bool[] rl2 = new bool[2];
            bool[] rl1 = new bool[2];
            byte[] sector = new byte[0];
            BitArray source = new BitArray(Flip_Endian(data));

            //for (int i = 0; i < sectors; i++)
            //{
            //    (sector, cksm) = Decode_CBM_Sector(data, i, false, source, pos);
            //    File.WriteAllBytes($@"c:\test\rlt18_s{i}", sector);
            //}

            (exist, pos) = Find_Sector(source, 3);
            if (exist)
            {
                (sector, cksm) = Decode_CBM_Sector(data, 3, true, source, pos);
                for (int i = 0; i < sector.Length; i++)
                {
                    if (rl6_t18s3[0, 0].SequenceEqual(sector.Skip(i).Take(rl6_t18s3[0, 0].Length))) rl6[0] = true;
                    if (rl6_t18s3[1, 0].SequenceEqual(sector.Skip(i).Take(rl6_t18s3[1, 0].Length))) rl6[1] = true;
                    if (rl6_t18s3[2, 0].SequenceEqual(sector.Skip(i).Take(rl6_t18s3[2, 0].Length))) rl6[2] = true;
                    if (rl6_t18s3[3, 0].SequenceEqual(sector.Skip(i).Take(rl6_t18s3[3, 0].Length))) rl6[3] = true;
                    if (rl6_t18s3[4, 0].SequenceEqual(sector.Skip(i).Take(rl6_t18s3[4, 0].Length))) rl6[4] = true;
                }
                if (rl6.All(x => x == true))
                {
                    Patch_v6();
                    patched = true;
                }
            }
            if (!patched)
            {
                (exist, pos) = Find_Sector(source, 9);
                if (exist)
                {
                    (sector, cksm) = Decode_CBM_Sector(data, 9, false, source, pos);
                    for (int i = 0; i < sector.Length; i++)
                    {
                        if (rl2_t18s9[0, 0].SequenceEqual(sector.Skip(i).Take(rl2_t18s9[0, 0].Length))) rl2[0] = true;
                        if (rl2_t18s9[1, 0].SequenceEqual(sector.Skip(i).Take(rl2_t18s9[1, 0].Length))) rl2[1] = true;
                        if (rl1_t18s9[0, 0].SequenceEqual(sector.Skip(i).Take(rl1_t18s9[0, 0].Length))) rl1[0] = true;
                        if (rl1_t18s9[1, 0].SequenceEqual(sector.Skip(i).Take(rl1_t18s9[1, 0].Length))) rl1[1] = true;
                    }
                    if (rl2.All(x => x == true))
                    {
                        Patch_v2();
                        patched = true;
                        p = "Protection Removed!";
                    }
                    if (rl1.All(x => x == true))
                    {
                        Patch_v1();
                        patched = true;
                        p = "Protection Removed!";
                    }
                    data = Replace_CBM_Sector(data, 9, sector);
                }
            }
            int r6 = 0;
            int r2 = 0;
            int r1 = 0;
            for (int i = 0; i < rl6.Length; i++) if (rl6[i]) r6++;
            for (int i = 0; i < rl2.Length; i++) if (rl2[i]) r2++;
            for (int i = 0; i < rl1.Length; i++) if (rl1[i]) r1++;
            if (debug) Invoke(new Action(() => Text = $"rl4-7 markers found ({r6}/5) rl2 markers ({r2}/2) rl1 markers ({r1}/2) {p}"));
            return data;

            void Patch_v2()
            {
                for (int i = 0; i < sector.Length; i++)
                {
                    if (rl2_t18s9[0, 0].SequenceEqual(sector.Skip(i).Take(rl2_t18s9[0, 0].Length))) Buffer.BlockCopy(rl2_t18s9[0, 1], 0, sector, i, rl2_t18s9[0, 1].Length);
                    if (rl2_t18s9[1, 0].SequenceEqual(sector.Skip(i).Take(rl2_t18s9[1, 0].Length))) Buffer.BlockCopy(rl2_t18s9[1, 1], 0, sector, i, rl2_t18s9[1, 1].Length);
                }
            }

            void Patch_v1()
            {
                for (int i = 0; i < sector.Length; i++)
                {
                    if (rl1_t18s9[0, 0].SequenceEqual(sector.Skip(i).Take(rl1_t18s9[0, 0].Length))) Buffer.BlockCopy(rl1_t18s9[0, 1], 0, sector, i, rl1_t18s9[0, 1].Length);
                    if (rl1_t18s9[1, 0].SequenceEqual(sector.Skip(i).Take(rl1_t18s9[1, 0].Length))) Buffer.BlockCopy(rl1_t18s9[1, 1], 0, sector, i, rl1_t18s9[1, 1].Length);
                }
            }

            void Patch_v6()
            {
                {
                    for (int i = 0; i < sector.Length; i++)
                    {
                        if (rl6_t18s3[0, 0].SequenceEqual(sector.Skip(i).Take(rl6_t18s3[0, 0].Length))) Buffer.BlockCopy(rl6_t18s3[0, 1], 0, sector, i, rl6_t18s3[0, 1].Length);
                        if (rl6_t18s3[1, 0].SequenceEqual(sector.Skip(i).Take(rl6_t18s3[1, 0].Length))) Buffer.BlockCopy(rl6_t18s3[1, 1], 0, sector, i, rl6_t18s3[1, 1].Length);
                        if (rl6_t18s3[2, 0].SequenceEqual(sector.Skip(i).Take(rl6_t18s3[2, 0].Length))) Buffer.BlockCopy(rl6_t18s3[2, 1], 0, sector, i, rl6_t18s3[2, 1].Length);
                        if (rl6_t18s3[3, 0].SequenceEqual(sector.Skip(i).Take(rl6_t18s3[3, 0].Length))) Buffer.BlockCopy(rl6_t18s3[3, 1], 0, sector, i, rl6_t18s3[3, 1].Length);
                        if (rl6_t18s3[4, 0].SequenceEqual(sector.Skip(i).Take(rl6_t18s3[4, 0].Length))) Buffer.BlockCopy(rl6_t18s3[4, 1], 0, sector, i, rl6_t18s3[4, 1].Length);
                    }
                    data = Replace_CBM_Sector(data, 3, sector);
                    (exist, pos) = Find_Sector(source, 6);
                    if (exist)
                    {
                        (sector, cksm) = Decode_CBM_Sector(data, 6, true, source, pos);
                        for (int i = 0; i < sector.Length; i++)
                        {
                            if (rl6_t18s6[0, 0].SequenceEqual(sector.Skip(i).Take(rl6_t18s6[0, 0].Length))) Buffer.BlockCopy(rl6_t18s6[0, 1], 0, sector, i, rl6_t18s6[0, 1].Length);
                        }
                        data = Replace_CBM_Sector(data, 6, sector);
                        p = "Protection Removed!";
                    }
                }
            }
        }


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

        (byte[], int, int, int, int, string[]) RapidLok_Track_Info_New(byte[] data, int trk)
        {
            int d_start = 0;
            int d_end = 0;
            int sectors = 0;
            int pos = 0;
            int snc_cnt = 0;
            int sevenb_pos = 0;
            bool sevenb = false;
            bool trk_id = false;
            bool sync = false;
            bool start_found = false;
            bool end_found = false;
            string[] header = new string[0];
            byte[] pre_head = new byte[32];
            bool p_head = false;
            //int track = trk;
            //if (tracks > 42) track = (trk / 2) + 1; else track += 1;
            //int max_sectors = 12;
            //if (track > 17) max_sectors = 11;
            List<string> headers = new List<string>();
            List<string> a_headers = new List<string>();
            List<int> sec_pos = new List<int>();
            List<int> secds_pos = new List<int>();
            List<int> secde_pos = new List<int>();
            byte[] c;
            byte[] adata = new byte[0];
            BitArray source = new BitArray(Flip_Endian(data));
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
            int diff = 0;
            if (pos >= source.Length - 1 && !end_found)
            {
                if (pos >= source.Length) pos = source.Length - 1;
                d_end = pos;
                int c_len = pos - secds_pos[secds_pos.Count - 1] - 1;
                diff = ((583 + 21) * 8) - c_len;
                //diff = (583 * 8) - c_len;
                d_start -= diff;
            }
            BitArray temp = new BitArray(d_end - d_start);
            for (int i = 0; i < temp.Length; i++)
            {
                temp[i] = source[sevenb_pos];
                sevenb_pos++;
                if (sevenb_pos == d_end) sevenb_pos = d_start;
            }
            adata = Flip_Endian(Bit2Byte(temp));
            adata[adata.Length - 1] = 0xff;
            adata = Rotate_Right(adata, 20);

            a_headers.Add($"track length {adata.Length} start {d_start / 8} end {d_end / 8} pos {pos} {start_found} {end_found} dif {diff}");
            //a_headers.Add($"track length 0 start {d_start / 8} end {d_end / 8} pos {pos} {sec_pos.Count} {secds_pos.Count}");


            void Compare()
            {
                if (pos + (10 * 8) < source.Length)
                {
                    c = Flip_Endian(Bit2Byte(source, pos, 10 * 8));
                    if (sevenb && Match(RLok_7b, c))
                    {
                        if (sevenb)
                        {
                            end_found = true;
                            d_end = pos;
                        }
                        else
                        {
                            if (!sevenb)
                            {
                                sevenb = true;
                                sevenb_pos = pos;
                            }
                            if (!start_found)
                            {
                                start_found = true;
                                d_start = pos;
                            }
                        }
                    }
                    if (trk_id)
                    {
                        byte[] dct = Decode_CBM_GCR(c);
                        if (dct[0] == 0x08 && dct[1] < 42 && dct[2] < 21)
                        {
                            end_found = true;
                            d_end = pos;
                        }
                    }
                    if (!trk_id)
                    {
                        byte[] dct = Decode_CBM_GCR(c);
                        if (dct[0] == 0x08 && dct[1] < 42 && dct[2] < 21)
                        {
                            if (!start_found)
                            {
                                start_found = true;
                                d_start = pos;
                            }
                        }
                    }


                    if (c[0] == 0x75 && (c[4] == 0xd6 || c[5] == 0xed))
                    {
                        if (!start_found)
                        {
                            start_found = true;
                            d_start = pos;
                            //if (pos >= (32 * 8)) pre_head = Flip_Endian(Bit2Byte(source, pos - (32 * 8)));
                        }
                        string head = Hex_Val(c, 0, 6);
                        if (!headers.Any(x => x == head))
                        {
                            a_headers.Add($"pos ({pos / 8}) {head}");
                            sectors++;
                            int tpos = pos;
                            int tsnc = 0;
                            while (tpos < source.Length - 16)
                            {
                                if (source[tpos]) tsnc++;
                                if (!source[tpos])
                                {
                                    if (tsnc > 24)
                                    {
                                        byte[] cc = Flip_Endian(Bit2Byte(source, tpos, 16));
                                        if (cc[0] == 0x6b || (cc[0] == 0x55 && cc[1] == 0x55))
                                        {
                                            secds_pos.Add(tpos);
                                            secde_pos.Add(tpos + (583 * 8));
                                            break;
                                        }
                                    }
                                    tsnc = 0;
                                }
                                tpos++;
                            }
                        }
                        else
                        {
                            if (!end_found)
                            {
                                end_found = true;
                                d_end = pos;
                                a_headers.Add($"pos ({pos / 8}) {head} ** Repeat **");
                            }
                        }
                        headers.Add(Hex_Val(c, 0, 6));
                        sec_pos.Add(pos);
                    }
                }
            }

            return (adata, d_start, d_end, d_end - d_start, sectors, a_headers.ToArray());
            //return (data, d_start, d_end, d_end - d_start, sectors, a_headers.ToArray());
        }

        (byte[], int, int, int, int, string[]) RapidLok_Track_Info(byte[] data, int trk)
        {
            int track = trk;
            if (tracks > 42) track = (trk / 2) + 1; else track += 1;
            int max_sectors = 12;
            if (track > 17) max_sectors = 11;
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
            for (int i = 0; i < 630; i++)
            {
                //if ((data[i] == 0x75 && (data[i + 1] == 0x92 || data[i + 1] == 0x93)))
                if (data[i] == 0x75 && (data[i + 4] == 0xd6 || data[i + 5] == 0xed))
                {
                    if (i > 0 && i < 630) data = Rotate_Left(data, i - 4);
                    if (track == 1) File.WriteAllBytes($@"c:\test\rl t1", data);
                    break;
                }
            }
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
                    if (end_found) break;
                    snc_cnt = 0;
                    sync = false;
                }
                pos++;
            }
            int ds = d_start; ;
            int de = d_end;
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
                c = Flip_Endian(Bit2Byte(source, pos, 6 * 8));
                if (c[0] == 0x75 && (c[4] == 0xd6 || c[5] == 0xed))
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
                        //if (sectors == max_sectors)
                        //{
                        //    end_found = true;
                        //    d_end = pos;
                        //    int dif = d_end sec_pos[sec_pos.Count - 1];
                        //    //d_end = pos + ((583 + 20 + 16 + 5) << 3);
                        //}
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