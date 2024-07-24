using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;


namespace V_Max_Tool
{

    public partial class Form1 : Form
    {
        private int dragIndex = -1;
        private bool isDragging = false;
        private Point dragStartPoint;
        private Timer scrollTimer;
        private string[] f_temp = new string[0];
        private byte[][] d_temp = new byte[0][];
        private int dropIndex = -1;
        private readonly string dir_def = "0 \"DRAG NIB/G64 TO \"START\n664 BLOCKS FREE.";
        private readonly byte[] Reverse_Endian_Table = new byte[256];
        private readonly CustomCheckedListBox Dir_Box = new CustomCheckedListBox();


        void Default_Dir_Screen()
        {
            Dir_screen.Clear();
            Dir_screen.Text = dir_def;
            Dir_screen.Select(2, 23);
            Dir_screen.SelectionBackColor = c64_text;
            Dir_screen.SelectionColor = C64_screen;
            DiskDir.Entries = 0;
            DiskDir.Sectors = new byte[0][];
            DiskDir.Entry = new byte[0][];
            DiskDir.FileName = new string[0];
            Dir_Box.Items.Clear();
        }

        void Update_Dir_Items()
        {
            f_temp = new string[DiskDir.Entries];
            d_temp = new byte[DiskDir.Entries][];
            for (int i = 0; i < Dir_Box.Items.Count; i++)
            {
                for (int j = 0; j < DiskDir.Entries; j++)
                {
                    if (DiskDir.FileName[j] == Dir_Box.Items[i].ToString())
                    {
                        f_temp[i] = DiskDir.FileName[j];
                        d_temp[i] = DiskDir.Entry[j];
                    }
                }
            }
        }

        private void Dir_Box_MouseDown(object sender, MouseEventArgs e)
        {
            dragIndex = Dir_Box.IndexFromPoint(e.Location);
            Get_File_Info(Convert.ToInt32(dragIndex));
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
            //-----------------------
            ((CustomCheckedListBox)Dir_Box).DraggingIndex = -1;
            Dir_Box.Invalidate();
            //-----------------------
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

                // Update the dragging index and invalidate
                ((CustomCheckedListBox)Dir_Box).DraggingIndex = dragIndex;
                Dir_Box.Invalidate();
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
            //--------------------
            ((CustomCheckedListBox)Dir_Box).DraggingIndex = -1;
            Dir_Box.Invalidate();
            //--------------------
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

        private void Dir_Edit_Click(object sender, LinkLabelLinkClickedEventArgs e)
        {
            groupBox3.Visible = !groupBox3.Visible;
            if (groupBox3.Visible)
            {
                Dir_Info.Text = string.Empty;
                Dir_FStart.Text = string.Empty;
            }
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
        }

        private void Dir_Apply_Click(object sender, EventArgs e)
        {
            int tot = 0;
            try
            {
                while (tot < DiskDir.Entries)
                {
                    for (int i = 0; i < DiskDir.Sectors.Length; i++)
                    {
                        int entry = 0;
                        int track = Convert.ToInt32(DiskDir.Sectors[i][0]);
                        int sector = Convert.ToInt32(DiskDir.Sectors[i][1]);
                        track -= 1;
                        if (tracks > 42) track *= 2;
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
            catch { }
        }

        private void Dir_Modify_LockBit(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (d_temp[0] == null) ReCopyArray();
            List<int> mod = new List<int>();
            for (int i = 0; i < Dir_Box.Items.Count; i++)
            {
                if (Dir_Box.Items?[i] != null && Dir_Box.GetItemChecked(i))
                {
                    if (d_temp[i] != null && d_temp[i].Length > 2)
                    {
                        mod.Add(i);
                        d_temp[i][2] = ToggleBit(d_temp[i][2], 6);
                        f_temp[i] = Get_FileName(d_temp[i]);
                    }
                }
            }
            Update_List_Items(mod.ToArray(), f_temp);
        }

        private void Dir_Modify_SplatBit(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (d_temp[0] == null) ReCopyArray();
            List<int> mod = new List<int>();
            for (int i = 0; i < Dir_Box.Items.Count; i++)
            {
                if (Dir_Box.Items?[i] != null && Dir_Box.GetItemChecked(i))
                {
                    mod.Add(i);
                    if (d_temp[i] != null && d_temp[i].Length > 2)
                    {
                        d_temp[i][2] = ToggleBit(d_temp[i][2], 7);
                        f_temp[i] = Get_FileName(d_temp[i]);
                    }
                }
            }
            Update_List_Items(mod.ToArray(), f_temp);
        }

        private void Dir_Modify_FileType(object sender, EventArgs e)
        {
            if (d_temp.Length > 0 && d_temp[0] == null) ReCopyArray();
            List<int> mod = new List<int>();
            for (int i = 0; i < Dir_Box.Items.Count; i++)
            {
                if (Dir_Box.Items?[i] != null && Dir_Box.GetItemChecked(i))
                {
                    mod.Add(i);
                    if (d_temp[i] != null && d_temp[i].Length > 2)
                    {
                        d_temp[i][2] = Handle_Bits(d_temp[i][2]);
                        f_temp[i] = Get_FileName(d_temp[i]);
                    }
                }
            }
            Update_List_Items(mod.ToArray(), f_temp);

            byte Handle_Bits(byte data)
            {
                switch (Dir_Ftype.SelectedIndex)
                {
                    case 0:
                        data = (byte)((data & 0xF0) | 0x02); // Clear bits 3, 2, 0 and set bit 1
                        break;
                    case 1:
                        data = (byte)((data & 0xF0) | 0x01); // Clear bits 3, 2, 1 and set bit 0
                        break;
                    case 2:
                        data = (byte)((data & 0xF0) | 0x03); // Clear bits 3, 2 and set bits 1, 0
                        break;
                    case 3:
                        data = (byte)((data & 0xF0) | 0x04); // Clear bits 3, 1, 0 and set bit 2
                        break;
                    case 4:
                        data = (byte)(data & 0xF0); // Clear bits 3, 2, 1, 0
                        break;
                }
                return data;
            }
        }

        string Get_FileName(byte[] data)
        {
            string sz = $"{BitConverter.ToUInt16(data, 30)}".PadRight(5);
            string fType = Get_DirectoryFileType(data[2]);
            string fName = Get_DirectoryFileName(data);
            return $"{sz}{fName}{fType}";
        }

        void ReCopyArray()
        {
            d_temp = new byte[DiskDir.Entries][];
            for (int i = 0; i < DiskDir.Entries; i++)
            {
                d_temp[i] = DiskDir.Entry[i];
            }
        }

        void Update_List_Items(int[] mod, string[] names)
        {
            Dir_Box.BeginUpdate();
            foreach (var m in mod)
            {
                if (names?[m] != null && names?[m].Length > 2)
                {
                    Dir_Box.Items[m] = $"{f_temp[m]}";
                }
            }
            Dir_Box.EndUpdate();
        }

        void Get_File_Info(int index)
        {
            try
            {
                int track = Convert.ToInt32(d_temp?[index]?[3]);
                int sector = Convert.ToInt32(d_temp?[index]?[4]);
                int strk = track;
                track -= 1;
                if (tracks > 42) track *= 2;
                if ((track >= 0 && track < tracks) && NDS.cbm?[track] == 1)
                {
                    (byte[] decoded_sector, bool nul) = Decode_CBM_Sector(NDS.Track_Data[track], sector, true);
                    if (decoded_sector != null)
                    {
                        string hex = $"{Hex_Val(decoded_sector, 3, 1)}" + $"{Hex_Val(decoded_sector, 2, 1)}";
                        byte low = decoded_sector[2];
                        byte high = decoded_sector[3];
                        Dir_Info.Text = $"Start Address : Decimal ( {(high << 8) + low} ) -  Hex [ {hex} ]";
                        Dir_FStart.Text = $"Location on disk : Track {strk}, Sector {sector + 1}";
                    }
                }
                else
                {
                    Dir_Info.Text = string.Empty;
                    Dir_FStart.Text = string.Empty;
                }
            }
            catch { }
        }

        private void Dir_ChgType_CheckedChanged(object sender, EventArgs e)
        {
            Dir_Ftype.Enabled = Dir_ChgType.Checked;
        }

        private void Dir_All_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            for (int i = 0; i < Dir_Box.Items.Count; i++) Dir_Box.SetItemCheckState(i, CheckState.Checked);
        }

        private void Dir_None_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            for (int i = 0; i < Dir_Box.Items.Count; i++) Dir_Box.SetItemCheckState(i, CheckState.Unchecked);
        }

        private void Dir_Rev_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            for (int i = 0; i < Dir_Box.Items.Count; i++)
            {
                var h = Dir_Box.GetItemCheckState(i);
                if (h == CheckState.Checked) Dir_Box.SetItemCheckState(i, CheckState.Unchecked);
                else Dir_Box.SetItemCheckState(i, CheckState.Checked);
            }
        }
    }
}