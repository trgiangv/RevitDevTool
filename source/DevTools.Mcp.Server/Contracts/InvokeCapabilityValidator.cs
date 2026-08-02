using System.Text.Json;
using DevTools.Mcp.Core;

namespace DevTools.Mcp.Server.Contracts;

public sealed record ValidationProblem(string Name, string Message);

public static class InvokeCapabilityValidator
{
    private const int DefaultReadLimit = 16;
    private const int HardReadLimit = 64;

    public static IReadOnlyList<ValidationProblem> Validate(InvokeCapabilityRequest request)
    {
        var problems = new List<ValidationProblem>();
        var hasSingle = !string.IsNullOrWhiteSpace(request.CapabilityId) || request.Arguments.HasValue;
        var hasReads = request.Reads is { Count: > 0 };

        ValidateRequestShape(hasSingle, hasReads, problems);
        if (hasSingle)
            ValidateSingle(request, problems);
        if (request.Reads is { } reads)
            ValidateReads(reads, problems);

        return problems;
    }

    private static void ValidateRequestShape(bool hasSingle, bool hasReads, List<ValidationProblem> problems)
    {
        if (hasSingle && hasReads)
            problems.Add(new ValidationProblem("reads", "reads cannot be combined with capabilityId or arguments."));
        if (!hasSingle && !hasReads)
            problems.Add(new ValidationProblem("capabilityId", "Provide capabilityId or a non-empty reads array."));
    }

    private static void ValidateSingle(InvokeCapabilityRequest request, List<ValidationProblem> problems)
    {
        if (!DynamicCapabilityId.TryDecode(request.CapabilityId, out _))
            problems.Add(new ValidationProblem("capabilityId", "capabilityId is malformed."));
        ValidateArguments(request.Arguments, "arguments", problems);
    }

    private static void ValidateReads(IReadOnlyList<ResourceReadRequest> reads, List<ValidationProblem> problems)
    {
        if (reads.Count > DefaultReadLimit)
            problems.Add(new ValidationProblem("reads", $"reads may contain at most {DefaultReadLimit} items (hard maximum {HardReadLimit})."));

        for (var index = 0; index < reads.Count; index++)
            ValidateReadItem(reads[index], index, problems);
    }

    private static void ValidateReadItem(ResourceReadRequest read, int index, List<ValidationProblem> problems)
    {
        var capabilityIdPath = $"reads[{index}].capabilityId";
        if (!DynamicCapabilityId.TryDecode(read.CapabilityId, out var locator))
            problems.Add(new ValidationProblem(capabilityIdPath, "capabilityId is malformed."));
        else if (locator?.Kind == HostCatalogKind.Tool)
            problems.Add(new ValidationProblem(capabilityIdPath, "reads supports resources and resource templates only."));

        ValidateArguments(read.Arguments, $"reads[{index}].arguments", problems);
    }

    private static void ValidateArguments(Dictionary<string, JsonElement>? value, string name, List<ValidationProblem> problems)
    {
        _ = value;
        _ = name;
    }

    private static void ValidateArguments(JsonElement? value, string name, List<ValidationProblem> problems)
    {
        if (value is { ValueKind: not JsonValueKind.Object and not JsonValueKind.Null and not JsonValueKind.Undefined })
            problems.Add(new ValidationProblem(name, "arguments must be a JSON object."));
    }
}
