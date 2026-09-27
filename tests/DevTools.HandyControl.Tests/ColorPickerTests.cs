using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using DevTools.UI.Controls;
using HandyComboBox = HandyControl.Controls.ComboBox;

namespace DevTools.HandyControl.Tests;

[TestClass]
[DoNotParallelize]
public sealed class ColorPickerTests
{
    [TestMethod]
    public void GenericDictionaryLoads()
    {
        WpfSta.Invoke(() =>
        {
            var dictionary = new ResourceDictionary
            {
                Source = new Uri("/DevTools.UI;component/Themes/Generic.xaml", UriKind.Relative),
            };

            Assert.IsTrue(dictionary.MergedDictionaries.Count > 0);
        });
    }

    [TestMethod]
    public void ThemeDictionaryLoads()
    {
        WpfSta.Invoke(() =>
        {
            var theme = LoadTheme();
            Assert.IsTrue(theme.MergedDictionaries.Count > 0);
        });
    }

    [TestMethod]
    public void ClosedPickerShowsSelectedColor()
    {
        WpfSta.Invoke(() =>
        {
            using var window = Show(out var picker);
            picker.SelectedColor = Colors.DodgerBlue;
            picker.UpdateLayout();

            Assert.AreEqual(Colors.DodgerBlue, picker.SelectedColor);
            Assert.AreEqual(Colors.DodgerBlue.R, picker.R);
            Assert.AreEqual(Colors.DodgerBlue.G, picker.G);
            Assert.AreEqual(Colors.DodgerBlue.B, picker.B);
            Assert.IsFalse(string.IsNullOrWhiteSpace(picker.ColorName));
            Assert.IsNotNull(picker.Template);
            Assert.IsNotNull(picker.Template.FindName("PART_Popup", picker));
            Assert.IsTrue(picker.DesiredSize.Width > 0);
            Assert.IsTrue(picker.DesiredSize.Height is > 0 and < 48, "Closed picker is taller than a single row. " + picker.DesiredSize);

            var toggle = FindVisualChild<ToggleButton>(picker);
            Assert.IsNotNull(toggle);
            Assert.IsTrue(toggle.ActualWidth > 0, "Drop-down toggle has no width. " + Describe(picker));
            Assert.IsTrue(toggle.ActualHeight > 0);
            var hit = VisualTreeHelper.HitTest(picker, new Point(picker.ActualWidth / 2, picker.ActualHeight / 2));
            Assert.IsNotNull(hit);
            Assert.IsTrue(IsDescendantOf(hit.VisualHit, toggle), "Click does not land on the drop-down toggle. " + Describe(picker));
        });
    }

    [TestMethod]
    public void DropDownOpensAndPaletteSelectionUpdatesColor()
    {
        WpfSta.Invoke(() =>
        {
            using var window = Show(out var picker);
            picker.IsDropDownOpen = true;
            picker.UpdateLayout();

            var popup = (Popup?)picker.Template.FindName("PART_Popup", picker);
            Assert.IsNotNull(popup);
            Assert.IsTrue(popup.IsOpen);

            var palette = (ColorPalette?)picker.Template.FindName("PART_ColorPaletteStandard", picker);
            Assert.IsNotNull(palette);
            palette.SelectedItem = Colors.Red;
            picker.UpdateLayout();

            Assert.AreEqual(Colors.Red, picker.SelectedColor);
            Assert.IsTrue(palette.ItemContainerGenerator.ContainerFromItem(Colors.Red) is ListBoxItem);

            var tabs = (TabControl?)picker.Template.FindName("PART_PopupTabControl", picker);
            var advanced = (TabItem?)picker.Template.FindName("PART_AdvancedTab", picker);
            Assert.IsNotNull(tabs);
            Assert.IsNotNull(advanced);
            tabs.SelectedItem = advanced;
            picker.UpdateLayout();
            Assert.IsNotNull(popup.Child);
            Assert.IsNotNull(FindChild<ColorCanvas>(popup.Child), "Advanced tab did not create a color canvas. " + Describe(popup.Child));
        });
    }

    [TestMethod]
    public void SelectedColorBindsTwoWayToColor()
    {
        WpfSta.Invoke(() =>
        {
            using var window = Show(out var picker);
            var source = new ColorSource { SurfaceColor = Colors.Orange };
            picker.SetBinding(
                ColorPickerBase.SelectedColorProperty,
                new Binding(nameof(ColorSource.SurfaceColor))
                {
                    Source = source,
                    Mode = BindingMode.TwoWay,
                });

            var expression = BindingOperations.GetBindingExpression(picker, ColorPickerBase.SelectedColorProperty);
            Assert.IsNotNull(expression);
            Assert.AreEqual(BindingStatus.Active, expression.Status, expression.ValidationError?.ErrorContent?.ToString());
            Assert.AreEqual(Colors.Orange, picker.SelectedColor);

            picker.SelectedColor = Colors.Purple;
            Assert.AreEqual(Colors.Purple, source.SurfaceColor);

            source.SurfaceColor = Colors.Teal;
            Assert.AreEqual(Colors.Teal, picker.SelectedColor);
        });
    }

    [TestMethod]
    public void ChannelEditUpdatesSelectedColor()
    {
        WpfSta.Invoke(() =>
        {
            using var window = Show(out var picker);
            picker.A = 255;
            picker.R = 10;
            picker.G = 20;
            picker.B = 30;

            Assert.AreEqual(Color.FromRgb(10, 20, 30), picker.SelectedColor);
        });
    }

    [TestMethod]
    public void HandyComboBoxStillReceivesThemeTemplate()
    {
        WpfSta.Invoke(() =>
        {
            var combo = new HandyComboBox { Width = 120 };
            using var window = ShowHost(combo);

            Assert.IsNotNull(combo.Template, "Handy ComboBox did not receive a template from the theme.");
            Assert.IsTrue(combo.ActualHeight > 0);
        });
    }

    [TestMethod]
    public void MouseDownOnHeaderOpensDropDownAndSecondClickClosesIt()
    {
        WpfSta.Invoke(() =>
        {
            using var window = Show(out var picker);
            window.Activate();
            var header = FindVisualChild<ToggleButton>(picker);
            Assert.IsNotNull(header);
            Assert.IsTrue(header.ActualWidth > 0);

            RaiseMouseDown(header);
            Drain();

            var popup = (Popup?)picker.Template.FindName("PART_Popup", picker);
            Assert.IsNotNull(popup);
            Assert.IsTrue(picker.IsDropDownOpen, "Mouse down on the header did not open the picker.");
            Assert.IsTrue(popup.IsOpen, "Popup closed again on the same click.");

            RaiseMouseDown(header);
            Drain();
            Assert.IsFalse(picker.IsDropDownOpen);
            Assert.IsFalse(popup.IsOpen);
        });
    }

    [TestMethod]
    public void DraggingAdvancedSurfaceKeepsPopupOpenAndUpdatesColor()
    {
        WpfSta.Invoke(() =>
        {
            using var window = Show(out var picker);
            window.Activate();
            var canvas = OpenAdvanced(picker);
            var box = (FrameworkElement?)canvas.Template.FindName("PART_SaturationValueBox", canvas);
            Assert.IsNotNull(box);
            Assert.IsTrue(box.ActualWidth > 1, "Saturation surface was not laid out.");

            picker.SelectedColor = Color.FromRgb(55, 179, 179);
            Drain();
            var before = picker.SelectedColor;
            Assert.AreEqual(Color.FromRgb(55, 179, 179), before);

            RaiseMouseDown(box);
            Drain();
            Assert.IsTrue(picker.IsDropDownOpen, "Mouse down on the advanced surface closed the popup.");

            box.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
            {
                RoutedEvent = UIElement.MouseLeftButtonDownEvent,
                Source = box,
            });
            Drain();

            Assert.IsTrue(picker.IsDropDownOpen, "Popup closed when the saturation surface captured the mouse.");
            Assert.AreEqual(box, Mouse.Captured, "Saturation surface did not keep mouse capture for the drag.");

            picker.Saturation = picker.Saturation > 0.5 ? 0.15 : 0.85;
            Drain();
            Assert.IsTrue(picker.IsDropDownOpen, "Popup closed while the color was still changing.");
            Assert.AreNotEqual(before, picker.SelectedColor);

            box.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
            {
                RoutedEvent = UIElement.MouseLeftButtonUpEvent,
                Source = box,
            });
            Drain();
            Assert.IsTrue(picker.IsDropDownOpen, "Popup closed when the drag ended.");
        });
    }

    [TestMethod]
    public void EyeDropperKeepsPopupOpenWithoutThrowing()
    {
        WpfSta.Invoke(() =>
        {
            using var errors = WatchDispatcher();
            using var window = Show(out var picker);
            window.Activate();
            var canvas = OpenAdvanced(picker);
            var dropper = (ColorEyeDropper?)canvas.Template.FindName("PART_ColorEyeDropper", canvas);
            Assert.IsNotNull(dropper);

            dropper.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
            {
                RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent,
                Source = dropper,
            });
            Drain();

            Assert.IsTrue(picker.IsDropDownOpen, "Popup closed when the eye dropper captured the mouse.");
            Assert.IsNull(errors.Exception, errors.Exception?.ToString());

            dropper.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
            {
                RoutedEvent = UIElement.PreviewMouseLeftButtonUpEvent,
                Source = dropper,
            });
            Drain();
            Assert.IsTrue(picker.IsDropDownOpen, "Popup closed when the eye dropper finished.");
            Assert.IsNull(errors.Exception, errors.Exception?.ToString());
        });
    }

    [TestMethod]
    public void EyeDropperWithoutPresentationSourceDoesNotThrow()
    {
        WpfSta.Invoke(() =>
        {
            using var errors = WatchDispatcher();
            var dropper = new ColorEyeDropper();
            dropper.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
            {
                RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent,
                Source = dropper,
            });
            Drain();
            dropper.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
            {
                RoutedEvent = UIElement.PreviewMouseLeftButtonUpEvent,
                Source = dropper,
            });
            Drain();
            Assert.IsNull(errors.Exception, errors.Exception?.ToString());
        });
    }

    private static void RaiseMouseDown(IInputElement source)
    {
        source.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
        {
            RoutedEvent = Mouse.MouseDownEvent,
            Source = source,
        });
    }

    private static void Drain()
    {
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }

    private static ColorCanvas OpenAdvanced(ColorPicker picker)
    {
        picker.IsDropDownOpen = true;
        Drain();
        var tabs = (TabControl?)picker.Template.FindName("PART_PopupTabControl", picker);
        var advanced = (TabItem?)picker.Template.FindName("PART_AdvancedTab", picker);
        Assert.IsNotNull(tabs);
        Assert.IsNotNull(advanced);
        tabs.SelectedItem = advanced;
        picker.UpdateLayout();
        Drain();

        var popup = (Popup?)picker.Template.FindName("PART_Popup", picker);
        Assert.IsNotNull(popup?.Child);
        var canvas = FindChild<ColorCanvas>(popup.Child);
        Assert.IsNotNull(canvas);
        canvas.ApplyTemplate();
        canvas.UpdateLayout();
        return canvas;
    }

    private static DispatcherError WatchDispatcher()
    {
        return new DispatcherError(Dispatcher.CurrentDispatcher);
    }

    private sealed class DispatcherError : IDisposable
    {
        private readonly Dispatcher _dispatcher;

        public DispatcherError(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher;
            _dispatcher.UnhandledException += OnUnhandledException;
        }

        public Exception? Exception { get; private set; }

        public void Dispose()
        {
            _dispatcher.UnhandledException -= OnUnhandledException;
        }

        private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Exception = e.Exception;
            e.Handled = true;
        }
    }

    private static ThemeWindow Show(out ColorPicker picker)
    {
        picker = new ColorPicker
        {
            Width = 200,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        return ShowHost(picker);
    }

    private static ThemeWindow ShowHost(FrameworkElement content)
    {
        var window = new ThemeWindow();
        window.Resources.MergedDictionaries.Add(LoadTheme());
        window.Content = content;
        window.Show();
        content.UpdateLayout();
        return window;
    }

    private static ResourceDictionary LoadTheme()
    {
        return new ResourceDictionary
        {
            Source = new Uri("/DevTools.UI;component/Theme/Theme.xaml", UriKind.Relative),
        };
    }

    private sealed class ThemeWindow : Window, IDisposable
    {
        public ThemeWindow()
        {
            Width = 640;
            Height = 480;
            WindowStyle = WindowStyle.None;
            ShowInTaskbar = false;
            ShowActivated = false;
        }

        public void Dispose()
        {
            Close();
        }
    }

    private sealed class ColorSource : INotifyPropertyChanged
    {
        private Color _surfaceColor;

        public Color SurfaceColor
        {
            get => _surfaceColor;
            set
            {
                if (_surfaceColor == value)
                {
                    return;
                }

                _surfaceColor = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SurfaceColor)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private static T? FindChild<T>(DependencyObject parent)
        where T : DependencyObject
    {
        var visual = FindVisualChild<T>(parent);
        if (visual is not null)
        {
            return visual;
        }

        foreach (var child in LogicalTreeHelper.GetChildren(parent))
        {
            if (child is not DependencyObject node)
            {
                continue;
            }

            if (node is T match)
            {
                return match;
            }

            var nested = FindChild<T>(node);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    private static string Describe(DependencyObject root)
    {
        var lines = new List<string>();
        Describe(root, 0, lines);
        return string.Join(" | ", lines);
    }

    private static void Describe(DependencyObject node, int depth, List<string> lines)
    {
        if (depth > 6 || lines.Count > 40)
        {
            return;
        }

        var detail = node switch
        {
            Border border => $" {border.ActualWidth:0}x{border.ActualHeight:0} bg={border.Background}",
            TextBlock text => $" '{text.Text}' fg={text.Foreground} vis={text.Visibility}",
            _ => string.Empty,
        };
        lines.Add(new string('>', depth) + node.GetType().Name + detail);
        var count = VisualTreeHelper.GetChildrenCount(node);
        for (var index = 0; index < count; index++)
        {
            Describe(VisualTreeHelper.GetChild(node, index), depth + 1, lines);
        }
    }

    private static T? FindVisualChild<T>(DependencyObject parent)
        where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var index = 0; index < count; index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match)
            {
                return match;
            }

            var nested = FindVisualChild<T>(child);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    private static bool IsDescendantOf(DependencyObject node, DependencyObject ancestor)
    {
        var current = node;
        while (current is not null)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current) ?? LogicalTreeHelper.GetParent(current);
        }

        return false;
    }
}
