using System.Text.Json.Serialization;

namespace DevTools.NUnit.Core.Contracts;

[JsonSerializable(typeof(NUnitHelloRequest))]
[JsonSerializable(typeof(NUnitHelloResponse))]
[JsonSerializable(typeof(NUnitDiscoverRequest))]
[JsonSerializable(typeof(NUnitDiscoveredTest))]
[JsonSerializable(typeof(NUnitDiscoverResponse))]
[JsonSerializable(typeof(NUnitRunRequest))]
[JsonSerializable(typeof(NUnitRunSummary))]
[JsonSerializable(typeof(NUnitCaseResult))]
[JsonSerializable(typeof(NUnitRunResponse))]
[JsonSerializable(typeof(NUnitProgressEvent))]
[JsonSerializable(typeof(NUnitDebugReadyEvent))]
[JsonSerializable(typeof(NUnitCancelRequest))]
[JsonSerializable(typeof(List<NUnitDiscoveredTest>))]
[JsonSerializable(typeof(List<NUnitCaseResult>))]
[JsonSerializable(typeof(NUnitDiscoveredTest[]))]
[JsonSerializable(typeof(NUnitCaseResult[]))]
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
public sealed partial class NUnitJsonContext : JsonSerializerContext;
