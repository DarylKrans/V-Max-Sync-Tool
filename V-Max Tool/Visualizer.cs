using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace V_Max_Tool
{
    public partial class Form1 : Form
    {

        private string def_text = "";
        private bool interp = false;
        private bool Dragging = false;
        private bool vm_reverse = false;
        private bool drawn = false;
        private int xPos;
        private int yPos;
        private readonly string[] Img_Quality = { "Very Low", "Low", "Normal", "High", "Ultra", "Atomic", "Insanity!" };
        private Bitmap flat_large;
        private Bitmap flat_small;
        private FastBitmap circle;
        private Bitmap circle_small;
        private Bitmap circle_full;
        private readonly Brush cbm_brush = new SolidBrush(Color.FromArgb(200, 67, 200));
        private readonly Brush ldr_brush = new SolidBrush(Color.FromArgb(133, 133, 200));
        private readonly Brush vmx_brush = new SolidBrush(Color.FromArgb(30, 200, 30));
        private readonly Brush vpl_brush = new SolidBrush(Color.FromArgb(30, 200, 200));
        private readonly Color Write_face = Color.FromArgb(41, 40, 36);
        private readonly Color Inner_face = Color.FromArgb(50, 49, 44);

        private void Draw_Init_Img(string bg_text)
        {
            var m = (Img_Q.SelectedIndex + 1) * 1000;
            circle = new FastBitmap(m, m);
            Draw_Disk(circle, 3, m, this.Text, bg_text);
            circle_full = (Bitmap)Resize_Image(circle.Bitmap, panPic.Width, panPic.Height, false, true);
            circle_small = (Bitmap)Resize_Image(circle.Bitmap, panPic.Width, panPic.Height, false, true);
            flat_large = (Bitmap)Resize_Image(circle.Bitmap, panPic.Width, panPic.Height, false, true);
            flat_small = (Bitmap)Resize_Image(circle.Bitmap, panPic.Width, panPic.Height, false, true);
            Disk_Image.Image = Resize_Image(circle.Bitmap, panPic.Width, panPic.Height, false, true);
        }

        private void Draw_Flat_Tracks(bool interpolate)
        {
            string ext = "";
            var d = 0;
            Font font = new Font("Ariel", 11);
            if (!interpolate)
            {
                double ht;
                bool halftracks = false;
                if (tracks > 42)
                {
                    ht = 0.5;
                    halftracks = true;
                }
                else ht = 0;
                flat_large = new Bitmap(8192, (42 * 14) - 16);
                Bitmap t = new Bitmap(flat_large.Width, flat_large.Height);
                int at = 0;
                int pt = 0;
                for (int h = 0; h < tracks; h++) if (NDG.Track_Length[h] > min_t_len && NDS.cbm[h] < 6) at++;
                if (at > 0) Invoke(new Action(() =>
                {
                    Flat_Render.Value = 0;
                    Flat_Render.Maximum = 100;
                    Flat_Render.Maximum *= 100;
                    Flat_Render.Value = Flat_Render.Maximum / 100;
                    Flat_Render.Visible = true;
                }));
                for (int i = 0; i < tracks; i++)
                {
                    if (Out_view.Checked)
                    {
                        if (NDG.Track_Length[i] > min_t_len && NDS.cbm[i] < 6)
                        {
                            d = Get_Density(NDG.Track_Data[i].Length);
                            t = Draw_Track(flat_large, (42 * 14), NDG.Track_Data[i], (int)ht, 0, 0, NDS.cbm[i], NDS.v2info[i], d, Out_view.Checked, NDS.cbm_sector[i]);
                            ext = "(flat_tracks).g64";
                        }
                    }
                    if (Src_view.Checked)
                    {
                        var ds = NDS.D_Start[i];
                        var de = NDS.D_End[i];
                        if (NDS.cbm[i] == 1 || NDS.cbm[i] == 5) { ds >>= 3; de >>= 3; } else { ds = 0; de = 8192; }
                        if (NDS.Track_Data[i].All(s => s != 0x00)) // <- view all tracks that aren't all 0x00 bytes
                        {
                            t = Draw_Track(flat_large, (42 * 14), NDS.Track_Data[i], (int)ht, ds, de, NDS.cbm[i], NDS.v2info[i], d, Out_view.Checked, NDS.cbm_sector[i]);
                            ext = $"(flat_tracks){fext}";
                        }
                    }
                    if (halftracks) ht += .5; else ht += 1;
                    pt++;
                    if (pt - 1 > 0) Invoke(new Action(() => Flat_Render.Maximum = (int)((double)Flat_Render.Value / (double)(pt + 1) * at)));
                }
                flat_large = (Bitmap)Resize_Image(t, t.Width, t.Height, false, false);
                flat_small = (Bitmap)Resize_Image(t, pan_defw, pan_defh - 16, false, Flat_Interp.Checked);
                Add_Text(flat_small, $"{fname}{fnappend}{ext}", Color.FromArgb(40, 40, 40),
                    Brushes.White, font, 20, flat_small.Height - 20, 600, flat_small.Height);
                Add_Text(flat_large, $"{fname}{fnappend}{ext}", Color.FromArgb(40, 40, 40),
                    Brushes.White, font, 20, flat_large.Height - 40, 600, flat_large.Height);
                Invoke(new Action(() =>
                {
                    if (Flat_View.Checked)
                    {
                        if (Img_zoom.Checked)
                        {
                            Disk_Image.Cursor = Cursors.Hand;
                            Disk_Image.Image = flat_large;
                        }
                        else
                        {
                            Disk_Image.Cursor = Cursors.Arrow;
                            Disk_Image.Image = flat_small;
                        }
                        Disk_Image.Top = 0;
                        Disk_Image.Left = 0;
                    }
                    Flat_Render.Visible = false;
                }));
                def_text = $"{fname}{fnappend}{ext}";
                t.Dispose();
            }
            else
            {
                flat_small = (Bitmap)Resize_Image(flat_large, pan_defw, pan_defh - 16, false, Flat_Interp.Checked);
                Add_Text(flat_small, $"{fname}{fnappend}{ext}", Color.FromArgb(40, 40, 40),
                        Brushes.White, font, 20, flat_small.Height - 20, 600, flat_small.Height);
                Disk_Image.Image = flat_small;
            }
            GC.Collect();
        }

        private void Draw_Circular_Tracks()
        {
            int at = 0;
            int pt = 0;
            for (int h = 0; h < tracks; h++) if (NDG.Track_Length[h] > min_t_len && NDS.cbm[h] < 6) at++;
            int m = 0;
            double sub = 1.25;
            Invoke(new Action(() =>
            {
                m = Img_Q.SelectedIndex + 1;
                Set_Circular_Draw_Options(false, 0);
            }));
            int skp = 1;
            if (tracks <= 42) skp = 2;
            string fi_ext = ".g64";
            string fi_nam = $"{fname}{fnappend}";
            bool v2 = false;
            sub *= m;
            int width = m * 1000;
            int height = m * 1000;
            int track = 0;
            int i;
            int de;
            int j = 0;
            int k;
            int x = width / 2;
            int y = height / 2;
            int r = (int)((width / 2) / 1.0316368638f);
            r = (width / 2) - (15 * m);
            int len;
            int t_width = (int)(3.1f * m);
            Color col;
            BitArray t_bit = new BitArray(0);
            circle = new FastBitmap(width, height);
            if (Src_view.Checked) { fi_ext = ".nib"; fi_nam = $"{fname}"; }
            int trk = 100;
            int a = 0;
            Random rnd = new Random();
            while (true || a < 1000)
            {
                trk = rnd.Next(0, tracks - 1);
                if (NDS.Track_Length[trk] > 0) break;
                a++;
            }
            Draw_Disk(circle, m, width, $"{fi_nam}{fi_ext}", ToBinary(Encoding.ASCII.GetString(NDS.Track_Data[trk], 0, 2000)));
            //Stopwatch sw = Stopwatch.StartNew();
            while (r > 80 && track < tracks)
            {
                bool v5 = false;
                if (NDG.Track_Length[track] > min_t_len && NDS.cbm[track] < 6)
                {
                    pt++;
                    v2 = false;
                    byte[] t_data = Get_Track_Data(track);
                    len = t_data.Length;
                    de = Get_Density(len);
                    if (len > min_t_len)
                    {
                        for (i = 0; i < t_data.Length; i++)
                        {
                            (col, v2, v5) = Get_Color(t_data[i], NDS.v2info[track], track, i, de, NDS.cbm[track], v2, v5);
                            Draw_Arc(circle, x, y, r + j, i, col);
                        }

                    }
                    if (Circle_View.Checked)
                    {
                        this.Invoke(new Action(() => Update_Image()));
                    }
                    this.Invoke(new Action(() => Update_Progress_Bar(pt, at)));
                }
                r -= (t_width * skp);
                track += 1;
            }
            Invoke(new Action(() =>
            {
                //sw.Stop();
                //Text = sw.Elapsed.TotalMilliseconds.ToString();
                Set_Circular_Draw_Options(true, width);
            }));

            void Draw_Arc(FastBitmap d, int cx, int cy, int radius, int startAngle, Color color)
            {
                int segments = 22 - track / 4;
                float tempang = (float)(len) / 359.1f;
                float angle = (startAngle / tempang) * 3.14159265f / 180; // initial angle in radians
                float angleInc = 3.14159265f / (180 * segments); // angle increment
                float c, s;
                for (k = 1; k < segments; k++, angle += angleInc)
                {
                    c = (float)Math.Cos(angle);
                    s = (float)Math.Sin(angle);
                    for (j = 0; j < (int)((t_width + m) - (int)sub); j++) d.SetPixel((int)(cx + (radius + j) * c), (int)(cy + (radius + j) * s), color);
                }
            }
        }

        private Bitmap Draw_Track(Bitmap bmp, int max_Height, byte[] data, int trk, int s, int e, int tf, byte[] v2i, int d, bool w, int[] v)
        {
            byte[] tdata = new byte[data.Length];
            Buffer.BlockCopy(data, 0, tdata, 0, data.Length);
            Pen pen;
            bool v2 = false;
            bool v5 = false;
            int t_height = (max_Height / 42) - 4;
            for (int j = 0; j < tdata.Length; j++)
            {
                if (w)
                {
                    if (Cap_margins.Checked)
                    {
                        //pen = new Pen(Color.FromArgb(tdata[j] / 2, tdata[j] / 2, tdata[j] / 2), 1);
                        pen = new Pen(Color.FromArgb(tdata[j], 0, 0), 1);
                        if (j <= density[d]) pen = new Pen(Color.FromArgb(30, tdata[j], 30));
                        if (j > density[d] && j < density[d] + 5) pen = new Pen(Color.FromArgb(tdata[j], tdata[j], 30));
                    }
                    else pen = new Pen(Color.FromArgb(30, tdata[j], 30));
                    if (tf == 2 && tdata[j] == v2i[0]) v2 = true;
                    if (v2 && tdata[j] == v2i[1]) v2 = false;
                    if (Show_sec.Checked && ((tf == 3 && tdata[j] == 0x49) || v2)) pen = new Pen(Color.FromArgb(30, 30, 255));
                    if (tf == 5 && Show_sec.Checked)
                    {
                        if ((Cap_margins.Checked && (j > s || j < e)) || !Cap_margins.Checked)
                        {
                            if (v.Any(x => x == j)) v5 = !v5;
                            if (v5) pen = new Pen(Color.FromArgb((int)(tdata[j] / 1.5f), (int)(tdata[j] / 1.5f), (int)(tdata[j] / 1.5f)));
                            else pen = new Pen(Color.FromArgb(0, (int)(tdata[j] / 1.5f), (int)(tdata[j] / 1.5f)));
                        }
                    }
                }
                else
                {
                    if (Cap_margins.Checked)
                    {
                        if (j < s || j > e) pen = new Pen(Color.FromArgb(tdata[j], 0, 0), 1); //pen = new Pen(Color.FromArgb(tdata[j], tdata[j], 50));
                        else pen = new Pen(Color.FromArgb(30, tdata[j], 30));
                    }
                    else pen = new Pen(Color.FromArgb(30, tdata[j], 30));
                    if (tf == 2 && tdata[j] == v2i[0]) v2 = true;
                    if (v2 && tdata[j] == v2i[1]) v2 = false;
                    if (Show_sec.Checked && ((tf == 3 && tdata[j] == 0x49) || v2)) pen = new Pen(Color.FromArgb(30, 30, 255));
                    if (tf == 5 && Show_sec.Checked)
                    {
                        if ((Cap_margins.Checked && (j > s || j < e)) || !Cap_margins.Checked)
                        {
                            if (v.Any(x => x == j)) v5 = !v5;
                            if (v5) pen = new Pen(Color.FromArgb((int)(tdata[j] / 1.5f), (int)(tdata[j] / 1.5f), (int)(tdata[j] / 1.5f)));
                            else pen = new Pen(Color.FromArgb(0, (int)(tdata[j] / 1.5f), (int)(tdata[j] / 1.5f)));
                        }
                    }
                }
                int x1 = j;
                int y1 = 0 + (trk * ((max_Height - 16) / 42));
                int x2 = j;
                int y2 = t_height + (trk * ((max_Height - 16) / 42));
                using (var graphics = Graphics.FromImage(bmp))
                {
                    graphics.DrawLine(pen, x1, y1, x2, y2);
                }
            }
            return bmp;
        }

        private (Color, bool, bool) Get_Color(byte d, byte[] v2info, int track, int position, int density, int track_fmt, bool v2, bool v5)
        {
            Color col;
            if (vm_reverse) // && NDS.cbm[track] > 1)
            {
                var sub = 255;
                if (track_fmt == 1) col = Color.FromArgb(d, (int)(d / 3), d);
                else
                {
                    if (d == 0) sub = 0; if (d == 255) sub = 255 + 255;
                    col = Color.FromArgb(30, sub - d, 30);
                    if (track_fmt == 4) col = Color.FromArgb((int)(d / 1.5f), (int)(d / 1.5f), d);
                    if (track_fmt == 5) col = Color.FromArgb(0, (int)(d / 1.5f), (int)(d / 1.5f));
                }
            }
            else col = Color.FromArgb(30, d, 30);
            if (Cap_margins.Checked)
            {
                Color bak = col;
                col = Color.FromArgb(d / 2, d / 2, d / 2);
                if (!vm_reverse)
                {
                    if (position <= this.density[density]) col = Color.FromArgb(30, d, 30);
                    if (position > this.density[density] && position < this.density[density] + 5) col = Color.FromArgb(d, d, 30);
                }
                else
                {
                    if (position <= this.density[density]) col = bak;
                    if (position > this.density[density] && position < this.density[density] + 5) col = Color.FromArgb(d, d, 30);
                }
            }
            if (track_fmt == 2 && d == NDS.v2info[track][0]) v2 = true;
            if (v2 && d == v2info[1]) v2 = false;
            if (Show_sec.Checked && ((track_fmt == 3 && d == 0x49) || v2)) col = Color.FromArgb(30, 30, 255);
            if (track_fmt == 5 && Show_sec.Checked)
            {
                if (NDS.cbm_sector[track].Any(x => x == position)) v5 = !v5;
                if (v5) col = Color.FromArgb((int)(d / 1.5f), (int)(d / 1.5f), (int)(d / 1.5f));
                //else col = Color.FromArgb(0, (int)(d / 1.5f), (int)(d / 1.5f));
            }

            return (col, v2, v5);
        }

        private byte[] Get_Track_Data(int track)
        {
            byte[] temp = new byte[0];
            if (Out_view.Checked)
            {
                temp = new byte[NDG.Track_Length[track]];
                Buffer.BlockCopy(NDG.Track_Data[track], 0, temp, 0, temp.Length);
            }
            if (Src_view.Checked)
            {
                if (NDS.cbm[track] == 1 && (NDS.D_End[track] - NDS.D_Start[track]) >> 3 >= min_t_len)
                {
                    temp = new byte[(NDS.D_End[track] - NDS.D_Start[track]) >> 3];
                    Buffer.BlockCopy(NDS.Track_Data[track], NDS.D_Start[track] >> 3, temp, 0, (NDS.D_End[track] - NDS.D_Start[track]) >> 3);
                }
                else
                {
                    temp = new byte[NDS.Track_Data[track].Length];
                    Buffer.BlockCopy(NDS.Track_Data[track], 0, temp, 0, NDS.Track_Data[track].Length);
                }
                if ((NDS.cbm[track] > 1 && NDS.cbm[track] < 5) && NDS.D_End[track] - NDS.D_Start[track] > min_t_len)
                {
                    temp = new byte[NDS.D_End[track] - NDS.D_Start[track]];
                    Buffer.BlockCopy(NDS.Track_Data[track], NDS.D_Start[track] >> 3, temp, 0, (NDS.D_End[track] - NDS.D_Start[track]));
                }
            }
            return temp;
        }

        private void Set_Circular_Draw_Options(bool t, int size)
        {
            Disk_Image.MouseDown -= Disk_Image_MouseDown;
            Disk_Image.Cursor = Cursors.No;
            Img_zoom.Enabled = t;
            if (!t)
            {
                Circle_Render.Value = 0;
                Circle_Render.Maximum = 100;
                Circle_Render.Maximum *= 100;
                Circle_Render.Value = Circle_Render.Maximum / 100;
                Circle_Render.Visible = !t;
            }
            if (!t && Circle_View.Checked)
            {

                Save_Circle_btn.Visible = t;
                Disk_Image.Top = 0; Disk_Image.Left = 0;
            }
            if (t)
            {
                circle_full = (Bitmap)Resize_Image(circle.Bitmap, size, size, false, false);
                circle_small = (Bitmap)Resize_Image(circle.Bitmap, panPic.Width, panPic.Height, false, true);
                if (Circle_View.Checked)
                {
                    if (Img_zoom.Checked)
                    {
                        Disk_Image.Image = circle_full;
                        Disk_Image.Top = 0 - ((circle_full.Height / 2) - (panPic.Height / 2)); Disk_Image.Left = 0 - ((circle_full.Width) - panPic.Width);
                    }
                    else
                    {
                        Disk_Image.Image = circle_small;
                        Disk_Image.Top = 0;
                        Disk_Image.Left = 0;
                    }
                }
                if (Img_zoom.Checked) Disk_Image.Cursor = Cursors.Hand; else Disk_Image.Cursor = Cursors.Arrow;
                Save_Circle_btn.Visible = true;
                Circle_Render.Visible = !t;
                Disk_Image.MouseDown += Disk_Image_MouseDown;
                circle.Dispose();
                GC.Collect();
            }
            interp = t;
        }

        private void Update_Progress_Bar(int t, int at)
        {
            if (t - 1 > 0) Circle_Render.Maximum = (int)((double)Circle_Render.Value / (double)(t + 1) * at);
        }

        private void Update_Image()
        {
            /// Uncomment to show Image updates (per track processed)
            //try
            //{
            //    Disk_Image.Image = Resize_Image(circle.Bitmap, panPic.Width, panPic.Height, false, false);
            //    Disk_Image.Refresh();
            //}
            //catch { }
        }

        private void Draw_Disk(FastBitmap d, int m, int size, string file_name, string bg_text)
        {
            Brush tx = new SolidBrush(Color.FromArgb(20, 155, 155, 155));
            Font font = new Font("Ariel", 7.4f * m);
            Brush b = new SolidBrush(Write_face);
            Pen p = new Pen(Color.Black, 2);
            using (var g = Graphics.FromImage(d.Bitmap))
            {
                RectangleF rect = new RectangleF(0, 0, size, size);
                g.FillRectangle(new SolidBrush(Color.FromArgb(0, 0, 0)), rect);
                // Draw binary background text
                Add_Text(d.Bitmap, bg_text, Color.FromArgb(0, 0, 0, 0), tx, font, 0, 0, size, size);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                // Draw Disk writeable surface area
                g.FillEllipse(b, 3.5f, 3.5f, d.Width - 10, d.Height - 10);
                g.DrawEllipse(p, 3.5f, 3.5f, d.Width - 10, d.Height - 10);
                // Draw inner non-writable surface of disk
                b = new SolidBrush(Inner_face);
                g.FillEllipse(b, (float)(275 * m), (float)(275 * m), (float)(450 * m), (float)(450 * m));
                // Draw inner ring of disk (also used for image name text)
                g.FillEllipse(new SolidBrush(Color.FromArgb(7, 7, 7)), (float)(350 * m), (float)(350 * m), (float)(300 * m), (float)(300 * m));
                // Draw remaing disk surface until center hole
                g.FillEllipse(b, (float)(367.5 * m), (float)(367.5 * m), (float)(265 * m), (float)(265 * m));
                b = new SolidBrush(Color.FromArgb(0, 0, 0));
                // Draw index hole
                g.FillEllipse(b, (float)(380 * m), (float)(380 * m), (float)(240 * m), (float)(240 * m));
                g.DrawEllipse(p, (float)(380 * m), (float)(380 * m), (float)(240 * m), (float)(240 * m));
                g.FillEllipse(b, (float)(672.5 * m), (float)(487.5 * m), (float)(20 * m), (float)(20 * m));
                g.DrawEllipse(p, (float)(672.5 * m), (float)(487.5 * m), (float)(20 * m), (float)(20 * m));
            }
            // Print File name on the disk image
            Brush bsh = new SolidBrush(Color.White);
            Font fnt = new Font("Arial", (float)(11.6 * m), FontStyle.Regular);
            DrawCurvedText(Graphics.FromImage(circle.Bitmap), $"{file_name}", new Point((int)(500 * m), (int)(500 * m)), (float)(128.34 * m), 0f, fnt, bsh, false);
            // Print rotation indicator on the disk image
            bsh = new SolidBrush(Color.Yellow);
            fnt = new Font("Arial", (float)(16 * m), FontStyle.Regular);
            DrawCurvedText(Graphics.FromImage(circle.Bitmap), $"\u2192 noitatoR", new Point((int)(513 * m), (int)(503 * m)), (float)(181.67 * m), 1.45f, fnt, bsh, true);

            if (vm_reverse)
            {
                Add_Text(circle.Bitmap, "CBM", Color.FromArgb(0, 40, 40, 40), cbm_brush, new Font("Ariel", 11 * m), (int)(1 * m), (int)(1 * m), (int)(60 * m), (int)(17 * m));
                if (NDS.cbm.Any(s => s == 2) || NDS.cbm.Any(s => s == 3))
                {
                    Add_Text(circle.Bitmap, "Loader", Color.FromArgb(0, 40, 40, 40), ldr_brush, new Font("Ariel", 11 * m), (int)(1 * m), (int)(18 * m), (int)(60 * m), (int)(17 * m));
                    Add_Text(circle.Bitmap, "V-Max!", Color.FromArgb(0, 40, 40, 40), vmx_brush, new Font("Ariel", 11 * m), (int)(1 * m), (int)(35 * m), (int)(60 * m), (int)(17 * m));
                }
                if (NDS.cbm.Any(s => s == 5))
                {
                    Add_Text(circle.Bitmap, "Vorpal", Color.FromArgb(0, 40, 40, 40), vpl_brush, new Font("Ariel", 11 * m), (int)(1 * m), (int)(18 * m), (int)(60 * m), (int)(17 * m));
                }
            }
        }

        private void Add_Text(Image temp, string text, Color c, Brush brsh, Font fnt, int x1, int y1, int x2, int y2)
        {
            Graphics g = Graphics.FromImage(temp);
            Brush b = new SolidBrush(c); // (Color.FromArgb(40, 40, 40));
            RectangleF rectf = new RectangleF(x1, y1, x2, y2);
            g.FillRectangle(b, rectf);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.Bicubic;
            g.PixelOffsetMode = PixelOffsetMode.Default;
            g.DrawString($"{text}", fnt, brsh, rectf);
        }

        private Image Resize_Image(Image temp, int width, int height, bool preserveAspectRatio, bool interpolate)
        {
            int newWidth;
            int newHeight;
            if (preserveAspectRatio)
            {
                int originalWidth = temp.Width;
                int originalHeight = temp.Height;
                float percentWidth = (float)width / (float)originalWidth;
                float percentHeight = (float)height / (float)originalHeight;
                float percent = percentHeight < percentWidth ? percentHeight : percentWidth;
                newWidth = (int)(originalWidth * percent);
                newHeight = (int)(originalHeight * percent);
            }
            else
            {
                newWidth = width;
                newHeight = height;
            }
            Image newImage = new Bitmap(newWidth, newHeight);
            try
            {
                using (Graphics graphicsHandle = Graphics.FromImage(newImage))
                {
                    if (!interpolate) graphicsHandle.InterpolationMode = InterpolationMode.NearestNeighbor;
                    else graphicsHandle.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphicsHandle.DrawImage(temp, 0, 0, newWidth, newHeight);
                }
            }
            catch { }
            return newImage;
        }

        private void DrawCurvedText(Graphics g, string text, Point center, float distFromCenterToBase, float radiansToTextCenter, Font font, Brush brush, bool rev)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var circleCircumference = (float)(Math.PI * 2 * distFromCenterToBase);
            var characterWidths = GetCharacterWidths(g, text, font).ToArray();
            var characterHeight = g.MeasureString(text, font).Height;
            var textLength = characterWidths.Sum();
            float fractionOfCircumference = textLength / circleCircumference;
            float currentCharacterRadians = radiansToTextCenter - (float)(Math.PI * fractionOfCircumference);
            if (rev) currentCharacterRadians = radiansToTextCenter + (float)(Math.PI * fractionOfCircumference);
            for (int characterIndex = 0; characterIndex < text.Length; characterIndex++)
            {
                char @char = text[characterIndex];
                float x = (float)(distFromCenterToBase * Math.Sin(currentCharacterRadians));
                float y = -(float)(distFromCenterToBase * Math.Cos(currentCharacterRadians));
                using (GraphicsPath characterPath = new GraphicsPath())
                {
                    characterPath.AddString(@char.ToString(), font.FontFamily, (int)font.Style, font.Size, Point.Empty, StringFormat.GenericTypographic);
                    var pathBounds = characterPath.GetBounds();
                    var transform = new Matrix();
                    transform.Translate(center.X + x, center.Y + y);
                    var rotationAngleDegrees = currentCharacterRadians * 180F / (float)Math.PI; // - 180F;
                    if (rev) rotationAngleDegrees = currentCharacterRadians * 180F / (float)Math.PI - 180F;
                    transform.Rotate(rotationAngleDegrees);
                    transform.Translate(-pathBounds.Width / 2F, -characterHeight);
                    characterPath.Transform(transform);
                    g.FillPath(brush, characterPath);
                }

                if (characterIndex != text.Length - 1)
                {
                    var distanceToNextChar = (characterWidths[characterIndex] + characterWidths[characterIndex + 1]) / 2F;
                    float charFractionOfCircumference = distanceToNextChar / circleCircumference;
                    currentCharacterRadians += charFractionOfCircumference * (float)(2F * Math.PI);
                }
            }
        }

        private IEnumerable<float> GetCharacterWidths(Graphics graphics, string text, Font font)
        {
            var spaceLength = graphics.MeasureString(" ", font, Point.Empty, StringFormat.GenericDefault).Width;
            return text.Select(c => c == ' ' ? spaceLength : graphics.MeasureString(c.ToString(), font, Point.Empty, StringFormat.GenericTypographic).Width);
        }

        private void Save_Image_Click(object sender, EventArgs e)
        {
            string Style;
            if (Circle_View.Checked) Style = "(disk_view)"; else Style = "(flat_view)";
            Save_Dialog.Filter = "JPeg Image|*.jpg|Bitmap Image|*.bmp";
            Save_Dialog.Title = "Save Image File";
            if (Out_view.Checked) Save_Dialog.FileName = $"{fname}{fnappend}{Style}(g64).jpg";
            else Save_Dialog.FileName = $"{fname}{Style}{fext.ToLower().Replace('.', '(')}).jpg";
            Save_Dialog.ShowDialog();
            string fs = Save_Dialog.FileName;
            if (Circle_View.Checked) Save_Circular(Save_Dialog.FilterIndex);
            else Save_Flat(Save_Dialog.FilterIndex);

            void Save_Flat(int ft)
            {
                if (Img_zoom.Checked)
                {
                    if (ft == 1) Disk_Image.Image.Save(fs, ImageFormat.Jpeg);
                    if (ft == 2) Disk_Image.Image.Save(fs, ImageFormat.Bmp);
                }
                else
                {
                    Image flat = Resize_Image(flat_large, 1920, 1080, false, Flat_Interp.Checked);
                    Add_Text(flat, def_text, Color.FromArgb(0, 0, 0), Brushes.White, new Font("Ariel", 11),
                        20, flat.Height - 20, 600, flat.Height);
                    if (ft == 1) flat.Save(fs, ImageFormat.Jpeg);
                    if (ft == 2) flat.Save(fs, ImageFormat.Bmp);
                }
            }

            void Save_Circular(int ft)
            {
                if (ft == 2) circle_full.Save(fs, ImageFormat.Bmp);
                if (ft == 1) circle_full.Save(fs, ImageFormat.Jpeg);
            }
        }

        private void Disk_Image_MouseDown(object sender, MouseEventArgs e)
        {
            if (Img_zoom.Checked && e.Button == MouseButtons.Left)
            {
                Dragging = coords.Visible = true;
                xPos = e.X;
                yPos = e.Y;
            }
        }

        private void Disk_Image_MouseMove(object sender, MouseEventArgs e)
        {
            if (Img_zoom.Checked && Disk_Image.Enabled && (Dragging && sender is Control c))
            {
                var j = -(c.Width - panPic.Width);
                var g = -(c.Height - panPic.Height);
                if ((c.Top <= 0 && (e.Y + c.Top - yPos) <= 0) && (c.Top >= g && (e.Y + c.Top - yPos) >= g)) c.Top = e.Y + c.Top - yPos;
                if ((c.Left <= 0 && (e.X + c.Left - xPos) <= 0) && (c.Left >= j && (e.X + c.Left - xPos) >= j)) c.Left = e.X + c.Left - xPos;
                var x = -c.Left; var y = -c.Top;
                coords.Text = $"x:({x}) y:({y})";
            }
        }

        private void Disk_Image_MouseUp(object sender, MouseEventArgs e)
        {
            Dragging = coords.Visible = false;
        }

        private void ImageZoom_CheckedChanged(object sender, EventArgs e)
        {
            if (circle_full != null)
            {
                if (Circle_View.Checked)
                {
                    if (!Img_zoom.Checked)
                    {
                        Disk_Image.Cursor = Cursors.Arrow;
                        Disk_Image.Image = circle_small;
                        Disk_Image.Top = 0;
                        Disk_Image.Left = 0;
                    }
                    else
                    {
                        Disk_Image.Cursor = Cursors.Hand;
                        Disk_Image.Image = circle_full;
                        Disk_Image.Top = 0 - ((circle_full.Height / 2) - (panPic.Height / 2)); Disk_Image.Left = 0 - ((circle_full.Width) - panPic.Width);
                    }
                }
                else
                {
                    if (!Img_zoom.Checked)
                    {
                        Disk_Image.Cursor = Cursors.Arrow;
                        Disk_Image.Image = flat_small;
                        Disk_Image.Top = 0;
                        Disk_Image.Left = 0;
                    }
                    else
                    {
                        Disk_Image.Cursor = Cursors.Hand;
                        Disk_Image.Image = flat_large;
                        Disk_Image.Top = 0; Disk_Image.Left = 0;
                    }
                    Flat_Interp.Enabled = !Img_zoom.Checked;
                }
            }
        }
        private void Adv_Ctrl_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!busy)
            {
                if (Adv_ctrl.Controls[2] == Adv_ctrl.SelectedTab && !displayed) Data_Viewer();
                if (Adv_ctrl.Controls[0] == Adv_ctrl.SelectedTab && !drawn) Check_Before_Draw(false);
            }
        }

        private void Src_view_CheckedChanged(object sender, EventArgs e)
        {
            if (sender is RadioButton rb)
            {
                if (rb.Checked)
                {
                    Update();
                    if (!busy) Check_Before_Draw(false);
                }
            }
        }

        private void Rev_View_CheckedChanged(object sender, EventArgs e)
        {
            if (!busy)
            {
                interp = !interp;
                flat?.Abort();
                flat?.Join();
                flat = new Thread(new ThreadStart(() => Draw_Flat_Tracks(false)));
                flat.Start();
                vm_reverse = !vm_reverse;
                Check_Before_Draw(true);
            }
        }

        private void Circle_View_CheckedChanged(object sender, EventArgs e)
        {
            Flat_Interp.Visible = Flat_View.Checked;
            Rev_View.Visible = Circle_View.Checked;
            if (!busy)
            {
                if (Flat_View.Checked)
                {
                    Img_Q.Enabled = false;
                    if (Img_zoom.Checked)
                    {
                        Disk_Image.Image = flat_large;
                        Cursor(false, 0, 0);
                    }
                    else
                    {
                        Disk_Image.Image = flat_small;
                        Cursor(true, 0, 0);
                    }
                }
                if (Circle_View.Checked)
                {
                    Img_Q.Enabled = true;
                    if (Img_zoom.Checked)
                    {
                        Disk_Image.Image = circle_full;
                        Cursor(false, 0 - ((circle_full.Height / 2) - (panPic.Height / 2)), 0 - ((circle_full.Width) - panPic.Width));
                    }
                    else
                    {
                        Disk_Image.Image = circle_small;
                        Cursor(true, 0, 0);
                    }
                }

                void Cursor(bool cursor, int x, int y)
                {
                    if (cursor) Disk_Image.Cursor = Cursors.Arrow; else Disk_Image.Cursor = Cursors.Hand;
                    Disk_Image.Top = y; Disk_Image.Left = x;
                }
            }
            Flat_Interp.Enabled = !Img_zoom.Checked;
            label4.Visible = Img_Q.Visible = Circle_View.Checked;
        }

        private void Progress_Thread_Check()
        {
            if (flat.IsAlive || circ.IsAlive)
            {
                check_alive = new Thread(new ThreadStart(monitor_threads));
                check_alive.Start();
            }

            void monitor_threads()
            {
                while (flat.IsAlive || circ.IsAlive) { Invoke(new Action(() => label3.Visible = true)); Thread.Sleep(10); }
                Invoke(new Action(() =>
                {
                    label3.Visible = Circle_Render.Visible = Flat_Render.Visible = false;
                    Img_opts.Enabled = Img_style.Enabled = Img_View.Enabled = true;
                }));
            }
        }

        private void Flat_Interp_CheckedChanged(object sender, EventArgs e)
        {
            if (!busy)
            {
                Draw_Flat_Tracks(true);
            }
        }
    }
}