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

}
