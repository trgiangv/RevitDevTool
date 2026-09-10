using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Providers;

namespace DevTools.Testing.Host;

public sealed class TestingProviderRegistry
{
    private readonly Dictionary<TestFrameworkId, ITestFrameworkProvider> _providers;

    public TestingProviderRegistry(IEnumerable<ITestFrameworkProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        _providers = [];
        foreach (var provider in providers)
        {
            if (provider is null)
                throw new ArgumentException("Provider list cannot contain null entries.", nameof(providers));

            if (!Enum.IsDefined(provider.FrameworkId))
                throw new ArgumentException("Provider framework id is required.", nameof(providers));

            if (_providers.ContainsKey(provider.FrameworkId))
            {
                throw new ArgumentException(
                    $"Duplicate host-test framework id '{provider.FrameworkId}'.",
                    nameof(providers));
            }

            _providers[provider.FrameworkId] = provider;
        }
    }

    public ITestFrameworkProvider GetRequired(TestFrameworkId frameworkId)
    {
        return _providers.TryGetValue(frameworkId, out var provider)
            ? provider
            : throw new KeyNotFoundException($"No host-test provider is registered for '{frameworkId}'.");
    }

    public bool Cancel(Guid runId)
    {
        var acknowledged = false;
        foreach (var provider in _providers.Values)
        {
            if (provider.Cancel(runId))
                acknowledged = true;
        }

        return acknowledged;
    }
}
