using System.Text.Json;
using DevTools.Mcp.Catalog.Discovery;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Tests;

public sealed class AlcInputRequiredBridgeTests
{
    [Fact]
    public void IsForeignInputRequired_DetectsNameMismatch()
    {
        var foreign = new ForeignMrtr.InputRequiredException("state-1");
        Assert.True(ToolsetMrtrBridge.IsForeignInputRequired(foreign));
        Assert.False(ToolsetMrtrBridge.IsForeignInputRequired(
            new InputRequiredException(requestState: "host")));
    }

    [Fact]
    public void ToHostException_MapsRequestState()
    {
        var foreign = new ForeignMrtr.InputRequiredException("demo-round1");
        var host = ToolsetMrtrBridge.ToHostException(foreign);
        Assert.Equal("demo-round1", host.Result.RequestState);
    }

    [Fact]
    public void ToHostException_MapsElicitationInputRequests()
    {
        var elicitParams = JsonSerializer.SerializeToElement(new
        {
            message = "Confirm?",
            requestedSchema = new
            {
                type = "object",
                properties = new { ok = new { type = "boolean" } },
                required = new[] { "ok" },
            },
        });

        var foreign = new ForeignMrtr.InputRequiredException(
            requestState: "demo-round1",
            inputRequests: new Dictionary<string, ForeignMrtr.InputRequest>
            {
                ["confirm"] = new ForeignMrtr.InputRequest
                {
                    Method = "elicitation/create",
                    Params = elicitParams,
                },
            });

        var host = ToolsetMrtrBridge.ToHostException(foreign);
        Assert.Equal("demo-round1", host.Result.RequestState);
        Assert.NotNull(host.Result.InputRequests);
        Assert.True(host.Result.InputRequests!.ContainsKey("confirm"));
        Assert.Equal("elicitation/create", host.Result.InputRequests["confirm"].Method);
        Assert.Equal("Confirm?", host.Result.InputRequests["confirm"].ElicitationParams?.Message);
    }

    [Fact]
    public void ToInputRequiredResponse_RoundTripsThroughMeta()
    {
        var original = new InputRequiredException(requestState: "meta-round1");
        var response = ToolsetMrtrBridge.ToInputRequiredResponse(original);
        Assert.True(ToolsetMrtrBridge.TryGetInputRequiredResult(response, out var restored));
        Assert.Equal("meta-round1", restored!.RequestState);
    }
}

file static class ForeignMrtr
{
    public sealed class InputRequiredException : Exception
    {
        public InputRequiredException(string requestState, IDictionary<string, InputRequest>? inputRequests = null)
            : base("foreign input required")
        {
            Result = new InputRequiredResult
            {
                RequestState = requestState,
                InputRequests = inputRequests,
            };
        }

        public InputRequiredResult Result { get; }
    }

    public sealed class InputRequiredResult
    {
        public string? RequestState { get; set; }
        public IDictionary<string, InputRequest>? InputRequests { get; set; }
    }

    public sealed class InputRequest
    {
        public string Method { get; set; } = "";
        public JsonElement? Params { get; set; }
    }
}
