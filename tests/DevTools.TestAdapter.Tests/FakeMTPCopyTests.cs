using System.Diagnostics;

namespace DevTools.TestAdapter.Tests;

public sealed class FakeMTPCopyTests
{
    [Fact]
    public void Copy_target_succeeds_for_unmapped_framework_when_dll_is_already_in_outdir()
    {
        var root = FindRepositoryRoot();
        var work = Path.Combine(Path.GetTempPath(), "DevTools.FakeMTPCopy", Guid.NewGuid().ToString("N"));
        var outDir = Path.Combine(work, "out");
        Directory.CreateDirectory(outDir);
        File.WriteAllBytes(Path.Combine(outDir, "Fake.MTP.dll"), [0x4D, 0x5A]);

        var targets = Path.Combine(root, "source", "DevTools.TestAdapter", "build", "RevitDevTool.TestAdapter.targets");
        File.WriteAllText(Path.Combine(work, "FakeCopy.proj"), $"""
            <Project>
              <PropertyGroup>
                <TestingFramework>fake</TestingFramework>
                <MTPAssembly>Fake.MTP.dll</MTPAssembly>
                <MTPEntry>Fake.Plugin</MTPEntry>
                <OutDir>{outDir}\</OutDir>
                <TargetFramework>net8.0-windows</TargetFramework>
                <TargetFrameworkIdentifier>.NETCoreApp</TargetFrameworkIdentifier>
                <TargetFrameworkVersion>v8.0</TargetFrameworkVersion>
                <IsTestingPlatformApplication>false</IsTestingPlatformApplication>
              </PropertyGroup>
              <Import Project="{targets.Replace('\\', '/')}"/>
            </Project>
            """);

        try
        {
            var start = new ProcessStartInfo("dotnet", "msbuild FakeCopy.proj -t:CopyMTPSibling -nologo")
            {
                WorkingDirectory = work,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start msbuild.");
            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            Assert.True(process.ExitCode == 0, stdout + stderr);
        }
        finally
        {
            if (Directory.Exists(work))
                Directory.Delete(work, recursive: true);
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "RevitDevTool.slnx")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate RevitDevTool.slnx.");
    }
}
