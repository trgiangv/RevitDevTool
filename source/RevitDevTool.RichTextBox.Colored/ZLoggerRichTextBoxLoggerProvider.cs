using Microsoft.Extensions.Logging;
using ZLogger;

namespace RevitDevTool.RichTextBox.Colored;

/// <summary>
/// ZLogger provider that outputs to a RichTextBox control with colored RTF formatting.
/// </summary>
[ProviderAlias("ZLoggerRichTextBox")]
public sealed class ZLoggerRichTextBoxLoggerProvider : ILoggerProvider, ISupportExternalScope, IAsyncDisposable
{
    private readonly ZLoggerRichTextBoxOptions _options;
    private readonly ZLoggerRichTextBoxProcessor _processor;
    private IExternalScopeProvider? _scopeProvider;

    public ZLoggerRichTextBoxLoggerProvider(
        System.Windows.Forms.RichTextBox richTextBox,
        ZLoggerRichTextBoxOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _processor = new ZLoggerRichTextBoxProcessor(richTextBox, options);
    }

    public ZLoggerRichTextBoxProcessor Processor => _processor;

    public ILogger CreateLogger(string categoryName)
    {
        return new ZLoggerLogger(
            categoryName,
            _processor,
            _options,
            _options.IncludeScopes ? _scopeProvider : null);
    }

    public void SetScopeProvider(IExternalScopeProvider scopeProvider)
    {
        _scopeProvider = scopeProvider;
    }

    public void Dispose()
    {
        _processor.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        return _processor.DisposeAsync();
    }
}
