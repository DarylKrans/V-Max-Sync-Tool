using System;
using System.Collections;
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
        private readonly byte[] blank = new byte[] { 0x00, 0x11, 0x22, 0x44, 0x45, 0x14, 0x12, 0x51, 0x88 };
        //private readonly byte[] cldr_id = new byte[] { 0xa0, 0x0f, 0xb9, 0x00, 0x03, 0x45, 0x38, 0x45, 0x39 }; /// <- Cyan Loader signature found on t2s2

        byte[] Pirate_Slayer(byte[] data)
        {
            byte[] dataend = new byte[] { 0x55, 0xae, 0x9b, 0x55, 0xad, 0x55, 0xcb, 0xae, 0x6b, 0xab, 0xad, 0xaf };
            byte[] dataend1 = new byte[] { 0x55, 0xae, 0x9b, 0x55, 0xad, 0x55, 0x2b, 0xae, 0x2b, 0xab, 0xad, 0xaf };
            byte[] end_gap = new byte[] { 0xc8, 0x00, 0x88, 0xaa, 0xaa, 0xba };
            byte[] lead = IArray(1001, 0xd7);
            byte[] ldout = IArray(127, 0xd7);
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
                for (int i = 0; i < 184; i++) write.Write((byte)0xeb);
                for (int i = start; i < end; i++) write.Write((byte)data[i]);
                while (buffer.Length < density[3]) write.Write((byte)0xd7);
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

        byte[] JvB(byte[] data)
        {
            bool nul = false;
            int end = 0, pos, start = 0;
            byte[] temp = IArray(density[3]);
            BitArray src = new BitArray(Flip_Endian(data));
            (nul, pos) = Find_Sector(src, 0);
            if (nul)
            {
                end = pos;
                if (data[pos] != 0xff)
                {
                    start = 5;
                    for (int i = 0; i < 5; i++) temp[i] = 0xff;
                }
                while (end < data.Length)
                {
                    if (blank.Any(x => x == data[end])) break;
                    end++;
                }
                Buffer.BlockCopy(data, pos, temp, start, end - pos);
            }
            return temp;
        }

        bool Check_Cyan_Loader(byte[] data)
        {
            byte[] cmp;
            bool pass;
            (cmp, pass) = Decode_CBM_Sector(data, 5, true);
            int match = 0;
            for (int i = 0; i < cmp.Length; i++) if (cmp[i] == cldr_id[i]) match++;
            if (match > 240) return true;
            return false;
        }

        byte[] Cyan_t32_GCR_Fix(byte[] data, int trk)
        {
            BitArray source = new BitArray(Flip_Endian(data));
            int pos;
            bool pass;
            int snc = 0;
            (pass, pos) = Find_Sector(source, 1);
            for (int i = pos << 3; i < source.Length ;i ++)
            {
                if (source[i]) snc++;
                if (!source[i])
                {
                    if (snc > 20)
                    {
                        pos = i >> 3;
                        data[pos + 320] = 0x00;
                        data[pos + 321] = 0x00;
                        data[pos + 322] = 0x00;
                        data[pos + 323] = 0x00;
                        data[pos + 324] = 0x00;
                        break;
                    }
                    snc = 0; 
                }
            }
            //data[pos + 324] = 0x00;
            //data[pos + 325] = 0x00;
            return data;
        }

        byte[] RainbowArts(byte[] data, bool large_key)
        {
            int pos = 0;
            int start;
            int end = 0;
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
                if (large_key) temp = IArray(7750, 0x55);
                else temp = IArray(density[1], 0x55);
                start = pos;
                while (pos < data.Length)
                {
                    if (data[pos] == 0xff) { end = pos; break; }
                    pos++;
                }
                if (end - start > 0)
                {
                    Buffer.BlockCopy(data, start, temp, 0, end - start);
                    for (int i = 0; i < 128; i++) temp[(end - start) + i] = 0xff;
                    temp[end - start + 128] = 0x52;
                }
            }

            void Key_Missing()
            {
                if (large_key) temp = IArray(7750, 0x55);
                else temp = IArray(density[1], 0x55);
                Buffer.BlockCopy(rak1, 0, temp, 0, rak1.Length);
            }
        }
    }
}