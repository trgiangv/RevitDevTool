using System.Text.Json;
using DevTools.Execution.External.Mcp.Backends;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Catalog.Tests;

[TestClass]
public sealed class PythonResourceResultParserTests
{
    private const string ResourceUri = "revit://model/worksets";

    [TestMethod]
    public void ParseReadResourceResult_SdkJson_RoundTrips()
    {
        var expected = new ReadResourceResult
        {
            Contents =
            [
                new TextResourceContents
                {
                    Uri = ResourceUri,
                    MimeType = "text/plain",
                    Text = "ok",
                },
            ],
        };
        var json = JsonSerializer.Serialize(expected, McpJsonUtilities.DefaultOptions);

        var actual = PythonMcpToolBackend.ReadResourceResult(json);
        Assert.HasCount(1, actual.Contents);
        var text = Assert.IsInstanceOfType<TextResourceContents>(actual.Contents[0]);

        Assert.AreEqual("ok", text.Text);
        Assert.AreEqual("text/plain", text.MimeType);
        Assert.AreEqual(ResourceUri, text.Uri);
    }

    [TestMethod]
    public void ParseReadResourceResult_HelperContentShape_Throws()
    {
        const string json = """{"contents":[{"content":"hello","mime_type":"text/plain"}]}""";

        var ex = Assert.ThrowsExactly<InvalidOperationException>(
            () => PythonMcpToolBackend.ReadResourceResult(json));
        Assert.Contains("resource", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void ParseReadResourceResult_StringContentEntry_Throws()
    {
        const string json = """{"contents":["hello"]}""";

        Assert.ThrowsExactly<InvalidOperationException>(
            () => PythonMcpToolBackend.ReadResourceResult(json));
    }

    [TestMethod]
    public void ParseReadResourceResult_InputRequired_ThrowsWithRequestsAndState()
    {
        var inputRequired = new InputRequiredResult
        {
            InputRequests = new Dictionary<string, InputRequest>
            {
                ["confirm"] = InputRequest.ForElicitation(new ElicitRequestParams { Message = "Continue?" }),
            },
            RequestState = "resource-round-1",
        };
        var json = JsonSerializer.Serialize(inputRequired, McpJsonUtilities.DefaultOptions);

        var ex = Assert.ThrowsExactly<InputRequiredException>(() =>
            PythonMcpToolBackend.ReadResourceResult(json));

        Assert.IsNotNull(ex.Result.InputRequests);
        Assert.Contains("confirm", ex.Result.InputRequests!.Keys);
        Assert.AreEqual("resource-round-1", ex.Result.RequestState);
        Assert.AreEqual("input_required", ex.Result.ResultType);
    }
}
