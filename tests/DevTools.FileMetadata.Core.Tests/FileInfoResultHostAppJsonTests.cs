using System.Text.Json;
using DevTools.FileMetadata.Core;
using DevTools.Hosting;

namespace DevTools.FileMetadata.Core.Tests;

public sealed class FileInfoResultHostAppJsonTests
{
    [Theory]
    [InlineData(HostApp.Revit, "\"hostApp\":\"Revit\"")]
    [InlineData(HostApp.AutoCad, "\"hostApp\":\"AutoCad\"")]
    public void FileInfoResult_serializes_family_host_app_as_wire_string(HostApp hostApp, string expected)
    {
        FileInfoResult result = new TestFileInfoResult
        {
            HostApplication = hostApp,
            FilePath = @"C:\sample",
            FileName = "sample"
        };

        var json = JsonSerializer.Serialize(result);

        Assert.Contains(expected, json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"hostApp\":\"Civil3D\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"hostApp\":\"Plant3D\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"hostApp\":\"AcadMep\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void IFileReader_keeps_supported_extensions()
    {
        IFileReader reader = new StubFileReader();

        Assert.Equal([".rvt"], reader.SupportedExtensions);
    }

    private sealed class TestFileInfoResult : FileInfoResult;

    private sealed class StubFileReader : IFileReader
    {
        public IReadOnlyList<string> SupportedExtensions { get; } = [".rvt"];

        public FileInfoResult Read(FileInfoRequest request) =>
            new TestFileInfoResult
            {
                HostApplication = HostApp.Revit,
                FilePath = request.FilePath,
                FileName = Path.GetFileName(request.FilePath)
            };
    }
}
