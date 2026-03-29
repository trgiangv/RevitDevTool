using DevTools.Logging;
using DevTools.Logging.Abstractions;
using RevitDevTool.Core;
using RevitDevTool.Logging.Enums;

namespace RevitDevTool.Logging.Enrichers;

public sealed class RevitContextProvider(IEnumerable<RevitEnricher> selected) : IContextEnricher
{
    private readonly HashSet<RevitEnricher> _enrichers = new(selected);
    private Dictionary<string, object?>? _staticCache;

    private static readonly RevitEnricher[] StaticEnrichers =
        [RevitEnricher.RevitVersion, RevitEnricher.RevitBuild, RevitEnricher.RevitUserName, RevitEnricher.RevitLanguage];

    private static readonly RevitEnricher[] DynamicEnrichers =
        [RevitEnricher.RevitDocumentTitle, RevitEnricher.RevitDocumentPathName, RevitEnricher.RevitDocumentModelPath];

    public Dictionary<string, object?> GetStaticProperties()
    {
        var matched = Array.FindAll(StaticEnrichers, _enrichers.Contains);
        if (matched.Length == 0)
            return new Dictionary<string, object?>();

        var statics = GetOrCacheStaticProperties();
        var result = new Dictionary<string, object?>();

        foreach (var enricher in matched)
        {
            if (statics.TryGetValue(enricher.ToString(), out var value))
                result[enricher.ToString()] = value;
        }

        return result;
    }

    public Dictionary<string, object?>? GetDynamicProperties()
    {
        var matched = Array.FindAll(DynamicEnrichers, _enrichers.Contains);
        if (matched.Length == 0) return null;

        var result = new Dictionary<string, object?>();

        foreach (var enricher in matched)
        {
            result[enricher.ToString()] = enricher switch
            {
                RevitEnricher.RevitDocumentTitle => GetDocumentTitle(),
                RevitEnricher.RevitDocumentPathName => GetDocumentPathName(),
                RevitEnricher.RevitDocumentModelPath => GetDocumentModelPath(),
                _ => null
            };
        }

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
                [RevitEnricher.RevitVersion.ToString()] = app.VersionNumber,
                [RevitEnricher.RevitBuild.ToString()] = app.VersionBuild,
                [RevitEnricher.RevitUserName.ToString()] = app.Username,
                [RevitEnricher.RevitLanguage.ToString()] = app.Language.ToString()
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
