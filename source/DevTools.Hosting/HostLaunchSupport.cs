namespace DevTools.Hosting;

/// <summary>
/// Selects the single matching host contract from a multi-host container.
/// Both composition roots register contracts via Add* and resolve through this helper.
/// </summary>
public static class HostLaunchSupport
{
    public static T? FindSingle<T>(IEnumerable<T> items, HostApp hostApp, Func<T, bool> supports)
    {
        T? found = default;
        var count = 0;
        foreach (var item in items)
        {
            if (!supports(item))
                continue;

            count++;
            if (count > 1)
            {
                throw new InvalidOperationException(
                    $"Multiple {typeof(T).Name} registrations support {hostApp}.");
            }

            found = item;
        }

        return found;
    }
}
