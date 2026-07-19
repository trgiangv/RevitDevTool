using System.Runtime.CompilerServices;
using System.Text.Json;
using DevTools.Execution.External.Mcp.BuiltIn;
using DevTools.Presentation.Formatting;
using ModelContextProtocol.Server;

namespace RevitDevTool.Server.Tests;

public sealed class McpSchemaSummaryFormatterTests
{
    [Fact]
    public void ExecutePythonCode_NullableDescription_FormatsWithoutThrowing()
    {
        var method = typeof(PythonCodeTool).GetMethod(nameof(PythonCodeTool.ExecutePythonAsync))!;
        var tool = McpServerTool.Create(method, RuntimeHelpers.GetUninitializedObject(typeof(PythonCodeTool)));

        var summary = McpSchemaSummaryFormatter.Format(tool.ProtocolTool.InputSchema);

        Assert.Contains("- code:", summary);
        Assert.Contains("(string)", summary);
        Assert.Contains("- description:", summary);
        Assert.Contains("(string | null)", summary);
    }

    [Theory]
    [InlineData("{\"properties\":{\"value\":{\"type\":\"string\"}}}", "string")]
    [InlineData("{\"properties\":{\"value\":{\"type\":[\"string\",\"null\"]}}}", "string | null")]
    [InlineData("{\"properties\":{\"value\":{\"anyOf\":[{\"type\":\"integer\"},{\"type\":\"null\"}]}}}", "integer | null")]
    [InlineData("{\"properties\":{\"value\":{\"oneOf\":[{\"type\":\"string\"},{\"type\":\"string\"},{\"type\":\"number\"}]}}}", "string | number")]
    [InlineData("{\"properties\":{\"value\":{\"type\":{}}}}", "any")]
    [InlineData("{\"properties\":{\"value\":{\"title\":[],\"description\":false}}}", "any")]
    public void Format_HandlesSupportedAndMalformedPropertyShapes(string json, string expectedType)
    {
        using var document = JsonDocument.Parse(json);

        var summary = McpSchemaSummaryFormatter.Format(document.RootElement);

        Assert.Contains($"- value: value ({expectedType})", summary);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("{\"properties\":[]}")]
    [InlineData("{\"type\":\"object\"}")]
    public void Format_NonPropertySchemas_ReturnEmpty(string json)
    {
        using var document = JsonDocument.Parse(json);
        Assert.Equal(string.Empty, McpSchemaSummaryFormatter.Format(document.RootElement));
    }

    [Fact]
    public void RegistryView_UsesObservedLoadedHandler()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "source", "DevTools.Presentation", "Views", "McpRegistryView.xaml.cs"));

        Assert.DoesNotContain("Dispatcher.BeginInvoke", source, StringComparison.Ordinal);
        Assert.Contains("Loaded += OnLoaded", source, StringComparison.Ordinal);
        Assert.Contains("Loaded -= OnLoaded", source, StringComparison.Ordinal);
        Assert.Contains("await viewModel.InitializeAsync()", source, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
            if (File.Exists(Path.Combine(current.FullName, "RevitDevTool.slnx")))
                return current.FullName;
        throw new DirectoryNotFoundException("RevitDevTool.slnx was not found.");
    }
}
