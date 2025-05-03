using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RevitDevTool.Geometry;
using RevitDevTool.Handlers;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.RichTextBox.Themes;
using Color = System.Windows.Media.Color;

namespace RevitDevTool.ViewModel;

public partial class TraceOutputVm : ObservableObject
{
    private readonly LoggingLevelSwitch _levelSwitch;
    private readonly SerilogTraceListener.SerilogTraceListener _listener;
    [ObservableProperty] private bool _isStarted;
    [ObservableProperty] private LogEventLevel _logLevel = LogEventLevel.Debug;

    partial void OnLogLevelChanging(LogEventLevel value)
    {
        _levelSwitch.MinimumLevel = value;
    }

    partial void OnIsStartedChanged(bool value)
    {
        if (value)
        {
            Trace.Listeners.Add(_listener);
            Trace.Listeners.Add(TraceGeometry.TraceListener);
        }
        else
        {
            Trace.Listeners.Remove(_listener);
            Trace.Listeners.Remove(TraceGeometry.TraceListener);
        }
    }

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
            .WriteTo.RichTextBox(LogTextBox, theme: RichTextBoxConsoleTheme.Colored)
            .CreateLogger();
        _listener = new SerilogTraceListener.SerilogTraceListener(logger) { Name = "RevitDevTool" };
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
}