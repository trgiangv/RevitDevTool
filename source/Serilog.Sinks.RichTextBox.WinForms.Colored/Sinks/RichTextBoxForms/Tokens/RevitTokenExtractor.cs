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

using Serilog.Events;
using System.Globalization;

namespace Serilog.Sinks.RichTextBoxForms.Tokens;

internal static class RevitTokenExtractor
{
    internal static List<DetectedToken> Extract(LogEvent logEvent)
    {
        var tokens = new List<DetectedToken>();
        foreach (var property in logEvent.Properties)
        {
            ExtractValue(tokens, property.Value, property.Key, logEvent);
        }

        return tokens;
    }

    private static void ExtractValue(List<DetectedToken> tokens, LogEventPropertyValue value, string path, LogEvent logEvent)
    {
        switch (value)
        {
            case ScalarValue scalar:
                ExtractScalar(tokens, scalar, path, logEvent);
                return;

            case SequenceValue sequence:
                for (var i = 0; i < sequence.Elements.Count; i++)
                {
                    ExtractValue(tokens, sequence.Elements[i], $"{path}[{i}]", logEvent);
                }
                return;

            case StructureValue structure:
                foreach (var property in structure.Properties)
                {
                    ExtractValue(tokens, property.Value, $"{path}.{property.Name}", logEvent);
                }
                return;

            case DictionaryValue dictionary:
                foreach (var item in dictionary.Elements)
                {
                    var key = item.Key;
                    var itemValue = item.Value;
                    var dictionaryKey = key.Value?.ToString() ?? "null";
                    ExtractValue(tokens, itemValue, $"{path}[{dictionaryKey}]", logEvent);
                }
                return;
        }
    }

    private static void ExtractScalar(List<DetectedToken> tokens, ScalarValue scalar, string path, LogEvent logEvent)
    {
        var value = scalar.Value;
        if (value is null) return;

        if (RevitTokenParser.TryParseElementIdObject(value, out var elementIdText))
        {
            tokens.Add(CreateToken(RevitTokenKind.ElementId, elementIdText, elementIdText, path, logEvent));
            return;
        }

        if (value is string text)
        {
            if (RevitTokenParser.TryParseTokenString(text, out var kind, out var normalizedValue))
            {
                tokens.Add(CreateToken(kind, text, normalizedValue, path, logEvent));
            }

            return;
        }

        if (IsElementIdPropertyPath(path))
        {
            switch (value)
            {
                case int intValue:
                    tokens.Add(CreateToken(
                        RevitTokenKind.ElementId,
                        intValue.ToString(CultureInfo.InvariantCulture),
                        intValue.ToString(CultureInfo.InvariantCulture),
                        path,
                        logEvent));
                    return;

                case long longValue:
                    tokens.Add(CreateToken(
                        RevitTokenKind.ElementId,
                        longValue.ToString(CultureInfo.InvariantCulture),
                        longValue.ToString(CultureInfo.InvariantCulture),
                        path,
                        logEvent));
                    return;
            }
        }
    }

    private static bool IsElementIdPropertyPath(string path)
    {
        return path.EndsWith("ElementId", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith("ElementID", StringComparison.OrdinalIgnoreCase) ||
               path.Contains(".ElementId", StringComparison.OrdinalIgnoreCase);
    }

    private static DetectedToken CreateToken(RevitTokenKind kind, string rawValue, string normalizedValue, string path, LogEvent logEvent)
    {
        return new DetectedToken(
            kind,
            rawValue,
            normalizedValue,
            rawValue,
            path,
            logEvent.Timestamp,
            logEvent.Level);
    }
}