using System.Runtime.InteropServices;

namespace RevitDevTool.RichTextBox.Colored.Extensions;

public static partial class RichTextBoxExtensions
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    private const int WM_USER = 0x400;
    private const int EM_GETSCROLLPOS = WM_USER + 221;
    private const int EM_SETSCROLLPOS = WM_USER + 222;
    private const int WM_VSCROLL = 277;
    private const int SB_PAGEBOTTOM = 7;

#if NET7_0_OR_GREATER
    [LibraryImport("user32.dll", EntryPoint = "SendMessageW")]
    private static partial IntPtr SendMessage(IntPtr hWnd, int wMsg, IntPtr wParam, IntPtr lParam);

    [LibraryImport("user32.dll", EntryPoint = "SendMessageW")]
    private static partial IntPtr SendMessage(IntPtr hWnd, int wMsg, int wParam, ref Point lParam);
#else
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int wMsg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int wMsg, int wParam, ref Point lParam);
#endif

    public static void SetRtf(this System.Windows.Forms.RichTextBox richTextBox, string rtf, bool autoScroll, CancellationToken token)
    {
        if (!richTextBox.IsHandleCreated)
        {
            var mre = new ManualResetEventSlim();
            EventHandler eh = (_, _) => mre.Set();
            richTextBox.HandleCreated += eh;
            try
            {
                if (richTextBox.IsHandleCreated) mre.Set();
                mre.Wait(token);
            }
            finally
            {
                richTextBox.HandleCreated -= eh;
            }
        }

        if (richTextBox.InvokeRequired)
        {
            richTextBox.BeginInvoke(new Action(() => SetRtfInternal(richTextBox, rtf, autoScroll)));
            return;
        }

        SetRtfInternal(richTextBox, rtf, autoScroll);
    }

    private static void SetRtfInternal(System.Windows.Forms.RichTextBox richTextBox, string rtf, bool autoScroll)
    {
        richTextBox.Suspend();
        var originalZoomFactor = richTextBox.ZoomFactor;
        var scrollPoint = new Point();

        if (!autoScroll)
        {
            SendMessage(richTextBox.Handle, EM_GETSCROLLPOS, 0, ref scrollPoint);
        }

        richTextBox.Rtf = rtf;

        if (Math.Abs(richTextBox.ZoomFactor - originalZoomFactor) > float.Epsilon)
        {
            richTextBox.ZoomFactor = originalZoomFactor;
        }

        if (!autoScroll)
        {
            SendMessage(richTextBox.Handle, EM_SETSCROLLPOS, 0, ref scrollPoint);
        }
        else
        {
            SendMessage(richTextBox.Handle, WM_VSCROLL, (IntPtr)SB_PAGEBOTTOM, IntPtr.Zero);
        }

        richTextBox.Resume();
    }
}