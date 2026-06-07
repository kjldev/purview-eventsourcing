using System.Linq.Expressions;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace Purview.EventSourcing.MongoDB.StorageClients;

partial class MongoDBClient
{
    public async Task SubmitBatchAsync(
        BatchOperation operation,
        CancellationToken cancellationToken = default
    )
    {
        var collection = GetCollection<BsonDocument>().WithWriteConcern(WriteConcern.WMajority);
        using var session = await _client.StartSessionAsync(cancellationToken: cancellationToken);

        TransactionOptions transactionOptions = new(writeConcern: WriteConcern.WMajority);
        await session.WithTransactionAsync(
            async (s, ct) =>
            {
                try
                {
                    foreach (var op in operation.GetActions())
                    {
                        switch (op.ActionType)
                        {
                            case TransactionActionType.Insert:
                                await collection.InsertOneAsync(
                                    session,
                                    op.Document.ToBsonDocument(),
                                    cancellationToken: cancellationToken
                                );
                                break;
                            case TransactionActionType.Update:
                                var d = op.Document.ToBsonDocument();
                                d.Remove("_id");

                                await collection.ReplaceOneAsync(
                                    session,
                                    m => m["_id"] == op.Document.Id,
                                    d,
                                    new ReplaceOptions { IsUpsert = false },
                                    cancellationToken
                                );

                                break;
                            case TransactionActionType.Delete:
                                await collection.DeleteOneAsync(
                                    session,
                                    m => m["_id"] == op.Document.Id,
                                    cancellationToken: cancellationToken
                                );
                                break;
                        }
                    }

                    await s.CommitTransactionAsync(ct);
                }
                catch (MongoWriteException)
                {
                    await s.AbortTransactionAsync(ct);

                    // Do something in response to the exception
                    throw; // NOTE: You must rethrow the exception otherwise an infinite loop can occur.
                }

                return true;
            },
            transactionOptions,
            cancellationToken
        );
    }

    public async Task SubmitDeleteBatchAsync(
        IEnumerable<string> entityIds,
        CancellationToken cancellationToken = default
    )
    {
        var collection = GetCollection<BsonDocument>().WithWriteConcern(WriteConcern.WMajority);
        using var session = await _client.StartSessionAsync(cancellationToken: cancellationToken);

        TransactionOptions transactionOptions = new(writeConcern: WriteConcern.WMajority);
        await session.WithTransactionAsync(
            async (s, ct) =>
            {
                try
                {
                    foreach (var id in entityIds)
                    {
                        var deleteResult = await collection.DeleteOneAsync(
                            session,
                            BuildPredicate<BsonDocument>(id, null),
                            cancellationToken: cancellationToken
                        );
                        if (deleteResult.IsAcknowledged)
                        {
                            if (deleteResult.DeletedCount == 0)
                                _telemetry.DeleteResultedInNoOp(id);
                        }
                    }
                }
                catch (MongoWriteException ex)
                {
                    await s.AbortTransactionAsync(ct);

                    _telemetry.FailedToWriteBatch(ex);
                    // Do something in response to the exception
                    throw; // NOTE: You must rethrow the exception otherwise an infinite loop can occur.
                }

                await s.CommitTransactionAsync(ct);

                return true;
            },
            transactionOptions,
            cancellationToken
        );
    }

    public async Task<T?> GetAsync<T>(
        FilterDefinition<T> predicate,
        CancellationToken cancellationToken = default
    )
        where T : class
    {
        var collection = GetCollection<T>();
        var findResult = await collection.FindAsync(
            predicate,
            new FindOptions<T, T> { Limit = 1 },
            cancellationToken
        );

        return await findResult.SingleOrDefaultAsync(cancellationToken);
    }

    public Task<T?> GetAsync<T>(
        string id,
        int? entityType,
        CancellationToken cancellationToken = default
    )
        where T : class => GetAsync(BuildPredicate<T>(id, entityType), cancellationToken);

    public async Task<T?> GetAsync<T>(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default
    )
        where T : class
    {
        var collection = GetCollection<T>();

        var result = await collection.FindAsync<T>(
            predicate,
            options: new() { Limit = 1 },
            cancellationToken: cancellationToken
        );
        return await result.MoveNextAsync(cancellationToken)
            ? result.Current.FirstOrDefault()
            : null;
    }

    public async Task<bool> UpsertAsync<T>(
        T document,
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default
    )
        where T : class
    {
        //var collection = GetCollection<BsonDocument>();
        //var d = document.ToBsonDocument();
        //d.Remove("_id");

        //var result = await collection.ReplaceOneAsync(m => m["_id"] == op.Document.Id, d, new ReplaceOptions { IsUpsert = false }, cancellationToken);

        var collection = GetCollection<T>();
        var result = await collection.ReplaceOneAsync(
            predicate,
            document,
            new ReplaceOptions { IsUpsert = true, BypassDocumentValidation = true },
            cancellationToken
        );
        return result.IsAcknowledged;
    }

    public async Task<bool> UpsertAsync<T>(
        T document,
        FilterDefinition<T> predicate,
        CancellationToken cancellationToken = default
    )
        where T : class
    {
        var collection = GetCollection<T>();

        var result = await collection.ReplaceOneAsync(
            predicate,
            document,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken
        );
        return result.IsAcknowledged;
    }

    public async Task InsertAsync<T>(T document, CancellationToken cancellationToken = default)
        where T : class
    {
        var collection = GetCollection<T>();

        await collection.InsertOneAsync(document, null, cancellationToken);
    }

    public async Task InsertAsync<T>(T[] documents, CancellationToken cancellationToken = default)
        where T : class => await InsertAsync(documents.AsEnumerable(), cancellationToken);

    public async Task InsertAsync<T>(
        IEnumerable<T> documents,
        CancellationToken cancellationToken = default
    )
        where T : class
    {
        var collection = GetCollection<T>();

        await collection.InsertManyAsync(documents, null, cancellationToken);
    }

    public async Task DeleteAsync<T>(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default
    )
        where T : class
    {
        var collection = GetCollection<T>();

        await collection.DeleteOneAsync(predicate, cancellationToken);
    }

    public async Task DeleteAsync<T>(
        FilterDefinition<T> predicate,
        CancellationToken cancellationToken = default
    )
        where T : class
    {
        var collection = GetCollection<T>();

        await collection.DeleteOneAsync(predicate, cancellationToken);
    }

    public async Task DeleteManyAsync<T>(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default
    )
        where T : class
    {
        var collection = GetCollection<T>();
        await collection.DeleteManyAsync(predicate, cancellationToken);
    }

    public async Task DeleteDatabaseAsync(CancellationToken cancellationToken = default) =>
        await _client.DropDatabaseAsync(_configuration.Database, cancellationToken);

    public async Task DeleteCollectionAsync(CancellationToken cancellationToken = default) =>
        await _database.DropCollectionAsync(_collectionName, cancellationToken);

    IMongoCollection<T> GetCollection<T>()
        where T : class => _database.GetCollection<T>(_collectionName);
}
