using DevTools.ModelsContext.Diagnostics;
using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Visibility;
using RevitDevTool.Tools.ModelsContext.Pane;
using RevitDevTool.Tools.ModelsContext.Selection;
using System.Reflection;
using ZLogger;

namespace RevitDevTool.Tools.ModelsContext.Visibility;

internal static class LinkedHideShow
{
    public const string NotHidden = "Revit did not hide these linked elements.";

    private const string HandlerAssembly = "UIFrameworkServices";
    private const string HandlerType = "UIFrameworkServices.CommandHandlerService";
    private const string HandlerMethod = "invokeCommandHandler";
    private const string HideCommand = "ID_VIEW_HIDE_ELEMENTS";
    private const string UnhideCommand = "ID_VIEW_UNHIDE_ELEMENTS";
    private const string HideName = "Model Context Hide";
    private const string ShowName = "Model Context Show";

    private static readonly Lazy<MethodInfo?> Handler = new(FindHandler);
    private static int _running;

    public static bool IsAvailable => Handler.Value is not null;

    public static bool IsRunning => Volatile.Read(ref _running) > 0;

    public static void Hide(UIApplication application, IReadOnlyCollection<ElementRef> elements)
    {
        Run(application, elements, HideName, (active, view, targets) =>
        {
            if (IsRevealOn(view))
            {
                Invoke(active, targets, HideCommand);
                return;
            }

            var before = LinkElements.VisibleOf(active.Document, view, targets);
            Invoke(active, targets, HideCommand);
            var after = LinkElements.VisibleOf(active.Document, view, before);
            RevitProfiler.Logger.ZLogTrace($"Hide links: {before.Count - after.Count} of {before.Count} visible elements hidden");
            if (before.Count > 0 && after.Count == before.Count)
            {
                throw new InvalidOperationException(NotHidden);
            }
        });
    }

    public static void Show(UIApplication application, IReadOnlyCollection<ElementRef> elements)
    {
        Run(application, elements, ShowName, (active, view, targets) =>
        {
            if (IsRevealOn(view))
            {
                Invoke(active, targets, UnhideCommand);
                return;
            }

            var visible = LinkElements.VisibleOf(active.Document, view, targets);
            var hidden = targets.Where(element => !visible.Contains(element)).ToList();
            if (hidden.Count == 0)
            {
                return;
            }

            SetReveal(active.Document, view, true);
            Invoke(active, hidden, UnhideCommand);
            SetReveal(active.Document, view, false);
            var shown = LinkElements.VisibleOf(active.Document, view, hidden).Count;
            RevitProfiler.Logger.ZLogTrace($"Show links: {shown} of {hidden.Count} hidden elements shown");
            if (shown == 0)
            {
                throw new InvalidOperationException(VisibilityReasons.NotShown);
            }
        });
    }

    private static void Run(
        UIApplication application,
        IReadOnlyCollection<ElementRef> elements,
        string name,
        Action<UIDocument, View, List<ElementRef>> work)
    {
        if (Handler.Value is null)
        {
            throw new InvalidOperationException(VisibilityReasons.ReferenceCommandsUnavailable);
        }

        var (active, view) = RevitViewActions.RequireView(application);
        var model = active.Document;
        var targets = LinkElements.ResolvedOf(model, elements);
        if (targets.Count == 0)
        {
            throw new InvalidOperationException(VisibilityReasons.Nothing);
        }

        RevitErrors.EnsureEditable(model);
        var selection = SelectionSnapshot.Take(active);
        Interlocked.Increment(ref _running);
        try
        {
            using var group = new TransactionGroup(model, name);
            group.Start();
            work(active, view, targets);
            group.Assimilate();
        }
        finally
        {
            Restore(application, selection);
            Interlocked.Decrement(ref _running);
        }
    }

    private static void Invoke(UIDocument active, IEnumerable<ElementRef> targets, string command)
    {
        active.Selection.SetReferences(LinkElements.ReferencesOf(active.Document, targets));
        try
        {
            Handler.Value!.Invoke(null, [command]);
        }
        catch (TargetInvocationException exception)
        {
            RevitProfiler.Logger.Swallowed(exception, "Link command", command);
            throw new InvalidOperationException(command == UnhideCommand
                ? VisibilityReasons.ShowFromReveal
                : NotHidden);
        }
    }

    private static void SetReveal(Document model, View view, bool on)
    {
        using var transaction = new Transaction(model, on ? "Reveal hidden on" : "Reveal hidden off");
        transaction.Start();
        if (on)
        {
            view.EnableRevealHiddenMode();
        }
        else
        {
            view.DisableTemporaryViewMode(TemporaryViewMode.RevealHiddenElements);
        }

        transaction.Commit();
    }

    private static bool IsRevealOn(View view)
    {
        return view.IsInTemporaryViewMode(TemporaryViewMode.RevealHiddenElements);
    }

    private static void Restore(UIApplication application, SelectionSnapshot selection)
    {
        try
        {
            selection.Restore(application);
        }
        catch (Exception exception)
        {
            RevitProfiler.Logger.Swallowed(exception, "Restore selection", application.ActiveUIDocument?.Document.Title);
        }
    }

    private static MethodInfo? FindHandler()
    {
        MethodInfo? method = null;
        try
        {
            method = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(assembly => assembly.GetName().Name == HandlerAssembly)
                ?.GetType(HandlerType)
                ?.GetMethod(HandlerMethod, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null,
                    [typeof(string)], null);
        }
        catch (Exception exception)
        {
            RevitProfiler.Logger.Swallowed(exception, "Command handler probe", HandlerMethod);
        }

        RevitProfiler.Logger.ZLogTrace($"Command handler probe: {HandlerMethod} found {method is not null}");
        return method;
    }
}
