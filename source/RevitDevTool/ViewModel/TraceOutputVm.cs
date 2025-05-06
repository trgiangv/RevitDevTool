using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RevitDevTool.Geometry;
using RevitDevTool.Handlers;
using RevitDevTool.Models;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.RichTextBox.Themes;

namespace RevitDevTool.ViewModel;

public partial class TraceOutputVm : ObservableObject, IDisposable
{
    public RichTextBox LogTextBox { get; }

    private readonly LoggingLevelSwitch _levelSwitch;
    private readonly SerilogTraceListener _traceListener;
    private readonly ConsoleRedirector _consoleRedirector;

    [ObservableProperty] private bool _isStarted = true;
    [ObservableProperty] private LogEventLevel _logLevel = LogEventLevel.Debug;

    partial void OnLogLevelChanging(LogEventLevel value)
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
        }
        else
        {
            Trace.Listeners.Remove(_traceListener);
            Trace.Listeners.Remove(TraceGeometry.TraceListener);
        }
    }

    public TraceOutputVm()
    {
        LogTextBox = new RichTextBox
        {
            FontFamily = new FontFamily("Cascadia Mono, Consolas, Courier New, monospace"),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalContentAlignment = VerticalAlignment.Top,
            IsReadOnly = true,
        };

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
        ExternalEventController.ActionEventHandler.Raise(app =>
        {
            var doc = app.ActiveUIDocument.Document;
            if (doc == null)
            {
                Trace.TraceWarning("No active document");
                return;
            }
            
            var hashKey = doc.GetHashCode();

            if (!TraceGeometry.DocGeometries.TryGetValue(hashKey, out var value)) 
                return;
        
            var transaction = new Transaction(doc, "RemoveTransient");
            try
            {
                transaction.Start();
                doc.Delete(value);
                transaction.Commit();
            }
            catch (Exception e)
            {
                Trace.TraceWarning($"Remove Transient Geometry Failed : [{e.Message}]");
                transaction.RollBack();
            }
            finally
            {
                TraceGeometry.DocGeometries.Remove(hashKey);
            }
        });
    }

    public void Dispose()
    {
        _consoleRedirector.Dispose();
        GC.SuppressFinalize(this);
    }
}