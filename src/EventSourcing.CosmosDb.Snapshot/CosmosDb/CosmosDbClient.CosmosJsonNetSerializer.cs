using System.Text;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;

namespace Purview.EventSourcing.CosmosDb;

partial class CosmosDbClient
{
	sealed public class CosmosJsonNetSerializer : CosmosSerializer
	{
		static readonly Encoding DefaultEncoding = new UTF8Encoding(false, true);

		readonly JsonSerializer _serializer;
		readonly JsonSerializerSettings _serializerSettings;

		public CosmosJsonNetSerializer()
			: this(new JsonSerializerSettings())
		{
		}

		public CosmosJsonNetSerializer(JsonSerializerSettings serializerSettings)
		{
			_serializerSettings = serializerSettings;
			_serializer = JsonSerializer.Create(_serializerSettings);
		}

		public override T FromStream<T>(Stream stream)
		{
			if (typeof(Stream).IsAssignableFrom(typeof(T)))
				return (T)(object)stream;

			using (stream)
			{
				using var sr = new StreamReader(stream);
				using var jsonTextReader = new JsonTextReader(sr);

				return _serializer.Deserialize<T>(jsonTextReader)!;
			}
		}

		public override Stream ToStream<T>(T input)
		{
			var streamPayload = new MemoryStream();
			using (var streamWriter = new StreamWriter(streamPayload, encoding: DefaultEncoding, bufferSize: 2048, leaveOpen: true))
			{
				using JsonWriter writer = new JsonTextWriter(streamWriter)
				{
					Formatting = Formatting.None
				};
				_serializer.Serialize(writer, input);

				writer.Flush();
				streamWriter.Flush();
			}

			streamPayload.Position = 0;
			return streamPayload;
		}
	}
}
