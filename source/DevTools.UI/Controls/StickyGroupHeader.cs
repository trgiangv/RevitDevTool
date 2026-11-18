using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace DevTools.UI.Controls;

public sealed class StickyGroupHeader : ContentControl
{
    public static readonly DependencyProperty IsCollapsedProperty = DependencyProperty.Register(
        nameof(IsCollapsed), typeof(bool), typeof(StickyGroupHeader),
        new PropertyMetadata(false, (d, _) => ((StickyGroupHeader)d).UpdateExpanded()));

    public static readonly DependencyProperty ShowAllProperty = DependencyProperty.RegisterAttached(
        "ShowAll", typeof(bool), typeof(StickyGroupHeader),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits,
            (d, _) => (d as StickyGroupHeader)?.UpdateExpanded()));

    private static readonly DependencyPropertyKey IsExpandedKey = DependencyProperty.RegisterReadOnly(
        nameof(IsExpanded), typeof(bool), typeof(StickyGroupHeader),
        new PropertyMetadata(true, (d, e) => ((StickyGroupHeader)d).OnExpandedChanged((bool)e.OldValue, (bool)e.NewValue)));

    public static readonly DependencyProperty IsExpandedProperty = IsExpandedKey.DependencyProperty;

    // Kept on the ItemsControl so recycled group containers keep the user's choice.
    private static readonly DependencyProperty CollapsedGroupsProperty = DependencyProperty.RegisterAttached(
        "CollapsedGroups", typeof(HashSet<string>), typeof(StickyGroupHeader));

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
        Cursor = Cursors.Hand;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public double Offset => _offset.Y;

    public bool IsCollapsed
    {
        get => (bool)GetValue(IsCollapsedProperty);
        set => SetValue(IsCollapsedProperty, value);
    }

    public bool IsExpanded => (bool)GetValue(IsExpandedProperty);

    public static bool GetShowAll(DependencyObject element) => (bool)element.GetValue(ShowAllProperty);

    public static void SetShowAll(DependencyObject element, bool value) => element.SetValue(ShowAllProperty, value);

    public void Toggle()
    {
        SetCollapsed(!IsCollapsed);
    }

    public void SetCollapsed(bool collapsed)
    {
        IsCollapsed = collapsed;
        if (GroupName is not { } name || Owner is not { } owner)
        {
            return;
        }

        var groups = (HashSet<string>?)owner.GetValue(CollapsedGroupsProperty);
        if (groups is null)
        {
            groups = new HashSet<string>(StringComparer.Ordinal);
            owner.SetValue(CollapsedGroupsProperty, groups);
        }

        if (collapsed)
        {
            groups.Add(name);
        }
        else
        {
            groups.Remove(name);
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        e.Handled = true;
        Toggle();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        e.Handled = true;
    }

    protected override void OnContentChanged(object oldContent, object newContent)
    {
        base.OnContentChanged(oldContent, newContent);
        Restore();
    }

    private string? GroupName => (Content as CollectionViewGroup)?.Name?.ToString();

    private ItemsControl? Owner => _group is null ? null : ItemsControl.ItemsControlFromItemContainer(_group);

    private void Restore()
    {
        var groups = Owner?.GetValue(CollapsedGroupsProperty) as HashSet<string>;
        IsCollapsed = GroupName is { } name && groups?.Contains(name) == true;
    }

    private void UpdateExpanded()
    {
        SetValue(IsExpandedKey, !IsCollapsed || GetShowAll(this));
    }

    private void OnExpandedChanged(bool before, bool after)
    {
        (UIElementAutomationPeer.FromElement(this) as StickyGroupHeaderAutomationPeer)?.RaiseExpanded(before, after);
    }

    public string HeaderText => string.Join(" · ", TextsOf(this));

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
        Restore();
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

public sealed class StickyGroupHeaderAutomationPeer(StickyGroupHeader owner)
    : FrameworkElementAutomationPeer(owner), IExpandCollapseProvider
{
    public ExpandCollapseState ExpandCollapseState =>
        ((StickyGroupHeader)Owner).IsExpanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

    public void Expand()
    {
        ((StickyGroupHeader)Owner).SetCollapsed(false);
    }

    public void Collapse()
    {
        ((StickyGroupHeader)Owner).SetCollapsed(true);
    }

    public override object? GetPattern(PatternInterface patternInterface)
    {
        return patternInterface == PatternInterface.ExpandCollapse ? this : base.GetPattern(patternInterface);
    }

    internal void RaiseExpanded(bool before, bool after)
    {
        RaisePropertyChangedEvent(ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
            before ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed,
            after ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed);
    }

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
