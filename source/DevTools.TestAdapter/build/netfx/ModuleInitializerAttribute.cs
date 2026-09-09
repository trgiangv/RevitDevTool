// Compiled into net4x TUnit test projects by RevitDevTool.TestAdapter.targets.
// TUnit's generated infrastructure applies [ModuleInitializer], which .NET Framework
// does not declare. The adapter skips this file when the project already has Polyfill
// or its own declaration; set NetFxModuleInitializer=false to opt out.

#if NETFRAMEWORK
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    internal sealed class ModuleInitializerAttribute : Attribute
    {
    }
}
#endif
