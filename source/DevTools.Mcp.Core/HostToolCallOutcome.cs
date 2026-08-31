using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Core;

/// <summary>Result of a single-round-trip host <c>tools/call</c> (no client-side MRTR auto-retry).</summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed class HostToolCallOutcome
{
    public CallToolResult? ToolResult { get; init; }
    public InputRequiredResult? InputRequired { get; init; }

    public bool IsInputRequired => InputRequired is not null;

    public static HostToolCallOutcome FromToolResult(CallToolResult result) =>
        new() { ToolResult = result };

    public static HostToolCallOutcome FromInputRequired(InputRequiredResult result) =>
        new() { InputRequired = result };
}
