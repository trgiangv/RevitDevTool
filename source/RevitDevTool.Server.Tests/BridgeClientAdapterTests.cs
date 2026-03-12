using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RevitDevTool.Contracts;

namespace RevitDevTool.Server.Tests;

public sealed class BridgeClientAdapterTests
{
    private static readonly JsonSerializerOptions JsonOptions = McpJsonUtilities.DefaultOptions;

    [Fact]
    public async Task ListToolsAsync_RoundTripsDefinitionsAndAnnotations()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var server = await FakeBridgeServer.StartAsync(request =>
        {
            Assert.Equal(BridgeActions.ListTools, request.Action);

            var tool = new Tool
            {
                Name = "read_walls",
                Title = "Read Walls",
                Description = "Reads walls",
                InputSchema = JsonDocument.Parse("{\"type\":\"object\"}").RootElement.Clone(),
                Annotations = new ToolAnnotations
                {
                    Title = "Read Walls",
                    ReadOnlyHint = true,
                    OpenWorldHint = false,
                },
            };

            return BuildResponse(request, BridgeActions.ListTools, new McpToolsListResponseBody { Tools = [tool] });
        });

        await using var client = new RevitBridgeClient();
        Assert.True(await client.ConnectAsync(server.Port, cancellationToken));

        var tools = await client.ListToolsAsync(cancellationToken);

        var tool = Assert.Single(tools);
        Assert.Equal("read_walls", tool.Name);
        Assert.Equal("Read Walls", tool.Title);
        Assert.NotNull(tool.Annotations);
        Assert.True(tool.Annotations!.ReadOnlyHint);
        Assert.False(tool.Annotations.OpenWorldHint);
    }

    [Fact]
    public void ToMcpServerTool_MapsDisplayNameAndHintsToProtocolTool()
    {
        using var outputSchemaDoc = JsonDocument.Parse("{\"type\":\"object\",\"properties\":{\"status\":{\"type\":\"string\"}}}");
        var protocolTool = new Tool
        {
            Name = "read_walls",
            Title = "Display Name",
            Description = "Reads walls",
            InputSchema = JsonDocument.Parse("{\"type\":\"object\"}").RootElement.Clone(),
            Annotations = new ToolAnnotations
            {
                Title = "Explicit Title",
                ReadOnlyHint = true,
                OpenWorldHint = false,
            },
            OutputSchema = outputSchemaDoc.RootElement.Clone(),
            Icons = [new Icon { Source = "https://example.com/icon.png", MimeType = "image/png" }],
            Meta = JsonNode.Parse("{\"feature\":\"demo\"}") as JsonObject,
        };

        var tool = RevitToolAdapter.ToMcpServerTool(protocolTool, "tool-1", new RevitBridgeClient());

        Assert.Equal("read_walls", tool.ProtocolTool.Name);
        Assert.Equal("Display Name", tool.ProtocolTool.Title);
        Assert.Equal("Explicit Title", tool.ProtocolTool.Annotations!.Title);
        Assert.True(tool.ProtocolTool.Annotations.ReadOnlyHint);
        Assert.False(tool.ProtocolTool.Annotations.OpenWorldHint);
        Assert.NotNull(tool.ProtocolTool.OutputSchema);
        Assert.Equal("string", tool.ProtocolTool.OutputSchema!.Value.GetProperty("properties").GetProperty("status").GetProperty("type").GetString());
        var icon = Assert.Single(tool.ProtocolTool.Icons!);
        Assert.Equal("https://example.com/icon.png", icon.Source);
        Assert.Equal("demo", tool.ProtocolTool.Meta!["feature"]!.GetValue<string>());
    }

    [Fact]
    public async Task ListPromptsAsync_RoundTripsDefinitions()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var server = await FakeBridgeServer.StartAsync(request =>
        {
            Assert.Equal(BridgeActions.ListPrompts, request.Action);

            var prompt = new Prompt
            {
                Name = "summarize_demo",
                Title = "Summarize Demo Context",
                Description = "Builds a parser-focused demo prompt.",
                Arguments = [new PromptArgument { Name = "topic", Description = "Topic to summarize.", Required = true }],
            };

            return BuildResponse(request, BridgeActions.ListPrompts, new McpPromptsListResponseBody { Prompts = [prompt] });
        });

        await using var client = new RevitBridgeClient();
        Assert.True(await client.ConnectAsync(server.Port, cancellationToken));

        var prompts = await client.ListPromptsAsync(cancellationToken);

        var prompt = Assert.Single(prompts);
        Assert.Equal("summarize_demo", prompt.Name);
        Assert.Equal("Summarize Demo Context", prompt.Title);
        Assert.Single(prompt.Arguments ?? []);
    }

    [Fact]
    public async Task ListResourcesAsync_RoundTripsDefinitions()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var server = await FakeBridgeServer.StartAsync(request =>
        {
            Assert.Equal(BridgeActions.ListResources, request.Action);

            var template = new ResourceTemplate
            {
                Name = "demo_view",
                Title = "Demo View Resource",
                Description = "Returns a template resource for a demo view.",
                UriTemplate = "sample://demo/views/{viewId}",
                MimeType = "application/json",
            };

            return BuildResponse(request, BridgeActions.ListResources, new McpResourcesListResponseBody { ResourceTemplates = [template] });
        });

        await using var client = new RevitBridgeClient();
        Assert.True(await client.ConnectAsync(server.Port, cancellationToken));

        var (_, templates) = await client.ListResourcesAsync(cancellationToken);

        var template = Assert.Single(templates);
        Assert.Equal("demo_view", template.Name);
        Assert.True(template.IsTemplated);
        Assert.Equal("sample://demo/views/{viewId}", template.UriTemplate);
    }

    [Fact]
    public async Task GetPromptAsync_RoundTripsPromptResult()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var server = await FakeBridgeServer.StartAsync(request =>
        {
            Assert.Equal(BridgeActions.GetPrompt, request.Action);
            var body = DeserializeBody<McpPromptGetRequestBody>(request)!;
            Assert.Equal("prompt-1", body.PromptId);
            Assert.Equal("summarize_demo", body.PromptName);
            Assert.Equal("walls", body.Arguments!["topic"].GetString());

            return BuildResponse(request, BridgeActions.GetPrompt, new McpPromptGetResponseBody
            {
                PromptId = "prompt-1",
                PromptName = "summarize_demo",
                Result = new GetPromptResult
                {
                    Description = "demo",
                    Messages = [new PromptMessage
                    {
                        Role = Role.User,
                        Content = new TextContentBlock { Text = "summarize walls" }
                    }]
                }
            });
        });

        await using var client = new RevitBridgeClient();
        Assert.True(await client.ConnectAsync(server.Port, cancellationToken));

        var result = await client.GetPromptAsync(
            "prompt-1",
            "summarize_demo",
            new Dictionary<string, JsonElement> { ["topic"] = JsonSerializer.SerializeToElement("walls") },
            cancellationToken);

        Assert.Equal("demo", result.Description);
        var message = Assert.Single(result.Messages);
        var text = Assert.IsType<TextContentBlock>(message.Content);
        Assert.Equal("summarize walls", text.Text);
    }

    [Fact]
    public async Task ReadResourceAsync_RoundTripsResourceResult()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var server = await FakeBridgeServer.StartAsync(request =>
        {
            Assert.Equal(BridgeActions.ReadResource, request.Action);
            var body = DeserializeBody<McpResourceReadRequestBody>(request)!;
            Assert.Equal("resource-1", body.ResourceId);
            Assert.Equal("demo_view", body.ResourceName);
            Assert.Equal("sample://demo/views/A101", body.Uri);

            return BuildResponse(request, BridgeActions.ReadResource, new McpResourceReadResponseBody
            {
                ResourceId = "resource-1",
                ResourceName = "demo_view",
                Result = new ReadResourceResult
                {
                    Contents = [new TextResourceContents
                    {
                        Uri = body.Uri,
                        MimeType = "application/json",
                        Text = "{\"viewId\":\"A101\"}"
                    }]
                }
            });
        });

        await using var client = new RevitBridgeClient();
        Assert.True(await client.ConnectAsync(server.Port, cancellationToken));

        var result = await client.ReadResourceAsync("resource-1", "demo_view", "sample://demo/views/A101", cancellationToken);

        var content = Assert.IsType<TextResourceContents>(Assert.Single(result.Contents));
        Assert.Equal("sample://demo/views/A101", content.Uri);
        Assert.Equal("{\"viewId\":\"A101\"}", content.Text);
    }

    [Fact]
    public async Task InvokeAsync_MapsStructuredBridgePayloadToCallToolResult()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var server = await FakeBridgeServer.StartAsync(request =>
        {
            Assert.Equal(BridgeActions.ToolCall, request.Action);
            var body = DeserializeBody<McpToolCallRequestBody>(request)!;
            Assert.Equal("tool-1", body.ToolId);
            Assert.Equal("read_walls", body.ToolName);

            return BuildResponse(request, BridgeActions.ToolCall, new McpToolCallResponseBody
            {
                ToolId = "tool-1",
                ToolName = "read_walls",
                State = ExecutionState.Completed,
                Detail = "done",
                Result = new CallToolResult
                {
                    Content = [new TextContentBlock { Text = "ok" }],
                    StructuredContent = JsonSerializer.SerializeToElement(new { answer = 42 }),
                },
            });
        });

        await using var client = new RevitBridgeClient();
        Assert.True(await client.ConnectAsync(server.Port, cancellationToken));

        var protocolTool = new Tool
        {
            Name = "read_walls",
            Title = "Read Walls",
            Description = "Reads walls",
            InputSchema = JsonDocument.Parse("{\"type\":\"object\"}").RootElement.Clone(),
        };
        var tool = RevitToolAdapter.ToMcpServerTool(protocolTool, "tool-1", client);

        var request = CreateRequestContext(new Dictionary<string, JsonElement>
        {
            ["level"] = JsonSerializer.SerializeToElement("L1")
        });

        var result = await tool.InvokeAsync(request, cancellationToken);

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Equal("ok", text.Text);
        Assert.NotNull(result.StructuredContent);
        Assert.Equal(42, result.StructuredContent.Value.GetProperty("answer").GetInt32());
        Assert.NotEqual(true, result.IsError);
    }

    [Fact]
    public async Task InvokeAsync_MapsBridgeErrorBodyToErrorCallToolResult()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var server = await FakeBridgeServer.StartAsync(request =>
            BuildErrorResponse(request, new McpErrorBody
            {
                Code = "tool.failed",
                Message = "bad input",
                Details = "trace"
            }));

        await using var client = new RevitBridgeClient();
        Assert.True(await client.ConnectAsync(server.Port, cancellationToken));

        var protocolTool = new Tool
        {
            Name = "read_walls",
            Title = "Read Walls",
            Description = "Reads walls",
            InputSchema = JsonDocument.Parse("{\"type\":\"object\"}").RootElement.Clone(),
        };
        var tool = RevitToolAdapter.ToMcpServerTool(protocolTool, "tool-1", client);

        var request = CreateRequestContext(new Dictionary<string, JsonElement>());
        var result = await tool.InvokeAsync(request, cancellationToken);

        Assert.True(result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Equal("bad input", text.Text);
    }

    [Fact]
    public async Task PromptAdapter_GetAsync_MapsBridgeResultToProtocolPrompt()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var server = await FakeBridgeServer.StartAsync(request =>
            BuildResponse(request, BridgeActions.GetPrompt, new McpPromptGetResponseBody
            {
                PromptId = "prompt-1",
                PromptName = "summarize_demo",
                Result = new GetPromptResult
                {
                    Description = "demo",
                    Messages = [new PromptMessage
                    {
                        Role = Role.User,
                        Content = new TextContentBlock { Text = "prompt body" }
                    }]
                }
            }));

        await using var client = new RevitBridgeClient();
        Assert.True(await client.ConnectAsync(server.Port, cancellationToken));

        var protocolPrompt = new Prompt
        {
            Name = "summarize_demo",
            Title = "Summarize Demo",
            Description = "Builds a demo prompt.",
            Arguments = [new PromptArgument { Name = "topic", Title = "Topic", Description = "Topic to summarize.", Required = true }],
        };

        var prompt = RevitPromptAdapter.ToMcpServerPrompt(protocolPrompt, "prompt-1", client);
        var result = await prompt.GetAsync(
            CreatePromptRequestContext(new Dictionary<string, JsonElement> { ["topic"] = JsonSerializer.SerializeToElement("walls") }),
            cancellationToken);

        Assert.Equal("demo", result.Description);
        var message = Assert.Single(result.Messages);
        Assert.Equal(Role.User, message.Role);
        Assert.Equal("prompt body", Assert.IsType<TextContentBlock>(message.Content).Text);
    }

    [Fact]
    public async Task ResourceAdapter_ReadAsync_MapsBridgeResultToProtocolResource()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var server = await FakeBridgeServer.StartAsync(request =>
            BuildResponse(request, BridgeActions.ReadResource, new McpResourceReadResponseBody
            {
                ResourceId = "resource-1",
                ResourceName = "demo_view",
                Result = new ReadResourceResult
                {
                    Contents = [new TextResourceContents
                    {
                        Uri = "sample://demo/views/A101",
                        MimeType = "application/json",
                        Text = "{\"viewId\":\"A101\"}"
                    }]
                }
            }));

        await using var client = new RevitBridgeClient();
        Assert.True(await client.ConnectAsync(server.Port, cancellationToken));

        var protocolTemplate = new ResourceTemplate
        {
            Name = "demo_view",
            Title = "Demo View",
            Description = "Returns a demo view resource.",
            UriTemplate = "sample://demo/views/{viewId}",
            MimeType = "application/json",
        };

        var resource = RevitResourceAdapter.ToMcpServerResource(null, protocolTemplate, "resource-1", client);
        Assert.True(resource.IsMatch("sample://demo/views/A101"));

        var result = await resource.ReadAsync(
            CreateResourceRequestContext("sample://demo/views/A101"),
            cancellationToken);

        var content = Assert.IsType<TextResourceContents>(Assert.Single(result.Contents));
        Assert.Equal("{\"viewId\":\"A101\"}", content.Text);
    }

    [Fact]
    public async Task ListToolsAsync_ReturnsEmptyWhenDisconnected()
    {
        await using var client = new RevitBridgeClient();
        var tools = await client.ListToolsAsync(TestContext.Current.CancellationToken);
        Assert.Empty(tools);
    }

    [Fact]
    public async Task CallToolAsync_ReturnsErrorWhenDisconnected()
    {
        await using var client = new RevitBridgeClient();
        var result = await client.CallToolAsync("tool-1", "read_walls", "{}", TestContext.Current.CancellationToken);
        Assert.Equal(ExecutionState.Failed, result.State);
    }

    [Fact]
    public async Task InvokeAsync_MapsFailedStateToErrorResult()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var server = await FakeBridgeServer.StartAsync(request =>
            BuildResponse(request, BridgeActions.ToolCall, new McpToolCallResponseBody
            {
                ToolId = "tool-1",
                ToolName = "read_walls",
                State = ExecutionState.Failed,
                Detail = "bad input",
                Result = new CallToolResult(),
            }));

        await using var client = new RevitBridgeClient();
        Assert.True(await client.ConnectAsync(server.Port, cancellationToken));

        var protocolTool = new Tool
        {
            Name = "read_walls",
            Title = "Read Walls",
            Description = "Reads walls",
            InputSchema = JsonDocument.Parse("{\"type\":\"object\"}").RootElement.Clone(),
        };
        var tool = RevitToolAdapter.ToMcpServerTool(protocolTool, "tool-1", client);

        var request = CreateRequestContext(new Dictionary<string, JsonElement>());
        var result = await tool.InvokeAsync(request, cancellationToken);

        Assert.True(result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Equal("bad input", text.Text);
    }

    private static RequestContext<CallToolRequestParams> CreateRequestContext(IDictionary<string, JsonElement> arguments)
    {
        var context = (RequestContext<CallToolRequestParams>)RuntimeHelpers.GetUninitializedObject(
            typeof(RequestContext<CallToolRequestParams>));
        context.Params = new CallToolRequestParams
        {
            Name = "tool",
            Arguments = arguments,
        };
        return context;
    }

    private static RequestContext<GetPromptRequestParams> CreatePromptRequestContext(IDictionary<string, JsonElement> arguments)
    {
        var context = (RequestContext<GetPromptRequestParams>)RuntimeHelpers.GetUninitializedObject(
            typeof(RequestContext<GetPromptRequestParams>));
        context.Params = new GetPromptRequestParams
        {
            Name = "prompt",
            Arguments = arguments,
        };
        return context;
    }

    private static RequestContext<ReadResourceRequestParams> CreateResourceRequestContext(string uri)
    {
        var context = (RequestContext<ReadResourceRequestParams>)RuntimeHelpers.GetUninitializedObject(
            typeof(RequestContext<ReadResourceRequestParams>));
        context.Params = new ReadResourceRequestParams
        {
            Uri = uri,
        };
        return context;
    }

    private static Envelope BuildResponse<TBody>(Envelope request, string action, TBody body)
    {
        return new Envelope
        {
            Id = request.Id,
            ExecutionId = request.ExecutionId,
            Kind = BridgeMessageKinds.Response,
            Action = action,
            Body = JsonSerializer.SerializeToElement(body, JsonOptions)
        };
    }

    private static Envelope BuildErrorResponse<TBody>(Envelope request, TBody body)
    {
        return new Envelope
        {
            Id = request.Id,
            ExecutionId = request.ExecutionId,
            Kind = BridgeMessageKinds.Response,
            Action = request.Action,
            IsError = true,
            Body = JsonSerializer.SerializeToElement(body, JsonOptions)
        };
    }

    private static TBody? DeserializeBody<TBody>(Envelope envelope)
    {
        if (envelope.Body is not { } body || body.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return default;

        return JsonSerializer.Deserialize<TBody>(body.GetRawText(), JsonOptions);
    }

    private sealed class FakeBridgeServer : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly Func<Envelope, Envelope> _handler;
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private readonly Task _runTask;

        private FakeBridgeServer(Func<Envelope, Envelope> handler)
        {
            _handler = handler;
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _runTask = RunAsync();
        }

        public int Port { get; }

        public static Task<FakeBridgeServer> StartAsync(Func<Envelope, Envelope> handler)
        {
            return Task.FromResult(new FakeBridgeServer(handler));
        }

        private async Task RunAsync()
        {
            try
            {
                using var client = await _listener.AcceptTcpClientAsync(_cancellationTokenSource.Token);
                using var stream = client.GetStream();

                while (!_cancellationTokenSource.IsCancellationRequested)
                {
                    var request = await ReadEnvelopeAsync(stream, _cancellationTokenSource.Token);
                    if (request is null)
                        break;

                    var response = _handler(request);
                    await WriteEnvelopeAsync(stream, response, _cancellationTokenSource.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown path when the test disposes the fake server.
            }
            catch (ObjectDisposedException)
            {
                // Listener/stream disposal during shutdown is expected.
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _cancellationTokenSource.CancelAsync();
            _listener.Stop();
            try
            {
                await _runTask;
            }
            catch (OperationCanceledException)
            {
                // Run loop observes test shutdown through cancellation.
            }
            _cancellationTokenSource.Dispose();
        }

        private static async Task WriteEnvelopeAsync(NetworkStream stream, Envelope envelope, CancellationToken cancellationToken)
        {
            var payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(envelope, JsonOptions));
            var header = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(header, payload.Length);
            await stream.WriteAsync(header, cancellationToken);
            await stream.WriteAsync(payload, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }

        private static async Task<Envelope?> ReadEnvelopeAsync(NetworkStream stream, CancellationToken cancellationToken)
        {
            var header = await ReadExactAsync(stream, 4, cancellationToken);
            if (header is null)
                return null;

            var payloadLength = BinaryPrimitives.ReadInt32LittleEndian(header);
            var payload = await ReadExactAsync(stream, payloadLength, cancellationToken);
            if (payload is null)
                return null;

            return JsonSerializer.Deserialize<Envelope>(Encoding.UTF8.GetString(payload), JsonOptions);
        }

        private static async Task<byte[]?> ReadExactAsync(NetworkStream stream, int length, CancellationToken cancellationToken)
        {
            var buffer = new byte[length];
            var offset = 0;
            while (offset < length)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(offset, length - offset), cancellationToken);
                if (read == 0)
                    return offset == 0 ? null : throw new EndOfStreamException("Socket closed mid-frame.");
                offset += read;
            }

            return buffer;
        }
    }
}