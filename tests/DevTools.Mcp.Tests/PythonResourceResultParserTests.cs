using System.Text.Json;
using DevTools.Mcp.Adapter.Execution;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Tests;

public sealed class PythonResourceResultParserTests
{
    private const string ResourceUri = "revit://model/worksets";

    [Fact]
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

        var actual = PythonResultParser.ParseReadResourceResult(json);
        var text = Assert.IsType<TextResourceContents>(Assert.Single(actual.Contents));

        Assert.Equal("ok", text.Text);
        Assert.Equal("text/plain", text.MimeType);
        Assert.Equal(ResourceUri, text.Uri);
    }

    [Fact]
    public void ParseReadResourceResult_HelperContentShape_Throws()
    {
        const string json = """{"contents":[{"content":"hello","mime_type":"text/plain"}]}""";

        var ex = Assert.Throws<InvalidOperationException>(
            () => PythonResultParser.ParseReadResourceResult(json));
        Assert.Contains("resource", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseReadResourceResult_StringContentEntry_Throws()
    {
        const string json = """{"contents":["hello"]}""";

        Assert.Throws<InvalidOperationException>(
            () => PythonResultParser.ParseReadResourceResult(json));
    }

    [Fact]
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

        var ex = Assert.Throws<InputRequiredException>(() =>
            PythonResultParser.ParseReadResourceResult(json));

        Assert.NotNull(ex.Result.InputRequests);
        Assert.Contains("confirm", ex.Result.InputRequests!.Keys);
        Assert.Equal("resource-round-1", ex.Result.RequestState);
        Assert.Equal("input_required", ex.Result.ResultType);
    }
}
