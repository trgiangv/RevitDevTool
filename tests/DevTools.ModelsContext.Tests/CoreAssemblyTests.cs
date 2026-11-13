using System.Reflection;

namespace DevTools.ModelsContext.Tests;

[TestClass]
public sealed class CoreAssemblyTests
{
    private static readonly string[] Forbidden =
    [
        "RevitAPI",
        "RevitAPIUI",
        "PresentationFramework",
        "PresentationCore",
        "WindowsBase",
        "System.Xaml",
        "DevTools.UI"
    ];

    [TestMethod]
    public void CoreReferencesNoHostOrWpf()
    {
        var core = Assembly.Load("DevTools.ModelsContext");

        var referenced = core.GetReferencedAssemblies().Select(name => name.Name).ToList();

        CollectionAssert.AreEqual(Array.Empty<string>(), referenced.Intersect(Forbidden).ToArray());
    }
}
