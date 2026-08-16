namespace DevTools.Hosting;

public interface IHostArgumentBuilder
{
    bool Supports(HostApp hostApp);

    IReadOnlyList<string> Build(HostLaunchRequest request, string executablePath);
}
