using System.Text.Json.Serialization;
namespace RevitDevTool.McpParser.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ExecutionMode
{
    Script,
    Assembly,
    Python,
    FSharp
}
