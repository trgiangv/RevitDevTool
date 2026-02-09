using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using RevitDevTool.Utils;
namespace RevitDevTool.CodeExecute.Providers.Python;

/// <summary>
/// Extracts embedded resources from the assembly to disk.
/// </summary>
// ReSharper disable once PartialTypeWithSinglePart
public static partial class UvInstaller
{
    private const string ResourceNamePattern = @"[^.]+\.[^.]+$";

#if NETCOREAPP
    [GeneratedRegex(ResourceNamePattern)]
    private static partial Regex ResourceNameRegex();
#else
    private static Regex ResourceNameRegex() => new(ResourceNamePattern, RegexOptions.Compiled);
#endif
    
    public static bool IsUvInstalled() => File.Exists(Path.Combine(GetBinPath(), "uv.exe"));
    private static string GetBinPath() => Path.Combine(SettingsUtils.GetApplicationDataPath(), "bin");
    public static Task SetupUvAsync()
    {
        var outputDir = GetBinPath();
        var assembly = Assembly.GetExecutingAssembly();
        Directory.CreateDirectory(outputDir);

        var extractTasks = from resourceName in assembly.GetManifestResourceNames()
            where resourceName.Contains("uv") && resourceName.EndsWith(".exe")
            let fileName = ResourceNameRegex().Match(resourceName).Value
            let outputPath = Path.Combine(outputDir, fileName)
            select Task.Run(() => ExtractResource(assembly, resourceName, outputPath));

        return Task.WhenAll(extractTasks);
    }

    private static void ExtractResource(Assembly assembly, string resourceName, string outputPath)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null) return;
        using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
        stream.CopyTo(fileStream);
    }
}
