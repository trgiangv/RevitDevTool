using System.Runtime.InteropServices;
// ReSharper disable InconsistentNaming

namespace ZLogger.RichTextBox.Winforms.Extensions;

#if NET7_0_OR_GREATER
public static partial class RichTextBoxExtensions
{
    // ReSharper disable UnusedMethodReturnValue.Local
    [LibraryImport("user32.dll", EntryPoint = "SendMessageW")]
    private static partial IntPtr SendMessage(IntPtr hWnd, int wMsg, IntPtr wParam, IntPtr lParam);

    [LibraryImport("user32.dll", EntryPoint = "SendMessageW")]
    private static partial IntPtr SendMessage(IntPtr hWnd, int wMsg, int wParam, ref Point lParam);
    // ReSharper restore UnusedMethodReturnValue.Local
#else
public static class RichTextBoxExtensions
{
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int wMsg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int wMsg, int wParam, ref Point lParam);
#endif
    
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
            richTextBox.BeginInvoke(() => SetRtfInternal(richTextBox, rtf, autoScroll));
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
            // ReSharper disable once RedundantCast
            SendMessage(richTextBox.Handle, WM_VSCROLL, (IntPtr)SB_PAGEBOTTOM, IntPtr.Zero);
        }

        richTextBox.Resume();
    }
}