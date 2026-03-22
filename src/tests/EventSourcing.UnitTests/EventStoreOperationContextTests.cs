namespace Purview.EventSourcing;

public class EventStoreOperationContextTests
{
	[Test]
	public async Task Constructor_DefaultValues_AreCorrect(CancellationToken cancellationToken)
	{
		// Act
		var context = new EventStoreOperationContext();

		// Assert
		await Assert.That(context.CorrelationId).IsNull();
		await Assert.That(context.DeleteMode).IsEqualTo(DeleteHandlingMode.ReturnsNull);
		await Assert.That(context.LockMode).IsEqualTo(LockHandlingMode.ThrowsException);
		await Assert.That(context.CacheMode).IsEqualTo(EventStoreCachingOptions.GetAndStore);
		await Assert.That(context.CacheOptions).IsNull();
		await Assert.That(context.SkipSnapshot).IsFalse();
		await Assert.That(context.NotificationMode).IsEqualTo(NotificationModes.All);
		await Assert.That(context.PermanentlyDelete).IsFalse();
		await Assert.That(context.UseIdempotencyMarker).IsFalse();
		await Assert.That(context.ClaimIdentifier).IsEqualTo("sub");
	}

	[Test]
	public async Task DefaultContext_ReturnsNonNullInstance(CancellationToken cancellationToken)
	{
		// Act
		var context = EventStoreOperationContext.DefaultContext;

		// Assert
		await Assert.That(context).IsNotNull();
	}

	[Test]
	public async Task DefaultContext_ReturnsSameInstanceEachTime(CancellationToken cancellationToken)
	{
		// Act
		var context1 = EventStoreOperationContext.DefaultContext;
		var context2 = EventStoreOperationContext.DefaultContext;

		// Assert
		await Assert.That(context1).IsSameReferenceAs(context2);
	}

	[Test]
	public async Task Properties_CanBeSet_ReturnNewValues(CancellationToken cancellationToken)
	{
		// Arrange & Act
		var context = new EventStoreOperationContext
		{
			CorrelationId = "test-correlation",
			DeleteMode = DeleteHandlingMode.ThrowsException,
			LockMode = LockHandlingMode.ReturnsFalse,
			CacheMode = EventStoreCachingOptions.None,
			SkipSnapshot = true,
			NotificationMode = NotificationModes.None,
			PermanentlyDelete = true,
			UseIdempotencyMarker = true,
			ClaimIdentifier = "custom-claim"
		};

		// Assert
		await Assert.That(context.CorrelationId).IsEqualTo("test-correlation");
		await Assert.That(context.DeleteMode).IsEqualTo(DeleteHandlingMode.ThrowsException);
		await Assert.That(context.LockMode).IsEqualTo(LockHandlingMode.ReturnsFalse);
		await Assert.That(context.CacheMode).IsEqualTo(EventStoreCachingOptions.None);
		await Assert.That(context.SkipSnapshot).IsTrue();
		await Assert.That(context.NotificationMode).IsEqualTo(NotificationModes.None);
		await Assert.That(context.PermanentlyDelete).IsTrue();
		await Assert.That(context.UseIdempotencyMarker).IsTrue();
		await Assert.That(context.ClaimIdentifier).IsEqualTo("custom-claim");
	}
}
