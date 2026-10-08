using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace DevTools.Daemon.Mcp.Code;

/// <summary>Maps a program return value onto the outer MCP <see cref="CallToolResult"/>.</summary>
public static class ProgramResult
{
    public const int MaxBytes = 1024 * 1024;

    /// <summary>
    /// Return table:
    /// <see cref="CallToolResult"/> passes through.
    /// One <see cref="ContentBlock"/>, or a list of them, becomes <see cref="CallToolResult.Content"/>.
    /// <see cref="ReadResourceResult"/> becomes one <see cref="EmbeddedResourceBlock"/> per content.
    /// <see cref="string"/> becomes one <see cref="TextContentBlock"/>.
    /// Any other object becomes one JSON <see cref="TextContentBlock"/>. <c>code_mode</c> does not advertise <c>outputSchema</c>, so <see cref="CallToolResult.StructuredContent"/> stays unset.
    /// Payloads over <see cref="MaxBytes"/> (base64 counted as stored) become an error with no payload.
    /// </summary>
    public static CallToolResult ToCallToolResult(object? value)
    {
        var mapped = Map(value);
        if (CountBytes(mapped) > MaxBytes)
        {
            return new CallToolResult
            {
                IsError = true,
                Content = [new TextContentBlock { Text = "Program result exceeds 1 MiB." }],
            };
        }

        return mapped;
    }

    private static CallToolResult Map(object? value) => value switch
    {
        null => new CallToolResult(),
        CallToolResult call => call,
        ContentBlock block => new CallToolResult { Content = [block] },
        IEnumerable<ContentBlock> blocks => new CallToolResult { Content = blocks.ToList() },
        ReadResourceResult read => FromResource(read),
        string text => new CallToolResult { Content = [new TextContentBlock { Text = text }] },
        _ => FromObject(value),
    };

    private static CallToolResult FromResource(ReadResourceResult read)
    {
        var content = new List<ContentBlock>();
        if (read.Contents is not null)
        {
            foreach (var item in read.Contents)
                content.Add(new EmbeddedResourceBlock { Resource = item });
        }

        return new CallToolResult { Content = content };
    }

    private static CallToolResult FromObject(object value)
    {
        var json = JsonSerializer.SerializeToElement(value);
        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = json.GetRawText() }],
        };
    }

    private static int CountBytes(CallToolResult result)
    {
        var total = 0;
        if (result.Content is not null)
        {
            foreach (var block in result.Content)
                total += BlockBytes(block);
        }

        if (result.StructuredContent is { } structured)
            total += JsonSerializer.SerializeToUtf8Bytes(structured).Length;

        return total;
    }

    private static int BlockBytes(ContentBlock block) => block switch
    {
        TextContentBlock text => Utf8(text.Text),
        ImageContentBlock image => Base64Bytes(image.Data) + Utf8(image.MimeType),
        AudioContentBlock audio => Base64Bytes(audio.Data) + Utf8(audio.MimeType),
        EmbeddedResourceBlock embedded => ResourceBytes(embedded.Resource),
        ResourceLinkBlock link => Utf8(link.Uri) + Utf8(link.Name) + Utf8(link.Description),
        _ => Utf8(JsonSerializer.Serialize(block)),
    };

    private static int ResourceBytes(ResourceContents? resource) => resource switch
    {
        null => 0,
        TextResourceContents text => Utf8(text.Text) + Utf8(text.Uri) + Utf8(text.MimeType),
        BlobResourceContents blob => Base64Bytes(blob.Blob) + Utf8(blob.Uri) + Utf8(blob.MimeType),
        _ => Utf8(JsonSerializer.Serialize(resource)),
    };

    private static int Utf8(string? value) =>
        value is null ? 0 : System.Text.Encoding.UTF8.GetByteCount(value);

    private static int Base64Bytes(ReadOnlyMemory<byte> data)
    {
        var length = data.Length;
        if (length == 0)
            return 0;

        return ((length + 2) / 3) * 4;
    }
}
