extern alias AcadAgents;
extern alias RevitAgents;

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using DevTools.Execution.External.Mcp.BuiltIn;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using AcadNavigateHistoryTool = AcadAgents::DevTools.Agents.Acad.Tools.NavigateHistoryTool;
using RevitNavigateHistoryTool = RevitAgents::DevTools.Agents.Revit.Tools.NavigateHistoryTool;

namespace RevitDevTool.Server.Tests;

public sealed class BuiltInPrimitiveSchemaTests
{
    [Fact]
    public void ExecuteCSharpCode_HasSdkGeneratedTypedSchema()
    {
        using var schema = JsonDocument.Parse(CreateTool<CSharpCodeTool>(nameof(CSharpCodeTool.ExecuteAsync)).ProtocolTool.InputSchema.GetRawText());

        AssertRequiredString(schema, "code");
    }

    [Fact]
    public void ExecutePythonCode_HasSdkGeneratedTypedSchema()
    {
        using var schema = JsonDocument.Parse(CreateTool<PythonCodeTool>(nameof(PythonCodeTool.ExecutePythonAsync)).ProtocolTool.InputSchema.GetRawText());

        AssertRequiredString(schema, "code");
        AssertOptionalString(schema, "description");
    }

    [Fact]
    public void OpenDocument_HasSdkGeneratedTypedSchema()
    {
        using var schema = JsonDocument.Parse(CreateTool<OpenDocumentTool>(nameof(OpenDocumentTool.OpenDocumentAsync)).ProtocolTool.InputSchema.GetRawText());

        AssertRequiredString(schema, "filePath");
    }

    [Fact]
    public void RevitNavigateHistory_HasSdkGeneratedTypedSchema()
    {
        using var schema = JsonDocument.Parse(CreateTool(typeof(RevitNavigateHistoryTool), nameof(RevitNavigateHistoryTool.NavigateHistoryAsync)).ProtocolTool.InputSchema.GetRawText());

        AssertRequiredString(schema, "direction");
        AssertOptionalIntegerWithDefault(schema, "steps", 1);
    }

    [Fact]
    public void AcadNavigateHistory_HasSdkGeneratedTypedSchema()
    {
        using var schema = JsonDocument.Parse(CreateTool(typeof(AcadNavigateHistoryTool), nameof(AcadNavigateHistoryTool.NavigateHistoryAsync)).ProtocolTool.InputSchema.GetRawText());

        AssertRequiredString(schema, "direction");
        AssertOptionalIntegerWithDefault(schema, "steps", 1);
    }

    [Fact]
    public async Task RevitNavigateHistory_InvalidDirectionAndStepsThrowMcpException()
    {
        var tool = new RevitNavigateHistoryTool(null!);

        await Assert.ThrowsAsync<McpException>(() => tool.NavigateHistoryAsync("sideways", cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<McpException>(() => tool.NavigateHistoryAsync("back", 0, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AcadNavigateHistory_InvalidDirectionAndStepsThrowMcpException()
    {
        var tool = new AcadNavigateHistoryTool(null!);

        await Assert.ThrowsAsync<McpException>(() => tool.NavigateHistoryAsync("sideways", cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<McpException>(() => tool.NavigateHistoryAsync("back", 0, TestContext.Current.CancellationToken));
    }

    private static McpServerTool CreateTool<T>(string methodName)
    {
        return CreateTool(typeof(T), methodName);
    }

    private static McpServerTool CreateTool(Type toolType, string methodName)
    {
        var method = toolType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(method);
        return McpServerTool.Create(method, RuntimeHelpers.GetUninitializedObject(toolType));
    }

    private static void AssertRequiredString(JsonDocument schema, string propertyName)
    {
        var properties = schema.RootElement.GetProperty("properties");
        Assert.True(properties.TryGetProperty(propertyName, out var property));
        Assert.True(IsString(property));
        Assert.Contains(propertyName, schema.RootElement.GetProperty("required").EnumerateArray().Select(item => item.GetString()));
    }

    private static void AssertOptionalString(JsonDocument schema, string propertyName)
    {
        var properties = schema.RootElement.GetProperty("properties");
        Assert.True(properties.TryGetProperty(propertyName, out var property));
        Assert.True(IsString(property));
        Assert.DoesNotContain(propertyName, schema.RootElement.GetProperty("required").EnumerateArray().Select(item => item.GetString()));
    }

    private static void AssertOptionalIntegerWithDefault(JsonDocument schema, string propertyName, int expectedDefault)
    {
        var properties = schema.RootElement.GetProperty("properties");
        Assert.True(properties.TryGetProperty(propertyName, out var property));
        Assert.Equal("integer", property.GetProperty("type").GetString());
        Assert.Equal(expectedDefault, property.GetProperty("default").GetInt32());
        Assert.DoesNotContain(propertyName, schema.RootElement.GetProperty("required").EnumerateArray().Select(item => item.GetString()));
    }

    private static bool IsString(JsonElement property) =>
        property.TryGetProperty("type", out var type) &&
        (type.ValueKind == JsonValueKind.String && type.GetString() == "string" ||
         type.ValueKind == JsonValueKind.Array && type.EnumerateArray().Any(item => item.GetString() == "string")) ||
        property.TryGetProperty("anyOf", out var alternatives) && alternatives.EnumerateArray()
            .Any(option => option.TryGetProperty("type", out var optionType) && optionType.ValueKind == JsonValueKind.String && optionType.GetString() == "string");
}
