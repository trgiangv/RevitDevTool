using System.Diagnostics;
using System.IO;

namespace DevTools.Logging.Listeners;

public sealed class ConsoleRedirector : IDisposable
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

        _consoleOutWriter = new ConsoleTextWriter();
        _consoleErrorWriter = new ConsoleTextWriter();

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

    private sealed class ConsoleTextWriter : TextWriter
    {
        public override void Write(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return;

            Trace.Write(value);
        }

        public override void WriteLine(string? value)
        {
            if (value != null) Write(value);
        }

        public override System.Text.Encoding Encoding => System.Text.Encoding.UTF8;
    }
}
