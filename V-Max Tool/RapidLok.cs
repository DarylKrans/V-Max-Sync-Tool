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
        private byte[] rl_nkey = new byte[54];
        private int[] rl_7b = new int[35];


        void RL_Remove_Protection()
        {
            string rem;
            byte[] f = new byte[0];
            if (NDS.cbm.Any(x => x == 7) && RL_Fix.Checked)
            {
                int track = 17;
                if (tracks > 43) track = 34;
                (NDS.Track_Data[track], rem) = Patch_RapidLok(NDS.Track_Data[track]);
            }
        }

        (byte[], string) Patch_RapidLok(byte[] data)
        {
            string status = " [!]";
            string fixedStatus = " (Fixed)";
            byte[] sector = new byte[0];
            BitArray source = new BitArray(Flip_Endian(data));

            bool cksm;
            bool[] rl6 = new bool[5];
            bool[] rl2 = new bool[2];
            bool[] rl1 = new bool[2];

            if (TryPatchSector(3, rl6, true, rl6_t18s3, Patch_v6))
            {
                status = fixedStatus;
            }
            else if (TryPatchSector(9, rl2, false, rl2_t18s9, Patch_v2) || TryPatchSector(9, rl1, false, rl1_t18s9, Patch_v1))
            {
                status = fixedStatus;
                data = Replace_CBM_Sector(data, 9, sector);
            }

            LogSectorStatus(rl6, rl2, rl1);
            return (data, status);

            void Patch_v2()
            {
                PatchSector(rl2_t18s9);
            }

            void Patch_v1()
            {
                PatchSector(rl1_t18s9);
            }

            void Patch_v6()
            {
                PatchSector(rl6_t18s3);
                data = Replace_CBM_Sector(data, 3, sector);

                (bool exist, int pos) = Find_Sector(source, 6);
                if (exist)
                {
                    (sector, cksm) = Decode_CBM_Sector(data, 6, true, source, pos);
                    PatchSector(rl6_t18s6);
                    data = Replace_CBM_Sector(data, 6, sector);
                }
            }

            bool TryPatchSector(int sectorIndex, bool[] flags, bool decode, byte[,][] patterns, Action patchAction)
            {
                (bool exist, int pos) = Find_Sector(source, sectorIndex);
                if (!exist) return false;

                (sector, cksm) = Decode_CBM_Sector(data, sectorIndex, decode, source, pos);
                for (int i = 0; i < sector.Length; i++)
                {
                    for (int j = 0; j < patterns.GetLength(0); j++)
                    {
                        if (patterns[j, 0].SequenceEqual(sector.Skip(i).Take(patterns[j, 0].Length)))
                        {
                            flags[j] = true;
                        }
                    }
                }

                if (flags.All(x => x))
                {
                    patchAction();
                    return true;
                }

                return false;
            }

            void PatchSector(byte[,][] patterns)
            {
                for (int i = 0; i < sector.Length; i++)
                {
                    for (int j = 0; j < patterns.GetLength(0); j++)
                    {
                        if (patterns[j, 0].SequenceEqual(sector.Skip(i).Take(patterns[j, 0].Length)))
                        {
                            Buffer.BlockCopy(patterns[j, 1], 0, sector, i, patterns[j, 1].Length);
                        }
                    }
                }
            }

            void LogSectorStatus(bool[] rl_6, bool[] rl_2, bool[] rl_1)
            {
                int r6 = rl_6.Count(x => x);
                int r2 = rl_2.Count(x => x);
                int r1 = rl_1.Count(x => x);
                // Log sector status
                //Invoke(new Action(() => Text = $"rl6 ({r6}) rl2 ({r2}) rl1 ({r1}) "));
            }
        }

        (byte[], byte[]) RapidLok_Key_Fix(byte[] data, byte[] new_key = null)
        {
            byte[] newkey = FastArray.Init(7153, 0xff);
            byte[] key = new byte[256];
            byte[] _key = new byte[54];
            int s = 0;
            if (data[s] == 0x6b) s += 200;
            for (int i = s; i < data.Length; i++)
            {
                if ((data[i] == 0x6b) && (i + 256) < data.Length) // && !blank.Any(x => x == data[i + 1])) && i + 256 < data.Length)
                {
                    Buffer.BlockCopy(data, i, key, 0, 256);
                    break;
                }
            }
            if (new_key != null) Buffer.BlockCopy(new_key, 0, key, 0, new_key.Length);
            for (int i = 54; i < key.Length; i++)
            {
                if (key[i] != 0xff) key[i] = 0x00;
                if (i >= 250) key[i] = 0xff;
                if (i < 250) key[i] = 0x00;
            }
            Buffer.BlockCopy(key, 0, newkey, newkey.Length - 286, 256);
            Buffer.BlockCopy(key, 0, _key, 0, _key.Length);
            return (newkey, _key);
        }

        (byte[], int, int, int, int, int, string[]) RapidLok_Track_Info(byte[] data, int trk, bool build, byte[] track_ID, int rl_7b_len = 0)
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
            int nsb = rl_7b_len;
            int sb_sec = rl_7b_len;
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
            }
            catch { }
            if (build) Rebuild_RapidLok_Track();
            a_headers.Add($"track length {adata.Length} start {d_start / 8} end {d_end / 8} pos {pos} {start_found} {end_found} dif {diff}");
            return (adata, d_start, d_end, d_end - d_start, sectors, rl_7b_len, a_headers.ToArray());

            void Rebuild_RapidLok_Track()
            {
                int den = (track >= 18) ? 1 : 0; // Determine density index based on track number
                int rem = density[den];
                int sb_sync = 20;   // Sync length before the 0x7b sector
                int id_sync = 40;   // Sync length before the Track ID
                int sz_sync = 60;   // Sync length before the first sector
                byte[] os_sync = FastArray.Init(5, 0xff);
                byte gap = 0x00;
                byte pad = 0x55;
                using (MemoryStream buffer = new MemoryStream())
                {
                    BinaryWriter write = new BinaryWriter(buffer);
                    int cursec = (first_sector == sectors || first_sector == -1) ? 0 : first_sector;
                    if (sb_sec > 0)
                    {
                        write.Write(FastArray.Init(sb_sync, 0xff));
                        write.Write(pad);
                        write.Write((nsb == 0) ? FastArray.Init(sb_sec - 1, 0x7b) : FastArray.Init(nsb, 0x7b));
                    }
                    write.Write(FastArray.Init(id_sync, 0xff));
                    write.Write(Verify_Track_ID(tid));
                    write.Write(FastArray.Init(sz_sync - os_sync.Length, 0xff));
                    rem -= (int)buffer.Length + (sectors * (583 + 7 + (os_sync.Length * 2)));
                    rem /= (sectors * 2);
                    if (rem < 0) rem = 5;
                    byte[] sector_gap = FastArray.Init(rem, gap);
                    if (sectors >= 11)
                    {
                        for (int i = 0; i < sectors; i++)
                        {
                            if (sec_data?[cursec]?.Length == 583)
                            {
                                write.Write(os_sync);
                                write.Write(sec_head[cursec]);
                                write.Write(sector_gap);
                                write.Write(os_sync);
                                write.Write(sec_data[cursec]);
                                write.Write(sector_gap);
                            }
                            cursec = (cursec + 1) % sectors;
                        }
                        if (buffer.Length < density[den]) write.Write((FastArray.Init(density[den] - (int)buffer.Length, pad)));
                    }
                    adata = buffer.ToArray();
                }
            }

            byte[] Verify_Track_ID(byte[] tk_id)
            {
                if (track_ID.Length == 4)
                {
                    byte[] temp = Decode_CBM_GCR(tk_id);
                    if (temp[3] != track || (temp[4] != track_ID[0] && temp[5] != track_ID[1]))
                    {
                        byte[] ID = FastArray.Init(12, 0x55);
                        Buffer.BlockCopy(Build_BlockHeader(track, 0, track_ID), 0, ID, 0, 10);
                        return ID;
                    }
                }
                return tk_id;
            }

            void Compare()
            {
                const int bitBlockSize = 10 * 8;
                if (pos + bitBlockSize >= source.Length) return;
                c = Bit2Byte(source, pos, bitBlockSize);
                if (Match(RLok_7b, c)) HandleRLok7bMatch();
                else if (trk_id) HandleTrackId(c);
                else
                {
                    if (c[0] == 0x52 && tid.Length == 0)
                    {
                        tid = Bit2Byte(source, pos, 12 * 8);
                        a_headers.Add($"pos ({pos >> 3}) Track ID {Hex_Val(Decode_CBM_GCR(tid))}");
                    }
                }
                if (c[0] == 0x75 && (c[4] == 0xd6 || c[5] == 0xed)) HandleC0x75(c);
            }

            void HandleRLok7bMatch()
            {
                if (sevenb)
                {
                    end_found = true;
                    d_end = pos;
                }
                else
                {
                    sevenb = true;
                    sevenb_pos = pos;
                    if (sb_sec == 0)
                    {
                        FindSbSec();
                    }
                    first_sector = sectors;
                }

                if (!start_found)
                {
                    start_found = true;
                    d_start = pos;
                }
            }

            void HandleTrackId(byte[] d)
            {
                if (d[0] == 0x52)
                {
                    end_found = true;
                    d_end = pos;
                }
            }

            void FindSbSec()
            {
                int sl = 0;
                int tsnc = 0;
                for (int i = pos; i < source.Length; i++)
                {
                    if (source[i]) tsnc++;
                    else
                    {
                        if (tsnc >= 16)
                        {
                            sb_sec = (sl - tsnc - 8) >> 3;
                            a_headers.Add($"pos ({pos >> 3}) 0x7B sector Length {sb_sec} First sector = {first_sector + 1}");
                            break;
                        }
                        tsnc = 0;
                    }
                    sl++;
                }
            }

            void HandleC0x75(byte[] d)
            {
                if (!start_found)
                {
                    start_found = true;
                    d_start = pos;
                }
                string head = Hex_Val(d, 0, 7); //6
                if (!headers.Any(x => x == head))
                {
                    a_headers.Add($"pos ({pos / 8}) {head}");
                    sectors++;
                    if (build) BuildSectorData();
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
                headers.Add(Hex_Val(d, 0, 7)); //6
                byte[] shd = new byte[7];
                Buffer.BlockCopy(d, 0, shd, 0, 7);
                sec_head.Add(shd);
                sec_pos.Add(pos);
            }

            void BuildSectorData()
            {
                int tpos = pos;
                int tsnc = 0;
                while (tpos < source.Length - 16)
                {
                    if (source[tpos]) tsnc++;
                    else
                    {
                        if (tsnc > 24)
                        {
                            byte[] cc = Bit2Byte(source, tpos, 16);
                            byte[] sdt = DetermineSectorData(cc, tpos);
                            if (sdt != null)
                            {
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

            byte[] DetermineSectorData(byte[] cc, int tpos)
            {
                if (cc[0] == 0x6b || (cc[0] == 0x55 && cc[1] == 0x55))
                {
                    if (pos + (rl_seclen << 3) < source.Length)
                    {
                        if (cc[0] == 0x6b)
                        {
                            byte[] sdt = FastArray.Init(rl_seclen, 0x00);
                            sdt = Bit2Byte(source, tpos, sdt.Length << 3);
                            return sdt;
                        }
                    }
                    else if (cc[0] == 0x6b)
                    {
                        byte[] sdt = HandleIncompleteSector(cc, tpos);
                        return sdt;
                    }
                    if (cc[0] == 0x55) return FastArray.Init(rl_seclen, 0x55);
                }
                return null;
            }

            byte[] HandleIncompleteSector(byte[] cc, int tpos)
            {
                byte[] sdt = Bit2Byte(source, tpos);
                int dif = rl_seclen - sdt.Length;
                byte[] comp = new byte[16];
                Buffer.BlockCopy(sdt, sdt.Length - 16, comp, 0, 16);
                for (int i = 0; i < 8000; i++)
                {
                    byte[] find = Bit2Byte(source, i, comp.Length << 3);
                    if (Match(find, comp))
                    {
                        int rpos = i + (comp.Length << 3);
                        byte[] rem = Bit2Byte(source, rpos, dif << 3);
                        MemoryStream buffer = new MemoryStream();
                        BinaryWriter write = new BinaryWriter(buffer);
                        write.Write(sdt);
                        write.Write(rem);
                        sdt = buffer.ToArray();
                        break;
                    }
                }
                return sdt;
            }
        }
    }
}