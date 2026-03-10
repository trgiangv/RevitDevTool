using System.Text.Json.Serialization;

namespace RevitDevTool.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ExecutionMode
{
    Script,
    Assembly,
    Python,
    FSharp
}
