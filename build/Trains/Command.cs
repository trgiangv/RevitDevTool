using System.Diagnostics;

namespace Build.Trains;

public static class Command
{
    public static string Run(string file, string root, params string[] arguments)
    {
        var (exitCode, output, error) = Execute(file, root, arguments);
        return exitCode == 0
            ? output
            : throw new InvalidOperationException($"{file} {string.Join(' ', arguments)} failed: {error.Trim()}");
    }

    public static bool Succeeds(string file, string root, params string[] arguments) => Execute(file, root, arguments).ExitCode == 0;

    private static (int ExitCode, string Output, string Error) Execute(string file, string root, string[] arguments)
    {
        var info = new ProcessStartInfo(file)
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);

        using var process = Process.Start(info) ?? throw new InvalidOperationException($"{file} could not start; install it and put it on PATH.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        return (process.ExitCode, output.Result, error.Result);
    }
}
