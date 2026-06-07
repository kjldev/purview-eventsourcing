using System.Diagnostics;
using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization.Attributes;

namespace Purview.EventSourcing.MongoDB.Entities;

[DebuggerStepThrough]
public sealed class EventEntity : IEntity
{
    [BsonId]
    [JsonPropertyName("id")]
    public string Id { get; set; } = default!;

    public int EntityType { get; set; } = EntityTypes.EventType;

    public string AggregateId { get; set; } = default!;

    public int Version { get; set; } = default!;

    public string Payload { get; set; } = default!;

    public string EventType { get; set; } = default!;

    public string IdempotencyId { get; set; } = default!;

    public DateTimeOffset? Timestamp { get; set; }
}
