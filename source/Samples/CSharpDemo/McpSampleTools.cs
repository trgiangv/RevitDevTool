using System;
using System.ComponentModel;
using ModelContextProtocol.Server;

namespace CSharpDemo;

[McpServerToolType]
public static class McpSampleTools
{
    [McpServerTool(Name = "get_demo_status")]
    [Description("Return demo status for MCP parser validation.")]
    public static object GetDemoStatus()
    {
        return new
        {
            status = "success",
            summary = "Demo MCP tool is reachable.",
            data = new
            {
                language = "csharp",
                sample = true,
            },
            warnings = Array.Empty<string>(),
        };
    }
}