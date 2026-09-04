using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using DevTools.Mcp.Core.Utils;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Core.Tests;

public sealed class ToolHelpersTests
{
    [Fact]
    public void RuntimeJsonOptions_UsesCamelCaseAndReflectionResolver()
    {
        var options = ToolHelpers.RuntimeJsonOptions;

        Assert.Same(JsonNamingPolicy.CamelCase, options.PropertyNamingPolicy);
        Assert.NotNull(options.TypeInfoResolver);
    }

    [Fact]
    public void ErrorResult_String_SetsIsErrorAndTextContent()
    {
        var result = ToolHelpers.ErrorResult("boom");

        Assert.True(result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Equal("boom", text);
    }

    [Fact]
    public void ErrorResult_Generic_SerializesPayload()
    {
        var result = ToolHelpers.ErrorResult(new { code = 42 });

        Assert.True(result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("\"code\":42", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Result_String_WrapsPlainText()
    {
        var result = ToolHelpers.Result("hello");

        Assert.Null(result.IsError);
        Assert.Equal("hello", Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text);
    }

    [Fact]
    public void Result_Generic_SerializesPayload()
    {
        var result = ToolHelpers.Result(new { ok = true });

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("\"ok\":true", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Result_WithTypeInfo_UsesProvidedSerializer()
    {
        var typeInfo = (JsonTypeInfo<string>)ToolHelpers.ProtocolOptions.GetTypeInfo(typeof(string))!;
        var result = ToolHelpers.Result("typed", typeInfo);

        Assert.Equal("\"typed\"", Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text);
    }

    [Fact]
    public void ImageResult_EncodesBinaryContent()
    {
        var bytes = new byte[] { 0x01, 0x02, 0x03 };
        var result = ToolHelpers.ImageResult(bytes, "image/png");

        var image = Assert.IsType<ImageContentBlock>(Assert.Single(result.Content));
        Assert.Equal("image/png", image.MimeType);
        Assert.Equal(bytes, image.DecodedData.ToArray());
    }

    [Fact]
    public void Serialize_Null_UsesDeclaredType()
    {
        string? value = null;
        Assert.Equal("null", ToolHelpers.Serialize(value));
    }

    [Fact]
    public void Serialize_WithExplicitType_UsesTypeArgument()
    {
        object value = 7;
        var json = ToolHelpers.Serialize(value, typeof(int));
        Assert.Equal("7", json);
    }

    [Fact]
    public void ToElement_Null_ReturnsNullElement()
    {
        string? value = null;
        var element = ToolHelpers.ToElement(value);
        Assert.Equal(JsonValueKind.Null, element.ValueKind);
    }

    [Fact]
    public void ToElement_DerivedInstance_PreservesRuntimeShape()
    {
        object value = new { baseField = "base", extra = "extra" };
        var element = ToolHelpers.ToElement(value);

        Assert.Equal("extra", element.GetProperty("extra").GetString());
        Assert.Equal("base", element.GetProperty("baseField").GetString());
    }
}
