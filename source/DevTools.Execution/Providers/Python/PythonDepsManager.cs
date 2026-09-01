using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CliWrap;
using DevTools.Execution.Models;
using Python.Runtime;

namespace DevTools.Execution.Providers.Python;

public static class PythonDepsManager
{
    private sealed record ParseResult(
        [property: JsonPropertyName("requires_python")] string RequiresPython, 
        [property: JsonPropertyName("to_install")] List<string> ToInstall)
    {
        public static readonly ParseResult Empty = new(string.Empty, []);
    }

    public static async Task<List<string>> ResolveDependenciesAsync(
        PyEnvironmentProvider provider,
        string scriptPathOrCode,
        CancellationToken cancellationToken = default)
    {
        string actualPath;
        var isTemp = false;

        if (File.Exists(scriptPathOrCode))
        {
            actualPath = scriptPathOrCode;
        }
        else
        {
            if (!scriptPathOrCode.Contains("# /// script"))
                return [];

            actualPath = Path.Combine(Path.GetTempPath(), $"pep723_{Guid.NewGuid():N}.py");
            await File.WriteAllTextAsync(actualPath, scriptPathOrCode, cancellationToken).ConfigureAwait(false);
            isTemp = true;
        }

        try
        {
            var stdinContent = await provider.GetListJsonAsync(cancellationToken).ConfigureAwait(false);
            var result = await RunParserAsync(provider, actualPath, stdinContent, cancellationToken).ConfigureAwait(false);
            return result.ToInstall;
        }
        finally
        {
            if (isTemp)
            {
                try { File.Delete(actualPath); }
                catch { /* best-effort */ }
            }
        }
    }

    public static async Task InstallDependenciesAsync(
        PyEnvironmentProvider provider,
        IEnumerable<string> dependencies,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        var depList = dependencies.ToList();
        if (depList.Count == 0) return;

        progress.Report($"Installing {depList.Count} package(s)...");

        if (!provider.IsEnvironmentReady())
            throw new DirectoryNotFoundException(
                $"Python environment is not ready at {provider.PythonHome}.");

        await provider.InstallPackagesAsync(depList, progress, cancellationToken).ConfigureAwait(false);

        progress.Report($"All {depList.Count} package(s) installed.");
    }

    private static async Task<ParseResult> RunParserAsync(
        PyEnvironmentProvider provider,
        string scriptPath,
        string stdinContent,
        CancellationToken cancellationToken)
    {
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        var cmd = await Cli.Wrap(provider.PythonExe)
            .WithArguments([PythonEmbedded.ParserScriptPath, scriptPath])
            .WithWorkingDirectory(provider.PythonHome)
            .WithStandardInputPipe(PipeSource.FromString(stdinContent))
            .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdout))
            .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stderr))
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync(cancellationToken)
            .ConfigureAwait(false);

        if (cmd.ExitCode != 0)
        {
            var err = stderr.ToString().Trim();
            throw new InvalidOperationException($"Parser.py failed (exit {cmd.ExitCode}): {err}");
        }

        var json = stdout.ToString().Trim();
        if (string.IsNullOrEmpty(json)) return ParseResult.Empty;

        return JsonSerializer.Deserialize<ParseResult>(json) ?? ParseResult.Empty;
    }

    public static void RefreshImportCache(PythonInitializer initializer)
    {
        if (!initializer.IsInitialized) return;

        using (Py.GIL())
            InjectSitePackages(initializer);
    }

    internal static string InjectSitePackages(PythonInitializer initializer)
    {
        var sitePackages = initializer.Provider?.SitePackagesDir ?? string.Empty;
        using var scope = Py.CreateScope();

        if (initializer.HostOwnsInterpreter)
        {
            if (!TryResolveSidecarStdlib(initializer.Provider, out var stdlibLib, out var stdlibDlls)
                || stdlibDlls.Length == 0)
            {
                throw new InvalidOperationException(
                    "Host attach requires sidecar Lib and DLLs next to pyvenv.cfg home.");
            }

            // Plant 3D: python313.zip next to python313.dll. Append sidecar DLLs+Lib
            // (host zip stays first). encodings is already imported from the zip —
            // encodings.idna is resolved via encodings.__path__, not sys.path.
            scope.Set("site_packages", new PyString(sitePackages));
            scope.Set("stdlib_lib", new PyString(stdlibLib));
            scope.Set("stdlib_dlls", new PyString(stdlibDlls));
            scope.Exec("""
                import codecs, encodings, importlib, os, site, sys

                if stdlib_dlls not in sys.path:
                    sys.path.append(stdlib_dlls)
                if stdlib_lib not in sys.path:
                    sys.path.append(stdlib_lib)
                if site_packages not in sys.path:
                    site.addsitedir(site_packages)

                enc_dir = os.path.join(stdlib_lib, "encodings")
                if enc_dir not in encodings.__path__:
                    encodings.__path__.append(enc_dir)
                encodings._cache.clear()
                importlib.invalidate_caches()

                import select, stringprep, unicodedata
                codecs.lookup("idna")
                __overlay_probe__ = "unicodedata=%s; stringprep=%s; select=%s; idna=%s" % (
                    unicodedata.__file__,
                    stringprep.__file__,
                    select.__file__,
                    codecs.lookup("idna").name,
                )
                """);
            return scope.Get("__overlay_probe__").As<string>() ?? string.Empty;
        }

        if (sitePackages.Length == 0 || !Directory.Exists(sitePackages))
        {
            scope.Exec("import importlib; importlib.invalidate_caches()");
            return string.Empty;
        }

        scope.Set("site_packages", new PyString(sitePackages));
        scope.Exec("""
            import importlib, sys
            if site_packages not in sys.path:
                sys.path.insert(0, site_packages)
            importlib.invalidate_caches()
            """);
        return string.Empty;
    }

    /// <summary>Matching-version sidecar <c>Lib</c> + <c>DLLs</c> (pyvenv home, else uv-python scan).</summary>
    internal static bool TryResolveSidecarStdlib(PyEnvironmentProvider? provider, out string stdlibLib, out string stdlibDlls)
        => TryResolveSidecarStdlib(
            provider?.StdlibLibDir ?? string.Empty,
            provider?.Backend == PythonBackend.Uv ? UvEnvironmentProvider.UvPythonInstallDir : null,
            out stdlibLib,
            out stdlibDlls);

    internal static bool TryResolveSidecarStdlib(
        string stdlibLibDir,
        string? uvPythonInstallDir,
        out string stdlibLib,
        out string stdlibDlls)
    {
        if (TryStdlibFromLibDir(stdlibLibDir, out stdlibLib, out stdlibDlls))
            return true;

        if (string.IsNullOrEmpty(uvPythonInstallDir) || !Directory.Exists(uvPythonInstallDir))
            return false;

        foreach (var dir in Directory.EnumerateDirectories(uvPythonInstallDir, "cpython-*"))
        {
            if (TryStdlibFromLibDir(Path.Combine(dir, "Lib"), out stdlibLib, out stdlibDlls))
                return true;
        }

        return false;
    }

    private static bool TryStdlibFromLibDir(string lib, out string stdlibLib, out string stdlibDlls)
    {
        stdlibLib = string.Empty;
        stdlibDlls = string.Empty;
        if (string.IsNullOrEmpty(lib) || !Directory.Exists(lib))
            return false;

        stdlibLib = lib;
        var prefix = Path.GetDirectoryName(lib);
        if (!string.IsNullOrEmpty(prefix))
        {
            var dlls = Path.Combine(prefix, "DLLs");
            if (Directory.Exists(dlls))
                stdlibDlls = dlls;
        }

        return true;
    }
}
