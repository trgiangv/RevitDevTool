namespace DevTools.Mcp.Core.Protocol;

/// <summary>
/// DevTools-owned JSON keys that the MCP C# SDK does not export as public constants.
/// Method names, <c>_meta</c> keys, and JSON-RPC error codes come from
/// <c>RequestMethods</c>, <c>NotificationMethods</c>, <c>MetaKeys</c>, and <c>McpErrorCode</c>.
/// </summary>
public static class McpSpecKeys
{
    /// <summary>JSON-RPC 2.0 envelope property names.</summary>
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
    }

    /// <summary>JSON-RPC <c>_meta</c> object key (SDK exports namespaced keys, not this wrapper).</summary>
    public static class Meta
    {
        public const string Key = "_meta";
    }

    /// <summary><c>server/discover</c> result fields (SEP-2575). SDK <c>DiscoverResult</c> is internal-shaped; host builds JsonNode by hand.</summary>
    public static class Discover
    {
        public const string SupportedVersions = "supportedVersions";
        public const string TtlMs = "ttlMs";
        public const string CacheScope = "cacheScope";
        public const string ResultType = "resultType";
        public const string Complete = "complete";
        public const string PrivateCacheScope = "private";
    }

    /// <summary>
    /// Host wire protocol version. SDK <c>McpProtocolVersions</c> is internal.
    /// </summary>
    public static class ProtocolVersions
    {
        public const string Current = "2026-07-28";
    }

    /// <summary><c>initialize</c> / discover payload fields not covered by SDK constants.</summary>
    public static class Initialize
    {
        public const string Capabilities = "capabilities";
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

    /// <summary><c>tools/call</c> params used when building Python / JsonNode payloads.</summary>
    public static class Tools
    {
        public const string Name = "name";
        public const string Arguments = "arguments";
        public const string InputResponses = "inputResponses";
        public const string RequestState = "requestState";
    }

    /// <summary><c>tools/call</c> result shape, including ALC PascalCase <c>Content</c>.</summary>
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

    /// <summary>MRTR <c>resultType</c> discriminator on incomplete tool results (MCP 2026+).</summary>
    public static class ResultType
    {
        public const string Key = "resultType";
        public const string InputRequired = "input_required";
    }

    /// <summary>Resource descriptor fields used when encoding content blocks by hand.</summary>
    public static class Resources
    {
        public const string Uri = "uri";
        public const string MimeType = "mimeType";
        public const string Size = "size";
    }

    /// <summary>JSON Schema draft keys used in tool <c>inputSchema</c> / <c>outputSchema</c>.</summary>
    public static class JsonSchema
    {
        public const string Type = "type";
        public const string Properties = "properties";
        public const string Required = "required";
        public const string Items = "items";
        public const string AdditionalProperties = "additionalProperties";
        public const string Description = "description";
        public const string Title = "title";

        public static class Types
        {
            public const string Boolean = "boolean";
            public const string Array = "array";
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
