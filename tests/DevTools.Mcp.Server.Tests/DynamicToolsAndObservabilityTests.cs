using DevTools.Mcp.Server.Contracts;
using DevTools.Mcp.Server.Tools;
using DevTools.Mcp.Server.Tests.Harness;
using ModelContextProtocol.Server;

namespace DevTools.Mcp.Server.Tests;

/// <summary>search_dynamic discovery and validation tests (invoke pass-through lives in <see cref="InvokeDynamicSdkHarnessTests"/>).</summary>
public sealed class DynamicToolsAndObservabilityTests
{
    [Fact]
    public async Task SearchDynamic_UsesLocalCatalog_AndReturnsOpaqueVersionedLocator()
    {
        var harness = McpSdkTestHarness.Create();
        var item = Assert.Single((await harness.Search(new { query = "find" })).Items);

        Assert.Equal("revit_find_elements", item.Target);
        Assert.NotEqual("revit_find_elements", item.CapabilityId);
        Assert.True(DynamicCapabilityId.TryDecode(item.CapabilityId, out var locator));
        Assert.Equal(101, locator!.HostInstanceId);
        Assert.Equal(0, harness.Session.PassthroughCount);
        Assert.Equal(0, harness.Session.ReadCount);
    }

    [Fact]
    public async Task SearchDynamic_DefaultsToSummary_AndSchemaIsExplicit()
    {
        var harness = McpSdkTestHarness.Create();

        var summary = await harness.Search(new { query = "find" });
        var schema = await harness.Search(new { query = "find", detail = "schema" });

        Assert.Null(Assert.Single(summary.Items).InputSchema);
        Assert.NotNull(Assert.Single(schema.Items).InputSchema);
        Assert.Contains("category", schema.Items[0].RequiredArgs!);
        Assert.Contains("selected_only", schema.Items[0].ArgsHint!);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(33)]
    public async Task SearchDynamic_RejectsInvalidLimitInsteadOfClamping(int limit)
    {
        var harness = McpSdkTestHarness.Create();
        var result = await McpToolInvoke.Invoke(harness.SearchTool, "search_dynamic", new { limit });

        Assert.True(result.IsError != true);
        Assert.Contains("limit must be between 1 and 32", McpToolInvoke.Text(result), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchDynamic_NormalizesTokens_AndReportsHasMore()
    {
        var options = McpSdkCatalogOptions.ManyTools(2);
        var harness = McpSdkTestHarness.Create(options);
        var response = await harness.Search(new { query = "revit find", limit = 1 });

        Assert.Single(response.Items);
        Assert.True(response.HasMore);
    }

    [Fact]
    public async Task SearchDynamic_FiltersResourceTemplateHits()
    {
        var harness = McpSdkTestHarness.Create(McpSdkCatalogOptions.WithTemplates());
        var response = await harness.Search(new { query = "element", kinds = new[] { "resource_template" } });
        var item = Assert.Single(response.Items);

        Assert.Equal("resource_template", item.Kind);
        Assert.Equal("revit://element/{elementId}", item.Target);
    }

    [Fact]
    public async Task SearchDynamic_TemplateHits_ListUriParametersInArgsHint()
    {
        var harness = McpSdkTestHarness.Create(McpSdkCatalogOptions.WithTemplates());
        var response = await harness.Search(new { kinds = new[] { "resource_template" } });

        var element = response.Items.Single(item => item.Target.Contains("element", StringComparison.Ordinal));
        Assert.Contains("elementId", element.ArgsHint!);
        var schedule = response.Items.Single(item => item.Target.Contains("schedule", StringComparison.Ordinal));
        Assert.Contains("scheduleId", schedule.ArgsHint!);
    }

    [Fact]
    public async Task InvokeDynamic_RoutesSingleCapabilityWithoutChangingFixedSurface()
    {
        var harness = McpSdkTestHarness.Create();
        var capabilityId = await harness.SearchFirstCapabilityId(new { query = "find" });
        var collection = new McpServerPrimitiveCollection<McpServerTool> { harness.SearchTool, harness.InvokeTool };

        var result = await harness.InvokeCapability(capabilityId, new { category = "Walls" });

        Assert.Equal("called:revit_find_elements", McpToolInvoke.Text(result));
        Assert.Equal(1, harness.Session.PassthroughCount);
        Assert.Equal(harness.Session.Key, harness.Broker.RequestedHostKey);
        Assert.Equal(["invoke_dynamic", "search_dynamic"], collection.Select(tool => tool.ProtocolTool.Name).OrderBy(name => name).ToArray());
    }

    [Fact]
    public async Task InvokeDynamic_BatchAppendsCompleteItems_AndUsesTypedOversizeError()
    {
        var harness = McpSdkTestHarness.Create(new McpSdkCatalogOptions(
            ["revit_find_elements"],
            ["revit://version"],
            [],
            new string('x', InvokeDynamicLimits.HardResultBudgetBytes + 1)));
        var resourceId = (await harness.Search(new { kinds = new[] { "resource" } })).Items.Single().CapabilityId;

        var response = McpToolInvoke.Parse<InvokeCapabilityResponse>(await harness.InvokeDynamic(new
        {
            reads = new[] { new { capabilityId = resourceId } },
        }));

        var item = Assert.Single(response.Results!);
        Assert.False(item.Ok);
        Assert.Equal("result_too_large", item.Error!.Type);
        Assert.Equal(1, harness.Session.ReadCount);
    }

    [Fact]
    public void InvokeCapabilityRequestValidator_RejectsMalformedArguments()
    {
        var problems = InvokeCapabilityValidator.Validate(
            new InvokeCapabilityRequest("bad", System.Text.Json.JsonSerializer.SerializeToElement("not-an-object")));
        Assert.Contains(problems, problem => problem.Name == "capabilityId");
        Assert.Contains(problems, problem => problem.Name == "arguments");
    }
}
