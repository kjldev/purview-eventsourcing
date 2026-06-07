using System.Text;
using Purview.EventSourcing.Aggregates;
using Purview.EventSourcing.MongoDB.Entities;

namespace Purview.EventSourcing.MongoDB;

partial class GenericMongoDBEventStoreTests<TAggregate>
{
    public async Task SaveAsync_GivenAggregateWithDataAnnotationsAndInvalidProperties_NoChangesAreMadeAndNotSaved(
        CancellationToken cancellationToken
    )
    {
        // Arrange
        var aggregateId = $"{Guid.NewGuid()}";
        var aggregate = TestHelpers.Aggregate<TAggregate>(
            aggregateId: aggregateId,
            a => a.SetValidatedProperty(-1)
        );

        var eventStore = fixture.CreateEventStore<TAggregate>();

        // Act
        var result = await eventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);

        // Assert
        await Assert.That(result.Saved).IsFalse();
        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(((bool)result)).IsFalse();
        await Assert.That(result.ValidationResult.Errors).HasSingleItem();
        await Assert
            .That(result.ValidationResult.Errors.Single().PropertyName)
            .IsEqualTo(nameof(IAggregateTest.IncrementInt32));
    }

    public async Task SaveAsync_GivenAggregateWithComplexProperty_SavesEventWithComplexProperty(
        CancellationToken cancellationToken
    )
    {
        // Arrange
        var complexProperty = CreateComplexTestType();

        var aggregateId = $"{Guid.NewGuid()}";
        var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);

        aggregate.SetComplexProperty(complexProperty);

        var eventStore = fixture.CreateEventStore<TAggregate>();

        var result = await eventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);
        await Assert.That(result.Saved).IsTrue();

        // Act
        var aggregateGetResult = await eventStore.GetAsync(
            aggregateId,
            cancellationToken: cancellationToken
        );

        // Assert
        await Assert.That(aggregateGetResult).IsNotNull();
        await Assert
            .That(aggregate.ComplexTestType)
            .IsEquivalentTo(aggregateGetResult.ComplexTestType);
    }

    public async Task SaveAsync_GivenAggregateWithNoChanges_DoesNotSave(
        CancellationToken cancellationToken
    )
    {
        // Arrange
        var aggregateId = $"{Guid.NewGuid()}";
        var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);

        var ctx = fixture.CreateEventStoreContext<TAggregate>();
        var eventStore = ctx.EventStore;
        var telemetry = ctx.Telemetry;

        // Act
        bool result = await eventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);

        // Assert
        await Assert.That(result).IsFalse();

        telemetry
            .Received(1)
            .SaveContainedNoChanges(aggregateId, Arg.Any<string>(), Arg.Any<string>());
    }

    public async Task SaveAsync_GivenNewAggregateWithChanges_SavesAggregate(
        CancellationToken cancellationToken
    )
    {
        // Arrange
        var aggregateId = $"{Guid.NewGuid()}";
        var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
        aggregate.IncrementInt32Value();

        var eventStore = fixture.CreateEventStore<TAggregate>();

        // Act
        var result = await eventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);

        // Assert
        await Assert.That(result.Saved).IsTrue();
        await Assert.That(result.Skipped).IsFalse();

        await Assert.That(aggregate.IsNew()).IsFalse();

        // Verify by re-getting the aggregate, knowing that the cache is disabled.
        var aggregateFromEventStore = await eventStore.GetAsync(
            aggregateId,
            cancellationToken: cancellationToken
        );

        await Assert.That(aggregateFromEventStore).IsNotNull();
        await Assert.That(aggregateFromEventStore.Id()).IsEqualTo(aggregate.Id());
        await Assert
            .That(aggregateFromEventStore.IncrementInt32)
            .IsEqualTo(aggregate.IncrementInt32);
        await Assert
            .That(aggregateFromEventStore.Details.SavedVersion)
            .IsEqualTo(aggregate.Details.SavedVersion);
        await Assert
            .That(aggregateFromEventStore.Details.CurrentVersion)
            .IsEqualTo(aggregate.Details.CurrentVersion);
        await Assert
            .That(aggregateFromEventStore.Details.SnapshotVersion)
            .IsEqualTo(aggregate.Details.SnapshotVersion);
        await Assert.That(aggregateFromEventStore.Details.Etag).IsEqualTo(aggregate.Details.Etag);
    }

    public async Task SaveAsync_GivenNewAggregateWithLargeChanges_SavesAggregateWithLargeEventRecord(
        CancellationToken cancellationToken
    )
    {
        // Arrange
        var aggregateId = $"{Guid.NewGuid()}";
        var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);

        var value = string.Empty;
        var sizeIsLessThan32K = true;
        while (sizeIsLessThan32K)
        {
            value += "abcdefghijklmnopqrstvwxyz";
            value += "ABCDEFGHIJKLMNOPQRSTVWXYZ";
            value += "1234567890";

            sizeIsLessThan32K = Encoding.UTF8.GetByteCount(value) < short.MaxValue;
        }

        aggregate.AppendString(value);

        var eventStore = fixture.CreateEventStore<TAggregate>();

        // Act
        bool result = await eventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);

        // Assert
        await Assert.That(result).IsTrue();
        await Assert.That(aggregate.IsNew()).IsFalse();

        // Verify by re-getting the aggregate, knowing that the cache is disabled.
        var aggregateFromEventStore = await eventStore.GetAsync(
            aggregateId,
            cancellationToken: cancellationToken
        );

        await Assert
            .That((aggregateFromEventStore?.StringProperty ?? string.Empty).Length)
            .IsEqualTo(aggregate.StringProperty.Length);

        await Assert
            .That(aggregateFromEventStore?.StringProperty)
            .IsEqualTo(aggregate.StringProperty);

        sizeIsLessThan32K =
            Encoding.UTF8.GetByteCount(aggregateFromEventStore?.StringProperty ?? string.Empty)
            < short.MaxValue;
        await Assert.That(sizeIsLessThan32K).IsFalse();
    }

    public async Task SaveAsync_GivenNewAggregateWithLargeChangesAndNoSnapshot_ReadsAggregateFromEvents(
        CancellationToken cancellationToken
    )
    {
        // Arrange
        var aggregateId = $"{Guid.NewGuid()}";
        var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);

        var value = string.Empty;
        var sizeIsLessThan32K = true;
        while (sizeIsLessThan32K)
        {
            value += "abcdefghijklmnopqrstvwxyz";
            value += "ABCDEFGHIJKLMNOPQRSTVWXYZ";
            value += "1234567890";

            sizeIsLessThan32K = Encoding.UTF8.GetByteCount(value) < short.MaxValue;
        }

        aggregate.AppendString(value);

        var eventStore = fixture.CreateEventStore<TAggregate>();
        var snapshotClient = fixture.SnapshotClient;

        // Act
        bool result = await eventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);

        // Assert
        await Assert.That(result).IsTrue();
        await Assert.That(aggregate.IsNew()).IsFalse();

        // Delete the snapshot to ensure the events are replayed.
        await snapshotClient.DeleteAsync<SnapshotEntity>(
            m => m.Id == aggregateId,
            cancellationToken: cancellationToken
        );

        // Verify by re-getting the aggregate, knowing that the cache is disabled.
        var aggregateFromEventStore = await eventStore.GetAsync(
            aggregateId,
            cancellationToken: cancellationToken
        );

        await Assert
            .That((aggregateFromEventStore?.StringProperty ?? string.Empty).Length)
            .IsEqualTo(aggregate.StringProperty.Length);

        await Assert
            .That(aggregateFromEventStore?.StringProperty)
            .IsEqualTo(aggregate.StringProperty);

        sizeIsLessThan32K =
            Encoding.UTF8.GetByteCount(aggregateFromEventStore?.StringProperty ?? string.Empty)
            < short.MaxValue;
        await Assert.That(sizeIsLessThan32K).IsFalse();
    }

    public async Task SaveAsync_GivenEventCountIsGreaterThanMaximumNumberOfAllowedEventsInSaveOperation_ThrowsException(
        int eventsToGenerate,
        CancellationToken cancellationToken
    )
    {
        // Arrange
        var aggregateId = $"{Guid.NewGuid()}";
        var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
        for (var i = 0; i < eventsToGenerate; i++)
            aggregate.IncrementInt32Value();

        var eventStore = fixture.CreateEventStore<TAggregate>();

        // Act
        async Task<SaveResult<TAggregate>?> Func() =>
            await eventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);

        // Get and update stream version to remove the Version property.
        await Assert.That(Func).Throws<ArgumentOutOfRangeException>();
    }
}
