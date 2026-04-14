using System.Data.Common;
using Microsoft.Data.SqlClient;
using Purview.EventSourcing.Aggregates;
using Purview.EventSourcing.Internal;

namespace Purview.EventSourcing.SqlServer.Snapshot;

partial class SqlServerSnapshotEventStore<T>
{
	public Task<T> CreateAsync(string? aggregateId = null, CancellationToken cancellationToken = default) =>
		_eventStore.CreateAsync(aggregateId, cancellationToken);

	public Task<T?> GetOrCreateAsync(
		string? aggregateId,
		EventStoreOperationContext? operationContext,
		CancellationToken cancellationToken = default
	) => _eventStore.GetOrCreateAsync(aggregateId, operationContext, cancellationToken);

	public Task<T?> GetAsync(
		string aggregateId,
		EventStoreOperationContext? operationContext,
		CancellationToken cancellationToken = default
	) => _eventStore.GetAsync(aggregateId, operationContext, cancellationToken);

	public Task<T?> GetAtAsync(
		string aggregateId,
		int version,
		EventStoreOperationContext? operationContext,
		CancellationToken cancellationToken = default
	) => _eventStore.GetAtAsync(aggregateId, version, operationContext, cancellationToken);

	public async Task<SaveResult<T>> SaveAsync(
		T aggregate,
		EventStoreOperationContext? operationContext,
		CancellationToken cancellationToken = default
	)
	{
		ArgumentNullException.ThrowIfNull(aggregate, nameof(aggregate));

		var result = await _eventStore.SaveAsync(aggregate, operationContext, cancellationToken);
		if (result)
			await SnapshotAsync(aggregate, cancellationToken);

		return result;
	}

	string ITransactionalEventStore<T>.TransactionBoundaryKey =>
		_eventStore is ITransactionalEventStore<T> transactionalEventStore
			? transactionalEventStore.TransactionBoundaryKey
			: string.Empty;

	DbConnection ITransactionalEventStore<T>.CreateTransactionConnection()
	{
		if (_eventStore is ITransactionalEventStore<T> transactionalEventStore)
			return transactionalEventStore.CreateTransactionConnection();

		return new SqlConnection(_sqlServerEventStoreOptions.Value.ConnectionString);
	}

	async Task ITransactionalEventStore<T>.EnsureTransactionConfiguredAsync(
		DbConnection connection,
		CancellationToken cancellationToken
	)
	{
		if (_eventStore is not ITransactionalEventStore<T> transactionalEventStore)
			throw new InvalidOperationException("The inner event store does not support transactional saves.");

		await transactionalEventStore.EnsureTransactionConfiguredAsync(connection, cancellationToken);
		await _sqlServerClient.EnsureTableExistsAsync(GetSqlConnection(connection), cancellationToken);
	}

	async Task<TransactionalSaveOperation<T>> ITransactionalEventStore<T>.SaveInTransactionAsync(
		T aggregate,
		EventStoreOperationContext? operationContext,
		DbConnection connection,
		DbTransaction transaction,
		CancellationToken cancellationToken
	)
	{
		ArgumentNullException.ThrowIfNull(aggregate, nameof(aggregate));

		if (_eventStore is not ITransactionalEventStore<T> transactionalEventStore)
			throw new InvalidOperationException("The inner event store does not support transactional saves.");

		var innerOperation = await transactionalEventStore.SaveInTransactionAsync(
			aggregate,
			operationContext,
			connection,
			transaction,
			cancellationToken
		);

		try
		{
			if (innerOperation.Result.Saved)
			{
				var snapshotSaved = await _sqlServerClient.UpsertAsync(
					aggregate,
					aggregate.Details.Id,
					GetAggregateTypeName(),
					GetSqlConnection(connection),
					GetSqlTransaction(transaction),
					cancellationToken
				);

				if (!snapshotSaved)
					throw new InvalidOperationException("Failed to persist the SQL Server query snapshot.");
			}

			return new TransactionalSaveOperation<T>(
				innerOperation.Result,
				innerOperation.AfterCommitAsync,
				innerOperation.AfterRollbackAsync
			);
		}
		catch
		{
			await innerOperation.AfterRollbackAsync(cancellationToken);
			throw;
		}
	}

	public Task<bool> IsDeletedAsync(string aggregateId, CancellationToken cancellationToken = default) =>
		_eventStore.IsDeletedAsync(aggregateId, cancellationToken);

	public Task<T?> GetDeletedAsync(string aggregateId, CancellationToken cancellationToken = default) =>
		_eventStore.GetDeletedAsync(aggregateId, cancellationToken);

	public async Task<bool> DeleteAsync(
		T aggregate,
		EventStoreOperationContext? operationContext,
		CancellationToken cancellationToken = default
	)
	{
		ArgumentNullException.ThrowIfNull(aggregate, nameof(aggregate));

		_telemetry.SnapshotDeleteStart(aggregate.Details.Id, _aggregateName);
		using var activity = _telemetry.SnapshotDelete(aggregate.Details.Id, _aggregateName);

		try
		{
			var result = await _eventStore.DeleteAsync(aggregate, operationContext, cancellationToken);
			if (result)
			{
				await _sqlServerClient.DeleteAsync(aggregate.Details.Id, cancellationToken);
				_telemetry.SnapshotDeleted(_aggregateName);
				_telemetry.SnapshotDeleteComplete(aggregate.Details.Id, _aggregateName);
			}

			return result;
		}
		catch (Exception ex)
		{
			_telemetry.SnapshotDeleteFailed(aggregate.Details.Id, _aggregateName, ex);
			throw;
		}
	}

	public async Task<bool> RestoreAsync(
		T aggregate,
		EventStoreOperationContext? operationContext,
		CancellationToken cancellationToken = default
	)
	{
		ArgumentNullException.ThrowIfNull(aggregate, nameof(aggregate));

		var result = await _eventStore.RestoreAsync(aggregate, operationContext, cancellationToken);
		if (result)
			await SnapshotAsync(aggregate, cancellationToken);

		return result;
	}

	public IAsyncEnumerable<string> GetAggregateIdsAsync(
		bool includeDeleted,
		CancellationToken cancellationToken = default
	) => _eventStore.GetAggregateIdsAsync(includeDeleted, cancellationToken);

	public Task<ExistsState> ExistsAsync(string aggregateId, CancellationToken cancellationToken = default) =>
		_eventStore.ExistsAsync(aggregateId, cancellationToken);

	public T FulfilRequirements(T aggregate) => _eventStore.FulfilRequirements(aggregate);

	static SqlConnection GetSqlConnection(DbConnection connection) =>
		connection as SqlConnection
		?? throw new InvalidOperationException("SQL Server transactions require a SqlConnection.");

	static SqlTransaction GetSqlTransaction(DbTransaction transaction) =>
		transaction as SqlTransaction
		?? throw new InvalidOperationException("SQL Server transactions require a SqlTransaction.");
}
