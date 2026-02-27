using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RevitDevTool.Scintilla.Contracts;

namespace RevitDevTool.Scintilla.Logging;

public static class ScintillaLoggingBuilderExtensions
{
    public static ILoggingBuilder AddZLoggerScintilla(this ILoggingBuilder builder, ILogIngress ingress)
        => AddZLoggerScintilla(builder, _ => ingress, _ => { });

    public static ILoggingBuilder AddZLoggerScintilla(
        this ILoggingBuilder builder,
        ILogIngress ingress,
        Action<ScintillaLoggerOptions> configure)
        => AddZLoggerScintilla(builder, _ => ingress, configure);

    public static ILoggingBuilder AddZLoggerScintilla(
        this ILoggingBuilder builder,
        Func<IServiceProvider, ILogIngress> ingressFactory)
        => AddZLoggerScintilla(builder, ingressFactory, _ => { });

    public static ILoggingBuilder AddZLoggerScintilla(
        this ILoggingBuilder builder,
        Func<IServiceProvider, ILogIngress> ingressFactory,
        Action<ScintillaLoggerOptions> configure)
    {
        EnsureNotNull(builder, nameof(builder));
        EnsureNotNull(ingressFactory, nameof(ingressFactory));
        EnsureNotNull(configure, nameof(configure));

        builder.Services.AddSingleton<ILoggerProvider>(sp =>
        {
            var options = new ScintillaLoggerOptions();
            configure(options);
            return new ScintillaLoggerProvider(ingressFactory(sp), options);
        });

        return builder;
    }

    private static void EnsureNotNull(object? value, string paramName)
    {
        if (value is null)
            throw new ArgumentNullException(paramName);
    }
}
