using System.Text.Json;

namespace DevTools.Mcp.Tests;

/// <summary>Parser coverage for RevitMcpToolSet resource templates (requires built sample DLL).</summary>
public sealed class RevitMcpToolSetParserTests
{
    private static readonly McpAssemblyParser Parser = new(Microsoft.Extensions.Logging.Abstractions.NullLogger<McpAssemblyParser>.Instance);

    [Fact]
    public void DotnetParser_ExtractsElementAndScheduleTemplates()
    {
        var assemblyPath = FindRevitMcpToolSetAssembly();
        if (assemblyPath is null)
        {
            Assert.Fail("RevitMcpToolSet.dll not found. Build samples/RevitMcpToolSet (Debug.Autodesk.2025) before running this test.");
            return;
        }

        var catalog = Parser.ParseCatalogFromAssembly(assemblyPath);
        var elementReg = catalog.Resources.FirstOrDefault(r => r.TemplateDescriptor?.Name == "revit_element");
        if (elementReg?.TemplateDescriptor is null)
        {
            Assert.Fail("revit_element template not found in RevitMcpToolSet.dll. Rebuild samples/RevitMcpToolSet (Debug.Autodesk.2025).");
            return;
        }

        var scheduleReg = catalog.Resources.FirstOrDefault(r => r.TemplateDescriptor?.Name == "revit_schedule_preview");
        if (scheduleReg?.TemplateDescriptor is null)
        {
            Assert.Fail("revit_schedule_preview template not found in RevitMcpToolSet.dll. Rebuild samples/RevitMcpToolSet (Debug.Autodesk.2025).");
            return;
        }

        var element = elementReg.TemplateDescriptor!;
        var schedule = scheduleReg.TemplateDescriptor!;

        Assert.Equal("revit://element/{elementId}", element.UriTemplate);
        Assert.Equal("application/json", element.MimeType);

        Assert.Equal("revit://schedule/{scheduleId}/preview", schedule.UriTemplate);
        Assert.Equal("text/csv", schedule.MimeType);

        Assert.Contains("elementId", element.UriTemplate, StringComparison.Ordinal);
        Assert.Contains("scheduleId", schedule.UriTemplate, StringComparison.Ordinal);
    }

    [Fact]
    public void DotnetParser_StructuredOutputTools_HaveOutputSchema()
    {
        var assemblyPath = FindRevitMcpToolSetAssembly();
        if (assemblyPath is null)
        {
            Assert.Fail("RevitMcpToolSet.dll not found. Build samples/RevitMcpToolSet (Debug.Autodesk.2025) before running this test.");
            return;
        }

        var catalog = Parser.ParseCatalogFromAssembly(assemblyPath);
        var structuredTools = new[] { "revit_find_elements", "revit_read_parameters", "revit_get_status" };

        foreach (var toolName in structuredTools)
        {
            var registration = catalog.Tools.FirstOrDefault(item => item.Descriptor.Name == toolName);
            if (registration is null)
            {
                Assert.Fail($"{toolName} not found in RevitMcpToolSet.dll. Rebuild samples/RevitMcpToolSet (Debug.Autodesk.2025).");
                return;
            }

            if (registration.Descriptor.OutputSchema is null)
            {
                Assert.Fail(
                    $"{toolName} is missing OutputSchema in parsed catalog. Rebuild samples/RevitMcpToolSet (Debug.Autodesk.2025) after enabling UseStructuredContent.");
                return;
            }

            Assert.Equal(JsonValueKind.Object, registration.Descriptor.OutputSchema.Value.ValueKind);
        }
    }

    [Fact]
    public void DotnetParser_ParsesNet48AndNet8Toolsets_WhenBothBuildsArePresent()
    {
        var root = FindRepositoryRoot();
        var candidates = new[]
        {
            Path.Combine(root, "samples", "RevitMcpToolSet", "bin", "Debug.Autodesk.2024", "RevitMcpToolSet.dll"),
            Path.Combine(root, "samples", "RevitMcpToolSet", "bin", "Debug.Autodesk.2025", "RevitMcpToolSet.dll"),
        };

        foreach (var assemblyPath in candidates)
        {
            Assert.True(File.Exists(assemblyPath), $"Expected built toolset: {assemblyPath}");
            var catalog = Parser.ParseCatalogFromAssembly(assemblyPath);
            Assert.NotEmpty(catalog.Tools);
            Assert.NotEmpty(catalog.Resources);
            Assert.Contains(catalog.Tools, item => item.Descriptor.Name == "revit_find_elements");
        }
    }

    private static string? FindRevitMcpToolSetAssembly()
    {
        var root = FindRepositoryRoot();
        var candidates = new[]
        {
            Path.Combine(root, "samples", "RevitMcpToolSet", "bin", "Debug.Autodesk.2025", "RevitMcpToolSet.dll"),
            Path.Combine(root, "samples", "RevitMcpToolSet", "bin", "Debug", "net8.0", "RevitMcpToolSet.dll"),
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "RevitDevTool.slnx"))
                || File.Exists(Path.Combine(current.FullName, "RevitDevTool.sln")))
                return current.FullName;

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the RevitDevTool repository root.");
    }
}
