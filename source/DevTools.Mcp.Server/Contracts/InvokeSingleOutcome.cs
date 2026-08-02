using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Server.Contracts;

/// <summary>Single-shot host invocation result before mapping to daemon <see cref="CallToolResult"/> or MRTR.</summary>
internal sealed record InvokeSingleOutcome(InvokeCapabilityResponse? Response, InputRequiredResult? InputRequired)
{
    public static InvokeSingleOutcome FromResponse(InvokeCapabilityResponse response) => new(response, null);

    public static InvokeSingleOutcome FromInputRequired(InputRequiredResult inputRequired) => new(null, inputRequired);
}
