using System.Diagnostics;
using System.IO;

namespace RevitDevTool.Models;

/// <summary>
/// Redirects Console.WriteLine output to the Trace system
/// </summary>
public class ConsoleRedirector : IDisposable
{
    private readonly TextWriter _originalOut;
    private readonly TextWriter _originalError;
    private readonly ConsoleTextWriter _consoleOutWriter;
    private readonly ConsoleTextWriter _consoleErrorWriter;
    private bool _disposed;

    public ConsoleRedirector()
    {
        _originalOut = Console.Out;
        _originalError = Console.Error;
            
        _consoleOutWriter = new ConsoleTextWriter(TraceEventType.Verbose);
        _consoleErrorWriter = new ConsoleTextWriter(TraceEventType.Error);
            
        Console.SetOut(_consoleOutWriter);
        Console.SetError(_consoleErrorWriter);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Console.SetOut(_originalOut);
            Console.SetError(_originalError);
            _consoleOutWriter.Dispose();
            _consoleErrorWriter.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }

    private class ConsoleTextWriter(TraceEventType eventType) : TextWriter
    {
        public override void Write(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return;

            switch (eventType)
            {
                case TraceEventType.Error:
                case TraceEventType.Critical:
                    Trace.TraceError(value);
                    break;
                case TraceEventType.Warning:
                    Trace.TraceWarning(value);
                    break;
                case TraceEventType.Information:
                    Trace.TraceInformation(value);
                    break;
                case TraceEventType.Verbose:
                case TraceEventType.Start:
                case TraceEventType.Stop:
                case TraceEventType.Suspend:
                case TraceEventType.Resume:
                case TraceEventType.Transfer:
                default:
                    Trace.WriteLine(value);
                    break;
            }
        }

        public override void WriteLine(string? value)
        {
            if (value != null) Write(value);
        }

        public override System.Text.Encoding Encoding => System.Text.Encoding.UTF8;
    }
}