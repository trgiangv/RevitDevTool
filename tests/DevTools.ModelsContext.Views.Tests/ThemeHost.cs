using FlaUI.Core.Tools;
using FlaUI.UIA3;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using AutomationElement = FlaUI.Core.AutomationElements.AutomationElement;

namespace DevTools.ModelsContext.Views.Tests;

internal sealed class ThemeHost : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
    private readonly UIA3Automation _automation = new();

    private ThemeHost(Window window)
    {
        Window = window;
    }

    public Window Window { get; }

    public static ThemeHost Show(FrameworkElement content, double width = 320, double height = 800)
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
            window.Show();
            window.Activate();
            content.UpdateLayout();
            return new ThemeHost(window);
        });
    }

    public static bool WaitFor(Func<bool> condition)
    {
        return Retry.WhileFalse(() => WpfSta.Invoke(condition), Timeout).Result;
    }

    public static bool WaitForUi(Func<bool> condition)
    {
        return Retry.WhileFalse(condition, Timeout).Result;
    }

    public AutomationElement FindInWindow(string automationId)
    {
        var element = Retry.WhileNull(() => TryFind(Handle, automationId), Timeout, ignoreException: true).Result;
        Assert.IsNotNull(element, automationId + " was not found.");
        return element;
    }

    public AutomationElement? TryFindInWindow(string automationId)
    {
        return TryFind(Handle, automationId);
    }

    public AutomationElement FindInMenu(ContextMenu menu, string automationId)
    {
        var element = Retry.WhileNull(() => TryFind(() => MenuHandle(menu), automationId), Timeout, ignoreException: true).Result;
        Assert.IsNotNull(element, automationId + " was not found in the menu.");
        return element;
    }

    public AutomationElement? TryFindInMenu(ContextMenu menu, string automationId)
    {
        return TryFind(() => MenuHandle(menu), automationId);
    }

    public AutomationElement Row(string name)
    {
        var list = FindInWindow("Tree.Rows");
        var row = Retry.WhileNull(
            () => list.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.ListItem).And(cf.ByName(name))),
            Timeout,
            ignoreException: true).Result;
        Assert.IsNotNull(row, name + " row was not found.");
        return row;
    }

    public void Dispose()
    {
        _automation.Dispose();
        WpfSta.Invoke(Window.Close);
    }

    private IntPtr Handle()
    {
        return WpfSta.Invoke(() => new WindowInteropHelper(Window).Handle);
    }

    private static IntPtr MenuHandle(ContextMenu menu)
    {
        return WpfSta.Invoke(() => PresentationSource.FromVisual(menu) is HwndSource source ? source.Handle : IntPtr.Zero);
    }

    private AutomationElement? TryFind(Func<IntPtr> handle, string automationId)
    {
        var root = handle();
        return root == IntPtr.Zero ? null : _automation.FromHandle(root).FindFirstDescendant(cf => cf.ByAutomationId(automationId));
    }
}
