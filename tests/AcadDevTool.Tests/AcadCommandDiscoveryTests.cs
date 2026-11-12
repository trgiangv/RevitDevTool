using System.Collections;
using System.Reflection;
using AcadDevTool.Adapters;
using Autodesk.AutoCAD.Runtime;

namespace AcadDevTool.Tests;

[TestClass]
public sealed class AcadCommandDiscoveryTests
{
    [TestMethod]
    public void Command_name_ignores_group_name()
    {
        List<string> names;
        try
        {
            names = ParseCommands(typeof(DiscoveryCommands).Assembly.Location)
                .Where(command => Read(command, "FullClassName").StartsWith(typeof(DiscoveryCommands).FullName!, StringComparison.Ordinal))
                .Select(command => Read(command, "Name"))
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList();
        }
        catch (BadImageFormatException)
        {
            Assert.Inconclusive("AutoCAD reference assemblies are not loadable outside the host process.");
            return;
        }

        CollectionAssert.AreEqual(new[] { "RDT_FLAGGED", "RDT_GROUPED", "RDT_LOCALIZED", "RDT_SIMPLE" }, names);
    }

    private static IEnumerable<object> ParseCommands(string assemblyPath)
    {
        var ctor = typeof(AcadCommandDiscovery).GetConstructors(BindingFlags.Instance | BindingFlags.Public).Single();
        var logger = AcadCompiledScriptBridgeTests.CreateNullLogger(ctor.GetParameters().Single().ParameterType);
        var discovery = ctor.Invoke([logger]);
        var parse = typeof(AcadCommandDiscovery).GetMethod(nameof(AcadCommandDiscovery.ParseCommands))!;
        return ((IEnumerable)parse.Invoke(discovery, [assemblyPath])!).Cast<object>();
    }

    private static string Read(object command, string property) =>
        (string)command.GetType().GetProperty(property)!.GetValue(command)!;
}

public sealed class DiscoveryCommands
{
    [CommandMethod("RDT_SIMPLE")]
    public void Simple()
    {
    }

    [CommandMethod("RDT_FLAGGED", CommandFlags.Modal)]
    public void Flagged()
    {
    }

    [CommandMethod("RDT_GROUP", "RDT_GROUPED", CommandFlags.Modal)]
    public static void Grouped()
    {
    }

    [CommandMethod("RDT_GROUP", "RDT_LOCALIZED", "RDT_LOCALIZED_ID", CommandFlags.Modal)]
    public void Localized()
    {
    }

    [CommandMethod("RDT_WITH_ARGS")]
    public void WithArguments(int value)
    {
    }
}
