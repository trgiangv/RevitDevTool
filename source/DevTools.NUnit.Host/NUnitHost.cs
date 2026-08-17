using DevTools.NUnit.Transport.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevTools.NUnit.Host;

public interface INUnitHost
{
    NUnitDiscoverResponse Discover(NUnitDiscoverRequest request);

    NUnitRunResponse Run(
        NUnitRunRequest request,
        Action<NUnitProgressEvent> publish,
        CancellationToken cancellationToken = default);

    void Cancel(Guid runId);
}

public sealed class NUnitHost(
    NUnitRuntimeManager runtimeManager,
    ILogger<NUnitHost>? logger = null) : INUnitHost
{
    private readonly ILogger<NUnitHost> _logger = logger ?? NullLogger<NUnitHost>.Instance;

    public NUnitDiscoverResponse Discover(NUnitDiscoverRequest request)
    {
        try
        {
            return runtimeManager.Discover(request);
        }
        catch (NUnitAssemblyLoadException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw CreateLoadException(request.AssemblyPath, ex);
        }
    }

    public NUnitRunResponse Run(
        NUnitRunRequest request,
        Action<NUnitProgressEvent> publish,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return runtimeManager.Run(request, publish, cancellationToken);
        }
        catch (NUnitAssemblyLoadException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw CreateLoadException(request.AssemblyPath, ex);
        }
    }

    public void Cancel(Guid runId) => runtimeManager.Cancel(runId);

    private static NUnitAssemblyLoadException CreateLoadException(string assemblyPath, Exception ex) =>
        new(NUnitAssemblyPreflightResult.Failed(
            assemblyPath,
            FormatLoadErrorMessage(ex),
            ex.ToString()));

    private static string FormatLoadErrorMessage(Exception ex)
    {
        if (ex.InnerException is { } inner
            && !string.IsNullOrWhiteSpace(inner.Message)
            && !string.Equals(inner.Message, ex.Message, StringComparison.Ordinal))
        {
            return $"{ex.Message} {inner.Message}";
        }

        return ex.Message;
    }
}
