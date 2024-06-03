using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace V_Max_Tool
{
    public partial class Form1 : Form
    {
        /// ---------------------------------- Get_LeadIn_Position Length of Loader Track ---------------------------------------------
        (int, byte[]) Get_Loader_Len(byte[] data, int start_pos, int comp_length, int skip_length)
        {
            int q = 8192;
            int qq = 0;
            byte[] dataa = data;
            while (q == 8192 && qq < 32)
            {
                q = find();
                if (q == 8192)
                {
                    dataa = Rotate_Left(data, qq);
                    qq++;
                }
            }
            return (q, dataa);
            int find()
            {
                int p = 0;
                if (dataa != null)
                {
                    byte[] star = new byte[comp_length];
                    Buffer.BlockCopy(dataa, start_pos, star, 0, comp_length);
                    byte[] comp = new byte[8192 - (comp_length + start_pos)];
                    Buffer.BlockCopy(dataa, comp_length, comp, 0, 8192 - (comp_length + start_pos));

                    for (p = skip_length; p < comp.Length; p++)
                    {
                        if (comp.Skip(p).Take(star.Length).SequenceEqual(star))
                        {
                            break;
                        }
                    }
                }
                return p + comp_length;
            }
        }

        /// ------------------------- Rotate Loader Track -------------------------------------------

        byte[] Rotate_Loader(byte[] temp)
        {
            //------- Checks to see if Loader track contains V-Max Headers (found on Mindscape titles) -----------
            bool rotated = false;
            if (NDS.Loader.Length > 0)
            {
                byte[] sb = new byte[1]; byte[] eb = new byte[1];
                sb[0] = NDS.Loader[0];
                eb[0] = NDS.Loader[1];
                int vs = Convert.ToInt32(NDS.Loader[2]);
                byte[] comp = new byte[2];
                for (int j = 0; j < 8; j++)
                {
                    byte[] tmp = new byte[temp.Length];
                    Buffer.BlockCopy(temp, 0, tmp, 0, tmp.Length);
                    BitArray s_bArray = new BitArray(Flip_Endian(tmp));
                    BitArray d_bArray = new BitArray(s_bArray.Count);
                    int dp = 0;
                    for (int h = j; h < s_bArray.Length; h++)
                    {
                        d_bArray[dp] = s_bArray[h];
                        dp++;
                        if (dp == d_bArray.Length) dp = 0;
                    }
                    byte[] cc = Flip_Endian(Bit2Byte(d_bArray));
                    int sec = 0;
                    for (int i = 0; i < cc.Length - 5; i++)
                    {

                        if (cc[i] == sb[0]) Buffer.BlockCopy(cc, i + 1, comp, 0, comp.Length);
                        if (vm2_ver[vs].Any(s => s == Hex_Val(comp)))
                        {
                            for (int g = (i + 2); g < cc.Length; g++)
                            {
                                if (cc[g] == eb[0] && g < (i + 40) && g > (i + 5))
                                {
                                    sec++;
                                    i += 340;
                                    if (sec > 1)
                                    {
                                        rotated = true;
                                        temp = Rotate_Left(temp, i + 1);
                                        goto End_rotate;
                                    }
                                    break;
                                }
                            }
                        }
                    }
                }
            }
        //----------------------------------------------------------------------------------------------------------
        End_rotate:
            if (!rotated)
            {
                int start = 0;
                int longest = 0;
                int count = 0;
                for (int i = 1; i < temp.Length; i++)
                {
                    if (temp[i] != temp[i - 1]) count = 0;
                    count++;
                    if (count > longest)
                    {
                        start = i - count;
                        longest = count;
                    }
                }
                if (longest > 2)
                {
                    temp = Rotate_Left(temp, start + (longest / 2));
                }
            }
            return temp;
        }

        byte[] Pad_Loader(byte[] data, byte padding, int Density)
        {
            MemoryStream buffer = new MemoryStream();
            BinaryWriter write = new BinaryWriter(buffer);
            var pad_len = (density[Density] - data.Length) / 2;
            pad();
            write.Write(data);
            pad();
            return buffer.ToArray();

            void pad()
            {
                for (int i = 0; i < pad_len; i++) write.Write((byte)padding);
            }
        }

        /// ------------------------ Add Sync to Loader Track --------------------------------------------------

        void Fix_Loader_Option(bool draw, int i) //, bool swap = false)
        {
            var trk_num = 0;
            if (f_load.Checked)
            {
                var tt = 0;
                if (tracks > 42) trk_num = (i / 2) + 1; else trk_num = i;
                Original.G = new byte[NDG.Track_Data[i].Length];
                Original.A = new byte[NDA.Track_Data[i].Length];
                Buffer.BlockCopy(NDG.Track_Data[i], 0, Original.G, 0, NDG.Track_Data[i].Length);
                Buffer.BlockCopy(NDA.Track_Data[i], 0, Original.A, 0, NDA.Track_Data[i].Length);
                var d = Get_Density(NDG.Track_Data[i].Length);
                if (NDS.cbm.Any(x => x == 3))
                {
                    if (NDG.Track_Data[i].Length > density[d]) Shrink_Loader(i);
                    byte[] temp = Rotate_Loader(NDG.Track_Data[i]);
                    NDG.L_Rot = true;
                    Set_Dest_Arrays(Fix_Loader(temp), i);
                    FL();
                }
                if (!(NDS.cbm.Any(x => x == 2) || NDS.cbm.Any(x => x == 3)))
                {
                    Set_Dest_Arrays(Pad_Loader(v2ldrcbm, loader_padding, density_map[trk_num]), i);
                    FL();
                }
                if (NDS.cbm.Any(x => x == 2))
                {
                    for (int x = 0; i < tracks; x++) if (NDS.v2info[x]?.Length > 0) { tt = x; break; }
                    if (!V2_swap_headers.Checked)
                    {
                        if (Hex_Val(NDS.v2info[tt], 0, 2) == "4E-64") Set_Dest_Arrays(Pad_Loader(v24e64pal, loader_padding, density_map[trk_num]), i);
                        if (Hex_Val(NDS.v2info[tt], 0, 2) == "64-46") Set_Dest_Arrays(Pad_Loader(v26446ntsc, loader_padding, density_map[trk_num]), i);
                        if (Hex_Val(NDS.v2info[tt], 0, 2) == "64-4E") Set_Dest_Arrays(Pad_Loader(v2644entsc, loader_padding, density_map[trk_num]), i);
                    }
                    else
                    {
                        if (Hex_Val(NDG.newheader, 0, 2) == "4E-64") Set_Dest_Arrays(Pad_Loader(v24e64pal, loader_padding, density_map[trk_num]), i);
                        if (Hex_Val(NDG.newheader, 0, 2) == "64-46") Set_Dest_Arrays(Pad_Loader(v26446ntsc, loader_padding, density_map[trk_num]), i);
                        if (Hex_Val(NDG.newheader, 0, 2) == "64-4E") Set_Dest_Arrays(Pad_Loader(v2644entsc, loader_padding, density_map[trk_num]), i);
                    }
                    FL();
                }
                loader_fixed = true;
            }
            if (!f_load.Checked)
            {
                Invoke(new Action(() =>
                {
                    f_load.Text = "Fix Loader";
                    if (tracks > 0) i = Array.FindIndex(NDS.cbm, s => s == 4);
                    if (i > -1 && i < 100)
                    {
                        if (Original.A.Length > 0) { NDA.Track_Data[i] = Original.A; }
                        if (Original.G.Length > 0)
                        {
                            NDG.Track_Data[i] = Original.G; f_load.Text += " (Restored)";
                            NDG.L_Rot = false;
                        }
                        loader_fixed = false;

                    }
                }));
            }
            displayed = false;
            drawn = false;
            if (draw)
            {
                Check_Before_Draw(false);
                Data_Viewer();
            }

            void FL()
            {
                Invoke(new Action(() => f_load.Text = "Fix Loader (Fixed)"));
            }
        }


        byte[] Fix_Loader(byte[] data)
        {
            byte[] tdata = data;
            byte[] v2 = new byte[] { 0x5b, 0x57, 0x52, 0x4d }; // Cinemaware and some other v2 variants
            byte[] v3 = new byte[] { 0xaa, 0xaf, 0xda, 0x5f }; // V3 Taito (arkanoid)
            byte[] v1 = new byte[] { 0xaa, 0xbf, 0xb4, 0xbf }; // v3 Taito (bubble bobble)
            byte[] v4 = new byte[] { 0x6b, 0xd9, 0xb6, 0xdd }; // Sega
            byte[] comp = new byte[4];
            bool f = false;
            for (int i = 0; i < tdata.Length - 4; i++)
            {
                Buffer.BlockCopy(tdata, i, comp, 0, comp.Length);
                if (Match(comp, v1)) { Patch_V3(i - 4); f = true; }
                if (Match(comp, v2)) { Patch_V2(i - 3); f = true; }
                if (Match(comp, v3)) { Patch_V3(i - 4); f = true; }
                if (Match(comp, v4)) { Patch_V2(i - 3); f = true; }
                if (f) break;
            }
            if (f) Invoke(new Action(() => f_load.Text = "Fix Loader (Fixed)"));
            return tdata;

            void Patch_V2(int pos)
            {
                if (pos > 0)
                {
                    tdata[pos] = 0xde;
                    tdata[pos + 1] = 0xff;
                    tdata[pos + 2] = 0xff;
                }
            }

            void Patch_V3(int pos)
            {
                if (pos > 0)
                {
                    tdata[pos] = 0x5f;
                    tdata[pos + 1] = 0xff;
                    tdata[pos + 2] = 0xff;
                }
            }
        }
    }
}