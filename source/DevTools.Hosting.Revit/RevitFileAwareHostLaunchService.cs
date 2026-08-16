namespace DevTools.Hosting.Revit;

public sealed class RevitFileAwareHostLaunchService : IHostLaunchService
{
    private readonly IHostLaunchService _inner;
    private readonly IHostPathResolver _revitPathResolver;
    private readonly Func<string, string?>? _readDocumentYear;

    public RevitFileAwareHostLaunchService(
        IHostLaunchService inner,
        IHostPathResolver revitPathResolver,
        Func<string, string?>? readDocumentYear)
    {
        _inner = inner;
        _revitPathResolver = revitPathResolver;
        _readDocumentYear = readDocumentYear;
    }

    public HostProcessStart Start(HostLaunchRequest request, CancellationToken cancellationToken)
    {
        if (request.HostApp != HostApp.Revit || !string.IsNullOrWhiteSpace(request.Version))
            return _inner.Start(request, cancellationToken);

        var installed = _revitPathResolver.GetInstalledVersions(HostApp.Revit);
        string? documentYear = null;
        if (_readDocumentYear is not null && !string.IsNullOrWhiteSpace(request.FilePath) && File.Exists(request.FilePath))
            documentYear = _readDocumentYear(request.FilePath!);

        var version = RevitVersionSelector.FindCompatibleVersion(documentYear, installed)
            ?? throw new InvalidOperationException("No compatible Revit version found.");

        return _inner.Start(request with { Version = version }, cancellationToken);
    }
}
