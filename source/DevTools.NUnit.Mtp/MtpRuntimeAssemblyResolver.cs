using System.Reflection;

namespace DevTools.NUnit.Mtp;

internal static class MtpRuntimeAssemblyResolver
{
    private static int _registered;

    internal static void EnsureRegistered()
    {
        if (Interlocked.Exchange(ref _registered, 1) != 0)
            return;

        AppDomain.CurrentDomain.AssemblyResolve += ResolvePrivateRuntimeAssembly;
    }

    private static Assembly? ResolvePrivateRuntimeAssembly(object? sender, ResolveEventArgs args)
    {
        var name = new AssemblyName(args.Name).Name;
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var path = Path.Combine(AppContext.BaseDirectory, name + ".dll");
        return File.Exists(path) ? Assembly.LoadFrom(path) : null;
    }
}
