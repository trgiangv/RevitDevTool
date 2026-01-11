namespace RevitDevTool.RichTextBox.Colored.Extensions;

public static class ControlExtensions
{
    private const int WM_SETREDRAW = 0x000B;

    public static void Resume(this Control control)
    {
        var resumeUpdateMessage = Message.Create(control.Handle, WM_SETREDRAW, new IntPtr(1), IntPtr.Zero);
        InvokeWindowProcedure(control, ref resumeUpdateMessage);
        control.Refresh();
    }

    public static void Suspend(this Control control)
    {
        var suspendUpdateMessage = Message.Create(control.Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
        InvokeWindowProcedure(control, ref suspendUpdateMessage);
    }

    private static void InvokeWindowProcedure(in Control control, ref Message message)
    {
        var window = NativeWindow.FromHandle(control.Handle);
        window?.DefWndProc(ref message);
    }
}