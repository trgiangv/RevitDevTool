using System.Text.Json.Serialization;
namespace DevTool.McpParser.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ExecutionMode
{
    Script,
    Assembly,
    Python,
    FSharp
}
