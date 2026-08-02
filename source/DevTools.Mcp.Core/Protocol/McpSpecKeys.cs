namespace DevTools.Mcp.Core.Protocol;

/// <summary>
/// Canonical MCP specification wire keys and protocol constants.
/// Single source of truth for JSON property names (camelCase) per MCP spec.
/// </summary>
public static class McpSpecKeys
{
    /// <summary>JSON-RPC method names for MCP host handler dispatch.</summary>
    public static class Methods
    {
        public const string Initialize = "initialize";
        public const string Initialized = "notifications/initialized";
        public const string Ping = "ping";
        public const string ToolsList = "tools/list";
        public const string ToolsCall = "tools/call";
        public const string ResourcesList = "resources/list";
        public const string ResourcesTemplatesList = "resources/templates/list";
        public const string ResourcesRead = "resources/read";
        public const string ServerDiscover = "server/discover";
    }

    /// <summary>JSON-RPC 2.0 envelope (<see href="https://www.jsonrpc.org/specification"/>).</summary>
    public static class JsonRpc
    {
        public const string Version = "2.0";
        public const string Envelope = "jsonrpc";
        public const string Id = "id";
        public const string Method = "method";
        public const string Params = "params";
        public const string Result = "result";
        public const string Error = "error";
        public const string Code = "code";
        public const string Message = "message";

        public const int InvalidRequest = -32600;
        public const int MethodNotFound = -32601;
        public const int InvalidParams = -32602;
        public const int InternalError = -32603;
        public const int UnsupportedProtocolVersion = -32022;
    }

    /// <summary>Per-request <c>_meta</c> keys (MCP 2026-07-28, SEP-2575).</summary>
    public static class Meta
    {
        public const string Key = "_meta";
        public const string ProtocolVersion = "io.modelcontextprotocol/protocolVersion";
    }

    /// <summary><c>server/discover</c> result fields (SEP-2575).</summary>
    public static class Discover
    {
        public const string SupportedVersions = "supportedVersions";
        public const string TtlMs = "ttlMs";
        public const string CacheScope = "cacheScope";
        public const string ResultType = "resultType";
        public const string Complete = "complete";
        public const string PrivateCacheScope = "private";
    }

    /// <summary>Host wire protocol version (MCP C# SDK 2.0 default).</summary>
    public static class ProtocolVersions
    {
        public const string Current = "2026-07-28";
    }

    /// <summary><c>initialize</c> request/result fields.</summary>
    public static class Initialize
    {
        public const string ProtocolVersion = "protocolVersion";
        public const string Capabilities = "capabilities";
        public const string ClientInfo = "clientInfo";
        public const string ServerInfo = "serverInfo";
    }

    /// <summary>Server/client capability objects.</summary>
    public static class Capabilities
    {
        public const string Tools = "tools";
        public const string Resources = "resources";
        public const string ListChanged = "listChanged";
        public const string Subscribe = "subscribe";
    }

    /// <summary><c>Implementation</c> info (client/server).</summary>
    public static class Implementation
    {
        public const string Name = "name";
        public const string Version = "version";
    }

    /// <summary><c>tools/list</c>, <c>tools/call</c>, and tool descriptors.</summary>
    public static class Tools
    {
        public const string List = "tools";
        public const string Name = "name";
        public const string Title = "title";
        public const string Description = "description";
        public const string InputSchema = "inputSchema";
        public const string OutputSchema = "outputSchema";
        public const string Annotations = "annotations";
        public const string Arguments = "arguments";
        public const string InputResponses = "inputResponses";
        public const string RequestState = "requestState";
        public const string Meta = "_meta";
        public const string ProgressToken = "progressToken";
    }

    /// <summary><c>tools/call</c> result shape.</summary>
    public static class ToolResult
    {
        public const string Content = "content";
        public const string ContentPascal = "Content";
        public const string IsError = "isError";
        public const string StructuredContent = "structuredContent";
    }

    /// <summary>Content block fields (<c>TextContent</c>, etc.).</summary>
    public static class Content
    {
        public const string Type = "type";
        public const string Text = "text";
    }

    /// <summary>Content block <c>type</c> discriminator values per MCP spec.</summary>
    public static class ContentBlockTypes
    {
        public const string Text = "text";
        public const string Image = "image";
        public const string Audio = "audio";
        public const string Resource = "resource";
        public const string ResourceLink = "resource_link";
    }

    /// <summary>Icon descriptor fields on tools, resources, prompts, and resource templates.</summary>
    public static class Icon
    {
        public const string List = "icons";
        public const string Src = "src";
        public const string MimeType = "mimeType";
        public const string Sizes = "sizes";
    }

    /// <summary>MRTR <c>resultType</c> discriminator on incomplete tool results (MCP 2026+).</summary>
    public static class ResultType
    {
        public const string Key = "resultType";
        public const string InputRequired = "input_required";
        public const string Complete = "complete";
    }

    /// <summary><c>resources/list</c>, <c>resources/read</c>, and resource descriptors.</summary>
    public static class Resources
    {
        public const string List = "resources";
        public const string ResourceTemplates = "resourceTemplates";
        public const string Uri = "uri";
        public const string UriTemplate = "uriTemplate";
        public const string MimeType = "mimeType";
        public const string Size = "size";
    }

    /// <summary>JSON Schema draft keys used in tool <c>inputSchema</c> / <c>outputSchema</c>.</summary>
    public static class JsonSchema
    {
        public const string Type = "type";
        public const string Properties = "properties";
        public const string Required = "required";
        public const string Description = "description";
        public const string Title = "title";

        public static class Types
        {
            public const string Boolean = "boolean";
            public const string Integer = "integer";
            public const string Number = "number";
            public const string Object = "object";
            public const string String = "string";
        }
    }

    /// <summary>
    /// Named argument keys on SDK <c>McpServerTool*</c> / <c>McpServerResource*</c> attributes (metadata reflection).
    /// </summary>
    public static class SdkAttributes
    {
        public const string Name = nameof(Name);
        public const string Title = nameof(Title);
        public const string Description = nameof(Description);
        public const string IconSource = nameof(IconSource);
        public const string UriTemplate = nameof(UriTemplate);
        public const string MimeType = nameof(MimeType);
        public const string UseStructuredContent = nameof(UseStructuredContent);
        public const string ReadOnly = nameof(ReadOnly);
        public const string Destructive = nameof(Destructive);
        public const string Idempotent = nameof(Idempotent);
        public const string OpenWorld = nameof(OpenWorld);
        public const string JsonValue = nameof(JsonValue);
    }
}
