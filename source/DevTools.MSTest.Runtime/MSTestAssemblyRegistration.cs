using System.Reflection;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration;

namespace DevTools.MSTest.Runtime;

internal static class MSTestAssemblyRegistration
{
    public static void Register(Assembly testAssembly)
    {
        ArgumentNullException.ThrowIfNull(testAssembly);
        ReflectionMetadataHook.Register(
            testAssembly,
            Type.EmptyTypes,
            new Dictionary<Type, MethodInfo[]>());
    }
}
