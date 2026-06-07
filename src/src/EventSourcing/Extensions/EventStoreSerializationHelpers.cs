using System.Text.Json;
using System.Text.Json.Serialization;

namespace Purview.EventSourcing;

public static class EventStoreSerializationHelpers
{
    public static JsonSerializerOptions JsonSerializerOptions { get; } =
        CreateJsonSerializerOptions();

    public static JsonSerializerOptions CreateJsonSerializerOptions()
    {
        JsonSerializerOptions options = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate,
            WriteIndented = false,
        };

        options.Converters.Add(new StringValuesConverter());
        return options;
    }

    public static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, JsonSerializerOptions);

    public static string Serialize(object? value, Type inputType) =>
        JsonSerializer.Serialize(value, inputType, JsonSerializerOptions);

    public static T? Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, JsonSerializerOptions);

    public static object? Deserialize(string json, Type returnType) =>
        JsonSerializer.Deserialize(json, returnType, JsonSerializerOptions);

    public static ValueTask<T?> DeserializeAsync<T>(
        Stream utf8Json,
        CancellationToken cancellationToken = default
    ) => JsonSerializer.DeserializeAsync<T>(utf8Json, JsonSerializerOptions, cancellationToken);

    public static ValueTask<object?> DeserializeAsync(
        Stream utf8Json,
        Type returnType,
        CancellationToken cancellationToken = default
    ) =>
        JsonSerializer.DeserializeAsync(
            utf8Json,
            returnType,
            JsonSerializerOptions,
            cancellationToken
        );

    public static Task SerializeAsync<T>(
        Stream utf8Json,
        T value,
        CancellationToken cancellationToken = default
    ) => JsonSerializer.SerializeAsync(utf8Json, value, JsonSerializerOptions, cancellationToken);

    public static Task SerializeAsync(
        Stream utf8Json,
        object? value,
        Type inputType,
        CancellationToken cancellationToken = default
    ) =>
        JsonSerializer.SerializeAsync(
            utf8Json,
            value,
            inputType,
            JsonSerializerOptions,
            cancellationToken
        );
}
