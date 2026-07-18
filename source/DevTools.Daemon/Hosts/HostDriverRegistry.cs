using DevTools.Logging;

namespace DevTools.Daemon.Hosts;

/// <summary>Deterministically resolves daemon host drivers by product or file extension.</summary>
internal sealed class HostDriverRegistry
{
    private readonly Dictionary<HostApp, IHostDriver> _driversByHostApp = [];
    private readonly Dictionary<string, IHostDriver> _driversByExtension =
        new(StringComparer.OrdinalIgnoreCase);

    public HostDriverRegistry(IEnumerable<IHostDriver> drivers)
    {
        foreach (var driver in drivers)
        {
            foreach (var hostApp in driver.SupportedHostApps)
            {
                if (_driversByHostApp.TryGetValue(hostApp, out var existing))
                    throw new InvalidOperationException(
                        $"Host product '{hostApp}' is registered by both '{existing.HostId}' and '{driver.HostId}'.");

                _driversByHostApp.Add(hostApp, driver);
            }

            foreach (var extension in driver.FileExtensions)
            {
                var normalized = NormalizeExtension(extension);
                if (_driversByExtension.TryGetValue(normalized, out var existing))
                    throw new InvalidOperationException(
                        $"File extension '{normalized}' is registered by both '{existing.HostId}' and '{driver.HostId}'.");

                _driversByExtension.Add(normalized, driver);
            }
        }
    }

    public IHostDriver ForHost(HostApp hostApp) =>
        TryForHost(hostApp) ?? throw new InvalidOperationException($"No host driver is registered for '{hostApp}'.");

    public IHostDriver? TryForHost(HostApp hostApp) =>
        _driversByHostApp.GetValueOrDefault(hostApp);

    public IHostDriver ForFile(string filePath) =>
        TryForFile(filePath) ?? throw new InvalidOperationException(
            $"No host driver is registered for file extension '{Path.GetExtension(filePath)}'.");

    public IHostDriver? TryForFile(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        return string.IsNullOrEmpty(extension)
            ? null
            : _driversByExtension.GetValueOrDefault(NormalizeExtension(extension));
    }

    private static string NormalizeExtension(string extension) =>
        extension.StartsWith('.', StringComparison.Ordinal) ? extension : $".{extension}";
}
