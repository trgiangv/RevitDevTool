using DevTools.Execution.Providers.Python;

namespace DevTools.Execution.Tests;

public sealed class PythonNativeEnvironmentTests
{
    [Fact]
    public void TryGetLibraryBin_ReturnsLibraryBinOnly()
    {
        var home = CreateFakeHome(withDlls: true, withLibraryBin: true);
        try
        {
            var libraryBin = PythonNativeEnvironment.TryGetLibraryBin(home);

            Assert.Equal(
                Path.GetFullPath(Path.Combine(home, "Library", "bin")),
                libraryBin,
                StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(home, recursive: true);
        }
    }

    [Fact]
    public void TryGetLibraryBin_NullWhenMissingEvenIfHomeAndDllsExist()
    {
        var home = CreateFakeHome(withDlls: true, withLibraryBin: false);
        try
        {
            Assert.Null(PythonNativeEnvironment.TryGetLibraryBin(home));
        }
        finally
        {
            Directory.Delete(home, recursive: true);
        }
    }

    [Fact]
    public void GetLibrariesToPreload_CryptoThenSsl_FromLibraryBinOnly()
    {
        var home = CreateFakeHome(withDlls: true, withLibraryBin: true);
        try
        {
            var libraryBin = Path.Combine(home, "Library", "bin");
            var dlls = Path.Combine(home, "DLLs");
            File.WriteAllBytes(Path.Combine(libraryBin, "libcrypto-3-x64.dll"), [1]);
            File.WriteAllBytes(Path.Combine(libraryBin, "libssl-3-x64.dll"), [1]);
            File.WriteAllBytes(Path.Combine(dlls, "libcrypto-3-x64.dll"), [2]);
            File.WriteAllBytes(Path.Combine(dlls, "libssl-3-x64.dll"), [2]);
            File.WriteAllBytes(Path.Combine(home, "other.dll"), [3]);

            var loaded = PythonNativeEnvironment.GetLibrariesToPreload(home);

            Assert.Equal(2, loaded.Count);
            Assert.EndsWith("libcrypto-3-x64.dll", loaded[0], StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith("libssl-3-x64.dll", loaded[1], StringComparison.OrdinalIgnoreCase);
            Assert.StartsWith(Path.GetFullPath(libraryBin), Path.GetFullPath(loaded[0]), StringComparison.OrdinalIgnoreCase);
            Assert.StartsWith(Path.GetFullPath(libraryBin), Path.GetFullPath(loaded[1]), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(home, recursive: true);
        }
    }

    [Fact]
    public void SelectHostPythonDll_PrefersVersionedOverStableAbiForwarder()
    {
        var selected = PythonNativeEnvironment.SelectHostPythonDll(
        [
            @"C:\Host\python3.dll",
            @"C:\Host\PLNT3D\python313.dll",
        ]);

        Assert.Equal(@"C:\Host\PLNT3D\python313.dll", selected);
    }

    [Fact]
    public void SelectHostPythonDll_FallsBackToStableAbiForwarder()
    {
        var selected = PythonNativeEnvironment.SelectHostPythonDll(
            [@"C:\Host\python3.dll"]);

        Assert.Equal(@"C:\Host\python3.dll", selected);
    }

    [Fact]
    public void TryGetCPythonVersion_FromVersionedDllName()
    {
        Assert.True(PythonNativeEnvironment.TryGetCPythonVersion(
            @"C:\Host\PLNT3D\python313.dll", out var v313));
        Assert.Equal("3.13", v313);

        Assert.True(PythonNativeEnvironment.TryGetCPythonVersion(
            @"C:\Python\python314.dll", out var v314));
        Assert.Equal("3.14", v314);

        Assert.True(PythonNativeEnvironment.TryGetCPythonVersion(
            @"C:\Python\python310.dll", out var v310));
        Assert.Equal("3.10", v310);

        Assert.True(PythonNativeEnvironment.TryGetCPythonVersion(
            @"C:\Python\python38.dll", out var v38));
        Assert.Equal("3.8", v38);
    }

    [Fact]
    public void TryGetCPythonVersion_DebugAndFreeThreadedSuffix()
    {
        Assert.True(PythonNativeEnvironment.TryGetCPythonVersion("python313_d.dll", out var debug));
        Assert.Equal("3.13", debug);
        Assert.True(PythonNativeEnvironment.TryGetCPythonVersion("python313_t.dll", out var free));
        Assert.Equal("3.13", free);
    }

    [Fact]
    public void TryGetCPythonVersion_StableAbiForwarderHasNoMinor()
    {
        Assert.False(PythonNativeEnvironment.TryGetCPythonVersion(@"C:\Host\python3.dll", out _));
    }

    [Fact]
    public void SelectHostPythonDll_EmptyWhenNoCandidates()
    {
        Assert.Null(PythonNativeEnvironment.SelectHostPythonDll([]));
    }

    [Fact]
    public void SelectHostPythonVersion_PrefersVersionedOverForwarder()
    {
        var version = PythonNativeEnvironment.SelectHostPythonVersion(
        [
            @"C:\Host\python3.dll",
            @"C:\Host\PLNT3D\python313.dll",
        ]);

        Assert.Equal("3.13", version);
    }

    [Fact]
    public void SelectHostPythonVersion_NullWhenOnlyForwarder()
    {
        var version = PythonNativeEnvironment.SelectHostPythonVersion(
            [@"C:\Host\python3.dll"]);

        Assert.Null(version);
    }

    [Fact]
    public void SelectHostPythonVersion_NullWhenEmpty()
    {
        Assert.Null(PythonNativeEnvironment.SelectHostPythonVersion([]));
    }

    [Fact]
    public void ResolveHostVersion_ReadsVersionedDll()
    {
        Assert.Equal("3.13", PythonNativeEnvironment.ResolveHostVersion(@"C:\Host\PLNT3D\python313.dll"));
    }

    [Fact]
    public void ResolveHostVersion_ForwarderUsesSiblingVersionedDll()
    {
        var dir = Path.Combine(Path.GetTempPath(), "rdt-native-ver-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var forwarder = Path.Combine(dir, "python3.dll");
            File.WriteAllBytes(forwarder, [1]);
            File.WriteAllBytes(Path.Combine(dir, "python313.dll"), [1]);

            Assert.Equal("3.13", PythonNativeEnvironment.ResolveHostVersion(forwarder));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void LoadStableAbiForwarder_NoopsWhenMissing()
    {
        PythonNativeEnvironment.LoadStableAbiForwarder(string.Empty);
        PythonNativeEnvironment.LoadStableAbiForwarder(
            Path.Combine(Path.GetTempPath(), "rdt-no-python3-" + Guid.NewGuid().ToString("N")));
    }

    [Fact]
    public void GetLibrariesToPreload_IgnoresOpenSslOutsideLibraryBin()
    {
        var home = CreateFakeHome(withDlls: true, withLibraryBin: true);
        try
        {
            File.WriteAllBytes(Path.Combine(home, "libcrypto-3-x64.dll"), [1]);
            File.WriteAllBytes(Path.Combine(home, "DLLs", "libssl-3-x64.dll"), [1]);

            Assert.Empty(PythonNativeEnvironment.GetLibrariesToPreload(home));
        }
        finally
        {
            Directory.Delete(home, recursive: true);
        }
    }

    [Fact]
    public void GetLibrariesToPreload_EmptyWhenNoOpenSsl()
    {
        var home = CreateFakeHome(withDlls: true, withLibraryBin: true);
        try
        {
            File.WriteAllBytes(Path.Combine(home, "python313.dll"), [1]);

            Assert.Empty(PythonNativeEnvironment.GetLibrariesToPreload(home));
        }
        finally
        {
            Directory.Delete(home, recursive: true);
        }
    }

    [Fact]
    public void PrepareProcess_WithLibraryBin_DoesNotThrow()
    {
        var home = CreateFakeHome(withDlls: true, withLibraryBin: true);
        try
        {
            File.WriteAllBytes(Path.Combine(home, "Library", "bin", "libcrypto-3-x64.dll"), [1]);
            PythonNativeEnvironment.PrepareProcess(home);
        }
        finally
        {
            Directory.Delete(home, recursive: true);
        }
    }

    [Collection(nameof(PythonRuntimeCollection))]
    public sealed class InitializedPythonNativeTests
    {
        [Fact]
        public async Task AddPythonDllDirectories_WhenInitialized_DoesNotThrow()
        {
            var initializer = await ExecutionTestHelpers.EnsurePixiPythonInitializedAsync();
            var home = initializer.Provider!.PythonHome;
            PythonNativeEnvironment.AddPythonDllDirectories(home);
        }
    }

    private static string CreateFakeHome(bool withDlls, bool withLibraryBin)
    {
        var home = Path.Combine(Path.GetTempPath(), "rdt-py-native-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(home);
        if (withDlls)
            Directory.CreateDirectory(Path.Combine(home, "DLLs"));
        if (withLibraryBin)
            Directory.CreateDirectory(Path.Combine(home, "Library", "bin"));
        return home;
    }
}
