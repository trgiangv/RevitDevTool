using System.Diagnostics;
using System.Runtime.InteropServices;
using DevTools.Hosting;

namespace DevTools.Hosting.Tests;

public sealed class StartupDialogResolverIntegrationTests
{
    [Fact]
    public async Task RunAsync_clicks_matching_startup_dialog()
    {
        using var window = FakeStartupDialog.Create(
            title: "Test unsigned add-in warning",
            buttonText: "OK");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(3));

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
                ClickTimeout = TimeSpan.FromSeconds(1),
            },
            cts.Token);

        Assert.True(result.ClickCount >= 1, $"clicked={string.Join(',', result.Clicked)} remaining={string.Join(',', result.Remaining)}");
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

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyWindow(IntPtr hwnd);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        public static IDisposable Create(string title, string buttonText)
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
            Assert.NotEqual(IntPtr.Zero, dialog);

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
            Assert.NotEqual(IntPtr.Zero, button);

            return new Handle(dialog);
        }

        private sealed class Handle(IntPtr hwnd) : IDisposable
        {
            public void Dispose()
            {
                if (hwnd != IntPtr.Zero)
                    DestroyWindow(hwnd);
            }
        }
    }
}
