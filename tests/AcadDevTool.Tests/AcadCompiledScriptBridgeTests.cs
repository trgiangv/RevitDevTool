using System.Reflection;
using AcadDevTool.Adapters;
using Autodesk.AutoCAD.Runtime;
using Microsoft.Extensions.Logging.Abstractions;

namespace AcadDevTool.Tests;

[TestClass]
public sealed class AcadCompiledScriptBridgeTests
{
    [TestMethod]
    public void Parent_bindings_include_the_core_api_used_by_compiled_scripts_without_duplicate_identities()
    {
        try
        {
            var bridge = CreateBridge();
            var parentBindings = bridge.GetParentBindings().ToArray();
            var coreApiAssembly = typeof(Autodesk.AutoCAD.ApplicationServices.Core.Application).Assembly;

            Assert.IsTrue(parentBindings.Any(assembly => assembly.FullName == coreApiAssembly.FullName),
                $"Expected Core API '{coreApiAssembly.FullName}'.");
            Assert.AreEqual(
                parentBindings.Length,
                parentBindings.Select(assembly => assembly.FullName).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }
        catch (BadImageFormatException)
        {
            Assert.Inconclusive("AutoCAD reference assemblies are not loadable outside the host process.");
        }
    }

    [TestMethod]
    public void Compiled_command_runner_returns_success_after_invoking_a_static_command()
    {
        SuccessfulCommand.Calls = 0;
        var result = InvokeRunCompiledCommand(CreateCommandRunner(), new SuccessfulCommand());

        Assert.IsTrue(GetExecutionSuccess(result));
        Assert.AreEqual(1, SuccessfulCommand.Calls);
    }

    [TestMethod]
    public void Compiled_command_runner_preserves_the_command_failure()
    {
        var error = Assert.ThrowsExactly<TargetInvocationException>(() =>
            InvokeRunCompiledCommand(CreateCommandRunner(), new FailingCommand()));

        Assert.IsInstanceOfType(error.InnerException, typeof(InvalidOperationException));
        Assert.AreEqual("command failure", error.InnerException!.Message);
    }

    private static AcadCompiledScriptBridge CreateBridge()
    {
        var ctor = typeof(AcadCompiledScriptBridge).GetConstructors(BindingFlags.Instance | BindingFlags.Public).Single();
        var args = CreateConstructorArgs(ctor);
        return (AcadCompiledScriptBridge)ctor.Invoke(args)!;
    }

    private static AcadCommandRunner CreateCommandRunner()
    {
        var ctor = typeof(AcadCommandRunner).GetConstructors(BindingFlags.Instance | BindingFlags.Public).Single();
        var args = CreateConstructorArgs(ctor);
        return (AcadCommandRunner)ctor.Invoke(args)!;
    }

    private static object[] CreateConstructorArgs(ConstructorInfo ctor)
    {
        var args = new object?[ctor.GetParameters().Length];
        for (var i = 0; i < args.Length; i++)
        {
            var parameterType = ctor.GetParameters()[i].ParameterType;
            args[i] = parameterType.FullName switch
            {
                "DevTools.Hosting.IHostAppInfo" => CreateHostAppInfo(),
                "DevTools.AssemblyIsolation.HostAssemblies" => CreateHostAssemblies(),
                _ when parameterType.IsGenericType
                    && parameterType.GetGenericTypeDefinition().FullName == "Microsoft.Extensions.Logging.ILogger`1"
                    => CreateNullLogger(parameterType),
                _ => throw new InvalidOperationException($"Unsupported constructor parameter type '{parameterType}'.")
            };
        }

        return args!;
    }

    private static object CreateHostAssemblies()
    {
        var type = typeof(AcadCommandRunner).Assembly.GetType(
            "AcadDevTool.Adapters.AcadHostAssemblies",
            throwOnError: true)!;
        return Activator.CreateInstance(type)!;
    }

    private static object CreateHostAppInfo()
    {
        var type = typeof(AcadCommandRunner).Assembly.GetType(
            "AcadDevTool.Adapters.AcadHostAppInfo",
            throwOnError: true)!;
        return Activator.CreateInstance(type)!;
    }

    private static object CreateNullLogger(Type expectedLoggerType)
    {
        var runnerType = expectedLoggerType.GetGenericArguments()[0];
        var nullLoggerOpen = expectedLoggerType.Assembly.GetType("Microsoft.Extensions.Logging.Abstractions.NullLogger`1")
            ?? typeof(NullLogger<>).Assembly.GetType("Microsoft.Extensions.Logging.Abstractions.NullLogger`1")
            ?? throw new InvalidOperationException("NullLogger<> was not found.");
        var nullLoggerClosed = nullLoggerOpen.MakeGenericType(runnerType);
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Static;
        return nullLoggerClosed.GetProperty("Instance", flags)?.GetValue(null)
            ?? nullLoggerClosed.GetField("Instance", flags)?.GetValue(null)
            ?? Activator.CreateInstance(nullLoggerClosed)!;
    }

    private static object InvokeRunCompiledCommand(AcadCommandRunner runner, object compiledCommand)
    {
        var method = typeof(AcadCommandRunner).GetMethod(
            nameof(AcadCommandRunner.RunCompiledCommand),
            [typeof(object)])!;
        return method.Invoke(runner, [compiledCommand])!;
    }

    private static bool GetExecutionSuccess(object executionResult) =>
        (bool)executionResult.GetType().GetProperty("Success")!.GetValue(executionResult)!;

    public sealed class SuccessfulCommand
    {
        public static int Calls;

        [CommandMethod("Success")]
        public static void Execute() => Calls++;
    }

    public sealed class FailingCommand
    {
        [CommandMethod("Failure")]
        public static void Execute() => throw new InvalidOperationException("command failure");
    }
}
