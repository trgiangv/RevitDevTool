using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Scope;

namespace DevTools.ModelsContext.Panel;

internal sealed class ContextLoader(PanelRuntime runtime, ModelContextViewModel owner)
{
    public async Task<int> LoadAsync(JobTicket ticket, ScopeKind scope)
    {
        var keys = await KeysInScopeAsync(scope, ticket.Token);
        if (!owner.IsCurrent(ticket) || !await ShowQuickCountsAsync(ticket, scope, keys))
        {
            return -1;
        }

        if (await ReadDocumentsAsync(ticket, scope, keys) is not { } documents)
        {
            return -1;
        }

        var data = new ContextData(scope, owner.ContextBar.CurrentModel, documents,
            owner.ContextBar.Documents.Select(item => item.Info).ToList());
        await owner.Explorer.LoadAsync(data, ticket.Token);
        if (!owner.IsCurrent(ticket))
        {
            return -1;
        }

        await owner.Filter.LoadAsync(data, ticket.Token);
        return documents.Count;
    }

    private async Task<IReadOnlyList<DocumentKey>> KeysInScopeAsync(ScopeKind scope, CancellationToken cancellationToken)
    {
        if (scope != ScopeKind.Selection)
        {
            return owner.ContextBar.TickedDocuments;
        }

        var selection = await runtime.Services.Selection.GetSelectionAsync(cancellationToken);
        return selection
            .Select(element => element.Document)
            .Distinct()
            .OrderBy(key => !key.IsCurrentModel)
            .ToList();
    }

    private async Task<bool> ShowQuickCountsAsync(JobTicket ticket, ScopeKind scope, IReadOnlyList<DocumentKey> keys)
    {
        var store = runtime.Store;
        var loaded = keys.Where(key => store.Find(key) is { IsLoaded: true }).ToList();
        if (scope == ScopeKind.Selection || loaded.TrueForAll(store.IsReady))
        {
            return true;
        }

        var quick = new List<QuickDocument>();
        foreach (var key in loaded)
        {
            var groups = await runtime.Services.Source.CountGroupsAsync(key, scope, ticket.Token);
            quick.Add(new QuickDocument(key, owner.ContextBar.LabelOf(key), groups));
        }

        if (!owner.IsCurrent(ticket))
        {
            return false;
        }

        owner.Explorer.ShowQuickCounts(quick, owner.ContextBar.CurrentModel);
        return true;
    }

    private async Task<List<DocumentScope>?> ReadDocumentsAsync(JobTicket ticket, ScopeKind scope,
        IReadOnlyList<DocumentKey> keys)
    {
        var documents = new List<DocumentScope>();
        foreach (var key in keys)
        {
            if (runtime.Store.Find(key) is not { } info)
            {
                continue;
            }

            if (!info.IsLoaded)
            {
                AddKept(documents, info, key);
                continue;
            }

            if (await ReadLoadedAsync(ticket, scope, info, key) is not { } document)
            {
                return null;
            }

            documents.Add(document);
        }

        return documents;
    }

    private void AddKept(List<DocumentScope> documents, DocumentInfo info, DocumentKey key)
    {
        if (runtime.Store.TryGet(key) is { } kept)
        {
            documents.Add(new DocumentScope(info, owner.ContextBar.LabelOf(key), kept, null));
        }
    }

    private async Task<DocumentScope?> ReadLoadedAsync(JobTicket ticket, ScopeKind scope, DocumentInfo info,
        DocumentKey key)
    {
        var snapshot = await runtime.Store.GetAsync(key, null, ticket.Token);
        if (!owner.IsCurrent(ticket))
        {
            return null;
        }

        var ids = scope == ScopeKind.WholeModel
            ? null
            : await runtime.Services.Source.CollectScopeAsync(key, scope, ticket.Token);
        return owner.IsCurrent(ticket) ? new DocumentScope(info, owner.ContextBar.LabelOf(key), snapshot, ids) : null;
    }
}
