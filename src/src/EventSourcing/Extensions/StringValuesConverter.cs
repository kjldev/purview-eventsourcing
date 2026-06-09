using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Primitives;

namespace Purview.EventSourcing;

/// <summary>
/// Converts values to the <see cref="StringValues"/> type.
/// </summary>
sealed class StringValuesConverter : JsonConverter<StringValues>
{
	/// <inheritdoc/>
	public override StringValues Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType == JsonTokenType.StartArray)
		{
			var values = JsonSerializer.Deserialize<string[]>(ref reader, options) ?? [];
			return new(values);
		}

		return reader.TokenType == JsonTokenType.String ? new(reader.GetString())
			: reader.TokenType == JsonTokenType.Null ? new StringValues()
			: throw new JsonException(
				$"Unable to deserialize {nameof(StringValues)} from token type {reader.TokenType}."
			);
	}

	/// <inheritdoc/>
	public override void Write(Utf8JsonWriter writer, StringValues value, JsonSerializerOptions options)
	{
		if (value.Count == 0)
			writer.WriteNullValue();
		else if (value.Count == 1)
		{
			writer.WriteStringValue(value[0]);
		}
		else
		{
			JsonSerializer.Serialize(writer, value.ToArray(), options);
		}
	}
}
