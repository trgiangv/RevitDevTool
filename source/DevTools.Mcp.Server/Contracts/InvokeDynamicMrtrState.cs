using System.Text.Json;

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
            return JsonSerializer.Deserialize(json, McpServerJsonContext.Default.InvokeDynamicMrtrState);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public string Serialize() => JsonSerializer.Serialize(this, McpServerJsonContext.Default.InvokeDynamicMrtrState);
}
