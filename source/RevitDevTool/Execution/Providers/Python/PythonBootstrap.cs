using System.IO;

namespace RevitDevTool.Execution.Providers.Python;

public static class PythonBootstrap
{
    public static async Task EnsureEnvironmentReadyAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!PixiEnvironment.IsEnvironmentReady() ||
            !File.Exists(Path.Combine(PixiEnvironment.PixiProjectDir, "pixi.toml")))
        {
            await PixiEnvironment.SetupEnvironmentAsync().ConfigureAwait(false);
        }

        PixiEnvironment.EnsureMcpServerFiles();
    }

    public static async Task EnsureExecutorReadyAsync(CancellationToken cancellationToken = default)
    {
        await EnsureEnvironmentReadyAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        await PythonInitializer.InitializeAsync().ConfigureAwait(false);
    }
}
