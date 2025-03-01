using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json.Linq;

namespace Newtonsoft.Json.Converters;

/// <summary>
/// Converts values to the <see cref="StringValues"/> type.
/// </summary>
sealed class StringValuesConverter : JsonConverter
{
	/// <inheritdoc/>
	public override bool CanConvert(Type objectType)
		=> objectType == typeof(StringValues);

	/// <inheritdoc/>
	public override object? ReadJson([NotNull] JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
	{
		if (reader!.TokenType == JsonToken.StartArray)
		{
			var stringValues = JArray.Load(reader);
			return new StringValues(stringValues.ToObject<string[]>());
		}
		else if (reader.TokenType == JsonToken.String)
		{
			var value = JToken.Load(reader);
			return new StringValues(value.Value<string>());
		}

		return new StringValues();
	}

	/// <inheritdoc/>
	public override void WriteJson([NotNull] JsonWriter writer, object? value, JsonSerializer serializer)
	{
		var stringValues = (StringValues)value!;
		if (stringValues.Count == 0)
			writer!.WriteNull();
		else if (stringValues.Count == 1)
		{
			var singularStringValue = (string)stringValues!;
			writer.WriteValue(singularStringValue);
		}
		else
		{
			writer.WriteStartArray();

			for (var i = 0; i < stringValues.Count; i++)
				writer.WriteValue(stringValues[i]);

			writer.WriteEndArray();
		}
	}
}
