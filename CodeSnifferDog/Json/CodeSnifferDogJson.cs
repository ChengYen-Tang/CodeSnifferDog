using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CodeSnifferDog.Json;

public static class CodeSnifferDogJson
{
    public static JsonSerializerOptions UnsafeRelaxedOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string Serialize<TValue>(TValue value) =>
        JsonSerializer.Serialize(value, UnsafeRelaxedOptions);

    public static string Serialize(object? value, Type inputType) =>
        JsonSerializer.Serialize(value, inputType, UnsafeRelaxedOptions);

    public static string ToJsonString(JsonNode value) =>
        value.ToJsonString(UnsafeRelaxedOptions);
}
