using DevTools.UI.Controls;
using FlaUI.Core.Tools;
using FlaUI.UIA3;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using AutomationElement = FlaUI.Core.AutomationElements.AutomationElement;
using HandyComboBox = HandyControl.Controls.ComboBox;

namespace DevTools.HandyControl.Tests;

[TestClass]
[DoNotParallelize]
public sealed class ColorPickerTests
{
    [TestMethod]
    public void ColorPickerDictionaryLoads()
    {
        WpfSta.Invoke(() =>
        {
            var dictionary = new ResourceDictionary
            {
                Source = new Uri("/DevTools.UI;component/Controls/ColorPicker/ColorPicker.xaml", UriKind.Relative),
            };

            Assert.IsTrue(dictionary.Count > 0);
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
        using var ui = PickerUi.Show();
        WpfSta.Invoke(() =>
        {
            var picker = ui.Picker;
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
        });

        var toggle = ui.FindInWindow("ColorPickerToggle");
        Assert.IsFalse(toggle.IsOffscreen, "Drop-down toggle is offscreen.");
        var bounds = toggle.BoundingRectangle;
        Assert.IsTrue(bounds.Width > 0 && bounds.Height > 0, "Drop-down toggle has no bounds. " + bounds);
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
    public void HeaderToggleOpensAndCloses()
    {
        using var ui = PickerUi.Show();
        var toggle = ui.FindInWindow("ColorPickerToggle").Patterns.Toggle.Pattern;

        toggle.Toggle();
        Assert.IsTrue(ui.WaitFor(picker => picker.IsDropDownOpen && PopupOf(picker).IsOpen), "Header toggle did not open the picker.");
        Assert.IsNotNull(ui.FindInPopup("ColorPickerStandardPalette"));

        toggle.Toggle();
        Assert.IsTrue(ui.WaitFor(picker => !picker.IsDropDownOpen && !PopupOf(picker).IsOpen), "Header toggle did not close the picker.");
    }

    [TestMethod]
    public void SaturationEditKeepsPopupOpen()
    {
        using var ui = PickerUi.Show();
        var start = Color.FromRgb(55, 179, 179);
        WpfSta.Invoke(() => ui.Picker.SelectedColor = start);
        ui.OpenAdvanced();

        var saturation = ui.FindInPopup("ColorPickerSaturationSlider").Patterns.RangeValue.Pattern;
        saturation.SetValue(saturation.Value.Value > 0.5 ? 0.15 : 0.85);

        Assert.IsTrue(ui.WaitFor(picker => picker.SelectedColor != start), "Saturation edit did not change the color.");
        Assert.IsTrue(ui.Read(picker => picker.IsDropDownOpen && PopupOf(picker).IsOpen), "Popup closed while the color was changing.");
    }

    [TestMethod]
    public void EyeDropperKeepsPopupOpen()
    {
        using var ui = PickerUi.Show();
        var errors = WpfSta.Invoke(WatchDispatcher);
        try
        {
            ui.OpenAdvanced();

            ui.FindInPopup("ColorPickerEyeDropper").Patterns.Invoke.Pattern.Invoke();
            WpfSta.Invoke(Drain);

            Assert.IsTrue(ui.Read(picker => picker.IsDropDownOpen && PopupOf(picker).IsOpen), "Popup closed when the eye dropper picked.");
            Assert.IsNull(errors.Exception, errors.Exception?.ToString());
        }
        finally
        {
            WpfSta.Invoke(errors.Dispose);
        }
    }

    [TestMethod]
    public void EyeDropperWithoutPresentationSourceDoesNotThrow()
    {
        WpfSta.Invoke(() =>
        {
            using var errors = WatchDispatcher();
            var dropper = new ColorEyeDropper();
            var peer = UIElementAutomationPeer.CreatePeerForElement(dropper);
            var invoke = (IInvokeProvider)peer.GetPattern(PatternInterface.Invoke);
            invoke.Invoke();
            Drain();
            Assert.IsNull(dropper.SelectedColor);
            Assert.IsNull(errors.Exception, errors.Exception?.ToString());
        });
    }

    private static void Drain()
    {
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }

    private static Popup PopupOf(ColorPicker picker)
    {
        var popup = (Popup?)picker.Template.FindName("PART_Popup", picker);
        Assert.IsNotNull(popup);
        return popup;
    }

    private sealed class PickerUi : IDisposable
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
        private readonly UIA3Automation _automation = new();
        private readonly ThemeWindow _window;

        private PickerUi(ThemeWindow window, ColorPicker picker)
        {
            _window = window;
            Picker = picker;
        }

        public ColorPicker Picker { get; }

        public static PickerUi Show()
        {
            return WpfSta.Invoke(() =>
            {
                var window = ColorPickerTests.Show(out var picker);
                window.Activate();
                return new PickerUi(window, picker);
            });
        }

        public T Read<T>(Func<ColorPicker, T> read)
        {
            return WpfSta.Invoke(() => read(Picker));
        }

        public bool WaitFor(Func<ColorPicker, bool> condition)
        {
            return Retry.WhileFalse(() => Read(condition), Timeout).Result;
        }

        public AutomationElement FindInWindow(string automationId)
        {
            return Find(() => WpfSta.Invoke(() => new WindowInteropHelper(_window).Handle), automationId);
        }

        public AutomationElement FindInPopup(string automationId)
        {
            return Find(() => Read(PopupHandle), automationId);
        }

        public void OpenAdvanced()
        {
            FindInWindow("ColorPickerToggle").Patterns.Toggle.Pattern.Toggle();
            Assert.IsTrue(WaitFor(picker => picker.IsDropDownOpen), "Header toggle did not open the picker.");
            FindInPopup("ColorPickerAdvancedTab").Patterns.SelectionItem.Pattern.Select();
            Assert.IsTrue(WaitFor(picker => ((TabItem)picker.Template.FindName("PART_AdvancedTab", picker)).IsSelected), "Advanced tab was not selected.");
        }

        public void Dispose()
        {
            _automation.Dispose();
            WpfSta.Invoke(_window.Close);
        }

        private static IntPtr PopupHandle(ColorPicker picker)
        {
            var child = PopupOf(picker).Child;
            return child is not null && PresentationSource.FromVisual(child) is HwndSource source ? source.Handle : IntPtr.Zero;
        }

        private AutomationElement Find(Func<IntPtr> handle, string automationId)
        {
            var element = Retry.WhileNull(
                () =>
                {
                    var root = handle();
                    return root == IntPtr.Zero ? null : _automation.FromHandle(root).FindFirstDescendant(cf => cf.ByAutomationId(automationId));
                },
                Timeout,
                ignoreException: true).Result;
            Assert.IsNotNull(element, automationId + " was not found. open=" + Read(picker => picker.IsDropDownOpen));
            return element;
        }
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
}
