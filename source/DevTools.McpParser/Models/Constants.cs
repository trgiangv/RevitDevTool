namespace DevTools.McpParser.Models;

public static class McpPropertyNames
{
    // Bridge envelope
    public const string ErrorMessage = "errorMessage";
    public const string Id = "id";
    public const string IsError = "isError";
    public const string Method = "method";
    public const string Params = "params";
    public const string Result = "result";

    // MCP payloads and tool arguments
    public const string Arguments = "arguments";
    public const string Code = "code";
    public const string FilePath = "filePath";
    public const string HostApp = "hostApp";
    public const string HostInstanceId = "hostInstanceId";
    public const string LanguageCode = "languageCode";
    public const string Name = "name";
    public const string Uri = "uri";
    public const string VersionNumber = "versionNumber";

    // MCP content and JSON Schema
    public const string Content = "content";
    public const string Description = "description";
    public const string Properties = "properties";
    public const string Required = "required";
    public const string Text = "text";
    public const string Type = "type";
}

public static class JsonSchemaTypeNames
{
    public const string Boolean = "boolean";
    public const string Integer = "integer";
    public const string Number = "number";
    public const string Object = "object";
    public const string String = "string";
}