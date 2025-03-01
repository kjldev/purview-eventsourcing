using System.Diagnostics;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;

namespace Purview.EventSourcing.MongoDB.Entities;

[DebuggerStepThrough]
public sealed class StreamVersionEntity : IEntity
{
	[BsonId]
	[JsonProperty("id")]
	public string Id { get; set; } = default!;

	public string AggregateId { get; set; } = default!;

	public int EntityType { get; set; } = EntityTypes.StreamVersionType;

	public bool IsDeleted { get; set; }

	public string AggregateType { get; set; } = default!;

	/// <summary>
	/// This is the most recently saved version of the aggregate.
	/// </summary>
	public int Version { get; set; }

	public DateTimeOffset? Timestamp { get; set; }
}
