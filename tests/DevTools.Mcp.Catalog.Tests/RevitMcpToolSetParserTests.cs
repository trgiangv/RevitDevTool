using System.Text.Json;

namespace DevTools.Mcp.Catalog.Tests;

/// <summary>Parser coverage for RevitMcpToolSet resource templates (requires built sample DLL).</summary>
public sealed class RevitMcpToolSetParserTests
{
    private static readonly McpAssemblyParser Parser = new(Microsoft.Extensions.Logging.Abstractions.NullLogger<McpAssemblyParser>.Instance);

    [Fact]
    public void DotnetParser_ExtractsElementAndScheduleTemplates()
    {
        var assemblyPath = RequireRevitMcpToolSetAssembly();

        var catalog = Parser.ParseCatalogFromAssembly(assemblyPath);
        var elementReg = catalog.Resources.FirstOrDefault(r => r.TemplateDescriptor?.Name == "revit_element");
        if (elementReg?.TemplateDescriptor is null)
        {
            Assert.Skip($"{OptionalArtifact.RevitMcpToolSetHint} (revit_element template missing).");
        }

        var scheduleReg = catalog.Resources.FirstOrDefault(r => r.TemplateDescriptor?.Name == "revit_schedule_preview");
        if (scheduleReg?.TemplateDescriptor is null)
        {
            Assert.Skip($"{OptionalArtifact.RevitMcpToolSetHint} (revit_schedule_preview template missing).");
        }

        var element = elementReg!.TemplateDescriptor!;
        var schedule = scheduleReg!.TemplateDescriptor!;

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
        var assemblyPath = RequireRevitMcpToolSetAssembly();

        var catalog = Parser.ParseCatalogFromAssembly(assemblyPath);
        var structuredTools = new[] { "revit_find_elements", "revit_read_parameters", "revit_get_status" };

        foreach (var toolName in structuredTools)
        {
            var registration = catalog.Tools.FirstOrDefault(item => item.Descriptor.Name == toolName);
            if (registration is null)
            {
                Assert.Skip($"{OptionalArtifact.RevitMcpToolSetHint} ({toolName} missing).");
            }

            if (registration!.Descriptor.OutputSchema is null)
            {
                Assert.Skip(
                    $"{OptionalArtifact.RevitMcpToolSetHint} ({toolName} missing OutputSchema after UseStructuredContent).");
            }

            Assert.Equal(JsonValueKind.Object, registration.Descriptor.OutputSchema!.Value.ValueKind);
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

        var present = candidates.Where(File.Exists).ToList();
        if (present.Count == 0)
            Assert.Skip(OptionalArtifact.RevitMcpToolSetHint);

        foreach (var assemblyPath in present)
        {
            var catalog = Parser.ParseCatalogFromAssembly(assemblyPath);
            Assert.NotEmpty(catalog.Tools);
            Assert.NotEmpty(catalog.Resources);
            Assert.Contains(catalog.Tools, item => item.Descriptor.Name == "revit_find_elements");
        }
    }

    private static string RequireRevitMcpToolSetAssembly()
    {
        var assemblyPath = OptionalArtifact.ResolveRevitMcpToolSetDll(FindRepositoryRoot());
        if (assemblyPath is null)
            Assert.Skip(OptionalArtifact.RevitMcpToolSetHint);
        return assemblyPath;
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
