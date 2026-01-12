using System.Diagnostics;
using System.IO;

namespace RevitDevTool.Models.Trace;

/// <summary>
/// Redirects Console.WriteLine output to the Trace system
/// </summary>
internal sealed class ConsoleRedirector : IDisposable
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

        _consoleOutWriter = new ConsoleTextWriter(TraceEventType.Information);
        _consoleErrorWriter = new ConsoleTextWriter(TraceEventType.Error);

        Console.SetOut(_consoleOutWriter);
        Console.SetError(_consoleErrorWriter);
    }

    public void Dispose()
    {
        if (_disposed) return;
        Console.SetOut(_originalOut);
        Console.SetError(_originalError);
        _consoleOutWriter.Dispose();
        _consoleErrorWriter.Dispose();
        _disposed = true;
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
                    System.Diagnostics.Trace.TraceError(value);
                    break;
                case TraceEventType.Warning:
                    System.Diagnostics.Trace.TraceWarning(value);
                    break;
                case TraceEventType.Information:
                    System.Diagnostics.Trace.TraceInformation(value);
                    break;
                case TraceEventType.Verbose:
                case TraceEventType.Start:
                case TraceEventType.Stop:
                case TraceEventType.Suspend:
                case TraceEventType.Resume:
                case TraceEventType.Transfer:
                default:
                    System.Diagnostics.Trace.WriteLine(value);
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