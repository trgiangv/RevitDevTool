using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using HandyControl.Tools;
// ReSharper disable once CheckNamespace
namespace DevTools.UI.Controls;

/// <summary>
/// Screen color sampler. Adapted from MahApps.Metro. Uses the HandyControl dropper cursor.
/// </summary>
public class ColorEyeDropper : Button
{
    private DispatcherOperation? _currentTask;
    private ToolTip? _previewToolTip;
    private Cursor? _dropperCursor;

    internal readonly ColorEyePreviewData PreviewData = new();

    public static readonly DependencyProperty SelectedColorProperty =
        DependencyProperty.Register(
            nameof(SelectedColor),
            typeof(Color?),
            typeof(ColorEyeDropper),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedColorPropertyChanged));

    public static readonly DependencyProperty PreviewImageOuterPixelCountProperty =
        DependencyProperty.Register(nameof(PreviewImageOuterPixelCount), typeof(int), typeof(ColorEyeDropper), new PropertyMetadata(2));

    public static readonly DependencyProperty EyeDropperCursorProperty =
        DependencyProperty.Register(nameof(EyeDropperCursor), typeof(Cursor), typeof(ColorEyeDropper), new PropertyMetadata(null));

    public static readonly DependencyProperty PreviewContentTemplateProperty =
        DependencyProperty.Register(nameof(PreviewContentTemplate), typeof(DataTemplate), typeof(ColorEyeDropper), new PropertyMetadata(null));

    public static readonly RoutedEvent SelectedColorChangedEvent =
        EventManager.RegisterRoutedEvent(
            nameof(SelectedColorChanged),
            RoutingStrategy.Bubble,
            typeof(RoutedPropertyChangedEventHandler<Color?>),
            typeof(ColorEyeDropper));

    static ColorEyeDropper()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ColorEyeDropper), new FrameworkPropertyMetadata(typeof(ColorEyeDropper)));
    }

    public Color? SelectedColor
    {
        get => (Color?)GetValue(SelectedColorProperty);
        set => SetValue(SelectedColorProperty, value);
    }

    public int PreviewImageOuterPixelCount
    {
        get => (int)GetValue(PreviewImageOuterPixelCountProperty);
        set => SetValue(PreviewImageOuterPixelCountProperty, value);
    }

    public Cursor? EyeDropperCursor
    {
        get => (Cursor?)GetValue(EyeDropperCursorProperty);
        set => SetValue(EyeDropperCursorProperty, value);
    }

    public DataTemplate? PreviewContentTemplate
    {
        get => (DataTemplate?)GetValue(PreviewContentTemplateProperty);
        set => SetValue(PreviewContentTemplateProperty, value);
    }

    public event RoutedPropertyChangedEventHandler<Color?> SelectedColorChanged
    {
        add => AddHandler(SelectedColorChangedEvent, value);
        remove => RemoveHandler(SelectedColorChangedEvent, value);
    }

    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonDown(e);
        Mouse.Capture(this);
        _previewToolTip ??= ColorEyePreview.GetPreviewToolTip(this);
        ColorEyePreview.Show(_previewToolTip);
        Cursor = EyeDropperCursor ?? (_dropperCursor ??= LoadDropperCursor());
        SetPreview(e.GetPosition(this));
    }

    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonUp(e);
        Mouse.Capture(null);
        if (_previewToolTip is not null)
        {
            ColorEyePreview.Hide(_previewToolTip);
        }

        Cursor = Cursors.Arrow;
        if (TryGetScreenPoint(e.GetPosition(this), out var screen))
        {
            SetCurrentValue(SelectedColorProperty, EyeDropperHelper.GetPixelColor(screen));
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            SetPreview(e.GetPosition(this));
        }
    }

    private void SetPreview(Point mousePos)
    {
        if (_previewToolTip is not null)
        {
            ColorEyePreview.Move(_previewToolTip, mousePos, new Point(16, 16));
        }

        if (_currentTask is { Status: DispatcherOperationStatus.Executing or DispatcherOperationStatus.Pending })
        {
            _currentTask.Abort();
        }

        // Resolve the screen point while this visual is still connected. The capture runs later,
        // and PointToScreen throws once the popup has torn the element off its PresentationSource.
        if (!TryGetScreenPoint(mousePos, out var screen))
        {
            return;
        }

        _currentTask = Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            var outerPixelCount = PreviewImageOuterPixelCount;
            var region = new Int32Rect(
                (int)Math.Round(screen.X - outerPixelCount),
                (int)Math.Round(screen.Y - outerPixelCount),
                (2 * outerPixelCount) + 1,
                (2 * outerPixelCount) + 1);
            var previewImage = EyeDropperHelper.CaptureRegion(region);
            var previewBrush = new SolidColorBrush(EyeDropperHelper.GetPixelColor(screen));
            previewBrush.Freeze();
            PreviewData.SetValue(ColorEyePreviewData.PreviewImagePropertyKey, previewImage);
            PreviewData.SetValue(ColorEyePreviewData.PreviewBrushPropertyKey, previewBrush);
        }));
    }

    private bool TryGetScreenPoint(Point relative, out Point screen)
    {
        if (PresentationSource.FromVisual(this) is null)
        {
            screen = default;
            return false;
        }

        screen = PointToScreen(relative);
        return true;
    }

    private static Cursor LoadDropperCursor()
    {
        var info = Application.GetResourceStream(ResourceHelper.GetComponentUri("Resources/dropper.cur"));
        return info is null ? Cursors.Cross : new Cursor(info.Stream);
    }

    private static void OnSelectedColorPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is ColorEyeDropper eyeDropper)
        {
            eyeDropper.RaiseEvent(new RoutedPropertyChangedEventArgs<Color?>((Color?)e.OldValue, (Color?)e.NewValue, SelectedColorChangedEvent));
        }
    }
}
