using System.IO;
using AcadDevTool.Logging.Enums;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using DevTools.Logging.Abstractions;

namespace AcadDevTool.Logging.Enrichers;

public sealed class AcadContextProvider(IEnumerable<AcadEnricher> selected) : IContextEnricher
{
    private readonly HashSet<AcadEnricher> _enrichers = new(selected);
    private Dictionary<string, object?>? _staticCache;

    private static readonly AcadEnricher[] StaticEnrichers =
        [AcadEnricher.AcadVersion, AcadEnricher.AcadBuild, AcadEnricher.AcadUserName];

    private static readonly AcadEnricher[] DynamicEnrichers =
        [AcadEnricher.AcadDocumentTitle, AcadEnricher.AcadDocumentPathName];

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
                AcadEnricher.AcadDocumentTitle => GetDocumentTitle(),
                AcadEnricher.AcadDocumentPathName => GetDocumentPathName(),
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
            var v = AcadApp.Version;
            _staticCache = new Dictionary<string, object?>
            {
                [nameof(AcadEnricher.AcadVersion)] = v.ToString(),
                [nameof(AcadEnricher.AcadBuild)] = v.Build.ToString(),
                [nameof(AcadEnricher.AcadUserName)] = Environment.UserName
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
        try
        {
            var doc = AcadApp.DocumentManager?.MdiActiveDocument;
            if (doc == null) return null;
            var name = doc.Name;
            if (string.IsNullOrEmpty(name)) return null;
            return Path.GetFileName(name);
        }
        catch { return null; }
    }

    private static string? GetDocumentPathName()
    {
        try
        {
            var doc = AcadApp.DocumentManager?.MdiActiveDocument;
            if (doc == null) return null;
            var path = doc.Database?.Filename;
            if (!string.IsNullOrEmpty(path)) return path;
            return doc.Name;
        }
        catch { return null; }
    }
}
