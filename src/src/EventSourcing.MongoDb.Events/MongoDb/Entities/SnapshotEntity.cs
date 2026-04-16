using System.Diagnostics;
using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization.Attributes;

namespace Purview.EventSourcing.MongoDB.Entities;

[DebuggerStepThrough]
public sealed class SnapshotEntity : IEntity
{
	[BsonId]
	[JsonPropertyName("id")]
	public string Id { get; set; } = default!;

	[BsonIgnore]
	public string AggregateId
	{
		get => Id;
		set => Id = value;
	}

	public int EntityType { get; set; } = EntityTypes.SnapshotType;

	public string AggregateType { get; set; } = default!;

	public string AggregateFullType { get; set; } = default!;

	public DateTimeOffset? Timestamp { get; set; }

	public string Payload { get; set; } = default!;
}
