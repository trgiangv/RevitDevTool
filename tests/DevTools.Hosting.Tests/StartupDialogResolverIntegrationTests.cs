using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DevTools.Hosting.Tests;

[TestClass]
public sealed class StartupDialogResolverIntegrationTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task RunAsync_clicks_matching_startup_dialog()
    {
        using var window = FakeStartupDialog.Create(
            title: "Test unsigned add-in warning",
            buttonText: "OK");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(1));

        var result = await StartupDialogResolver.RunAsync(
            Process.GetCurrentProcess().Id,
            new StartupDialogOptions
            {
                WindowClassName = "#32770",
                ButtonClassName = "Button",
                DialogTitleKeywords = ["unsigned add-in"],
                PreferredButtonKeywords = ["OK"],
                BlockedButtonKeywords = ["Cancel"],
                PollInterval = TimeSpan.FromMilliseconds(50),
                ClickTimeout = TimeSpan.FromMilliseconds(250),
            },
            cts.Token);

        Assert.IsTrue(result.ClickCount >= 1, $"clicked={string.Join(',', result.Clicked)} remaining={string.Join(',', result.Remaining)}");
    }

    private static class FakeStartupDialog
    {
        private const int WsChild = 0x40000000;
        private const int WsVisible = 0x10000000;
        private const int BsPushbutton = 0x00000000;

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateWindowEx(
            int exStyle,
            string className,
            string windowName,
            int style,
            int x,
            int y,
            int width,
            int height,
            IntPtr parent,
            IntPtr menu,
            IntPtr instance,
            IntPtr param);

        [DllImport("user32.dll")]
        private static extern int GetMessage(out Msg msg, IntPtr hwnd, uint min, uint max);

        [DllImport("user32.dll")]
        private static extern bool TranslateMessage(ref Msg msg);

        [DllImport("user32.dll")]
        private static extern IntPtr DispatchMessage(ref Msg msg);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PostThreadMessage(uint threadId, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        [StructLayout(LayoutKind.Sequential)]
        private struct Msg
        {
            public IntPtr Hwnd;
            public uint Message;
            public IntPtr WParam;
            public IntPtr LParam;
            public uint Time;
            public int X;
            public int Y;
        }

        public static IDisposable Create(string title, string buttonText)
        {
            uint threadId = 0;
            using var ready = new ManualResetEventSlim();
            var thread = new Thread(() =>
            {
                threadId = GetCurrentThreadId();
                CreateWindows(title, buttonText);
                ready.Set();
                while (GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
                {
                    TranslateMessage(ref msg);
                    DispatchMessage(ref msg);
                }
            })
            { IsBackground = true };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            ready.Wait();
            return new Handle(threadId, thread);
        }

        private static void CreateWindows(string title, string buttonText)
        {
            var instance = GetModuleHandle(null);
            var dialog = CreateWindowEx(
                0,
                "#32770",
                title,
                WsVisible,
                100,
                100,
                320,
                160,
                IntPtr.Zero,
                IntPtr.Zero,
                instance,
                IntPtr.Zero);
            Assert.AreNotEqual(IntPtr.Zero, dialog);

            var button = CreateWindowEx(
                0,
                "Button",
                buttonText,
                WsChild | WsVisible | BsPushbutton,
                20,
                20,
                80,
                28,
                dialog,
                IntPtr.Zero,
                instance,
                IntPtr.Zero);
            Assert.AreNotEqual(IntPtr.Zero, button);
        }

        private sealed class Handle(uint threadId, Thread thread) : IDisposable
        {
            private const uint WmQuit = 0x0012;

            public void Dispose()
            {
                PostThreadMessage(threadId, WmQuit, IntPtr.Zero, IntPtr.Zero);
                thread.Join();
            }
        }
    }
}
