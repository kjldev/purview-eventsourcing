using Purview.EventSourcing.Aggregates;

namespace Purview.EventSourcing.Services;

/// <summary>
/// Generates new Ids for <see cref="IAggregate"/>s.
/// </summary>
public interface IAggregateIdFactory
{
	/// <summary>
	/// <para>Generates a new id for the <see cref="IAggregate"/> of type <typeparamref name="T"/>.</para>
	/// <para>The implementation has the opportunity to check existing stores or lookup ids from an external source.</para>
	/// </summary>
	/// <typeparam name="T">The type of the <see cref="IAggregate"/>.</typeparam>
	/// <param name="cancellationToken">A stopping token.</param>
	/// <returns>A new, ideally unique, id.</returns>
	Task<string> CreateAsync<T>(CancellationToken cancellationToken = default)
		where T : IAggregate;
}
