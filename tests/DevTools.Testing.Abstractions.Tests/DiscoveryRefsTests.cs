using DevTools.Testing.Abstractions;

namespace DevTools.Testing.Abstractions.Tests;

[TestClass]
public sealed class DiscoveryRefsTests
{
    [TestMethod]
    public void FilePathFor_uses_assembly_name_suffix()
    {
        var path = DiscoveryRefs.FilePathFor(@"C:\tests\Host.Tests.dll");
        Assert.AreEqual(@"C:\tests\Host.Tests.discovery-refs.txt", path);
    }

    [TestMethod]
    public void Read_maps_simple_name_to_existing_paths()
    {
        var directory = Directory.CreateTempSubdirectory("abstractions-discovery-refs-").FullName;
        try
        {
            var assemblyPath = Path.Combine(directory, "Host.Tests.dll");
            File.WriteAllBytes(assemblyPath, [0]);
            var apiPath = Path.Combine(directory, "other", "RevitAPI.dll");
            Directory.CreateDirectory(Path.GetDirectoryName(apiPath)!);
            File.WriteAllBytes(apiPath, [1]);
            File.WriteAllText(
                DiscoveryRefs.FilePathFor(assemblyPath),
                apiPath + Environment.NewLine + Path.Combine(directory, "missing.dll") + Environment.NewLine);

            var map = DiscoveryRefs.Read(assemblyPath);

            Assert.AreEqual(apiPath, map.Single(pair => pair.Key == "RevitAPI").Value);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void Read_skips_blank_lines_and_missing_files()
    {
        var directory = Directory.CreateTempSubdirectory("abstractions-discovery-refs-").FullName;
        try
        {
            var assemblyPath = Path.Combine(directory, "Host.Tests.dll");
            File.WriteAllBytes(assemblyPath, [0]);
            File.WriteAllText(
                DiscoveryRefs.FilePathFor(assemblyPath),
                "   " + Environment.NewLine + Path.Combine(directory, "gone.dll"));

            Assert.IsEmpty(DiscoveryRefs.Read(assemblyPath));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void Read_skips_framework_targeting_packs()
    {
        var directory = Directory.CreateTempSubdirectory("abstractions-discovery-refs-").FullName;
        try
        {
            var assemblyPath = Path.Combine(directory, "Host.Tests.dll");
            File.WriteAllBytes(assemblyPath, [0]);
            var packDir = Path.Combine(directory, "Reference Assemblies", "Microsoft", "Framework");
            Directory.CreateDirectory(packDir);
            var packPath = Path.Combine(packDir, "mscorlib.dll");
            File.WriteAllBytes(packPath, [1]);
            File.WriteAllText(DiscoveryRefs.FilePathFor(assemblyPath), packPath);

            Assert.IsEmpty(DiscoveryRefs.Read(assemblyPath));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void Read_skips_nuget_cached_targeting_packs()
    {
        var directory = Directory.CreateTempSubdirectory("abstractions-discovery-refs-").FullName;
        try
        {
            var assemblyPath = Path.Combine(directory, "Host.Tests.dll");
            File.WriteAllBytes(assemblyPath, [0]);
            var packDir = Path.Combine(
                directory, "packages", "microsoft.netcore.app.ref", "8.0.31", "ref", "net8.0");
            Directory.CreateDirectory(packDir);
            var packPath = Path.Combine(packDir, "System.Runtime.dll");
            File.WriteAllBytes(packPath, [1]);
            var apiDir = Path.Combine(directory, "revit");
            Directory.CreateDirectory(apiDir);
            var apiPath = Path.Combine(apiDir, "RevitAPI.dll");
            File.WriteAllBytes(apiPath, [2]);
            File.WriteAllText(
                DiscoveryRefs.FilePathFor(assemblyPath),
                packPath + Environment.NewLine + apiPath);

            var map = DiscoveryRefs.Read(assemblyPath);
            Assert.AreEqual(apiPath, map.Single(pair => pair.Key == "RevitAPI").Value);
            Assert.IsFalse(map.ContainsKey("System.Runtime"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void Read_missing_file_is_empty()
    {
        Assert.IsEmpty(DiscoveryRefs.Read(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".dll")));
    }
}
