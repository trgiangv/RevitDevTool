using System.Diagnostics;
using System.Text;

namespace Build.Tools;

/// <summary>
///     Runs a command-line tool outside the pipeline. <see cref="Run"/> captures its output, <see cref="Stream"/> shows it.
///     A failure names the tool and its first argument only, so a secret argument never reaches the log.
/// </summary>
public static class Shell
{
    public static string Run(string root, string file, params string[] arguments)
    {
        var (exitCode, output, error) = Execute(root, file, arguments, capture: true);
        return exitCode == 0 ? output : throw Failed(file, arguments, error.Trim());
    }

    public static bool Succeeds(string root, string file, params string[] arguments) =>
        Execute(root, file, arguments, capture: true).ExitCode == 0;

    public static void Stream(string root, string file, params string[] arguments)
    {
        var exitCode = Execute(root, file, arguments, capture: false).ExitCode;
        if (exitCode != 0) throw Failed(file, arguments, $"exit code {exitCode}");
    }

    private static (int ExitCode, string Output, string Error) Execute(string root, string file, string[] arguments, bool capture)
    {
        var info = new ProcessStartInfo(file)
        {
            WorkingDirectory = root,
            RedirectStandardOutput = capture,
            RedirectStandardError = capture,
            StandardOutputEncoding = capture ? Encoding.UTF8 : null,
            UseShellExecute = false
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);

        using var process = Process.Start(info) ?? throw new InvalidOperationException($"{file} could not start; install it and put it on PATH.");
        if (!capture)
        {
            process.WaitForExit();
            return (process.ExitCode, "", "");
        }

        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        return (process.ExitCode, output.Result, error.Result);
    }

    private static InvalidOperationException Failed(string file, string[] arguments, string detail) =>
        new($"{file} {arguments.FirstOrDefault()} failed: {detail}");
}
