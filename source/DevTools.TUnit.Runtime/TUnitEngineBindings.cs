using System.Reflection;
using DevTools.Testing.Abstractions.Runtime;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using MtpTestSessionContext = Microsoft.Testing.Platform.TestHost.TestSessionContext;
using SessionUid = Microsoft.Testing.Platform.TestHost.SessionUid;

namespace DevTools.TUnit.Runtime;

#pragma warning disable TPEXP

/// <summary>
/// Every reach into TUnit.Engine and MTP internals the in-host run needs, resolved
/// once per process (the generation owns its own copy of this type) and validated
/// together, so a TUnit or MTP bump fails here with the missing member named.
/// TUnit is hosted without <c>TestApplication</c>: the engine framework is built
/// directly over a hand-made MTP service set.
/// </summary>
internal sealed class TUnitEngineBindings
{
    private const string EngineAssemblyName = "TUnit.Engine";
    private const string ExtensionTypeName = "TUnit.Engine.Framework.TUnitExtension";
    private const string FrameworkTypeName = "TUnit.Engine.Framework.TUnitTestFramework";
    private const string ServiceProviderTypeName = "Microsoft.Testing.Platform.Services.ServiceProvider";
    private const string OutputDeviceTypeName = "Microsoft.Testing.Platform.OutputDevice.NopPlatformOutputDevice";

    // PublicationOnly: a failed resolve (engine not loadable yet) is not cached.
    private static readonly Lazy<TUnitEngineBindings> Shared =
        new(Resolve, LazyThreadSafetyMode.PublicationOnly);

    private readonly ConstructorInfo _extension;
    private readonly ConstructorInfo _framework;
    private readonly ConstructorInfo _serviceProvider;
    private readonly ConstructorInfo _nopOutputDevice;
    private readonly MethodInfo _addService;
    private readonly ConstructorInfo _sessionContext;
    private readonly ConstructorInfo _createContext;
    private readonly ConstructorInfo _closeContext;

    private TUnitEngineBindings(
        ConstructorInfo extension,
        ConstructorInfo framework,
        ConstructorInfo serviceProvider,
        ConstructorInfo nopOutputDevice,
        MethodInfo addService,
        ConstructorInfo sessionContext,
        ConstructorInfo createContext,
        ConstructorInfo closeContext)
    {
        _extension = extension;
        _framework = framework;
        _serviceProvider = serviceProvider;
        _nopOutputDevice = nopOutputDevice;
        _addService = addService;
        _sessionContext = sessionContext;
        _createContext = createContext;
        _closeContext = closeContext;
    }

    public static TUnitEngineBindings Instance => Shared.Value;

    /// <summary>A new engine framework over <paramref name="services"/> (one per run: it holds run state).</summary>
    public ITestFramework CreateFramework(object services) =>
        (ITestFramework)_framework.Invoke(
            [(IExtension)_extension.Invoke(null), services, new TestFrameworkCapabilities()]);

    /// <summary>The MTP <c>ServiceProvider</c> the engine reads its platform services from.</summary>
    public object CreateServices(string workingDirectory, string resultDirectory)
    {
        var provider = _serviceProvider.Invoke(null);
        Add(provider, new TUnitEngineLoggerFactory());
        Add(provider, new TUnitEngineCommandLine());
        Add(provider, new TUnitEngineConfiguration(workingDirectory, resultDirectory));
        Add(provider, new TUnitEngineOutputDevice());
        Add(provider, _nopOutputDevice.Invoke(null));
        Add(provider, new TUnitEngineClientInfo());
        return provider;
    }

    public MtpTestSessionContext CreateSessionContext(SessionUid sessionUid) =>
        (MtpTestSessionContext)_sessionContext.Invoke([sessionUid]);

    public CreateTestSessionContext CreateCreateContext(
        SessionUid sessionUid,
        CancellationToken cancellationToken) =>
        (CreateTestSessionContext)
        _createContext.Invoke([sessionUid, cancellationToken]);

    public CloseTestSessionContext CreateCloseContext(
        SessionUid sessionUid,
        CancellationToken cancellationToken) =>
        (CloseTestSessionContext)
        _closeContext.Invoke([sessionUid, cancellationToken]);

    private void Add(object provider, object service) => _addService.Invoke(provider, [service, false]);

    private static TUnitEngineBindings Resolve()
    {
        var engine = System.Reflection.Assembly.Load(EngineAssemblyName);
        var platform = typeof(ITestFramework).Assembly;
        var serviceProvider = InternalMembers.Type(platform, ServiceProviderTypeName);

        return new TUnitEngineBindings(
            InternalMembers.Constructor(InternalMembers.Type(engine, ExtensionTypeName)),
            InternalMembers.Constructor(
                InternalMembers.Type(engine, FrameworkTypeName),
                typeof(IExtension),
                typeof(IServiceProvider),
                typeof(ITestFrameworkCapabilities)),
            InternalMembers.Constructor(serviceProvider),
            InternalMembers.Constructor(InternalMembers.Type(platform, OutputDeviceTypeName)),
            InternalMembers.Method(serviceProvider, "AddService", typeof(object), typeof(bool)),
            InternalMembers.Constructor(typeof(MtpTestSessionContext), typeof(SessionUid)),
            InternalMembers.Constructor(
                typeof(CreateTestSessionContext),
                typeof(SessionUid),
                typeof(CancellationToken)),
            InternalMembers.Constructor(
                typeof(CloseTestSessionContext),
                typeof(SessionUid),
                typeof(CancellationToken)));
    }
}
