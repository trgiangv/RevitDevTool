using System.IO;
using System.Reflection;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;
using DevTools.Execution.Providers.Dotnet;

namespace AcadDevTool.HostAdapters;

/// <summary>
/// Runs [CommandMethod] commands by loading the assembly, resolving the type and method from
/// <see cref="CommandItem.FullClassName"/> (<c>TypeFullName.MethodName</c>), and invoking the
/// method (instance or static).
/// </summary>
public sealed class AcadCommandRunner : ICommandRunner
{
    public ExecutionResult RunCommand(CommandItem commandItem)
    {
#if NET
        var alc = new CommandLoadContext(commandItem.AssemblyPath);
        try
        {
            return ExecuteInContext(alc, commandItem);
        }
        finally
        {
            alc.Unload();
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
#else
        return ExecuteInAppDomain(commandItem);
#endif
    }

    public ExecutionResult RunCompiledCommand(object compiledCommand)
    {
        var method = compiledCommand.GetType().GetMethod("Execute", BindingFlags.Public | BindingFlags.Instance);
        if (method == null)
            return ExecutionResult.Failed("Compiled command does not have an Execute method.");

        method.Invoke(compiledCommand, null);
        return ExecutionResult.Succeeded();
    }

#if NET
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static ExecutionResult ExecuteInContext(CommandLoadContext alc, CommandItem commandItem)
    {
        var (typeName, methodName) = SplitFullClassName(commandItem.FullClassName);
        using var stream = new FileStream(commandItem.AssemblyPath, FileMode.Open, FileAccess.Read);
        var assembly = alc.LoadFromStream(stream);
        var type = assembly.GetType(typeName);
        if (type == null)
            return ExecutionResult.Failed($"Type '{typeName}' not found in assembly.");

        var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
        if (method == null)
            return ExecutionResult.Failed($"Method '{methodName}' not found on type '{typeName}'.");

        var instance = method.IsStatic ? null : Activator.CreateInstance(type);
        method.Invoke(instance, null);
        return ExecutionResult.Succeeded();
    }
#else
    private static ExecutionResult ExecuteInAppDomain(CommandItem commandItem)
    {
        var (typeName, methodName) = SplitFullClassName(commandItem.FullClassName);
        var assemblyBytes = File.ReadAllBytes(commandItem.AssemblyPath);
        var assembly = Assembly.Load(assemblyBytes);
        var type = assembly.GetType(typeName);
        if (type == null)
            return ExecutionResult.Failed($"Type '{typeName}' not found in assembly.");

        var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
        if (method == null)
            return ExecutionResult.Failed($"Method '{methodName}' not found on type '{typeName}'.");

        var instance = method.IsStatic ? null : Activator.CreateInstance(type);
        method.Invoke(instance, null);
        return ExecutionResult.Succeeded();
    }
#endif

    private static (string TypeName, string MethodName) SplitFullClassName(string fullClassName)
    {
        var lastDot = fullClassName.LastIndexOf('.');
        return lastDot < 0
            ? (fullClassName, "Execute")
            : (fullClassName[..lastDot], fullClassName[(lastDot + 1)..]);
    }
}
