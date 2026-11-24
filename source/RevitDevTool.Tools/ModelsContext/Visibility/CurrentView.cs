using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Visibility;
using RevitDevTool.Tools.ModelsContext.Documents;
using RevitDevTool.Tools.ModelsContext.Pane;

namespace RevitDevTool.Tools.ModelsContext.Visibility;

internal static class CurrentView
{
    public static (UIDocument Active, View View) Require(UIApplication application)
    {
        if (application.ActiveUIDocument is not { } active)
        {
            throw new InvalidOperationException(RevitErrors.Busy);
        }

        return (active, active.ActiveView ?? throw new InvalidOperationException(VisibilityText.NoActiveView));
    }

    public static (UIDocument Active, View View) Require(UIApplication application, string? viewId)
    {
        var (active, view) = Require(application);
        if (viewId is not null && viewId != view.Id.ToString())
        {
            throw new ViewChangedException();
        }

        return (active, view);
    }

    public static ViewInfo InfoOf(Document model, View view)
    {
        return new ViewInfo(DocumentRegistry.KeyOf(model),
            view.Id.ToString(), view.Name, ElementDrawing.KindOf(view));
    }
}
