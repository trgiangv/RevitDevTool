namespace DevTools.Hosting;

public interface IHostPathResolver
{
    bool Supports(HostApp hostApp);

    string? FindExecutable(HostApp hostApp, string version);

    IReadOnlyList<string> GetInstalledVersions(HostApp hostApp);
}
