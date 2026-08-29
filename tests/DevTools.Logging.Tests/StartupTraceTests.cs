using DevTools.Logging.Diagnostics;

namespace DevTools.Logging.Tests;

[Collection(nameof(StartupTrace))]
public sealed class StartupTraceTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("startup-trace-").FullName;

    public void Dispose()
    {
        StartupTrace.Current?.Dispose();
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch
        {
            // temp cleanup
        }
    }

    [Fact]
    public void Dispose_without_Fail_creates_no_file()
    {
        using (var trace = StartupTrace.Begin("Revit", "26.0", 4242, _dir))
        {
            trace.Mark("Host.Start");
        }

        Assert.Empty(Directory.GetFiles(_dir));
        Assert.Null(StartupTrace.Current);
    }

    [Fact]
    public void Fail_writes_crash_file_with_elapsed_and_exception()
    {
        var trace = StartupTrace.Begin("Revit", "26.0", 4242, _dir);
        trace.Mark("Host.Start");

        var error = new InvalidOperationException("startup exploded");
        trace.Fail(error);
        trace.Fail(new Exception("second"));

        var path = Path.Combine(_dir, "crash_Revit_26.0_4242.log");
        Assert.True(File.Exists(path));
        var text = File.ReadAllText(path);
        Assert.Contains("app=Revit ver=26.0 pid=4242", text, StringComparison.Ordinal);
        Assert.Contains("+", text, StringComparison.Ordinal);
        Assert.Contains("OnStartup", text, StringComparison.Ordinal);
        Assert.Contains("Host.Start", text, StringComparison.Ordinal);
        Assert.Contains("FAIL InvalidOperationException", text, StringComparison.Ordinal);
        Assert.Contains("startup exploded", text, StringComparison.Ordinal);
        Assert.DoesNotContain("second", text, StringComparison.Ordinal);
        Assert.Single(Directory.GetFiles(_dir));
    }

    [Fact]
    public void Fail_does_not_throw_when_folder_is_invalid()
    {
        var blocked = Path.Combine(_dir, "not-a-directory");
        File.WriteAllText(blocked, "x");
        using var trace = StartupTrace.Begin("Revit", "26.0", 1, blocked);
        var ex = Record.Exception(() => trace.Fail(new InvalidOperationException("x")));
        Assert.Null(ex);
    }
}
