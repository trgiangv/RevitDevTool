using System.Text.Json.Serialization;

namespace DevTools.Mcp.Core;

[JsonSerializable(typeof(McpError))]
[JsonSerializable(typeof(ValidationProblem))]
[JsonSerializable(typeof(McpInvocationResponse))]
public partial class McpContractJsonContext : JsonSerializerContext;
