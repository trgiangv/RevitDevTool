using System.Runtime.CompilerServices;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Runtime;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Requests;
using ReflectionAssembly = System.Reflection.Assembly;
using MtpTestSessionContext = Microsoft.Testing.Platform.TestHost.TestSessionContext;
using SessionUid = Microsoft.Testing.Platform.TestHost.SessionUid;
using BindingFlags = System.Reflection.BindingFlags;

namespace DevTools.TUnit.Runtime;

#pragma warning disable TPEXP

internal static class TUnitEngineHost
{
    private const string EngineAssemblyName = "TUnit.Engine";
    private const string ExtensionTypeName = "TUnit.Engine.Framework.TUnitExtension";
    private const string FrameworkTypeName = "TUnit.Engine.Framework.TUnitTestFramework";
    private const string ServiceProviderTypeName = "Microsoft.Testing.Platform.Services.ServiceProvider";
    private const string ClientInfoTypeName = "Microsoft.Testing.Platform.Services.ClientInfoService";
    private const string OutputDeviceTypeName = "Microsoft.Testing.Platform.OutputDevice.NopPlatformOutputDevice";

    public static IReadOnlyList<TestingCaseResult> Run(
        ReflectionAssembly testAssembly,
        TestingSelection selection,
        CancellationToken cancellationToken)
    {
        if (testAssembly is null)
            throw new ArgumentNullException(nameof(testAssembly));

        SourceRegistrar.IsEnabled = true;
        RuntimeHelpers.RunModuleConstructor(testAssembly.ManifestModule.ModuleHandle);
        TUnitSourceCatalog.Retain(testAssembly);
        var captured = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(null);
        try
        {
            return RunEngine(selection, cancellationToken);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(captured);
        }
    }

    private static IReadOnlyList<TestingCaseResult> RunEngine(
        TestingSelection selection,
        CancellationToken cancellationToken)
    {
        var workingDirectory = Directory.GetCurrentDirectory();
        var resultDirectory = Path.Combine(Path.GetTempPath(), "DevTools", "TUnit", "Results");
        Directory.CreateDirectory(resultDirectory);

        var platform = typeof(ICommandLineOptions).Assembly;
        var services = CreateServiceProvider(platform, workingDirectory, resultDirectory);
        var engine = ReflectionAssembly.Load(EngineAssemblyName);
        var extension = (IExtension)Activator.CreateInstance(RequiredType(engine, ExtensionTypeName))!;
        var framework = (ITestFramework)Activator.CreateInstance(
            RequiredType(engine, FrameworkTypeName),
            extension,
            services,
            new TestFrameworkCapabilities())!;

        var sessionUid = new SessionUid(Guid.NewGuid().ToString("N"));
        var session = (MtpTestSessionContext)Activator.CreateInstance(
            typeof(MtpTestSessionContext),
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            args: [sessionUid],
            culture: null)!;
        var filter = CreateFilter(selection);
        var request = new RunTestExecutionRequest(session, filter);
        using var traceScope = new TestingRunTraceScope();
        var messageBus = new TUnitEngineMessageBus(traceScope);
        var executeContext = new ExecuteRequestContext(
            request,
            messageBus,
            new TUnitEngineCompletionNotifier(),
            cancellationToken);
        var createContext = (CreateTestSessionContext)Activator.CreateInstance(
            typeof(CreateTestSessionContext),
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            args: [sessionUid, cancellationToken],
            culture: null)!;
        var closeContext = (CloseTestSessionContext)Activator.CreateInstance(
            typeof(CloseTestSessionContext),
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            args: [sessionUid, cancellationToken],
            culture: null)!;

        framework.CreateTestSessionAsync(createContext).GetAwaiter().GetResult();
        try
        {
            framework.ExecuteRequestAsync(executeContext).GetAwaiter().GetResult();
        }
        finally
        {
            framework.CloseTestSessionAsync(closeContext).GetAwaiter().GetResult();
        }

        return TUnitEngineResults.Map(messageBus.Nodes.Values, messageBus.CapturedByUid);
    }

    private static ITestExecutionFilter CreateFilter(TestingSelection selection)
    {
        var ids = (selection.TestIds ?? [])
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal)
            .Select(id => new TestNodeUid(id))
            .ToArray();
        return ids.Length == 0
            ? new NopFilter()
            : new TestNodeUidListFilter(ids);
    }

    private static object CreateServiceProvider(ReflectionAssembly platform, string workingDirectory, string resultDirectory)
    {
        var provider = Activator.CreateInstance(RequiredType(platform, ServiceProviderTypeName))!;
        var add = provider.GetType().GetMethod("AddService", [typeof(object), typeof(bool)])
            ?? throw new InvalidOperationException("MTP ServiceProvider.AddService was not found.");
        add.Invoke(provider, [new TUnitEngineLoggerFactory(), false]);
        add.Invoke(provider, [new TUnitEngineCommandLine(), false]);
        add.Invoke(provider, [new TUnitEngineConfiguration(workingDirectory, resultDirectory), false]);
        add.Invoke(provider, [new TUnitEngineOutputDevice(), false]);
        add.Invoke(provider, [Activator.CreateInstance(RequiredType(platform, OutputDeviceTypeName))!, false]);
        add.Invoke(provider, [Activator.CreateInstance(RequiredType(platform, ClientInfoTypeName), "devtools-revit-host", "1.0")!, false]);
        return provider;
    }

    private static Type RequiredType(ReflectionAssembly assembly, string typeName) =>
        assembly.GetType(typeName, throwOnError: true)
        ?? throw new InvalidOperationException($"Type '{typeName}' was not found in '{assembly.GetName().Name}'.");
}
