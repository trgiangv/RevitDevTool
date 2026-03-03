using RevitDevTool.Logging.Enums;
using Serilog.Events;
using Serilog.Sinks.RichTextBoxForms.Tokens;

namespace RevitDevTool.Logging.Linkify;

internal static class RevitTokenExtractor
{
    internal static List<DetectedToken> Extract(LogEvent logEvent)
    {
        var tokens = new List<DetectedToken>();
        foreach (var property in logEvent.Properties)
        {
            ExtractValue(tokens, property.Value, property.Key);
        }

        return tokens;
    }

    private static void ExtractValue(List<DetectedToken> tokens, LogEventPropertyValue value, string path)
    {
        switch (value)
        {
            case ScalarValue scalar:
                ExtractScalar(tokens, scalar, path);
                return;

            case SequenceValue sequence:
                for (var i = 0; i < sequence.Elements.Count; i++)
                {
                    ExtractValue(tokens, sequence.Elements[i], $"{path}[{i}]");
                }
                return;

            case StructureValue structure:
                foreach (var property in structure.Properties)
                {
                    ExtractValue(tokens, property.Value, $"{path}.{property.Name}");
                }
                return;

            case DictionaryValue dictionary:
                foreach (var item in dictionary.Elements)
                {
                    var key = item.Key;
                    var itemValue = item.Value;
                    var dictionaryKey = key.Value?.ToString() ?? "null";
                    ExtractValue(tokens, itemValue, $"{path}[{dictionaryKey}]");
                }
                return;
        }
    }

    private static void ExtractScalar(List<DetectedToken> tokens, ScalarValue scalar, string path)
    {
        var value = scalar.Value;
        switch (value)
        {
            case null:
                return;
            case ElementId elementId:
                tokens.Add(new DetectedToken(nameof(RevitTokenKind.ElementId),elementId.ToString()));
                return;
            case string text:
            {
                if (RevitTokenParser.TryParseTokenString(text, out var kind, out var normalizedValue))
                {
                    tokens.Add(new DetectedToken(kind, normalizedValue));
                }

                return;
            }
        }

        if (path.EndsWith("ElementId", StringComparison.OrdinalIgnoreCase))
        {
            tokens.Add(new DetectedToken(nameof(RevitTokenKind.ElementId), value.ToString()!));
        }
    }
}
