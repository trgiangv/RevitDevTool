using System.Text.Json;

namespace RevitDevTool.ModelsContext.LiveTests;

[TestFixture]
public sealed class SafetyEndTests
{
    [Test]
    [Explicit("Run by scripts/test-models-context.ps1 after L3: the run's documents are unchanged and no temporary mode is left.")]
    public void EndStateIsUnchanged()
    {
        var folder = Evidence.LatestFolder();
        if (folder is null)
        {
            Assert.Inconclusive("No run folder under artifacts/models-context-tests; run L2 first.");
        }

        var violations = SafetyGuard.CheckAgainst(folder!, includeSelection: false);
        File.WriteAllText(Path.Combine(folder!, SafetyGuard.EndFile),
            JsonSerializer.Serialize(new { Violations = violations, State = SafetyGuard.Current() }, CaseFiles.Options));

        Assert.That(violations, Is.Empty, "Safety end state: " + string.Join("; ", violations));
    }
}
