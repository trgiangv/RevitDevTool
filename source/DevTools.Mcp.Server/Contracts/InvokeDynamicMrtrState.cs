using System.Text.Json;
using ModelContextProtocol;

namespace DevTools.Mcp.Server.Contracts;

/// <summary>Opaque MRTR state embedded in daemon <c>invoke_dynamic</c> incomplete-result <c>requestState</c>.</summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
internal sealed record InvokeDynamicMrtrState(string CapabilityId, JsonElement? Arguments, string? HostRequestState)
{
    public static InvokeDynamicMrtrState? TryParse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<InvokeDynamicMrtrState>(json, McpJsonUtilities.DefaultOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public string Serialize() => JsonSerializer.Serialize(this, McpJsonUtilities.DefaultOptions);
}
