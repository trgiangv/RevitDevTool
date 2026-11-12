using System.Reflection;
using AcadDevTool.Adapters;
using Autodesk.AutoCAD.Runtime;
using DevTools.AssemblyIsolation;
using DevTools.Execution.Providers.Dotnet;
using Microsoft.Extensions.Logging.Abstractions;

namespace AcadDevTool.Tests;

[TestClass]
public sealed class AcadCommandDiscoveryTests
{
    private static readonly Lazy<List<CommandItem>> DiscoveredCommands = new(() =>
        new AcadCommandDiscovery(NullLogger<AcadCommandDiscovery>.Instance, new AutoCadApiOnly())
            .ParseCommands(typeof(AcadCommandDiscoveryTests).Assembly.Location));

    private static List<CommandItem> Commands => DiscoveredCommands.Value;

    [TestMethod]
    public void Global_name_overload_uses_the_first_argument()
    {
        var command = Single("DISCOVERY_PLAIN");

        Assert.AreEqual($"{typeof(PlainCommands).FullName}.{nameof(PlainCommands.Plain)}", command.FullClassName);
    }

    [TestMethod]
    public void Group_overload_uses_the_global_name_not_the_group()
    {
        Single("DISCOVERY_GROUPED");
        Single("DISCOVERY_GROUPED_LOCALIZED");

        Assert.IsFalse(Commands.Any(c => c.Name == "DISCOVERY_GROUP"), "The group name was reported as a command name.");
    }

    [TestMethod]
    public void Methods_with_parameters_or_a_return_value_are_not_commands()
    {
        Assert.IsFalse(Commands.Any(c => c.Name is "DISCOVERY_WITH_PARAMETER" or "DISCOVERY_WITH_RETURN"));
    }

    [TestMethod]
    public void An_overloaded_method_name_is_listed_once()
    {
        var command = Single("DISCOVERY_OVERLOADED");

        Assert.AreEqual($"{typeof(OverloadedCommands).FullName}.{nameof(OverloadedCommands.Run)}", command.FullClassName);
    }

    [TestMethod]
    public void A_concrete_base_command_is_not_repeated_for_each_subclass()
    {
        var command = Single("DISCOVERY_CONCRETE_BASE");

        Assert.AreEqual(
            $"{typeof(ConcreteBaseCommands).FullName}.{nameof(ConcreteBaseCommands.FromBase)}",
            command.FullClassName);
    }

    [TestMethod]
    public void An_abstract_base_command_is_listed_under_its_concrete_subclass()
    {
        var command = Single("DISCOVERY_ABSTRACT_BASE");

        Assert.AreEqual(
            $"{typeof(DerivedFromAbstractCommands).FullName}.{nameof(AbstractBaseCommands.FromAbstractBase)}",
            command.FullClassName);
    }

    [TestMethod]
    public void Global_name_reader_accepts_every_constructor_shape()
    {
        Assert.AreEqual("DISCOVERY_PLAIN", AcadCommandDiscovery.GetGlobalCommandName(Method<PlainCommands>(nameof(PlainCommands.Plain))));
        Assert.AreEqual("DISCOVERY_FLAGGED", AcadCommandDiscovery.GetGlobalCommandName(Method<PlainCommands>(nameof(PlainCommands.Flagged))));
        Assert.AreEqual("DISCOVERY_GROUPED", AcadCommandDiscovery.GetGlobalCommandName(Method<PlainCommands>(nameof(PlainCommands.Grouped))));
        Assert.IsNull(AcadCommandDiscovery.GetGlobalCommandName(Method<PlainCommands>(nameof(PlainCommands.NotACommand))));
    }

    [TestMethod]
    public void A_missing_assembly_returns_no_commands()
    {
        var discovery = new AcadCommandDiscovery(NullLogger<AcadCommandDiscovery>.Instance, new AutoCadApiOnly());

        Assert.IsEmpty(discovery.ParseCommands(Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.dll")));
    }

    private static CommandItem Single(string name)
    {
        var matches = Commands.Where(c => c.Name == name).ToList();
        Assert.HasCount(1, matches, $"Expected exactly one command named '{name}'.");
        return matches[0];
    }

    private static MethodInfo Method<T>(string name) =>
        typeof(T).GetMethod(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static, null, Type.EmptyTypes, null)!;

    /// <summary>Resolves against the AutoCAD API package only, without the vertical assemblies the host would add.</summary>
    private sealed class AutoCadApiOnly : HostAssemblies
    {
        protected override IEnumerable<Assembly> LoadedByType
        {
            get { yield return typeof(CommandMethodAttribute).Assembly; }
        }

        protected override IReadOnlyList<string> LoadedByName { get; } = [];
    }

    public sealed class PlainCommands
    {
        [CommandMethod("DISCOVERY_PLAIN")]
        public static void Plain() { }

        [CommandMethod("DISCOVERY_FLAGGED", CommandFlags.Modal)]
        public void Flagged() { }

        [CommandMethod("DISCOVERY_GROUP", "DISCOVERY_GROUPED", CommandFlags.Modal)]
        public void Grouped() { }

        [CommandMethod("DISCOVERY_GROUP", "DISCOVERY_GROUPED_LOCALIZED", "DISCOVERY_GROUPED_LOCAL", CommandFlags.Modal)]
        public void GroupedLocalized() { }

        [CommandMethod("DISCOVERY_WITH_PARAMETER")]
        public void WithParameter(int value) { }

        [CommandMethod("DISCOVERY_WITH_RETURN")]
        public int WithReturn() => 0;

        public void NotACommand() { }
    }

    public sealed class OverloadedCommands
    {
        [CommandMethod("DISCOVERY_OVERLOADED")]
        public void Run() { }

        public void Run(int value) { }
    }

    public class ConcreteBaseCommands
    {
        [CommandMethod("DISCOVERY_CONCRETE_BASE")]
        public void FromBase() { }
    }

    public sealed class DerivedFromConcreteCommands : ConcreteBaseCommands;

    public abstract class AbstractBaseCommands
    {
        [CommandMethod("DISCOVERY_ABSTRACT_BASE")]
        public void FromAbstractBase() { }
    }

    public sealed class DerivedFromAbstractCommands : AbstractBaseCommands;
}
