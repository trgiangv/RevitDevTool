using DevTools.Testing.Abstractions.Providers;

namespace DevTools.Testing.Host;

public sealed class TestingProviderRegistry
{
    private readonly Dictionary<string, IHostTestFrameworkProvider> _providers;

    public TestingProviderRegistry(IEnumerable<IHostTestFrameworkProvider> providers)
    {
        if (providers is null)
            throw new ArgumentNullException(nameof(providers));

        _providers = new Dictionary<string, IHostTestFrameworkProvider>(StringComparer.OrdinalIgnoreCase);
        foreach (var provider in providers)
        {
            if (provider is null)
                throw new ArgumentException("Provider list cannot contain null entries.", nameof(providers));

            if (string.IsNullOrWhiteSpace(provider.FrameworkId))
                throw new ArgumentException("Provider framework id is required.", nameof(providers));

            var id = Normalize(provider.FrameworkId);
            if (_providers.ContainsKey(id))
            {
                throw new ArgumentException(
                    $"Duplicate host-test framework id '{id}'.",
                    nameof(providers));
            }

            _providers[id] = provider;
        }
    }

    public IHostTestFrameworkProvider GetRequired(string frameworkId)
    {
        var id = Normalize(frameworkId);
        if (_providers.TryGetValue(id, out var provider))
            return provider;

        throw new KeyNotFoundException($"No host-test provider is registered for '{id}'.");
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

    private static string Normalize(string frameworkId)
    {
        var trimmed = frameworkId?.Trim() ?? string.Empty;
        return trimmed.ToLowerInvariant();
    }
}
