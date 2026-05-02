using CliWrap;
using CliWrap.Buffered;

namespace Installer;

/// <summary>
/// Inno Setup CLI Builder - Generates temporary Setup.iss and compiles to .exe
/// </summary>
internal static class Program
{
    private const string AppId = "B2BC2881-A08A-41D8-B1B3-424045E529DB";
    private const string AppName = "RevitDevTool";

    /// <summary>
    /// CLI entry point - delegates to specific handlers for each phase
    /// </summary>
    public static async Task<int> Main(string[] args)
    {
        if (!TryParseArguments(args, out var version, out var bundlePath, out var outputPath))
            return 1;

        var tempDir = CreateTempDirectory();

        try
        {
            await BuildInstaller(version, bundlePath, outputPath, tempDir);
            return 0;
        }
        catch (Exception ex)
        {
            await LogErrorAsync(ex);
            return 1;
        }
        finally
        {
            CleanupTempDirectory(tempDir);
        }
    }

    /// <summary>
    /// Validate and parse CLI arguments
    /// </summary>
    private static bool TryParseArguments(string[] args, out string version, out string bundlePath, out string outputPath)
    {
        version = string.Empty;
        bundlePath = string.Empty;
        outputPath = string.Empty;

        if (args.Length < 3)
        {
            PrintUsage();
            return false;
        }

        version = args[0];
        bundlePath = Path.GetFullPath(args[1]);
        outputPath = Path.GetFullPath(args[2]);

        if (!Directory.Exists(bundlePath))
        {
            Console.Error.WriteLine($"Error: Bundle path does not exist: {bundlePath}");
            return false;
        }

        Directory.CreateDirectory(outputPath);
        return true;
    }

    /// <summary>
    /// Print CLI usage information
    /// </summary>
    private static void PrintUsage()
    {
        Console.WriteLine("""
            InnoSetup Builder - CLI Tool for RevitDevTool

            Usage:
              InnoSetupBuilder.exe <version> <bundle-path> <output-path>

            Arguments:
              version      - Version string (e.g., "1.0.0" or "1.0.0-beta.1")
              bundle-path  - Path to bundle folder containing PackageContents.xml and Contents/
              output-path  - Path where .exe installer will be generated

            Example:
              InnoSetupBuilder.exe 1.2.3 C:\build\RevitDevTool.bundle C:\output
            """);
    }

    /// <summary>
    /// Create unique temp directory for build process
    /// </summary>
    private static string CreateTempDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"InnoSetupBuilder-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        return tempDir;
    }

    /// <summary>
    /// Main build orchestration
    /// </summary>
    private static async Task BuildInstaller(string version, string bundlePath, string outputPath, string tempDir)
    {
        PrintBuildInfo(version, bundlePath, outputPath, tempDir);

        await PrepareBuildEnvironment(bundlePath, tempDir);
        var outputIssPath = await GenerateSetupScript(version, tempDir);
        var isccPath = ValidateIsccAvailable();
        var setupOutputDir = await CompileWithIscc(isccPath, outputIssPath, tempDir);

        await MoveOutputToDestination(setupOutputDir, outputPath);
    }

    /// <summary>
    /// Print build configuration information
    /// </summary>
    private static void PrintBuildInfo(string version, string bundlePath, string outputPath, string tempDir)
    {
        Console.WriteLine($"Building installer for {AppName} v{version}");
        Console.WriteLine($"Bundle: {bundlePath}");
        Console.WriteLine($"Output: {outputPath}");
        Console.WriteLine($"Temp: {tempDir}");
    }

    /// <summary>
    /// Copy bundle contents to temp directory (same as Setup.iss)
    /// </summary>
    private static async Task PrepareBuildEnvironment(string bundlePath, string tempDir)
    {
        await Task.Run(() => CopyDirectory(bundlePath, tempDir));
    }

    /// <summary>
    /// Load template, process placeholders, write to temp, copy Resources
    /// </summary>
    private static async Task<string> GenerateSetupScript(string version, string tempDir)
    {
        var templatePath = GetTemplatePath();
        var setupIssContent = await File.ReadAllTextAsync(templatePath);
        var processedIss = ProcessTemplate(setupIssContent, version);

        var outputIssPath = Path.Combine(tempDir, "Setup.iss");
        await File.WriteAllTextAsync(outputIssPath, processedIss);

        // Copy Resources folder to temp directory
        var templateDir = Path.GetDirectoryName(templatePath)!;
        var sourceResources = Path.Combine(templateDir, "Resources");
        var destResources = Path.Combine(tempDir, "Resources");
        if (Directory.Exists(sourceResources))
        {
            await Task.Run(() => CopyDirectory(sourceResources, destResources));
        }

        var sourceIncludes = Path.Combine(templateDir, "includes");
        var destIncludes = Path.Combine(tempDir, "includes");
        if (Directory.Exists(sourceIncludes))
        {
            await Task.Run(() => CopyDirectory(sourceIncludes, destIncludes));
        }

        Console.WriteLine($"Generated: {outputIssPath}");
        return outputIssPath;
    }

    /// <summary>
    /// Validate ISCC is available in PATH
    /// </summary>
    private static string ValidateIsccAvailable()
    {
        var isccPath = FindIscc();
        if (string.IsNullOrEmpty(isccPath))
        {
            throw new InvalidOperationException("""
                ISCC (Inno Setup Compiler) not found in PATH.
                Please install Inno Setup 6 and ensure ISCC.exe is in your PATH.
                Download: https://jrsoftware.org/isdl.php
                """);
        }

        Console.WriteLine($"Using ISCC: {isccPath}");
        return isccPath;
    }

    /// <summary>
    /// Compile Setup.iss with ISCC
    /// </summary>
    private static async Task<string> CompileWithIscc(string isccPath, string issPath, string tempDir)
    {
        var setupOutputDir = Path.Combine(tempDir, "Output");
        Directory.CreateDirectory(setupOutputDir);

        // Output name comes from Setup.iss OutputBaseFilename (single source of truth).
        var args = $"/O\"{setupOutputDir}\" \"{issPath}\"";
        Console.WriteLine($"ISCC command: {isccPath} {args}");

        var result = await Cli.Wrap(isccPath)
            .WithArguments(args)
            .WithWorkingDirectory(tempDir)
            .ExecuteBufferedAsync();

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"ISCC compilation failed (exit {result.ExitCode}):\n{result.StandardError}");
        }

        Console.WriteLine(result.StandardOutput);
        return setupOutputDir;
    }

    /// <summary>
    /// Move compiled .exe to final destination. Version is embedded via <see cref="ProcessTemplate"/> into Setup.iss (AppVersion, VersionInfo*), not the file name.
    /// </summary>
    private static async Task MoveOutputToDestination(string sourceDir, string outputPath)
    {
        // Matches Setup.iss OutputBaseFilename={#AppName}-Setup → RevitDevTool-Setup.exe (version only in PE / Inno AppVersion via ProcessTemplate)
        var exeName = $"{AppName}-Setup.exe";
        var sourceExe = Path.Combine(sourceDir, exeName);
        var destExe = Path.Combine(outputPath, exeName);

        if (!File.Exists(sourceExe))
        {
            throw new FileNotFoundException($"Expected output not found: {sourceExe}");
        }

        if (File.Exists(destExe))
            File.Delete(destExe);

        await Task.Run(() => File.Move(sourceExe, destExe));

        var sizeMb = new FileInfo(destExe).Length / 1024 / 1024;
        Console.WriteLine($"✓ Installer created: {destExe}");
        Console.WriteLine($"  Size: {sizeMb} MB");
    }

    /// <summary>
    /// Log error details
    /// </summary>
    private static async Task LogErrorAsync(Exception ex)
    {
        await Console.Error.WriteLineAsync($"Error: {ex.Message}");
        if (ex.StackTrace != null)
            await Console.Error.WriteLineAsync(ex.StackTrace);
    }

    /// <summary>
    /// Clean up temporary build directory
    /// </summary>
    private static void CleanupTempDirectory(string tempDir)
    {
        try
        {
            if (!Directory.Exists(tempDir)) return;
            Directory.Delete(tempDir, recursive: true);
            Console.WriteLine($"Cleaned temp: {tempDir}");
        }
        catch
        {
            // Best effort cleanup
        }
    }

    /// <summary>
    /// Gets the path to the Setup.iss template file
    /// </summary>
    private static string GetTemplatePath()
    {
        // Look in multiple locations
        var candidates = new[]
        {
            // Same directory as executable (deployed)
            Path.Combine(AppContext.BaseDirectory, "Setup.iss"),
            // Source directory (development)
            Path.Combine(GetSourceDirectory(), "Setup.iss"),
            // Parent directory
            Path.Combine(Path.GetDirectoryName(typeof(Program).Assembly.Location)!, "..", "Setup.iss"),
        };

        foreach (var candidate in candidates)
        {
            var fullPath = Path.GetFullPath(candidate);
            if (File.Exists(fullPath))
                return fullPath;
        }

        throw new FileNotFoundException("Could not find Setup.iss template. Searched:\n" +
            string.Join("\n", candidates.Select(c => "  - " + Path.GetFullPath(c))));
    }

    /// <summary>
    /// Attempts to find the source directory for development scenarios
    /// </summary>
    private static string GetSourceDirectory()
    {
        var assemblyLocation = typeof(Program).Assembly.Location;
        var dir = Path.GetDirectoryName(assemblyLocation);

        // Walk up looking for .csproj or .git
        while (!string.IsNullOrEmpty(dir))
        {
            if (Directory.GetFiles(dir, "*.csproj").Length != 0 ||
                Directory.Exists(Path.Combine(dir, ".git")))
            {
                return dir;
            }
            dir = Path.GetDirectoryName(dir);
        }

        return assemblyLocation;
    }

    /// <summary>
    /// Process the Setup.iss template and substitute placeholders
    /// </summary>
    private static string ProcessTemplate(string template, string version)
    {
        var sb = new System.Text.StringBuilder(template);

        // Replace version placeholders - quote if contains special chars
        var quotedVersion = version.Contains('-') ? $"\"{version}\"" : version;
        sb.Replace("{#AppVersion}", quotedVersion);

        // If version contains pre-release, format properly
        var versionParts = version.Split('-', 2);
        var baseVersion = versionParts[0];
        var suffix = versionParts.Length > 1 ? versionParts[1] : "";

        sb.Replace("{#AppVersionBase}", baseVersion);
        sb.Replace("{#AppVersionSuffix}", suffix);

        // Ensure AppId is consistent but can be derived from version for uniqueness if needed
        sb.Replace("{#AppId}", AppId);

        // Add a comment header indicating this is auto-generated
        var header = "; ==============================================================================" + Environment.NewLine +
                     "; Auto-generated by InnoSetupBuilder" + Environment.NewLine +
                     $"; Version: {version}" + Environment.NewLine +
                     $"; Generated at: {DateTime.Now:O}" + Environment.NewLine +
                     "; Source template: Setup.iss" + Environment.NewLine +
                     "; ==============================================================================" + Environment.NewLine +
                     Environment.NewLine;
        sb.Insert(0, header);

        return sb.ToString();
    }

    /// <summary>
    /// Recursively copy directory
    /// </summary>
    private static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDir, file);
            var destFile = Path.Combine(destDir, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);
            File.Copy(file, destFile, overwrite: true);
        }
    }

    /// <summary>
    /// Find ISCC compiler using where.exe
    /// </summary>
    private static string? FindIscc()
    {
        try
        {
            var result = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "where.exe",
                Arguments = "iscc",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (result == null) return null;
            result.WaitForExit();

            if (result.ExitCode == 0)
            {
                var output = result.StandardOutput.ReadToEnd();
                var separators = new[] { '\n', '\r' };
                var path = output.Split(separators, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    return path;
            }
        }
        catch
        {
            // Ignore errors
        }

        return null;
    }
}