using System.Diagnostics;

namespace Build.Trains;

public static class Tool
{
    public static int Run(string root, string file, params string[] arguments)
    {
        var info = new ProcessStartInfo(file) { WorkingDirectory = root, UseShellExecute = false };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);

        using var process = Process.Start(info) ?? throw new InvalidOperationException($"{file} could not start; install it and put it on PATH.");
        process.WaitForExit();
        return process.ExitCode;
    }

    public static void Require(string root, string file, params string[] arguments)
    {
        var exitCode = Run(root, file, arguments);
        if (exitCode != 0)
        {
            throw new InvalidOperationException($"{file} {arguments[0]} failed with exit code {exitCode}.");
        }
    }
}
