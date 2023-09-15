using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using RevitDevTool.UI.Binding;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.RichTextBox.Themes;

namespace RevitDevTool.ViewModel
{
    public class TraceOutputVM : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = "")
        {
            if (this.PropertyChanged != null)
                this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

        public bool IsStarted
        {
            get => _isStarted;
            set
            {
                _isStarted = value;
                OnPropertyChanged();
            }
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

        private Logger _logger;
        private LoggingLevelSwitch _levelSwitch;
        private bool _isStarted = false;
        private LogEventLevel _logLevel = LogEventLevel.Debug;
        private readonly global::SerilogTraceListener.SerilogTraceListener _listener;

        public RichTextBox LogTextBox { get; }

        public TraceOutputVM()
        {
            LogTextBox = new RichTextBox()
            {
                Background = new SolidColorBrush(Color.FromRgb(29, 29, 31)),
                Foreground = new SolidColorBrush(Color.FromRgb(245, 245, 247)),
                FontFamily = new FontFamily("Cascadia Mono, Consolas, Courier New, monospace"),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                IsReadOnly = true,
            };
            _levelSwitch = new LoggingLevelSwitch(_logLevel);
            _logger = new LoggerConfiguration()
                .MinimumLevel.ControlledBy(_levelSwitch)
                .WriteTo.RichTextBox(LogTextBox, theme: RichTextBoxConsoleTheme.Literate)
                .CreateLogger();
            _listener = new global::SerilogTraceListener.SerilogTraceListener(_logger) { Name = "RevitDevTool" };
        }

        public DelegateCommand ClearCommand => new()
        {
            ExecuteCommand = (obj) => { LogTextBox.Document.Blocks.Clear(); }
        };

        public DelegateCommand StatusCommand => new()
        {
            ExecuteCommand = (obj) =>
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
}
