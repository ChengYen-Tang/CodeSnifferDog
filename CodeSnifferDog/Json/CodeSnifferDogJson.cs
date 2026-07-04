using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CodeSnifferDog.Json;

/// <summary>
/// Centralizes JSON serialization settings used across the project.
/// </summary>
public static class CodeSnifferDogJson
{
    /// <summary>
    /// Gets JSON serializer options that use the web defaults and relaxed escaping.
    /// </summary>
    public static JsonSerializerOptions UnsafeRelaxedOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Serializes a value with the shared JSON settings.
    /// </summary>
    /// <typeparam name="TValue">Value type to serialize.</typeparam>
    /// <param name="value">Value to serialize.</param>
    /// <returns>The serialized JSON string.</returns>
    public static string Serialize<TValue>(TValue value) =>
        JsonSerializer.Serialize(value, UnsafeRelaxedOptions);

    /// <summary>
    /// Serializes a value with an explicit runtime type and the shared JSON settings.
    /// </summary>
    /// <param name="value">Value to serialize.</param>
    /// <param name="inputType">Runtime type used for serialization.</param>
    /// <returns>The serialized JSON string.</returns>
    public static string Serialize(object? value, Type inputType) =>
        JsonSerializer.Serialize(value, inputType, UnsafeRelaxedOptions);

    /// <summary>
    /// Serializes a <see cref="JsonNode"/> with the shared JSON settings.
    /// </summary>
    /// <param name="value">JSON node to serialize.</param>
    /// <returns>The serialized JSON string.</returns>
    public static string ToJsonString(JsonNode value) =>
        value.ToJsonString(UnsafeRelaxedOptions);
}
