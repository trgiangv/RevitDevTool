using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using WpfDragDrop = System.Windows.DragDrop;

namespace DevTools.UI.Behaviors.DragDrop;

public static class DragReorder
{
    public const double DimmedOpacity = 0.35;

    private const string Format = "DevTools.UI.DragInfo";
    private const double ScrollMargin = 24;
    private const double ScrollStep = 12;

    public static readonly DependencyProperty IsDragSourceProperty = DependencyProperty.RegisterAttached(
        "IsDragSource",
        typeof(bool),
        typeof(DragReorder),
        new PropertyMetadata(false, OnIsDragSourceChanged));

    public static readonly DependencyProperty DropTargetProperty = DependencyProperty.RegisterAttached(
        "DropTarget",
        typeof(IDropTarget),
        typeof(DragReorder),
        new PropertyMetadata(null, OnDropTargetChanged));

    public static readonly DependencyProperty DropListProperty = DependencyProperty.RegisterAttached(
        "DropList",
        typeof(ItemsControl),
        typeof(DragReorder),
        new PropertyMetadata(null));

    private static readonly DependencyProperty PendingProperty = DependencyProperty.RegisterAttached(
        "Pending",
        typeof(DragInfo),
        typeof(DragReorder));

    private static InsertionAdorner? _line;
    private static double _cardOpacity = 1;
    private static AdornerLayer? _previewLayer;

    public static DragAdorner? Preview { get; private set; }

    public static bool GetIsDragSource(DependencyObject element)
    {
        return (bool)element.GetValue(IsDragSourceProperty);
    }

    public static void SetIsDragSource(DependencyObject element, bool value)
    {
        element.SetValue(IsDragSourceProperty, value);
    }

    public static IDropTarget? GetDropTarget(DependencyObject element)
    {
        return (IDropTarget?)element.GetValue(DropTargetProperty);
    }

    public static void SetDropTarget(DependencyObject element, IDropTarget? value)
    {
        element.SetValue(DropTargetProperty, value);
    }

    public static ItemsControl? GetDropList(DependencyObject element)
    {
        return (ItemsControl?)element.GetValue(DropListProperty);
    }

    public static void SetDropList(DependencyObject element, ItemsControl? value)
    {
        element.SetValue(DropListProperty, value);
    }

    public static DropInfo? DropAtPoint(UIElement host, DragInfo drag, Point point)
    {
        if (host.InputHitTest(point) is not DependencyObject hit || TargetList(host, hit, out var placeholder) is not { } list
            || GetDropTarget(list) is not { } target)
        {
            return null;
        }

        var index = placeholder is null ? NearestIndex(list, host.TranslatePoint(point, list).Y) : -1;
        var container = index < 0 ? null : list.ItemContainerGenerator.ContainerFromIndex(index) as FrameworkElement;
        DropInfo info;
        if (container is not null && index >= 0)
        {
            var item = list.ItemContainerGenerator.ItemFromContainer(container);
            var offset = host.TranslatePoint(point, container).Y;
            var previous = ReferenceEquals(drag.LastItem, item) ? drag.LastPosition : null;
            var position = DropZones.Position(offset, container.ActualHeight, previous);
            var after = position == InsertPosition.After;
            info = new DropInfo(drag.Data, list.DataContext, list.ItemsSource, item, after ? index + 1 : index, position)
            {
                LineHost = list,
                LineY = container.TranslatePoint(new Point(0, after ? container.ActualHeight : 0), list).Y
            };
            drag.LastItem = item;
            drag.LastPosition = position;
        }
        else
        {
            var last = LastContainer(list);
            info = new DropInfo(drag.Data, list.DataContext, list.ItemsSource, null, list.Items.Count, InsertPosition.After)
            {
                LineHost = placeholder ?? list,
                LineY = placeholder is not null
                    ? placeholder.ActualHeight / 2
                    : last?.TranslatePoint(new Point(0, last.ActualHeight), list).Y ?? 0
            };
            drag.LastItem = null;
            drag.LastPosition = null;
        }

        info.Target = target;
        target.DragOver(info);
        return info;
    }

    public static DragAdorner? StartPreview(FrameworkElement card, Point grab)
    {
        EndPreview();
        if (TopLayer(card) is not { } layer || VisualTreeHelper.GetParent(layer) is not UIElement root)
        {
            return null;
        }

        Preview = new DragAdorner(root, card, grab);
        _previewLayer = layer;
        layer.Add(Preview);
        _cardOpacity = card.Opacity;
        card.Opacity = DimmedOpacity;
        return Preview;
    }

    public static void EndPreview()
    {
        if (Preview is null)
        {
            return;
        }

        Preview.Card.Opacity = _cardOpacity;
        _previewLayer?.Remove(Preview);
        _previewLayer = null;
        Preview = null;
    }

    public static void ShowLine(DropInfo info)
    {
        if (info.LineHost is not { } host || info.Effects == DragDropEffects.None)
        {
            HideLine();
            return;
        }

        if (_line?.AdornedElement != host)
        {
            HideLine();
            if (AdornerLayer.GetAdornerLayer(host) is not { } layer)
            {
                return;
            }

            var brush = (host as FrameworkElement)?.TryFindResource("PrimaryBrush") as Brush ?? SystemColors.HighlightBrush;
            _line = new InsertionAdorner(host, brush);
            layer.Add(_line);
        }

        _line.Y = info.LineY;
    }

    public static void HideLine()
    {
        if (_line is null)
        {
            return;
        }

        AdornerLayer.GetAdornerLayer(_line.AdornedElement)?.Remove(_line);
        _line = null;
    }

    public static bool IsLineShown => _line is not null;

    private static void OnIsDragSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element)
        {
            return;
        }

        element.PreviewMouseLeftButtonDown -= OnSourceDown;
        element.PreviewMouseMove -= OnSourceMove;
        element.PreviewMouseLeftButtonUp -= OnSourceUp;
        element.QueryContinueDrag -= OnQueryContinueDrag;
        if (e.NewValue is true)
        {
            element.PreviewMouseLeftButtonDown += OnSourceDown;
            element.PreviewMouseMove += OnSourceMove;
            element.PreviewMouseLeftButtonUp += OnSourceUp;
            element.QueryContinueDrag += OnQueryContinueDrag;
        }
    }

    private static void OnDropTargetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ItemsControl list)
        {
            return;
        }

        list.PreviewDragEnter -= OnDragOver;
        list.PreviewDragOver -= OnDragOver;
        list.DragLeave -= OnDragLeave;
        list.PreviewDrop -= OnDrop;
        list.AllowDrop = e.NewValue is not null;
        if (e.NewValue is not null)
        {
            list.PreviewDragEnter += OnDragOver;
            list.PreviewDragOver += OnDragOver;
            list.DragLeave += OnDragLeave;
            list.PreviewDrop += OnDrop;
        }
    }

    private static void OnSourceDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: { } data } element)
        {
            element.SetValue(PendingProperty, new DragInfo(data, element, e.GetPosition(element)));
        }
    }

    private static void OnSourceUp(object sender, MouseButtonEventArgs e)
    {
        ((UIElement)sender).ClearValue(PendingProperty);
    }

    private static void OnSourceMove(object sender, MouseEventArgs e)
    {
        var element = (UIElement)sender;
        if (element.GetValue(PendingProperty) is not DragInfo drag)
        {
            return;
        }

        if (e.LeftButton != MouseButtonState.Pressed)
        {
            element.ClearValue(PendingProperty);
            return;
        }

        if (!drag.IsFarEnough(e.GetPosition(element)))
        {
            return;
        }

        element.ClearValue(PendingProperty);
        if (element.IsMouseCaptured)
        {
            element.ReleaseMouseCapture();
        }

        if (element is FrameworkElement grip && CardOf(grip) is { } card)
        {
            StartPreview(card, element.TranslatePoint(drag.Start, card));
        }

        try
        {
            WpfDragDrop.DoDragDrop(element, new DataObject(Format, drag), DragDropEffects.Move);
        }
        finally
        {
            HideLine();
            EndPreview();
        }

        e.Handled = true;
    }

    private static void OnQueryContinueDrag(object sender, QueryContinueDragEventArgs e)
    {
        Preview?.FollowCursor();
    }

    private static void OnDragOver(object sender, DragEventArgs e)
    {
        var host = (UIElement)sender;
        e.Handled = true;
        var info = e.Data.GetData(Format) is DragInfo drag ? DropAtPoint(host, drag, e.GetPosition(host)) : null;
        e.Effects = info?.Effects ?? DragDropEffects.None;
        if (info is null)
        {
            HideLine();
        }
        else
        {
            ShowLine(info);
        }

        AutoScroll(host, e);
    }

    private static void OnDragLeave(object sender, DragEventArgs e)
    {
        var list = (ItemsControl)sender;
        var point = e.GetPosition(list);
        if (point.X < 0 || point.Y < 0 || point.X > list.ActualWidth || point.Y > list.ActualHeight)
        {
            HideLine();
        }
    }

    private static void OnDrop(object sender, DragEventArgs e)
    {
        var host = (UIElement)sender;
        e.Handled = true;
        HideLine();
        if (e.Data.GetData(Format) is DragInfo drag
            && DropAtPoint(host, drag, e.GetPosition(host)) is { Effects: not DragDropEffects.None, Target: { } target } info)
        {
            target.Drop(info);
        }
    }

    private static ItemsControl? TargetList(UIElement host, DependencyObject hit, out FrameworkElement? placeholder)
    {
        placeholder = null;
        for (DependencyObject? node = hit; node is not null; node = Parent(node))
        {
            if (GetDropList(node) is { } empty)
            {
                placeholder = node as FrameworkElement;
                return empty;
            }

            if (node is ItemsControl list && GetDropTarget(list) is not null)
            {
                return list;
            }

            if (ReferenceEquals(node, host))
            {
                break;
            }
        }

        return host as ItemsControl;
    }

    public static FrameworkElement? CardOf(FrameworkElement grip)
    {
        DependencyObject previous = grip;
        for (var node = Parent(grip); node is not null; node = Parent(node))
        {
            if (node is ContentPresenter presenter && ReferenceEquals(presenter.Content, grip.DataContext))
            {
                return previous as FrameworkElement;
            }

            if (node is ItemsControl list && GetDropTarget(list) is not null)
            {
                return list.ContainerFromElement(grip) as FrameworkElement;
            }

            previous = node;
        }

        return null;
    }

    private static AdornerLayer? TopLayer(Visual element)
    {
        AdornerLayer? top = null;
        for (var node = element as DependencyObject; node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (node is Visual visual && AdornerLayer.GetAdornerLayer(visual) is { } layer)
            {
                top = layer;
            }
        }

        return top;
    }

    private static DependencyObject? Parent(DependencyObject node)
    {
        return node is Visual or System.Windows.Media.Media3D.Visual3D
            ? VisualTreeHelper.GetParent(node)
            : LogicalTreeHelper.GetParent(node);
    }

    private static int NearestIndex(ItemsControl list, double y)
    {
        var nearest = -1;
        var distance = double.MaxValue;
        for (var index = 0; index < list.Items.Count; index++)
        {
            if (list.ItemContainerGenerator.ContainerFromIndex(index) is not FrameworkElement container)
            {
                continue;
            }

            var top = container.TranslatePoint(new Point(), list).Y;
            var bottom = top + container.ActualHeight;
            if (y >= top && y < bottom)
            {
                return index;
            }

            var gap = y < top ? top - y : y - bottom;
            if (gap < distance)
            {
                distance = gap;
                nearest = index;
            }
        }

        return nearest;
    }

    private static FrameworkElement? LastContainer(ItemsControl list)
    {
        return list.Items.Count == 0
            ? null
            : list.ItemContainerGenerator.ContainerFromIndex(list.Items.Count - 1) as FrameworkElement;
    }

    private static void AutoScroll(DependencyObject list, DragEventArgs e)
    {
        var viewer = Ancestor<ScrollViewer>(list);
        if (viewer is null)
        {
            return;
        }

        var y = e.GetPosition(viewer).Y;
        if (y < ScrollMargin)
        {
            viewer.ScrollToVerticalOffset(viewer.VerticalOffset - ScrollStep);
        }
        else if (y > viewer.ActualHeight - ScrollMargin)
        {
            viewer.ScrollToVerticalOffset(viewer.VerticalOffset + ScrollStep);
        }
    }

    private static T? Ancestor<T>(DependencyObject node)
        where T : DependencyObject
    {
        for (var current = VisualTreeHelper.GetParent(node); current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is T match)
            {
                return match;
            }
        }

        return null;
    }
}
