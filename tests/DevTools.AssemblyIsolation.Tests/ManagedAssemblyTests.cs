using DevTools.AssemblyIsolation;

namespace DevTools.AssemblyIsolation.Tests;

[TestClass]
public sealed class ManagedAssemblyTests
{
    [TestMethod]
    public void IsManaged_is_true_for_a_managed_dll()
    {
        Assert.IsTrue(ManagedAssembly.IsManaged(typeof(ManagedAssemblyTests).Assembly.Location));
        Assert.IsTrue(ManagedAssembly.TryGetName(typeof(ManagedAssemblyTests).Assembly.Location, out var name));
        Assert.AreEqual(typeof(ManagedAssemblyTests).Assembly.GetName().Name, name.Name);
    }

    [TestMethod]
    public void IsManaged_is_false_for_a_native_pe()
    {
        var path = Path.Combine(Path.GetTempPath(), "managed-assembly-native", Guid.NewGuid().ToString("N"), "native.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, [0x4D, 0x5A, 0x90, 0x00]);

        Assert.IsFalse(ManagedAssembly.IsManaged(path));
        Assert.IsFalse(ManagedAssembly.TryGetName(path, out _));
    }

    [TestMethod]
    public void IsManaged_is_false_for_a_missing_path()
    {
        Assert.IsFalse(ManagedAssembly.IsManaged(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.dll")));
    }
}
