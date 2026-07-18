using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using DevTools.Execution.External.Mcp.BuiltIn;
using ModelContextProtocol.Server;

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

    private static McpServerTool CreateTool<T>(string methodName)
    {
        var method = typeof(T).GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(method);
        return McpServerTool.Create(method, RuntimeHelpers.GetUninitializedObject(typeof(T)));
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

    private static bool IsString(JsonElement property) =>
        property.TryGetProperty("type", out var type) &&
        (type.ValueKind == JsonValueKind.String && type.GetString() == "string" ||
         type.ValueKind == JsonValueKind.Array && type.EnumerateArray().Any(item => item.GetString() == "string")) ||
        property.TryGetProperty("anyOf", out var alternatives) && alternatives.EnumerateArray()
            .Any(option => option.TryGetProperty("type", out var optionType) && optionType.ValueKind == JsonValueKind.String && optionType.GetString() == "string");
}
