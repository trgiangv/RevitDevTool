using Autodesk.Revit.UI.Events;
using RevitDevTool.Core;

namespace RevitDevTool.Tools.ModelsContext;

internal static class ViewActivations
{
    public const string ChangedInCall = "The active view changed in this call; query it in a later call.";

    private const long None = -1;

    private static UIControlledApplication? _application;
    private static long _turn = None;

    public static bool Watch(UIControlledApplication application)
    {
        if (_application is not null)
        {
            return false;
        }

        _application = application;
        application.ViewActivated += OnViewActivated;
        return true;
    }

    public static void Unwatch()
    {
        if (_application is not { } application)
        {
            return;
        }

        application.ViewActivated -= OnViewActivated;
        _application = null;
        _turn = None;
    }

    public static void ThrowIfChangedInCall()
    {
        if (_turn != None && _turn == RevitContextExecutor.Turn)
        {
            throw new InvalidOperationException(ChangedInCall);
        }
    }

    private static void OnViewActivated(object? sender, ViewActivatedEventArgs e)
    {
        _turn = RevitContextExecutor.Turn;
    }
}
