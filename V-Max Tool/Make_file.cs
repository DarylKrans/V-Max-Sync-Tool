﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace V_Max_Tool
{
    public partial class Form1 : Form
    {
        void Export_File(int last_track = -1)
        {
            Save_Dialog.FileName = $"{fname}{fnappend}";
            //Save_Dialog.Filter = "G64|*.g64|NIB|*.nib|D64|*.d64|NBZ|*.nbz|Compressed G64 [only supported by ReMaster]|*.z64";
            Save_Dialog.Filter = "G64|*.g64|NIB|*.nib|D64|*.d64|NBZ|*.nbz";
            Save_Dialog.Title = "Save File";
            if (Save_Dialog.ShowDialog() == DialogResult.OK)
            {
                string fs = Save_Dialog.FileName;
                switch (Save_Dialog.FilterIndex)
                {
                    case 1:
                        Make_G64(fs, last_track);
                        break;
                    case 2:
                        Make_NIB(fs);
                        break;
                    case 3:
                        Make_D64(fs, last_track);
                        break;
                    case 4:
                        Make_NIB(fs, true);
                        break;
                }
                if (nib_error || g64_error)
                {
                    string s = "";
                    using (Message_Center center = new Message_Center(this)) // center message box
                    {
                        string t = "File Access Error!";
                        if (nib_error) s = $"{nib_err_msg}";
                        if (g64_error) s = $"{g64_err_msg}";
                        if (nib_error && g64_error) s = $"{nib_err_msg}\n\n{g64_err_msg}";
                        AutoClosingMessageBox.Show(s, t, 5000);
                        error = true;
                    }
                    nib_error = g64_error = false;
                }
            }
        }

        void Make_NIB(string fname, bool compress = false)
        {
            var buffer = new MemoryStream();
            var write = new BinaryWriter(buffer);
            write.Write(Encoding.ASCII.GetBytes("MNIB-1541-RAW"));
            byte[] htks = new byte[] { 0x03, 0x00, 0x00 };
            if (tracks > 42) htks[2] = 0x01;
            write.Write(htks);
            for (int i = 0; i < tracks; i++)
            {
                if (tracks > 42) write.Write((byte)(i + 2)); else write.Write((byte)((i + 2) * 2));
                if (NDS.cbm[i] > 0) write.Write((byte)(3 - Get_Density(NDG.Track_Length[i])));
                else write.Write((byte)0x00);
            }
            write.Write(FastArray.Init(256 - (int)buffer.Length, 0x00));
            for (int i = 0; i < tracks; i++)
            {
                if (NDA.Track_Data?[i] != null) write.Write(NDA.Track_Data[i]);
                else write.Write(FastArray.Init(8192, 0x00));
            }
            try
            {
                if (compress) File.WriteAllBytes(fname, LZcompress(buffer.ToArray()));
                else File.WriteAllBytes(fname, buffer.ToArray());
            }
            catch (Exception ex)
            {
                nib_error = true;
                nib_err_msg = ex.Message;
            }
            buffer.Close();
            write.Close();
        }

        void Make_G64(string fname, int l_trk, bool compress = false)
        {
            fname = fname.Replace($"\\\\", $"\\");
            if (l_trk < 0) l_trk = tracks;
            if (!Directory.Exists(Path.GetDirectoryName(fname))) Directory.CreateDirectory(Path.GetDirectoryName(fname));
            var buffer = new MemoryStream();
            var write = new BinaryWriter(buffer);
            byte[] watermark = Encoding.ASCII.GetBytes($"    ReMaster Utility{ver} https://github.com/DarylKrans/ReMaster-Utility                  ");
            //byte[] watermark = new byte[0];
            for (int i = 0; i < watermark.Length; i++) if (watermark[i] == 0x20) watermark[i] = 0x00;
            byte[] head = Encoding.ASCII.GetBytes("GCR-1541");
            byte z = 0;
            List<int> len = new List<int>(0);
            for (int i = 0; i < tracks; i++) if (NDS.cbm[i] >= 0 && NDS.cbm[i] < secF.Length - 1) len.Add(NDG.Track_Length[i]);
            short m = Convert.ToInt16(len.Max());
            if (m < 7928) m = 7928;
            write.Write(head);
            write.Write(z);
            write.Write((byte)84);
            write.Write(m);
            int offset = 684 + watermark.Length;
            int th = 0;
            int[] tp = new int[84];
            int[] td = new int[84];
            if (tracks > 42) Big(l_trk);
            else Small(l_trk);
            for (int i = 0; i < tp.Length; i++) write.Write(tp[i]);
            for (int i = 0; i < td.Length; i++) write.Write(td[i]);
            write.Write(watermark);
            List<string> type = new List<string>();
            for (int i = 0; i < l_trk; i++)
            {
                if (NDG.Track_Length[i] > 6000 && NDS.cbm[i] >= 0 && NDS.cbm[i] < secF.Length - 1)
                {
                    write.Write((short)NDG.Track_Length[i]);
                    if (DB_g64.Checked)
                    {
                        if (NDG.Track_Data[i].Length < m) write.Write(NDG.Track_Data[i]);
                        else
                        {
                            byte[] t = new byte[m];
                            Buffer.BlockCopy(NDG.Track_Data[i], 0, t, 0, m);
                            write.Write(t);
                        }
                        var o = m - NDG.Track_Length[i];
                        if (o > 0) for (int j = 0; j < o; j++) write.Write((byte)0);
                    }
                    else write.Write(NDG.Track_Data[i]);
                }
            }
            try
            {
                if (compress) File.WriteAllBytes(fname, LZcompress(buffer.ToArray()));
                else File.WriteAllBytes(fname, buffer.ToArray());
            }
            catch (Exception ex)
            {
                g64_error = true;
                g64_err_msg = ex.Message;
            }

            void Big(int trk) /// 84 track nib file
            {
                int prev_ofs = 0;
                for (int i = 0; i < 84; i++)
                {
                    if (i < NDG.Track_Data.Length && NDG.Track_Length[i] > 6000 && NDS.cbm[i] < secF.Length - 1)
                    {
                        if (i <= trk) tp[i] = offset + th;
                        prev_ofs = offset + th;
                        th += 2;
                        if (DB_g64.Checked) offset += m; else offset += NDG.Track_Data[i].Length;
                        if (i <= trk) td[i] = 3 - Get_Density(NDG.Track_Data[i].Length);
                    }
                    else if (i > 0 && NDG.Fat_Track[i - 1] && NDG.Fat_Track[i + 1])
                    {
                        tp[i] = prev_ofs;
                        td[i] = td[i - 1];
                    }
                }
            }

            void Small(int trk) /// 42 track nib file
            {
                int r = 0;
                int prev_ofs;
                for (int i = 0; i < 42; i++)
                {
                    if (i < NDG.Track_Data.Length && NDG.Track_Length[i] > 6000 && NDS.cbm[i] < secF.Length - 1)
                    {
                        if (i <= trk) tp[i * 2] = offset + th;
                        prev_ofs = offset + th;
                        th += 2;
                        if (DB_g64.Checked) offset += m; else offset += NDG.Track_Data[i].Length;
                        if (i <= trk) td[r] = 3 - Get_Density(NDG.Track_Data[i].Length); else td[r] = 0;
                        r += 2;
                        if (i + 1 < trk && (NDG.Fat_Track[i] && NDG.Fat_Track[i + 1]))
                        {
                            tp[(i * 2) + 1] = prev_ofs;
                            td[r - 1] = td[r - 2];
                        }
                    }
                }
            }
        }

        void Make_D64(string path, int endTrack)
        {
            int stop = -1;
            int lastTrack = 0;
            for (int i = NDS.cbm.Length - 1; i >= 0; i--)
            {
                if ((NDS.cbm[i] == 1))
                {
                    stop = i;
                    break;
                }
            }
            int ht = tracks > 42 ? 2 : 1;
            endTrack = Math.Min(stop, endTrack + 2);
            stop = ht == 2 ? stop / ht : stop;
            lastTrack = Math.Max(Math.Min(stop, endTrack), 34);
            endTrack = Math.Max(endTrack, (35 * ht) - 1);
            int sectors = 0;
            for (int i = 0; i <= lastTrack; i++)
            {
                sectors += Available_Sectors[i];
            }
            byte[] errorMap = FastArray.Init(sectors, 0x01);
            byte[] ID = GetDiskID(true);
            MemoryStream buffer = new MemoryStream();
            BinaryWriter write = new BinaryWriter(buffer);
            int currentSector = 0;
            int currentTrack = 0;
            byte[] empty_sector = FastArray.Init(256, 0x01);
            empty_sector[0] = 0x4b;
            BitArray source = new BitArray(0);
            int[] c = new int[] { 2, 3, 4, 5, 6 };
            bool alt = (NDS.cbm.Any(x => c.Any()));
            for (int i = 0; i <= endTrack; i++)
            {
                if (NDS.cbm?[i] == 1 && NDG.Track_Data?[i] != null)
                {
                    int start = !alt ? 0 : NDS.D_Start[i];
                    //source = new BitArray(Flip_Endian(NDG.Track_Data[i]));
                    source = new BitArray(Flip_Endian(!alt ? NDG.Track_Data[i] : NDS.Track_Data[i]));
                    for (int j = 0; j < Available_Sectors[currentTrack]; j++)
                    {
                        (byte[] sec_data, int errorCode, _) = GetSectorWithErrorCode(NDG.Track_Data[i], j, true, ID, source, start);
                        errorMap[currentSector] = (byte)errorCode;
                        write.Write((sec_data != null && !(errorCode == 2 || errorCode == 4)) ? sec_data : empty_sector);
                        currentSector++;
                    }
                }
                else CreateEmptyTrack();
                currentTrack++;
                if (ht == 2) i++;
            }
            if (Array.Exists(errorMap, e => e != 1))
            {
                write.Write(errorMap);
            }
            File.WriteAllBytes(path, buffer.ToArray());

            void CreateEmptyTrack()
            {
                for (int j = 0; j < Available_Sectors[currentTrack]; j++)
                {
                    write.Write(empty_sector);
                    errorMap[currentSector++] = 2;
                }
            }
        }
    }
}