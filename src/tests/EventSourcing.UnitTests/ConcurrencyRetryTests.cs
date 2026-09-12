namespace Purview.EventSourcing;

public sealed class ConcurrencyRetryTests
{
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1032:Implement standard exception constructors")]
	sealed class TestConflictException : Exception, IConcurrencyConflict { }

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1032:Implement standard exception constructors")]
	sealed class OtherException : Exception { }

	[Test]
	public async Task ExecuteAsync_GivenOperationSucceedsImmediately_ReturnsResult()
	{
		// Arrange
		var calls = 0;

		// Act
		var result = await ConcurrencyRetry.ExecuteAsync(() =>
		{
			calls++;
			return Task.FromResult("ok");
		});

		// Assert
		await Assert.That(result).IsEqualTo("ok");
		await Assert.That(calls).IsEqualTo(1);
	}

	[Test]
	public async Task ExecuteAsync_GivenTransientConflictsThenSuccess_RetriesAndSucceeds()
	{
		// Arrange
		var calls = 0;

		// Act
		var result = await ConcurrencyRetry.ExecuteAsync(
			() =>
			{
				calls++;
				if (calls < 3)
					throw new TestConflictException();

				// Succeeds on the 3rd attempt
				return Task.FromResult("recovered");
			},
			maxAttempts: 5,
			initialBackoff: TimeSpan.FromMilliseconds(1)
		);

		// Assert
		await Assert.That(result).IsEqualTo("recovered");
		await Assert.That(calls).IsEqualTo(3);
	}

	[Test]
	public async Task ExecuteAsync_GivenPersistentConflicts_ThrowsAfterMaxAttempts()
	{
		// Arrange
		var calls = 0;

		// Act & Assert
		await Assert
			.That(async () =>
				await ConcurrencyRetry.ExecuteAsync<string>(
					() =>
					{
						calls++;
						throw new TestConflictException();
					},
					maxAttempts: 3,
					initialBackoff: TimeSpan.FromMilliseconds(1)
				)
			)
			.Throws<TestConflictException>();

		await Assert.That(calls).IsEqualTo(3);
	}

	[Test]
	public async Task ExecuteAsync_GivenNonConcurrencyException_DoesNotRetry()
	{
		// Arrange
		var calls = 0;

		// Act & Assert
		await Assert
			.That(async () =>
				await ConcurrencyRetry.ExecuteAsync<string>(
					() =>
					{
						calls++;
						throw new OtherException();
					},
					maxAttempts: 3,
					initialBackoff: TimeSpan.FromMilliseconds(1)
				)
			)
			.Throws<OtherException>();

		await Assert.That(calls).IsEqualTo(1);
	}

	[Test]
	public async Task ExecuteAsync_GivenCustomPredicate_RetriesMatchingExceptions()
	{
		// Arrange
		var calls = 0;

		// Act
		var result = await ConcurrencyRetry.ExecuteAsync(
			() =>
			{
				calls++;
				if (calls < 2)
					throw new OtherException();

				// Succeeds on the 2nd attempt
				return Task.FromResult("done");
			},
			maxAttempts: 3,
			initialBackoff: TimeSpan.FromMilliseconds(1),
			isConcurrencyException: static ex => ex is OtherException
		);

		// Assert
		await Assert.That(result).IsEqualTo("done");
		await Assert.That(calls).IsEqualTo(2);
	}

	[Test]
	public async Task ExecuteAsync_GivenCancellation_ThrowsOperationCanceledException()
	{
		// Arrange
		var calls = 0;
		using CancellationTokenSource cts = new();
		cts.CancelAfter(TimeSpan.FromMilliseconds(50));

		// Act & Assert
		await Assert
			.That(async () =>
				await ConcurrencyRetry.ExecuteAsync<string>(
					() =>
					{
						calls++;
						throw new TestConflictException();
					},
					maxAttempts: 3,
					initialBackoff: TimeSpan.FromSeconds(30),
					cancellationToken: cts.Token
				)
			)
			.Throws<OperationCanceledException>();
	}
}
