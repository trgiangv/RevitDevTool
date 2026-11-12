using DevTools.AssemblyIsolation.Metadata;
using static DevTools.AssemblyIsolation.Tests.MetadataAssemblySessionTests;

namespace DevTools.AssemblyIsolation.Tests;

[TestClass]
public sealed class MetadataScanTests
{
    [TestMethod]
    public void Broken_type_does_not_hide_others()
    {
        using var directory = new TemporaryDirectory();
        using var session = OpenConsumer(directory.Path);
        var scan = new MetadataScan();

        var disposable = scan.GetTypes(session.LoadEntryAssembly())
            .Where(type => type.Namespace == "MissingReferenceConsumer")
            .Where(type => scan.TryRead(type.FullName!, () => type.GetInterfaces().Any(i => i.FullName == typeof(IDisposable).FullName), out var found) && found)
            .Select(type => type.Name)
            .ToArray();

        CollectionAssert.AreEqual(new[] { "WorkingCommand" }, disposable);
        Assert.AreEqual(1, scan.Skipped.Count);
        StringAssert.Contains(scan.Skipped[0], "BrokenCommand");
        StringAssert.Contains(scan.Skipped[0], "MissingReferenceDependency");
    }

    [TestMethod]
    public void Attribute_lookup_skips_unresolved_attributes()
    {
        using var directory = new TemporaryDirectory();
        using var session = OpenConsumer(directory.Path);
        var run = WorkingCommand(session).GetMethod("Run")!;

        var obsolete = MetadataScan.FindAttribute(run, typeof(ObsoleteAttribute).FullName!);

        Assert.IsNotNull(obsolete);
        Assert.AreEqual("marker", obsolete.ConstructorArguments[0].Value);
    }

    [TestMethod]
    public void Unresolved_member_is_recorded()
    {
        using var directory = new TemporaryDirectory();
        using var session = OpenConsumer(directory.Path);
        var value = WorkingCommand(session).GetProperty("Value")!;
        var scan = new MetadataScan();

        var read = scan.TryRead("WorkingCommand.Value", () => value.PropertyType.FullName, out _);

        Assert.IsFalse(read);
        StringAssert.StartsWith(scan.Skipped.Single(), "WorkingCommand.Value:");
    }

    static MetadataAssemblySession OpenConsumer(string directory)
    {
        var path = CopyFixture("MissingReferenceConsumer", "MissingReferenceConsumer.dll", directory);
        return MetadataAssemblySession.Create(path,
            Directory.GetFiles(Path.GetDirectoryName(typeof(object).Assembly.Location)!, "*.dll"));
    }

    static Type WorkingCommand(MetadataAssemblySession session) =>
        session.LoadEntryAssembly().GetType("MissingReferenceConsumer.WorkingCommand", throwOnError: true)!;
}
