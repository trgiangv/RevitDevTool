using DevTools.FileMetadata.Acad;
using DevTools.FileMetadata.Core;
using DevTools.FileMetadata.Revit;
using Microsoft.Extensions.DependencyInjection;

namespace DevTools.FileMetadata.Core.Tests;

[TestClass]
public sealed class FileReaderCatalogTests
{
    [TestMethod]
    public void FileReaderCatalog_SelectsReaderThatSupportsRequest()
    {
        var revit = new Moq.Mock<IFileReader>();
        revit.SetupGet(reader => reader.SupportedExtensions).Returns([".rvt"]);
        var catalog = new FileReaderCatalog([revit.Object]);

        Assert.AreSame(revit.Object, catalog.GetReader("sample.rvt"));
    }

    [TestMethod]
    public void FileReaderCatalog_ThrowsFileErrorForUnknownExtension()
    {
        var catalog = new FileReaderCatalog([]);

        var exception = Assert.ThrowsExactly<FileReadException>(() => catalog.GetReader("sample.txt"));
        Assert.AreEqual(FileError.UnsupportedFormat, exception.Error);
    }

    [TestMethod]
    public void FormatSupportedExtensions_lists_distinct_sorted_extensions()
    {
        var first = new Moq.Mock<IFileReader>();
        first.SetupGet(reader => reader.SupportedExtensions).Returns([".dwg", ".rvt"]);
        var second = new Moq.Mock<IFileReader>();
        second.SetupGet(reader => reader.SupportedExtensions).Returns([".RVT", ".dxf"]);

        var catalog = new FileReaderCatalog([first.Object, second.Object]);
        Assert.AreEqual(".dwg, .dxf, .rvt", catalog.FormatSupportedExtensions());
    }

    [TestMethod]
    public void FileMetadataComposition_ResolvesCatalogWithBothFormatReaders()
    {
        var services = new ServiceCollection();
        services
            .AddFileMetadataReaders()
            .AddRevitFileMetadataReader()
            .AddAcadFileMetadataReader();
        using var provider = services.BuildServiceProvider();

        var catalog = provider.GetRequiredService<IFileReaderCatalog>();
        Assert.IsInstanceOfType<RevitFileMetadataReader>(catalog.GetReader("sample.rvt"));
        Assert.IsInstanceOfType<AcadFileMetadataReader>(catalog.GetReader("sample.dwg"));
    }

    [TestMethod]
    public void AcadFileMetadataReader_RecognizesDwgCaseInsensitively()
    {
        var reader = new AcadFileMetadataReader();

        Assert.Contains(".dwg", reader.SupportedExtensions, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain(".rvt", reader.SupportedExtensions, StringComparer.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void RevitFileMetadataReader_RegistersOnlySupportedRevitExtensions()
    {
        var reader = new RevitFileMetadataReader();

        Assert.AreSequenceEqual([".rvt", ".rfa", ".rft", ".rte"], reader.SupportedExtensions);
        Assert.DoesNotContain(".dwg", reader.SupportedExtensions, StringComparer.OrdinalIgnoreCase);
    }
}
