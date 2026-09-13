using System.Text.Json;
using DevTools.Mcp.Catalog.Discovery;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Catalog.Tests;

[TestClass]
public sealed class AlcInputRequiredBridgeTests
{
    [TestMethod]
    public void IsIsolatedInputRequired_DetectsIdentityMismatch()
    {
        var foreign = new ForeignMrtr.InputRequiredException("state-1");
        Assert.IsTrue(ToolsetMrtrBridge.IsIsolatedInputRequired(foreign));
        Assert.IsFalse(ToolsetMrtrBridge.IsIsolatedInputRequired(
            new InputRequiredException(requestState: "host")));
    }

    [TestMethod]
    public void ToHostException_MapsRequestState()
    {
        var foreign = new ForeignMrtr.InputRequiredException("demo-round1");
        var host = ToolsetMrtrBridge.ToHostException(foreign);
        Assert.AreEqual("demo-round1", host.Result.RequestState);
    }

    [TestMethod]
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
        Assert.AreEqual("demo-round1", host.Result.RequestState);
        Assert.IsNotNull(host.Result.InputRequests);
        Assert.IsTrue(host.Result.InputRequests!.ContainsKey("confirm"));
        Assert.AreEqual("elicitation/create", host.Result.InputRequests["confirm"].Method);
        Assert.AreEqual("Confirm?", host.Result.InputRequests["confirm"].ElicitationParams?.Message);
    }

    [TestMethod]
    public void ToInputRequiredResponse_SetsInputRequiredField()
    {
        var original = new InputRequiredException(requestState: "field-round1");
        var response = ToolsetMrtrBridge.ToInputRequiredResponse(original);
        Assert.AreEqual("field-round1", response.InputRequired?.RequestState);
        Assert.IsTrue(ToolsetMrtrBridge.TryGetInputRequiredResult(response, out var restored));
        Assert.AreEqual("field-round1", restored!.RequestState);
        Assert.IsNull(response.Meta);
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
