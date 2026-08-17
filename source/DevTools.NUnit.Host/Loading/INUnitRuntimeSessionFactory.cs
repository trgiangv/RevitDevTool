using DevTools.NUnit.Transport.Runtime;

namespace DevTools.NUnit.Host.Loading;

public interface INUnitRuntimeSessionFactory
{
    INUnitRuntimeSession Create(NUnitGenerationManifest generation);
}
