using System.Text.Json.Serialization;
namespace DevTools.McpParser.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ExecutionMode
{
    Script,
    Assembly,
    Python,
    FSharp
}
