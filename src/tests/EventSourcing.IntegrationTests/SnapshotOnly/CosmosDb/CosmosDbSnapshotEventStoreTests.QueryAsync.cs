namespace Purview.EventSourcing.CosmosDb.Snapshot;

partial class CosmosDbSnapshotEventStoreTests
{
    [Test]
    [Arguments(1, 1)]
    [Arguments(1, 5)]
    [Arguments(1, 10)]
    [Arguments(5, 1)]
    [Arguments(5, 5)]
    [Arguments(5, 10)]
    [Arguments(10, 1)]
    [Arguments(10, 5)]
    [Arguments(10, 10)]
    public async Task CanQuery_GivenAggregatesExist_QueryAsExpected(
        int numberOfAggregates,
        int numberOfEvents,
        CancellationToken cancellationToken
    )
    {
        // Arrange
        await using var context = fixture.CreateContext(
            correlationIdsToGenerate: numberOfAggregates
        );

        for (var aggregateIndex = 0; aggregateIndex < numberOfAggregates; aggregateIndex++)
        {
            var aggregate = CreateAggregate($"{aggregateIndex}_{context.RunId}");

            for (var eventIndex = 0; eventIndex < numberOfEvents; eventIndex++)
            {
                aggregate.IncrementInt32Value();
            }

            bool saveResult = await context.EventStore.SaveAsync(
                aggregate,
                cancellationToken: cancellationToken
            );

            await Assert.That(saveResult).IsTrue();
        }

        // Act
        var aggregates = (
            await context.EventStore.QueryAsync(
                m => m.IncrementInt32 == numberOfEvents,
                maxRecordCount: numberOfAggregates + 1,
                cancellationToken: cancellationToken
            )
        ).Results;

        // Assert
        await Assert.That(aggregates.Length).IsEqualTo(numberOfAggregates);
    }

    [Test]
    [Arguments(1)]
    [Arguments(5)]
    [Arguments(10)]
    public async Task CanQuery_GivenAggregateType_QueryAsExpected(
        int numberOfAggregates,
        CancellationToken cancellationToken
    )
    {
        // Arrange
        await using var context = fixture.CreateContext(
            correlationIdsToGenerate: numberOfAggregates
        );

        var aggregateType = CreateAggregate().AggregateType;

        for (var aggregateIndex = 0; aggregateIndex < numberOfAggregates; aggregateIndex++)
        {
            var aggregate = CreateAggregate($"{aggregateIndex}_{context.RunId}");
            aggregate.IncrementInt32Value();

            bool saveResult = await context.EventStore.SaveAsync(
                aggregate,
                cancellationToken: cancellationToken
            );

            await Assert.That(saveResult).IsTrue();
        }

        // Act
        var aggregates = (
            await context.EventStore.QueryAsync(
                m => m.AggregateType == aggregateType,
                maxRecordCount: numberOfAggregates + 1,
                cancellationToken: cancellationToken
            )
        ).Results;

        // Assert
        await Assert.That(aggregates.Length).IsEqualTo(numberOfAggregates);
    }
}
