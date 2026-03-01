using Microsoft.Extensions.DependencyInjection;
using RevitDevTool.Scintilla.Control;
using RevitDevTool.Scintilla.Formatting;
using ZLogger;
namespace RevitDevTool.Scintilla.Extensions;

public sealed class ScintillaRegistrationOptions
{
    public Func<IServiceProvider, IScintillaLogViewHost> ViewHostResolver { get; set; }
        = static services => services.GetRequiredService<IScintillaLogViewHost>();
    public Func<IServiceProvider, ILogViewerControlEvents> ControlEventsResolver { get; set; }
        = static services => services.GetRequiredService<ILogViewerControlEvents>();
    public Action<ZLoggerOptions>? ConfigureZLogger { get; set; }
    public Action<ZLoggerOptions, IServiceProvider>? ConfigureZLoggerWithServices { get; set; }
    public Func<ILogViewerController, ILogViewerControlEvents, Action>? BindControllerEvents { get; set; }

    /// <summary>
    /// When <see langword="true"/> (default), automatically configures ZLogger to use
    /// <see cref="ScintillaLogFormatter"/> which
    /// produces compact UTF-8 output in the exact format the viewer's fast-path prefix parser
    /// expects, guaranteeing zero string allocation during style colouring.
    /// <para>
    /// Set to <see langword="false"/> and configure <see cref="ConfigureZLogger"/> to call
    /// <c>opts.UseFormatter(() => ...)</c> when you need a custom ZLogger formatter.
    /// </para>
    /// </summary>
    public bool UseScintillaFormatter { get; set; } = true;
}
