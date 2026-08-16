using System.Text.Json.Serialization;
using DevTools.Hosting;

namespace DevTools.FileMetadata.Core;

public enum FileInfoDetail
{
    Summary,
    Full
}

public sealed record FileInfoRequest(string FilePath, FileInfoDetail Detail);

public abstract class FileInfoResult
{
    [JsonPropertyName("hostApp")]
    [JsonConverter(typeof(JsonStringEnumConverter<HostApp>))]
    public required HostApp HostApplication { get; init; }
    public required string FilePath { get; init; }
    public required string FileName { get; init; }
}

public interface IFileReader
{
    IReadOnlyList<string> SupportedExtensions { get; }
    FileInfoResult Read(FileInfoRequest request);
}

public interface IFileReaderCatalog
{
    IFileReader GetReader(string filePath);
    string FormatSupportedExtensions();
}

public sealed class FileReaderCatalog(IEnumerable<IFileReader> readers) : IFileReaderCatalog
{
    private readonly IFileReader[] _readers = readers.ToArray();

    public IFileReader GetReader(string filePath) =>
        _readers.FirstOrDefault(reader => reader.SupportedExtensions.Contains(
            Path.GetExtension(filePath), StringComparer.OrdinalIgnoreCase))
        ?? throw new FileReadException(
            FileError.UnsupportedFormat,
            $"Unsupported file type: '{Path.GetExtension(filePath)}'.");

    public string FormatSupportedExtensions() =>
        string.Join(", ", _readers.SelectMany(reader => reader.SupportedExtensions)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(extension => extension, StringComparer.OrdinalIgnoreCase));
}

public static class FileError
{
    public const string UnsupportedFormat = "file.unsupported_format";
    public const string InvalidFile = "file.invalid";
    public const string ReadFailed = "file.read_failed";
}

public sealed class FileReadException(string error, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public string Error { get; } = error;
}
