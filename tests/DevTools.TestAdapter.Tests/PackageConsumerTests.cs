using System.Diagnostics;
using System.IO.Compression;

namespace DevTools.TestAdapter.Tests;

public sealed class PackageConsumerTests
{
    [Fact]
    public void Packed_package_keeps_internal_runtime_closure_private_and_bootstraps_from_a_clean_consumer()
    {
        var root = FindRepositoryRoot();
        var work = Path.Combine(Path.GetTempPath(), "RevitDevTool.TestAdapter.PackageTest", Guid.NewGuid().ToString("N"));
        var packages = Path.Combine(work, "packages");
        var globalPackages = Path.Combine(work, "global-packages");
        var consumer = Path.Combine(work, "consumer");
        Directory.CreateDirectory(packages);
        Directory.CreateDirectory(consumer);

        try
        {
            // Do not let a prior test build prove a stale package: pack performs a
            // fresh Release build, then the consumer restores from an empty cache.
            Run("dotnet", $"pack \"{Path.Combine(root, "source", "DevTools.TestAdapter", "DevTools.TestAdapter.csproj")}\" -c Release -o \"{packages}\"");
            var nupkg = Directory.GetFiles(packages, "RevitDevTool.TestAdapter.*.nupkg", SearchOption.TopDirectoryOnly).Single();
            var packageVersion = Path.GetFileNameWithoutExtension(nupkg)
                ["RevitDevTool.TestAdapter.".Length..];
            AssertPackageClosure(nupkg);

            File.WriteAllText(Path.Combine(work, "NuGet.Config"), $"""
                <?xml version="1.0" encoding="utf-8"?>
                <configuration>
                  <packageSources>
                    <clear />
                    <add key="local" value="{packages.Replace("\\", "/")}" />
                    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
                  </packageSources>
                  <packageSourceMapping>
                    <packageSource key="local"><package pattern="RevitDevTool.TestAdapter" /></packageSource>
                    <packageSource key="nuget.org"><package pattern="*" /></packageSource>
                  </packageSourceMapping>
                </configuration>
                """);
            File.WriteAllText(Path.Combine(consumer, "CleanConsumer.csproj"), $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFrameworks>net48;net8.0-windows;net8.0-windows10.0.19041.0;net10.0-windows;net10.0-windows10.0.19041.0</TargetFrameworks>
                    <OutputType>Exe</OutputType>
                    <LangVersion>latest</LangVersion>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
                    <!-- Exercise the documented package graph without asking this
                         synthetic executable to launch a CAD host. -->
                    <IsTestingPlatformApplication>false</IsTestingPlatformApplication>
                    <EnableMicrosoftTestingPlatform>false</EnableMicrosoftTestingPlatform>
                    <GenerateTestingPlatformEntryPoint>false</GenerateTestingPlatformEntryPoint>
                  </PropertyGroup>
                  <ItemGroup>
                    <Compile Remove="ProviderLeak.cs" />
                    <PackageReference Include="RevitDevTool.TestAdapter" Version="{packageVersion}" />
                    <PackageReference Include="Microsoft.Testing.Platform.MSBuild" Version="2.3.3" />
                    <PackageReference Include="NUnit" Version="4.6.1" />
                  </ItemGroup>
                </Project>
                """);
            File.WriteAllText(Path.Combine(consumer, "Program.cs"), """
                using System.Runtime.CompilerServices;
                using DevTools.TestAdapter;

                File.WriteAllText(
                    Path.Combine(AppContext.BaseDirectory, "testconfig.json"),
                    "{\"devtools\":{\"frameworkId\":\"nunit\",\"mtpAssembly\":\"DevTools.NUnit.MTP.dll\",\"mtpEntry\":\"DevTools.NUnit.MTP.NUnitMTP\"}}");
                RuntimeHelpers.RunClassConstructor(typeof(TestingPlatformBuilderHook).TypeHandle);
                return typeof(TestingPlatformBuilderHook) is null ? 1 : 0;
                """);
            File.WriteAllText(Path.Combine(consumer, "ProviderLeak.csproj"), $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0-windows</TargetFramework>
                    <LangVersion>latest</LangVersion>
                    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
                    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
                    <IsTestingPlatformApplication>false</IsTestingPlatformApplication>
                    <EnableMicrosoftTestingPlatform>false</EnableMicrosoftTestingPlatform>
                  </PropertyGroup>
                  <ItemGroup>
                    <Compile Include="ProviderLeak.cs" />
                    <PackageReference Include="RevitDevTool.TestAdapter" Version="{packageVersion}" />
                    <PackageReference Include="Microsoft.Testing.Platform.MSBuild" Version="2.3.3" />
                    <PackageReference Include="NUnit" Version="4.6.1" />
                  </ItemGroup>
                </Project>
                """);
            File.WriteAllText(Path.Combine(consumer, "ProviderLeak.cs"), """
                using System;
                using DevTools.TestAdapter;
                namespace Consumer;
                public static class ProviderLeak
                {
                    public static Type ProviderType => typeof(HostTestFramework);
                }
                """);

            Run("dotnet", "restore CleanConsumer.csproj --configfile ../NuGet.Config", consumer, globalPackages);
            AssertNoInternalPackageRestore(globalPackages);
            Run("dotnet", "restore ProviderLeak.csproj --configfile ../NuGet.Config", consumer, globalPackages);
            RunExpectFailure("dotnet", "build ProviderLeak.csproj -c Release --no-restore", consumer, globalPackages, "CS0122");
            // Both synthetic projects share a folder, so restore the consumer
            // again after the negative project's assets file has been written.
            Run("dotnet", "restore CleanConsumer.csproj --configfile ../NuGet.Config", consumer, globalPackages);

            foreach (var tfm in new[] { "net48", "net8.0-windows", "net8.0-windows10.0.19041.0", "net10.0-windows", "net10.0-windows10.0.19041.0" })
            {
                Run("dotnet", $"build CleanConsumer.csproj -c Release --no-restore -f {tfm}", consumer, globalPackages);
                var output = Path.Combine(consumer, "bin", "Release", tfm);
                if (!tfm.Equals("net48", StringComparison.Ordinal))
                    AssertRuntimeClosure(output);
                else
                    Assert.True(
                        File.Exists(Path.Combine(output, "DevTools.Testing.Abstractions.dll")),
                        $"Missing DevTools.Testing.Abstractions.dll for net48.{Environment.NewLine}"
                        + string.Join(Environment.NewLine, Directory.GetFiles(output, "*.dll").Select(Path.GetFileName)));
                Assert.True(
                    File.Exists(Path.Combine(output, "DevTools.NUnit.MTP.dll")),
                    $"Missing DevTools.NUnit.MTP.dll for {tfm}.{Environment.NewLine}"
                    + string.Join(Environment.NewLine, Directory.GetFiles(output, "*.dll").Select(Path.GetFileName)));

                Run(tfm.Equals("net48", StringComparison.Ordinal) ? Path.Combine(output, "CleanConsumer.exe") : "dotnet",
                    tfm.Equals("net48", StringComparison.Ordinal) ? string.Empty : "CleanConsumer.dll", output, globalPackages);
            }
        }
        finally
        {
            if (Directory.Exists(work))
                Directory.Delete(work, recursive: true);
        }
    }

    [Fact]
    public void Framework_id_only_testconfig_does_not_throw_from_hook_static_ctor()
    {
        var root = FindRepositoryRoot();
        var work = Path.Combine(Path.GetTempPath(), "RevitDevTool.TestAdapter.PartialConfig", Guid.NewGuid().ToString("N"));
        var packages = Path.Combine(work, "packages");
        var globalPackages = Path.Combine(work, "global-packages");
        var consumer = Path.Combine(work, "consumer");
        Directory.CreateDirectory(packages);
        Directory.CreateDirectory(consumer);

        try
        {
            Run("dotnet", $"pack \"{Path.Combine(root, "source", "DevTools.TestAdapter", "DevTools.TestAdapter.csproj")}\" -c Release -o \"{packages}\"");
            var nupkg = Directory.GetFiles(packages, "RevitDevTool.TestAdapter.*.nupkg", SearchOption.TopDirectoryOnly).Single();
            var packageVersion = Path.GetFileNameWithoutExtension(nupkg)
                ["RevitDevTool.TestAdapter.".Length..];

            File.WriteAllText(Path.Combine(work, "NuGet.Config"), $"""
                <?xml version="1.0" encoding="utf-8"?>
                <configuration>
                  <packageSources>
                    <clear />
                    <add key="local" value="{packages.Replace("\\", "/")}" />
                    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
                  </packageSources>
                  <packageSourceMapping>
                    <packageSource key="local"><package pattern="RevitDevTool.TestAdapter" /></packageSource>
                    <packageSource key="nuget.org"><package pattern="*" /></packageSource>
                  </packageSourceMapping>
                </configuration>
                """);
            File.WriteAllText(Path.Combine(consumer, "PartialConfigConsumer.csproj"), $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0-windows</TargetFramework>
                    <OutputType>Exe</OutputType>
                    <LangVersion>latest</LangVersion>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <IsTestingPlatformApplication>false</IsTestingPlatformApplication>
                    <EnableMicrosoftTestingPlatform>false</EnableMicrosoftTestingPlatform>
                    <GenerateTestingPlatformEntryPoint>false</GenerateTestingPlatformEntryPoint>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="RevitDevTool.TestAdapter" Version="{packageVersion}" />
                    <PackageReference Include="Microsoft.Testing.Platform.MSBuild" Version="2.3.3" />
                  </ItemGroup>
                </Project>
                """);
            File.WriteAllText(Path.Combine(consumer, "Program.cs"), """
                using System.Runtime.CompilerServices;
                using DevTools.TestAdapter;

                File.WriteAllText(
                    Path.Combine(AppContext.BaseDirectory, "testconfig.json"),
                    "{\"devtools\":{\"frameworkId\":\"nunit\"}}");
                Exception? caught = null;
                try
                {
                    RuntimeHelpers.RunClassConstructor(typeof(TestingPlatformBuilderHook).TypeHandle);
                }
                catch (Exception ex)
                {
                    caught = ex;
                }

                return caught is TypeInitializationException ? 1 : 0;
                """);

            Run("dotnet", "restore PartialConfigConsumer.csproj --configfile ../NuGet.Config", consumer, globalPackages);
            Run("dotnet", "build PartialConfigConsumer.csproj -c Release --no-restore", consumer, globalPackages);
            var output = Path.Combine(consumer, "bin", "Release", "net10.0-windows");
            Run("dotnet", "PartialConfigConsumer.dll", output, globalPackages);
        }
        finally
        {
            if (Directory.Exists(work))
                Directory.Delete(work, recursive: true);
        }
    }

    private static void AssertPackageClosure(string nupkg)
    {
        using var package = ZipFile.OpenRead(nupkg);
        var entries = package.Entries.Select(entry => entry.FullName.Replace('\\', '/')).ToArray();
        var nuspec = package.Entries.Single(entry => entry.Name.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));
        using var reader = new StreamReader(nuspec.Open());
        var nuspecText = reader.ReadToEnd();

        foreach (var internalName in InternalRuntimeAssemblies.Append("DevTools.NUnit.Core"))
            Assert.DoesNotContain(internalName, nuspecText, StringComparison.Ordinal);

        Assert.All(
            entries.Where(entry => entry.StartsWith("lib/", StringComparison.OrdinalIgnoreCase)),
            entry => Assert.EndsWith("/DevTools.TestAdapter.dll", entry, StringComparison.OrdinalIgnoreCase));
        Assert.Contains("lib/net48/DevTools.TestAdapter.dll", entries, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("build/runtime/net48/DevTools.NUnit.MTP.dll", entries, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("build/runtime/net48/DevTools.Testing.Abstractions.dll", entries, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("lib/net48/DevTools.NUnit.MTP.dll", entries, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("lib/net48/DevTools.Testing.Abstractions.dll", entries, StringComparer.OrdinalIgnoreCase);
        foreach (var tfm in new[] { "net8.0-windows7.0", "net10.0-windows7.0" })
        {
            Assert.Contains($"lib/{tfm}/DevTools.TestAdapter.dll", entries, StringComparer.OrdinalIgnoreCase);
            foreach (var assembly in InternalRuntimeAssemblies.Where(name => name != "DevTools.TestAdapter.dll"))
            {
                Assert.DoesNotContain($"lib/{tfm}/{assembly}", entries, StringComparer.OrdinalIgnoreCase);
                Assert.Contains($"build/runtime/{tfm}/{assembly}", entries, StringComparer.OrdinalIgnoreCase);
            }

            Assert.DoesNotContain($"build/runtime/{tfm}/DevTools.TestAdapter.dll", entries, StringComparer.OrdinalIgnoreCase);
            Assert.Contains($"build/runtime/{tfm}/DevTools.NUnit.MTP.dll", entries, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain($"lib/{tfm}/DevTools.NUnit.MTP.dll", entries, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain($"lib/{tfm}/Microsoft.Bcl.AsyncInterfaces.dll", entries, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain($"build/runtime/{tfm}/Microsoft.Bcl.AsyncInterfaces.dll", entries, StringComparer.OrdinalIgnoreCase);
        }

        Assert.DoesNotContain(entries, entry => entry.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(entries, entry => entry.EndsWith("/DevTools.AssemblyIsolation.dll", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("DevTools.AssemblyIsolation", nuspecText, StringComparison.Ordinal);
        Assert.DoesNotContain(entries, entry => entry.Contains("DevTools.NUnit.Core", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(entries, entry => entry.Contains("DevTools.Testing.Discovery", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(entries.Length, entries.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    private static readonly string[] InternalRuntimeAssemblies =
    [
        "DevTools.Ipc.dll",
        "DevTools.TestAdapter.dll",
        "DevTools.Testing.Abstractions.dll",
        "DevTools.Testing.Transport.dll",
    ];

    private static void AssertRuntimeClosure(string outputDirectory)
    {
        foreach (var assembly in InternalRuntimeAssemblies)
        {
            Assert.True(
                File.Exists(Path.Combine(outputDirectory, assembly)),
                $"Missing consumer runtime asset: {assembly}{Environment.NewLine}"
                + string.Join(Environment.NewLine, Directory.GetFiles(outputDirectory, "*.dll").Select(Path.GetFileName)));
        }
    }

    private static void AssertNoInternalPackageRestore(string globalPackages)
    {
        foreach (var internalAssembly in InternalRuntimeAssemblies.Where(name => name != "DevTools.TestAdapter.dll"))
        {
            var packageDirectory = Path.Combine(globalPackages, Path.GetFileNameWithoutExtension(internalAssembly).ToLowerInvariant());
            Assert.False(Directory.Exists(packageDirectory), $"Internal package unexpectedly restored: {packageDirectory}");
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

    private static void Run(string fileName, string arguments, string? workingDirectory = null, string? globalPackages = null)
    {
        var output = RunProcess(fileName, arguments, workingDirectory, globalPackages);
        Assert.True(output.ExitCode == 0, $"{fileName} {arguments} failed:{Environment.NewLine}{output.Text}");
    }

    private static void RunExpectFailure(string fileName, string arguments, string workingDirectory, string globalPackages, string expectedText)
    {
        var output = RunProcess(fileName, arguments, workingDirectory, globalPackages);
        Assert.True(output.ExitCode != 0, $"{fileName} {arguments} unexpectedly succeeded.");
        Assert.Contains(expectedText, output.Text, StringComparison.Ordinal);
    }

    private static (int ExitCode, string Text) RunProcess(string fileName, string arguments, string? workingDirectory, string? globalPackages)
    {
        var startInfo = new ProcessStartInfo(fileName, arguments)
        {
            WorkingDirectory = workingDirectory ?? FindRepositoryRoot(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        if (globalPackages is not null)
            startInfo.Environment["NUGET_PACKAGES"] = globalPackages;
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Could not start {fileName}.");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        Task.WaitAll(standardOutput, standardError);
        return (process.ExitCode, standardOutput.Result + standardError.Result);
    }
}
