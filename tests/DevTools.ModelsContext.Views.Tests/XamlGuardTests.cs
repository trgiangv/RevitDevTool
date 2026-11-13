using System.IO;
using System.Text.RegularExpressions;

namespace DevTools.ModelsContext.Views.Tests;

[TestClass]
public sealed class XamlGuardTests
{
    private static readonly Regex ComponentUri = new(@"/([\w\.]+);component/", RegexOptions.CultureInvariant);

    [TestMethod]
    public void PresentationXamlUsesNoSelfUris()
    {
        var root = Path.Combine(RepositoryRoot(), "source", "DevTools.Presentation");
        var hits = Directory.EnumerateFiles(root, "*.xaml", SearchOption.AllDirectories)
            .Where(file => !file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                           && !file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
            .SelectMany(file => ComponentUri.Matches(File.ReadAllText(file)).Cast<Match>()
                .Where(match => match.Groups[1].Value != "DevTools.UI")
                .Select(match => Path.GetFileName(file) + ": " + match.Value))
            .ToList();

        Assert.IsEmpty(hits, string.Join(Environment.NewLine, hits));
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "RevitDevTool.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("RevitDevTool.slnx was not found above " + AppContext.BaseDirectory);
    }
}
