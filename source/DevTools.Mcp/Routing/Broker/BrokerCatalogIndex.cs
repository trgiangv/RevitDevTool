using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DevTools.Mcp.Routing.Catalog;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Routing.Broker;

public sealed class BrokerCatalogIndex
{
    private readonly Lock gate = new();
    private readonly ILogger<BrokerCatalogIndex>? logger;
    private readonly Func<JsonElement?, IReadOnlyDictionary<string, object?>?> convertArguments;
    private BrokerSnapshot snapshot = BrokerSnapshot.Empty;

    public BrokerCatalogIndex(ILogger<BrokerCatalogIndex>? logger = null)
        : this(logger, BrokerArgumentConverter.ToObjects)
    {
    }

    internal BrokerCatalogIndex(
        ILogger<BrokerCatalogIndex>? logger,
        Func<JsonElement?, IReadOnlyDictionary<string, object?>?> argumentConverter)
    {
        this.logger = logger;
        convertArguments = argumentConverter;
    }

    public void ReplacePublications(IEnumerable<HostCatalogPublication> publications)
    {
        var hostPublications = publications.ToArray();
        var availablePublications = hostPublications
            .Where(publication => publication.State is HostCatalogState.Ready or HostCatalogState.Stale &&
                                  publication.Snapshot is not null)
            .ToArray();
        var entries = availablePublications
            .SelectMany(CreateEntries)
            .OrderBy(entry => entry.Host.ProcessId)
            .ThenBy(entry => entry.Kind)
            .ThenBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var hosts = hostPublications.Select(publication => publication.Instance)
            .DistinctBy(host => host.ProcessId)
            .OrderBy(host => host.ProcessId)
            .ToArray();
        var catalogs = hostPublications
            .OrderBy(publication => publication.Instance.ProcessId)
            .Select(publication => new HostCatalogStatus(
                publication.Instance.ProcessId,
                publication.State,
                publication.UpdatedAt,
                publication.StaleSince,
                publication.LastErrorCode))
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
            snapshot = new BrokerSnapshot(revision, hosts, entries, byTarget, byHost, catalogs);
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
        return new BrokerSearchResponse(
            current.Revision,
            current.Hosts,
            selected,
            matches.Length > selected.Length,
            current.Catalogs);
    }

    public async Task<CallToolResult> InvokeAsync(
        IInstanceManager sessions,
        BrokerPrimitiveTarget target,
        int? hostId,
        JsonElement? arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        BrokerSnapshot current;
        lock (gate) current = snapshot;
        if (!current.ByTarget.TryGetValue(target.ToString(), out var registered) || registered.Length == 0)
            throw CreateInfrastructureFailure(BrokerInvokeStatus.TargetNotFound, target, hostId, []);

        if (hostId is { } requestedHostId && registered.All(entry => entry.Host.ProcessId != requestedHostId))
            throw CreateInfrastructureFailure(
                BrokerInvokeStatus.HostMismatch,
                target,
                hostId,
                registered.Select(ToInvokeCandidate).ToArray());

        var candidates = registered.Where(entry => hostId is null || entry.Host.ProcessId == hostId).ToArray();
        if (candidates.Length > 1)
            throw CreateInfrastructureFailure(
                BrokerInvokeStatus.HostSelectionRequired,
                target,
                hostId,
                candidates.Select(ToInvokeCandidate).ToArray());

        var candidate = candidates[0];
        IReadOnlyDictionary<string, object?>? convertedArguments = null;
        if (candidate.Kind is BrokerPrimitiveKind.Tool or BrokerPrimitiveKind.Prompt)
        {
            try
            {
                convertedArguments = convertArguments(arguments);
            }
            catch (Exception ex)
            {
                LogInvokeFailure(ex, BrokerInvokeStatus.HostFailed, target, candidate);
                throw CreateInfrastructureFailure(BrokerInvokeStatus.HostFailed, target, hostId, []);
            }
        }

        var session = sessions.GetSession(candidate.Host.ProcessId, candidate.SessionGeneration);
        if (session is null || !session.IsConnected)
            throw CreateInfrastructureFailure(BrokerInvokeStatus.HostDisconnected, target, hostId, []);

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            return candidate.Kind switch
            {
                BrokerPrimitiveKind.Tool => await session.CallToolAsync(candidate.Key, convertedArguments, deadline.Token).ConfigureAwait(false),
                BrokerPrimitiveKind.Resource => ConvertResource(await session.ReadResourceAsync(candidate.Key, deadline.Token).ConfigureAwait(false)),
                BrokerPrimitiveKind.Prompt => ConvertPrompt(await session.GetPromptAsync(candidate.Key, convertedArguments, deadline.Token).ConfigureAwait(false)),
                _ => throw CreateInfrastructureFailure(BrokerInvokeStatus.HostFailed, target, hostId, [], mayHaveExecuted: true)
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException ex) when (deadline.IsCancellationRequested)
        {
            LogInvokeFailure(ex, BrokerInvokeStatus.TimedOut, target, candidate);
            throw CreateInfrastructureFailure(BrokerInvokeStatus.TimedOut, target, hostId, [], mayHaveExecuted: true);
        }
        catch (IOException ex)
        {
            LogInvokeFailure(ex, BrokerInvokeStatus.ConnectionLost, target, candidate);
            throw CreateInfrastructureFailure(BrokerInvokeStatus.ConnectionLost, target, hostId, [], mayHaveExecuted: true);
        }
        catch (ObjectDisposedException ex)
        {
            LogInvokeFailure(ex, BrokerInvokeStatus.ConnectionLost, target, candidate);
            throw CreateInfrastructureFailure(BrokerInvokeStatus.ConnectionLost, target, hostId, [], mayHaveExecuted: true);
        }
        catch (Exception ex)
        {
            LogInvokeFailure(ex, BrokerInvokeStatus.HostFailed, target, candidate);
            throw CreateInfrastructureFailure(BrokerInvokeStatus.HostFailed, target, hostId, [], mayHaveExecuted: true);
        }
    }

    private static IEnumerable<BrokerEntry> CreateEntries(HostCatalogPublication publication)
    {
        var host = publication.Snapshot!;
        foreach (var tool in host.Tools)
            yield return new BrokerEntry(BrokerPrimitiveKind.Tool, tool.ProtocolTool.Name, tool.ProtocolTool.Name,
                tool.ProtocolTool.Description, tool.ProtocolTool.InputSchema, host.Instance, publication.Identity.SessionGeneration);
        foreach (var resource in host.Resources)
            yield return new BrokerEntry(BrokerPrimitiveKind.Resource, resource.ProtocolResource.Uri, resource.ProtocolResource.Name,
                resource.ProtocolResource.Description, null, host.Instance, publication.Identity.SessionGeneration);
        foreach (var template in host.ResourceTemplates)
            yield return new BrokerEntry(BrokerPrimitiveKind.Resource, template.ProtocolResourceTemplate.UriTemplate, template.ProtocolResourceTemplate.Name,
                template.ProtocolResourceTemplate.Description, null, host.Instance, publication.Identity.SessionGeneration);
        foreach (var prompt in host.Prompts)
            yield return new BrokerEntry(BrokerPrimitiveKind.Prompt, prompt.ProtocolPrompt.Name, prompt.ProtocolPrompt.Name,
                prompt.ProtocolPrompt.Description, null, host.Instance, publication.Identity.SessionGeneration);
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

    private static BrokerInvokeCandidate ToInvokeCandidate(BrokerEntry entry) =>
        new(entry.Host.ProcessId, entry.Host.HostApp, entry.Host.VersionNumber);

    private static McpException CreateInfrastructureFailure(
        string status,
        BrokerPrimitiveTarget target,
        int? requestedHostId,
        IReadOnlyList<BrokerInvokeCandidate> candidates,
        bool mayHaveExecuted = false)
    {
        var candidateIds = candidates.Count > 0
            ? string.Join(", ", candidates.Select(candidate => candidate.HostId))
            : null;
        var message = status switch
        {
            BrokerInvokeStatus.HostSelectionRequired =>
                candidateIds is null
                    ? $"Target '{target}' is available on multiple hosts; retry with one of the candidate hostId values."
                    : $"Target '{target}' is available on multiple hosts ({candidateIds}); retry with one of those hostId values.",
            BrokerInvokeStatus.HostMismatch =>
                candidateIds is null
                    ? $"Target '{target}' is not available on host {requestedHostId}; retry with one of the candidate hostId values."
                    : $"Target '{target}' is not available on host {requestedHostId}; retry with hostId {candidateIds}.",
            BrokerInvokeStatus.TargetNotFound =>
                $"Target '{target}' was not found; search the current broker catalog before retrying.",
            BrokerInvokeStatus.HostDisconnected =>
                $"The selected host disconnected before target '{target}' could be dispatched.",
            BrokerInvokeStatus.ConnectionLost =>
                $"The connection to the selected host was lost while invoking target '{target}'.",
            BrokerInvokeStatus.TimedOut =>
                $"Target '{target}' exceeded its broker deadline; confirm host state before retrying.",
            _ => $"The selected host failed while invoking target '{target}'."
        };
        if (mayHaveExecuted)
            message += " The target may have executed before the failure.";

        return new McpException(message);
    }

    private void LogInvokeFailure(
        Exception exception,
        string status,
        BrokerPrimitiveTarget target,
        BrokerEntry candidate) =>
        logger?.LogWarning(
            exception,
            "Broker invocation {Status} for target {Target} on PID {ProcessId}, generation {SessionGeneration}; correlation {CorrelationId}.",
            status,
            target.ToString(),
            candidate.Host.ProcessId,
            candidate.SessionGeneration,
            Activity.Current?.Id ?? "none");

    private sealed record BrokerEntry(
        BrokerPrimitiveKind Kind,
        string Key,
        string Name,
        string? Description,
        JsonElement? Schema,
        HostInstanceDescriptor Host,
        int SessionGeneration);
    private sealed record BrokerSnapshot(
        string Revision,
        IReadOnlyList<HostInstanceDescriptor> Hosts,
        IReadOnlyList<BrokerEntry> Entries,
        IReadOnlyDictionary<string, BrokerEntry[]> ByTarget,
        IReadOnlyDictionary<int, BrokerEntry[]> ByHost,
        IReadOnlyList<HostCatalogStatus> Catalogs)
    {
        public static BrokerSnapshot Empty { get; } = new(
            "e3b0c44298fc1c14", [], [],
            new Dictionary<string, BrokerEntry[]>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<int, BrokerEntry[]>(),
            []);
    }
}
