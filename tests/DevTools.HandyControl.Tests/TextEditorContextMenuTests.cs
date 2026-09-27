using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using DevTools.UI.Theme;

namespace DevTools.HandyControl.Tests;

[TestClass]
[DoNotParallelize]
public sealed class TextEditorContextMenuTests
{
    [TestMethod]
    public void EditorContextMenusUseThemeBrushes()
    {
        WpfSta.Invoke(() =>
        {
            ThemeManager.Current.ApplySettingsTheme(AppTheme.Dark);
            try
            {
                using var window = Show(out var textBox, out var richTextBox, out var passwordBox);
                AssertThemedMenu(window, OpenEditorMenu(textBox));
                AssertThemedMenu(window, OpenEditorMenu(richTextBox));
                AssertThemedMenu(window, OpenEditorMenu(passwordBox));
            }
            finally
            {
                ThemeManager.Current.ApplySettingsTheme(AppTheme.Light);
            }
        });
    }

    [TestMethod]
    public void ContextMenuWidthFollowsWidestItem()
    {
        WpfSta.Invoke(() =>
        {
            ThemeManager.Current.ApplySettingsTheme(AppTheme.Dark);
            try
            {
                using var window = ShowMenuHost();
                var wide = OpenMenu(window, "Execute", "Open Location", "-", "Remove Selected", "Clear All");
                var narrow = OpenMenu(window, "Id");

                Assert.IsTrue(wide.ActualWidth < 220, "Menu is still using the 240px minimum. width=" + wide.ActualWidth);
                Assert.IsTrue(narrow.ActualWidth < wide.ActualWidth, "A shorter menu did not shrink. narrow=" + narrow.ActualWidth + " wide=" + wide.ActualWidth);

                var itemWidths = wide.Items.OfType<MenuItem>().Select(item => item.ActualWidth).ToList();
                Assert.IsTrue(itemWidths.Max() - itemWidths.Min() < 1, "Items are not aligned to the widest row.");

                var separator = wide.Items.OfType<Separator>().Single();
                Assert.AreEqual(0, separator.Margin.Left, "Separator still reserves the hidden icon column.");
                Assert.IsTrue(Math.Abs(separator.ActualWidth - itemWidths.Max()) < 1, "Separator is narrower than the row. separator=" + separator.ActualWidth + " item=" + itemWidths.Max());

                wide.IsOpen = false;
                narrow.IsOpen = false;
            }
            finally
            {
                ThemeManager.Current.ApplySettingsTheme(AppTheme.Light);
            }
        });
    }

    private static ContextMenu OpenMenu(Window window, params string[] headers)
    {
        var menu = new ContextMenu { PlacementTarget = window };
        foreach (var header in headers)
        {
            menu.Items.Add(header == "-" ? new Separator() : new MenuItem { Header = header });
        }

        menu.IsOpen = true;
        window.Dispatcher.Invoke(DispatcherPriority.ContextIdle, new Action(() => { }));
        menu.UpdateLayout();
        return menu;
    }

    private static ThemeWindow ShowMenuHost()
    {
        var window = new ThemeWindow();
        window.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("/DevTools.UI;component/Theme/Theme.xaml", UriKind.Relative),
        });
        window.Show();
        return window;
    }

    private static void AssertThemedMenu(Window window, ContextMenu menu)
    {
        Assert.AreEqual("EditorContextMenu", menu.GetType().Name);

        var region = BrushColor((Brush)window.FindResource("RegionBrush"));
        var background = BrushColor(menu.Background);
        Assert.AreEqual(region, background, "Context menu is still using the classic theme background.");
        Assert.AreNotEqual(Colors.White, background);

        var commands = menu.Items.OfType<MenuItem>().Select(item => item.Command).ToList();
        CollectionAssert.Contains(commands, ApplicationCommands.Cut);
        CollectionAssert.Contains(commands, ApplicationCommands.Copy);
        CollectionAssert.Contains(commands, ApplicationCommands.Paste);

        var item = menu.Items.OfType<MenuItem>().First();
        item.ApplyTemplate();
        menu.UpdateLayout();
        Assert.IsNotNull(item.Template.FindName("InputGestureText", item), "Menu item is not using the editor menu template.");
        Assert.IsTrue(item.ActualHeight is > 0 and <= 28, "Menu item is taller than the system menu. height=" + item.ActualHeight);
        Assert.IsTrue(menu.ActualWidth is > 40 and < 220, "Menu is wider than the system menu. width=" + menu.ActualWidth);

        menu.IsOpen = false;
    }

    private static ContextMenu OpenEditorMenu(FrameworkElement editor)
    {
        editor.Focus();
        var args = CreateOpeningArgs(editor);
        args.RoutedEvent = FrameworkElement.ContextMenuOpeningEvent;
        editor.RaiseEvent(args);
        editor.Dispatcher.Invoke(DispatcherPriority.ContextIdle, new Action(() => { }));

        var menu = FindOpenMenu();
        Assert.IsNotNull(menu, editor.GetType().Name + " did not open the editor context menu.");
        return menu;
    }

    private static ContextMenuEventArgs CreateOpeningArgs(FrameworkElement editor)
    {
        var ctor = typeof(ContextMenuEventArgs).GetConstructor(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            new[] { typeof(object), typeof(bool), typeof(double), typeof(double) },
            null);
        Assert.IsNotNull(ctor);
        return (ContextMenuEventArgs)ctor.Invoke(new object[] { editor, true, -1d, -1d });
    }

    private static ContextMenu? FindOpenMenu()
    {
        foreach (PresentationSource source in PresentationSource.CurrentSources)
        {
            if (source.RootVisual is ContextMenu root && root.IsOpen)
                return root;

            if (source.RootVisual is DependencyObject visual)
            {
                var menu = FindVisualChild<ContextMenu>(visual);
                if (menu is { IsOpen: true })
                    return menu;
            }
        }

        return null;
    }

    private static Color BrushColor(Brush? brush)
    {
        Assert.IsInstanceOfType<SolidColorBrush>(brush);
        return ((SolidColorBrush)brush).Color;
    }

    private static ThemeWindow Show(out TextBox textBox, out RichTextBox richTextBox, out PasswordBox passwordBox)
    {
        textBox = new TextBox { Width = 200, Text = "This is the content" };
        richTextBox = new RichTextBox { Width = 200, Height = 40 };
        passwordBox = new PasswordBox { Width = 200 };
        var panel = new StackPanel();
        panel.Children.Add(textBox);
        panel.Children.Add(richTextBox);
        panel.Children.Add(passwordBox);

        var window = new ThemeWindow
        {
            Content = panel,
        };
        window.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("/DevTools.UI;component/Theme/Theme.xaml", UriKind.Relative),
        });
        window.Show();
        panel.UpdateLayout();
        return window;
    }

    private static T? FindVisualChild<T>(DependencyObject parent)
        where T : DependencyObject
    {
        if (parent is T match)
            return match;

        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var index = 0; index < count; index++)
        {
            var nested = FindVisualChild<T>(VisualTreeHelper.GetChild(parent, index));
            if (nested is not null)
                return nested;
        }

        return null;
    }

    private sealed class ThemeWindow : Window, IDisposable
    {
        public ThemeWindow()
        {
            Width = 320;
            Height = 200;
            WindowStyle = WindowStyle.None;
            ShowInTaskbar = false;
            ShowActivated = false;
        }

        public void Dispose() => Close();
    }
}
