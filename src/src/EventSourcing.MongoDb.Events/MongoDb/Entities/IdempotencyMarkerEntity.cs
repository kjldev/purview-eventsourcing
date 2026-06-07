using System.Diagnostics;
using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization.Attributes;

namespace Purview.EventSourcing.MongoDB.Entities;

[DebuggerStepThrough]
public sealed class IdempotencyMarkerEntity : IEntity
{
    [BsonId]
    [JsonPropertyName("id")]
    public string Id { get; set; } = default!;

    public int EntityType { get; set; } = EntityTypes.IdempotencyMarkerType;

    public string AggregateId { get; set; } = default!;

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1819:Properties should not return arrays",
        Justification = "This is a DTO."
    )]
    public int[] EventVersions { get; set; } = [];

    public DateTimeOffset? Timestamp { get; set; }
}
