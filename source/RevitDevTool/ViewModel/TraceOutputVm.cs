using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RevitDevTool.UI.Binding;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.RichTextBox.Themes;
using Color = System.Windows.Media.Color;

namespace RevitDevTool.ViewModel;

public class TraceOutputVm : ObservableObject
{
    public bool IsStarted
    {
        get => _isStarted;
        private set => SetProperty(ref _isStarted, value);
    }

    public LogEventLevel LogLevel
    {
        get => _logLevel;
        set
        {
            _logLevel = value;
            _levelSwitch.MinimumLevel = value;
            OnPropertyChanged();
        }
    }

    private readonly LoggingLevelSwitch _levelSwitch;
    private bool _isStarted;
    private LogEventLevel _logLevel = LogEventLevel.Debug;
    private readonly global::SerilogTraceListener.SerilogTraceListener _listener;

    public RichTextBox LogTextBox { get; }

    public TraceOutputVm()
    {
        LogTextBox = new RichTextBox
        {
            Background = new SolidColorBrush(Color.FromRgb(29, 29, 31)),
            Foreground = new SolidColorBrush(Color.FromRgb(245, 245, 247)),
            FontFamily = new FontFamily("Cascadia Mono, Consolas, Courier New, monospace"),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalContentAlignment = VerticalAlignment.Top,
            IsReadOnly = true,
        };
        _levelSwitch = new LoggingLevelSwitch(_logLevel);
        var logger = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(_levelSwitch)
            .WriteTo.RichTextBox(LogTextBox, theme: RichTextBoxConsoleTheme.Literate)
            .CreateLogger();
        _listener = new global::SerilogTraceListener.SerilogTraceListener(logger) { Name = "RevitDevTool" };
    }

    public DelegateCommand ClearCommand => new()
    {
        ExecuteCommand = _ => { LogTextBox.Document.Blocks.Clear(); }
    };

    public DelegateCommand StatusCommand => new()
    {
        ExecuteCommand = _ =>
        {
            IsStarted = !IsStarted;
            if (IsStarted)
            {
                Trace.Listeners.Add(_listener);
            }
            else
            {
                Trace.Listeners.Remove(_listener);
            }
        }
    };
}