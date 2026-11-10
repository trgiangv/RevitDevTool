using Build.Modules;
using Build.Tools;
using System.Text.Json.Nodes;

namespace Build.Tests;

[TestClass]
public sealed class CiJobsTests
{
    [TestMethod]
    public void Addin_has_a_job_per_host_version()
    {
        var jobs = CiJobs.For(Product.Addin, ["2025", "2026"]);

        CollectionAssert.AreEqual(new[] { "addin tests", "Autodesk 2025", "Autodesk 2026" }, jobs.Select(job => job.Name).ToArray());
        Assert.AreEqual(new CiJob("Autodesk 2026", "compile addin --host-version 2026", "autodesk-2026", HostVersion: "2026"), jobs[2]);
        Assert.IsTrue(jobs.All(job => job.Pack is null));
    }

    [TestMethod]
    public void Other_products_run_ci_and_pack()
    {
        Assert.AreEqual(new CiJob("daemon", "ci daemon", "daemon", "daemon"), CiJobs.For(Product.Daemon, ["2025"]).Single());
    }

    [TestMethod]
    public async Task Committed_jobs_file_is_current()
    {
        var versions = await ResolveConfigsModule.HostVersionsAsync(CancellationToken.None);

        Assert.IsTrue(CiJobs.IsCurrent(ProductPathsTests.RepositoryRoot, versions), "Run 'dotnet run --project build -- paths' and commit .github/ci-jobs.json.");
    }

    [TestMethod]
    [DataRow("""{"pull_request":{"base":{"sha":"a1"}},"before":"b2"}""", "a1")]
    [DataRow("""{"merge_group":{"base_sha":"c3"}}""", "c3")]
    [DataRow("""{"before":"b2"}""", "b2")]
    [DataRow("""{"inputs":{}}""", null)]
    public void Base_comes_from_the_event(string payload, string? expected)
    {
        Assert.AreEqual(expected, GitHubEvent.BaseSha(JsonNode.Parse(payload)));
    }
}
