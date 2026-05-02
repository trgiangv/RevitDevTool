using Build.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.Git.Extensions;
using ModularPipelines.Modules;

namespace Build.Modules;

/// <summary>
///     Sign assemblies using self-signed certificate (batch mode with input file list).
///     Only runs in CI when SIGN_CERT_BASE64 is set.
/// </summary>
[DependsOn<CreateInstallerModule>]
[UsedImplicitly]
public sealed class SignModule(IOptions<BuildOptions> buildOptions) : Module
{
    // Only sign files modified within this window (prevents re-signing old files)
    private static readonly TimeSpan SigningTimeWindow = TimeSpan.FromHours(1);

    protected override async Task ExecuteModuleAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var certBase64 = Environment.GetEnvironmentVariable("SIGN_CERT_BASE64");
        var certPassword = Environment.GetEnvironmentVariable("SIGN_CERT_PASSWORD");

        if (string.IsNullOrWhiteSpace(certBase64))
        {
            context.Logger.LogInformation("No signing certificate → skip signing");
            return;
        }

        // Restore cert.pfx to temp location
        var certPath = Path.Combine(Path.GetTempPath(), $"RevitDevTool-{Guid.NewGuid():N}.pfx");
        await File.WriteAllBytesAsync(certPath, Convert.FromBase64String(certBase64), cancellationToken);

        try
        {
            var targetFiles = GetFilesToSign(context);

            if (targetFiles.Count == 0)
            {
                context.Logger.LogWarning("No files found to sign");
                return;
            }

            context.Logger.LogInformation("Signing {Count} files (modified within {Window})",
                targetFiles.Count, SigningTimeWindow);

            // Write input file list for batch signing
            var inputFile = ModularPipelines.FileSystem.File.GetNewTemporaryFilePath();
            await inputFile.WriteAsync(targetFiles, cancellationToken);

            // Batch sign using input file list
            var result = await context.Shell.Command.ExecuteCommandLineTool(
                new ModularPipelines.Options.GenericCommandLineToolOptions("signtool")
                {
                    Arguments = [
                        "sign",
                        "/f", certPath,
                        "/p", certPassword!,
                        "/fd", "sha256",
                        "/tr", "http://timestamp.digicert.com",
                        "/td", "sha256",
                        "/ifl", inputFile.Path
                    ]
                },
                cancellationToken: cancellationToken);

            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException($"Signing failed: {result.StandardError}");
            }

            context.Summary.KeyValue("Artifacts", "Signed Files", targetFiles.Count.ToString());
        }
        finally
        {
            try { File.Delete(certPath); } catch { /* best effort */ }
        }
    }

    private List<string> GetFilesToSign(IModuleContext context)
    {
        var outputDir = context.Git().RootDirectory.GetFolder(buildOptions.Value.OutputDirectory);
        var files = new List<string>();

        if (!outputDir.Exists) return files;

        // Find all signable files (DLLs and the installer EXE)
        var patterns = new[] { "*.dll", "*.exe" };
        var now = DateTime.UtcNow;

        foreach (var pattern in patterns)
        {
            var matches = Directory.GetFiles(outputDir.Path, pattern, SearchOption.AllDirectories)
                .Where(file =>
                {
                    // Time filter: only sign recently modified files
                    var lastWrite = File.GetLastWriteTimeUtc(file);
                    if (now - lastWrite > SigningTimeWindow) return false;

                    // Name filter: only our assemblies
                    var fileName = Path.GetFileName(file);
                    return fileName.StartsWith("DevTools", StringComparison.OrdinalIgnoreCase)
                           || fileName.Equals("RevitDevTool.dll", StringComparison.OrdinalIgnoreCase)
                           || fileName.Equals("AcadDevTool.dll", StringComparison.OrdinalIgnoreCase)
                           || fileName.Equals("RevitDevTool-Setup.exe", StringComparison.OrdinalIgnoreCase);
                });

            files.AddRange(matches);
        }

        return files.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
}