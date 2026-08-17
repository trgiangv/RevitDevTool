using System.Text.Json.Serialization;
using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.Testing.Transport;

[JsonSerializable(typeof(TestingHelloRequest))]
[JsonSerializable(typeof(TestingHelloResponse))]
[JsonSerializable(typeof(TestingCancelRequest))]
[JsonSerializable(typeof(TestingHostOptions))]
[JsonSerializable(typeof(TestingAssemblyReference))]
[JsonSerializable(typeof(TestingSelection))]
[JsonSerializable(typeof(TestingRunRequest))]
[JsonSerializable(typeof(TestingAttachment))]
[JsonSerializable(typeof(TestingSourceLocation))]
[JsonSerializable(typeof(TestingTrait))]
[JsonSerializable(typeof(TestingCaseResult))]
[JsonSerializable(typeof(TestingEvent))]
[JsonSerializable(typeof(TestingRunResponse))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(List<TestingTrait>))]
[JsonSerializable(typeof(List<TestingAttachment>))]
[JsonSerializable(typeof(List<TestingCaseResult>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
public sealed partial class TestingJsonContext : JsonSerializerContext;
