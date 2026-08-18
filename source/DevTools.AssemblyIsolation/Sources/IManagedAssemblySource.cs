using System.Reflection;

namespace DevTools.AssemblyIsolation.Sources;

public interface IManagedAssemblySource
{
    AssemblyCandidate? Resolve(AssemblyName requested);
}
