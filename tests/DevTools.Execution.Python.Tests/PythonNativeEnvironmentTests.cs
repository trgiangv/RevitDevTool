using DevTools.Execution.Providers.Python;

namespace DevTools.Execution.Tests;

[TestClass]
public sealed class PythonNativeEnvironmentTests
{
    [TestMethod]
    public void TryGetLibraryBin_ReturnsLibraryBinOnly()
    {
        var home = CreateFakeHome(withDlls: true, withLibraryBin: true);
        try
        {
            var libraryBin = PythonNativeEnvironment.TryGetLibraryBin(home);

            Assert.AreEqual(
                Path.GetFullPath(Path.Combine(home, "Library", "bin")),
                libraryBin,
                StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(home, recursive: true);
        }
    }

    [TestMethod]
    public void TryGetLibraryBin_NullWhenMissingEvenIfHomeAndDllsExist()
    {
        var home = CreateFakeHome(withDlls: true, withLibraryBin: false);
        try
        {
            Assert.IsNull(PythonNativeEnvironment.TryGetLibraryBin(home));
        }
        finally
        {
            Directory.Delete(home, recursive: true);
        }
    }

    [TestMethod]
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

            Assert.AreEqual(2, loaded.Count);
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

    [TestMethod]
    public void SelectHostPythonDll_PrefersVersionedOverStableAbiForwarder()
    {
        var selected = PythonNativeEnvironment.SelectHostPythonDll(
        [
            @"C:\Host\python3.dll",
            @"C:\Host\PLNT3D\python313.dll",
        ]);

        Assert.AreEqual(@"C:\Host\PLNT3D\python313.dll", selected);
    }

    [TestMethod]
    public void SelectHostPythonDll_FallsBackToStableAbiForwarder()
    {
        var selected = PythonNativeEnvironment.SelectHostPythonDll(
            [@"C:\Host\python3.dll"]);

        Assert.AreEqual(@"C:\Host\python3.dll", selected);
    }

    [TestMethod]
    public void TryGetCPythonVersion_FromVersionedDllName()
    {
        Assert.IsTrue(PythonNativeEnvironment.TryGetCPythonVersion(
            @"C:\Host\PLNT3D\python313.dll", out var v313));
        Assert.AreEqual("3.13", v313);

        Assert.IsTrue(PythonNativeEnvironment.TryGetCPythonVersion(
            @"C:\Python\python314.dll", out var v314));
        Assert.AreEqual("3.14", v314);

        Assert.IsTrue(PythonNativeEnvironment.TryGetCPythonVersion(
            @"C:\Python\python310.dll", out var v310));
        Assert.AreEqual("3.10", v310);

        Assert.IsTrue(PythonNativeEnvironment.TryGetCPythonVersion(
            @"C:\Python\python38.dll", out var v38));
        Assert.AreEqual("3.8", v38);
    }

    [TestMethod]
    public void TryGetCPythonVersion_DebugAndFreeThreadedSuffix()
    {
        Assert.IsTrue(PythonNativeEnvironment.TryGetCPythonVersion("python313_d.dll", out var debug));
        Assert.AreEqual("3.13", debug);
        Assert.IsTrue(PythonNativeEnvironment.TryGetCPythonVersion("python313_t.dll", out var free));
        Assert.AreEqual("3.13", free);
    }

    [TestMethod]
    public void TryGetCPythonVersion_StableAbiForwarderHasNoMinor()
    {
        Assert.IsFalse(PythonNativeEnvironment.TryGetCPythonVersion(@"C:\Host\python3.dll", out _));
    }

    [TestMethod]
    public void SelectHostPythonDll_EmptyWhenNoCandidates()
    {
        Assert.IsNull(PythonNativeEnvironment.SelectHostPythonDll([]));
    }

    [TestMethod]
    public void SelectHostPythonVersion_PrefersVersionedOverForwarder()
    {
        var version = PythonNativeEnvironment.SelectHostPythonVersion(
        [
            @"C:\Host\python3.dll",
            @"C:\Host\PLNT3D\python313.dll",
        ]);

        Assert.AreEqual("3.13", version);
    }

    [TestMethod]
    public void SelectHostPythonVersion_NullWhenOnlyForwarder()
    {
        var version = PythonNativeEnvironment.SelectHostPythonVersion(
            [@"C:\Host\python3.dll"]);

        Assert.IsNull(version);
    }

    [TestMethod]
    public void SelectHostPythonVersion_NullWhenEmpty()
    {
        Assert.IsNull(PythonNativeEnvironment.SelectHostPythonVersion([]));
    }

    [TestMethod]
    public void ResolveHostVersion_ReadsVersionedDll()
    {
        Assert.AreEqual("3.13", PythonNativeEnvironment.ResolveHostVersion(@"C:\Host\PLNT3D\python313.dll"));
    }

    [TestMethod]
    public void ResolveHostVersion_ForwarderUsesSiblingVersionedDll()
    {
        var dir = Path.Combine(Path.GetTempPath(), "rdt-native-ver-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var forwarder = Path.Combine(dir, "python3.dll");
            File.WriteAllBytes(forwarder, [1]);
            File.WriteAllBytes(Path.Combine(dir, "python313.dll"), [1]);

            Assert.AreEqual("3.13", PythonNativeEnvironment.ResolveHostVersion(forwarder));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [TestMethod]
    public void LoadStableAbiForwarder_NoopsWhenMissing()
    {
        PythonNativeEnvironment.LoadStableAbiForwarder(string.Empty);
        PythonNativeEnvironment.LoadStableAbiForwarder(
            Path.Combine(Path.GetTempPath(), "rdt-no-python3-" + Guid.NewGuid().ToString("N")));
    }

    [TestMethod]
    public void GetLibrariesToPreload_IgnoresOpenSslOutsideLibraryBin()
    {
        var home = CreateFakeHome(withDlls: true, withLibraryBin: true);
        try
        {
            File.WriteAllBytes(Path.Combine(home, "libcrypto-3-x64.dll"), [1]);
            File.WriteAllBytes(Path.Combine(home, "DLLs", "libssl-3-x64.dll"), [1]);

            Assert.IsEmpty(PythonNativeEnvironment.GetLibrariesToPreload(home));
        }
        finally
        {
            Directory.Delete(home, recursive: true);
        }
    }

    [TestMethod]
    public void GetLibrariesToPreload_EmptyWhenNoOpenSsl()
    {
        var home = CreateFakeHome(withDlls: true, withLibraryBin: true);
        try
        {
            File.WriteAllBytes(Path.Combine(home, "python313.dll"), [1]);

            Assert.IsEmpty(PythonNativeEnvironment.GetLibrariesToPreload(home));
        }
        finally
        {
            Directory.Delete(home, recursive: true);
        }
    }

    [TestMethod]
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

    [TestClass]
    public sealed class InitializedPythonNativeTests
    {
        [TestMethod]
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
