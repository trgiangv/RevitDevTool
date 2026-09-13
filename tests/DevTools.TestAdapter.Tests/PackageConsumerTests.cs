using System.Diagnostics;
using System.IO.Compression;

namespace DevTools.TestAdapter.Tests;

[TestClass]
[DoNotParallelize]
public sealed class PackageConsumerTests
{
    [TestMethod]
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
            var (nupkg, packageVersion) = PackAdapter(root, packages);
            AssertPackageClosure(nupkg);

            WriteIsolatedNuGetConfig(work, packages);

            var testhost = Path.Combine(work, "nunit-testhost");
            Directory.CreateDirectory(testhost);
            File.WriteAllText(Path.Combine(testhost, "NUnitTesthost.csproj"), $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFrameworks>net48;net8.0-windows</TargetFrameworks>
                    <OutputType>Exe</OutputType>
                    <LangVersion>latest</LangVersion>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <IsTestProject>true</IsTestProject>
                    <HostName>Revit</HostName>
                    <HostVersion>2025</HostVersion>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="RevitDevTool.TestAdapter" Version="{packageVersion}" />
                    <PackageReference Include="NUnit" Version="4.6.1" />
                  </ItemGroup>
                </Project>
                """);
            File.WriteAllText(Path.Combine(testhost, "SmokeTests.cs"), """
                using NUnit.Framework;

                public class SmokeTests
                {
                    [Test]
                    public void Ok() { }
                }
                """);
            File.WriteAllText(Path.Combine(testhost, "global.json"), """
                {
                  "sdk": { "rollForward": "latestMinor" },
                  "test": { "runner": "Microsoft.Testing.Platform" }
                }
                """);
            Run("dotnet", "restore NUnitTesthost.csproj --configfile ../NuGet.Config", testhost, globalPackages);
            Run("dotnet", "build NUnitTesthost.csproj -c Release --no-restore", testhost, globalPackages);
            foreach (var tfm in new[] { "net48", "net8.0-windows" })
            {
                Assert.IsTrue(
                    File.Exists(Path.Combine(testhost, "bin", "Release", tfm, "NUnitTesthost.exe")),
                    $"NUnit-only consumer should get a testhost Main from Microsoft.Testing.Platform.MSBuild ({tfm}).");
            }

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
                    <PackageReference Include="NUnit" Version="4.6.1" />
                  </ItemGroup>
                </Project>
                """);
            File.WriteAllText(Path.Combine(consumer, "Program.cs"), """
                using System.Runtime.CompilerServices;
                using DevTools.TestAdapter;

                File.WriteAllText(
                    Path.Combine(AppContext.BaseDirectory, "testconfig.json"),
                    "{\"devtools\":{\"frameworkId\":\"nunit\"}}");
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
                    public static Type ProviderType => typeof(TestFramework);
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
                AssertKeptRuntime(output);
                Assert.IsTrue(
                    File.Exists(Path.Combine(output, "DevTools.NUnit.MTP.dll")),
                    $"Missing DevTools.NUnit.MTP.dll for {tfm}.{Environment.NewLine}"
                    + string.Join(Environment.NewLine, Directory.GetFiles(output, "*.dll").Select(Path.GetFileName)));

                Run(tfm.Equals("net48", StringComparison.Ordinal) ? Path.Combine(output, "CleanConsumer.exe") : "dotnet",
                    tfm.Equals("net48", StringComparison.Ordinal) ? string.Empty : "CleanConsumer.dll", output, globalPackages);
            }
        }
        finally
        {
            TryDeleteDirectory(work);
        }
    }

    /// <summary>
    /// Building a testhost is not proof it runs: 0.0.6 shipped a consumer that could not
    /// load Microsoft.Testing.Platform (nuspec dropped runtime assets) or
    /// DevTools.Testing.Abstractions (resolver read discovery refs before hooking), and a
    /// TUnit project that got the NUnit plugin because the map lived in the .props.
    /// </summary>
    [TestMethod]
    public void Packed_package_discovers_tests_for_both_nunit_and_tunit_consumers()
    {
        var root = FindRepositoryRoot();
        var work = Path.Combine(Path.GetTempPath(), "RevitDevTool.TestAdapter.Discovery", Guid.NewGuid().ToString("N"));
        var packages = Path.Combine(work, "packages");
        Directory.CreateDirectory(packages);

        try
        {
            var (_, packageVersion) = PackAdapter(root, packages);

            WriteIsolatedNuGetConfig(work, packages);
            var globalPackages = Path.Combine(work, "global-packages");
            Directory.CreateDirectory(globalPackages);

            // Every shipped runtime folder: net48, net8, net10 — both engines.
            foreach (var tfm in new[] { "net48", "net8.0-windows", "net10.0-windows" })
            {
                var suffix = tfm.Replace('.', '_');

                AssertDiscovers(
                    work,
                    globalPackages,
                    $"NUnitDiscovery{suffix}",
                    packageVersion,
                    tfm,
                    engine: null,
                    framework: """<PackageReference Include="NUnit" Version="4.6.1" />""",
                    test: """
                        using NUnit.Framework;

                        public class DiscoveredTests
                        {
                            [Test]
                            public void Runs_in_host() { }
                        }
                        """,
                    expectedMtpAssembly: "DevTools.NUnit.MTP.dll");

                // net48 + TUnit needs [ModuleInitializer], which the package supplies: no
                // Polyfill here proves the consumer declares nothing beyond TUnit itself.
                AssertDiscovers(
                    work,
                    globalPackages,
                    $"TUnitDiscovery{suffix}",
                    packageVersion,
                    tfm,
                    engine: "tunit",
                    framework: """<PackageReference Include="TUnit" Version="1.67.0" />""",
                    test: """
                        public class DiscoveredTests
                        {
                            [TUnit.Core.Test]
                            public void Runs_in_host() { }
                        }
                        """,
                    expectedMtpAssembly: "DevTools.TUnit.MTP.dll");
            }
        }
        finally
        {
            TryDeleteDirectory(work);
        }
    }

    private static void AssertDiscovers(
        string work,
        string globalPackages,
        string name,
        string packageVersion,
        string tfm,
        string? engine,
        string framework,
        string test,
        string expectedMtpAssembly)
    {
        var netFx = tfm.StartsWith("net4", StringComparison.Ordinal);
        var consumer = Path.Combine(work, name);
        Directory.CreateDirectory(consumer);
        File.WriteAllText(Path.Combine(consumer, $"{name}.csproj"), $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>{tfm}</TargetFramework>
                <LangVersion>latest</LangVersion>
                <ImplicitUsings>enable</ImplicitUsings>
                <HostName>Revit</HostName>
                <HostVersion>2025</HostVersion>
                {(netFx ? "<RuntimeIdentifier>win-x64</RuntimeIdentifier>" : "")}
                {(engine is null ? "" : $"<TestingFramework>{engine}</TestingFramework>")}
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="RevitDevTool.TestAdapter" Version="{packageVersion}" />
                {framework}
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(consumer, "DiscoveredTests.cs"), test);
        File.WriteAllText(Path.Combine(consumer, "global.json"), """
            {
              "sdk": { "rollForward": "latestMinor" },
              "test": { "runner": "Microsoft.Testing.Platform" }
            }
            """);

        Run("dotnet", $"restore {name}.csproj --configfile ../NuGet.Config", consumer, globalPackages);
        Run("dotnet", $"build {name}.csproj -c Release --no-restore", consumer, globalPackages);

        var output = Directory
            .GetFiles(Path.Combine(consumer, "bin", "Release"), $"{name}.exe", SearchOption.AllDirectories)
            .Select(Path.GetDirectoryName)
            .Single()!;
        Assert.DoesNotContain("win-x64", output, StringComparison.OrdinalIgnoreCase);
        Assert.IsTrue(
            File.Exists(Path.Combine(output, expectedMtpAssembly)),
            $"{name} should copy {expectedMtpAssembly} ({tfm}).");
        Assert.Contains(
            $"\"frameworkId\": \"{(engine ?? "nunit")}\"",
            File.ReadAllText(Path.Combine(output, $"{name}.testconfig.json")),
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "mtpAssembly",
            File.ReadAllText(Path.Combine(output, $"{name}.testconfig.json")),
            StringComparison.Ordinal);

        // Discovery is local, so it must succeed without a CAD host.
        var discovery = netFx
            ? RunProcess(Path.Combine(output, $"{name}.exe"), "--list-tests", output, globalPackages)
            : RunProcess("dotnet", $"{name}.dll --list-tests", output, globalPackages);
        Assert.IsTrue(discovery.ExitCode == 0, $"{name} discovery failed ({tfm}):{Environment.NewLine}{discovery.Text}");
        Assert.Contains("Runs_in_host", discovery.Text, StringComparison.Ordinal);
    }

    [TestMethod]
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
            var (_, packageVersion) = PackAdapter(root, packages);

            WriteIsolatedNuGetConfig(work, packages);
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
            TryDeleteDirectory(work);
        }
    }

    private static (string Nupkg, string Version) PackAdapter(string root, string packages)
    {
        // Pack copies MTP from bin/Release; build siblings first so the nupkg
        // is not a stale Debug leftover. Same order as scripts/pack-test-adapter.ps1.
        Run("dotnet", $"build \"{Path.Combine(root, "source", "DevTools.NUnit.MTP", "DevTools.NUnit.MTP.csproj")}\" -c Release");
        Run("dotnet", $"build \"{Path.Combine(root, "source", "DevTools.TUnit.MTP", "DevTools.TUnit.MTP.csproj")}\" -c Release");
        Run("dotnet", $"pack \"{Path.Combine(root, "source", "DevTools.TestAdapter", "DevTools.TestAdapter.csproj")}\" -c Release -o \"{packages}\"");
        var nupkg = Directory.GetFiles(packages, "RevitDevTool.TestAdapter.*.nupkg", SearchOption.TopDirectoryOnly).Single();
        return (nupkg, Path.GetFileNameWithoutExtension(nupkg)["RevitDevTool.TestAdapter.".Length..]);
    }

    private static void AssertPackageClosure(string nupkg)
    {
        using var package = ZipFile.OpenRead(nupkg);
        var entries = package.Entries.Select(entry => entry.FullName.Replace('\\', '/')).ToArray();
        var nuspec = package.Entries.Single(entry => entry.Name.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));
        using var reader = new StreamReader(nuspec.Open());
        var nuspecText = reader.ReadToEnd();

        foreach (var internalName in MergedRuntimeAssemblies.Append("DevTools.Testing.Abstractions.dll").Append("DevTools.NUnit.Core"))
            Assert.DoesNotContain(internalName, nuspecText, StringComparison.Ordinal);

        Assert.Contains("id=\"Microsoft.Testing.Platform.MSBuild\"", nuspecText, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"Microsoft.Testing.Platform\"", nuspecText, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"DevTools.Testing.Abstractions\"", nuspecText, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"DevTools.Testing.Transport\"", nuspecText, StringComparison.Ordinal);
        foreach (var dependency in nuspecText.Split('\n').Where(line =>
                     line.Contains("id=\"Microsoft.Testing.Platform.MSBuild\"", StringComparison.Ordinal)))
        {
            Assert.DoesNotContain("Build,Analyzers", dependency, StringComparison.Ordinal);
            Assert.Contains("2.4.0", dependency, StringComparison.Ordinal);
        }

        Assert.Contains("build/RevitDevTool.TestAdapter.props", entries, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("build/RevitDevTool.TestAdapter.targets", entries, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("build/netfx/ModuleInitializerAttribute.cs", entries, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("build/hooks/NUnitMtpBuilderHook.cs", entries, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("build/hooks/TUnitMtpBuilderHook.cs", entries, StringComparer.OrdinalIgnoreCase);
        // The dev-loop targets stay in the checkout: a consumer build must not see repo paths
        // or this repo's Autodesk configuration names.
        Assert.DoesNotContain("build/RevitDevTool.TestAdapter.Local.targets", entries, StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries.Where(entry => entry.StartsWith("lib/", StringComparison.OrdinalIgnoreCase)))
            Assert.EndsWith("/DevTools.TestAdapter.dll", entry, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("lib/net48/DevTools.TestAdapter.dll", entries, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("lib/net48/DevTools.NUnit.MTP.dll", entries, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("lib/net48/DevTools.Testing.Abstractions.dll", entries, StringComparer.OrdinalIgnoreCase);
        foreach (var tfm in new[] { "net48", "net8.0-windows7.0", "net10.0-windows7.0" })
        {
            if (tfm != "net48")
                Assert.Contains($"lib/{tfm}/DevTools.TestAdapter.dll", entries, StringComparer.OrdinalIgnoreCase);

            Assert.Contains($"build/runtime/{tfm}/DevTools.NUnit.MTP.dll", entries, StringComparer.OrdinalIgnoreCase);
            Assert.Contains($"build/runtime/{tfm}/DevTools.TUnit.MTP.dll", entries, StringComparer.OrdinalIgnoreCase);
            Assert.Contains($"build/runtime/{tfm}/DevTools.Testing.Abstractions.dll", entries, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain($"build/runtime/{tfm}/DevTools.TestAdapter.dll", entries, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain($"build/runtime/{tfm}/DevTools.Ipc.dll", entries, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain($"build/runtime/{tfm}/DevTools.Testing.Transport.dll", entries, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain($"lib/{tfm}/DevTools.NUnit.MTP.dll", entries, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain($"lib/{tfm}/Microsoft.Bcl.AsyncInterfaces.dll", entries, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain($"build/runtime/{tfm}/Microsoft.Bcl.AsyncInterfaces.dll", entries, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain($"build/runtime/{tfm}/System.Text.Json.dll", entries, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain($"build/runtime/{tfm}/System.Text.Encodings.Web.dll", entries, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain($"build/runtime/{tfm}/System.IO.Pipelines.dll", entries, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain($"build/runtime/{tfm}/System.Runtime.CompilerServices.Unsafe.dll", entries, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain($"build/runtime/{tfm}/System.Threading.Tasks.Extensions.dll", entries, StringComparer.OrdinalIgnoreCase);
        }

        Assert.IsFalse(entries.Any(entry => entry.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase)));
        Assert.IsFalse(entries.Any(entry => entry.EndsWith("/DevTools.AssemblyIsolation.dll", StringComparison.OrdinalIgnoreCase)));
        Assert.DoesNotContain("DevTools.AssemblyIsolation", nuspecText, StringComparison.Ordinal);
        Assert.IsFalse(entries.Any(entry => entry.Contains("DevTools.NUnit.Core", StringComparison.OrdinalIgnoreCase)));
        Assert.IsFalse(entries.Any(entry => entry.Contains("DevTools.Testing.Discovery", StringComparison.OrdinalIgnoreCase)));
        Assert.AreEqual(entries.Length, entries.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    private static readonly string[] MergedRuntimeAssemblies =
    [
        "DevTools.Ipc.dll",
        "DevTools.Testing.Transport.dll",
    ];

    private static void AssertKeptRuntime(string outputDirectory)
    {
        var names = Directory.GetFiles(outputDirectory, "*.dll").Select(Path.GetFileName).ToArray();
        Assert.IsTrue(
            File.Exists(Path.Combine(outputDirectory, "DevTools.Testing.Abstractions.dll")),
            $"Missing DevTools.Testing.Abstractions.dll.{Environment.NewLine}{string.Join(Environment.NewLine, names)}");
        Assert.IsTrue(
            File.Exists(Path.Combine(outputDirectory, "DevTools.TestAdapter.dll")),
            $"Missing DevTools.TestAdapter.dll.{Environment.NewLine}{string.Join(Environment.NewLine, names)}");
        foreach (var merged in MergedRuntimeAssemblies)
        {
            Assert.IsFalse(
                File.Exists(Path.Combine(outputDirectory, merged)),
                $"Merged assembly should not be copied loose: {merged}");
        }
    }

    private static void AssertNoInternalPackageRestore(string globalPackages)
    {
        foreach (var internalAssembly in MergedRuntimeAssemblies.Append("DevTools.Testing.Abstractions.dll"))
        {
            var packageDirectory = Path.Combine(globalPackages, Path.GetFileNameWithoutExtension(internalAssembly).ToLowerInvariant());
            Assert.IsFalse(Directory.Exists(packageDirectory), $"Internal package unexpectedly restored: {packageDirectory}");
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

    private static void TryDeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
            return;

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
            // Microsoft.Testing.Platform.MSBuild.dll stays locked in the isolated
            // NUGET_PACKAGES cache after testhost generation.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void WriteIsolatedNuGetConfig(string work, string packages)
    {
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
    }

    private static void Run(string fileName, string arguments, string? workingDirectory = null, string? globalPackages = null)
    {
        var output = RunProcess(fileName, arguments, workingDirectory, globalPackages);
        Assert.IsTrue(output.ExitCode == 0, $"{fileName} {arguments} failed:{Environment.NewLine}{output.Text}");
    }

    private static void RunExpectFailure(string fileName, string arguments, string workingDirectory, string globalPackages, string expectedText)
    {
        var output = RunProcess(fileName, arguments, workingDirectory, globalPackages);
        Assert.IsTrue(output.ExitCode != 0, $"{fileName} {arguments} unexpectedly succeeded.");
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
