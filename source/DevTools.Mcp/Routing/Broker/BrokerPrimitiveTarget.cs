namespace DevTools.Mcp.Routing.Broker;

public sealed record BrokerPrimitiveTarget(BrokerPrimitiveKind Kind, string Key)
{
    public static BrokerPrimitiveTarget Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Broker target is required.", nameof(value));

        var separator = value.IndexOf(':');
        if (separator <= 0 || separator == value.Length - 1)
            throw new ArgumentException("Broker target must include a supported kind and a non-empty key.", nameof(value));

        var kind = value[..separator] switch
        {
            "tool" => BrokerPrimitiveKind.Tool,
            "resource" => BrokerPrimitiveKind.Resource,
            "prompt" => BrokerPrimitiveKind.Prompt,
            _ => throw new ArgumentException("Broker target kind must be tool, resource, or prompt.", nameof(value))
        };
        var key = value[(separator + 1)..];
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Broker target key is required.", nameof(value));

        return new BrokerPrimitiveTarget(kind, key);
    }

    public override string ToString() => $"{Kind.ToString().ToLowerInvariant()}:{Key}";
}
