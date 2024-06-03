using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace V_Max_Tool
{
    public static class NDS  // Global variables for Nib file source data
    {
        public static byte[][] Track_Data = new byte[0][];
        public static int[] Track_Length = new int[0];
        public static int[] Sector_Zero = new int[0];
        public static int[] D_Start = new int[0];
        public static int[] D_End = new int[0];
        public static int[] cbm = new int[0];
        public static int[] sectors = new int[0];
        public static int[][] sector_pos = new int[0][];
        public static int[] Header_Len = new int[0];
        public static int[][] cbm_sector = new int[0][];
        public static byte[][] v2info = new byte[0][];
        public static byte[] Loader = new byte[0];
        public static int[] Total_Sync = new int[0];
        public static byte[][] Disk_ID = new byte[0][];
        public static int[] Gap_Sector = new int[0];
    }

    public static class NDA  // Global variables for adjusted-sync arrays
    {
        public static byte[][] Track_Data = new byte[0][];
        public static int[] Track_Length = new int[0];
        public static int[] Sector_Zero = new int[0];
        public static int[] D_Start = new int[0];
        public static int[] D_End = new int[0];
        public static int[] sectors = new int[0];
        public static int[] Total_Sync = new int[0];
    }

    public static class NDG  // Global variables for G64 array data
    {
        public static byte[][] Track_Data = new byte[0][];
        public static int[] Track_Length = new int[0];
        public static bool L_Rot = false;
        public static int[] s_len = new int[0];
        public static byte[] newheader = new byte[0];
    }

    public static class Original  // Global variable for retaining original loader track data
    {
        public static byte[] G = new byte[0];
        public static byte[] A = new byte[0];
        public static byte[] SG = new byte[0];
        public static byte[] SA = new byte[0];
        public static byte[][] OT = new byte[0][];
    }

    class LineColor
    {
        public string Text;
        public Color Color;
    };

    public class AutoClosingMessageBox
    {
        readonly System.Threading.Timer _timeoutTimer;
        readonly string _caption;
        AutoClosingMessageBox(string text, string caption, int timeout)
        {
            _caption = caption;
            _timeoutTimer = new System.Threading.Timer(OnTimerElapsed,
                null, timeout, System.Threading.Timeout.Infinite);
            using (_timeoutTimer)
                MessageBox.Show(text, caption);
        }
        public static void Show(string text, string caption, int timeout)
        {
            new AutoClosingMessageBox(text, caption, timeout);
        }
        void OnTimerElapsed(object state)
        {
            IntPtr mbWnd = FindWindow("#32770", _caption); // lpClassName is #32770 for MessageBox
            if (mbWnd != IntPtr.Zero)
                SendMessage(mbWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            _timeoutTimer.Dispose();
        }
        const int WM_CLOSE = 0x0010;
        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        static extern IntPtr SendMessage(IntPtr hWnd, UInt32 Msg, IntPtr wParam, IntPtr lParam);
    }

    public class Message_Center : IDisposable
    {
        private readonly IWin32Window owner;
        private readonly HookProc hookProc;
        private readonly IntPtr hHook = IntPtr.Zero;

        public Message_Center(IWin32Window owner)
        {
            this.owner = owner ?? throw new ArgumentNullException("owner");
            hookProc = DialogHookProc;

            hHook = SetWindowsHookEx(WH_CALLWNDPROCRET, hookProc, IntPtr.Zero, GetCurrentThreadId());
        }

        private IntPtr DialogHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode < 0)
            {
                return CallNextHookEx(hHook, nCode, wParam, lParam);
            }

            CWPRETSTRUCT msg = (CWPRETSTRUCT)Marshal.PtrToStructure(lParam, typeof(CWPRETSTRUCT));
            IntPtr hook = hHook;

            if (msg.message == (int)CbtHookAction.HCBT_ACTIVATE)
            {
                try
                {
                    CenterWindow(msg.hwnd);
                }
                finally
                {
                    UnhookWindowsHookEx(hHook);
                }
            }
            return CallNextHookEx(hook, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            UnhookWindowsHookEx(hHook);
        }

        private void CenterWindow(IntPtr hChildWnd)
        {
            Rectangle recChild = new Rectangle(0, 0, 0, 0);
            bool success = GetWindowRect(hChildWnd, ref recChild);
            if (!success)
            {
                return;
            }
            int width = recChild.Width - recChild.X;
            int height = recChild.Height - recChild.Y;
            Rectangle recParent = new Rectangle(0, 0, 0, 0);
            success = GetWindowRect(owner.Handle, ref recParent);
            if (!success)
            {
                return;
            }

            Point ptCenter = new Point(0, 0)
            {
                X = recParent.X + ((recParent.Width - recParent.X) / 2),
                Y = recParent.Y + ((recParent.Height - recParent.Y) / 2)
            };

            Point ptStart = new Point(0, 0)
            {
                X = (ptCenter.X - (width / 2)),
                Y = (ptCenter.Y - (height / 2))
            };

            Task.Factory.StartNew(() => SetWindowPos(hChildWnd, (IntPtr)0, ptStart.X, ptStart.Y, width, height, SetWindowPosFlags.SWP_ASYNCWINDOWPOS | SetWindowPosFlags.SWP_NOSIZE | SetWindowPosFlags.SWP_NOACTIVATE | SetWindowPosFlags.SWP_NOOWNERZORDER | SetWindowPosFlags.SWP_NOZORDER));
        }

        public delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);
        public delegate void TimerProc(IntPtr hWnd, uint uMsg, UIntPtr nIDEvent, uint dwTime);
        private const int WH_CALLWNDPROCRET = 12;
        private enum CbtHookAction : int
        {
            HCBT_MOVESIZE = 0,
            HCBT_MINMAX = 1,
            HCBT_QS = 2,
            HCBT_CREATEWND = 3,
            HCBT_DESTROYWND = 4,
            HCBT_ACTIVATE = 5,
            HCBT_CLICKSKIPPED = 6,
            HCBT_KEYSKIPPED = 7,
            HCBT_SYSCOMMAND = 8,
            HCBT_SETFOCUS = 9
        }

        [DllImport("kernel32.dll")]
        static extern int GetCurrentThreadId();

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, ref Rectangle lpRect);

        [DllImport("user32.dll")]
        private static extern int MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, SetWindowPosFlags uFlags);

        [DllImport("User32.dll")]
        public static extern UIntPtr SetTimer(IntPtr hWnd, UIntPtr nIDEvent, uint uElapse, TimerProc lpTimerFunc);

        [DllImport("User32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hInstance, int threadId);

        [DllImport("user32.dll")]
        public static extern int UnhookWindowsHookEx(IntPtr idHook);

        [DllImport("user32.dll")]
        public static extern IntPtr CallNextHookEx(IntPtr idHook, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int maxLength);

        [DllImport("user32.dll")]
        public static extern int EndDialog(IntPtr hDlg, IntPtr nResult);

        [StructLayout(LayoutKind.Sequential)]
        public struct CWPRETSTRUCT
        {
            public IntPtr lResult;
            public IntPtr lParam;
            public IntPtr wParam;
            public uint message;
            public IntPtr hwnd;
        };
    }

    [Flags]
    public enum SetWindowPosFlags : uint
    {
        SWP_ASYNCWINDOWPOS = 0x4000,
        SWP_DEFERERASE = 0x2000,
        SWP_DRAWFRAME = 0x0020,
        SWP_FRAMECHANGED = 0x0020,
        SWP_HIDEWINDOW = 0x0080,
        SWP_NOACTIVATE = 0x0010,
        SWP_NOCOPYBITS = 0x0100,
        SWP_NOMOVE = 0x0002,
        SWP_NOOWNERZORDER = 0x0200,
        SWP_NOREDRAW = 0x0008,
        SWP_NOREPOSITION = 0x0200,
        SWP_NOSENDCHANGING = 0x0400,
        SWP_NOSIZE = 0x0001,
        SWP_NOZORDER = 0x0004,
        SWP_SHOWWINDOW = 0x0040,
    }

    public class FastBitmap : IDisposable
    {
        public Bitmap Bitmap { get; private set; }
        public Int32[] Bits { get; private set; }
        public bool Disposed { get; private set; }
        public int Height { get; private set; }
        public int Width { get; private set; }

        protected GCHandle BitsHandle { get; private set; }

        public FastBitmap(int width, int height)
        {
            Width = width;
            Height = height;
            Bits = new Int32[width * height];
            BitsHandle = GCHandle.Alloc(Bits, GCHandleType.Pinned);
            Bitmap = new Bitmap(width, height, width * 4, PixelFormat.Format32bppPArgb, BitsHandle.AddrOfPinnedObject());
        }

        public void SetPixel(int x, int y, Color color)
        {
            int index = x + (y * Width);
            int col = color.ToArgb();

            Bits[index] = col;
        }

        public Color GetPixel(int x, int y)
        {
            int index = x + (y * Width);
            int col = Bits[index];
            Color result = Color.FromArgb(col);

            return result;
        }

        public void Dispose()
        {
            if (Disposed) return;
            Disposed = true;
            Bitmap.Dispose();
            BitsHandle.Free();
        }
    }

    public class Gbox : GroupBox
    {
        private Color _borderColor = Color.Black;

        public Color BorderColor
        {
            get { return this._borderColor; }
            set { this._borderColor = value; }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Size tSize = TextRenderer.MeasureText(this.Text, this.Font);
            Rectangle borderRect = e.ClipRectangle;
            borderRect.Y += (tSize.Height / 2);
            borderRect.Height -= (tSize.Height / 2);
            ControlPaint.DrawBorder(e.Graphics, borderRect, this._borderColor, ButtonBorderStyle.Solid);
            Rectangle textRect = e.ClipRectangle;
            textRect.X += 6;
            textRect.Width = tSize.Width;
            textRect.Height = tSize.Height;
            e.Graphics.FillRectangle(new SolidBrush(this.BackColor), textRect);
            e.Graphics.DrawString(this.Text, this.Font, new SolidBrush(this.ForeColor), textRect);
        }
    }

    //public static class Lz77
    //{
    //    private const int RingBufferSize = 4096;
    //    private const int UpperMatchLength = 18;
    //    private const int LowerMatchLength = 2;
    //    private const int None = RingBufferSize;
    //    private static readonly int[] Parent = new int[RingBufferSize + 1];
    //    private static readonly int[] LeftChild = new int[RingBufferSize + 1];
    //    private static readonly int[] RightChild = new int[RingBufferSize + 257];
    //    private static readonly ushort[] Buffer = new ushort[RingBufferSize + UpperMatchLength - 1];
    //    private static int matchPosition, matchLength;
    //    /// <summary>
    //    ///     Size of the compressed code during and after compression
    //    /// </summary>
    //    public static int CompressedSize { get; set; }
    //    /// <summary>
    //    ///     Size of the original code packet while decompressing
    //    /// </summary>
    //    public static int DeCompressedSize { get; set; }
    //    public static double Ratio => (double)CompressedSize / DeCompressedSize * 100.0;
    //    public static byte[] Lz77Decompress(this byte[] ins)
    //    {
    //        if (ins == null)
    //            throw new Exception("Input buffer is null.");
    //        if (ins.Length == 0)
    //            throw new Exception("Input buffer is empty.");
    //        var outa = new GArray<byte>();
    //        var ina = new GArray<byte>(ins);
    //        CompressedSize = 0;
    //        DeCompressedSize = ina.Read(4).ToInt32(0);
    //        for (var i = 0; i < RingBufferSize - UpperMatchLength; i++)
    //            Buffer[i] = 0;
    //        var r = RingBufferSize - UpperMatchLength;
    //        uint flags = 7;
    //        var z = 7;
    //        while (true)
    //        {
    //            flags <<= 1;
    //            z++;
    //            if (z == 8)
    //            {
    //                if (ina.ReadEnd)
    //                    break;
    //                flags = ina.Read();
    //                z = 0;
    //            }
    //
    //            if ((flags & 0x80) == 0)
    //            {
    //                if (ina.ReadEnd)
    //                    break;
    //                var c = ina.Read();
    //                if (CompressedSize < DeCompressedSize)
    //                    outa.Write(c);
    //                Buffer[r++] = c;
    //                r &= RingBufferSize - 1;
    //                CompressedSize++;
    //            }
    //            else
    //            {
    //                if (ina.ReadEnd)
    //                    break;
    //                int i = ina.Read();
    //                if (ina.ReadEnd)
    //                    break;
    //                int j = ina.Read();
    //                j = j | ((i << 8) & 0xF00);
    //                i = ((i >> 4) & 0xF) + LowerMatchLength;
    //                for (var k = 0; k <= i; k++)
    //                {
    //                    var c = Buffer[(r - j - 1) & (RingBufferSize - 1)];
    //                    if (CompressedSize < DeCompressedSize)
    //                        outa.Write((byte)c);
    //                    Buffer[r++] = (byte)c;
    //                    r &= RingBufferSize - 1;
    //                    CompressedSize++;
    //                }
    //            }
    //        }
    //
    //        return outa.ToArray();
    //    }
    //    /// <summary>
    //    ///     E:12.5, R:17.3 E:25.0, R:35.9 E:32.3, R:47.7 E:37.5, R:56.5 E:41.5, R:63.0 E:44.8, R:67.6 E:47.6, R:71.7 E:50.0,
    //    ///     R:75.8 E:52.1, R:79.9 E:54.0, R:83.9 E:55.7, R:87.7
    //    ///     E:57.3, R:91.0 E:58.8, R:93.9 E:60.1, R:96.6 E:61.3, R:98.8 E:62.5, R:100.6 E:63.6, R:102.1 E:64.6, R:103.5 E:65.6,
    //    ///     R:104.7 E:66.5, R:105.6 E:67.4, R:106.5
    //    ///     E:68.2, R:107.2 E:69.0, R:107.8 E:69.8, R:108.3 E:70.5, R:108.7
    //    /// </summary>
    //    public static byte[] Lz77Compress(this byte[] ins, bool TestForCompressibility = false)
    //    {
    //        if (ins == null)
    //            throw new Exception("Input buffer is null.");
    //        if (ins.Length == 0)
    //            throw new Exception("Input buffer is empty.");
    //        if (TestForCompressibility)
    //            if ((int)Entropy(ins) > 61)
    //                throw new Exception("Input buffer Cannot be compressed.");
    //        matchLength = 0;
    //        matchPosition = 0;
    //        CompressedSize = 0;
    //        DeCompressedSize = ins.Length;
    //        int length;
    //        var codeBuffer = new int[UpperMatchLength - 1];
    //        var outa = new GArray<byte>();
    //        var ina = new GArray<byte>(ins);
    //        outa.Write(DeCompressedSize.GetBytes(0, 4));
    //        InitTree();
    //        codeBuffer[0] = 0;
    //        var codeBufferPointer = 1;
    //        var mask = 0x80;
    //        var s = 0;
    //        var r = RingBufferSize - UpperMatchLength;
    //        for (var i = s; i < r; i++)
    //            Buffer[i] = 0xFFFF;
    //        for (length = 0; length < UpperMatchLength && !ina.ReadEnd; length++)
    //            Buffer[r + length] = ina.Read();
    //        if (length == 0)
    //            throw new Exception("No Data to Compress.");
    //        for (var i = 1; i <= UpperMatchLength; i++)
    //            InsertNode(r - i);
    //        InsertNode(r);
    //        do
    //        {
    //            if (matchLength > length)
    //                matchLength = length;
    //            if (matchLength <= LowerMatchLength)
    //            {
    //                matchLength = 1;
    //                codeBuffer[codeBufferPointer++] = Buffer[r];
    //            }
    //            else
    //            {
    //                codeBuffer[0] |= mask;
    //                codeBuffer[codeBufferPointer++] = (byte)(((r - matchPosition - 1) >> 8) & 0xF) | ((matchLength - (LowerMatchLength + 1)) << 4);
    //                codeBuffer[codeBufferPointer++] = (byte)((r - matchPosition - 1) & 0xFF);
    //            }
    //
    //            if ((mask >>= 1) == 0)
    //            {
    //                for (var i = 0; i < codeBufferPointer; i++)
    //                    outa.Write((byte)codeBuffer[i]);
    //                CompressedSize += codeBufferPointer;
    //                codeBuffer[0] = 0;
    //                codeBufferPointer = 1;
    //                mask = 0x80;
    //            }
    //
    //            var lastMatchLength = matchLength;
    //            var ii = 0;
    //            for (ii = 0; ii < lastMatchLength && !ina.ReadEnd; ii++)
    //            {
    //                DeleteNode(s);
    //                var c = ina.Read();
    //                Buffer[s] = c;
    //                if (s < UpperMatchLength - 1)
    //                    Buffer[s + RingBufferSize] = c;
    //                s = (s + 1) & (RingBufferSize - 1);
    //                r = (r + 1) & (RingBufferSize - 1);
    //                InsertNode(r);
    //            }
    //
    //            while (ii++ < lastMatchLength)
    //            {
    //                DeleteNode(s);
    //                s = (s + 1) & (RingBufferSize - 1);
    //                r = (r + 1) & (RingBufferSize - 1);
    //                if (--length != 0)
    //                    InsertNode(r);
    //            }
    //        } while (length > 0);
    //
    //        if (codeBufferPointer > 1)
    //        {
    //            for (var i = 0; i < codeBufferPointer; i++)
    //                outa.Write((byte)codeBuffer[i]);
    //            CompressedSize += codeBufferPointer;
    //        }
    //
    //        if (CompressedSize % 4 != 0)
    //            for (var i = 0; i < 4 - CompressedSize % 4; i++)
    //                outa.Write(0);
    //        return outa.ToArray();
    //    }
    //    private static void InitTree()
    //    {
    //        for (var i = RingBufferSize + 1; i <= RingBufferSize + 256; i++)
    //            RightChild[i] = None;
    //        for (var i = 0; i < RingBufferSize; i++)
    //            Parent[i] = None;
    //    }
    //    private static void InsertNode(int r)
    //    {
    //        var cmp = 1;
    //        var p = RingBufferSize + 1 + (Buffer[r] == 0xFFFF ? 0 : Buffer[r]);
    //        RightChild[r] = LeftChild[r] = None;
    //        matchLength = 0;
    //        while (true)
    //        {
    //            if (cmp >= 0)
    //            {
    //                if (RightChild[p] != None)
    //                {
    //                    p = RightChild[p];
    //                }
    //                else
    //                {
    //                    RightChild[p] = r;
    //                    Parent[r] = p;
    //                    return;
    //                }
    //            }
    //            else
    //            {
    //                if (LeftChild[p] != None)
    //                {
    //                    p = LeftChild[p];
    //                }
    //                else
    //                {
    //                    LeftChild[p] = r;
    //                    Parent[r] = p;
    //                    return;
    //                }
    //            }
    //
    //            int i;
    //            for (i = 1; i < UpperMatchLength; i++)
    //                if ((cmp = Buffer[r + i] - Buffer[p + i]) != 0)
    //                    break;
    //            if (i > matchLength)
    //            {
    //                matchPosition = p;
    //                if ((matchLength = i) >= UpperMatchLength)
    //                    break;
    //            }
    //        }
    //
    //        Parent[r] = Parent[p];
    //        LeftChild[r] = LeftChild[p];
    //        RightChild[r] = RightChild[p];
    //        Parent[LeftChild[p]] = r;
    //        Parent[RightChild[p]] = r;
    //        if (RightChild[Parent[p]] == p)
    //            RightChild[Parent[p]] = r;
    //        else LeftChild[Parent[p]] = r;
    //        Parent[p] = None;
    //    }
    //    private static void DeleteNode(int p)
    //    {
    //        int q;
    //        if (Parent[p] == None)
    //            return;
    //        if (RightChild[p] == None)
    //        {
    //            q = LeftChild[p];
    //        }
    //        else if (LeftChild[p] == None)
    //        {
    //            q = RightChild[p];
    //        }
    //        else
    //        {
    //            q = LeftChild[p];
    //            if (RightChild[q] != None)
    //            {
    //                do
    //                {
    //                    q = RightChild[q];
    //                } while (RightChild[q] != None);
    //
    //                RightChild[Parent[q]] = LeftChild[q];
    //                Parent[LeftChild[q]] = Parent[q];
    //                LeftChild[q] = LeftChild[p];
    //                Parent[LeftChild[p]] = q;
    //            }
    //
    //            RightChild[q] = RightChild[p];
    //            Parent[RightChild[p]] = q;
    //        }
    //
    //        Parent[q] = Parent[p];
    //        if (RightChild[Parent[p]] == p)
    //            RightChild[Parent[p]] = q;
    //        else LeftChild[Parent[p]] = q;
    //        Parent[p] = None;
    //    }
    //    private static double Entropy(byte[] ba)
    //    {
    //        var map = new Dictionary<byte, int>();
    //        foreach (var c in ba)
    //            if (!map.ContainsKey(c))
    //                map.Add(c, 1);
    //            else
    //                map[c]++;
    //        double Len = ba.Length;
    //        var re = map.Select(item => item.Value / Len)
    //            .Aggregate(0.0, (current, frequency) =>
    //                current - frequency * (Math.Log(frequency) / Math.Log(2)));
    //        return re / 8.00D * 100D;
    //    }
    //}
}
