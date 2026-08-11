using DevTools.NUnit.Core.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevTools.NUnit.Host;

public interface INUnitHost
{
    NUnitDiscoverResponse Discover(NUnitDiscoverRequest request);

    NUnitRunResponse Run(
        NUnitRunRequest request,
        Action<NUnitProgressEvent> publish);

    void Cancel(Guid runId);
}

public sealed class NUnitHost(
    NUnitReflectionRunner runner,
    ILogger<NUnitHost>? logger = null) : INUnitHost
{
    private readonly ILogger<NUnitHost> _logger = logger ?? NullLogger<NUnitHost>.Instance;

    public NUnitDiscoverResponse Discover(NUnitDiscoverRequest request)
    {
        try
        {
            return runner.Discover(request.AssemblyPath, request.Filter);
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
        Action<NUnitProgressEvent> publish)
    {
        try
        {
            if (request.WaitForDebugger)
            {
                _logger.LogWarning(
                    "NUnit run {RunId}: wait_for_debugger is ignored (host-process debugging deferred).",
                    request.RunId);
            }

            return runner.Run(request.RunId, request.AssemblyPath, request.Filter, publish);
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

    public void Cancel(Guid runId) => runner.Cancel(runId);

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
