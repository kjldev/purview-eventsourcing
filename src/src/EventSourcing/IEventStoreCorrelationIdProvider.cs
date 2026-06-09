using System.Diagnostics;

namespace Purview.EventSourcing;

/// <summary>
/// Provides ambient correlation IDs for logical event-store transactions.
/// </summary>
public interface IEventStoreCorrelationIdProvider
{
	/// <summary>
	/// Gets the current correlation ID, or <see langword="null"/> when none is available.
	/// </summary>
	string? GetCorrelationId();
}

sealed class ActivityEventStoreCorrelationIdProvider : IEventStoreCorrelationIdProvider
{
	public string? GetCorrelationId() => Activity.Current?.Id;
}
