using System.Text;
using Microsoft.Extensions.Logging;
using RevitDevTool.Scintilla.Core;
using RevitDevTool.Scintilla.Formatting;
using RevitDevTool.Scintilla.Internal;
using RevitDevTool.Scintilla.Render;

namespace RevitDevTool.Scintilla.Tests;

public sealed class RenderFormattingTests
{
    [Fact]
    public void PrettyJson_MultiObject_FormatsInlineWithoutArrayBlock()
    {
        var formatter = new JsonValueFormatter(enablePrettyJson: true, callbacks: null, errorSink: null);
        var message =
            "multi-object sample: primary=RevitDevTool.Scintilla.Demo.DemoStructuredPayload; secondary=RevitDevTool.Scintilla.Demo.DemoStructuredPayload";

        var payload = new object[]
        {
            new Dictionary<string, object?> { ["UserId"] = "class-user-01", ["RetryCount"] = 3 },
            new Dictionary<string, object?> { ["UserId"] = "class-user-02", ["RetryCount"] = 1 }
        };

        var context = CreateContext(
            message,
            new Dictionary<string, object?>
            {
                [LogPropertyKeys.StructuredPayloadObject] = payload,
                [LogPropertyKeys.StructuredPayloadTypeNames] = new[]
                {
                    "RevitDevTool.Scintilla.Demo.DemoStructuredPayload",
                    "RevitDevTool.Scintilla.Demo.DemoStructuredPayload"
                }
            });

        var ok = formatter.TryGetPrettyPrintedMessage(context, message, out var pretty);

        Assert.True(ok);
        Assert.Contains("primary=", pretty, StringComparison.Ordinal);
        Assert.Contains("; secondary=", pretty, StringComparison.Ordinal);
        Assert.DoesNotContain("[\r\n  {", pretty, StringComparison.Ordinal);
        Assert.DoesNotContain("[\n  {", pretty, StringComparison.Ordinal);
    }

    [Fact]
    public void PrettyJsonFalse_TypeName_IsStyledAsJsonString()
    {
        var options = new ScintillaLogViewerOptions
        {
            EnablePrettyJson = false
        };
        var strategy = new LogRenderStrategy(
            "Cascadia Mono",
            10,
            new StaticLogThemeProvider(ScintillaTheme.EnhancedDark),
            DefaultLogStyleRegistry.Instance,
            options);

        var typeName = "RevitDevTool.Scintilla.Demo.DemoStructuredPayload";
        var message = $"multi-object sample: primary={typeName}; secondary={typeName}";
        var entry = CreateEntry(
            message,
            new Dictionary<string, object?>
            {
                [LogPropertyKeys.StructuredPayloadObject] = new object[] { new { A = 1 }, new { B = 2 } },
                [LogPropertyKeys.StructuredPayloadTypeNames] = new[] { typeName, typeName }
            });

        var segments = new List<RenderSegment>();
        strategy.BuildSegments(entry, segments);

        var expectedTypeBytes = Encoding.UTF8.GetByteCount(typeName);
        Assert.Contains(segments, s => s.SemanticStyle == LogSemanticStyle.JsonString && s.Utf8Length == expectedTypeBytes);
    }

    private static LogRenderContext CreateContext(string message, IReadOnlyDictionary<string, object?> properties)
    {
        var bytes = Encoding.UTF8.GetBytes(message);
        return new LogRenderContext(
            DateTime.UtcNow,
            LogLevel.Information,
            source: "tests",
            message,
            bytes,
            exceptionText: null,
            properties);
    }

    private static LogEntry CreateEntry(string message, IReadOnlyDictionary<string, object?> properties)
    {
        var bytes = Encoding.UTF8.GetBytes(message);
        return new LogEntry
        {
            TimestampUtc = DateTime.UtcNow,
            Level = LogLevel.Information,
            Source = "tests",
            Message = new ArraySegment<byte>(bytes),
            Properties = properties
        };
    }
}
