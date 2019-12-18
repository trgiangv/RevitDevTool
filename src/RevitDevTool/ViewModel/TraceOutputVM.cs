using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RevitDevTool.UI.Binding;


namespace RevitDevTool.ViewModel
{
    public class TraceOutputVM : TextWriter, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName]string propertyName = "")
        {
            if (this.PropertyChanged != null)
                this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

        private string _data;

        public string Data
        {
            get => _data;
            set
            {
                _data = value;
                OnPropertyChanged();
            }
        }

        private bool _isStarted = false;
        public bool IsStarted
        {
            get => _isStarted;
            set
            {
                _isStarted = value;
                OnPropertyChanged();
            }
        }

        public TraceOutputVM()
        {
            Console.SetOut(this);
            TextWriterTraceListener traceListener = new TextWriterTraceListener();

            System.Diagnostics.Trace.Listeners.Add(traceListener);
            traceListener.Writer = System.Console.Out;
        }

        public override void Write(char value)
        {
            if (IsStarted)
            {
                this.Data += value;
            }
        }

        public override void Write(string value)
        {
            if (IsStarted)
            {
                this.Data += value;
            }
        }

        public override Encoding Encoding => Encoding.UTF8;

        public DelegateCommand ClearCommand => new DelegateCommand()
        {
            ExecuteCommand = (obj) => { this.Data = string.Empty; }
        };

        public DelegateCommand StatusCommand => new DelegateCommand()
        {
            ExecuteCommand = (obj) =>
            {
                if (IsStarted)
                {
                    IsStarted = false;
                }
                else
                {
                    IsStarted = true;
                }
            }
        };
    }
}
