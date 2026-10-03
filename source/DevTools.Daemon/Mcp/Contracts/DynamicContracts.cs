using System.Text.Json;
using DevTools.Daemon.Mcp.Processes;

namespace DevTools.Daemon.Mcp.Contracts;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record SearchResponse(
    int Count,
    bool HasMore,
    IReadOnlyList<SearchItem> Items,
    IReadOnlyList<string>? AvailableNames = null);

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record SearchItem(
    string Id,
    CatalogType Kind,
    string Target,
    string? Description,
    int ProcessId,
    string? HostApp,
    string? VersionNumber,
    string[]? RequiredArgs,
    string[]? ArgsHint,
    JsonElement? InputSchema,
    string? MimeType);

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record InvokeRequest(
    string? Id = null,
    JsonElement? Arguments = null,
    IReadOnlyList<ResourceReadRequest>? Reads = null);

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record ResourceReadRequest(string? Id, Dictionary<string, JsonElement>? Arguments = null);

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record InvokeResponse(
    bool Ok,
    bool ExecutionStarted,
    object? Result = null,
    InvocationError? Error = null,
    IReadOnlyList<ResourceReadResult>? Results = null);

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record ResourceReadResult(int Index, bool Ok, object? Result = null, InvocationError? Error = null);

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record InvocationError(string Type, string Message, bool Retryable = false, string? Reason = null, string? Retry = null);

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record ValidationProblem(string Name, string Message);

/// <summary>Request shape checks and batch size bounds for <c>invoke_dynamic</c>.</summary>
public static class InvokeValidator
{
    public static class Argument
    {
        public const string Id = "id";
        public const string Reads = "reads";
        public const string Arguments = "arguments";
    }

    public const int DefaultResultBudgetBytes = 1024 * 1024;
    public const int HardResultBudgetBytes = 4 * 1024 * 1024;

    private const int DefaultReadLimit = 16;
    private const int HardReadLimit = 64;

    public static IReadOnlyList<ValidationProblem> Validate(InvokeRequest request)
    {
        var problems = new List<ValidationProblem>();
        var hasSingle = !string.IsNullOrWhiteSpace(request.Id) || request.Arguments.HasValue;
        var hasReads = request.Reads is { Count: > 0 };

        if (hasSingle && hasReads)
            problems.Add(new ValidationProblem(Argument.Reads, $"{Argument.Reads} cannot be combined with {Argument.Id} or {Argument.Arguments}."));
        if (!hasSingle && !hasReads)
            problems.Add(new ValidationProblem(Argument.Id, $"Provide {Argument.Id} or a non-empty {Argument.Reads} array."));
        if (hasSingle)
            ValidateSingle(request, problems);
        if (request.Reads is { } reads)
            ValidateReads(reads, problems);

        return problems;
    }

    private static void ValidateSingle(InvokeRequest request, List<ValidationProblem> problems)
    {
        if (!CatalogId.TryDecode(request.Id, out _))
            problems.Add(new ValidationProblem(Argument.Id, $"{Argument.Id} is malformed."));
        ValidateArguments(request.Arguments, Argument.Arguments, problems);
    }

    private static void ValidateReads(IReadOnlyList<ResourceReadRequest> reads, List<ValidationProblem> problems)
    {
        if (reads.Count > DefaultReadLimit)
            problems.Add(new ValidationProblem(Argument.Reads, $"{Argument.Reads} may contain at most {DefaultReadLimit} items (hard maximum {HardReadLimit})."));

        for (var index = 0; index < reads.Count; index++)
            ValidateReadItem(reads[index], index, problems);
    }

    private static void ValidateReadItem(ResourceReadRequest read, int index, List<ValidationProblem> problems)
    {
        var path = $"{Argument.Reads}[{index}].{Argument.Id}";
        if (!CatalogId.TryDecode(read.Id, out var locator))
            problems.Add(new ValidationProblem(path, "id is malformed."));
        else if (locator?.Kind == CatalogType.Tool)
            problems.Add(new ValidationProblem(path, "reads supports resources and resource templates only."));
    }

    private static void ValidateArguments(JsonElement? value, string name, List<ValidationProblem> problems)
    {
        if (value is { ValueKind: not JsonValueKind.Object and not JsonValueKind.Null and not JsonValueKind.Undefined })
            problems.Add(new ValidationProblem(name, "arguments must be a JSON object."));
    }
}

/// <summary>Opaque state embedded in daemon <c>invoke_dynamic</c> incomplete-result <c>requestState</c>.</summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
internal sealed record InvokeState(string Id, JsonElement? Arguments, string? HostRequestState)
{
    public static InvokeState? TryParse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize(json, McpServerJsonContext.Default.InvokeState);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public string Serialize() => JsonSerializer.Serialize(this, McpServerJsonContext.Default.InvokeState);
}
