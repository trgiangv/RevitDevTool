using System.Text.Json;
using System.Text.Json.Nodes;
using Bogus;
using DevTools.Daemon.Mcp.Processes;
using DevTools.Ipc;
using DevTools.Mcp.Core;
using DevTools.Daemon.Mcp.Contracts;
using DevTools.Daemon.Mcp.Tools;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Moq;

namespace DevTools.Mcp.Server.Tests.Harness;

using System.Linq;

/// <summary>SDK-aligned tool behaviors for mock host sessions (SEP-2322 / CallToolResult pass-through).</summary>
public enum McpToolBehavior
{
    PlainText,
    ImagePng,
    StructuredFind,
    ErrorWithMeta,
    MixedTextAndImage,
    MrtrElicitationConfirm,
}

/// <summary>Catalog shape for <see cref="McpSdkTestHarness"/>.</summary>
internal sealed record McpSdkCatalogOptions(
    IReadOnlyList<string> Tools,
    IReadOnlyList<string> Resources,
    IReadOnlyList<string> ResourceTemplates,
    string? LargeResourceText = null,
    IReadOnlyDictionary<string, McpToolBehavior>? ToolBehaviors = null,
    bool BumpToolSchema = false)
{
    public static McpSdkCatalogOptions Default { get; } = new(
        ["revit_find_elements"],
        ["revit://version"],
        [],
        ToolBehaviors: new Dictionary<string, McpToolBehavior>
        {
            ["revit_find_elements"] = McpToolBehavior.PlainText,
        });

    public static McpSdkCatalogOptions WithTemplates() => new(
        ["revit_find_elements"],
        ["revit://version"],
        ["revit://element/{elementId}", "revit://schedule/{scheduleId}/preview"],
        ToolBehaviors: new Dictionary<string, McpToolBehavior>
        {
            ["revit_find_elements"] = McpToolBehavior.PlainText,
        });

    public static McpSdkCatalogOptions ForTool(string toolName, McpToolBehavior behavior) => new(
        [toolName],
        ["revit://version"],
        [],
        ToolBehaviors: new Dictionary<string, McpToolBehavior> { [toolName] = behavior });

    public static McpSdkCatalogOptions ManyTools(int count)
    {
        var faker = new Faker();
        var tools = Enumerable.Range(0, count)
            .Select(index => $"revit_find_{faker.Commerce.ProductMaterial()}_{index}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(count)
            .ToArray();
        var behaviors = tools.ToDictionary(name => name, _ => McpToolBehavior.PlainText, StringComparer.OrdinalIgnoreCase);
        return new McpSdkCatalogOptions(tools, ["revit://version"], [], ToolBehaviors: behaviors);
    }
}

/// <summary>Central harness: mock host catalog + session wired to search_dynamic / invoke_dynamic.</summary>
internal sealed class McpSdkTestHarness
{
    private static readonly Faker Faker = new();

    public McpSdkProcessSessions Broker { get; }
    public McpSdkProcessSession Session { get; }
    public McpServerTool SearchTool { get; }
    public McpServerTool InvokeTool { get; }

    private McpSdkTestHarness(McpSdkProcessSessions broker, McpSdkProcessSession session)
    {
        Broker = broker;
        Session = session;
        SearchTool = DevTools.Daemon.Mcp.Tools.SearchTool.Create(broker);
        InvokeTool = DevTools.Daemon.Mcp.Tools.InvokeTool.Create(broker);
    }

    public static McpSdkTestHarness Create(McpSdkCatalogOptions? options = null)
    {
        options ??= McpSdkCatalogOptions.Default;
        var session = new McpSdkProcessSession(101, options);
        var broker = new McpSdkProcessSessions(session);
        broker.Catalog.Replace(McpSdkCatalogBuilder.BuildCatalog(session, options));
        return new McpSdkTestHarness(broker, session);
    }

    public static McpSdkTestHarness ForTool(string toolName, McpToolBehavior behavior) =>
        Create(McpSdkCatalogOptions.ForTool(toolName, behavior));

    public async Task<SearchResponse> Search(object args) =>
        McpToolInvoke.Parse<SearchResponse>(await McpToolInvoke.Invoke(SearchTool, "search_dynamic", args));

    public async Task<string> SearchFirstId(object args)
    {
        var response = await Search(args);
        return Enumerable.Single(response.Items).Id;
    }

    public async Task<CallToolResult> InvokeDynamic(object args) =>
        await McpToolInvoke.Invoke(InvokeTool, "invoke_dynamic", args);

    public async Task<CallToolResult> InvokeId(string id, object? arguments = null) =>
        await InvokeDynamic(new { id, arguments });

    public async Task<InputRequiredException> InvokeExpectingInputRequired(string id, object? arguments = null) =>
        await Assert.ThrowsExactlyAsync<InputRequiredException>(() => InvokeId(id, arguments));

    public async Task<CallToolResult> InvokeMrtrRetry(
        string id,
        InputRequiredException firstRound,
        IDictionary<string, object> inputResponses,
        object? arguments = null)
    {
        return await InvokeDynamic(new Dictionary<string, object>
        {
            ["id"] = id,
            ["arguments"] = arguments ?? new { },
            ["inputResponses"] = inputResponses,
            ["requestState"] = firstRound.Result.RequestState!,
        });
    }

    public void ReplaceCatalog(McpSdkCatalogOptions options) =>
        Broker.Catalog.Replace(McpSdkCatalogBuilder.BuildCatalog(Session, options));
}

internal static class McpToolInvoke
{
    public static async Task<CallToolResult> Invoke(McpServerTool tool, string name, object args)
    {
        var argumentMap = args is Dictionary<string, object> dict
            ? JsonSerializer.SerializeToElement(dict).EnumerateObject()
                .ToDictionary(property => property.Name, property => property.Value)
            : JsonSerializer.SerializeToElement(args).EnumerateObject()
                .ToDictionary(property => property.Name, property => property.Value);

        var server = new Mock<McpServer>();
        server.Setup(s => s.IsMrtrSupported).Returns(true);

        return await tool.InvokeAsync(
            new RequestContext<CallToolRequestParams>(
                server.Object,
                new JsonRpcRequest { Method = "tools/call", Id = new RequestId("1") },
                new CallToolRequestParams
                {
                    Name = name,
                    Arguments = argumentMap,
                    InputResponses = TryDeserializeInputResponses(argumentMap),
                    RequestState = argumentMap.TryGetValue("requestState", out var stateNode) && stateNode.ValueKind is JsonValueKind.String
                        ? stateNode.GetString()
                        : null,
                }),
            CancellationToken.None);
    }

    public static string Text(CallToolResult result) =>
        result.Content.OfType<TextContentBlock>().Single().Text;

    public static T Parse<T>(CallToolResult result)
    {
        var text = Text(result);
        return JsonSerializer.Deserialize<T>(text, McpJsonUtilities.DefaultOptions)
            ?? throw new InvalidOperationException(text);
    }

    private static IDictionary<string, InputResponse>? TryDeserializeInputResponses(Dictionary<string, JsonElement> argumentMap)
    {
        if (!argumentMap.TryGetValue("inputResponses", out var node) || node.ValueKind is not JsonValueKind.Object)
            return null;

        var responses = new Dictionary<string, InputResponse>();
        foreach (var property in node.EnumerateObject())
            responses[property.Name] = new InputResponse { RawValue = property.Value.Clone() };
        return responses;
    }
}

internal sealed class McpSdkProcessSessions(McpSdkProcessSession session) : IProcessSessions
{
    public ProcessCatalogs Catalog { get; } = new();
    public event Action? Changed { add { } remove { } }

    public IProcessSession? GetByProcessId(int processId) =>
        processId == session.ProcessId ? session : null;

    public Task RunAsync(CancellationToken ct) => Task.CompletedTask;
}

/// <summary>Mock host session: one passthrough round per SDK MRTR semantics (no client auto-retry).</summary>
internal sealed class McpSdkProcessSession(int pid, McpSdkCatalogOptions options) : IProcessSession
{
  private readonly Dictionary<string, McpToolBehavior> _behaviors =
      options.ToolBehaviors?.ToDictionary(
          pair => pair.Key,
          pair => pair.Value,
          StringComparer.OrdinalIgnoreCase)
      ?? new Dictionary<string, McpToolBehavior>(StringComparer.OrdinalIgnoreCase);

  public int ProcessId => pid;
  public string PipeName { get; } = $"DevToolsMcp_Revit_2025_{pid}";
  public InstanceInfo Info { get; } = new() { HostApp = "Revit", ProcessId = pid, VersionNumber = "2025" };
  public bool IsConnected => true;
  public int PassthroughCount { get; private set; }
  public int ReadCount { get; private set; }
  public int TemplateReadCount { get; private set; }

  public Task<Result> CallToolPassthroughAsync(CallToolRequestParams parameters, CancellationToken ct = default)
  {
    PassthroughCount++;
    var behavior = ResolveBehavior(parameters.Name);
    if (behavior is McpToolBehavior.MrtrElicitationConfirm)
      return Task.FromResult(BuildMrtrOutcome(parameters));

    return Task.FromResult<Result>(BuildToolResult(parameters.Name));
  }

  public Task<ReadResourceResult> ReadResourceAsync(string uri, CancellationToken ct = default)
  {
    ReadCount++;
    var text = options.LargeResourceText ?? "ok";
    return Task.FromResult(new ReadResourceResult
    {
      Contents = [new TextResourceContents { Uri = uri, Text = text }],
    });
  }

  public Task<ReadResourceResult> ReadResourceAsync(
      string uriTemplate,
      IDictionary<string, JsonElement> arguments,
      CancellationToken ct = default)
  {
    TemplateReadCount++;
    var elementId = arguments.TryGetValue("elementId", out var idNode) ? idNode.GetInt64() : 0;
    return Task.FromResult(new ReadResourceResult
    {
      Contents = [new TextResourceContents
      {
        Uri = uriTemplate,
        Text = $"template:{elementId}",
        MimeType = uriTemplate.Contains("schedule", StringComparison.Ordinal) ? "text/csv" : "application/json",
      }],
    });
  }

  public ValueTask DisposeAsync() => ValueTask.CompletedTask;

  private McpToolBehavior ResolveBehavior(string toolName) =>
      _behaviors.TryGetValue(toolName, out var behavior) ? behavior : McpToolBehavior.PlainText;

  private Result BuildMrtrOutcome(CallToolRequestParams parameters)
  {
    if (parameters.InputResponses is null)
      return new InputRequiredResult
      {
        InputRequests = new Dictionary<string, InputRequest>
        {
          ["confirm"] = InputRequest.ForElicitation(new ElicitRequestParams
          {
            Message = "Confirm host MRTR round?",
            RequestedSchema = new ElicitRequestParams.RequestSchema
            {
              Properties = new Dictionary<string, ElicitRequestParams.PrimitiveSchemaDefinition>
              {
                ["confirm"] = new ElicitRequestParams.BooleanSchema { Description = "Confirm" },
              },
            },
          }),
        },
        RequestState = "host-round1",
      };

    return new CallToolResult
    {
      Content = [new TextContentBlock { Text = "confirmed" }],
    };
  }

  private CallToolResult BuildToolResult(string toolName)
  {
    switch (ResolveBehavior(toolName))
    {
      case McpToolBehavior.ImagePng:
        return new CallToolResult { Content = [ImageContentBlock.FromBytes(new byte[] { 1, 2, 3 }, "image/png")] };
      case McpToolBehavior.ErrorWithMeta:
        return new CallToolResult
        {
          IsError = true,
          Meta = new JsonObject { ["response"] = "meta" },
          StructuredContent = JsonDocument.Parse("{\"ok\":false}").RootElement.Clone(),
          Content = [new TextContentBlock { Text = "tool failed" }],
        };
      case McpToolBehavior.MixedTextAndImage:
        return new CallToolResult
        {
          Content =
          [
            new TextContentBlock { Text = "screenshot attached" },
            ImageContentBlock.FromBytes(new byte[] { 4, 5, 6 }, "image/png"),
          ],
        };
      case McpToolBehavior.StructuredFind:
        return new CallToolResult
        {
          StructuredContent = JsonDocument.Parse(
              "{\"elementIds\":[1,2,3],\"totalCount\":240,\"hasMore\":true,\"offset\":0}").RootElement.Clone(),
          Content = [new TextContentBlock { Text = "Found 3 elements (total 240, hasMore=true)" }],
        };
      default:
        return new CallToolResult { Content = [new TextContentBlock { Text = $"called:{toolName}" }] };
    }
  }
}

internal static class McpSdkCatalogBuilder
{
  internal static ProcessCatalog BuildCatalog(McpSdkProcessSession session, McpSdkCatalogOptions options) => new()
  {
    ProcessId = session.ProcessId,
    Instance = session.Info,
    PipeName = session.PipeName,
    Tools = options.Tools.Select(name => new Tool
    {
      Name = name,
      Description = DescribeTool(name),
      InputSchema = name.Contains("screenshot", StringComparison.OrdinalIgnoreCase)
          ? JsonSerializer.SerializeToElement(new { type = "object" })
          : SerializeFindToolSchema(options.BumpToolSchema),
    }).ToArray(),
    Resources = options.Resources.Select(uri => new Resource
    {
      Uri = uri,
      Name = uri,
      MimeType = "text/plain",
    }).ToArray(),
    ResourceTemplates = options.ResourceTemplates.Select(uri => new ResourceTemplate
    {
      UriTemplate = uri,
      Name = uri,
      Description = uri.Contains("element", StringComparison.Ordinal) ? "Element summary" : "Schedule preview",
      MimeType = uri.Contains("schedule", StringComparison.Ordinal) ? "text/csv" : "application/json",
    }).ToArray(),
  };

  private static string DescribeTool(string name) =>
      name.Contains("screenshot", StringComparison.OrdinalIgnoreCase) ? "Capture view screenshot"
      : name.Contains("walls", StringComparison.OrdinalIgnoreCase) ? "Find walls"
      : "Find elements";

  private static JsonElement SerializeFindToolSchema(bool bumpToolSchema) =>
      bumpToolSchema
          ? JsonSerializer.SerializeToElement(new
          {
              type = "object",
              required = new[] { "category", "limit" },
              properties = new
              {
                  category = new { type = "string" },
                  selected_only = new { type = "boolean" },
                  limit = new { type = "integer" },
              },
          })
          : JsonSerializer.SerializeToElement(new
          {
              type = "object",
              required = new[] { "category" },
              properties = new { category = new { type = "string" }, selected_only = new { type = "boolean" } },
          });
}
