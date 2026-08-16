using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DevTools.Hosting;

public sealed class HostLaunchService : IHostLaunchService
{
    private readonly IReadOnlyList<IHostPathResolver> _pathResolvers;
    private readonly IReadOnlyList<IHostArgumentBuilder> _argumentBuilders;
    private readonly IReadOnlyList<IHostStartupDialogStrategy> _dialogStrategies;

    public HostLaunchService(
        IEnumerable<IHostPathResolver> pathResolvers,
        IEnumerable<IHostArgumentBuilder> argumentBuilders,
        IEnumerable<IHostStartupDialogStrategy> dialogStrategies)
    {
        _pathResolvers = pathResolvers as IReadOnlyList<IHostPathResolver> ?? pathResolvers.ToArray();
        _argumentBuilders = argumentBuilders as IReadOnlyList<IHostArgumentBuilder> ?? argumentBuilders.ToArray();
        _dialogStrategies = dialogStrategies as IReadOnlyList<IHostStartupDialogStrategy> ?? dialogStrategies.ToArray();
    }

    public HostProcessStart Start(HostLaunchRequest request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.FilePath) && !File.Exists(request.FilePath))
            throw new InvalidOperationException($"File not found: {request.FilePath}");

        var pathResolver = HostLaunchSupport.FindSingle(_pathResolvers, request.HostApp, r => r.Supports(request.HostApp));
        var argumentBuilder = HostLaunchSupport.FindSingle(_argumentBuilders, request.HostApp, b => b.Supports(request.HostApp));
        if (pathResolver is null || argumentBuilder is null)
            throw new InvalidOperationException($"Launch not yet supported for {request.HostApp}.");

        var version = request.Version;
        if (string.IsNullOrWhiteSpace(version))
        {
            var installed = pathResolver.GetInstalledVersions(request.HostApp);
            version = installed.Count > 0 ? installed[0] : null;
        }

        if (string.IsNullOrWhiteSpace(version))
            throw new InvalidOperationException($"No compatible {request.HostApp} version found.");

        var exePath = pathResolver.FindExecutable(request.HostApp, version!);
        if (string.IsNullOrWhiteSpace(exePath))
            throw new InvalidOperationException($"{request.HostApp} {version} installation not found.");

        var resolved = request with { Version = version! };
        var arguments = argumentBuilder.Build(resolved, exePath!);
        if (arguments.Count == 0)
            throw new InvalidOperationException($"Launch not yet supported for {request.HostApp}.");

        var process = StartProcess(request.HostApp, exePath!, arguments);
        var dialogStrategy = HostLaunchSupport.FindSingle(
            _dialogStrategies, request.HostApp, s => s.Supports(request.HostApp));
        var dialogHandle = HostLaunchCoordinator.StartDialogResolver(
            dialogStrategy, process.Id, cancellationToken);

        return new HostProcessStart(
            process,
            version!,
            exePath!,
            resolved.LanguageCulture,
            arguments,
            dialogHandle);
    }

    private static Process StartProcess(HostApp hostApp, string exePath, IReadOnlyList<string> arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = false,
                CreateNoWindow = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = Path.GetDirectoryName(exePath) ?? Environment.CurrentDirectory,
            };
            foreach (var arg in arguments)
                startInfo.ArgumentList.Add(arg);

            Process process;
            using (StdioInheritance.Suppress())
            {
                process = Process.Start(startInfo)
                    ?? throw new InvalidOperationException($"Failed to start {hostApp} process.");
            }

            process.StandardInput.Close();
            _ = process.StandardOutput.ReadToEndAsync();
            _ = process.StandardError.ReadToEndAsync();
            return process;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to launch {hostApp}: {ex.Message}", ex);
        }
    }

    private static class StdioInheritance
    {
        private const uint HandleFlagInherit = 1;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetHandleInformation(IntPtr hObject, out uint lpdwFlags);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetHandleInformation(IntPtr hObject, uint dwMask, uint dwFlags);

        public static IDisposable Suppress()
        {
            var previous = new List<(IntPtr Handle, uint Flags)>();
            foreach (var std in new[] { -10, -11, -12 })
            {
                var handle = GetStdHandle(std);
                if (handle == IntPtr.Zero || handle == new IntPtr(-1))
                    continue;
                if (!GetHandleInformation(handle, out var flags))
                    continue;
                previous.Add((handle, flags));
                SetHandleInformation(handle, HandleFlagInherit, 0);
            }

            return new Restore(previous);
        }

        private sealed class Restore(List<(IntPtr Handle, uint Flags)> previous) : IDisposable
        {
            public void Dispose()
            {
                foreach (var entry in previous)
                    SetHandleInformation(entry.Handle, HandleFlagInherit, entry.Flags & HandleFlagInherit);
            }
        }
    }
}
