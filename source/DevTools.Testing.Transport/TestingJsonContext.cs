using System.Text.Json.Serialization;
using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.Testing.Transport;

[JsonSerializable(typeof(TestFrameworkId))]
[JsonSerializable(typeof(TestHelloRequest))]
[JsonSerializable(typeof(TestHelloResponse))]
[JsonSerializable(typeof(TestCancelRequest))]
[JsonSerializable(typeof(TestCancelResponse))]
[JsonSerializable(typeof(TestRunnerStreamMessage))]
[JsonSerializable(typeof(TestHostOptions))]
[JsonSerializable(typeof(TestAssemblyReference))]
[JsonSerializable(typeof(TestSelection))]
[JsonSerializable(typeof(TestSelectionKind))]
[JsonSerializable(typeof(TestRunRequest))]
[JsonSerializable(typeof(TestRunExecute))]
[JsonSerializable(typeof(TestAttachment))]
[JsonSerializable(typeof(TestSourceLocation))]
[JsonSerializable(typeof(TestTrait))]
[JsonSerializable(typeof(TestCaseResult))]
[JsonSerializable(typeof(TestEvent))]
[JsonSerializable(typeof(TestRunResponse))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(List<TestTrait>))]
[JsonSerializable(typeof(List<TestAttachment>))]
[JsonSerializable(typeof(List<TestCaseResult>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true,
    Converters = [typeof(TestFrameworkIdJsonConverter)])]
public sealed partial class TestingJsonContext : JsonSerializerContext;
