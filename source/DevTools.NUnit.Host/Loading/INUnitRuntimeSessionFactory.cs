using DevTools.NUnit.Core.Runtime;

namespace DevTools.NUnit.Host.Loading;

public interface INUnitRuntimeSessionFactory
{
    INUnitRuntimeSession Create(NUnitGenerationManifest generation);
}
