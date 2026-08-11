using System.Text.Json.Serialization;

namespace DevTools.NUnit.Core.Contracts;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record NUnitDebugReadyEvent(
    [property: JsonPropertyName("run_id")] Guid RunId,
    [property: JsonPropertyName("process_id")] int ProcessId);
