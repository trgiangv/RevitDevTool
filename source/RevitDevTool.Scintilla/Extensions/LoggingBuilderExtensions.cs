using Microsoft.Extensions.Logging;
using RevitDevTool.Scintilla.Control;
using RevitDevTool.Scintilla.Core;
using RevitDevTool.Scintilla.Formatting;
using RevitDevTool.Scintilla.Logger;
using RevitDevTool.Scintilla.Search;
using ZLogger;
namespace RevitDevTool.Scintilla.Extensions;

public static class LoggingBuilderExtensions
{
    public static ILoggingBuilder AddZLoggerScintilla(this ILoggingBuilder builder)
    {
        EnsureNotNull(builder, nameof(builder));
        return AddCore(builder, new ScintillaRegistrationOptions(), null);
    }

    public static ILoggingBuilder AddZLoggerScintilla(this ILoggingBuilder builder, Action<ZLoggerOptions> configureZLogger)
    {
        EnsureNotNull(builder, nameof(builder));
        EnsureNotNull(configureZLogger, nameof(configureZLogger));

        return AddCore(builder, new ScintillaRegistrationOptions { ConfigureZLogger = configureZLogger }, null);
    }

    public static ILoggingBuilder AddZLoggerScintilla(
        this ILoggingBuilder builder,
        ScintillaRegistrationOptions options)
    {
        EnsureNotNull(builder, nameof(builder));
        EnsureNotNull(options, nameof(options));

        return AddCore(builder, options, null);
    }

    public static ILoggingBuilder AddZLoggerScintilla(
        this ILoggingBuilder builder,
        Action<ScintillaRegistrationOptions> configure)
    {
        EnsureNotNull(builder, nameof(builder));
        EnsureNotNull(configure, nameof(configure));

        var options = new ScintillaRegistrationOptions();
        configure(options);

        return AddCore(builder, options, null);
    }

    public static ILoggingBuilder AddZLoggerScintilla(
        this ILoggingBuilder builder,
        Action<ScintillaRegistrationOptions, IServiceProvider> configure)
    {
        EnsureNotNull(builder, nameof(builder));
        EnsureNotNull(configure, nameof(configure));

        return AddCore(builder, new ScintillaRegistrationOptions(), configure);
    }

    private static ILoggingBuilder AddCore(
        ILoggingBuilder builder,
        ScintillaRegistrationOptions baseOptions,
        Action<ScintillaRegistrationOptions, IServiceProvider>? configureWithServices)
    {
        builder.AddZLoggerLogProcessor((zloggerOptions, serviceProvider) =>
        {
            var options = CloneOptions(baseOptions);
            configureWithServices?.Invoke(options, serviceProvider);

            var host = ResolveHost(options, serviceProvider);
            var controller = host.Controller;
            if (controller is not ILogEntrySink sink)
                throw new InvalidOperationException("ILogViewerController must implement internal log sink contract.");

            if (options.UseScintillaFormatter)
                zloggerOptions.UseFormatter(static () => new ScintillaLogFormatter());

            options.ConfigureZLogger?.Invoke(zloggerOptions);
            options.ConfigureZLoggerWithServices?.Invoke(zloggerOptions, serviceProvider);

            var controlEvents = ResolveControlEvents(options, serviceProvider);
            var bind = options.BindControllerEvents ?? BindDefaultControllerEvents;
            var unbind = bind(controller, controlEvents);

            var processor = new ScintillaLogProcessor(zloggerOptions, sink);
            return new ScintillaLogProcessorWithLifecycle(processor, unbind);
        });

        return builder;
    }

    private static ScintillaRegistrationOptions CloneOptions(ScintillaRegistrationOptions options)
    {
        return new ScintillaRegistrationOptions
        {
            ViewHostResolver = options.ViewHostResolver,
            ControlEventsResolver = options.ControlEventsResolver,
            ConfigureZLogger = options.ConfigureZLogger,
            ConfigureZLoggerWithServices = options.ConfigureZLoggerWithServices,
            BindControllerEvents = options.BindControllerEvents,
            UseScintillaFormatter = options.UseScintillaFormatter
        };
    }

    private static IScintillaLogViewHost ResolveHost(
        ScintillaRegistrationOptions options,
        IServiceProvider serviceProvider)
    {
        try
        {
            return options.ViewHostResolver(serviceProvider);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Unable to resolve IScintillaLogViewHost. " +
                "Call AddScintillaLogViewerWinForms(...) or AddScintillaLogViewerWpf(...) " +
                "before AddZLoggerScintilla(...), or provide a custom ViewHostResolver.",
                ex);
        }
    }

    private static ILogViewerControlEvents ResolveControlEvents(
        ScintillaRegistrationOptions options,
        IServiceProvider serviceProvider)
    {
        try
        {
            return options.ControlEventsResolver(serviceProvider);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Unable to resolve ILogViewerControlEvents. " +
                "Ensure viewer services are registered before AddZLoggerScintilla(...), " +
                "or provide a custom ControlEventsResolver.",
                ex);
        }
    }

    private static void EnsureNotNull<T>(T value, string paramName) where T : class
    {
        if (value is null)
            throw new ArgumentNullException(paramName);
    }

    private static Action BindDefaultControllerEvents(
        ILogViewerController controller,
        ILogViewerControlEvents controlEvents)
    {
        void OnStart() => controller.Start();
        void OnStop() => controller.Stop();
        void OnClear(ClearMode mode) => controller.Clear(mode);
        void OnAutoScroll(bool enabled) => controller.SetAutoScroll(enabled);
        void OnFilter(LogFilterOptions options) => controller.ApplyFilter(options);
        void OnSearch(LogSearchRequest request)
        {
            if (request.HighlightOnly)
            {
                controller.HighlightSearch(request.Pattern, request.MatchCase, request.UseRegex);
                return;
            }

            if (request.SearchBackward)
                controller.FindPrevious(request.Pattern, request.MatchCase, request.UseRegex);
            else
                controller.FindNext(request.Pattern, request.MatchCase, request.UseRegex);
        }

        controlEvents.StartRequested += OnStart;
        controlEvents.StopRequested += OnStop;
        controlEvents.ClearRequested += OnClear;
        controlEvents.AutoScrollChanged += OnAutoScroll;
        controlEvents.FilterRequested += OnFilter;
        controlEvents.SearchRequested += OnSearch;

        return () =>
        {
            controlEvents.StartRequested -= OnStart;
            controlEvents.StopRequested -= OnStop;
            controlEvents.ClearRequested -= OnClear;
            controlEvents.AutoScrollChanged -= OnAutoScroll;
            controlEvents.FilterRequested -= OnFilter;
            controlEvents.SearchRequested -= OnSearch;
        };
    }

}
