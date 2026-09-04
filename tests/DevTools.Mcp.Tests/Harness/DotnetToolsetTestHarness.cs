using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using DevTools.Mcp.Catalog.Discovery;

using Microsoft.Extensions.DependencyInjection;

using ModelContextProtocol;

using ModelContextProtocol.Protocol;

using ModelContextProtocol.Server;

using Moq;



namespace DevTools.Mcp.Tests.Harness;



internal static class DotnetToolsetTestHarness

{

    private static readonly IServiceProvider EmptyServices = new ServiceCollection().BuildServiceProvider();



    public static RequestContext<CallToolRequestParams> CreateRequest(

        Mock<McpServer>? server = null,

        IDictionary<string, JsonElement>? arguments = null,

        IDictionary<string, InputResponse>? inputResponses = null,

        string? requestState = null,

        ProgressToken? progressToken = null,

        ClaimsPrincipal? user = null)

    {

        server ??= CreateMrtrServer();

        var parameters = new CallToolRequestParams

        {

            Name = "stub",

            Arguments = arguments,

            InputResponses = inputResponses,

            RequestState = requestState,

        };

        if (progressToken is not null)

        {

            parameters.Meta = new JsonObject

            {

                ["progressToken"] = progressToken.Value.Token switch

                {

                    string s => s,

                    long l => l,

                    _ => progressToken.Value.ToString(),

                },

            };

        }



        var request = new RequestContext<CallToolRequestParams>(

            server.Object,

            new JsonRpcRequest { Method = "tools/call", Id = new RequestId("1") },

            parameters);

        if (user is not null)

            request.User = user;

        return request;

    }



    public static Mock<McpServer> CreateMrtrServer(bool isMrtrSupported = true)

    {

        var server = new Mock<McpServer>();

        server.Setup(s => s.IsMrtrSupported).Returns(isMrtrSupported);

        return server;

    }



    public static Dictionary<string, JsonElement> Arguments(params (string Key, object Value)[] pairs) =>

        pairs.ToDictionary(

            pair => pair.Key,

            pair => JsonSerializer.SerializeToElement(pair.Value, McpJsonUtilities.DefaultOptions));



    public static InputRequiredException InvokeExpectingInputRequired(

        MethodInfo method,

        RequestContext<CallToolRequestParams> request,

        IServiceProvider? services = null) =>

        Assert.Throws<InputRequiredException>(() =>

            ToolsetInvoker.InvokeRaw(method, null, request, services ?? EmptyServices, CancellationToken.None));



    public static McpInvocationResponse InvokeToResponse(

        MethodInfo method,

        RequestContext<CallToolRequestParams> request,

        JsonElement? outputSchema = null,

        IServiceProvider? services = null)

    {

        var response = ToolsetInvoker.InvokeToResponse(

            method,

            null,

            request,

            services ?? EmptyServices,

            outputSchema,

            CancellationToken.None);

        return response;

    }



    public static object? InvokeRaw(

        MethodInfo method,

        RequestContext<CallToolRequestParams> request,

        IServiceProvider? services = null) =>

        ToolsetInvoker.InvokeRaw(method, null, request, services ?? EmptyServices, CancellationToken.None);

}


