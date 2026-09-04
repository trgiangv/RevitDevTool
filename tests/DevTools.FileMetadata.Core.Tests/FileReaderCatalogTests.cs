using DevTools.FileMetadata.Acad;
using DevTools.FileMetadata.Core;
using DevTools.FileMetadata.Revit;
using Microsoft.Extensions.DependencyInjection;

namespace DevTools.FileMetadata.Core.Tests;

public sealed class FileReaderCatalogTests
{
    [Fact]
    public void FileReaderCatalog_SelectsReaderThatSupportsRequest()
    {
        var revit = new Moq.Mock<IFileReader>();
        revit.SetupGet(reader => reader.SupportedExtensions).Returns([".rvt"]);
        var catalog = new FileReaderCatalog([revit.Object]);

        Assert.Same(revit.Object, catalog.GetReader("sample.rvt"));
    }

    [Fact]
    public void FileReaderCatalog_ThrowsFileErrorForUnknownExtension()
    {
        var catalog = new FileReaderCatalog([]);

        var exception = Assert.Throws<FileReadException>(() => catalog.GetReader("sample.txt"));
        Assert.Equal(FileError.UnsupportedFormat, exception.Error);
    }

    [Fact]
    public void FormatSupportedExtensions_lists_distinct_sorted_extensions()
    {
        var first = new Moq.Mock<IFileReader>();
        first.SetupGet(reader => reader.SupportedExtensions).Returns([".dwg", ".rvt"]);
        var second = new Moq.Mock<IFileReader>();
        second.SetupGet(reader => reader.SupportedExtensions).Returns([".RVT", ".dxf"]);

        var catalog = new FileReaderCatalog([first.Object, second.Object]);
        Assert.Equal(".dwg, .dxf, .rvt", catalog.FormatSupportedExtensions());
    }

    [Fact]
    public void FileMetadataComposition_ResolvesCatalogWithBothFormatReaders()
    {
        var services = new ServiceCollection();
        services
            .AddFileMetadataReaders()
            .AddRevitFileMetadataReader()
            .AddAcadFileMetadataReader();
        using var provider = services.BuildServiceProvider();

        var catalog = provider.GetRequiredService<IFileReaderCatalog>();
        Assert.IsType<RevitFileMetadataReader>(catalog.GetReader("sample.rvt"));
        Assert.IsType<AcadFileMetadataReader>(catalog.GetReader("sample.dwg"));
    }

    [Fact]
    public void AcadFileMetadataReader_RecognizesDwgCaseInsensitively()
    {
        var reader = new AcadFileMetadataReader();

        Assert.Contains(".dwg", reader.SupportedExtensions, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain(".rvt", reader.SupportedExtensions, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void RevitFileMetadataReader_RegistersOnlySupportedRevitExtensions()
    {
        var reader = new RevitFileMetadataReader();

        Assert.Equal([".rvt", ".rfa", ".rft", ".rte"], reader.SupportedExtensions);
        Assert.DoesNotContain(".dwg", reader.SupportedExtensions, StringComparer.OrdinalIgnoreCase);
    }
}
