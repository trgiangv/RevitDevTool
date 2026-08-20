using System.Diagnostics;
using System.Reflection;
using NUnit;
using NUnit.Framework;
using NUnit.Framework.Api;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using NUnit.Framework.Internal.Builders;

namespace DevTools.NUnit.Runtime;

/// <summary>
/// NUnit's <see cref="DefaultTestAssemblyBuilder"/> calls
/// <see cref="Assembly.GetTypes"/> and marks the whole assembly invalid on
/// <see cref="ReflectionTypeLoadException"/>. Revit test assemblies often
/// have a few unloadable types while fixtures still load. Use the types that
/// did load.
/// </summary>
internal sealed class NUnitTolerantAssemblyBuilder : ITestAssemblyBuilder
{
    private readonly ISuiteBuilder _suiteBuilder = new DefaultSuiteBuilder();

    public ITest Build(Assembly assembly, IDictionary<string, object> options)
    {
        var assemblyPath = AssemblyHelper.GetAssemblyPath(assembly);
        var suiteName = string.IsNullOrEmpty(assemblyPath) || assemblyPath == " "
            ? AssemblyHelper.GetAssemblyName(assembly).Name
            : assemblyPath;
        if (string.IsNullOrEmpty(suiteName))
            suiteName = " ";

        return Build(assembly, suiteName!, options);
    }

    public ITest Build(string assemblyNameOrPath, IDictionary<string, object> options)
    {
        try
        {
            var assembly = AssemblyHelper.Load(assemblyNameOrPath);
            return Build(assembly, assemblyNameOrPath, options);
        }
        catch (Exception ex)
        {
            var testAssembly = new TestAssembly(assemblyNameOrPath);
            testAssembly.MakeInvalid(ExceptionHelper.BuildMessage(ex, true));
            return testAssembly;
        }
    }

    private TestSuite Build(Assembly assembly, string assemblyNameOrPath, IDictionary<string, object> options)
    {
        try
        {
            ApplyBuilderOptions(options);
            var fixtures = GetFixtures(assembly);
            return BuildTestAssembly(assembly, assemblyNameOrPath, fixtures);
        }
        catch (Exception ex)
        {
            var testAssembly = new TestAssembly(assemblyNameOrPath);
            testAssembly.MakeInvalid(ExceptionHelper.BuildMessage(ex, true));
            return testAssembly;
        }
    }

    private List<Test> GetFixtures(Assembly assembly)
    {
        var fixtures = new List<Test>();
        foreach (var testType in GetLoadableTypes(assembly))
        {
            var typeInfo = new TypeWrapper(testType);
            if (!_suiteBuilder.CanBuildFrom(typeInfo))
                continue;

            fixtures.Add(_suiteBuilder.BuildFrom(typeInfo));
        }

        return fixtures;
    }

    /// <summary>
    /// Same process-wide NUnit state <see cref="DefaultTestAssemblyBuilder"/> sets.
    /// <c>TestContext.DefaultWorkDirectory</c> is internal on NUnit 4.6.1.
    /// </summary>
    internal static void ApplyBuilderOptions(IDictionary<string, object> options)
    {
        if (options.TryGetValue(FrameworkPackageSettings.DefaultTestNamePattern, out var defaultTestNamePattern))
            TestNameGenerator.DefaultTestNamePattern = (string)defaultTestNamePattern;

        string workDirectory;
        if (options.TryGetValue(FrameworkPackageSettings.WorkDirectory, out var workDirectoryValue)
            && workDirectoryValue is string configured
            && !string.IsNullOrWhiteSpace(configured))
        {
            workDirectory = configured;
        }
        else
        {
            workDirectory = Directory.GetCurrentDirectory();
        }

        SetDefaultWorkDirectory(workDirectory);
    }

    internal static void SetDefaultWorkDirectory(string workDirectory)
    {
        var field = typeof(TestContext).GetField(
            "DefaultWorkDirectory",
            BindingFlags.Static | BindingFlags.NonPublic);
        if (field is null)
        {
            throw new InvalidOperationException(
                "NUnit TestContext.DefaultWorkDirectory is missing; WorkDirectory cannot be initialized.");
        }

        field.SetValue(null, workDirectory);
    }

    internal static IReadOnlyList<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(type => type is not null).Cast<Type>().ToList();
        }
    }

    private static TestSuite BuildTestAssembly(Assembly assembly, string assemblyNameOrPath, List<Test> fixtures)
    {
        TestSuite testAssembly = new TestAssembly(assembly, assemblyNameOrPath);
        if (fixtures.Count == 0)
        {
            testAssembly.MakeInvalid("No test fixtures were found.");
            return testAssembly;
        }

        var treeBuilder = new NamespaceTreeBuilder(testAssembly);
        treeBuilder.Add(fixtures);
        testAssembly = treeBuilder.RootSuite;
        testAssembly.ApplyAttributesToTest(assembly);
        try
        {
            using var process = Process.GetCurrentProcess();
            testAssembly.Properties.Set(PropertyNames.ProcessId, process.Id);
        }
        catch (PlatformNotSupportedException)
        {
        }

        testAssembly.Properties.Set(PropertyNames.AppDomain, AppDomain.CurrentDomain.FriendlyName);
        testAssembly.Sort();
        return testAssembly;
    }
}
