namespace DevTools.NUnit.MTP.Tests;

public sealed class NUnitDiscoveryRefsTests
{
    [Fact]
    public void FilePathFor_uses_assembly_name_suffix()
    {
        var path = NUnitDiscoveryRefs.FilePathFor(@"C:\tests\Host.Tests.dll");
        Assert.Equal(@"C:\tests\Host.Tests.discovery-refs.txt", path);
    }

    [Fact]
    public void Read_maps_simple_name_to_existing_paths()
    {
        var directory = Directory.CreateTempSubdirectory("nunit-discovery-refs-").FullName;
        try
        {
            var assemblyPath = Path.Combine(directory, "Host.Tests.dll");
            File.WriteAllBytes(assemblyPath, [0]);
            var apiPath = Path.Combine(directory, "other", "RevitAPI.dll");
            Directory.CreateDirectory(Path.GetDirectoryName(apiPath)!);
            File.WriteAllBytes(apiPath, [1]);
            File.WriteAllText(
                NUnitDiscoveryRefs.FilePathFor(assemblyPath),
                apiPath + Environment.NewLine + Path.Combine(directory, "missing.dll") + Environment.NewLine);

            var map = NUnitDiscoveryRefs.Read(assemblyPath);

            Assert.Equal(apiPath, Assert.Single(map, pair => pair.Key == "RevitAPI").Value);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Read_skips_framework_targeting_packs()
    {
        var directory = Directory.CreateTempSubdirectory("nunit-discovery-refs-").FullName;
        try
        {
            var assemblyPath = Path.Combine(directory, "Host.Tests.dll");
            File.WriteAllBytes(assemblyPath, [0]);
            var packDir = Path.Combine(directory, "dotnet", "packs", "Microsoft.NETCore.App.Ref");
            Directory.CreateDirectory(packDir);
            var packPath = Path.Combine(packDir, "System.Runtime.dll");
            File.WriteAllBytes(packPath, [1]);
            File.WriteAllText(NUnitDiscoveryRefs.FilePathFor(assemblyPath), packPath);

            Assert.Empty(NUnitDiscoveryRefs.Read(assemblyPath));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Read_missing_file_is_empty()
    {
        Assert.Empty(NUnitDiscoveryRefs.Read(Path.Combine(Path.GetTempPath(), "no-such-tests.dll")));
    }
}
