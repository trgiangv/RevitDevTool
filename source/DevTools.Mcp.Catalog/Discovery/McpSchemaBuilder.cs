namespace DevTools.Mcp.Catalog.Discovery;
using JsonTypes = McpSpecKeys.JsonSchema.Types;

/// <summary>
/// Shared CLR → JSON Schema type mapping for discovery paths that cannot use
/// SDK <c>McpServerTool.Create</c> (MetadataLoadContext assembly parsing).
/// Daemon and host built-in tools should prefer SDK Create for schema + invoke.
/// </summary>
public static class McpSchemaBuilder
{
    private const string NullableGenericFullName = "System.Nullable`1";

    /// <summary>
    /// Maps a CLR type (including MetadataLoadContext types matched by FullName)
    /// to a JSON Schema primitive type name.
    /// </summary>
    public static string FromClrType(Type type)
    {
        type = UnwrapNullable(type);
        var fullName = type.FullName ?? type.Name;
        return fullName switch
        {
            "System.String" => JsonTypes.String,
            "System.Int32" or "System.Int64" or "System.Int16" or "System.Byte" or "System.SByte" or "System.UInt16"
                or "System.UInt32" or "System.UInt64" => JsonTypes.Integer,
            "System.Double" or "System.Single" or "System.Decimal" => JsonTypes.Number,
            "System.Boolean" => JsonTypes.Boolean,
            _ => JsonTypes.String
        };
    }

    private static Type UnwrapNullable(Type type)
    {
        while (true)
        {
            if (!type.IsGenericType || type.GetGenericArguments().Length == 0)
                return type;

            var genericDefFullName = type.GetGenericTypeDefinition().FullName;
            if (!string.Equals(genericDefFullName, NullableGenericFullName, StringComparison.Ordinal)
                && !string.Equals(genericDefFullName, typeof(Nullable<>).FullName, StringComparison.Ordinal))
                return type;

            type = type.GetGenericArguments()[0];
        }
    }
}
