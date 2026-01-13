using Microsoft.Extensions.Logging;

namespace ZLogger.RichTextBox.Winforms;

/// <summary>
/// ZLogger provider that outputs to a RichTextBox control with colored RTF formatting.
/// </summary>
[ProviderAlias("ZLoggerRichTextBox")]
public sealed class ZLoggerRichTextBoxLoggerProvider(
    System.Windows.Forms.RichTextBox richTextBox,
    ZLoggerRichTextBoxOptions options) : ILoggerProvider, ISupportExternalScope, IAsyncDisposable
{
    private readonly ZLoggerRichTextBoxOptions _options = options ?? throw new ArgumentNullException(nameof(options));
    private IExternalScopeProvider? _scopeProvider;

    public ZLoggerRichTextBoxProcessor Processor { get; } = new(richTextBox, options);

    public ILogger CreateLogger(string categoryName)
    {
        return new ZLoggerLogger(
            categoryName,
            Processor,
            _options,
            _options.IncludeScopes ? _scopeProvider : null);
    }

    public void SetScopeProvider(IExternalScopeProvider scopeProvider)
    {
        _scopeProvider = scopeProvider;
    }

    public void Dispose()
    {
        Processor.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        return Processor.DisposeAsync();
    }
}
