using System.Reflection;
using NUnit;
using NUnit.Framework.Api;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using NUnit.Framework.Internal.Builders;

namespace DevTools.NUnit.MTP;

/// <summary>
/// Same GetTypes policy as the in-host runtime: keep fixtures that loaded
/// when a few referenced types are missing.
/// </summary>
internal sealed class NUnitLocalAssemblyBuilder : ITestAssemblyBuilder
{
    private readonly ISuiteBuilder _suiteBuilder = new DefaultSuiteBuilder();

    public ITest Build(Assembly assembly, IDictionary<string, object> options)
    {
        ApplyWorkDirectory(options);
        try
        {
            var fixtures = new List<Test>();
            foreach (var type in GetLoadableTypes(assembly))
            {
                var typeInfo = new TypeWrapper(type);
                if (!_suiteBuilder.CanBuildFrom(typeInfo))
                    continue;

                fixtures.Add(_suiteBuilder.BuildFrom(typeInfo));
            }

            if (fixtures.Count == 0)
            {
                var empty = new TestAssembly(assembly.Location);
                empty.MakeInvalid("No test fixtures were found.");
                return empty;
            }

            TestSuite root = new TestAssembly(assembly, assembly.Location);
            var treeBuilder = new NamespaceTreeBuilder(root);
            treeBuilder.Add(fixtures);
            return treeBuilder.RootSuite;
        }
        catch (Exception ex)
        {
            var failed = new TestAssembly(assembly.Location);
            failed.MakeInvalid(ex.Message);
            return failed;
        }
    }

    public ITest Build(string assemblyNameOrPath, IDictionary<string, object> options)
    {
        var assembly = Assembly.LoadFrom(assemblyNameOrPath);
        return Build(assembly, options);
    }

    private static void ApplyWorkDirectory(IDictionary<string, object> options)
    {
        if (!options.TryGetValue(FrameworkPackageSettings.WorkDirectory, out var value)
            || value is not string workDirectory
            || string.IsNullOrWhiteSpace(workDirectory))
        {
            workDirectory = Directory.GetCurrentDirectory();
        }

        var field = typeof(global::NUnit.Framework.TestContext).GetField(
            "DefaultWorkDirectory",
            BindingFlags.Static | BindingFlags.NonPublic);
        field?.SetValue(null, workDirectory);
    }

    private static IReadOnlyList<Type> GetLoadableTypes(Assembly assembly)
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
}
