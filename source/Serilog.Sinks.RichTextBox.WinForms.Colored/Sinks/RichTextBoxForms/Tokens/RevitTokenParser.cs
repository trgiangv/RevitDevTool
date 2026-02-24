#region Copyright 2025 Simon Vonhoff & Contributors

//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//

#endregion

using System.Globalization;
using System.Reflection;

namespace Serilog.Sinks.RichTextBoxForms.Tokens;

internal static class RevitTokenParser
{
    private const string UriScheme = "revitlog";
    private const string ElementIdPath = "elementid";
    private const string UniqueIdPath = "uniqueid";
    private const string IfcGuidPath = "ifcguid";

    internal static bool TryParseElementIdObject(object value, out string elementId)
    {
        elementId = string.Empty;

        var type = value.GetType();
        if (!string.Equals(type.Name, "ElementId", StringComparison.Ordinal))
        {
            return false;
        }

        var integerValueProperty = type.GetProperty("IntegerValue", BindingFlags.Public | BindingFlags.Instance);
        if (integerValueProperty?.GetValue(value) is int intValue)
        {
            elementId = intValue.ToString(CultureInfo.InvariantCulture);
            return true;
        }

        var valueProperty = type.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
        if (valueProperty?.GetValue(value) is long longValue)
        {
            elementId = longValue.ToString(CultureInfo.InvariantCulture);
            return true;
        }

        return false;
    }

    internal static bool TryParseTokenString(string text, out RevitTokenKind kind, out string normalizedValue)
    {
        kind = default;
        normalizedValue = string.Empty;
        const int maxElementIdLength = 19; // Max length of a 64-bit integer in decimal representation
        const int uniqueIdLength = 45; // Length of a Revit UniqueId
        const int ifcGuidLength = 22; // Length of an IFC GUID

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();

        switch (trimmed.Length)
        {
            case > 0 and <= maxElementIdLength when trimmed.All(char.IsDigit):
                kind = RevitTokenKind.ElementId;
                normalizedValue = trimmed;
                return true;
            case uniqueIdLength when trimmed.Count(c => c == '-') == 5:
                kind = RevitTokenKind.UniqueId;
                normalizedValue = trimmed;
                return true;
            case ifcGuidLength when trimmed.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '$'):
                kind = RevitTokenKind.IfcGuid;
                normalizedValue = trimmed;
                return true;
            default:
                return false;
        }
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
            RevitTokenKind.ElementId => ElementIdPath,
            RevitTokenKind.UniqueId => UniqueIdPath,
            RevitTokenKind.IfcGuid => IfcGuidPath,
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

    internal static bool TryParseUri(string uriText, out RevitTokenKind kind, out string normalizedValue)
    {
        kind = default;
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
            ElementIdPath => RevitTokenKind.ElementId,
            UniqueIdPath => RevitTokenKind.UniqueId,
            IfcGuidPath => RevitTokenKind.IfcGuid,
            _ => default
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