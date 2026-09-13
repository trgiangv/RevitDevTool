using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using DevTools.Mcp.Core.Utils;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Core.Tests;

[TestClass]
public sealed class ToolHelpersTests
{
    [TestMethod]
    public void RuntimeJsonOptions_UsesCamelCaseAndReflectionResolver()
    {
        var options = ToolHelpers.RuntimeJsonOptions;

        Assert.AreSame(JsonNamingPolicy.CamelCase, options.PropertyNamingPolicy);
        Assert.IsNotNull(options.TypeInfoResolver);
    }

    [TestMethod]
    public void ErrorResult_String_SetsIsErrorAndTextContent()
    {
        var result = ToolHelpers.ErrorResult("boom");

        Assert.IsTrue(result.IsError);
        var text = Assert.IsInstanceOfType<TextContentBlock>(result.Content.Single()).Text;
        Assert.AreEqual("boom", text);
    }

    [TestMethod]
    public void ErrorResult_Generic_SerializesPayload()
    {
        var result = ToolHelpers.ErrorResult(new { code = 42 });

        Assert.IsTrue(result.IsError);
        var text = Assert.IsInstanceOfType<TextContentBlock>(result.Content.Single()).Text;
        Assert.Contains("\"code\":42", text, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Result_String_WrapsPlainText()
    {
        var result = ToolHelpers.Result("hello");

        Assert.IsNull(result.IsError);
        Assert.AreEqual("hello", Assert.IsInstanceOfType<TextContentBlock>(result.Content.Single()).Text);
    }

    [TestMethod]
    public void Result_Generic_SerializesPayload()
    {
        var result = ToolHelpers.Result(new { ok = true });

        var text = Assert.IsInstanceOfType<TextContentBlock>(result.Content.Single()).Text;
        Assert.Contains("\"ok\":true", text, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Result_WithTypeInfo_UsesProvidedSerializer()
    {
        var typeInfo = (JsonTypeInfo<string>)ToolHelpers.ProtocolOptions.GetTypeInfo(typeof(string))!;
        var result = ToolHelpers.Result("typed", typeInfo);

        Assert.AreEqual("\"typed\"", Assert.IsInstanceOfType<TextContentBlock>(result.Content.Single()).Text);
    }

    [TestMethod]
    public void ImageResult_EncodesBinaryContent()
    {
        var bytes = new byte[] { 0x01, 0x02, 0x03 };
        var result = ToolHelpers.ImageResult(bytes, "image/png");

        var image = Assert.IsInstanceOfType<ImageContentBlock>(result.Content.Single());
        Assert.AreEqual("image/png", image.MimeType);
        Assert.AreSequenceEqual(bytes, image.DecodedData.ToArray());
    }

    [TestMethod]
    public void Serialize_Null_UsesDeclaredType()
    {
        string? value = null;
        Assert.AreEqual("null", ToolHelpers.Serialize(value));
    }

    [TestMethod]
    public void Serialize_WithExplicitType_UsesTypeArgument()
    {
        object value = 7;
        var json = ToolHelpers.Serialize(value, typeof(int));
        Assert.AreEqual("7", json);
    }

    [TestMethod]
    public void ToElement_Null_ReturnsNullElement()
    {
        string? value = null;
        var element = ToolHelpers.ToElement(value);
        Assert.AreEqual(JsonValueKind.Null, element.ValueKind);
    }

    [TestMethod]
    public void ToElement_DerivedInstance_PreservesRuntimeShape()
    {
        object value = new { baseField = "base", extra = "extra" };
        var element = ToolHelpers.ToElement(value);

        Assert.AreEqual("extra", element.GetProperty("extra").GetString());
        Assert.AreEqual("base", element.GetProperty("baseField").GetString());
    }
}
