using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

/// CBM Block Header structure
/// 8 plain bytes converted to 10 GCR bytes
/// 
/// Byte 0          0x08  (always 08)
/// byte 1          EOR of next 4 bytes (sector, track, ID byte 2, ID byte 1)
/// byte 2          Sector #
/// byte 3          Track #
/// byte 4          Disk ID byte 2
/// byte 5          Disk ID byte 1
/// byte 6          0x0f (filler to make full GCR chunk) not used
/// byte 7          0x0f (filler to make full GCR chunk) not used
///
/// CBM Block Data structure
/// 
/// byte 0          0x07 (always 07) Sector marker?
/// byte 1-256      (sector data) 256 bytes
/// byte 257        Checksum (0 ^ bytes 1-257)
/// byte 258-260    0x00 (not used)
/// 
/// Standard CBM sector
/// 
/// byte 0          (track of next sector in file) 0x00 if end of file
/// byte 1          (sector # of next sector in fiel) 0xff if end of file
/// byte 2-256      file data

/// CBM File Type bit settings
///     BIT: 7  6  5  4 | 3  2  1  0
/// DEL      -  -  -  - | 0  0  0  0
/// SEQ      -  -  -  - | 0  0  0  1
/// PRG      -  -  -  - | 0  0  1  0
/// USR      -  -  -  - | 0  0  1  1
/// REL      -  -  -  - | 0  1  0  0
/// OK       1  -  -  - | -  -  -  -
/// SPLAT    0  -  -  - | -  -  -  -
/// LOCK     -  1  -  - | -  -  -  -

/// BAM Layout (Block Allocation Map)
/// t18 s0
/// byte 0  Track#                      hex 0x12
/// byte 1  Next sector in directory    hex 0x01
/// byte 2  hex 0x41
/// byte 3  hex 0x00
/// byte 4 - 143 (map of free and allocated blocks) in sets of 4 bytes
///              byte 0 # of available sectors in track (starting at track 1)
///              bytes 1 - 3 set each bit to '1' for block available and '0' for used blocks up to the # of sectors in that track
///              repeat for each track up to 35
/// 




namespace V_Max_Tool
{
    public partial class Form1 : Form
    {
        //readonly bool write_dir = false;
        private readonly byte[] sz = { 0x52, 0xc0, 0x0f, 0xfc };
        private readonly byte cbm_gap = 0x55;
        int SelectionLength = 0;

        byte[] Rebuild_CBM(byte[] data, int sectors, byte[] Disk_ID, int t_density, int trk, int start, bool cyan = false)
        {
            if (!(data?.Length > 0)) return null;
            BitArray tk = new BitArray(Flip_Endian(data));
            sectors = sectors < Available_Sectors[trk] ? Available_Sectors[trk] : sectors;
            int dif = cyan ? 3 : 0;
            int errorCode = 1;
            int pos;
            int[] c = new int[] { 2, 3, 4, 5, 6 };
            bool alt = (NDS.cbm.Any(x => c.Any()));
            byte[] nosync = FastArray.Init(5, cbm_gap);
            byte[] noheader = FastArray.Init(10, cbm_gap);
            byte[] emptySector = Encode_CBM_GCR(Create_Empty_Sector());
            byte[] sync = FastArray.Init(5, 0xff);
            byte[] head_gap = SetSectorGap(sector_gap_density[t_density] - dif);
            byte[] tail_gap = SetSectorGap(sector_gap_density[t_density] + dif);
            //byte[] head_gap = SetSectorGap(7);
            //byte[] tail_gap = SetSectorGap(19);
            byte[] current_sector;
            byte[] block_header = new byte[10];
            start = !alt ? 0 : start;
            var buffer = new MemoryStream();
            var write = new BinaryWriter(buffer);
            for (int i = 0; i < sectors; i++)
            {
                (current_sector, errorCode, pos) = GetSectorWithErrorCode(data, i, false, null, tk, start);
                if (pos >= 0) block_header = Bit2Byte(tk, pos, 80);
                block_header = (alt && pos >= 0) ? Bit2Byte(tk, pos, 80) : Build_BlockHeader(trk, i, Disk_ID);
                write.Write((errorCode == 2 || errorCode == 3) ? nosync : sync);
                write.Write(errorCode == 2 ? noheader : block_header);
                write.Write(head_gap);
                write.Write(errorCode == 3 ? nosync : sync);
                write.Write((errorCode == 2 || errorCode == 4 || current_sector == null) ? emptySector : current_sector);
                if (i != sectors - 1) write.Write(tail_gap);
            }
            int rem = (int)(density[t_density] - buffer.Length);
            //write.Write(new byte[] { 0x55, 0x55, 0x55, 0x50, 0x00, 0x05, 0x55, 0x55, 0x55 });
            if (rem > 0)
            {
                write.Write(FastArray.Init(rem, cbm_gap));
            }

            return buffer.ToArray();
        }

        (int, int, int, int, string[], int, int[], int, byte[], int[], int, bool) CBM_Track_Info(byte[] data, bool checksums, int trk = -1)
        {
            int track = trk;
            if (tracks > 42) track = (trk / 2);
            string[] csm = new string[] { "OK", "Bad!" };
            string decoded_header;
            int sectors = 0;
            int[] s_st = new int[valid_cbm.Length];
            int pos = 0;
            int track_id = 0;
            int sync_count = 0;
            int data_start = 0;
            int sector_zero = 0;
            int data_end = 0;
            int total_sync = 0;
            int comp = 32;
            bool sec_zero = false;
            bool sync = false;
            bool start_found = false;
            bool end_found = false;
            bool dont_adj = true;
            byte[] dec_hdr;
            byte[] sec_hdr = new byte[10];
            byte[] Disk_ID = new byte[4];
            BitArray source = new BitArray(Flip_Endian(data));
            List<int> list = new List<int>();
            List<int> spos = new List<int>();
            List<string> dchr = new List<string>();
            List<string> headers = new List<string>();
            int[] s_pos = new int[22];
            byte[] d = new byte[4];
            var h = "";
            var sect = 0;
            Compare(pos);
            while (pos < source.Length - 32)
            {
                if (source[pos])
                {
                    sync_count++;
                    //if (sync_count == 15) sync = true;
                    if (sync_count == 10) sync = true;
                }
                if (!source[pos])
                {
                    if (sync) Compare(pos);
                    if (end_found) { add_total(); break; }
                    add_total();
                    sync = false;
                    sync_count = 0;
                }
                pos++;
            }
            if (!end_found)
            {
                if (start_found && !end_found && data_start == 0)
                {
                    data_end = (density[density_map[trk]] + 80) << 3;
                    dont_adj = false;
                }
                else data_end = pos;
                if (!start_found) data_start = s_pos[0];
                sectors = list.Count;
                if (!batch)
                {
                    try
                    {
                        headers.Add($"Track length ({(data_end - data_start) >> 3}) Sectors ({list.Count}) Avg sync length ({(total_sync + sync_count) / (list.Count * 2)} bits)");
                    }
                    catch { }
                }
                headers.Add($"{data_start} {data_end} {start_found} {end_found} {sectors}");
            }
            var len = (data_end - data_start);
            return (data_start, data_end, sector_zero, len, headers.ToArray(), sectors, s_st, total_sync, Disk_ID, s_pos, track_id, dont_adj);

            void add_total()
            {
                if (sync && sync_count < 80) total_sync += sync_count;
            }

            void Compare(int p)
            {
                d = Bit2Byte(source, pos, comp);
                bool skip = false;
                if (pos == 0 && d[0] == 0x52)
                {
                    int test = pos;
                    int sc = 0;

                    while (test < 3000)
                    {
                        if (source[test]) sc++;
                        if (!source[test])
                        {
                            if (sc > 12)
                            {
                                byte[] dd = Bit2Byte(source, test, comp);
                                if (dd[0] != 0x52) break;
                                if (dd[0] == 0x52) { skip = true; break; }
                            }
                            sc = 0;
                        }
                        test++;
                    }
                }
                if (d[0] == 0x52 && !skip && pos + 80 < source.Length)
                {
                    for (int i = 1; i < sz.Length; i++) d[i] &= sz[i];
                    h = Hex_Val(d);
                    //var hg = pos;
                    //bool shit = false;
                    //int ssinc = 0;
                    //while (!shit)
                    //{
                    //    if (source[hg]) ssinc++;
                    //    else
                    //    {
                    //        if (ssinc > 24)
                    //        {
                    //            shit = true;
                    //            break;
                    //        }
                    //        ssinc = 0;
                    //    }
                    //    hg++;
                    //}
                    //hg -= ssinc;
                    dec_hdr = Decode_CBM_GCR(Bit2Byte(source, pos, 80));
                    //dec_hdr = Decode_CBM_GCR(Bit2Byte(source, pos, hg - pos));
                    sect = Convert.ToInt32(dec_hdr[2]);

                    if (dec_hdr[2] < 21 && (dec_hdr[3] > 0 && dec_hdr[3] < 43))
                    {
                        if (!list.Any(s => s == sect))
                        {
                            dchr.Add($"pos {pos / 8} sec {sect} {Hex_Val(dec_hdr)}");
                            if (track_id == 0) track_id = Convert.ToInt32(dec_hdr[3]);
                            if (track_id < 1 || track_id > 42) track_id = 0;
                            decoded_header = Hex_Val(dec_hdr);
                            int chksum = 0;
                            string hdr_c = csm[0];
                            s_pos[dec_hdr[2]] = pos;
                            for (int i = 0; i < 4; i++) chksum ^= dec_hdr[i + 2];
                            if (chksum != dec_hdr[1]) hdr_c = csm[1];
                            string sz = "";
                            if (dec_hdr[2] == 0x00) sz = "*";
                            if (!start_found) { data_start = pos; start_found = true; }
                            if (!sec_zero && dec_hdr[2] == 0x00)
                            {
                                Buffer.BlockCopy(dec_hdr, 4, Disk_ID, 0, 4);
                                if (track == 17)
                                {
                                    NDS.t18_ID = new byte[4];
                                    Buffer.BlockCopy(dec_hdr, 4, NDS.t18_ID, 0, 4);
                                }
                                sector_zero = pos;
                                sec_zero = true;
                            }
                            string sec_c = csm[0];
                            if (checksums)
                            {
                                (byte[] s_dat, bool c) = Decode_CBM_Sector(data, sect, true, source);
                                if (!c) sec_c = csm[1];
                            }
                            //if (!batch) headers.Add($"Sector ({sect}){sz} Checksum ({sec_c}) pos ({p / 8}) Sync ({sync_count} bits) Header-ID [ {decoded_header.Substring(6, decoded_header.Length - 12)} ] Header ({hdr_c})");
                            if (!batch) headers.Add($"Sector ({sect}){sz} Header-ID [ {decoded_header} ] Header ({hdr_c})");
                        }
                        else
                        {
                            if (list.Any(s => s == sect))
                            {
                                string sz = "";
                                if (dec_hdr[2] == 0x00) sz = "*";
                                decoded_header = Hex_Val(dec_hdr);
                                int chksum = 0;
                                string hdr_c = csm[0];
                                for (int i = 0; i < 4; i++) chksum ^= dec_hdr[i + 2];
                                if (chksum != dec_hdr[1]) hdr_c = csm[1];
                                string sec_c = csm[0];
                                if (checksums)
                                {
                                    (byte[] s_dat, bool c) = Decode_CBM_Sector(data, sect, true, source, data_start);
                                    if (!c) sec_c = csm[1];
                                }
                                if (!batch)
                                {
                                    headers[0] = $"Sector ({sect}){sz} Checksum ({sec_c}) pos ({data_start / 8}) Sync ({sync_count} bits) Header-ID [ {decoded_header.Substring(6, decoded_header.Length - 12)} ] Header ({hdr_c})";
                                    headers.Add($"pos {p / 8} ** repeat ** {h}");
                                }
                                if (data_start == 0) data_end = pos;
                                else data_end = pos;
                                end_found = true;
                                if (!batch) headers.Add($"Track length ({(data_end - data_start) >> 3}) Sectors ({list.Count}) Avg sync length ({(total_sync + sync_count) / (list.Count * 2)} bits)");
                                sectors = list.Count;
                            }
                        }
                        list.Add(sect);
                    }
                }
            }
        }

        byte[] Adjust_Sync_CBM(byte[] data, int expected_sync, int minimum_sync, int exception, int Data_Start_Pos, int Data_End_Pos, int Sec_0, int Track_Len, int Track_Num, bool adjust = true)
        {
            if (Track_Num == Track_Num - 0) { };
            if (exception > expected_sync && expected_sync > minimum_sync)
            {
                byte[] tempp = Flip_Endian(data);
                BitArray s = new BitArray(Track_Len);
                BitArray z = new BitArray(tempp);
                var r = Sec_0;
                for (int i = 0; i < Track_Len; i++)
                {
                    s[i] = z[r];
                    r++;
                    if (r == Data_End_Pos) r = Data_Start_Pos;
                }

                BitArray d = new BitArray(s.Length + 4096);
                if (data.Length >= 5000)
                {
                    int sync_count = 0;
                    bool sync = false;
                    int dest_pos = 0;
                    for (int i = 0; i < s.Count; i++)
                    {
                        if (s[i])
                        {
                            sync_count++;
                            d[dest_pos] = true;
                            if (sync_count == minimum_sync && adjust) sync = true;
                        }
                        if (!s[i] && sync)
                        {
                            if (sync_count < expected_sync)
                            {
                                var m = expected_sync - sync_count;
                                for (int j = 0; j < m; j++) d[dest_pos + j] = true;
                                dest_pos += m;
                            }
                            if (expected_sync < sync_count && sync_count < exception)
                            {
                                dest_pos += (expected_sync - sync_count);
                                for (int j = dest_pos; j < dest_pos + (sync_count - expected_sync); j++) d[j] = false;
                            }
                        }
                        if (!s[i])
                        {
                            sync_count = 0;
                            sync = false;
                        }
                        dest_pos++;
                        if (dest_pos == d.Length) break;
                    }

                    if (dest_pos < d.Length) Pad_Bits(dest_pos, d.Length - dest_pos, d);
                    int bcnt;
                    var a = Math.Abs(((dest_pos >> 3) << 3) - dest_pos);
                    if (a != 0) bcnt = (dest_pos >> 3) + 1;
                    else bcnt = dest_pos >> 3;
                    var y = (bcnt * 8) - dest_pos;
                    if (y != 0)
                    {
                        for (int i = 0; i < ((expected_sync + 8) + 1); i++)
                        {
                            d[dest_pos + (y - i)] = d[dest_pos - (y + i)];
                        }
                        Pad_Bits(dest_pos - (y + expected_sync + 8), (8 - y) + 1, d);
                    }
                    return Rotate_Right(Bit2Byte(d, 0, bcnt << 3), 6);
                }
            }
            return data;

            //bool Match(byte[] s, int p, byte[] m)
            //{
            //    for (int i = 0; i < m.Length; i++) if (s[i + p] != m[i]) return false;
            //    return true;
            //}
        }

        (byte[], bool) Decode_CBM_Sector(byte[] data, int sector, bool decode, BitArray source = null, int pos = 0)
        {
            if (source == null) source = new BitArray(Flip_Endian(data));

            const int sectorDataLength = 325 * 8;
            byte[] tmp = null;
            bool sectorFound = false;
            bool sectorMarker = false;
            bool sync = false;
            int syncCount = 0;
            /// --- next line commented out to skip first sector found if it resides at position 0.  Uncomment to check position 0 for sector
            CompareSectorMarker();
            while (pos < source.Length - 32)
            {
                if (source[pos])
                {
                    syncCount++;
                    //if (syncCount == 12) sync = true;
                    if (syncCount == 8) sync = true;
                }
                else
                {
                    if (sync) sectorMarker = CompareSectorMarker();
                    if (pos + sectorDataLength < source.Length)
                    {
                        if (sync && sectorFound && !sectorMarker)
                        {
                            var (decodedSector, checksum) = DecodeSector();
                            if (!decode) return (decodedSector, checksum);

                            tmp = new byte[decodedSector.Length - 4];
                            Buffer.BlockCopy(decodedSector, 1, tmp, 0, tmp.Length);
                            return (tmp, checksum);
                        }
                    }

                    sync = false;
                    syncCount = 0;
                }
                pos++;
            }
            return (tmp ?? Array.Empty<byte>(), false);
            //return (tmp ?? new byte[0], false); /// <- For .Net 3.5 

            (byte[], bool) DecodeSector()
            {
                byte[] sectorBytes = Bit2Byte(source, pos, sectorDataLength);
                if (!decode) return (sectorBytes, false);

                byte[] decodedSector = Decode_CBM_GCR(sectorBytes);
                int checksum = 0;

                for (int i = 1; i < 257; i++)
                    checksum ^= decodedSector[i];

                bool isValid = checksum == decodedSector[257];
                return (decodedSector, isValid);
            }

            bool CompareSectorMarker()
            {
                int checkLength = decode ? 5 : 10;
                byte[] header = Bit2Byte(source, pos, checkLength * 8);

                if (header[0] == 0x52)
                {
                    byte[] decodedHeader = Decode_CBM_GCR(header);
                    if (decodedHeader[3] > 0 && decodedHeader[3] < 43 && decodedHeader[2] == sector)
                    {
                        sectorFound = true;
                        return true;
                    }
                    pos += sectorDataLength;
                }
                return false;
            }
        }

        byte[] Replace_CBM_Sector(byte[] data, int sector, byte[] new_sector, byte[] padding = null, int pos = 0)
        {
            if (new_sector.Length == 256)
                new_sector = Build_Sector(new_sector);

            BitArray source = new BitArray(Flip_Endian(data));
            BitArray sec = new BitArray(Flip_Endian(new_sector));
            BitArray pad = padding != null ? new BitArray(Flip_Endian(padding)) : new BitArray(0);

            //int pos = 0;
            const int sectorDataLength = 325 * 8;
            bool sector_found = false;
            bool sync = false;
            bool sector_marker;// = false;
            int sync_count = 0;
            sector_marker = Compare();
            while (pos < source.Length - 32)
            {
                if (source[pos])
                {
                    sync_count++;
                    if (sync_count == 15)
                        sync = true;
                }
                else
                {
                    if (sync) sector_marker = Compare();
                    if (pos + sectorDataLength < source.Length)
                    {
                        if (sync && sector_found && !sector_marker)
                        {
                            ReplaceSector(pos, sec, pad, source);
                            return Bit2Byte(source);
                        }
                    }
                    sync = false;
                    sync_count = 0;
                }
                pos++;
            }

            return data;

            bool Compare()
            {
                const int checkLength = 5;
                byte[] d = Bit2Byte(source, pos, checkLength * 8);

                if (d[0] == 0x52)
                {
                    byte[] g = Decode_CBM_GCR(d);
                    if (g[3] > 0 && g[3] < 43 && g[2] == sector)
                    {
                        sector_found = true;
                        return true;
                    }
                    pos += sectorDataLength;
                }
                return false;
            }

            void ReplaceSector(int startPos, BitArray sectorBits, BitArray paddingBits, BitArray sourceBits)
            {
                for (int i = 0; i < sectorBits.Count; i++)
                    sourceBits[startPos + i] = sectorBits[i];

                if (paddingBits.Length > 0)
                {
                    startPos += sectorBits.Length;
                    for (int i = 0; i < paddingBits.Count; i++)
                        sourceBits[startPos + i] = paddingBits[i];
                }
            }
        }

        byte[] Build_Sector(byte[] sect, bool badChecksum = false)
        {

            int checksum = 0;
            for (int i = 0; i < sect.Length; i++)
                checksum ^= sect[i];
            if (badChecksum) checksum = Flip_Endian(new byte[] { (byte)checksum })[0];

            using (MemoryStream buffer = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(buffer))
                {
                    writer.Write((byte)0x07);
                    writer.Write(sect);
                    writer.Write((byte)checksum);
                    writer.Write((byte)0x00);
                    writer.Write((byte)0x00);
                }
                return Encode_CBM_GCR(buffer.ToArray());
            }
        }

        (bool, int, byte[], bool) Find_Sector(BitArray source, int sector, int pos = -1, bool bit_pos = false)
        {
            if (pos < 0) pos = 0;
            byte[] dID;
            bool sector_found;
            bool cksm = false;
            (sector_found, dID, cksm) = Compare();
            if (!sector_found)
            {
                bool sync = false;
                int sync_count = 0;

                while (pos < source.Length - 32)
                {
                    if (source[pos])
                    {
                        sync_count++;
                        if (sync_count == 10) sync = true;
                    }
                    else
                    {
                        if (sync)
                        {
                            (sector_found, dID, cksm) = Compare();
                            if (sector_found) break;
                        }
                        sync = false;
                        sync_count = 0;
                    }
                    pos++;
                }
            }
            if (bit_pos) return sector_found ? (true, pos, dID, cksm) : (false, -1, null, cksm);
            return sector_found ? (true, pos / 8, dID, cksm) : (false, -1, null, cksm);

            (bool, byte[], bool) Compare()
            {
                int cl = 10;
                byte[] d = Bit2Byte(source, pos, cl * 8);
                if (d[0] == 0x52)
                {
                    byte[] g = Decode_CBM_GCR(d);
                    byte[] ID = new byte[2];
                    byte csm = 0x00;
                    for (int i = 2; i < 6; i++) csm ^= g[i];
                    cksm = (g[1] == csm);
                    Buffer.BlockCopy(g, 4, ID, 0, 2);
                    if (g[3] > 0 && g[3] < 43 && g[2] == sector)
                    {
                        return (true, ID, cksm);
                    }
                    pos += (320 * 8);
                }
                return (false, null, cksm);
            }
        }

        void Get_Disk_Directory()
        {
            string ret = "Disk Directory ID : n/a";
            var buff = new MemoryStream();
            var wrt = new BinaryWriter(buff);
            List<string> d_files = new List<string>();
            List<string> d_sec = new List<string>();
            List<string> filename = new List<string>();
            int halftrack;
            int track;
            int blocksFree = 0;
            SelectionLength = 0;

            if (tracks <= 42)
            {
                halftrack = 17;
                track = halftrack + 1;
            }
            else
            {
                halftrack = 34;
                track = (halftrack / 2) + 1;
            }

            if (NDS.cbm[halftrack] == 1)
            {
                List<string> list = new List<string>();
                byte[] nextSector = new byte[] { (byte)track, 0x00 };
                byte[] lastSector = new byte[2];
                int tnum = Convert.ToInt32(nextSector[0]);
                int snum = Convert.ToInt32(nextSector[1]);
                while ((tnum != 0 && tnum < 42) && !list.Any(x => x == Hex_Val(nextSector)))
                {
                    list.Add(Hex_Val(nextSector));
                    if (snum < 22 && !(tnum == 18 && snum == 0)) d_sec.Add(Hex_Val(nextSector).Replace("-", ""));
                    Buffer.BlockCopy(nextSector, 0, lastSector, 0, 2);
                    byte[] temp = new byte[0];
                    try
                    {
                        (temp, _) = Decode_CBM_Sector(NDG.Track_Data[halftrack], Convert.ToInt32(nextSector[1]), true);
                        if (temp.Length > 0)
                        {
                            Buffer.BlockCopy(temp, 0, nextSector, 0, nextSector.Length);
                            tnum = Convert.ToInt32(nextSector[0]);
                            snum = Convert.ToInt32(nextSector[1]);

                            if (tracks <= 42) halftrack = tnum - 1;
                            else halftrack = (tnum - 1) * 2;
                            wrt.Write(temp);
                        }
                        else
                        {
                            ret = "Error processing directory!";
                            break;
                        }
                    }
                    catch { }
                }

                if (buff.Length != 0)
                {
                    try
                    {
                        if (buff.Length < 257)
                        {
                            byte[] temp;
                            (temp, _) = Decode_CBM_Sector(NDG.Track_Data[halftrack], 1, true);
                            wrt.Write(temp);
                        }
                    }
                    catch { }

                    byte[] directory = buff.ToArray();

                    if (directory.Length >= 256)
                    {
                        for (int i = 0; i < 35; i++)
                        {
                            if (i != 17)
                                blocksFree += directory[4 + (i * 4)];
                        }

                        ret = $"0 \"";
                        SelectionLength = 0;
                        for (int i = 0; i < 23; i++)
                        {
                            if (directory[144 + i] != 0x00)
                            {
                                if (i != 16) ret += Encoding.ASCII.GetString(directory, 144 + i, 1).Replace('?', ' ');
                                else ret += "\"";
                                SelectionLength = ret.Length - 2;
                            }
                        }
                    }

                    if (directory.Length > 256)
                    {
                        for (int i = 1; i < directory.Length / 256; i++)
                        {
                            byte[] file = new byte[32];
                            for (int j = 0; j < 8; j++)
                            {
                                Buffer.BlockCopy(directory, 256 * i + (j * 32), file, 0, file.Length);
                                if (file[2] != 0x00)
                                {
                                    DiskDir.Entries++;
                                    file[0] = 0x00; file[1] = 0x00;
                                    d_files.Add(Hex_Val(file).Replace("-", ""));
                                    string sz = Get_FileName(file);
                                    filename.Add($"{sz}");
                                    ret += $"\n{sz}";
                                }
                            }
                        }
                        ret += $"\n{blocksFree} BLOCKS FREE.";
                        DiskDir.Entry = new byte[DiskDir.Entries][];
                        d_temp = new byte[DiskDir.Entries][];
                        DiskDir.Sectors = new byte[d_sec.Count][];
                        for (int i = 0; i < DiskDir.Entries; i++)
                        {
                            DiskDir.Entry[i] = Hex2Byte(d_files[i]);
                            d_temp[i] = Hex2Byte(d_files[i]);
                        }
                        for (int i = 0; i < d_sec.Count; i++)
                        {
                            DiskDir.Sectors[i] = Hex2Byte(d_sec[i]);
                        }
                        f_temp = filename.ToArray();
                        DiskDir.FileName = filename.ToArray();
                        Dir_Box.Items.Clear();
                        for (int k = 0; k < filename.Count; k++) Dir_Box.Items.Add(filename[k]);
                    }
                }
            }

            if (ret.Length > 0)
            {
                Dir_screen.Text = ret;
                Dir_screen.Select(2, SelectionLength);
                Dir_screen.SelectionBackColor = c64_text;
                Dir_screen.SelectionColor = C64_screen;
            }
        }

        void Create_Blank_Disk()
        {
            Invoke(new Action(() => Disable_Core_Controls(true)));
            if (BD_name.Text == "") BD_name.Text = "BLANK DISK";
            if (BD_id.Text == "") BD_id.Text = "00 2A";
            byte[] name = Encoding.ASCII.GetBytes($"{BD_name.Text}");
            fname = BD_name.Text;
            tracks = Convert.ToInt32(BD_tracks.Value);
            sl.DataSource = null;
            out_size.DataSource = null;
            Data_Box.Clear();
            Track_Info.Items.Clear();
            Set_Arrays(tracks);
            Set_ListBox_Items(true, false);
            byte[] id = Encoding.ASCII.GetBytes(BD_id.Text);
            byte[] Disk_ID = new byte[] { id[1], id[0], 0x0f, 0x0f };
            byte[] sync = new byte[] { 0xff, 0xff, 0xff, 0xff, 0xff };
            byte[] dir_s0 = Encode_CBM_GCR(T18S0());
            byte[] dir_s1 = Encode_CBM_GCR(T18S1());
            byte[] blank = Encode_CBM_GCR(Create_Empty_Sector());

            for (int i = 0; i < Convert.ToInt32(BD_tracks.Value); i++)
            {
                byte[] gap = SetSectorGap(sector_gap_length[i]);
                MemoryStream buffer = new MemoryStream();
                BinaryWriter write = new BinaryWriter(buffer);
                for (int j = 0; j < Available_Sectors[i]; j++)
                {
                    bool w = true;
                    write.Write(sync);
                    write.Write(Build_BlockHeader(i + 1, j, Disk_ID));
                    write.Write(gap);
                    write.Write(sync);
                    if (i == 17 && j == 0) { write.Write(dir_s0); w = false; }
                    if (i == 17 && j == 1) { write.Write(dir_s1); w = false; }
                    if (w) write.Write(blank);
                    write.Write(gap);
                }
                int rem = (int)(density[density_map[i]] - buffer.Length);
                if (rem > 0) write.Write(FastArray.Init(rem, cbm_gap));
                byte[] nt = buffer.ToArray();
                Set_Dest_Arrays(nt, i);
                NDS.Track_Data[i] = new byte[8192];
                Buffer.BlockCopy(NDA.Track_Data[i], 0, NDS.Track_Data[i], 0, 8192);
            }
            if (!DontThread)
            {
                if (Worker_Alt != null) Worker_Alt?.Abort();
                Worker_Alt = new Thread(new ThreadStart(() => Parse_Disk()));
                Worker_Alt.Start();
            }
            else Parse_Disk();

            void Parse_Disk()
            {
                Stopwatch pn = Parse_Nib_Data();
                Invoke(new Action(() =>
                {
                    Stopwatch po = Process_Nib_Data(true, false, false, false, true);
                    Get_Disk_Directory();
                    Set_BlockMap();
                    Set_ListBox_Items(false, false);
                    Import_File.Visible = false;
                    Adv_ctrl.Enabled = true;
                    Save_Disk.Visible = true;
                    Batch_List_Box.Visible = false;
                    linkLabel1.Visible = false;
                    Disable_Core_Controls(false);
                    if (DB_timers.Checked) label2.Text = $"New Disk Time - Parse : {pn.Elapsed.TotalMilliseconds} Process: {po.Elapsed.TotalMilliseconds} Total : {pn.Elapsed.TotalMilliseconds + po.Elapsed.TotalMilliseconds}";
                }));
            }

            byte[] T18S0()
            {
                int chksum = 0;
                byte[] title = new byte[27];
                byte[] ds0 = new byte[] { 0x12, 0x01, 0x41, 0x00 };
                for (int i = 0; i < title.Length; i++)
                {
                    if (i < 18) if (i < name.Length) title[i] = name[i]; else title[i] = 0xa0;
                    else if (i - 18 < id.Length) title[i] = id[i - 18]; else title[i] = 0xa0;
                }
                byte[] bam = Create_BAM();
                AllocBlock(bam, 17, 0, Set);
                AllocBlock(bam, 17, 1, Set);
                var buff = new MemoryStream();
                var wrt = new BinaryWriter(buff);
                wrt.Write((byte)0x07);
                wrt.Write(ds0);
                wrt.Write(bam);
                wrt.Write(title);
                while (buff.Length < 256) wrt.Write((byte)0x00);
                byte[] s = new byte[260];
                Buffer.BlockCopy(buff.ToArray(), 0, s, 0, (int)buff.Length);
                for (int i = 1; i < 257; i++) chksum ^= s[i];
                s[257] = (byte)chksum;
                return s;
            }

            byte[] T18S1()
            {
                int chksum = 0;
                var buff = new MemoryStream();
                var wrt = new BinaryWriter(buff);
                wrt.Write((byte)0x07);
                wrt.Write((byte)0x00);
                wrt.Write((byte)0xff);
                while (buff.Length < 260) wrt.Write((byte)0x00);
                byte[] t = buff.ToArray();
                for (int i = 1; i < 257; i++) chksum ^= t[i];
                t[257] = (byte)chksum;
                return t;
            }

            byte[] Create_BAM()
            {
                var buff = new MemoryStream();
                var wrt = new BinaryWriter(buff);
                byte[] bf = new byte[35];
                byte[][] used_sectors = new byte[35][];
                BitArray us = new BitArray(24);
                for (int i = 0; i < 35; i++)
                {
                    bf[i] = Available_Sectors[i];
                    used_sectors[i] = new byte[3];
                    for (int j = 0; j < Available_Sectors[i]; j++) us[j] = true;
                    used_sectors[i] = Flip_Endian(Bit2Byte(us));
                    wrt.Write(bf[i]);
                    wrt.Write(used_sectors[i]);
                }
                return buff.ToArray();
            }
        }

        void AllocBlock(byte[] bam, int track, int sector, bool set)
        {
            if (track < 35 && sector < Available_Sectors[track])
            {
                int getbyte = (sector / 8) + 1;
                int getbit = sector % 8;
                int pos = (track << 2) + getbyte;
                if (BlockAllocStatus(bam, track, sector) != set)
                {
                    // Use the 'set' flag to either allocate (clear bit) or free (set bit)
                    bam[pos] = set ? SetBit(bam[pos], getbit) : ClearBit(bam[pos], getbit);

                    // Adjust the block count
                    bam[track << 2] = (byte)(bam[track << 2] + (set ? 1 : -1));
                }
            }
        }

        bool BlockAllocStatus(byte[] bam, int track, int sector)
        {
            if (track < 35 && sector < Available_Sectors[track])
            {
                int getbyte = (sector / 8) + 1;
                int getbit = sector % 8;
                int pos = (track << 2) + getbyte;
                return GetBitStatus(bam[pos], getbit);
            }
            return true;
        }

        byte[] GetBam()
        {
            int dirtrack = tracks > 42 ? 34 : 17;
            if (NDS.cbm[dirtrack] == 1)
            {
                (byte[] data, _) = Decode_CBM_Sector(NDG.Track_Data[dirtrack], 0, true);
                byte[] bam = new byte[140];
                Buffer.BlockCopy(data, 4, bam, 0, bam.Length);
                return bam;
            }
            return null;
        }

        void UpdateBam(byte[] bam)
        {
            int dirtrack = tracks > 42 ? 34 : 17;
            if (NDS.cbm[dirtrack] == 1)
            {
                (byte[] data, _) = Decode_CBM_Sector(NDG.Track_Data[dirtrack], 0, true);
                Buffer.BlockCopy(bam, 0, data, 4, bam.Length);
                byte[] temp = Replace_CBM_Sector(NDG.Track_Data[dirtrack], 0, data);
                Set_Dest_Arrays(temp, dirtrack);
                Buffer.BlockCopy(NDA.Track_Data[dirtrack], 0, NDS.Track_Data[dirtrack], 0, MAX_TRACK_SIZE);
            }
        }

        void Set_BlockMap()
        {
            Blk_pan.Visible = false;
            ResetAllBlocks();
            byte[] bam = GetBam();
            bool vbam = bam != null;
            string usedsec = string.Empty;
            for (int i = 0; i < tracks; i++)
            {
                int trk = tracks > 42 ? (i / 2) : i;
                if (NDS.cbm[i] == 1)
                {
                    int validSectors = Available_Sectors[trk];
                    int sectors = NDS.sectors[i] < validSectors ? validSectors : NDS.sectors[i];
                    int[] c = new int[] { 2, 3, 4, 5, 6 };
                    bool alt = (NDS.cbm.Any(x => c.Any()));
                    int start = trk == 17 || alt ? 0 : NDS.D_Start[i];
                    byte[] data = trk == 17 || alt ? new byte[NDG.Track_Data[i].Length] : new byte[MAX_TRACK_SIZE];
                    Buffer.BlockCopy(trk == 17 || alt ? NDG.Track_Data[i] : NDS.Track_Data[i], 0, data, 0, data.Length);
                    BitArray tk = new BitArray(Flip_Endian(trk == 17 ? NDG.Track_Data[i] : NDS.Track_Data[i]));
                    for (int j = 0; j < 21; j++)
                    {
                        if (j < sectors)
                        {
                            bool valid = j < Available_Sectors[trk];
                            (_, int errorCode, _) = GetSectorWithErrorCode(data, j, true, null, tk, start);
                            bool error = errorCode > 1;
                            bool available = BlockAllocStatus(bam, trk, j);
                            usedsec = !available ? "Block Allocated (Used)" : "Block Available (Free)";
                            usedsec += (error ? $"\nError {c1541error[errorCode]}" : string.Empty);
                            Color color = Color.FromArgb(valid && trk < 35 ? 255 : 100, error ? 200 : 30, error ? 30 : !available ? 200 : 75, 30);
                            BlkMap_bam[trk][j].BackColor = color;
                            tips.SetToolTip(BlkMap_bam[trk][j], $"Track {trk + 1} Sector {j + 1}\n{usedsec}" + (errorCode == 1 ? $"\n{ErrorCodes[errorCode]}" : ""));
                            BlkMap_bam[trk][j].Visible = true;
                        }
                        else
                        {
                            Color color = Color.FromArgb(30, 100, 100, 100);
                            tips.SetToolTip(BlkMap_bam[trk][j], string.Empty);
                            BlkMap_bam[trk][j].BackColor = color;
                            BlkMap_bam[trk][j].Visible = true;
                        }
                    }
                }
                else
                {
                    try
                    {
                        var fmt = NDS.cbm[i];
                        if (fmt < secF.Length - 1)
                        {
                            int sec = Sectors_by_density[(Get_Density(NDG.Track_Data[i].Length))];
                            for (int j = 0; j < 21; j++)
                            {
                                Color color = fmt < 2 || fmt == secF.Length - 1 || j >= sec ? Color.FromArgb(30, 100, 100, 100) : Color.FromArgb(200, 100, 30, 100);
                                BlkMap_bam[trk][j].Visible = true;
                                BlkMap_bam[trk][j].BackColor = color;
                                tips.SetToolTip(BlkMap_bam[trk][j], (fmt > 0 && fmt < secF.Length - 1) ? j < sec ? $"Track {trk + 1} {secF[NDS.cbm[i]]}" : string.Empty : string.Empty);
                            }
                        }
                    }
                    catch { }
                }
                if (tracks > 42) i++;
            }
            Blk_pan.Visible = true;
        }

        void AddFileToDisk(byte[] prg, string filename, byte[] bam, byte[][] freesec)
        {
            int blocks = prg.Length % 254 == 0 ? prg.Length / 254 : prg.Length / 254 + 1;
            if (freesec != null)
            {
                blocks = blocks > freesec.Length ? freesec.Length : blocks;

                if (blocks <= freesec.Length)
                {
                    List<int> ttrks = new List<int>();
                    int prevTrack = -1;
                    int curtrack = -1;
                    byte[] temp = new byte[0];
                    for (int i = 0; i < blocks; i++)
                    {
                        curtrack = tracks > 42 ? (freesec[i][0]) << 1 : freesec[i][0];
                        if (prevTrack != curtrack)
                        {
                            if (!(prevTrack < 0)) Set_Dest_Arrays(temp, prevTrack);
                            temp = new byte[NDG.Track_Data[curtrack].Length];
                            Buffer.BlockCopy(NDG.Track_Data[curtrack], 0, temp, 0, temp.Length);
                            prevTrack = curtrack;
                        }
                        if (!ttrks.Contains(curtrack)) ttrks.Add(curtrack);
                        int track = freesec[i][0];
                        int cursec = freesec[i][1];

                        try
                        {
                            byte[] secData = FastArray.Init(256, 00);
                            int len = (i + 1) * 254 > prg.Length ? prg.Length - (i * 254) : 254;
                            Buffer.BlockCopy(prg, i * 254, secData, 2, len);
                            secData[0] = i == blocks - 1 ? (byte)0x00 : (byte)(freesec[i + 1][0] + 1);
                            secData[1] = i == blocks - 1 ? (byte)0xff : (byte)freesec[i + 1][1];
                            temp = Replace_CBM_Sector(temp, cursec, secData);
                            AllocBlock(bam, track, cursec, Set);
                        }
                        catch { }
                    }
                    Set_Dest_Arrays(temp, curtrack);
                    foreach (int a in ttrks)
                    {
                        Buffer.BlockCopy(NDA.Track_Data[a], 0, NDS.Track_Data[a], 0, MAX_TRACK_SIZE);
                    }
                    UpdateBam(bam);
                }
            }
        }

        void AddEntryToDirectory(byte[] newfile)
        {
            int ht = tracks > 42 ? 2 : 1;
            int dirtrack = 17 * ht;
            int atrack = 17;
            bool newsector = false;
            bool added = false;
            if (NDS.cbm[dirtrack] == 1)
            {
                int nexttrack = dirtrack;
                int nextsector = 1;
                int prevsector = 0;
                int i = nextsector;
                while (!newsector)
                {
                    int curtrack = nexttrack;
                    int cursector = nextsector;
                    (byte[] cursec, _) = Decode_CBM_Sector(NDG.Track_Data[curtrack], nextsector, true);
                    if (cursec != null && cursec.Length == 256)
                    {
                        nexttrack = Convert.ToInt32(cursec[0] - 1);
                        nextsector = cursec[1];
                        if (nextsector == prevsector) newsector = true;
                        else prevsector = nextsector;
                        if (nexttrack == 0 || nexttrack > 35 || nextsector > Available_Sectors[atrack]) newsector = true;
                        else
                        {
                            nexttrack *= ht;
                            atrack = nexttrack + 1;
                        }
                        if (i > 0)
                        {
                            int pos = 2;
                            for (int j = 0; j < 8; j++)
                            {
                                int tpos = pos + (j * 32);
                                if (cursec[tpos] == 0x00 && !added)
                                {
                                    Buffer.BlockCopy(newfile, 0, cursec, tpos, 30);
                                    byte[] temp = Replace_CBM_Sector(NDG.Track_Data[curtrack], cursector, cursec);
                                    Set_Dest_Arrays(temp, curtrack);
                                    Buffer.BlockCopy(NDA.Track_Data[curtrack], 0, NDS.Track_Data[curtrack], 0, MAX_TRACK_SIZE);
                                    added = true;
                                }
                                if (added) break;
                            }

                        }
                    }
                    if (added) break;
                    if (newsector)
                    {
                        int intlv = Sec_Interleave.SelectedIndex;
                        byte[] tbam = GetBam();
                        HashSet<int> processedSectors = new HashSet<int>();
                        int tsec = Available_Sectors[17];
                        int newsec = (cursector * sectorInterleave[intlv]) % tsec;
                        for (int k = 1; k < tsec; k++)
                        {
                            while (processedSectors.Contains(newsec))
                            {
                                newsec = (newsec + 1) % tsec; // Increment sec and wrap around if needed
                            }
                            if (BlockAllocStatus(tbam, 17, newsec))
                            {
                                cursec[0] = (byte)(18);
                                cursec[1] = (byte)(newsec);
                                byte[] stemp = Replace_CBM_Sector(NDG.Track_Data[dirtrack], cursector, cursec);
                                byte[] nsector = FastArray.Init(256, 0x00);
                                nsector[1] = 0xff;
                                Buffer.BlockCopy(newfile, 0, nsector, 2, 30);
                                stemp = Replace_CBM_Sector(stemp, newsec, nsector);
                                Set_Dest_Arrays(stemp, dirtrack);
                                Buffer.BlockCopy(NDA.Track_Data[dirtrack], 0, NDS.Track_Data[dirtrack], 0, MAX_TRACK_SIZE);
                                AllocBlock(tbam, 17, newsec, Set);
                                UpdateBam(tbam);
                                added = true;
                            }
                            if (added) break;
                            processedSectors.Add(newsec);
                        }
                    }
                    i++;
                }
            }
        }

        List<byte[]> Get_Directory_Entries()
        {
            int ht = tracks > 42 ? 2 : 1;
            int dirtrack = 17 * ht;
            int atrack = 17;
            bool stop = false;
            List<byte[]> entries = new List<byte[]>();
            if (NDS.cbm[dirtrack] == 1)
            {
                int nexttrack = dirtrack;
                int nextsector = 0;
                int prevsector = 0;
                int i = 0;
                while (!stop)
                {
                    (byte[] cursec, _) = Decode_CBM_Sector(NDG.Track_Data[nexttrack], nextsector, true);
                    if (cursec != null && cursec.Length == 256)
                    {
                        nexttrack = Convert.ToInt32(cursec[0] - 1);
                        nextsector = cursec[1];
                        if (nextsector == prevsector) stop = true;
                        else prevsector = nextsector;
                        if (nexttrack == 0 || nexttrack > 35 || nextsector > Available_Sectors[atrack]) stop = true;
                        else
                        {
                            nexttrack *= ht;
                            atrack = nexttrack + 1;
                        }
                        if (i > 0)
                        {
                            int pos = 2;
                            for (int j = 0; j < 8; j++)
                            {
                                int tpos = pos + (j * 32);
                                if (cursec[tpos] != 0x00)
                                {
                                    byte[] entry = new byte[30];
                                    Buffer.BlockCopy(cursec, tpos, entry, 0, 30);
                                    entries.Add(entry);
                                }
                            }

                        }
                    }
                    if (stop) break;
                    i++;
                }
                if (entries.Count > 0)
                {
                    return entries;
                }
            }
            return entries;
        }

        (byte[], byte[][]) GetAvailableSectors(bool extra_sectors = false)
        {
            byte[] bam = GetBam();
            int ht = tracks > 42 ? 2 : 1;
            int intlv = Sec_Interleave.SelectedIndex;
            bool rev = false;
            int strk = rev ? 18 : 16;
            int lastSector = 0; // Tracks the last sector processed
            //int strk = 16;
            List<byte[]> available = new List<byte[]>();
            if (bam != null)
            {
                for (int i = 0; i < 35; i++)
                {
                    if (NDS.cbm[i * ht] == 1)
                    {
                        HashSet<int> processedSectors = new HashSet<int>();
                        int max = Available_Sectors[strk];
                        int sec = (lastSector + sectorInterleave[intlv]) % max; // Start from the interleave of the last sector

                        for (int j = 0; j < Available_Sectors[strk]; j++)
                        {
                            while (processedSectors.Contains(sec))
                            {
                                sec = (sec + 1) % max; // Increment sec and wrap around if needed
                            }

                            if (BlockAllocStatus(bam, strk, sec))
                            {
                                available.Add(new byte[] { (byte)strk, (byte)sec });
                            }

                            processedSectors.Add(sec);
                            lastSector = (sec - 1) % max; // Update the last processed sector
                            sec = (sec + sectorInterleave[intlv]) % max; // Continue with the interleave for the next sector
                        }
                    }

                    strk += rev ? 1 : -1;

                    if (strk < 0)
                    {
                        strk = 18;
                        rev = true;
                    }

                    if (strk > 34 && extra_sectors)
                    {
                        strk = 17;
                    }
                }
                //for (int i = 0; i < 35; i++)
                //{
                //    if (NDS.cbm[i * ht] == 1)
                //    {
                //        HashSet<int> processedSectors = new HashSet<int>();
                //        int max = Available_Sectors[strk];
                //        for (int j = 0; j < Available_Sectors[strk]; j++)
                //        {
                //            int sec = (j * sectorInterleave[intlv]) % max;
                //            while (processedSectors.Contains(sec))
                //            {
                //                sec = (sec + 1) % max; // Increment sec and wrap around if needed
                //                if (sec > max) sec = 0;
                //            }
                //            if (BlockAllocStatus(bam, strk, sec))
                //            {
                //                available.Add(new byte[] { (byte)strk, (byte)sec });
                //            }
                //            processedSectors.Add(sec);
                //        }
                //    }
                //    strk += rev ? 1 : -1;
                //    if (strk < 0)
                //    {
                //        strk = 18;
                //        rev = true;
                //    }
                //    //if (strk < 0)
                //    //{
                //    //    strk = 16;
                //    //    rev = false;
                //    //}
                //    //if (i == 34 && extra_sectors) strk = 17;
                //    if (strk > 34 && extra_sectors) strk = 17;
                //}
            }
            if (available.Count > 0)
            {
                byte[][] freeSectors = new byte[available.Count][];
                for (int i = 0; i < available.Count; i++)
                {
                    freeSectors[i] = new byte[2];
                    freeSectors[i] = available[i];
                }
                return (bam, freeSectors);
            }
            return (bam, new byte[0][]);
        }

        void ProcessNewFiletoImage(string[] files)
        {
            bool fastload = true;
            DontThread = true;
            if (tracks == 0) RunBusy(Create_Blank_Disk);
            DontThread = false;
            bool refresh = false;
            foreach (string file in files)
            {
                byte[] data = File.ReadAllBytes(file);
                string filename = Path.GetFileName(file);
                long length = new FileInfo(file).Length;
                (byte[] bam, byte[][] freesec) = GetAvailableSectors(true);
                List<string> frsec = new List<string>();
                for (int i = 0; i < freesec.Length; i++)
                {
                    frsec.Add($"{Convert.ToInt32(freesec[i][0])}, {Convert.ToInt32(freesec[i][1])}");
                }
                File.WriteAllLines($@"c:\test\bamtest", frsec.ToArray());
                length = length % 254 == 0 ? length / 254 : length / 254 + 1;
                if (length <= freesec.Length)
                {
                    List<byte[]> entries = Get_Directory_Entries();
                    if (entries == null || entries.Count < 144)
                    {
                        List<string> entNames = new List<string>();
                        foreach (var entry in entries)
                        {
                            entNames.Add(ExtractFileName(entry));
                        }
                        int blocks = data.Length % 254 == 0 ? data.Length / 254 : data.Length / 254 + 1;
                        byte[] newfile = CreateFileEntry(filename.ToUpper(), blocks, freesec[0][0] + 1, freesec[0][1]);
                        string newfilename = ExtractFileName(newfile);

                        if (!entNames.Contains(newfilename) && newfile != null)
                        {
                            if (fastload)
                            {
                                AddFastLoad(newfilename);
                                (bam, freesec) = GetAvailableSectors(true);
                                newfile = CreateFileEntry(filename.ToUpper(), blocks, freesec[0][0] + 1, freesec[0][1]);
                            }
                            AddFileToDisk(data, filename, bam, freesec);
                            AddEntryToDirectory(newfile);

                            refresh = true;
                        }
                        else
                        {
                            //MessageBox.Show($"File already exists in directory");
                            using (Message_Center center = new Message_Center(this)) // center message box
                            {
                                string s = $"File {newfilename} already exists in directory";
                                string t = "Error!";
                                MessageBox.Show(s, t, MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                        }
                    }
                    else
                    {
                        if (entries.Count >= 144)
                        {
                            using (Message_Center center = new Message_Center(this)) // center message box
                            {
                                string t = "Error!";
                                string s = $"Directory is FULL!";
                                MessageBox.Show(s, t, MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                            break;
                        }
                    }

                    void AddFastLoad(string newfilename)
                    {
                        byte[] fl = new byte[fastloader.Length];
                        fastloader.CopyTo(fl, 0);
                        for (int i = 0; i < newfilename.Length; i++) fl[fldOffset + i] = (byte)newfilename[i];
                        fl[fldOffset + 16] = (byte)(newfilename.Length);
                        string flname = $"BOOT.{newfilename}";
                        if (newfilename.Length > 16) flname = flname.Substring(0, 16);
                        byte[] fld = CreateFileEntry(flname, 5, freesec[0][0] + 1, freesec[0][1]);
                        AddFileToDisk(fl, flname, bam, freesec);
                        AddEntryToDirectory(fld);
                    }
                }
                else
                {
                    using (Message_Center center = new Message_Center(this)) // center message box
                    {
                        string s = $"Not enough free space\nBlocks needed {length}\nBlocks free {freesec.Length}";
                        string t = "Error!";
                        MessageBox.Show(s, t, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            if (refresh)
            {
                Clear_Out_Items();
                Process_Nib_Data(true, false, false, true);
                Default_Dir_Screen();
                Get_Disk_Directory();
                Set_BlockMap();
                linkLabel1.Visible = false;
                Save_Disk.Visible = true;
                Source.Visible = Output.Visible = true;
                label1.Text = $"{fname}{fext}";
                M_render.Enabled = true;
                Import_File.Visible = false;
                Adv_ctrl.Enabled = true;
                Blk_pan.Enabled = true;
                Disable_Core_Controls(false);
            }
        }

        string ExtractFileName(byte[] file)
        {
            string fName = "";
            for (int k = 3; k < 19; k++)
            {
                if (file[k] != 0xa0)
                {
                    if (file[k] != 0x00) fName += Encoding.ASCII.GetString(file, k, 1);
                    else fName += "@";
                }
            }
            return fName;
        }

        byte[] Decode_CBM_GCR(byte[] gcr)
        {
            byte[] plain = new byte[(gcr.Length / 5) << 2];
            for (int i = 0; i < gcr.Length / 5; i++)
            {
                int baseIndex = i * 5;
                byte b1 = gcr[baseIndex];
                byte b2 = gcr[baseIndex + 1];
                plain[(i << 2) + 0] = CombineNibbles((byte)(b1 >> 3), (byte)(((b1 << 2) | (b2 >> 6)) & 0x1f));
                b1 = gcr[baseIndex + 1];
                b2 = gcr[baseIndex + 2];
                plain[(i << 2) + 1] = CombineNibbles((byte)((b1 >> 1) & 0x1f), (byte)(((b1 << 4) | (b2 >> 4)) & 0x1f));
                b1 = gcr[baseIndex + 2];
                b2 = gcr[baseIndex + 3];
                plain[(i << 2) + 2] = CombineNibbles((byte)(((b1 << 1) | (b2 >> 7)) & 0x1f), (byte)((b2 >> 2) & 0x1f));
                b1 = gcr[baseIndex + 3];
                b2 = gcr[baseIndex + 4];
                plain[(i << 2) + 3] = CombineNibbles((byte)(((b1 << 3) | (b2 >> 5)) & 0x1f), (byte)(b2 & 0x1f));
            }
            return plain;

            byte CombineNibbles(byte hnib, byte lnib)
            {
                hnib = GCR_decode_high[hnib];
                lnib = GCR_decode_low[lnib];
                if (hnib == 0xff || lnib == 0xff) return 0x00;
                else return (byte)(hnib | lnib);
            }
        }

        byte[] Encode_CBM_GCR(byte[] plain)
        {
            int l = plain.Length >> 2;
            byte[] gcr = new byte[l * 5];
            for (int i = 0; i < l; i++)
            {
                int baseIndex = i << 2;
                byte p1 = plain[baseIndex];
                byte p2 = plain[baseIndex + 1];
                byte p3 = plain[baseIndex + 2];
                byte p4 = plain[baseIndex + 3];
                gcr[0 + (i * 5)] = (byte)((GCR_encode[p1 >> 4] << 3) | (GCR_encode[p1 & 0x0f] >> 2));
                gcr[1 + (i * 5)] = (byte)((GCR_encode[p1 & 0x0f] << 6) | (GCR_encode[p2 >> 4] << 1) | (GCR_encode[p2 & 0x0f] >> 4));
                gcr[2 + (i * 5)] = (byte)((GCR_encode[p2 & 0x0f] << 4) | (GCR_encode[p3 >> 4] >> 1));
                gcr[3 + (i * 5)] = (byte)((GCR_encode[p3 >> 4] << 7) | (GCR_encode[p3 & 0x0f] << 2) | (GCR_encode[p4 >> 4] >> 3));
                gcr[4 + (i * 5)] = (byte)((GCR_encode[p4 >> 4] << 5) | GCR_encode[p4 & 0x0f]);
            }
            return gcr;
        }
    }
}