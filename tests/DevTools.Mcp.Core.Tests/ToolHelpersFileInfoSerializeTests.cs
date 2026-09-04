using System.Text.Json;
using DevTools.FileMetadata.Acad;
using DevTools.FileMetadata.Core;
using DevTools.FileMetadata.Revit;
using DevTools.Hosting;
using DevTools.Mcp.Core.Utils;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Core.Tests;

public sealed class ToolHelpersFileInfoSerializeTests
{
    [Fact]
    public void ToolHelpers_Serialize_PreservesDerivedFileInfoFieldsWhenDeclaredAsBase()
    {
        FileInfoResult result = new RevitFileInfoSummaryResult
        {
            HostApplication = HostApp.Revit,
            FilePath = @"C:\sample.rvt",
            FileName = "sample.rvt",
            BasicInfo = new RevitBasicInfoSummary
            {
                FileVersion = 1,
                RevitVersion = "2025",
                IsWorkshared = false,
                WorksharingType = "Not enabled",
                Locale = "ENU"
            },
            ProjectTitle = "Demo",
            WorksetCount = 3,
            ExternalReferenceCount = 1
        };

        var json = ToolHelpers.Serialize(result);
        var toolResult = ToolHelpers.Result(result);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(toolResult.Content)).Text;

        Assert.Contains("\"hostApp\":\"Revit\"", json, StringComparison.Ordinal);
        Assert.Contains("\"basicInfo\"", json, StringComparison.Ordinal);
        Assert.Contains("\"revitVersion\":\"2025\"", json, StringComparison.Ordinal);
        Assert.Contains("\"worksetCount\":3", json, StringComparison.Ordinal);
        Assert.Contains("\"projectTitle\":\"Demo\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\"basicInfo\":null", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"hostApp\":\"Civil3D\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ToolHelpers_Serialize_PreservesAutoCadHostAppWireNameForDwg()
    {
        FileInfoResult result = new DwgFileInfoSummaryResult
        {
            HostApplication = HostApp.AutoCad,
            FilePath = @"C:\sample.dwg",
            FileName = "sample.dwg",
            AcadVersion = "AC1032",
            Title = "Demo",
            LayerCount = 2,
            BlockCount = 1
        };

        var json = ToolHelpers.Serialize(result);

        Assert.Contains("\"hostApp\":\"AutoCad\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"hostApp\":\"Civil3D\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"hostApp\":\"Plant3D\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"hostApp\":\"AcadMep\"", json, StringComparison.Ordinal);
        Assert.Contains("\"acadVersion\":\"AC1032\"", json, StringComparison.Ordinal);
    }
}
