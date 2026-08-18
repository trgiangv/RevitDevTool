using System.Reflection;

namespace IsolationEntry;

public static class Entry
{
    public static string GetPrivateDependencyName() => Load("System.Private.IsolationFixture");

    public static string GetAfterDisposeDependencyName() => Load("System.Private.AfterDisposeFixture");

    static string Load(string assemblyName) => Assembly.Load(new AssemblyName(assemblyName)).FullName!;
}
