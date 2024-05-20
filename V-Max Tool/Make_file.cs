using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace V_Max_Tool
{
    public partial class Form1 : Form
    {
        void Export_File()
        {
            Save_Dialog.FileName = $"{fname}{fnappend}";
            if (Out_Type) Save_Dialog.Filter = "G64|*.g64|NIB|*.nib|Both|*.g64;*.nib";
            else Save_Dialog.Filter = "G64|*.g64";
            Save_Dialog.Title = "Save File";
            if (Save_Dialog.ShowDialog() == DialogResult.OK)
            {
                string fs = Save_Dialog.FileName;
                if (Save_Dialog.FilterIndex == 1) Make_G64(fs);
                if (Save_Dialog.FilterIndex == 2) Make_NIB(fs);
                if (Save_Dialog.FilterIndex == 3)
                {
                    Make_NIB($@"{Path.GetDirectoryName(fs)}\{Path.GetFileNameWithoutExtension(fs)}.nib");
                    Make_G64($@"{Path.GetDirectoryName(fs)}\{Path.GetFileNameWithoutExtension(fs)}.g64");
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

        void Make_NIB(string fname)
        {
            //if (!Directory.Exists($@"{dirname}\Output")) Directory.CreateDirectory($@"{dirname}\Output");
            var buffer = new MemoryStream();
            var write = new BinaryWriter(buffer);
            write.Write(nib_header);
            for (int i = 0; i < tracks; i++) write.Write(NDA.Track_Data[i]);
            try
            {
                File.WriteAllBytes(fname, buffer.ToArray());
            }
            catch (Exception ex)
            {
                nib_error = true;
                nib_err_msg = ex.Message;
            }
            buffer.Close();
            write.Close();
        }

        void Make_G64(string fname)
        {
            //if (!Directory.Exists($@"{dirname}\Output")) Directory.CreateDirectory($@"{dirname}\Output");
            var buffer = new MemoryStream();
            var write = new BinaryWriter(buffer);
            byte[] head = Encoding.ASCII.GetBytes("GCR-1541");
            byte z = 0;
            List<int> len = new List<int>(0);
            for (int i = 0; i < tracks; i++)
            {
                if (NDS.cbm[i] > 0 && NDS.cbm[i] < 6) len.Add(NDG.Track_Length[i]);
            }
            short m = Convert.ToInt16(len.Max());
            //short m = Convert.ToInt16(NDG.Track_Length.Max());
            if (m < 7928) m = 7928;
            write.Write(head);
            write.Write(z);
            write.Write((byte)84);
            write.Write(m);
            int offset = 684;
            int th = 0;
            int[] td = new int[84];
            if (tracks > 42) Big(); else Small();
            for (int i = 0; i < 84; i++) write.Write(td[i]);
            for (int i = 0; i < tracks; i++)
            {
                if (NDG.Track_Length[i] > 6000 && NDS.cbm[i] > 0 && NDS.cbm[i] < 6)
                {
                    write.Write((short)NDG.Track_Length[i]);
                    if (debug)
                    {
                        if (NDG.Track_Data[i].Length < m) write.Write(NDG.Track_Data[i]);
                        else
                        {
                            byte[] t = new byte[m];
                            Array.Copy(NDG.Track_Data[i], 0, t, 0, m);
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
                File.WriteAllBytes(fname, buffer.ToArray());
            }
            catch (Exception ex)
            {
                g64_error = true;
                g64_err_msg = ex.Message;
            }

            void Big() // 84 track nib file
            {
                for (int i = 0; i < 84; i++)
                {
                    if (i < NDG.Track_Data.Length && NDG.Track_Length[i] > 6000 && NDS.cbm[i] < 6)
                    {
                        write.Write((int)offset + th);
                        th += 2;
                        if (debug) offset += m; else offset += NDG.Track_Data[i].Length;
                        td[i] = 3 - Get_Density(NDG.Track_Data[i].Length);
                    }
                    else write.Write((int)0);
                }
            }

            void Small() // 42 track nib file
            {
                int r = 0;
                for (int i = 0; i < 42; i++)
                {
                    if (i < NDG.Track_Data.Length && NDG.Track_Length[i] > 6000 && NDS.cbm[i] < 6)
                    {
                        write.Write((int)offset + th);
                        th += 2;
                        if (debug) offset += m; else offset += NDG.Track_Data[i].Length;
                        td[r] = 3 - Get_Density(NDG.Track_Data[i].Length);
                        r++; td[r] = 0; r++;
                    }
                    else write.Write((int)0);
                    write.Write((int)0);
                }
            }
        }
    }
}