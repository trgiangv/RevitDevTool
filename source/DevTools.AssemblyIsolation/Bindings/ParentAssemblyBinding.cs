using System.Reflection;

namespace DevTools.AssemblyIsolation.Bindings;

public sealed class ParentAssemblyBinding
{
    public ParentAssemblyBinding(Assembly assembly)
    {
        Assembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
        Identity = assembly.GetName();
        SimpleName = Identity.Name
            ?? throw new ArgumentException("The parent assembly must have a simple name.", nameof(assembly));
    }

    public Assembly Assembly { get; }

    public AssemblyName Identity { get; }

    public string SimpleName { get; }
}
