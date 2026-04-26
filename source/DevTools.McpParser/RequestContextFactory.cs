using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.McpParser;

public static class RequestContextFactory
{
    public static RequestContext<T> Create<T>(T requestParams, string method)
    {
        var jsonRpcRequest = new JsonRpcRequest
        {
            Id = new RequestId(Guid.NewGuid().ToString("N")),
            Method = method,
            Params = null
        };
        return new RequestContext<T>(null!, jsonRpcRequest, requestParams);
    }
}
