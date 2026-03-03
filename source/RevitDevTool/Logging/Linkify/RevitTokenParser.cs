using RevitDevTool.Logging.Enums;
using Serilog.Sinks.RichTextBoxForms.Tokens;

namespace RevitDevTool.Logging.Linkify;

internal static class RevitTokenParser
{
    private const string UriScheme = "revitlog";
    private const string ElementIdPath = "elementid";
    private const string UniqueIdPath = "uniqueid";
    private const string IfcGuidPath = "ifcguid";

    internal static bool TryParseTokenString(string text, out string kind, out string normalizedValue)
    {
        kind = string.Empty;
        normalizedValue = string.Empty;
        const int uniqueIdLength = 45;
        const int ifcGuidLength = 22;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();

        switch (trimmed.Length)
        {
            case uniqueIdLength when LooksLikeRevitUniqueId(trimmed):
                kind = nameof(RevitTokenKind.UniqueId);
                normalizedValue = trimmed;
                return true;
            case ifcGuidLength when trimmed.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '$'):
                kind = nameof(RevitTokenKind.IfcGuid);
                normalizedValue = trimmed;
                return true;
            default:
                return false;
        }
    }

    private static bool LooksLikeRevitUniqueId(string value)
    {
        // Revit UniqueId shape:
        // 8-4-4-4-12-8 (hex segments)
        // Example: 2f6f2f4a-25b6-4f6f-9f95-4f1d8b27f6cb-00012345
        if (value.Length != 45)
        {
            return false;
        }

        return value[8] == '-' &&
               value[13] == '-' &&
               value[18] == '-' &&
               value[23] == '-' &&
               value[36] == '-' &&
               IsHexSegment(value, 0, 8) &&
               IsHexSegment(value, 9, 4) &&
               IsHexSegment(value, 14, 4) &&
               IsHexSegment(value, 19, 4) &&
               IsHexSegment(value, 24, 12) &&
               IsHexSegment(value, 37, 8);
    }

    private static bool IsHexSegment(string value, int start, int length)
    {
        for (var i = 0; i < length; i++)
        {
            var c = value[start + i];
            var isHex = c 
                is >= '0' and <= '9' 
                or >= 'a' and <= 'f' 
                or >= 'A' and <= 'F';
            if (!isHex)
            {
                return false;
            }
        }

        return true;
    }

    internal static bool TryBuildUri(DetectedToken? token, out string uri)
    {
        if (token is null)
        {
            uri = string.Empty;
            return false;
        }

        var path = token.Kind switch
        {
            nameof(RevitTokenKind.ElementId) => ElementIdPath,
            nameof(RevitTokenKind.UniqueId) => UniqueIdPath,
            nameof(RevitTokenKind.IfcGuid)=> IfcGuidPath,
            _ => null
        };

        if (path is null)
        {
            uri = string.Empty;
            return false;
        }

        uri = $"{UriScheme}://{path}/{Uri.EscapeDataString(token.NormalizedValue)}";
        return true;
    }

    internal static bool TryParseUri(string uriText, out string kind, out string normalizedValue)
    {
        kind = string.Empty;
        normalizedValue = string.Empty;

        if (!Uri.TryCreate(uriText, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!string.Equals(uri.Scheme, UriScheme, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (uri.Segments.Length < 2)
        {
            return false;
        }

        var category = uri.Host;
        normalizedValue = Uri.UnescapeDataString(uri.Segments[1].Trim('/'));
        kind = category.ToLowerInvariant() switch
        {
            ElementIdPath => nameof(RevitTokenKind.ElementId),
            UniqueIdPath => nameof(RevitTokenKind.UniqueId),
            IfcGuidPath => nameof(RevitTokenKind.IfcGuid),
            _ => string.Empty
        };

        return category.Equals(ElementIdPath, StringComparison.OrdinalIgnoreCase) ||
               category.Equals(UniqueIdPath, StringComparison.OrdinalIgnoreCase) ||
               category.Equals(IfcGuidPath, StringComparison.OrdinalIgnoreCase);
    }

    internal static string BuildUniqueKey(DetectedToken token)
    {
        return $"{token.Kind}:{token.NormalizedValue}";
    }
}
