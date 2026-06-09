using Microsoft.Azure.Cosmos;

namespace Purview.EventSourcing.CosmosDb;

partial class CosmosDbClient
{
	public sealed class CosmosSystemTextJsonSerializer : CosmosSerializer
	{
		public override T FromStream<T>(Stream stream)
		{
			if (typeof(Stream).IsAssignableFrom(typeof(T)))
				return (T)(object)stream;

			using (stream)
				return System.Text.Json.JsonSerializer.Deserialize<T>(stream)!;
		}

		public override Stream ToStream<T>(T input)
		{
			var streamPayload = new MemoryStream();
			System.Text.Json.JsonSerializer.Serialize(streamPayload, input);

			streamPayload.Position = 0;
			return streamPayload;
		}
	}
}
