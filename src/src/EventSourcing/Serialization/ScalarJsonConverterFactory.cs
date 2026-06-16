using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Purview.EventSourcing.Serialization;

public sealed class ScalarJsonConverterFactory : JsonConverterFactory
{
	static readonly ConcurrentDictionary<Type, JsonConverter> Cache = new();

	public override bool CanConvert(Type typeToConvert) =>
		typeToConvert.GetCustomAttribute<ScalarAttribute>() is not null;

	public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
		Cache.GetOrAdd(
			typeToConvert,
			static t =>
			{
				var attr = t.GetCustomAttribute<ScalarAttribute>()!;
				var scalarProp =
					t.GetProperty(attr.PropertyName, BindingFlags.Instance | BindingFlags.Public)
					?? throw new InvalidOperationException(
						$"'{t.Name}' missing scalar property '{attr.PropertyName}'."
					);

				var converterType = typeof(ScalarJsonConverter<,>).MakeGenericType(t, scalarProp.PropertyType);
				return (JsonConverter)Activator.CreateInstance(converterType, scalarProp, attr.DeserializationMode)!;
			}
		);

	sealed class ScalarJsonConverter<TScalarObject, TScalar>(
		PropertyInfo scalarProperty,
		ValueObjectDeserializationMode deserializationMode
	) : JsonConverter<TScalarObject>
	{
		readonly Func<TScalarObject, TScalar> _getScalar = BuildGetter(scalarProperty);
		readonly Func<TScalar, TScalarObject> _create = BuildCreator(deserializationMode);

		public override TScalarObject Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			var scalar = JsonSerializer.Deserialize<TScalar>(ref reader, options);
			return scalar is null
				? throw new JsonException($"Cannot deserialize {typeof(TScalarObject).Name} from null.")
				: _create(scalar);
		}

		public override void Write(Utf8JsonWriter writer, TScalarObject value, JsonSerializerOptions options) =>
			JsonSerializer.Serialize(writer, _getScalar(value), options);

		static Func<TScalarObject, TScalar> BuildGetter(PropertyInfo property)
		{
			var obj = Expression.Parameter(typeof(TScalarObject), "x");
			var body = Expression.Property(obj, property);
			return Expression.Lambda<Func<TScalarObject, TScalar>>(body, obj).Compile();
		}

		static Func<TScalar, TScalarObject> BuildCreator(ValueObjectDeserializationMode deserializationMode)
		{
			var t = typeof(TScalarObject);
			var preferredFactoryName =
				deserializationMode == ValueObjectDeserializationMode.Strict ? "Create" : "Hydrate";
			var secondaryFactoryName = preferredFactoryName == "Hydrate" ? "Create" : "Hydrate";

			var create =
				t.GetMethod(preferredFactoryName, BindingFlags.Public | BindingFlags.Static, [typeof(TScalar)])
				?? t.GetMethod(secondaryFactoryName, BindingFlags.Public | BindingFlags.Static, [typeof(TScalar)]);
			if (create is not null)
			{
				var p = Expression.Parameter(typeof(TScalar), "v");
				return Expression.Lambda<Func<TScalar, TScalarObject>>(Expression.Call(create, p), p).Compile();
			}

			// Fallback: ctor(TScalar) (public or non-public)
			var ctor = t.GetConstructor(
				BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
				[typeof(TScalar)]
			);
			if (ctor is not null)
			{
				var p = Expression.Parameter(typeof(TScalar), "v");
				return Expression.Lambda<Func<TScalar, TScalarObject>>(Expression.New(ctor, p), p).Compile();
			}

			throw new InvalidOperationException(
				$"{t.Name} must expose static {preferredFactoryName}({typeof(TScalar).Name}), static {secondaryFactoryName}({typeof(TScalar).Name}), or ctor({typeof(TScalar).Name})."
			);
		}
	}
}
