using DevTools.Execution.Providers.Python;

namespace DevTools.Execution.Tests;

public sealed class PyEnvironmentProviderStdlibTests
{
    [Fact]
    public void TryReadPyvenvHome_ReadsHomeLine()
    {
        var dir = Directory.CreateTempSubdirectory("pyvenv-cfg-");
        try
        {
            var cfg = Path.Combine(dir.FullName, "pyvenv.cfg");
            File.WriteAllText(cfg, """
                home = C:\Users\truon\AppData\Roaming\RevitDevTool\uv-env\uv-python\cpython-3.13-windows-x86_64-none
                implementation = CPython
                uv = 0.12.8
                version_info = 3.13
                include-system-site-packages = false
                """);

            Assert.True(PyEnvironmentProvider.TryReadPyvenvHome(cfg, out var home));
            Assert.Equal(
                @"C:\Users\truon\AppData\Roaming\RevitDevTool\uv-env\uv-python\cpython-3.13-windows-x86_64-none",
                home);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void TryReadPyvenvHome_MissingFile_ReturnsFalse()
    {
        Assert.False(PyEnvironmentProvider.TryReadPyvenvHome(
            Path.Combine(Path.GetTempPath(), "no-such-pyvenv.cfg"),
            out var home));
        Assert.Equal(string.Empty, home);
    }

    [Fact]
    public void TryResolveSidecarStdlib_UsesLibAndSiblingDlls()
    {
        var root = Directory.CreateTempSubdirectory("sidecar-stdlib-");
        try
        {
            var lib = Path.Combine(root.FullName, "Lib");
            var dlls = Path.Combine(root.FullName, "DLLs");
            Directory.CreateDirectory(lib);
            Directory.CreateDirectory(dlls);

            Assert.True(PythonDepsManager.TryResolveSidecarStdlib(lib, uvPythonInstallDir: null, out var resolvedLib, out var resolvedDlls));
            Assert.Equal(lib, resolvedLib);
            Assert.Equal(dlls, resolvedDlls);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public void TryResolveSidecarStdlib_LibWithoutDlls_StillResolvesLib()
    {
        var root = Directory.CreateTempSubdirectory("sidecar-lib-only-");
        try
        {
            var lib = Path.Combine(root.FullName, "Lib");
            Directory.CreateDirectory(lib);

            Assert.True(PythonDepsManager.TryResolveSidecarStdlib(lib, uvPythonInstallDir: null, out var resolvedLib, out var resolvedDlls));
            Assert.Equal(lib, resolvedLib);
            Assert.Equal(string.Empty, resolvedDlls);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public void TryResolveSidecarStdlib_ScansUvPythonWhenStdlibMissing()
    {
        var root = Directory.CreateTempSubdirectory("sidecar-uv-");
        try
        {
            var prefix = Path.Combine(root.FullName, "cpython-3.13.15-windows-x86_64-none");
            var lib = Path.Combine(prefix, "Lib");
            var dlls = Path.Combine(prefix, "DLLs");
            Directory.CreateDirectory(lib);
            Directory.CreateDirectory(dlls);

            Assert.True(PythonDepsManager.TryResolveSidecarStdlib(
                stdlibLibDir: string.Empty,
                uvPythonInstallDir: root.FullName,
                out var resolvedLib,
                out var resolvedDlls));
            Assert.Equal(lib, resolvedLib);
            Assert.Equal(dlls, resolvedDlls);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }
}
