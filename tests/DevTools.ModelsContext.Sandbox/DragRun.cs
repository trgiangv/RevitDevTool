using DevTools.ModelsContext.Filters;
using DevTools.ModelsContext.Tests.Shared;
using DevTools.Presentation.Views.ModelsContext.Filters;
using DevTools.Presentation.Views.ModelsContext.Panel;
using DevTools.UI.Behaviors.DragDrop;
using FlaUI.Core.Input;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using DrawingPoint = System.Drawing.Point;

namespace DevTools.ModelsContext.Sandbox;

/// <summary>
/// Drag checks of the rule tree. <c>--drag-shots</c> drives <see cref="DragReorder"/> in process (preview, line,
/// drop) and saves before, during and after images of the sandbox window only; it never touches the OS pointer.
/// <c>--drag</c> is an opt-in a person runs on their own desktop: it moves the real pointer with FlaUI
/// <see cref="Mouse"/>, because UI Automation has no drag pattern. It brings the sandbox to the front first and
/// stops with one sentence, before or during the drag, when another window is under the pointer or takes focus.
/// </summary>
internal static class DragRun
{
    public static async Task<int> ShotsAsync(SandboxWindow window, Application app, string directory)
    {
        try
        {
            var (filter, view, mark, last) = await PrepareAsync(window, directory);
            var host = Find<FilterItemsControl>(view, "Filter.Rules");
            var first = (FrameworkElement)host.ItemContainerGenerator.ContainerFromIndex(0);
            var grip = Grips(view)[^1];
            var card = DragReorder.CardOf(grip)!;
            var grab = grip.TranslatePoint(new Point(grip.ActualWidth / 2, grip.ActualHeight / 2), card);
            var drag = new DragInfo(last, grip, new Point());
            DragReorder.StartPreview(card, grab);
            var point = first.TranslatePoint(new Point(grip.TranslatePoint(new Point(), first).X + 4, first.ActualHeight * 0.2), host);
            var info = DragReorder.DropAtPoint(host, drag, point);
            if (info is not null)
            {
                DragReorder.ShowLine(info);
            }

            var preview = DragReorder.Preview!;
            preview.MoveTo(host.TranslatePoint(point, (UIElement)preview.AdornedElement));
            await Settle(400);
            ShotRun.Save(window, Path.Combine(directory, "during.png"));
            Console.WriteLine($"during: line {(DragReorder.IsLineShown ? "shown" : "hidden")} at index {info?.InsertIndex} {info?.Position}");
            DragReorder.HideLine();
            DragReorder.EndPreview();
            if (info is { Effects: not DragDropEffects.None, Target: { } target })
            {
                target.Drop(info);
            }

            return await FinishAsync(window, directory, filter, mark, last);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
        finally
        {
            app.Shutdown();
        }
    }

    public static async Task<int> MouseAsync(SandboxWindow window, Application app, string directory)
    {
        try
        {
            var (filter, view, mark, last) = await PrepareAsync(window, directory);
            var handle = new WindowInteropHelper(window).Handle;
            window.Topmost = true;
            SetForegroundWindow(handle);
            window.Activate();
            await Settle(500);
            var grips = Grips(view);
            var from = Screen(grips[^1], grips[^1].ActualWidth / 2, grips[^1].ActualHeight / 2);
            var to = Screen(grips[0], (grips[0].ActualWidth / 2) + 40, -20);
            if (GetForegroundWindow() != handle || !Owns(from) || !Owns(to))
            {
                Console.WriteLine("STOPPED: the sandbox is not the window under the pointer or in front, so nothing was clicked.");
                return 2;
            }

            var stopped = await Task.Run(() => Drag(from, to, handle));
            if (stopped is not null)
            {
                Console.WriteLine(stopped);
                return 2;
            }

            return await FinishAsync(window, directory, filter, mark, last);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
        finally
        {
            app.Shutdown();
        }
    }

    private static async Task<(FilterTabViewModel Filter, ModelContextView View, FilterNodeViewModel Mark, FilterNodeViewModel Last)>
        PrepareAsync(SandboxWindow window, string directory)
    {
        Directory.CreateDirectory(directory);
        await window.MeasureAsync(SandboxSize.Hundred, _ => { });
        var panel = window.CurrentRun!.Panel;
        panel.IsFilterTab = true;
        var filter = panel.Filter;
        filter.Library.AddCommand.Execute(null);
        filter.Library.CancelRename();
        filter.Root.Children.Clear();
        var mark = FilterExamples.Condition(filter.Root, FakeParameters.Mark, FilterOperator.Undefined);
        FilterExamples.Condition(filter.Root, FakeParameters.Level, FilterOperator.Equal, "Level 2");
        var last = FilterExamples.Condition(filter.Root, FakeParameters.FireRating, FilterOperator.Equal, "2h");
        await Settle(800);
        ShotRun.Save(window, Path.Combine(directory, "before.png"));
        Console.WriteLine($"order before: {Names(filter)}");
        return (filter, (ModelContextView)window.Host.Content, mark, last);
    }

    private static async Task<int> FinishAsync(SandboxWindow window, string directory, FilterTabViewModel filter,
        FilterNodeViewModel mark, FilterNodeViewModel last)
    {
        await Settle(800);
        ShotRun.Save(window, Path.Combine(directory, "after.png"));
        Console.WriteLine($"order after: {Names(filter)}");
        var moved = ReferenceEquals(filter.Root.Children[0], last) && ReferenceEquals(filter.Root.Children[1], mark);
        Console.WriteLine(moved ? "PASS: the last condition moved to the top" : "FAIL: the drag did not move the condition");
        return moved ? 0 : 1;
    }

    private static string? Drag(DrawingPoint from, DrawingPoint to, IntPtr handle)
    {
        Mouse.MoveTo(from);
        Wait.UntilInputIsProcessed();
        if (GetForegroundWindow() != handle || !Owns(from))
        {
            return "STOPPED: another window took the front before the press, so nothing was clicked.";
        }

        Mouse.Down(MouseButton.Left);
        const int Steps = 20;
        for (var step = 1; step <= Steps; step++)
        {
            var next = new DrawingPoint(from.X + ((to.X - from.X) * step / Steps), from.Y + ((to.Y - from.Y) * step / Steps));
            if (GetForegroundWindow() != handle || !Owns(next))
            {
                Mouse.MoveTo(from);
                Mouse.Up(MouseButton.Left);
                return "STOPPED: another window came under the pointer during the drag; the press was released on the sandbox grip.";
            }

            Mouse.MoveTo(next);
            Thread.Sleep(30);
        }

        Thread.Sleep(200);
        Mouse.Up(MouseButton.Left);
        Wait.UntilInputIsProcessed();
        return null;
    }

    private static DrawingPoint Screen(FrameworkElement element, double x, double y)
    {
        var point = element.PointToScreen(new Point(x, y));
        return new DrawingPoint((int)point.X, (int)point.Y);
    }

    private static bool Owns(DrawingPoint point)
    {
        GetWindowThreadProcessId(WindowFromPoint(new NativePoint { X = point.X, Y = point.Y }), out var process);
        return process == Environment.ProcessId;
    }

    private static List<Thumb> Grips(DependencyObject root)
    {
        var grips = new List<Thumb>();
        var queue = new Queue<DependencyObject>();
        queue.Enqueue(root);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current is Thumb { IsVisible: true } thumb && AutomationProperties.GetAutomationId(thumb) == "Filter.Item.Grip")
            {
                grips.Add(thumb);
            }

            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(current); index++)
            {
                queue.Enqueue(VisualTreeHelper.GetChild(current, index));
            }
        }

        return grips.OrderBy(grip => grip.TranslatePoint(new Point(), (UIElement)root).Y).ToList();
    }

    private static T Find<T>(DependencyObject root, string automationId)
        where T : FrameworkElement
    {
        var queue = new Queue<DependencyObject>();
        queue.Enqueue(root);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current is T { IsVisible: true } found && AutomationProperties.GetAutomationId(found) == automationId)
            {
                return found;
            }

            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(current); index++)
            {
                queue.Enqueue(VisualTreeHelper.GetChild(current, index));
            }
        }

        throw new InvalidOperationException(automationId);
    }

    private static string Names(FilterTabViewModel filter)
    {
        return string.Join(", ", filter.Root.Children.OfType<RuleViewModel>().Select(rule => rule.ParameterName));
    }

    private static Task Settle(int milliseconds)
    {
        return Task.Delay(milliseconds);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(NativePoint point);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out int process);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr window);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }
}
