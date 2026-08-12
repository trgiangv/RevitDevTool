using System.Reflection;

namespace DevTools.NUnit.Host.Tests;

internal static class NUnitGenerationShadowLoadTestHelper
{
    internal static Assembly LoadShadowAssembly(string shadowAssemblyPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shadowAssemblyPath);

        var fullPath = Path.GetFullPath(shadowAssemblyPath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Shadow assembly not found: {fullPath}", fullPath);

        return Assembly.LoadFrom(fullPath);
    }

    internal static void AssertSourceOutputsRemainWritable(string sourceDllPath, string sourcePdbPath)
    {
        Assert.True(File.Exists(sourceDllPath));
        Assert.True(File.Exists(sourcePdbPath));

        using (var dllStream = new FileStream(
                   sourceDllPath,
                   FileMode.Open,
                   FileAccess.ReadWrite,
                   FileShare.ReadWrite | FileShare.Delete))
        {
            Assert.True(dllStream.CanWrite);
            var firstByte = (byte)dllStream.ReadByte();
            dllStream.Position = 0;
            dllStream.WriteByte(firstByte);
        }

        using (var pdbStream = new FileStream(
                   sourcePdbPath,
                   FileMode.Open,
                   FileAccess.ReadWrite,
                   FileShare.ReadWrite | FileShare.Delete))
        {
            Assert.True(pdbStream.CanWrite);
            var firstByte = (byte)pdbStream.ReadByte();
            pdbStream.Position = 0;
            pdbStream.WriteByte(firstByte);
        }
    }
}
