using System.Text;
using System.Text.Json;

namespace RevitDevTool.Scintilla.Benchmarks.Scenarios;

/// <summary>
/// Order data for structured logging benchmarks
/// </summary>
public sealed class OrderData
{
    public long OrderId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string UserId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string[] Items { get; set; } = Array.Empty<string>();
}

public static class ScenarioDataFactory
{
    private static readonly DateTime BaseTimestampUtc = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static IReadOnlyList<string> BuildMessages(
        int count,
        int sizeBytes,
        TokenDensity tokenDensity,
        bool structuredPayload,
        int seed = 12345)
    {
        var random = new Random(seed);
        var list = new List<string>(count);
        for (var i = 0; i < count; i++)
            list.Add(BuildSingleMessage(i, sizeBytes, tokenDensity, structuredPayload, random));
        return list;
    }

    public static IReadOnlyList<byte[]> BuildUtf8Messages(IReadOnlyList<string> messages)
    {
        var list = new List<byte[]>(messages.Count);
        for (var i = 0; i < messages.Count; i++)
            list.Add(Encoding.UTF8.GetBytes(messages[i]));
        return list;
    }

    /// <summary>
    /// Build order data for structured logging benchmarks
    /// </summary>
    public static IReadOnlyList<OrderData> BuildOrderData(int count, int seed = 54321)
    {
        var random = new Random(seed);
        var list = new List<OrderData>(count);
        var statuses = new[] { "Created", "Processing", "Shipped", "Delivered", "Cancelled" };
        var users = new[] { "user001", "user002", "user003", "user004", "user005" };

        for (var i = 0; i < count; i++)
        {
            list.Add(new OrderData
            {
                OrderId = 10000 + i,
                Status = statuses[i % statuses.Length],
                Timestamp = BaseTimestampUtc.AddSeconds(i),
                UserId = users[i % users.Length],
                Amount = ((i + 1) * 9.99m) + random.Next(0, 25),
                Items = new[]
                {
                    $"item-{i:D5}",
                    $"item-{(i + 1):D5}",
                    $"item-{(i + 2):D5}"
                }
            });
        }

        return list;
    }

    private static string BuildSingleMessage(
        int index,
        int sizeBytes,
        TokenDensity tokenDensity,
        bool structuredPayload,
        Random random)
    {
        var timestamp = BaseTimestampUtc.AddSeconds(index);
        var payload = structuredPayload
            ? JsonSerializer.Serialize(new
            {
                id = index,
                source = "bench",
                level = "INF",
                elapsedMs = (index % 200) + 1,
                token = "ORD-12345",
                uri = "https://example.com/revit/docs?x=1&y=2",
                guid = CreateDeterministicGuid(random).ToString("N")
            })
            : "simple-message";

        var tokenPart = tokenDensity switch
        {
            TokenDensity.None => "plain",
            TokenDensity.Medium => "WRN threshold=1024 ORD-12345",
            _ => "WRN threshold=1024 ERR E500 ORD-12345 https://example.com/revit/docs IfcGuid=3H7dK2oQfA5Q9v7cYx2mQa"
        };

        var baseText = $"[{timestamp:HH:mm:ss} INF] {tokenPart} idx={index} payload={payload}";
        if (Encoding.UTF8.GetByteCount(baseText) >= sizeBytes)
            return baseText;

        var builder = new StringBuilder(baseText, sizeBytes + 32);
        while (Encoding.UTF8.GetByteCount(builder.ToString()) < sizeBytes)
            builder.Append(" filler-token");
        return builder.ToString();
    }

    private static Guid CreateDeterministicGuid(Random random)
    {
        var bytes = new byte[16];
        random.NextBytes(bytes);
        return new Guid(bytes);
    }
}
