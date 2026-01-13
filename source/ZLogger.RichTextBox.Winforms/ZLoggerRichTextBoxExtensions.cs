using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
// ReSharper disable ConvertToExtensionBlock

namespace ZLogger.RichTextBox.Winforms;

[PublicAPI]
public static class ZLoggerRichTextBoxExtensions
{
    public static ILoggingBuilder AddZLoggerRichTextBox(
        this ILoggingBuilder builder,
        System.Windows.Forms.RichTextBox richTextBox)
    {
        return builder.AddZLoggerRichTextBox(richTextBox, _ => { });
    }

    public static ILoggingBuilder AddZLoggerRichTextBox(
        this ILoggingBuilder builder,
        System.Windows.Forms.RichTextBox richTextBox,
        Action<ZLoggerRichTextBoxOptions> configure)
    {
        return builder.AddZLoggerRichTextBox(richTextBox, (options, _) => configure(options));
    }

    public static ILoggingBuilder AddZLoggerRichTextBox(
        this ILoggingBuilder builder,
        System.Windows.Forms.RichTextBox richTextBox,
        Action<ZLoggerRichTextBoxOptions, IServiceProvider> configure)
    {
        builder.Services.AddSingleton<ILoggerProvider, ZLoggerRichTextBoxLoggerProvider>(serviceProvider =>
        {
            var options = new ZLoggerRichTextBoxOptions();
            configure(options, serviceProvider);
            return new ZLoggerRichTextBoxLoggerProvider(richTextBox, options);
        });

        return builder;
    }

    public static ILoggingBuilder AddZLoggerRichTextBox(
        this ILoggingBuilder builder,
        System.Windows.Forms.RichTextBox richTextBox,
        out ZLoggerRichTextBoxLoggerProvider provider,
        Action<ZLoggerRichTextBoxOptions>? configure = null)
    {
        var options = new ZLoggerRichTextBoxOptions();
        configure?.Invoke(options);

        provider = new ZLoggerRichTextBoxLoggerProvider(richTextBox, options);
        builder.Services.AddSingleton<ILoggerProvider>(provider);

        return builder;
    }

    /// <summary>
    /// Adds a ZLogger RichTextBox provider WITHOUT registering it in DI.
    /// Use this when the caller owns the provider lifecycle (Single Owner Pattern).
    /// The provider must be disposed manually by the caller.
    /// </summary>
    public static ILoggingBuilder AddZLoggerRichTextBoxUnmanaged(
        this ILoggingBuilder builder,
        System.Windows.Forms.RichTextBox richTextBox,
        out ZLoggerRichTextBoxLoggerProvider provider,
        Action<ZLoggerRichTextBoxOptions>? configure = null)
    {
        var options = new ZLoggerRichTextBoxOptions();
        configure?.Invoke(options);

        provider = new ZLoggerRichTextBoxLoggerProvider(richTextBox, options);

        // Add provider directly without DI registration
        // This prevents double-ownership: caller owns lifecycle, not the factory
        builder.AddProvider(provider);

        return builder;
    }
}
