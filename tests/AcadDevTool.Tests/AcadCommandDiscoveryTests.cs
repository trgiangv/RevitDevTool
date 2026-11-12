using AcadDevTool.Adapters;
using Autodesk.AutoCAD.Runtime;
using Microsoft.Extensions.Logging.Abstractions;

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
            var discovery = new AcadCommandDiscovery(NullLogger<AcadCommandDiscovery>.Instance);
            names = discovery.ParseCommands(typeof(DiscoveryCommands).Assembly.Location)
                .Where(command => command.FullClassName.StartsWith(typeof(DiscoveryCommands).FullName!, StringComparison.Ordinal))
                .Select(command => command.Name)
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
