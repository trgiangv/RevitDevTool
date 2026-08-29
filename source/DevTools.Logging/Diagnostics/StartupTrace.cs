using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace DevTools.Logging.Diagnostics;

/// <summary>
/// Pre-DI startup buffer. Writes <c>crash_{app}_{ver}_{pid}.log</c> only when
/// <see cref="Fail"/> runs (explicit catch or <see cref="AppDomain.UnhandledException"/>).
/// Successful <see cref="Dispose"/> discards the buffer.
/// </summary>
public sealed class StartupTrace : IDisposable
{
    private static readonly AsyncLocal<StartupTrace?> Ambient = new();
    private static StartupTrace? instance;

    private readonly string _app;
    private readonly string _ver;
    private readonly int _pid;
    private readonly string _logsDirectory;
    private readonly DateTimeOffset _startedUtc;
    private readonly Stopwatch _watch;
    private readonly List<string> _lines = [];
    private readonly object _gate = new();
    private readonly UnhandledExceptionEventHandler _unhandled;
    private bool _failed;
    private bool _disposed;

    private StartupTrace(string app, string ver, int pid, string logsDirectory)
    {
        _app = app;
        _ver = ver;
        _pid = pid;
        _logsDirectory = logsDirectory;
        _startedUtc = DateTimeOffset.UtcNow;
        _watch = Stopwatch.StartNew();
        _unhandled = (_, args) => Fail(args.ExceptionObject as Exception);
        AppDomain.CurrentDomain.UnhandledException += _unhandled;
        Append("OnStartup");
    }

    public static StartupTrace? Current => Ambient.Value ?? instance;

    public static StartupTrace Begin(string app, string ver, int pid, string logsDirectory)
    {
        instance?.Dispose();
        var trace = new StartupTrace(app, ver, pid, logsDirectory);
        instance = trace;
        Ambient.Value = trace;
        return trace;
    }

    public void Mark(string note)
    {
        lock (_gate)
        {
            if (_disposed) return;
            Append(note);
        }
    }

    public void Fail(Exception? ex)
    {
        lock (_gate)
        {
            if (_disposed || _failed) return;
            _failed = true;
            Append(ex is null ? "FAIL" : $"FAIL {ex.GetType().Name}");
            try
            {
                Directory.CreateDirectory(_logsDirectory);
                var path = Path.Combine(_logsDirectory, $"crash_{_app}_{_ver}_{_pid}.log");
                File.WriteAllText(path, FormatDump(ex));
            }
            catch
            {
                // Never throw from a crash dump.
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            AppDomain.CurrentDomain.UnhandledException -= _unhandled;
            Detach(this);
            _lines.Clear();
        }
    }

    private static void Detach(StartupTrace trace)
    {
        if (ReferenceEquals(instance, trace))
            instance = null;
        if (ReferenceEquals(Ambient.Value, trace))
            Ambient.Value = null;
    }

    private void Append(string note)
    {
        _lines.Add(string.Format(
            CultureInfo.InvariantCulture,
            "{0}  {1}",
            FormatElapsed(_watch.Elapsed),
            note));
    }

    private static string FormatElapsed(TimeSpan elapsed) =>
        string.Format(CultureInfo.InvariantCulture, "+{0:0.000}", elapsed.TotalSeconds);

    private string FormatDump(Exception? ex)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Format(
            CultureInfo.InvariantCulture,
            "utc={0:yyyy-MM-ddTHH:mm:ss.fffZ}",
            _startedUtc.UtcDateTime));
        sb.AppendLine($"app={_app} ver={_ver} pid={_pid}");
        sb.AppendLine();
        foreach (var line in _lines)
            sb.AppendLine(line);
        if (ex is not null)
            sb.AppendLine(ex.ToString());

        return sb.ToString();
    }
}
