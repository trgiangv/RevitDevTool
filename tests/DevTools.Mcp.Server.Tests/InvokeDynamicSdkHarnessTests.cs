using DevTools.Mcp.Server.Contracts;
using DevTools.Mcp.Server.Tests.Harness;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Server.Tests;

/// <summary>Consolidated invoke_dynamic pass-through tests using SDK-aligned mock harness (no live host).</summary>
public sealed class InvokeDynamicSdkHarnessTests
{
    [Theory]
    [InlineData(McpToolBehavior.PlainText, "called:plain_tool")]
    [InlineData(McpToolBehavior.StructuredFind, "Found 3 elements")]
    public async Task InvokeDynamic_PassThroughToolBehaviors(McpToolBehavior behavior, string expectedTextFragment)
    {
        const string toolName = "plain_tool";
        var harness = McpSdkTestHarness.ForTool(toolName, behavior);
        var capabilityId = await harness.SearchFirstCapabilityId(new { query = toolName });

        var result = await harness.InvokeCapability(capabilityId, new { category = "Walls" });

        Assert.Contains(expectedTextFragment, McpToolInvoke.Text(result), StringComparison.Ordinal);
        Assert.Equal(1, harness.Session.PassthroughCount);
        Assert.Equal(harness.Session.Key, harness.Broker.RequestedHostKey);
    }

    [Fact]
    public async Task InvokeDynamic_PassThroughHostImageContentBlock()
    {
        const string toolName = "view_screenshot";
        var harness = McpSdkTestHarness.ForTool(toolName, McpToolBehavior.ImagePng);
        var capabilityId = await harness.SearchFirstCapabilityId(new { query = toolName });

        var result = await harness.InvokeCapability(capabilityId);
        var image = Assert.IsType<ImageContentBlock>(Assert.Single(result.Content));

        Assert.Equal("image/png", image.MimeType);
        Assert.Equal(new byte[] { 1, 2, 3 }, image.DecodedData.ToArray());
    }

    [Fact]
    public async Task InvokeDynamic_PassThroughPreservesIsErrorMetaStructuredContent()
    {
        const string toolName = "failing_tool";
        var harness = McpSdkTestHarness.ForTool(toolName, McpToolBehavior.ErrorWithMeta);
        var capabilityId = await harness.SearchFirstCapabilityId(new { query = "failing" });

        var result = await harness.InvokeCapability(capabilityId);

        Assert.True(result.IsError);
        Assert.Equal("meta", result.Meta!["response"]!.GetValue<string>());
        Assert.Equal("{\"ok\":false}", result.StructuredContent!.Value.GetRawText());
        Assert.Equal("tool failed", McpToolInvoke.Text(result));
    }

    [Fact]
    public async Task InvokeDynamic_PassThroughMixedTextAndImageContent()
    {
        const string toolName = "mixed_tool";
        var harness = McpSdkTestHarness.ForTool(toolName, McpToolBehavior.MixedTextAndImage);
        var capabilityId = await harness.SearchFirstCapabilityId(new { query = "mixed" });

        var result = await harness.InvokeCapability(capabilityId);

        Assert.Equal(2, result.Content.Count);
        Assert.Equal("screenshot attached", ((TextContentBlock)result.Content[0]).Text);
        var image = Assert.IsType<ImageContentBlock>(result.Content[1]);
        Assert.Equal(new byte[] { 4, 5, 6 }, image.DecodedData.ToArray());
    }

    [Fact]
    public async Task InvokeDynamic_StructuredOutput_PreservesHostPayloadWithShortText()
    {
        const string toolName = "revit_find_elements";
        var harness = McpSdkTestHarness.ForTool(toolName, McpToolBehavior.StructuredFind);
        var capabilityId = await harness.SearchFirstCapabilityId(new { query = "find" });

        var result = await harness.InvokeCapability(capabilityId, new { category = "Walls" });

        Assert.Equal(240, result.StructuredContent!.Value.GetProperty("totalCount").GetInt32());
        Assert.True(result.StructuredContent.Value.GetProperty("hasMore").GetBoolean());
        var text = McpToolInvoke.Text(result);
        Assert.Contains("Found 3 elements", text, StringComparison.Ordinal);
        Assert.True(text.Length < 120);
    }

    [Fact]
    public async Task InvokeDynamic_ForwardsHostInputRequired_AndWrapsRequestState()
    {
        const string toolName = "mrtr_confirm";
        var harness = McpSdkTestHarness.ForTool(toolName, McpToolBehavior.MrtrElicitationConfirm);
        var capabilityId = await harness.SearchFirstCapabilityId(new { query = toolName });

        var ex = await harness.InvokeExpectingInputRequired(capabilityId);

        Assert.NotNull(ex.Result.InputRequests);
        Assert.Contains("confirm", ex.Result.InputRequests!.Keys);
        Assert.NotNull(ex.Result.RequestState);
        Assert.Contains(capabilityId, ex.Result.RequestState!, StringComparison.Ordinal);
        Assert.Equal(1, harness.Session.PassthroughCount);
    }

    [Fact]
    public async Task InvokeDynamic_MrtrRetry_ForwardsInputResponsesAndHostRequestState()
    {
        const string toolName = "mrtr_confirm";
        var harness = McpSdkTestHarness.ForTool(toolName, McpToolBehavior.MrtrElicitationConfirm);
        var capabilityId = await harness.SearchFirstCapabilityId(new { query = toolName });

        var first = await harness.InvokeExpectingInputRequired(capabilityId);
        var result = await harness.InvokeMrtrRetry(
            capabilityId,
            first,
            new Dictionary<string, object> { ["confirm"] = new { action = "accept" } });

        Assert.Equal("confirmed", McpToolInvoke.Text(result));
        Assert.Equal(2, harness.Session.PassthroughCount);
    }

    [Fact]
    public async Task InvokeDynamic_StaleLocator_RequiresResearchBeforeExecution()
    {
        var harness = McpSdkTestHarness.Create();
        var oldId = await harness.SearchFirstCapabilityId(new { query = "find" });
        harness.ReplaceCatalog(McpSdkCatalogOptions.Default);

        var response = McpToolInvoke.Parse<InvokeCapabilityResponse>(
            await harness.InvokeDynamic(new { capabilityId = oldId }));

        Assert.False(response.Ok);
        Assert.False(response.ExecutionStarted);
        Assert.True(response.Error!.Retryable);
        Assert.Equal("host_catalog_changed", response.Error.Reason);
        Assert.Equal("research_then_reinvoke", response.Error.Retry);
        Assert.Equal(0, harness.Session.PassthroughCount);
    }

    [Fact]
    public async Task InvokeDynamic_BatchReadsFixedAndTemplateResources()
    {
        var harness = McpSdkTestHarness.Create(McpSdkCatalogOptions.WithTemplates());
        var capabilities = await harness.Search(new { kinds = new[] { "resource", "resource_template" } });
        var resourceId = capabilities.Items.Single(item => item.Kind == "resource").CapabilityId;
        var templateId = capabilities.Items.First(item => item.Kind == "resource_template" && item.Target.Contains("element", StringComparison.Ordinal)).CapabilityId;

        var response = McpToolInvoke.Parse<InvokeCapabilityResponse>(await harness.InvokeDynamic(new
        {
            reads = new object[]
            {
                new { capabilityId = resourceId },
                new { capabilityId = templateId, arguments = new { elementId = 99 } },
            },
        }));

        Assert.Equal(2, response.Results!.Count);
        Assert.True(response.Results[0].Ok);
        Assert.True(response.Results[1].Ok);
        Assert.Equal(1, harness.Session.ReadCount);
        Assert.Equal(1, harness.Session.TemplateReadCount);
    }

    [Fact]
    public async Task InvokeDynamic_BatchRejectsToolReadsAndOverLimit()
    {
        var harness = McpSdkTestHarness.Create(McpSdkCatalogOptions.WithTemplates());
        var capabilities = await harness.Search(new { });
        var toolId = capabilities.Items.Single(item => item.Kind == "tool").CapabilityId;
        var resourceId = capabilities.Items.Single(item => item.Kind == "resource").CapabilityId;

        var mixed = await harness.InvokeDynamic(new { capabilityId = toolId, reads = new[] { new { capabilityId = resourceId } } });
        var toolRead = await harness.InvokeDynamic(new { reads = new[] { new { capabilityId = toolId } } });
        var tooMany = await harness.InvokeDynamic(new
        {
            reads = Enumerable.Range(0, 17).Select(_ => new { capabilityId = resourceId }).ToArray(),
        });

        Assert.Contains("cannot be combined", McpToolInvoke.Text(mixed), StringComparison.Ordinal);
        Assert.Contains("resources and resource templates only", McpToolInvoke.Text(toolRead), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("at most 16", McpToolInvoke.Text(tooMany), StringComparison.Ordinal);
        Assert.Equal(0, harness.Session.ReadCount);
    }
}
