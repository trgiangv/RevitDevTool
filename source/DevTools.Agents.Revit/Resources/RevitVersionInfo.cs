using System.Text;
using DevTools.Mcp.BuiltIn;
using ModelContextProtocol.Protocol;
using RevitDevTool.Core;

namespace DevTools.Agents.Revit.Resources;

/// <summary>
/// Host version and API information.
/// Helps agents avoid deprecated API calls and use version-appropriate patterns.
/// </summary>
public sealed class RevitVersionInfo : IBuiltInMcpResource
{
    public string UriTemplate => "revit://version";

    public Resource ProtocolResource { get; } = new()
    {
        Uri = "revit://version",
        Name = "Revit Version Info",
        Description = "Host version, API version, runtime (.NET Framework or .NET 8+), and version-specific API notes.",
        MimeType = "text/markdown"
    };

    public ReadResourceResult Read(string uri)
    {
        var app = RevitContext.Application;
        var sb = new StringBuilder();

        sb.AppendLine("# Revit Version");
        sb.AppendLine($"- Product: {app.VersionName}");
        sb.AppendLine($"- Build: {app.VersionBuild}");
        sb.AppendLine($"- Number: {app.VersionNumber}");
        sb.AppendLine($"- Language: {app.Language}");
        sb.AppendLine();

        var versionNumber = app.VersionNumber ?? "";
        var versionYear = versionNumber.Length >= 4 && int.TryParse(versionNumber.Substring(0, 4), out var y) ? y : 0;
        var runtime = versionYear >= 2025 ? ".NET 8+ (net8.0-windows)" : ".NET Framework 4.8 (net48)";
        sb.AppendLine("## Runtime");
        sb.AppendLine($"- Framework: {runtime}");
        sb.AppendLine();

        sb.AppendLine("## API Version Notes");
        if (versionYear >= 2025)
        {
            sb.AppendLine("- Use `ElementId.Value` (long) — `IntegerValue` is obsolete");
            sb.AppendLine("- `ForgeTypeId` replaces `UnitType` and `DisplayUnitType`");
            sb.AppendLine("- `UnitUtils.ConvertToInternalUnits(value, UnitTypeId.Millimeters)`");
        }
        else
        {
            sb.AppendLine("- Use `ElementId.IntegerValue` (int)");
            sb.AppendLine("- `UnitType` / `DisplayUnitType` enums still valid");
            sb.AppendLine("- `UnitUtils.ConvertToInternalUnits(value, DisplayUnitType.DUT_MILLIMETERS)`");
        }

        if (versionYear >= 2024)
        {
            sb.AppendLine("- `Document.GetWarnings()` available");
            sb.AppendLine("- `FailureMessage.GetFailureDefinitionId()` returns `FailureDefinitionId`");
        }

        if (versionYear >= 2022)
        {
            sb.AppendLine("- `FilteredElementCollector` supports `.GetElementCount()`");
            sb.AppendLine("- `Wall.Create` overload with all parameters available");
        }
        sb.AppendLine();

        return new ReadResourceResult
        {
            Contents = [new TextResourceContents { Uri = uri, MimeType = "text/markdown", Text = sb.ToString() }]
        };
    }
}
