using DevTools.Daemon.Mcp.Contracts;
using DevTools.Daemon.Mcp.Processes;
using DevTools.Mcp.Server.Tests.Harness;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Server.Tests;

using System.Linq;

/// <summary>Consolidated invoke_dynamic pass-through tests using SDK-aligned mock harness (no live host).</summary>
[TestClass]
public sealed class InvokeDynamicSdkHarnessTests
{
    [TestMethod]
    [DataRow(McpToolBehavior.PlainText, "called:plain_tool")]
    [DataRow(McpToolBehavior.StructuredFind, "Found 3 elements")]
    public async Task InvokeDynamic_PassThroughToolBehaviors(McpToolBehavior behavior, string expectedTextFragment)
    {
        const string toolName = "plain_tool";
        var harness = McpSdkTestHarness.ForTool(toolName, behavior);
        var id = await harness.SearchFirstId(new { query = toolName });

        var result = await harness.InvokeId(id, new { category = "Walls" });

        Assert.Contains(expectedTextFragment, McpToolInvoke.Text(result), StringComparison.Ordinal);
        Assert.AreEqual(1, harness.Session.PassthroughCount);
        Assert.AreEqual(101, harness.Session.ProcessId);
    }

    [TestMethod]
    public async Task InvokeDynamic_PassThroughHostImageContentBlock()
    {
        const string toolName = "view_screenshot";
        var harness = McpSdkTestHarness.ForTool(toolName, McpToolBehavior.ImagePng);
        var id = await harness.SearchFirstId(new { query = toolName });

        var result = await harness.InvokeId(id);
        var image = Assert.IsInstanceOfType<ImageContentBlock>(Enumerable.Single(result.Content));

        Assert.AreEqual("image/png", image.MimeType);
        Assert.AreSequenceEqual(new byte[] { 1, 2, 3 }, image.DecodedData.ToArray());
    }

    [TestMethod]
    public async Task InvokeDynamic_PassThroughPreservesIsErrorMetaStructuredContent()
    {
        const string toolName = "failing_tool";
        var harness = McpSdkTestHarness.ForTool(toolName, McpToolBehavior.ErrorWithMeta);
        var id = await harness.SearchFirstId(new { query = "failing" });

        var result = await harness.InvokeId(id);

        Assert.IsTrue(result.IsError);
        Assert.AreEqual("meta", result.Meta!["response"]!.GetValue<string>());
        Assert.AreEqual("{\"ok\":false}", result.StructuredContent!.Value.GetRawText());
        Assert.AreEqual("tool failed", McpToolInvoke.Text(result));
    }

    [TestMethod]
    public async Task InvokeDynamic_PassThroughMixedTextAndImageContent()
    {
        const string toolName = "mixed_tool";
        var harness = McpSdkTestHarness.ForTool(toolName, McpToolBehavior.MixedTextAndImage);
        var id = await harness.SearchFirstId(new { query = "mixed" });

        var result = await harness.InvokeId(id);

        Assert.AreEqual(2, result.Content.Count);
        Assert.AreEqual("screenshot attached", ((TextContentBlock)result.Content[0]).Text);
        var image = Assert.IsInstanceOfType<ImageContentBlock>(result.Content[1]);
        Assert.AreSequenceEqual(new byte[] { 4, 5, 6 }, image.DecodedData.ToArray());
    }

    [TestMethod]
    public async Task InvokeDynamic_StructuredOutput_PreservesHostPayloadWithShortText()
    {
        const string toolName = "revit_find_elements";
        var harness = McpSdkTestHarness.ForTool(toolName, McpToolBehavior.StructuredFind);
        var id = await harness.SearchFirstId(new { query = "find" });

        var result = await harness.InvokeId(id, new { category = "Walls" });

        Assert.AreEqual(240, result.StructuredContent!.Value.GetProperty("totalCount").GetInt32());
        Assert.IsTrue(result.StructuredContent.Value.GetProperty("hasMore").GetBoolean());
        var text = McpToolInvoke.Text(result);
        Assert.Contains("Found 3 elements", text, StringComparison.Ordinal);
        Assert.IsTrue(text.Length < 120);
    }

    [TestMethod]
    public async Task InvokeDynamic_ForwardsHostInputRequired_AndWrapsRequestState()
    {
        const string toolName = "mrtr_confirm";
        var harness = McpSdkTestHarness.ForTool(toolName, McpToolBehavior.MrtrElicitationConfirm);
        var id = await harness.SearchFirstId(new { query = toolName });

        var ex = await harness.InvokeExpectingInputRequired(id);

        Assert.IsNotNull(ex.Result.InputRequests);
        Assert.Contains("confirm", ex.Result.InputRequests!.Keys);
        Assert.IsNotNull(ex.Result.RequestState);
        Assert.Contains(id, ex.Result.RequestState!, StringComparison.Ordinal);
        Assert.AreEqual(1, harness.Session.PassthroughCount);
    }

    [TestMethod]
    public async Task InvokeDynamic_MrtrRetry_ForwardsInputResponsesAndHostRequestState()
    {
        const string toolName = "mrtr_confirm";
        var harness = McpSdkTestHarness.ForTool(toolName, McpToolBehavior.MrtrElicitationConfirm);
        var id = await harness.SearchFirstId(new { query = toolName });

        var first = await harness.InvokeExpectingInputRequired(id);
        var result = await harness.InvokeMrtrRetry(
            id,
            first,
            new Dictionary<string, object> { ["confirm"] = new { action = "accept" } });

        Assert.AreEqual("confirmed", McpToolInvoke.Text(result));
        Assert.AreEqual(2, harness.Session.PassthroughCount);
    }

    [TestMethod]
    public async Task InvokeDynamic_StaleLocator_RequiresResearchBeforeExecution()
    {
        var harness = McpSdkTestHarness.Create();
        var oldId = await harness.SearchFirstId(new { query = "find" });
        harness.ReplaceCatalog(McpSdkCatalogOptions.Default with { BumpToolSchema = true });

        var response = McpToolInvoke.Parse<InvokeResponse>(
            await harness.InvokeDynamic(new { id = oldId }));

        Assert.IsFalse(response.Ok);
        Assert.IsFalse(response.ExecutionStarted);
        Assert.IsTrue(response.Error!.Retryable);
        Assert.AreEqual("changed", response.Error.Reason);
        Assert.AreEqual("research_then_reinvoke", response.Error.Retry);
        Assert.AreEqual(0, harness.Session.PassthroughCount);
    }

    [TestMethod]
    public async Task InvokeDynamic_BatchReadsFixedAndTemplateResources()
    {
        var harness = McpSdkTestHarness.Create(McpSdkCatalogOptions.WithTemplates());
        var capabilities = await harness.Search(new { kinds = new[] { "resource", "resource_template" } });
        var resourceId = capabilities.Items.Single(item => item.Kind == CatalogType.Resource).Id;
        var templateId = capabilities.Items.First(item => item.Kind == CatalogType.ResourceTemplate && item.Target.Contains("element", StringComparison.Ordinal)).Id;

        var response = McpToolInvoke.Parse<InvokeResponse>(await harness.InvokeDynamic(new
        {
            reads = new object[]
            {
                new { id = resourceId },
                new { id = templateId, arguments = new { elementId = 99 } },
            },
        }));

        Assert.AreEqual(2, response.Results!.Count);
        Assert.IsTrue(response.Results[0].Ok);
        Assert.IsTrue(response.Results[1].Ok);
        Assert.AreEqual(1, harness.Session.ReadCount);
        Assert.AreEqual(1, harness.Session.TemplateReadCount);
    }

    [TestMethod]
    public async Task InvokeDynamic_BatchRejectsToolReadsAndOverLimit()
    {
        var harness = McpSdkTestHarness.Create(McpSdkCatalogOptions.WithTemplates());
        var capabilities = await harness.Search(new { });
        var toolId = capabilities.Items.Single(item => item.Kind == CatalogType.Tool).Id;
        var resourceId = capabilities.Items.Single(item => item.Kind == CatalogType.Resource).Id;

        var mixed = await harness.InvokeDynamic(new { id = toolId, reads = new[] { new { id = resourceId } } });
        var toolRead = await harness.InvokeDynamic(new { reads = new[] { new { id = toolId } } });
        var tooMany = await harness.InvokeDynamic(new
        {
            reads = Enumerable.Range(0, 17).Select(_ => new { id = resourceId }).ToArray(),
        });

        Assert.Contains("cannot be combined", McpToolInvoke.Text(mixed), StringComparison.Ordinal);
        Assert.Contains("resources and resource templates only", McpToolInvoke.Text(toolRead), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("at most 16", McpToolInvoke.Text(tooMany), StringComparison.Ordinal);
        Assert.AreEqual(0, harness.Session.ReadCount);
    }
}
