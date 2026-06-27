using System.Diagnostics;
using System.IO;
using System.Text;

namespace DevTools.Logging.Listeners;

/// <summary>
/// Routes <see cref="Console.Out"/> and <see cref="Console.Error"/> to <see cref="Trace"/>
/// so active trace listeners (e.g. <see cref="LoggerTraceListener"/>) receive console output.
/// </summary>
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
        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value) => Trace.Write(value);

        public override void Write(string? value)
        {
            if (!string.IsNullOrEmpty(value))
                Trace.Write(value);
        }

        public override void Write(char[]? buffer, int index, int count)
        {
            if (buffer is null || count <= 0)
                return;
            Trace.Write(new string(buffer, index, count));
        }

        public override void WriteLine() => Trace.WriteLine(string.Empty);

        public override void WriteLine(string? value) => Trace.WriteLine(value);

        public override void Flush() => Trace.Flush();
    }
}
