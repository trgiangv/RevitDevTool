using Microsoft.Extensions.Logging;
using Xunit;

namespace RevitDevTool.RichTextBox.Colored.Tests.Integration;

public class BasicFormattingTests : ZLoggerRichTextBoxTestBase
{
    [Fact]
    public void Render_WithSimpleMessage_FormatsCorrectly()
    {
        var entry = CreateLogEntry(LogLevel.Information, "Hello World");

        var text = RenderAndGetText(entry);

        Assert.Contains("INF", text);
        Assert.Contains("Hello World", text);
    }

    [Fact]
    public void Render_WithDifferentLogLevels_FormatsCorrectly()
    {
        var traceEntry = CreateLogEntry(LogLevel.Trace, "Trace message");
        var debugEntry = CreateLogEntry(LogLevel.Debug, "Debug message");
        var infoEntry = CreateLogEntry(LogLevel.Information, "Info message");
        var warnEntry = CreateLogEntry(LogLevel.Warning, "Warning message");
        var errorEntry = CreateLogEntry(LogLevel.Error, "Error message");
        var criticalEntry = CreateLogEntry(LogLevel.Critical, "Critical message");

        Assert.Contains("TRC", RenderAndGetText(traceEntry));
        Assert.Contains("DBG", RenderAndGetText(debugEntry));
        Assert.Contains("INF", RenderAndGetText(infoEntry));
        Assert.Contains("WRN", RenderAndGetText(warnEntry));
        Assert.Contains("ERR", RenderAndGetText(errorEntry));
        Assert.Contains("CRT", RenderAndGetText(criticalEntry));
    }

    [Fact]
    public void Render_WithException_IncludesExceptionDetails()
    {
        var exception = new InvalidOperationException("Test exception");
        var entry = CreateLogEntry(LogLevel.Error, "Error occurred", exception);

        var text = RenderAndGetText(entry);

        Assert.Contains("Error occurred", text);
        Assert.Contains("InvalidOperationException", text);
        Assert.Contains("Test exception", text);
    }

    [Fact]
    public void Render_WithNestedExceptions_IncludesAllExceptionDetails()
    {
        var innerException = new ArgumentException("Inner exception");
        var outerException = new InvalidOperationException("Outer exception", innerException);
        var entry = CreateLogEntry(LogLevel.Error, "Nested error", outerException);

        var text = RenderAndGetText(entry);

        Assert.Contains("Nested error", text);
        Assert.Contains("InvalidOperationException", text);
        Assert.Contains("Outer exception", text);
        Assert.Contains("ArgumentException", text);
        Assert.Contains("Inner exception", text);
    }

    [Fact]
    public void Render_WithTimestampFormat_FormatsCorrectly()
    {
        var entry = CreateLogEntry(LogLevel.Information, "Test message");

        var text = RenderAndGetText(entry, "[{Timestamp:yyyy-MM-dd HH:mm:ss}] {Message}");

        // Check that timestamp format is applied (should have date format)
        Assert.Matches(@"\[\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\]", text);
        Assert.Contains("Test message", text);
    }

    [Fact]
    public void Render_WithCategory_FormatsCorrectly()
    {
        var entry = CreateLogEntry(LogLevel.Information, "Test message");

        var text = RenderAndGetText(entry, "[{Category}] {Message}");

        Assert.Contains("[TestCategory]", text);
        Assert.Contains("Test message", text);
    }

    [Fact]
    public void Render_WithNewLine_AddsNewLine()
    {
        var entry = CreateLogEntry(LogLevel.Information, "Test message");

        var text = RenderAndGetText(entry, "{Message}{NewLine}End");

        Assert.Contains("Test message", text);
        // RichTextBox normalizes newlines to \n
        Assert.Contains("\n", text);
        Assert.Contains("End", text);
    }

    [Fact]
    public void Render_WithLiteralText_IncludesLiteralText()
    {
        var entry = CreateLogEntry(LogLevel.Information, "Test message");

        var text = RenderAndGetText(entry, "PREFIX: {Message} :SUFFIX");

        Assert.Contains("PREFIX:", text);
        Assert.Contains("Test message", text);
        Assert.Contains(":SUFFIX", text);
    }
}
