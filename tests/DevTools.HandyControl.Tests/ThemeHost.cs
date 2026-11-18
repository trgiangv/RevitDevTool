using FlaUI.Core.Tools;
using FlaUI.UIA3;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using AutomationElement = FlaUI.Core.AutomationElements.AutomationElement;

namespace DevTools.HandyControl.Tests;

internal sealed class ThemeHost : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
    private readonly UIA3Automation _automation = new();

    private ThemeHost(Window window)
    {
        Window = window;
    }

    public Window Window { get; }

    public static ThemeHost Show(FrameworkElement content, double width = 640, double height = 480)
    {
        return WpfSta.Invoke(() =>
        {
            var window = new Window
            {
                Width = width,
                Height = height,
                WindowStyle = WindowStyle.None,
                ShowInTaskbar = false,
                ShowActivated = false,
            };
            window.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("/DevTools.UI;component/Theme/Theme.xaml", UriKind.Relative),
            });
            window.Content = content;
            DevTools.Testing.Ui.ForegroundGuard.NoActivateOnShow(window);
            window.Show();
            content.UpdateLayout();
            return new ThemeHost(window);
        });
    }

    public static bool WaitFor(Func<bool> condition)
    {
        return Retry.WhileFalse(() => WpfSta.Invoke(condition), Timeout).Result;
    }

    public AutomationElement FindInWindow(string automationId)
    {
        return Find(() => new WindowInteropHelper(Window).Handle, automationId);
    }

    public AutomationElement FindInPopup(Popup popup, string automationId)
    {
        return Find(
            () => popup.Child is not null && PresentationSource.FromVisual(popup.Child) is HwndSource source ? source.Handle : IntPtr.Zero,
            automationId);
    }

    public void Dispose()
    {
        _automation.Dispose();
        WpfSta.Invoke(Window.Close);
    }

    private AutomationElement Find(Func<IntPtr> handle, string automationId)
    {
        var element = Retry.WhileNull(
            () =>
            {
                var root = WpfSta.Invoke(handle);
                return root == IntPtr.Zero ? null : _automation.FromHandle(root).FindFirstDescendant(cf => cf.ByAutomationId(automationId));
            },
            Timeout,
            ignoreException: true).Result;
        Assert.IsNotNull(element, automationId + " was not found.");
        return element;
    }
}
