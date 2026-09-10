using System.Text.Json;
using System.Text.Json.Serialization;
using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.Testing.Transport;

public sealed class TestFrameworkIdJsonConverter : JsonConverter<TestFrameworkId>
{
    public override TestFrameworkId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException("framework_id must be a string.");

        var text = reader.GetString();
        if (string.IsNullOrWhiteSpace(text)
            || !Enum.TryParse(text, ignoreCase: true, out TestFrameworkId id)
            || !Enum.IsDefined(id))
        {
            throw new JsonException("framework_id must be NUnit or TUnit.");
        }

        return id;
    }

    public override void Write(Utf8JsonWriter writer, TestFrameworkId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}
