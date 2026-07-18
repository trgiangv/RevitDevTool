using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Routing.Broker;

public sealed class BrokerCatalogIndex
{
    private readonly Lock gate = new();
    private BrokerSnapshot snapshot = BrokerSnapshot.Empty;

    public void ReplaceSnapshots(IEnumerable<HostCatalogSnapshot> snapshots)
    {
        var hostSnapshots = snapshots.ToArray();
        var entries = hostSnapshots
            .SelectMany(CreateEntries)
            .OrderBy(entry => entry.Host.ProcessId)
            .ThenBy(entry => entry.Kind)
            .ThenBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var hosts = hostSnapshots.Select(snapshot => snapshot.Instance)
            .DistinctBy(host => host.ProcessId)
            .OrderBy(host => host.ProcessId)
            .ToArray();
        var revisionInput = string.Join("\n", entries.Select(entry =>
            $"{entry.Host.ProcessId}\t{entry.Kind}\t{entry.Key}\t{entry.Schema?.GetRawText() ?? string.Empty}"));
        using var hash = SHA256.Create();
        var revision = string.Concat(hash.ComputeHash(Encoding.UTF8.GetBytes(revisionInput)).Select(value => value.ToString("x2")))[..16];
        var byTarget = entries.GroupBy(entry => new BrokerPrimitiveTarget(entry.Kind, entry.Key).ToString(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
        var byHost = entries.GroupBy(entry => entry.Host.ProcessId)
            .ToDictionary(group => group.Key, group => group.ToArray());

        lock (gate)
            snapshot = new BrokerSnapshot(revision, hosts, entries, byTarget, byHost);
    }

    public BrokerSearchResponse Search(BrokerSearchRequest request)
    {
        BrokerSnapshot current;
        lock (gate) current = snapshot;

        var candidates = request.HostId is { } hostId
            ? current.ByHost.GetValueOrDefault(hostId, [])
            : current.Entries;
        var matches = candidates
            .Where(entry => request.Kinds is null || request.Kinds.Count == 0 || request.Kinds.Contains(entry.Kind))
            .Select(entry => new { Entry = entry, Score = Score(entry, request.Query) })
            .Where(match => match.Score >= 0)
            .OrderBy(match => match.Score)
            .ThenBy(match => match.Entry.Kind)
            .ThenBy(match => match.Entry.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(match => match.Entry.Host.ProcessId)
            .ToArray();
        var selected = matches.Take(request.Limit).Select(match => ToItem(match.Entry, request.Detail)).ToArray();
        return new BrokerSearchResponse(current.Revision, current.Hosts, selected, matches.Length > selected.Length);
    }

    public async Task<CallToolResult> InvokeAsync(
        IInstanceManager sessions,
        BrokerPrimitiveTarget target,
        int? hostId,
        JsonElement? arguments,
        CancellationToken cancellationToken)
    {
        BrokerSnapshot current;
        lock (gate) current = snapshot;
        if (!current.ByTarget.TryGetValue(target.ToString(), out var registered))
            registered = [];
        var candidates = registered.Where(entry => hostId is null || entry.Host.ProcessId == hostId).ToArray();
        if (candidates.Length == 0)
            return Error($"Target '{target}' is not available on a connected host.");
        if (candidates.Length > 1)
            return Ambiguous(target, candidates);

        var candidate = candidates[0];
        var session = sessions.GetSessionByProcessId(candidate.Host.ProcessId);
        if (session is null || !session.IsConnected)
            return Error($"Host {candidate.Host.ProcessId} is no longer connected.");

        try
        {
            return candidate.Kind switch
            {
                BrokerPrimitiveKind.Tool => await session.CallToolAsync(candidate.Key, BrokerArgumentConverter.ToObjects(arguments), cancellationToken).ConfigureAwait(false),
                BrokerPrimitiveKind.Resource => ConvertResource(await session.ReadResourceAsync(candidate.Key, cancellationToken).ConfigureAwait(false)),
                BrokerPrimitiveKind.Prompt => ConvertPrompt(await session.GetPromptAsync(candidate.Key, BrokerArgumentConverter.ToObjects(arguments), cancellationToken).ConfigureAwait(false)),
                _ => Error("Unsupported broker target.")
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Error($"Host invocation failed: {ex.Message}");
        }
    }

    private static IEnumerable<BrokerEntry> CreateEntries(HostCatalogSnapshot host)
    {
        foreach (var tool in host.Tools)
            yield return new BrokerEntry(BrokerPrimitiveKind.Tool, tool.ProtocolTool.Name, tool.ProtocolTool.Name,
                tool.ProtocolTool.Description, tool.ProtocolTool.InputSchema, host.Instance);
        foreach (var resource in host.Resources)
            yield return new BrokerEntry(BrokerPrimitiveKind.Resource, resource.ProtocolResource.Uri, resource.ProtocolResource.Name,
                resource.ProtocolResource.Description, null, host.Instance);
        foreach (var template in host.ResourceTemplates)
            yield return new BrokerEntry(BrokerPrimitiveKind.Resource, template.ProtocolResourceTemplate.UriTemplate, template.ProtocolResourceTemplate.Name,
                template.ProtocolResourceTemplate.Description, null, host.Instance);
        foreach (var prompt in host.Prompts)
            yield return new BrokerEntry(BrokerPrimitiveKind.Prompt, prompt.ProtocolPrompt.Name, prompt.ProtocolPrompt.Name,
                prompt.ProtocolPrompt.Description, null, host.Instance);
    }

    private static int Score(BrokerEntry entry, string? query)
    {
        if (string.IsNullOrWhiteSpace(query)) return 0;
        var comparison = StringComparison.OrdinalIgnoreCase;
        if (string.Equals(entry.Key, query, comparison) || string.Equals(entry.Name, query, comparison)) return 0;
        if (entry.Key.StartsWith(query, comparison) || entry.Name.StartsWith(query, comparison)) return 1;
        if (entry.Key.Contains(query, comparison) || entry.Name.Contains(query, comparison) ||
            entry.Description?.Contains(query, comparison) == true || entry.Host.HostApp.Contains(query, comparison) ||
            entry.Host.VersionNumber.Contains(query, comparison) || entry.Host.ProcessId.ToString().Contains(query, StringComparison.Ordinal)) return 2;
        return -1;
    }

    private static BrokerSearchItem ToItem(BrokerEntry entry, BrokerSearchDetail detail) => new(
        new BrokerPrimitiveTarget(entry.Kind, entry.Key).ToString(), entry.Kind, entry.Name, entry.Description,
        entry.Host.ProcessId, entry.Host.HostApp, entry.Host.VersionNumber,
        detail == BrokerSearchDetail.Schema ? entry.Schema : null);

    private static CallToolResult ConvertResource(ReadResourceResult result)
    {
        var content = result.Contents.SelectMany(contents => contents switch
        {
            TextResourceContents text => new ContentBlock[] { new TextContentBlock { Text = text.Text } },
            BlobResourceContents blob => new ContentBlock[] { new EmbeddedResourceBlock { Resource = blob } },
            _ => []
        }).ToArray();
        return new CallToolResult { Content = content.Length > 0 ? content : [new TextContentBlock { Text = "(empty resource)" }] };
    }

    private static CallToolResult ConvertPrompt(GetPromptResult result) => new()
    {
        Content = result.Messages.Select(message => message.Content).ToArray(),
        StructuredContent = JsonSerializer.SerializeToElement(new { result.Description })
    };

    private static CallToolResult Ambiguous(BrokerPrimitiveTarget target, IReadOnlyList<BrokerEntry> candidates) => new()
    {
        IsError = true,
        Content = [new TextContentBlock { Text = $"Target '{target}' is available on multiple hosts. Specify hostId." }],
        StructuredContent = JsonSerializer.SerializeToElement(new { candidates = candidates.Select(candidate => candidate.Host).ToArray() })
    };

    private static CallToolResult Error(string message) => new()
    {
        IsError = true,
        Content = [new TextContentBlock { Text = message }]
    };

    private sealed record BrokerEntry(BrokerPrimitiveKind Kind, string Key, string Name, string? Description, JsonElement? Schema, HostInstanceDescriptor Host);
    private sealed record BrokerSnapshot(
        string Revision,
        IReadOnlyList<HostInstanceDescriptor> Hosts,
        IReadOnlyList<BrokerEntry> Entries,
        IReadOnlyDictionary<string, BrokerEntry[]> ByTarget,
        IReadOnlyDictionary<int, BrokerEntry[]> ByHost)
    {
        public static BrokerSnapshot Empty { get; } = new(
            "e3b0c44298fc1c14", [], [],
            new Dictionary<string, BrokerEntry[]>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<int, BrokerEntry[]>());
    }
}
