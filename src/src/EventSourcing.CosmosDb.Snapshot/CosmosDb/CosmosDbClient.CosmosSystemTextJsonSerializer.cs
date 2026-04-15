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
				return JsonHelpers.DeserializeAsync<T>(stream).AsTask().GetAwaiter().GetResult()!;
		}

		public override Stream ToStream<T>(T input)
		{
			var streamPayload = new MemoryStream();
			JsonHelpers.SerializeAsync(streamPayload, input, typeof(T)).GetAwaiter().GetResult();

			streamPayload.Position = 0;
			return streamPayload;
		}
	}
}
