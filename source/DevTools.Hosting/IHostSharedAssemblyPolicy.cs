namespace DevTools.Hosting;

public interface IHostSharedAssemblyPolicy
{
    IReadOnlyCollection<string> HostApiSimpleNames { get; }

    IReadOnlyCollection<string> HostApiPrefixes { get; }
}
