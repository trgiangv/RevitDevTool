using System.ComponentModel;
using ModelContextProtocol.Server;

namespace RevitMcpToolSet.Resources;

[McpServerResourceType]
[Description("Static toolset reference resources: capabilities, patterns, errors, and units.")]
public static class ToolsetResources
{
    [McpServerResource(
        UriTemplate = "revit://toolset/capabilities",
        Name = "revit_toolset_capabilities",
        Title = "Toolset Capabilities",
        MimeType = "text/markdown")]
    [Description("Full tool catalog with when-to-use hints, constraints, and toolset vs god tool decision tree.")]
    public static string GetCapabilities() => EmbeddedContent.Capabilities;

    [McpServerResource(
        UriTemplate = "revit://toolset/patterns/query",
        Name = "revit_toolset_patterns_query",
        Title = "Query Patterns",
        MimeType = "text/markdown")]
    [Description("FilterSpec composition examples, spatial queries, and performance tips.")]
    public static string GetQueryPatterns() => EmbeddedContent.PatternsQuery;

    [McpServerResource(
        UriTemplate = "revit://toolset/patterns/mep",
        Name = "revit_toolset_patterns_mep",
        Title = "MEP Patterns",
        MimeType = "text/markdown")]
    [Description("MEP workflow: type discovery, system binding, segment placement, and validation.")]
    public static string GetMepPatterns() => EmbeddedContent.PatternsMep;

    [McpServerResource(
        UriTemplate = "revit://toolset/patterns/documentation",
        Name = "revit_toolset_patterns_documentation",
        Title = "Documentation Patterns",
        MimeType = "text/markdown")]
    [Description("Sheet package workflow: views, sheets, viewports, templates, and export.")]
    public static string GetDocumentationPatterns() => EmbeddedContent.PatternsDocumentation;

    [McpServerResource(
        UriTemplate = "revit://toolset/patterns/export",
        Name = "revit_toolset_patterns_export",
        Title = "Export Patterns",
        MimeType = "text/markdown")]
    [Description("Export options: PDF/image config, path conventions, and batch patterns.")]
    public static string GetExportPatterns() => EmbeddedContent.PatternsExport;

    [McpServerResource(
        UriTemplate = "revit://toolset/errors",
        Name = "revit_toolset_errors",
        Title = "Tool Errors",
        MimeType = "text/markdown")]
    [Description("Standard ToolError codes with meaning, examples, and recovery actions.")]
    public static string GetErrors() => EmbeddedContent.Errors;

    [McpServerResource(
        UriTemplate = "revit://toolset/units",
        Name = "revit_toolset_units",
        Title = "Units Reference",
        MimeType = "text/markdown")]
    [Description("Unit conversion reference: feet, mm, m, display vs internal units.")]
    public static string GetUnits() => EmbeddedContent.Units;
}
