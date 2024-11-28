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
        private readonly byte[] ps1 = { 0xeb, 0xd7, 0xaa, 0x55, 0xaa }; /// <- Piraye Slayer v1 secondary check
        private readonly byte[] ps2 = { 0xd7, 0xd7, 0xeb, 0xcc, 0xad }; /// <- Pirate Slayer v1/2 check
        private readonly byte[] ramb = new byte[] { 0xbe, 0x55, 0x5b, 0xe5, 0x55 }; /// <- RainbowArts / MagicBytes key signature found on t36
        private readonly byte[] blank = new byte[] { 0x00, 0x11, 0x22, 0x44, 0x45, 0x14, 0x12, 0x51, 0x88, 0x18, 0x31, 0x23 };
        private readonly byte[] gmt = new byte[] { 0x69, 0x50, 0x50, 0xa0, 0xa0 };
        private readonly byte[] ssp = new byte[] { 0xff, 0x56, 0x56, 0xa3, 0xa3 };

        byte[] Pirate_Slayer(byte[] data)
        {
            byte[] dataend = new byte[] { 0x55, 0xae, 0x9b, 0x55, 0xad, 0x55, 0xcb, 0xae, 0x6b, 0xab, 0xad, 0xaf };
            byte[] dataend1 = new byte[] { 0x55, 0xae, 0x9b, 0x55, 0xad, 0x55, 0x2b, 0xae, 0x2b, 0xab, 0xad, 0xaf };
            byte[] end_gap = new byte[] { 0xc8, 0x00, 0x88, 0xaa, 0xaa, 0xba };
            byte[] lead = FastArray.Init(1001, 0xd7);
            byte[] ldout = FastArray.Init(127, 0xd7);
            byte[] de = new byte[dataend.Length];
            int start = 0;
            int end = 0;
            int pos = 0;
            int slay_ver = 2;
            MemoryStream buffer = new MemoryStream();
            BinaryWriter write = new BinaryWriter(buffer);
            byte[] comp = new byte[ps1.Length];
            for (int i = 0; i < data.Length - comp.Length; i++)
            {
                Buffer.BlockCopy(data, i, comp, 0, comp.Length);
                if (Match(ps1, comp)) { slay_ver = 1; break; }
                if (Match(ps2, comp)) { slay_ver = 2; break; }
            }
            if (slay_ver == 2) Slayer2();
            if (slay_ver == 1) Slayer1();

            void Slayer1()
            {
                comp = new byte[2];
                byte[] exp = new byte[] { 0xd7, 0xaa };
                for (int i = start; i < data.Length; i++)
                {
                    if (data[i] == 0x55)
                    {
                        Buffer.BlockCopy(data, i, de, 0, de.Length);
                        if (Match(dataend1, de)) { end = i + de.Length + 3; pos = i; }
                    }
                }
                while (pos >= 0)
                {
                    Buffer.BlockCopy(data, pos, comp, 0, comp.Length);
                    if (Match(exp, comp)) { start = pos; break; }
                    pos--;
                }
                write.Write(FastArray.Init(184, 0xeb));
                for (int i = start; i < end; i++) write.Write((byte)data[i]);
                if (buffer.Length < density[3]) write.Write(FastArray.Init(density[3] - (int)buffer.Length, 0xd7));
            }

            void Slayer2()
            {
                for (int i = start; i < data.Length; i++)
                {
                    if (data[i] == 0x55)
                    {
                        Buffer.BlockCopy(data, i, de, 0, de.Length);
                        if (Match(dataend, de) || Match(dataend1, de)) { end = i + de.Length; pos = i; }
                    }
                }
                while (pos >= 0)
                {
                    if (data[pos] == ps2[0])
                    {
                        if (data[pos + 1] == ps2[1] && data[pos + 2] == ps2[2] && data[pos + 3] == ps2[3] && data[pos + 4] == ps2[4]) { start = pos + 2; break; }
                    }
                    pos--;
                }
                byte[] key = new byte[end - start];
                Buffer.BlockCopy(data, start, key, 0, end - start);
                for (int i = 0; i < 2; i++)
                {
                    write.Write(key);
                    write.Write(lead);
                }
                write.Write(key);
                write.Write(ldout);
                write.Write(end_gap);
                while (buffer.Position < density[3]) write.Write((byte)0xfa);
            }
            if (buffer.Length == density[3]) return buffer.ToArray();
            else return data;
        }

        (bool, byte[], int) Radwar(byte[] data, bool fix = false, int sector = -1)
        {
            int rw = 0;
            bool rad = false;
            BitArray source = new BitArray(Flip_Endian(data));
            byte[] temp = new byte[0];
            bool f = false;
            int pos;
            if (!fix && sector == -1)
            {
                for (sector = 2; sector < 19; sector++)
                {
                    (f, pos, _, _) = Find_Sector(source, sector);
                    if (f)
                    {
                        rw = 0;
                        (temp, f) = Decode_CBM_Sector(data, sector, false, source);
                        for (int j = 0; j < temp.Length - 1; j++)
                        {
                            if (blank.Any(x => x == temp[j]))
                            {
                                rw++;
                                if (rw > 4) { rad = true; break; }
                            }
                        }
                    }
                    if (rad) break;
                }
            }
            else if (sector >= 0) Fix(sector);
            return (rad, data, sector);

            void Fix(int sec)
            {
                (temp, f) = Decode_CBM_Sector(data, sec, false, source);
                for (int k = 0; k < temp.Length - 1; k++)
                {
                    if (blank.Any(x => x == temp[k]) && temp[k] != 0x00)
                    {
                        temp[k] = 0x00;
                    }
                }
                data = Replace_CBM_Sector(data, sec, temp);
            }
        }

        byte[] JvB(byte[] data)
        {
            byte[] temp = FastArray.Init(density[3], 0x00);
            int run = 0;
            int longest = 0;
            int pos = 0;
            for (int i = 0; i < data.Length; i++)
            {
                if (!blank.Any((x) => x == data[i])) run++;
                else
                {
                    if (run > longest)
                    {
                        longest = run;
                        pos = i - run;
                    }
                    run = 0;
                }
            }
            Buffer.BlockCopy(data, pos, temp, 0, longest);
            return temp;
        }

        (bool, int) Check_Cyan_Loader(bool patch = false)
        {
            bool cyan = false;
            int c_cyn = 8;
            int c_gcr = 62;
            int c_v1 = 78;
            int w_trk = 76;
            bool cpt = false;
            try
            {
                if (tracks < 43) { c_cyn = 4; c_gcr = 31; c_v1 = 39; w_trk = 38; }
                if (NDS.cbm[c_cyn] == 1) cyan = Find_Cyan_Sector(NDS.Track_Data[c_cyn]);
                if (cyan && !patch)
                {
                    NDS.Prot_Method = "Protection: Cyan Loader";
                    if (NDS.cbm[c_v1] != 1) (NDS.Track_Data[c_gcr], cpt) = Cyan_t32_GCR_Fix(NDS.Track_Data[c_gcr]);
                    if (NDS.cbm[w_trk] == 1 && NDS.Track_ID[w_trk] == 40)
                    {
                        NDS.cbm[c_v1] = 1;
                        NDS.Track_Data[c_v1] = NDS.Track_Data[w_trk];
                        NDS.Track_ID[c_v1] = NDS.Track_ID[w_trk];
                        NDS.Track_Length[c_v1] = NDS.Track_Length[w_trk];
                        NDS.Track_Data[w_trk] = FastArray.Init(8192, 0x00);
                        NDS.Track_ID[w_trk] = 0;
                        NDS.cbm[w_trk] = 0;
                        NDS.Track_Length[w_trk] = 0;
                    }
                    else if ((((NDS.cbm[c_v1] == 1 && NDS.Track_ID[c_v1] != 40) || NDS.cbm[c_v1] != 1) && !cpt) || (NDS.cbm[c_v1] == 1 && NDS.sectors[c_v1] < 16))
                    {
                        if (!batch)
                        {
                            using (Message_Center center = new Message_Center(this))
                            {
                                string t = "Cyan Loader detected!";
                                string s = "Cyan Loader detected!\n\nProtected track is missing or corrupt\n\nWould you like to patch out the protection check?";
                                DialogResult result = MessageBox.Show(s, t, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                                if (result == DialogResult.Yes) Remove_Protection();
                            }
                        }
                        if (batch) Remove_Protection();
                    }
                }
                if (cyan && patch) Remove_Protection();
                if (NDS.cbm[c_v1] == 1) c_gcr = -1;
            }
            catch { }
            return (cyan, c_gcr);

            void Remove_Protection()
            {
                NDS.Track_Data[c_cyn] = Cyan_Loader_Patch(NDS.Track_Data[c_cyn]);
                if (NDS.cbm[c_v1] == 1)
                {
                    NDS.Track_Length[c_v1] = 0;
                    NDS.cbm[c_v1] = 0;
                }
            }

            bool Find_Cyan_Sector(byte[] data)
            {
                byte[] cmp;
                bool pass;
                (cmp, pass) = Decode_CBM_Sector(data, 5, true);
                if (pass || !pass)
                {
                    int match = 0;
                    for (int i = 0; i < cmp.Length; i++) if (cmp[i] == cldr_id[i]) match++;
                    if (match > 240) return true;
                }
                return false;
            }
        }

        byte[] Cyan_Loader_Patch(byte[] data) /// <- removes track 32 bad GCR check (cracks the proteciton)
        {
            byte[] cmp;
            (cmp, _) = Decode_CBM_Sector(data, 5, true);
            int match = 0;
            for (int i = 0; i < cmp.Length; i++) if (cmp[i] == cldr_id[i]) match++;
            if (match > 240)
            {
                if (cmp[45] == 0x2f) cmp[45] = 0xa2;
                if (cmp[53] == 0x18) cmp[53] = 0x0f;
                data = Replace_CBM_Sector(data, 5, cmp);
            }
            return data;
        }

        (byte[], bool) Cyan_t32_GCR_Fix(byte[] data)
        {
            BitArray s = new BitArray(Flip_Endian(data));
            (bool exists, _, _, _) = Find_Sector(s, 1, 0, true);
            if (exists)
            {
                (byte[] new_sec, _) = Decode_CBM_Sector(data, 1, false, s);
                byte[] padding = FastArray.Init(4, 0x00);
                data = Replace_CBM_Sector(data, 1, new_sec, padding);
            }
            return (data, exists);
        }

        byte[] Securispeed(byte[] data)
        {
            byte[] key = new byte[density[3]];
            byte[] sync = FastArray.Init(5, 0xff);
            int start = 0;
            int pos = 0;
            bool weak = false;
            bool end = false;
            byte[] s1 = new byte[gmt.Length];
            for (int i = 0; i < data.Length - gmt.Length; i++)
            {
                if (data[i] == gmt[0] || (data[i] == ssp[0] && data[i + 1] == ssp[1]))
                {
                    Buffer.BlockCopy(data, i, s1, 0, s1.Length);
                    if (Match(ssp, s1))
                    {
                        pos = i;
                        if (pos == 0) start = 5;
                        break;
                    }
                    for (int j = 1; j < gmt.Length; j++) s1[j] &= gmt[j];
                    if (Match(gmt, s1))
                    {
                        for (int k = i; k < i + 60; k++)
                        {
                            if (k < data.Length && data[k] == 0xff)
                            {
                                weak = true;
                                pos = i;
                                end = true;
                                break;
                            }
                        }
                        if (end) break;
                    }
                }
            }


            if (pos >= 5) data = Rotate_Left(data, pos - 5);
            if (weak) data = Remove_Weak_Bits(data, true);
            if (start == 5) Buffer.BlockCopy(sync, 0, key, 0, sync.Length);
            Buffer.BlockCopy(data, 0, key, start, key.Length - start);
            if (key[0] == gmt[0])
            {
                key = Rotate_Right(key, 6);
                Buffer.BlockCopy(sync, 0, key, 0, sync.Length);
            }
            return key;
        }

        byte[] RainbowArts(byte[] data)
        {
            int pos = 0;
            int start;
            int end = 0;
            int sync;
            byte[] s2 = new byte[ramb.Length];
            byte[] temp = new byte[0];
            int v = 0;
            for (int i = 0; i < data.Length - ramb.Length; i++)
            {
                if (data[i] == ramb[0])
                {
                    Buffer.BlockCopy(data, i, s2, 0, s2.Length);
                    if (Match(ramb, s2)) { pos = i; v = 1; break; }
                }
                if (data[i] == 0xff && (i > 0 && data[i - 1] != 0xff)) { v = 255; break; }
            }
            if (pos < data.Length)
            {
                if (v == 1) Key_Exists();
                if (v == 255) Key_Missing();
            }
            return temp;

            void Key_Exists()
            {
                temp = FastArray.Init(density[1], 0x55);
                start = pos;
                while (pos < data.Length)
                {
                    if (data[pos] == 0xff) { end = pos; break; }
                    pos++;
                }
                if (end - start > 0)
                {
                    Buffer.BlockCopy(data, start, temp, 0, end - start);
                    sync = Get_Sync_Length();
                    for (int i = 0; i < sync; i++) temp[(end - start) + i] = 0xff;
                    temp[end - start + sync] = 0x52;
                }
            }

            void Key_Missing()
            {
                temp = FastArray.Init(density[1], 0x55);
                sync = Get_Sync_Length();
                Buffer.BlockCopy(rak1, 0, temp, 0, rak1.Length);
                for (int i = 0; i < sync; i++) temp[rak1.Length + i] = 0xff;
                temp[rak1.Length + sync] = 0x52;
            }

            int Get_Sync_Length()
            {
                int snc = 0;
                for (int i = 0; i < data.Length; i++)
                {
                    if (data[i] == 0xff) snc++;
                    if (snc > 108 && data[i] != 0xff) break;
                }
                if (snc > 108 && snc < 123) return 116;
                return 128;
            }
        }

        byte[] Custom_Format(byte[] data)
        {
            byte[] skip = new byte[] { 0x55, 0xaa };
            HashSet<byte> BlankSet = new HashSet<byte>(blank);
            HashSet<byte> Padding = new HashSet<byte>(skip);
            int nb = 0;
            int pad = 0;
            int dataLength = data.Length;

            for (int i = 0; i < dataLength; i++)
            {
                if (!BlankSet.Contains(data[i])) nb++;
                if (data[i] == 0x55 || data[i] == 0xaa) pad++;
            }
            if (pad > density[0] - 100) return new byte[0];

            if (nb > 6200)
            {
                const int clen = 64;
                int d_end = dataLength;
                byte[] compare = new byte[clen];
                Buffer.BlockCopy(data, dataLength - clen, compare, 0, clen);

                for (int i = 0; i < dataLength - clen; i++)
                {
                    if (Match(data, compare, i, clen))
                    {
                        int newLength = d_end - (i + clen);
                        if (newLength > 6000)
                        {
                            byte[] temp = new byte[newLength];
                            Buffer.BlockCopy(data, i + clen, temp, 0, newLength);

                            int snc = 0;
                            for (int j = 0; j < newLength; j++)
                            {
                                if (temp[j] == 0xff) snc++;
                                else
                                {
                                    if (snc > 3)
                                    {
                                        temp = Rotate_Left(temp, j - snc);
                                        break;
                                    }
                                    snc = 0;
                                }
                            }
                            return temp;
                        }
                        else
                        {
                            int p = density[3] >> 1;
                            return ArrayConcat(FastArray.Init(p - 1, 0xac), new byte[] { 0xa0 }, FastArray.Init(density[3] - p, 0xca));
                        }
                    }
                }
            }

            if (nb > 500)
            {
                int workLength = data.Length;
                int snc = 0;
                int spos = 0;
                for (int i = 0; i < workLength; i++)
                {
                    if (data[i] == 0xff) snc++;
                    else
                    {
                        if (snc > 4)
                        {
                            spos = i - snc;
                            break;
                        }
                        snc = 0;
                    }
                }
                if (spos > 0) data = Rotate_Left(data, spos);

                int actual_data = (Check_Valid_Data(data, true));
                byte[] temp = new byte[Check_Valid_Data(data, false, true) < 1000 ? density[2] : density[3]];
                Buffer.BlockCopy(data, 0, temp, 0, temp.Length);
                if (actual_data > 500) temp = Remove_Weak_Bits(temp, true);
                temp = Add_Weak_Bit(temp);
                return temp;
            }
            return data;

            bool Match(byte[] source, byte[] compare, int startIndex, int length)
            {
                for (int j = 0; j < length; j++)
                {
                    if (source[startIndex + j] != compare[j]) return false;
                }
                return true;
            }

            int Check_Valid_Data(byte[] array, bool include_Padding = false, bool only_blank = false)
            {
                int ad = 0;
                int bd = 0;
                for (int j = 0; j < array.Length; j++)
                {
                    if (BlankSet.Contains(array[j])) bd++;
                    else ad++;
                    if (include_Padding)
                    {
                        if (Padding.Contains(array[j])) bd++;
                        else ad++;
                    }
                }
                if (only_blank) return bd;
                return ad;
            }

            byte[] Add_Weak_Bit(byte[] input)
            {
                int run = 0;
                byte g = 0x00;
                for (int j = 0; j < input.Length; j++)
                {
                    if (input[j] == g) run++;
                    else
                    {
                        g = input[j];
                        if (run > 300)
                        {
                            input[j] = 0x00;
                            if (j + 1 < input.Length) input[j + 1] = 0x00;
                            byte[] output = new byte[density[3]];
                            int start = (input.Length - density[3]) / 2;
                            Buffer.BlockCopy(input, start, output, 0, density[3]);
                            return output;
                        }
                        run = 0;
                    }
                }
                return input;
            }
        }
    }
}