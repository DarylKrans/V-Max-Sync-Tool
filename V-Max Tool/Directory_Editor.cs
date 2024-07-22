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


namespace V_Max_Tool
{

    public partial class Form1 : Form
    {
        private int dragIndex = -1;
        private bool isDragging = false;
        private Point dragStartPoint;
        private System.Windows.Forms.Timer scrollTimer;
        //private int originalTopIndex;
        string[] f_temp = new string[0];
        byte[][] d_temp = new byte[0][];
        private int dropIndex = -1;
        private readonly string dir_def = "0 \"DRAG NIB/G64 TO \"START\n664 BLOCKS FREE.";
        private readonly byte[] Reverse_Endian_Table = new byte[256];
        private readonly BufferedCheckedListBox Dir_Box = new BufferedCheckedListBox();


        private void Dir_Box_MouseDown(object sender, MouseEventArgs e)
        {
            dragIndex = Dir_Box.IndexFromPoint(e.Location);
            if (dragIndex != ListBox.NoMatches)
            {
                dragStartPoint = e.Location;
                isDragging = true;
                scrollTimer.Start();
            }
        }

        private void Dir_Box_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDragging)
            {
                if (Math.Abs(e.Y - dragStartPoint.Y) > SystemInformation.DragSize.Height)
                {
                    DoDragDrop(Dir_Box.Items[dragIndex], DragDropEffects.Move);
                    isDragging = false; // Stop further dragging
                    scrollTimer.Stop();
                }
            }
        }

        private void Dir_Box_MouseUp(object sender, MouseEventArgs e)
        {
            isDragging = false;
            scrollTimer.Stop();
            Dir_Box.TopIndex = dropIndex;//  originalTopIndex;
            Update_Dir_Items();
        }

        private void Dir_Box_DragOver(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.Move;

            Point point = Dir_Box.PointToClient(new Point(e.X, e.Y));
            int index = Dir_Box.IndexFromPoint(point);
            if (index != ListBox.NoMatches && index != dragIndex)
            {
                object item = Dir_Box.Items[dragIndex];
                bool isChecked = Dir_Box.GetItemChecked(dragIndex);
                Dir_Box.BeginUpdate();
                Dir_Box.Items.RemoveAt(dragIndex);
                Dir_Box.Items.Insert(index, item);
                Dir_Box.TopIndex = dropIndex;
                Dir_Box.SetItemChecked(index, isChecked);
                Dir_Box.EndUpdate();
                dragIndex = index;
            }
        }

        private void Dir_Box_DragDrop(object sender, DragEventArgs e)
        {
            Point point = Dir_Box.PointToClient(new Point(e.X, e.Y));
            int index = Dir_Box.IndexFromPoint(point);

            // Add a threshold to ensure it was a drag operation
            if (index != ListBox.NoMatches && index != dragIndex && Math.Abs(point.Y - dragStartPoint.Y) > SystemInformation.DragSize.Height)
            {
                object item = Dir_Box.Items[dragIndex];
                bool isChecked = Dir_Box.GetItemChecked(dragIndex);
                Dir_Box.Items.RemoveAt(dragIndex);
                Dir_Box.Items.Insert(index, item);
                Dir_Box.SetItemChecked(index, isChecked);
            }

            scrollTimer.Stop();
            // Restore the scroll position
            Dir_Box.TopIndex = dropIndex;// originalTopIndex;
            Update_Dir_Items();
        }

        private void ScrollTimer_Tick(object sender, EventArgs e)
        {
            Point clientCursorPos = Dir_Box.PointToClient(Cursor.Position);

            if (clientCursorPos.Y < 20)
            {
                // Scroll up
                if (Dir_Box.TopIndex > 0)
                {
                    Dir_Box.TopIndex--;
                }
            }
            else if (clientCursorPos.Y > Dir_Box.Height - 20)
            {
                // Scroll down
                if (Dir_Box.TopIndex < Dir_Box.Items.Count - 1)
                {
                    Dir_Box.TopIndex++;
                }
            }
            dropIndex = Dir_Box.TopIndex;
        }

        private void Dir_Box_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            for (int ix = 0; ix < Dir_Box.Items.Count; ++ix)
                if (ix != e.Index) Dir_Box.SetItemChecked(ix, false);
        }

        private void Dir_Edit_Click(object sender, EventArgs e)
        {
            groupBox3.Visible = !groupBox3.Visible;
        }

        private void Dir_Cancel_Click(object sender, EventArgs e)
        {
            if (DiskDir.Entries > 0)
            {
                Dir_Box.Items.Clear();
                for (int i = 0; i < DiskDir.Entries; ++i)
                {
                    Dir_Box.Items.Add(DiskDir.FileName[i]);
                    d_temp[i] = DiskDir.Entry[i];
                    f_temp[i] = DiskDir.FileName[i];
                }
            }
            groupBox3.Visible = false;
            busy = true;
            Dir_Lock.Checked = false;
            busy = false;
        }

        private void Dir_Apply_Click(object sender, EventArgs e)
        {
            int tot = 0;
            while (tot < DiskDir.Entries)
            {
                for (int i = 0; i < DiskDir.Sectors.Length; i++)
                {
                    int entry = 0;
                    int track = Convert.ToInt32(DiskDir.Sectors[i][0]);
                    int sector = Convert.ToInt32(DiskDir.Sectors[i][1]);
                    track -= 1;
                    if (tracks > 42) track *= 2;
                    File.WriteAllBytes($@"c:\test\original18", NDS.Track_Data[track]);
                    (byte[] decoded_sector, bool nul) = Decode_CBM_Sector(NDS.Track_Data[track], sector, true);
                    if (nul)
                    {
                        while (entry < 8 && tot < DiskDir.Entries)
                        {
                            if (entry == 0) Buffer.BlockCopy(d_temp[tot], 2, decoded_sector, 2 + (entry * 32), 30);
                            else Buffer.BlockCopy(d_temp[tot], 0, decoded_sector, 0 + (entry * 32), 32);
                            entry++;
                            tot++;
                        }
                        File.WriteAllBytes($@"c:\test\{track} sector {sector}", decoded_sector);
                        NDS.Track_Data[track] = Replace_CBM_Sector(NDS.Track_Data[track], sector, decoded_sector);
                    }
                }
            }
            groupBox3.Visible = false;
            out_track.Items.Clear();
            out_size.Items.Clear();
            out_dif.Items.Clear();
            Out_density.Items.Clear();
            out_rpm.Items.Clear();
            Process_Nib_Data(true, false, false, true);
            Default_Dir_Screen();
            Get_Disk_Directory();
        }

        private void Dir_Lock_CheckedChanged(object sender, EventArgs e)
        {
            for (int i = 0; i < Dir_Box.Items.Count; i++)
            {
                byte[] temp = new byte[1];
                if (Dir_Box.GetItemChecked(i))
                {
                    if (d_temp[i] != null && d_temp[i].Length > 2)
                    {
                        temp[0] = d_temp[i][2];
                        BitArray bt = new BitArray(temp);
                        if (Dir_Lock.Checked) bt[6] = true; else bt[6] = false;
                        bt.CopyTo(temp, 0);
                        d_temp[i][2] = temp[0];
                        string sz = $"{BitConverter.ToUInt16(d_temp[i], 30)}".PadRight(5);
                        string fType = Get_DirectoryFileType(temp[0]);
                        string fName = Get_DirectoryFileName(d_temp[i]);
                        Dir_Box.Items[i] = $"{sz}{fName}{fType}";
                        f_temp[i] = $"{sz}{fName}{fType}";
                    }
                }
            }
        }
    }
}
