using DevTools.Execution.Providers.Python;

namespace DevTools.Execution.Tests;

public sealed class PythonNativeEnvironmentTests
{
    [Fact]
    public void GetSearchDirectories_ReturnsExistingHomeDllsAndLibraryBin()
    {
        var home = CreateFakeHome(withDlls: true, withLibraryBin: true);
        try
        {
            var dirs = PythonNativeEnvironment.GetSearchDirectories(home);

            Assert.Equal(3, dirs.Count);
            Assert.Equal(Path.GetFullPath(home), dirs[0], StringComparer.OrdinalIgnoreCase);
            Assert.Equal(Path.GetFullPath(Path.Combine(home, "DLLs")), dirs[1], StringComparer.OrdinalIgnoreCase);
            Assert.Equal(
                Path.GetFullPath(Path.Combine(home, "Library", "bin")),
                dirs[2],
                StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(home, recursive: true);
        }
    }

    [Fact]
    public void GetSearchDirectories_SkipsMissingDllsAndLibraryBin()
    {
        var home = CreateFakeHome(withDlls: false, withLibraryBin: false);
        try
        {
            var dirs = PythonNativeEnvironment.GetSearchDirectories(home);

            Assert.Equal([Path.GetFullPath(home)], dirs);
        }
        finally
        {
            Directory.Delete(home, recursive: true);
        }
    }

    [Fact]
    public void GetLibrariesToPreload_CryptoThenSsl_PrefersLibraryBin()
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
