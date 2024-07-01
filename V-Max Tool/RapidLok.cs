using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

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


        void RL_Remove_Protection()
        {
            string rem;
            byte[] f = new byte[0];
            if (NDS.cbm.Any(x => x == 7) && RL_Fix.Checked)
            {
                int track = 17;
                if (tracks > 43) track = 34;
                (NDS.Track_Data[track], rem) = Patch_RapidLok(NDS.Track_Data[track], NDS.sectors[track]);
            }
        }

        (byte[], string) Patch_RapidLok(byte[] data, int sectors)
        {
            bool patched = false;
            bool exist = false;
            bool cksm = false;
            int pos = 0;
            string p = " [!]";
            string f = " (Fixed)";
            bool[] rl6 = new bool[5];
            bool[] rl2 = new bool[2];
            bool[] rl1 = new bool[2];
            byte[] sector = new byte[0];
            BitArray source = new BitArray(Flip_Endian(data));
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
                    p = f; // " (Fixed)";
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
                        p = f; //" (Fixed)";
                    }
                    if (rl1.All(x => x == true))
                    {
                        Patch_v1();
                        patched = true;
                        p = f; //" (Fixed)";
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
            return (data, p);

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
            byte[] newkey = IArray(7153, 0xff);
            byte[] key = new byte[256];
            int s = 0;
            if (data[s] == 0x6b) s += 200;
            for (int i = s; i < data.Length; i++)
            {
                if ((data[i] == 0x6b && !blank.Any(x => x == data[i + 1])) && i + 256 < data.Length)
                {
                    Buffer.BlockCopy(data, i, key, 0, 256);
                    break;
                }
            }
            for (int i = 54; i < key.Length; i++)
            {
                if (key[i] != 0xff) key[i] = 0x00;
                if (i >= 250) key[i] = 0xff;
                if (i < 250) key[i] = 0x00;
            }
            Buffer.BlockCopy(key, 0, newkey, newkey.Length - 286, 256);
            return newkey;
        }

        (byte[], int, int, int, int, string[]) RapidLok_Track_Info(byte[] data, int trk)
        {
            int track = trk;
            if (tracks > 42) track = (trk / 2) + 1; else track += 1;
            int rl_seclen = 583;
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
            byte[] sb_sec = new byte[0];
            byte[] tid = new byte[0];
            int first_sector = -1;
            List<string> headers = new List<string>();
            List<string> a_headers = new List<string>();
            List<int> sec_pos = new List<int>();
            List<int> secds_pos = new List<int>();
            List<int> secde_pos = new List<int>();
            List<byte[]> sec_data = new List<byte[]>();
            List<byte[]> sec_head = new List<byte[]>();
            BitArray source = new BitArray(Flip_Endian(data));
            byte[] c;
            byte[] adata = new byte[0];
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
            try
            {
                if (pos >= source.Length - 1 && !end_found)
                {
                    if (pos >= source.Length) pos = source.Length - 1;
                    d_end = pos;
                    int c_len = pos - secds_pos[secds_pos.Count - 1] - 1;
                    diff = ((583 + 21) * 8) - c_len;
                    d_start -= diff;
                }
            } catch { }
            Build_New();
            a_headers.Add($"track length {adata.Length} start {d_start / 8} end {d_end / 8} pos {pos} {start_found} {end_found} dif {diff}");

            void Build_New()
            {
                int sb_sync = 20;   /// <- sets the sync length before the 0x7b sector
                int id_sync = 40;   /// <- sets the sync length before the Track ID
                int sz_sync = 60;   /// <- sets the sync length before the first sector
                int os_sync = 5;    /// <- sets the sync length for all other track sync
                int head_gap = 10;  /// <- sets the gap length after each sector header before the block sync
                int tail_gap = 8;   /// <- sets the tail gap after the block data before the next sector header
                byte snc = 0xff;
                byte gap = 0x00;
                byte pad = 0x55;
                int den = 0;
                if (track >= 18) den = 1;
                MemoryStream buffer = new MemoryStream();
                BinaryWriter write = new BinaryWriter(buffer);
                int cursec = first_sector;
                if (cursec == sectors || cursec == -1) cursec = 0;
                if (sb_sec.Length > 0)
                {
                    for (int j = 0; j < sb_sync; j++) write.Write((byte)snc);
                    write.Write(sb_sec); /// <- writes the 0x7b sector
                }
                if (tid.Length > 0)
                {
                    for (int j = 0; j < id_sync; j++) write.Write((byte)snc);
                    write.Write(tid);   /// <- writes the track ID
                }
                for (int i = 0; i < sectors; i++)
                {
                    if (i == 0) for (int j = 0; j < sz_sync; j++) write.Write((byte)snc);
                    else for (int j = 0; j < os_sync; j++) write.Write((byte)snc);
                    write.Write(sec_head[cursec]);
                    for (int j = 0; j < head_gap; j++) write.Write((byte)gap);
                    for (int j = 0; j < os_sync; j++) write.Write((byte)snc);
                    try { write.Write(sec_data[cursec]); } catch { } /// <- writes the block data and is error-handled in case the block data is missing
                    for (int j = 0; j < tail_gap; j++) write.Write((byte)gap);
                    cursec++;
                    if (cursec == sectors) cursec = 0;
                }
                while (buffer.Length < density[den]) write.Write((byte)pad); /// <- fills the track with padding byte to fit the track density
                adata = buffer.ToArray();
            }
            return (adata, d_start, d_end, d_end - d_start, sectors, a_headers.ToArray());

            void Compare()
            {
                if (pos + (10 * 8) < source.Length)
                {
                    c = Flip_Endian(Bit2Byte(source, pos, 10 * 8));
                    if (Match(RLok_7b, c))
                    {
                        if (sevenb)
                        {
                            end_found = true;
                            d_end = pos;
                        }
                        if (!sevenb)
                        {
                            sevenb = true;
                            sevenb_pos = pos;
                            if (sb_sec.Length == 0)
                            {
                                int sl = 0;
                                int tsnc = 0;
                                for (int i = pos; i < source.Length; i++)
                                {
                                    if (source[i]) tsnc++;
                                    if (!source[i])
                                    {
                                        if (tsnc >= 16)
                                        {
                                            sb_sec = Flip_Endian(Bit2Byte(source, pos, (sl - tsnc) - 8));
                                            sb_sec[sb_sec.Length - 1] = 0x7b;
                                            first_sector = sectors;
                                            a_headers.Add($"pos ({pos >> 3}) 0x7B sector Length {sb_sec.Length} First sector = {first_sector + 1}");
                                            break;
                                        }
                                        tsnc = 0;
                                    }
                                    sl++;
                                }
                            }
                        }
                        if (!start_found)
                        {
                            start_found = true;
                            d_start = pos;
                        }
                    }
                    if (trk_id)
                    {
                        if (c[0] == 0x52)
                        {
                            end_found = true;
                            d_end = pos;
                        }
                    }
                    if (!trk_id)
                    {
                        if (c[0] == 0x52)
                        {
                            if (tid.Length == 0)
                            {
                                tid = Flip_Endian(Bit2Byte(source, pos, 12 * 8));
                                a_headers.Add($"pos ({pos >> 3}) Track ID {Hex_Val(Decode_CBM_GCR(tid))}");
                            }
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
                        }
                        string head = Hex_Val(c, 0, 7); //6
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
                                            byte[] sdt = new byte[0];
                                            if (pos + (rl_seclen << 3) < source.Length)
                                            {
                                                if (cc[0] == 0x6b)
                                                {
                                                    sdt = IArray(rl_seclen);
                                                    sdt = Flip_Endian(Bit2Byte(source, tpos, sdt.Length << 3));
                                                }
                                            }
                                            if (pos + (rl_seclen << 3) > source.Length && cc[0] == 0x6b)
                                            {
                                                if (cc[0] != 0x55)
                                                {
                                                    sdt = Flip_Endian(Bit2Byte(source, tpos));
                                                    int dif = rl_seclen - sdt.Length;
                                                    byte[] comp = new byte[16];
                                                    Buffer.BlockCopy(sdt, sdt.Length - 16, comp, 0, 16);
                                                    byte[] find = new byte[0];
                                                    for (int i = 0; i < 8000; i++)
                                                    {
                                                        find = Flip_Endian(Bit2Byte(source, i, comp.Length << 3));
                                                        if (Match(find, comp))
                                                        {
                                                            int rpos = i + (comp.Length << 3);
                                                            byte[] rem = new byte[dif];
                                                            rem = Flip_Endian(Bit2Byte(source, rpos, dif << 3));
                                                            MemoryStream buffer = new MemoryStream();
                                                            BinaryWriter write = new BinaryWriter(buffer);
                                                            write.Write(sdt);
                                                            write.Write(rem);
                                                            sdt = buffer.ToArray();
                                                            break;
                                                        }
                                                    }
                                                }
                                            }
                                            if (cc[0] == 0x55) sdt = IArray(rl_seclen, 0x55);
                                            sec_data.Add(sdt);
                                            secds_pos.Add(tpos);
                                            secde_pos.Add(tpos + (rl_seclen * 8));
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
                        headers.Add(Hex_Val(c, 0, 7)); //6
                        byte[] shd = new byte[7];
                        Buffer.BlockCopy(c, 0, shd, 0, 7);
                        sec_head.Add(shd);
                        sec_pos.Add(pos);
                    }
                }
            }
        }
    }
}