using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;

namespace DevTools.UI.Controls;

public sealed class StickyGroupHeader : ContentControl
{
    private readonly TranslateTransform _offset = new();
    private GroupItem? _group;
    private ScrollViewer? _scroller;

    static StickyGroupHeader()
    {
        FocusableProperty.OverrideMetadata(typeof(StickyGroupHeader), new FrameworkPropertyMetadata(false));
    }

    public StickyGroupHeader()
    {
        RenderTransform = _offset;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public double Offset => _offset.Y;

    public string HeaderText => string.Concat(TextsOf(this));

    public void Update()
    {
        if (_group is null || _scroller is null || !_group.IsVisible)
        {
            _offset.Y = 0;
            return;
        }

        var top = _group.TranslatePoint(new Point(), _scroller).Y;
        var room = Math.Max(0, _group.ActualHeight - ActualHeight);
        _offset.Y = Math.Min(Math.Max(0, -top), room);
    }

    protected override AutomationPeer OnCreateAutomationPeer()
    {
        return new StickyGroupHeaderAutomationPeer(this);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _group = Ancestor<GroupItem>(this);
        _scroller = Ancestor<ScrollViewer>(this);
        if (_scroller is not null)
        {
            _scroller.ScrollChanged += OnScrollChanged;
        }

        Update();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_scroller is not null)
        {
            _scroller.ScrollChanged -= OnScrollChanged;
        }

        _scroller = null;
        _group = null;
    }

    private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        Update();
    }

    private static T? Ancestor<T>(DependencyObject start) where T : DependencyObject
    {
        var current = VisualTreeHelper.GetParent(start);
        while (current is not null and not T)
        {
            current = VisualTreeHelper.GetParent(current);
        }

        return current as T;
    }

    private static IEnumerable<string> TextsOf(DependencyObject root)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is TextBlock { Text.Length: > 0 } text)
            {
                yield return text.Text;
            }

            foreach (var nested in TextsOf(child))
            {
                yield return nested;
            }
        }
    }
}

public sealed class StickyGroupHeaderAutomationPeer(StickyGroupHeader owner) : FrameworkElementAutomationPeer(owner)
{
    protected override AutomationControlType GetAutomationControlTypeCore()
    {
        return AutomationControlType.Header;
    }

    protected override string GetClassNameCore()
    {
        return nameof(StickyGroupHeader);
    }

    protected override string GetNameCore()
    {
        var name = System.Windows.Automation.AutomationProperties.GetName(Owner);
        return string.IsNullOrEmpty(name) ? ((StickyGroupHeader)Owner).HeaderText : name;
    }

    protected override List<AutomationPeer>? GetChildrenCore()
    {
        return null;
    }

    protected override bool IsKeyboardFocusableCore()
    {
        return false;
    }
}
