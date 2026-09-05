#if NETFRAMEWORK
using System.Reflection;

namespace DevTools.AssemblyIsolation.Runtime;

/// <summary>
/// net48 <see cref="AppDomain.AssemblyResolve"/> returns the first non-null
/// handler. <c>+=</c> appends, so a later Isolated session loses to an earlier
/// simple-name resolver (Costura). Insert first so <c>Pin</c>/<c>Share</c>
/// still run.
/// </summary>
internal static class AppDomainResolver
{
    private static readonly object Gate = new();
    private const BindingFlags FieldFlags = BindingFlags.Instance | BindingFlags.NonPublic;

    public static void InsertFirst(AppDomain domain, ResolveEventHandler handler)
    {
        if (domain is null) throw new ArgumentNullException(nameof(domain));
        if (handler is null) throw new ArgumentNullException(nameof(handler));

        lock (Gate)
        {
            var field = GetField(domain);
            if (field is null)
            {
                domain.AssemblyResolve += handler;
                return;
            }

            var existing = (ResolveEventHandler?)field.GetValue(domain);
            field.SetValue(
                domain,
                existing is null ? handler : (ResolveEventHandler)Delegate.Combine(handler, existing));
        }
    }

    public static void Remove(AppDomain domain, ResolveEventHandler handler)
    {
        if (domain is null) throw new ArgumentNullException(nameof(domain));
        if (handler is null) throw new ArgumentNullException(nameof(handler));

        lock (Gate)
            domain.AssemblyResolve -= handler;
    }

    private static FieldInfo? GetField(AppDomain domain) =>
        domain.GetType().GetField("_AssemblyResolve", FieldFlags);
}
#endif
