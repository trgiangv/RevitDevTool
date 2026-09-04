using System.Text.Json;
using System.Text.Json.Nodes;
using DevTools.FileMetadata.Core;
using DevTools.FileMetadata.Acad;
using DevTools.FileMetadata.Revit;
using DevTools.Hosting;
using DevTools.Mcp.Catalog;
using DevTools.Mcp.Adapter.Bridging;
using DevTools.Execution.External.Mcp.Backends;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Tests;

public class ContractTests
{
    [Fact]
    public void McpResult_Success_HasValueAndNoError()
    {
        var result = McpResult<string>.Success("ok");
        Assert.True(result.IsSuccess);
        Assert.Equal("ok", result.Value);
        Assert.Null(result.Error);
    }

    [Fact]
    public void McpResult_Failure_HasErrorAndNoValue()
    {
        var error = new McpError(McpErrorCode.ValidationFailed, "Invalid request", [], "test-1");
        var result = McpResult<string>.Failure(error);
        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Equal(error, result.Error);
    }

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

    [Fact]
    public void ToolHelpers_Serialize_PreservesDerivedFileInfoFieldsWhenDeclaredAsBase()
    {
        FileInfoResult result = new RevitFileInfoSummaryResult
        {
            HostApplication = HostApp.Revit,
            FilePath = @"C:\sample.rvt",
            FileName = "sample.rvt",
            BasicInfo = new RevitBasicInfoSummary
            {
                FileVersion = 1,
                RevitVersion = "2025",
                IsWorkshared = false,
                WorksharingType = "Not enabled",
                Locale = "ENU"
            },
            ProjectTitle = "Demo",
            WorksetCount = 3,
            ExternalReferenceCount = 1
        };

        var json = ToolHelpers.Serialize(result);
        var toolResult = ToolHelpers.Result(result);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(toolResult.Content)).Text;

        Assert.Contains("\"hostApp\":\"Revit\"", json, StringComparison.Ordinal);
        Assert.Contains("\"basicInfo\"", json, StringComparison.Ordinal);
        Assert.Contains("\"revitVersion\":\"2025\"", json, StringComparison.Ordinal);
        Assert.Contains("\"worksetCount\":3", json, StringComparison.Ordinal);
        Assert.Contains("\"projectTitle\":\"Demo\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\"basicInfo\":null", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"hostApp\":\"Civil3D\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ToolHelpers_Serialize_PreservesAutoCadHostAppWireNameForDwg()
    {
        FileInfoResult result = new DwgFileInfoSummaryResult
        {
            HostApplication = HostApp.AutoCad,
            FilePath = @"C:\sample.dwg",
            FileName = "sample.dwg",
            AcadVersion = "AC1032",
            Title = "Demo",
            LayerCount = 2,
            BlockCount = 1
        };

        var json = ToolHelpers.Serialize(result);

        Assert.Contains("\"hostApp\":\"AutoCad\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"hostApp\":\"Civil3D\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"hostApp\":\"Plant3D\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"hostApp\":\"AcadMep\"", json, StringComparison.Ordinal);
        Assert.Contains("\"acadVersion\":\"AC1032\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void AddMcpCatalog_RegistersCatalogStoreAndLoader()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Moq.Mock.Of<Settings.ISettingsService>());

        services.AddMcpCatalog();
        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<McpCatalogStore>());
        Assert.NotNull(provider.GetRequiredService<IMcpCatalogLoader>());
        Assert.Contains(provider.GetServices<IMcpRegistryProvider>(), registry => registry is DotnetMcpRegistryProvider);
        Assert.Contains(provider.GetServices<IMcpRegistryProvider>(), registry => registry is BuiltInMcpRegistryProvider);
    }

    [Fact]
    public void McpRegistryCatalog_DefaultsAreEmpty()
    {
        var catalog = new McpRegistryCatalog();
        Assert.Empty(catalog.Tools);
        Assert.Empty(catalog.Resources);

        Assert.Same(McpRegistryCatalog.Empty, McpRegistryCatalog.Empty);
    }

    [Fact]
    public void McpPrimitiveBinding_CreatePrimitiveId_NormalizesDisplayNameAndToolId()
    {
        var id = McpPrimitiveBinding.CreatePrimitiveId("Read Walls", "Tools/Wall Tools");
        Assert.Equal("Read-Walls_[Tools/Wall-Tools]", id);

        var idWithSpaces = McpPrimitiveBinding.CreatePrimitiveId("read_walls", "sample:read_walls");
        Assert.Equal("read_walls_[sample:read_walls]", idWithSpaces);
    }

    [Fact]
    public void SdkInvocationMapper_ToSdk_PreservesEverySupportedContentShape()
    {
        var annotations = new Annotations { Priority = 0.5f };
        var blob = BlobResourceContents.FromBytes(new byte[] { 6, 7 }, "test://blob", "application/octet-stream");
        blob.Meta = new JsonObject { ["resource"] = "blob" };
        var response = new McpInvocationResponse
        {
            IsError = true,
            StructuredContent = JsonDocument.Parse("{\"answer\":42}").RootElement.Clone(),
            Meta = new JsonObject { ["response"] = "meta" },
            Content =
            [
                new McpTextContent("text") { Annotations = annotations, Meta = new JsonObject { ["text"] = 1 } },
                new McpImageContent(new byte[] { 1, 2, 3 }, "image/png"),
                new McpAudioContent(new byte[] { 4, 5 }, "audio/wav"),
                new McpEmbeddedTextResourceContent("test://text", "resource", "text/plain") { ResourceMeta = new JsonObject { ["resource"] = "text" } },
                new McpEmbeddedBlobResourceContent("test://blob", new byte[] { 6, 7 }, "application/octet-stream") { ResourceMeta = new JsonObject { ["resource"] = "blob" } },
                new McpResourceLinkContent("test://link", "link", "Link title", "A linked resource", "text/plain", 42) { Meta = new JsonObject { ["link"] = 1 } }
            ]
        };

        var sdk = SdkInvocationMapper.ToSdk(response);

        Assert.True(sdk.IsError);
        Assert.Equal("meta", sdk.Meta!["response"]!.GetValue<string>());
        Assert.Equal("{\"answer\":42}", sdk.StructuredContent!.Value.GetRawText());
        Assert.Equal(6, sdk.Content.Count);
        Assert.Equal(0.5f, sdk.Content[0].Annotations!.Priority);
        Assert.Equal("text", ((TextContentBlock)sdk.Content[0]).Text);
        Assert.Equal([1, 2, 3], ((ImageContentBlock)sdk.Content[1]).DecodedData.ToArray());
        Assert.Equal([4, 5], ((AudioContentBlock)sdk.Content[2]).DecodedData.ToArray());
        var textResource = Assert.IsType<TextResourceContents>(((EmbeddedResourceBlock)sdk.Content[3]).Resource);
        Assert.Equal("test://text", textResource.Uri);
        Assert.Equal("resource", textResource.Text);
        Assert.Equal("text", textResource.Meta!["resource"]!.GetValue<string>());
        var blobResource = Assert.IsType<BlobResourceContents>(((EmbeddedResourceBlock)sdk.Content[4]).Resource);
        Assert.Equal("test://blob", blobResource.Uri);
        Assert.Equal("application/octet-stream", blobResource.MimeType);
        Assert.Equal([6, 7], blobResource.DecodedData.ToArray());
        Assert.Equal("blob", blobResource.Meta!["resource"]!.GetValue<string>());
        var resourceLink = Assert.IsType<ResourceLinkBlock>(sdk.Content[5]);
        Assert.Equal("test://link", resourceLink.Uri);
        Assert.Equal("link", resourceLink.Name);
        Assert.Equal("Link title", resourceLink.Title);
        Assert.Equal("A linked resource", resourceLink.Description);
        Assert.Equal("text/plain", resourceLink.MimeType);
        Assert.Equal(42, resourceLink.Size);
        Assert.Equal(1, resourceLink.Meta!["link"]!.GetValue<int>());
    }

    [Fact]
    public void PythonResultParser_PreservesNativeSdkResponseSemantics()
    {
        var resource = BlobResourceContents.FromBytes(new byte[] { 8, 9 }, "test://python", "application/octet-stream");
        resource.Meta = new JsonObject { ["resource"] = "meta" };
        var expected = new CallToolResult
        {
            IsError = true,
            StructuredContent = JsonDocument.Parse("{\"ok\":false}").RootElement.Clone(),
            Meta = new JsonObject { ["response"] = "meta" },
            Content =
            [
                new TextContentBlock { Text = "failure", Meta = new JsonObject { ["content"] = "meta" } },
                ImageContentBlock.FromBytes(new byte[] { 1, 2 }, "image/png"),
                new EmbeddedResourceBlock { Resource = resource }
            ]
        };

        var actual = PythonMcpToolBackend.ReadToolResult(JsonSerializer.Serialize(expected, ModelContextProtocol.McpJsonUtilities.DefaultOptions));

        Assert.True(actual.IsError);
        Assert.Equal("meta", actual.Meta!["response"]!.GetValue<string>());
        Assert.Equal("{\"ok\":false}", actual.StructuredContent!.Value.GetRawText());
        Assert.Equal("meta", actual.Content[0].Meta!["content"]!.GetValue<string>());
        Assert.Equal(new byte[] { 1, 2 }, ((ImageContentBlock)actual.Content[1]).DecodedData.ToArray());
        var blob = Assert.IsType<BlobResourceContents>(((EmbeddedResourceBlock)actual.Content[2]).Resource);
        Assert.Equal("test://python", blob.Uri);
        Assert.Equal("application/octet-stream", blob.MimeType);
        Assert.Equal(new byte[] { 8, 9 }, blob.DecodedData.ToArray());
        Assert.Equal("meta", blob.Meta!["resource"]!.GetValue<string>());
    }

    [Fact]
    public void McpPrimitiveBinding_CreatePrimitiveId_ForResources()
    {
        var resourceId = McpPrimitiveBinding.CreatePrimitiveId("demo_view", "sample.dll:McpToolsetDemo.McpSampleResources.DemoView");

        Assert.Equal("demo_view_[sample.dll:McpToolsetDemo.McpSampleResources.DemoView]", resourceId);
    }

    [Fact]
    public void McpPrimitiveBinding_CreatePrimitiveId_HandlesNullAndEmpty()
    {
        var id = McpPrimitiveBinding.CreatePrimitiveId(null, null);
        Assert.Equal("unknown_[unknown]", id);

        var idWithName = McpPrimitiveBinding.CreatePrimitiveId("tool", null);
        Assert.Equal("tool_[unknown]", idWithName);
    }
}
