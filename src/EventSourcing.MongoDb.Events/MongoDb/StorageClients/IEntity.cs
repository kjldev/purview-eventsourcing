namespace Purview.EventSourcing.MongoDB.Entities;

interface IEntity
{
	string Id { get; set; }

	string AggregateId { get; set; }

	int EntityType { get; }
}
