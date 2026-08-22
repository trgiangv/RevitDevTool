using DevTools.Testing.Abstractions.Contracts;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Requests;
using TUnit.Core;
using ReflectionAssembly = System.Reflection.Assembly;
using MtpTestSessionContext = Microsoft.Testing.Platform.TestHost.TestSessionContext;
using SessionUid = Microsoft.Testing.Platform.TestHost.SessionUid;
using BindingFlags = System.Reflection.BindingFlags;

namespace DevTools.TUnit.Runtime;

#pragma warning disable TPEXP

internal static class TUnitEngineHost
{
    const string EngineAssemblyName = "TUnit.Engine";
    const string ExtensionTypeName = "TUnit.Engine.Framework.TUnitExtension";
    const string FrameworkTypeName = "TUnit.Engine.Framework.TUnitTestFramework";
    const string ServiceProviderTypeName = "Microsoft.Testing.Platform.Services.ServiceProvider";
    const string ClientInfoTypeName = "Microsoft.Testing.Platform.Services.ClientInfoService";
    const string OutputDeviceTypeName = "Microsoft.Testing.Platform.OutputDevice.NopPlatformOutputDevice";

    public static IReadOnlyList<TestingCaseResult> Run(
        TestingSelection selection,
        CancellationToken cancellationToken)
    {
        SourceRegistrar.IsEnabled = true;
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

    static IReadOnlyList<TestingCaseResult> RunEngine(
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
        var messageBus = new TUnitEngineMessageBus();
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

        return TUnitEngineResults.Map(messageBus.Nodes.Values);
    }

    static ITestExecutionFilter CreateFilter(TestingSelection selection)
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

    static object CreateServiceProvider(ReflectionAssembly platform, string workingDirectory, string resultDirectory)
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

    static Type RequiredType(ReflectionAssembly assembly, string typeName) =>
        assembly.GetType(typeName, throwOnError: true)
        ?? throw new InvalidOperationException($"Type '{typeName}' was not found in '{assembly.GetName().Name}'.");
}
