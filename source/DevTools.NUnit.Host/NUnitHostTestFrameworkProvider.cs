using DevTools.NUnit.Transport.Contracts;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Providers;

namespace DevTools.NUnit.Host;

public static class NUnitFramework
{
    public const string Id = "nunit";
}

/// <summary>
/// Thin <see cref="IHostTestFrameworkProvider"/> over <see cref="INUnitHost"/>.
/// Live <c>nunit/*</c> JSON stays on <see cref="NUnitRequestHandler"/> so NUnit
/// fields that the generic DTO cannot represent are not round-tripped.
/// </summary>
public sealed class NUnitHostTestFrameworkProvider : IHostTestFrameworkProvider
{
    private readonly INUnitHost _host;

    public NUnitHostTestFrameworkProvider(INUnitHost host)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
    }

    public string FrameworkId => NUnitFramework.Id;

    public TestingRunResponse Run(
        TestingRunRequest request,
        ITestingEventSink eventSink,
        CancellationToken cancellationToken)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));
        if (eventSink is null)
            throw new ArgumentNullException(nameof(eventSink));
        if (request.Assembly is null)
            throw new ArgumentException("Assembly is required.", nameof(request));

        if (!string.Equals(request.FrameworkId, NUnitFramework.Id, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"NUnit provider cannot execute framework '{request.FrameworkId}'.",
                nameof(request));
        }

        var filter = NUnitSelectionFilter.ToNUnitFilter(request.Selection);
        var nunitRequest = new NUnitRunRequest(request.RunId, request.Assembly.Path, filter);
        var response = _host.Run(
            nunitRequest,
            progress => NUnitTestingMapper.Publish(progress, eventSink),
            cancellationToken);

        return NUnitTestingMapper.ToTesting(response, NUnitFramework.Id);
    }

    public bool Cancel(Guid runId)
    {
        _host.Cancel(runId);
        return true;
    }
}
