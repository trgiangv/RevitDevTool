using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RevitDevTool.Models;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.RichTextBox.Themes;

namespace RevitDevTool.ViewModel;

internal partial class TraceLogViewModel : ObservableObject, IDisposable
{
    public RichTextBox LogTextBox { get; }

    private readonly LoggingLevelSwitch _levelSwitch;
    private readonly SerilogTraceListener _traceListener;
    private readonly ConsoleRedirector _consoleRedirector;

    [ObservableProperty] private bool _isStarted = true;
    [ObservableProperty] private LogEventLevel _logLevel = LogEventLevel.Debug;

    partial void OnLogLevelChanged(LogEventLevel value)
    {
        _levelSwitch.MinimumLevel = value;
    }

    partial void OnIsStartedChanged(bool value)
    {
        TraceStatus(value);
    }
    
    private void TraceStatus(bool isStarted)
    {
        if (isStarted)
        {
            Trace.Listeners.Add(_traceListener);
            Trace.Listeners.Add(TraceGeometry.TraceListener);
            VisualizationController.Start();
        }
        else
        {
            Trace.Listeners.Remove(_traceListener);
            Trace.Listeners.Remove(TraceGeometry.TraceListener);
            VisualizationController.Stop();
        }
    }

    public TraceLogViewModel()
    {
        LogTextBox = new RichTextBox
        {
            FontFamily = new FontFamily("Cascadia Mono, Consolas, Courier New, monospace"),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalContentAlignment = VerticalAlignment.Top,
            IsReadOnly = true,
        };
        
        PresentationTraceSources.ResourceDictionarySource.Switch.Level = SourceLevels.Critical;
        _levelSwitch = new LoggingLevelSwitch(_logLevel);

        var logger = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(_levelSwitch)
            .WriteTo.RichTextBox(LogTextBox, theme: RichTextBoxConsoleTheme.Colored)
            .CreateLogger();
        
        _traceListener = new SerilogTraceListener(logger);
        _consoleRedirector = new ConsoleRedirector();
        TraceStatus(IsStarted);
    }

    [RelayCommand] private void Clear()
    {
        LogTextBox.Document.Blocks.Clear();
    }
    
    [RelayCommand] private static void ClearGeometry()
    {
        VisualizationController.Clear();
    }

    public void Dispose()
    {
        _consoleRedirector.Dispose();
        GC.SuppressFinalize(this);
    }
}