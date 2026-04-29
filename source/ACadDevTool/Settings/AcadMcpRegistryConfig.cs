using DevTools.Settings.Configs;

namespace AcadDevTool.Settings;

/// <summary>
/// AutoCAD-specific MCP registry config. Inherits all properties from McpRegistryConfig.
/// Uses a distinct type name so FileConfig persists to AcadMcpRegistryConfig.json
/// instead of colliding with Revit's McpRegistryConfig.json.
/// </summary>
public sealed class AcadMcpRegistryConfig : McpRegistryConfig;
