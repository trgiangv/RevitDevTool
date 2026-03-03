using RevitDevTool.Logging.Enums;
using Serilog.Events;
using Serilog.Sinks.RichTextBoxForms.Tokens;

namespace RevitDevTool.Logging.Linkify;

internal sealed class RevitTokenDetector : ITokenDetector
{
    internal static RevitTokenDetector Instance { get; } = new();

    private RevitTokenDetector()
    {
    }

    public bool TryCreateToken(object? rawValue, out DetectedToken token)
    {
        switch (rawValue)
        {
            case null:
                token = null!;
                return false;
            case ElementId elementId:
                token = new DetectedToken(nameof(RevitTokenKind.ElementId),elementId.ToString());
                return true;
            case string str when TryCreateTokenFromString(str, out token):
                return true;
            default:
                token = null!;
                return false;
        }
    }

    public bool TryCreateTokenFromString(string rawValue, out DetectedToken token)
    {
        if (RevitTokenParser.TryParseTokenString(rawValue, out var kind, out var normalized))
        {
            token = new DetectedToken(kind, normalized);
            return true;
        }

        token = null!;
        return false;
    }

    public bool TryBuildUri(DetectedToken token, out string uri)
    {
        return RevitTokenParser.TryBuildUri(token, out uri);
    }

    public bool TryParseUri(string uriText, out DetectedToken token)
    {
        if (RevitTokenParser.TryParseUri(uriText, out var kind, out var normalizedValue))
        {
            token = new DetectedToken(kind, normalizedValue);
            return true;
        }

        token = null!;
        return false;
    }

    public IReadOnlyList<DetectedToken> Extract(LogEvent logEvent)
    {
        return RevitTokenExtractor.Extract(logEvent);
    }

    public string BuildUniqueKey(DetectedToken token)
    {
        return RevitTokenParser.BuildUniqueKey(token);
    }
}
