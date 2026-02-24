using System.Reflection;
using Autodesk.Revit.UI;
namespace RevitDevTool.CodeExecute.Providers.Dotnet;

internal static class AddinCommandData
{
    private static ExternalCommandData? _externalCommandData;
    private static ElementSet? _elementSet;

    public static ElementSet ElementSet => CreateElementSet();
    public static ExternalCommandData ExternalCommandData => CreateExternalCommandData();

    private static ExternalCommandData CreateExternalCommandData()
    {
        if (_externalCommandData != null)
        {
            _externalCommandData.View = Context.UiApplication.ActiveUIDocument?.ActiveView;
            return _externalCommandData;
        }
        var type = typeof(ExternalCommandData);
        var constructorInfos = type.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        var instance = (ExternalCommandData) constructorInfos[0].Invoke(null);
        instance.Application = Context.UiApplication;
        instance.JournalData ??= new Dictionary<string, string>();
        instance.View = Context.UiApplication.ActiveUIDocument?.ActiveView;

        _externalCommandData = instance;
        return instance;
    }

    private static ElementSet CreateElementSet()
    {
        _elementSet ??= new ElementSet();
        if (Context.UiApplication.ActiveUIDocument == null)
        {
            _elementSet.Clear();
            return _elementSet;
        }

        _elementSet.Clear();
        var ids = Context.UiApplication.ActiveUIDocument.Selection.GetElementIds();
        foreach (var id in ids)
        {
            var elem = Context.UiApplication.ActiveUIDocument.Document.GetElement(id);
            if (elem != null) _elementSet.Insert(elem);
        }

        return _elementSet;
    }
}