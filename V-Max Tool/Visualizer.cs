using System;
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
        private bool CV_cpp = true;
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
        private readonly Brush rpl_brush = new SolidBrush(Color.FromArgb(200, 200, 30));
        private readonly Brush key_brush = new SolidBrush(Color.FromArgb(30, 200, 30));
        private readonly Brush nds_brush = new SolidBrush(Color.FromArgb(120, 102, 153));
        private readonly Brush[] trk_brush = new SolidBrush[2]; // (Color.FromArgb(200, 200, 200));
        private readonly Color Write_face = Color.FromArgb(41, 40, 36);
        private readonly Color Inner_face = Color.FromArgb(50, 49, 44);

        private void Draw_Init_Img(string bg_text)
        {
            trk_brush[0] = new SolidBrush(Color.FromArgb(200, 200, 200));
            trk_brush[1] = new SolidBrush(Color.FromArgb(125, 125, 125));
            var m = (Img_Q.SelectedIndex + 1) * 1000;
            circle = new FastBitmap(m, m);
            Draw_Disk(circle, 3, m, this.Text, bg_text);
            circle_full = (Bitmap)Resize_Image(circle.Bitmap, panPic.Width, panPic.Height, false, true);
            circle_small = (Bitmap)Resize_Image(circle.Bitmap, panPic.Width, panPic.Height, false, true);
            flat_large = (Bitmap)Resize_Image(circle.Bitmap, panPic.Width, panPic.Height, false, true);
            flat_small = (Bitmap)Resize_Image(circle.Bitmap, panPic.Width, panPic.Height, false, true);
            Disk_Image.Image = Resize_Image(circle.Bitmap, panPic.Width, panPic.Height, false, true);
        }

        private void Draw_Flat_Tracks(bool interpolate, bool wait)
        {
            if (wait) Thread.Sleep(1000);

            string ext = "";
            int d = 0;
            Font font = new Font("Arial", 11);
            bool halftracks = tracks > 42;
            double ht = halftracks ? 0.5 : 0;
            int trk = (tracks > 42) ? 2 : 1;
            if (!interpolate)
            {
                flat_large = new Bitmap(8224, (42 * 14) - 16);
                Bitmap t = new Bitmap(flat_large.Width, flat_large.Height);
                int actualTracks = 0, processedTracks = 0;

                // Count valid tracks
                for (int h = 0; h < tracks; h++)
                {
                    if (NDG.Track_Length[h] > min_t_len && NDS.cbm[h] < secF.Length - 1) actualTracks++;
                }

                if (actualTracks > 0)
                {
                    Invoke(new Action(() =>
                    {
                        Flat_Render.Value = 0;
                        Flat_Render.Maximum = 100 * 100;
                        Flat_Render.Value = Flat_Render.Maximum / 100;
                        Flat_Render.Visible = true;
                    }));
                }

                for (int i = 0; i < tracks; i++)
                {
                    bool shouldDrawTrack = false;
                    if (Out_view.Checked && NDG.Track_Length[i] > min_t_len && NDS.cbm[i] < secF.Length - 1)
                    {
                        d = Get_Density(NDG.Track_Data[i].Length);
                        t = Draw_Track(flat_large, (42 * 14), NDG.Track_Data[i], (int)ht, 0, 0, NDS.cbm[i], NDS.v2info[i], d, Out_view.Checked, NDS.cbm_sector[i]);
                        ext = "(flat_tracks).g64";
                        shouldDrawTrack = true;
                    }
                    else if (Src_view.Checked)
                    {
                        int ds = NDS.D_Start[i], de = NDS.D_End[i];
                        d = (NDS.Track_Length?[i] != 0) ? Get_Density(NDS.Track_Length[i] >> 3) : density_map[i / trk];
                        if (NDS.Track_Data[i].Any(s => s != 0x00)) // View all tracks that aren't all 0x00 bytes
                        {
                            t = Draw_Track(flat_large, (42 * 14), NDS.Track_Data[i], (int)ht, ds, de, NDS.cbm[i], NDS.v2info[i], d, Out_view.Checked, NDS.cbm_sector[i]);
                            ext = $"(flat_tracks){fext}";
                            shouldDrawTrack = true;
                        }
                    }

                    if (shouldDrawTrack)
                    {
                        processedTracks++;
                        if (processedTracks - 1 > 0)
                        {
                            Invoke(new Action(() => Flat_Render.Maximum = (int)((double)Flat_Render.Value / (double)(processedTracks + 1) * actualTracks)));
                        }
                    }

                    ht += halftracks ? 0.5 : 1;
                }

                flat_large = (Bitmap)Resize_Image(t, t.Width, t.Height, false, false);
                flat_small = (Bitmap)Resize_Image(t, pan_defw, pan_defh - 16, false, Flat_Interp.Checked);

                Add_Text(flat_small, $"{fname}{fnappend}{ext}", Color.FromArgb(40, 40, 40), Brushes.White, font, 20, flat_small.Height - 20, 600, flat_small.Height);
                Add_Text(flat_large, $"{fname}{fnappend}{ext}", Color.FromArgb(40, 40, 40), Brushes.White, font, 20, flat_large.Height - 40, 600, flat_large.Height);

                Invoke(new Action(() =>
                {
                    if (Flat_View.Checked)
                    {
                        Disk_Image.Cursor = Img_zoom.Checked ? Cursors.Hand : Cursors.Arrow;
                        Disk_Image.Image = Img_zoom.Checked ? flat_large : flat_small;
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
                Add_Text(flat_small, $"{fname}{fnappend}{ext}", Color.FromArgb(40, 40, 40), Brushes.White, font, 20, flat_small.Height - 20, 600, flat_small.Height);
                Disk_Image.Image = flat_small;
            }

            GC.Collect();
        }

        private void Draw_Circular_Tracks(bool wait)
        {
            //Stopwatch sw = new Stopwatch();
            if (wait) Thread.Sleep(1000);
            int scale = 0;
            int activeTracks = 0;
            for (int h = 0; h < tracks; h++) if (NDG.Track_Length[h] > min_t_len && NDS.cbm[h] < secF.Length - 1) activeTracks++;
            Invoke(new Action(() =>
            {
                scale = Img_Q.SelectedIndex + 1;
                Set_Circular_Draw_Options(false, 0);
            }));

            double sub = 1.25;
            int imageSize = scale * 1000;
            int x = imageSize >> 1;
            int y = imageSize >> 1;
            int radius = (int)(x / 1.0316368638f) + (5 * scale);
            int trackWidth = (int)(3.1f * scale);
            string fileExtension = Src_view.Checked ? ".nib" : ".g64";
            string fileName = $"{fname}{fnappend}{fileExtension}";
            Random random = new Random();
            bool isReverse = vm_reverse;

            circle = new FastBitmap(imageSize, imageSize);

            int sampleTrack = GetSampleTrack(random);
            string bgText = ToBinary(Encoding.ASCII.GetString(NDS.Track_Data[sampleTrack], 0, 1225));
            Draw_Disk(circle, scale, imageSize, fileName, bgText);
            int skipFactor = tracks <= 42 ? 2 : 1;
            int progress = 0;
            IntPtr bmpPtr = circle.GetPixelPtr();
            for (int track = 0; track < tracks && radius > 80; track++)
            {
                if (NDG.Track_Length[track] > min_t_len && NDS.cbm[track] < secF.Length - 1)
                {
                    int sb = 0;
                    progress++;
                    byte[] trackData = Get_Track_Data(track);
                    int dataLength = trackData.Length;
                    int[] colors = new int[dataLength];
                    if (dataLength > min_t_len)
                    {
                        int density = Get_Density(dataLength);
                        bool v2 = false;
                        bool v5 = false;
                        for (int i = 0; i < dataLength; i++)
                        {
                            if (NDS.cbm[track] == 6 && trackData[i] == 0x7b) sb++; else sb = 0;
                            var (color, updatedV2, updatedV5) = Get_Color(trackData[i], NDS.v2info[track], track, i, density, NDS.cbm[track], v2, v5, sb);
                            v2 = updatedV2;
                            v5 = updatedV5;
                            colors[i] = color.ToArgb();
                        }
                        if (usecpp && CV_cpp)
                        {
                            try
                            {
                                NativeMethods.Draw_Arc(bmpPtr, imageSize, imageSize, x, y, radius, colors, colors.Length, track, dataLength, trackWidth, sub);
                            }
                            catch { CV_cpp = false; }
                        }
                        if (!CV_cpp || !usecpp) Draw_Arc(circle, x, y, radius, colors, track, dataLength, trackWidth, sub);
                    }

                    if (Circle_View.Checked && Cores <= 3) Update_Image();
                    Invoke(new Action(() => Update_Progress_Bar(progress, activeTracks)));
                }
                radius -= (trackWidth * skipFactor);
            }

            Invoke(new Action(() => Set_Circular_Draw_Options(true, imageSize)));
        }

        private int GetSampleTrack(Random random)
        {
            int track = 100;
            int attempts = 0;

            while (attempts < 1000)
            {
                track = random.Next(0, tracks - 1);
                if (NDS.Track_Length[track] > 0) break;
                attempts++;
            }

            return track;
        }

        private void Draw_Arc(FastBitmap bitmap, int centerX, int centerY, int radius, int[] color, int track, int len, int trackWidth, double sub)
        {
            for (int startAngle = 0; startAngle < color.Length; startAngle++)
            {
                int segments = 22 - track / 4;
                float tempAngle = len / 359.1f;
                float angle = (startAngle / tempAngle) * 3.14159265f / 180; // initial angle in radians
                float angleInc = 3.14159265f / (180 * segments); // angle increment
                Color clr = Color.FromArgb(color[startAngle]);
                for (int k = 1; k < segments; k++, angle += angleInc)
                {
                    float cos = (float)Math.Cos(angle);
                    float sin = (float)Math.Sin(angle);
                    for (int j = 0; j < trackWidth - (int)sub; j++)
                    {
                        bitmap.SetPixel((int)(centerX + (radius + j) * cos), (int)(centerY + (radius + j) * sin), clr);
                    }
                }
            }
        }

        private Bitmap Draw_Track(Bitmap bmp, int maxHeight, byte[] trackData, int track, int start, int end, int trackFormat, byte[] v2info, int densityIndex, bool write, int[] v)
        {
            int trackHeight = (maxHeight / 42) - 4;
            int segmentHeight = (maxHeight - 16) / 42;
            int c = (track % 2);
            int sb = 0;
            int skip = tracks > 42 ? 2 : 1;
            float div = (c == 0 || trackFormat == 4) ? 1.0f : 1.7f;
            bool v2 = false, v5 = false;

            // Cache colors and pens if possible
            Pen pen = new Pen(Color.Empty, 1);

            // Precompute commonly used values
            int segmentYOffset = track * segmentHeight;
            int xOffset = 32;

            // Use graphics object once
            using (var graphics = Graphics.FromImage(bmp))
            {
                for (int i = 0; i < trackData.Length; i++)
                {
                    byte data = trackData[i];

                    if (trackFormat == 6 && data == 0x7b) sb++;
                    else sb = 0;

                    bool inDensityRange = i <= density[densityIndex];
                    bool inMargins = i > start && i < end;

                    // Use the Get_Color method to determine the color
                    (Color color, bool newV2, bool newV5) = Get_Color(data, v2info, track * skip, i, densityIndex, trackFormat, v2, v5, sb, true);

                    // Apply division modifier outside the loop if possible or cache results
                    pen.Color = ApplyDivisionModifier(color, div);
                    v2 = newV2;
                    v5 = newV5;

                    // Draw the line segment
                    int x1 = i + xOffset, y1 = segmentYOffset, x2 = i + xOffset, y2 = trackHeight + segmentYOffset;
                    graphics.DrawLine(pen, x1, y1, x2, y2);
                }

                // Cache Font and Brush objects
                Font font = new Font("Ariel", 11);
                Brush brush = trk_brush[c];

                // Use the precomputed values for Add_Text method
                Add_Text(bmp, $"{track + 1}", Color.FromArgb(0, 40, 40, 40), brush, font, 0, -5 + (track * 13), 60, 17 * track);
            }

            return bmp;
        }

        private (Color, bool, bool) Get_Color(byte d, byte[] v2info, int track, int position, int density, int trackFmt, bool v2, bool v5, int sb, bool flat = false)
        {
            Color col;
            int dd = d >> 1;
            if (vm_reverse)
            {
                int sub = (d == 0) ? 0 : (d == 255) ? 510 : 255;
                switch (trackFmt)
                {
                    case 0: col = col = Color.FromArgb((dd * 100) >> 6, (dd * 85) >> 6, (dd * 127) >> 6); break;
                    case 1: col = Color.FromArgb(d, d / 3, d); break;
                    case 4: col = Color.FromArgb(dd, dd, d); break;
                    case 5: col = Color.FromArgb(0, dd, dd); break;
                    case 6: col = Color.FromArgb(dd, dd, 0); break;
                    case 10: col = Color.FromArgb(dd, dd, 0); break;
                    default: col = Color.FromArgb(30, sub - d, 30); break;
                }
            }
            else
            {
                col = Color.FromArgb(30, d, 30);
            }

            if (flat && trackFmt == 0)
            {
                col = Color.FromArgb(dd, dd, dd);
                return (col, v2, v5);
            }

            if (Cap_margins.Checked)
            {
                Color backupColor = col;
                if (!flat) col = Color.FromArgb(dd, dd, dd);
                else col = Color.FromArgb(d >> 1, 0, 0);
                if (position <= this.density[density])
                {
                    col = vm_reverse ? backupColor : Color.FromArgb(30, d, 30);
                }
                else if (position > this.density[density] && position < this.density[density] + 5)
                {
                    col = Color.FromArgb(d, d, 30);
                }
            }

            if (trackFmt == 2 && d == v2info[0]) v2 = true;
            if (v2 && d == v2info[1]) v2 = false;
            if (Show_sec.Checked && ((trackFmt == 3 && d == 0x49) || v2)) col = Color.FromArgb(30, 30, 255);

            if (trackFmt == 5 && Show_sec.Checked && position <= this.density[density])
            {
                if (NDS.cbm_sector[track].Any(x => x == position)) v5 = !v5;
                col = v5 ? Color.FromArgb(dd, dd, dd) : col;
            }

            if (trackFmt == 6 && Show_sec.Checked)
            {
                if (d == 0xff) col = Color.FromArgb(30, 30, d);
                if (d == 0x7b && sb > 3) col = Color.FromArgb(128, 130, 188);
            }

            if ((trackFmt == 1 || trackFmt == 10) && Show_sec.Checked)
            {
                if (d == 0xff) col = Color.FromArgb(d, d, 0);
            }

            return (col, v2, v5);
        }

        private byte[] Get_Track_Data(int track)
        {
            byte[] temp = new byte[0];

            if (Out_view.Checked)
            {
                try
                {
                    int length = NDG.Track_Length[track];
                    temp = new byte[length];
                    Buffer.BlockCopy(NDG.Track_Data[track], 0, temp, 0, length);
                }
                catch { }
            }

            if (Src_view.Checked)
            {
                int start = NDS.D_Start[track] >> 3;
                int end = NDS.D_End[track] >> 3;
                int length = end - start;

                if (NDS.cbm[track] == 1 && length >= min_t_len)
                {
                    temp = new byte[length];
                    Buffer.BlockCopy(NDS.Track_Data[track], start, temp, 0, length);
                }
                else
                {
                    length = NDS.Track_Data[track].Length;
                    temp = new byte[length];
                    Buffer.BlockCopy(NDS.Track_Data[track], 0, temp, 0, length);
                }

                if (NDS.cbm[track] > 1 && NDS.cbm[track] < 5 && length >= min_t_len)
                {
                    length = NDS.D_End[track] - NDS.D_Start[track];
                    temp = new byte[length];
                    Buffer.BlockCopy(NDS.Track_Data[track], start, temp, 0, length);
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
            try
            {
                Disk_Image.Image = Resize_Image(circle.Bitmap, panPic.Width, panPic.Height, false, false);
                Disk_Image.Refresh();
            }
            catch { }
        }

        private void Draw_Disk(FastBitmap d, int m, int size, string file_name, string bg_text = "")
        {
            using (var g = Graphics.FromImage(d.Bitmap))
            {
                //var fontSmall = new Font("Ariel", 7.4f * m);
                var fontSmall = new Font("Ariel", 9.4f * m); // <- binary bits (background)
                var fontLarge = new Font("Arial", (float)(13.4 * m), FontStyle.Regular); // <- file name (black ring)
                var fontLarger = new Font("Arial", (float)(18 * m), FontStyle.Italic); // <- rotataion indicator
                var txBrush = new SolidBrush(Color.FromArgb(20, 155, 155, 155));
                var writeBrush = new SolidBrush(Write_face);
                var innerBrush = new SolidBrush(Inner_face);
                var blackBrush = new SolidBrush(Color.Black);
                var holeBrush = new SolidBrush(Color.FromArgb(0, 0, 0));
                var whiteBrush = new SolidBrush(Color.White);
                var yellowBrush = new SolidBrush(Color.Yellow);
                var blackPen = new Pen(Color.Black, 2);

                g.FillRectangle(blackBrush, 0, 0, size, size);
                Add_Text(d.Bitmap, bg_text, Color.FromArgb(0, 0, 0, 0), txBrush, fontSmall, 0, 0, size, size);

                g.SmoothingMode = SmoothingMode.AntiAlias;
                DrawEllipse(g, writeBrush, blackPen, 3.5f, 3.5f, d.Width - 10, d.Height - 10);
                g.FillEllipse(innerBrush, 275 * m, 275 * m, 450 * m, 450 * m);
                g.FillEllipse(blackBrush, 350 * m, 350 * m, 300 * m, 300 * m);
                g.FillEllipse(innerBrush, 367.5f * m, 367.5f * m, 265 * m, 265 * m);
                DrawEllipse(g, holeBrush, blackPen, 380 * m, 380 * m, 240 * m, 240 * m);
                DrawEllipse(g, holeBrush, blackPen, 672.5f * m, 487.5f * m, 20 * m, 20 * m);

                DrawCurvedText(g, file_name, new Point((int)(500 * m), (int)(500 * m)), 128.34f * m, 0f, fontLarge, whiteBrush, false);
                DrawCurvedText(g, "\u2192 noitatoR", new Point((int)(500 * m), (int)(500 * m)), 200.65f * m, 2.55f, fontLarger, yellowBrush, true);
                int clm = -16;
                if (vm_reverse)
                {
                    Add_Text(d.Bitmap, "CBM", Color.FromArgb(0, 40, 40, 40), cbm_brush, new Font("Ariel", 11 * m), 1 * m, (clm += 17) * m, 60 * m, 17 * m);
                    if (NDS.cbm.Any(s => s == 2 || s == 3))
                    {
                        Add_Text(d.Bitmap, "V-Max!", Color.FromArgb(0, 40, 40, 40), vmx_brush, new Font("Ariel", 11 * m), 1 * m, (clm += 17) * m, 60 * m, 17 * m);
                    }
                    if (NDS.cbm.Any(s => s == 4))
                    {
                        Add_Text(d.Bitmap, "Loader", Color.FromArgb(0, 40, 40, 40), ldr_brush, new Font("Ariel", 11 * m), 1 * m, (clm += 17) * m, 60 * m, 17 * m);
                    }
                    if (NDS.cbm.Any(s => s == 5))
                    {
                        Add_Text(d.Bitmap, "Vorpal", Color.FromArgb(0, 40, 40, 40), vpl_brush, new Font("Ariel", 11 * m), 1 * m, (clm += 17) * m, 60 * m, 17 * m);
                    }
                    if (NDS.cbm.Any(s => s == 6) || NDS.cbm.Any(s => s == 10))
                    {
                        string result = (NDS.cbm.Any(s => s == 6)) ? "Rapidlok" : "MicroProse";
                        Add_Text(d.Bitmap, result, Color.FromArgb(0, 40, 40, 40), rpl_brush, new Font("Ariel", 11 * m), 1 * m, (clm += 17) * m, 80 * m, 17 * m);
                        if (NDS.cbm.Any(s => s == 6)) Add_Text(d.Bitmap, "Key", Color.FromArgb(0, 40, 40, 40), key_brush, new Font("Ariel", 11 * m), 1 * m, (clm += 17) * m, 80 * m, 17 * m);
                    }
                    if (NDS.cbm.Any(s => s == 0))
                    {
                        Add_Text(d.Bitmap, "Non-DOS", Color.FromArgb(0, 40, 40, 40), nds_brush, new Font("Ariel", 11 * m), 1 * m, (clm += 17) * m, 80 * m, 17 * m);
                    }
                }
            }

            void DrawEllipse(Graphics g, Brush fillBrush, Pen outlinePen, float x, float y, float width, float height)
            {
                g.FillEllipse(fillBrush, x, y, width, height);
                g.DrawEllipse(outlinePen, x, y, width, height);
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
            if (!Img_zoom.Checked || !Disk_Image.Enabled || !Dragging || !(sender is Control c))
                return;

            var maxLeft = -(c.Width - panPic.Width);
            var maxTop = -(c.Height - panPic.Height);

            var newTop = e.Y + c.Top - yPos;
            var newLeft = e.X + c.Left - xPos;

            if (newTop <= 0 && newTop >= maxTop)
                c.Top = newTop;

            if (newLeft <= 0 && newLeft >= maxLeft)
                c.Left = newLeft;

            coords.Text = $"x:({-c.Left}) y:({-c.Top})";
        }

        private void Disk_Image_MouseUp(object sender, MouseEventArgs e)
        {
            Dragging = coords.Visible = false;
        }

        private void ImageZoom_CheckedChanged(object sender, EventArgs e)
        {
            if (circle_full == null) return;

            if (Circle_View.Checked)
            {
                UpdateDiskImage(Img_zoom.Checked, circle_full, circle_small, true);
            }
            else
            {
                UpdateDiskImage(Img_zoom.Checked, flat_large, flat_small, false);
                Flat_Interp.Enabled = !Img_zoom.Checked;
            }

            void UpdateDiskImage(bool isZoomed, Image largeImage, Image smallImage, bool isCircleView)
            {
                Disk_Image.Cursor = isZoomed ? Cursors.Hand : Cursors.Arrow;
                Disk_Image.Image = isZoomed ? largeImage : smallImage;

                if (isCircleView && isZoomed)
                {
                    Disk_Image.Top = 0 - ((largeImage.Height / 2) - (panPic.Height / 2));
                    Disk_Image.Left = 0 - (largeImage.Width - panPic.Width);
                }
                else
                {
                    Disk_Image.Top = 0;
                    Disk_Image.Left = 0;
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
                flat = new Thread(new ThreadStart(() => Draw_Flat_Tracks(false, true)));
                flat.Start();
                vm_reverse = !vm_reverse;
                //Check_Before_Draw(true);
                Check_Before_Draw(false);
            }
        }

        private void Circle_View_CheckedChanged(object sender, EventArgs e)
        {
            Flat_Interp.Visible = Flat_View.Checked;
            Rev_View.Visible = Circle_View.Checked;

            if (busy) return;

            Img_Q.Enabled = Circle_View.Checked;

            if (Flat_View.Checked)
            {
                SetImage(Img_zoom.Checked ? flat_large : flat_small, Img_zoom.Checked ? Cursors.Hand : Cursors.Arrow, 0, 0);
            }
            else if (Circle_View.Checked)
            {
                SetImage(Img_zoom.Checked ? circle_full : circle_small, Img_zoom.Checked ? Cursors.Hand : Cursors.Arrow,
                         Img_zoom.Checked ? 0 - ((circle_full.Height / 2) - (panPic.Height / 2)) : 0,
                         Img_zoom.Checked ? 0 - ((circle_full.Width) - panPic.Width) : 0);
            }

            Flat_Interp.Enabled = !Img_zoom.Checked;
            label4.Visible = Img_Q.Visible = Circle_View.Checked;
        }

        private void SetImage(Bitmap image, Cursor cursor, int top, int left)
        {
            Disk_Image.Image = image;
            Disk_Image.Cursor = cursor;
            Disk_Image.Top = top;
            Disk_Image.Left = left;
        }

        private void Progress_Thread_Check(bool wait)
        {
            if (wait) Thread.Sleep(1000);
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
                Draw_Flat_Tracks(true, false);
            }
        }
    }
}