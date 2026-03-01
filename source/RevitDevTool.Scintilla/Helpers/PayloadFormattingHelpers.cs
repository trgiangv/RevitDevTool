using RevitDevTool.Scintilla.Formatting;
using RevitDevTool.Scintilla.Internal;
namespace RevitDevTool.Scintilla.Helpers;

internal static class PayloadFormattingHelpers
{
    public static IReadOnlyList<string> GetStructuredTypeNames(IReadOnlyDictionary<string, object?> properties)
    {
        if (properties.TryGetValue(LogPropertyKeys.StructuredPayloadTypeNames, out var many) &&
            many is string[] manyNames &&
            manyNames.Length > 0)
        {
            return manyNames;
        }

        if (properties.TryGetValue(LogPropertyKeys.StructuredPayloadTypeName, out var single) &&
            single is string singleName &&
            !string.IsNullOrWhiteSpace(singleName))
        {
            return new[] { singleName };
        }

        return Array.Empty<string>();
    }

    public static bool HasStructuredPayloadTypeMetadata(LogRenderContext context)
    {
        return context.Properties.ContainsKey(LogPropertyKeys.StructuredPayloadTypeName) ||
               context.Properties.ContainsKey(LogPropertyKeys.StructuredPayloadTypeNames);
    }

    public static bool IsStructuredPayloadTypeToken(LogRenderContext context, string tokenText)
    {
        if (string.IsNullOrWhiteSpace(tokenText))
            return false;

        var typeNames = GetStructuredTypeNames(context.Properties);
        for (var i = 0; i < typeNames.Count; i++)
        {
            if (string.Equals(typeNames[i], tokenText, StringComparison.Ordinal))
                return true;
        }

        return context.Properties.ContainsKey(LogPropertyKeys.StructuredPayloadObject) &&
               LooksLikeClrTypeName(tokenText);
    }

    private static bool LooksLikeClrTypeName(string token)
    {
        if (string.IsNullOrWhiteSpace(token) ||
            token.IndexOf('.') <= 0 ||
            token.IndexOf("://", StringComparison.Ordinal) >= 0 ||
            token.Contains("@", StringComparison.Ordinal))
        {
            return false;
        }

        var parts = token.Split('.');
        if (parts.Length < 2)
            return false;

        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            if (string.IsNullOrWhiteSpace(part))
                return false;
            if (!char.IsLetter(part[0]) || !char.IsUpper(part[0]))
                return false;
        }

        return true;
    }

}
