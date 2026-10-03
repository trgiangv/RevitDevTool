using DevTools.Daemon.Mcp.Contracts;

namespace DevTools.Daemon.Mcp.Processes;

/// <summary>Turns an id from search into the current catalog item, or a stale/validation error.</summary>
public readonly record struct CatalogLookup(
    CatalogId? Id,
    IProcessSession? Session,
    InvokeResponse? Error);

public static class CatalogResolver
{
    public static CatalogLookup Resolve(IProcessSessions sessions, string id)
    {
        if (!CatalogId.TryDecode(id, out var locator) || locator is null)
            return new CatalogLookup(null, null, Error("validation_error", "id is malformed."));

        var catalog = sessions.Catalog.List().FirstOrDefault(item => item.ProcessId == locator.ProcessId);
        var session = sessions.GetByProcessId(locator.ProcessId);
        if (catalog is null || session is null || !session.IsConnected)
            return new CatalogLookup(locator, null, Stale("host_disconnected", "The host session is no longer connected."));

        var stale = Validate(sessions, locator);
        return stale is null
            ? new CatalogLookup(locator, session, null)
            : new CatalogLookup(locator, session, stale);
    }

    private static InvokeResponse? Validate(IProcessSessions sessions, CatalogId locator)
    {
        var item = sessions.Catalog.Find(locator.Kind, locator.Target, locator.ProcessId);
        if (item is null)
            return Stale("removed", "The item is no longer in this process catalog.");

        var currentHash = CatalogId.ContentHashFor(item);
        return string.Equals(locator.ContentHash, currentHash, StringComparison.OrdinalIgnoreCase)
            ? null
            : Stale("changed", "The item changed; search again before invoking.");
    }

    private static InvokeResponse Stale(string reason, string message) =>
        new(false, false, Error: new InvocationError("stale", message, true, reason, "research_then_reinvoke"));

    private static InvokeResponse Error(string type, string message) =>
        new(false, false, Error: new InvocationError(type, message));
}
