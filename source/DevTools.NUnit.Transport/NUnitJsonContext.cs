using System.Text.Json.Serialization;
using DevTools.NUnit.Core.Contracts;

namespace DevTools.NUnit.Transport;

[JsonSerializable(typeof(NUnitHelloRequest))]
[JsonSerializable(typeof(NUnitHelloResponse))]
[JsonSerializable(typeof(NUnitDiscoverRequest))]
[JsonSerializable(typeof(NUnitTrait))]
[JsonSerializable(typeof(NUnitSourceLocation))]
[JsonSerializable(typeof(NUnitAttachment))]
[JsonSerializable(typeof(NUnitRuntimeDiagnostic))]
[JsonSerializable(typeof(NUnitDiscoveredTest))]
[JsonSerializable(typeof(NUnitDiscoverResponse))]
[JsonSerializable(typeof(NUnitRunRequest))]
[JsonSerializable(typeof(NUnitRunSummary))]
[JsonSerializable(typeof(NUnitCaseResult))]
[JsonSerializable(typeof(NUnitRunResponse))]
[JsonSerializable(typeof(NUnitProgressEvent))]
[JsonSerializable(typeof(NUnitCancelRequest))]
[JsonSerializable(typeof(List<NUnitDiscoveredTest>))]
[JsonSerializable(typeof(List<NUnitCaseResult>))]
[JsonSerializable(typeof(List<NUnitTrait>))]
[JsonSerializable(typeof(List<NUnitAttachment>))]
[JsonSerializable(typeof(NUnitDiscoveredTest[]))]
[JsonSerializable(typeof(NUnitCaseResult[]))]
[JsonSerializable(typeof(NUnitTrait[]))]
[JsonSerializable(typeof(NUnitAttachment[]))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
public sealed partial class NUnitJsonContext : JsonSerializerContext;
