namespace DevTools.AssemblyIsolation.Sources;

public interface INativeAssemblySource
{
    AssemblyCandidate? Resolve(string name);
}
