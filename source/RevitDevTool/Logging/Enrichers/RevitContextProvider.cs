using DevTools.Logging;
using DevTools.Logging.Abstractions;
using RevitDevTool.Core;
using RevitDevTool.Logging.Enums;

namespace RevitDevTool.Logging.Enrichers;

public sealed class RevitContextProvider(RevitEnricher flags) : IContextEnricher
{
    private Dictionary<string, object?>? _staticCache;

    private const RevitEnricher StaticFlags = RevitEnricher.RevitVersion | RevitEnricher.RevitBuild | RevitEnricher.RevitUserName | RevitEnricher.RevitLanguage;
    private const RevitEnricher DynamicFlags = RevitEnricher.RevitDocumentTitle | RevitEnricher.RevitDocumentPathName | RevitEnricher.RevitDocumentModelPath;

    public Dictionary<string, object?> GetStaticProperties()
    {
        var staticPart = flags & StaticFlags;
        if (staticPart == RevitEnricher.None)
            return new Dictionary<string, object?>();

        var statics = GetOrCacheStaticProperties();
        var result = new Dictionary<string, object?>();

        if (staticPart.HasFlag(RevitEnricher.RevitVersion) && statics.TryGetValue(nameof(RevitEnricher.RevitVersion), out var v))
            result[nameof(RevitEnricher.RevitVersion)] = v;
        if (staticPart.HasFlag(RevitEnricher.RevitBuild) && statics.TryGetValue(nameof(RevitEnricher.RevitBuild), out var b))
            result[nameof(RevitEnricher.RevitBuild)] = b;
        if (staticPart.HasFlag(RevitEnricher.RevitUserName) && statics.TryGetValue(nameof(RevitEnricher.RevitUserName), out var u))
            result[nameof(RevitEnricher.RevitUserName)] = u;
        if (staticPart.HasFlag(RevitEnricher.RevitLanguage) && statics.TryGetValue(nameof(RevitEnricher.RevitLanguage), out var l))
            result[nameof(RevitEnricher.RevitLanguage)] = l;

        return result;
    }

    public Dictionary<string, object?>? GetDynamicProperties()
    {
        var dynamicPart = flags & DynamicFlags;
        if (dynamicPart == RevitEnricher.None) return null;

        var result = new Dictionary<string, object?>();

        if (dynamicPart.HasFlag(RevitEnricher.RevitDocumentTitle))
            result[nameof(RevitEnricher.RevitDocumentTitle)] = GetDocumentTitle();
        if (dynamicPart.HasFlag(RevitEnricher.RevitDocumentPathName))
            result[nameof(RevitEnricher.RevitDocumentPathName)] = GetDocumentPathName();
        if (dynamicPart.HasFlag(RevitEnricher.RevitDocumentModelPath))
            result[nameof(RevitEnricher.RevitDocumentModelPath)] = GetDocumentModelPath();

        return result.Count > 0 ? result : null;
    }

    private Dictionary<string, object?> GetOrCacheStaticProperties()
    {
        if (_staticCache != null) return _staticCache;

        try
        {
            var app = RevitContext.Application;
            _staticCache = new Dictionary<string, object?>
            {
                [nameof(RevitEnricher.RevitVersion)] = app.VersionNumber,
                [nameof(RevitEnricher.RevitBuild)] = app.VersionBuild,
                [nameof(RevitEnricher.RevitUserName)] = app.Username,
                [nameof(RevitEnricher.RevitLanguage)] = app.Language.ToString()
            };
        }
        catch
        {
            _staticCache = new Dictionary<string, object?>();
        }

        return _staticCache;
    }

    private static string? GetDocumentTitle()
    {
        try { return RevitContext.ActiveDocument?.Title; }
        catch { return null; }
    }

    private static string? GetDocumentPathName()
    {
        try { return RevitContext.ActiveDocument?.PathName; }
        catch { return null; }
    }

    private static string? GetDocumentModelPath()
    {
        try
        {
            var doc = RevitContext.ActiveDocument;
            var modelPath = doc?.GetWorksharingCentralModelPath();
            return modelPath != null
                ? ModelPathUtils.ConvertModelPathToUserVisiblePath(modelPath)
                : null;
        }
        catch { return null; }
    }
}
