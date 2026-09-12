namespace Purview.EventSourcing;

public sealed class AggregateWriteLockTests
{
	static string UniqueId() => $"order-{Guid.NewGuid():N}";

	[Test]
	public async Task AcquireAsync_GivenTwoWriters_SerializesThem()
	{
		// Arrange
		var streamId = UniqueId();
		var overlap = false;
		var active = 0;
		var maxActive = 0;

		async Task Work()
		{
			await using var lease = await AggregateWriteLock.AcquireAsync("orders", streamId);
			active++;
			Interlocked.Exchange(ref maxActive, Math.Max(Volatile.Read(ref maxActive), Volatile.Read(ref active)));
			if (active > 1)
				overlap = true;

			await Task.Delay(20);

			Interlocked.Decrement(ref active);
		}

		// Act
		await Task.WhenAll(Work(), Work(), Work(), Work());

		// Assert
		await Assert.That(overlap).IsFalse();
		await Assert.That(maxActive).IsEqualTo(1);
	}

	[Test]
	public async Task AcquireAsync_GivenDifferentStreams_DoNotBlockEachOther()
	{
		// Arrange
		var streamId1 = UniqueId();
		var streamId2 = UniqueId();
		var active = 0;
		var maxActive = 0;
		TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);

		async Task Work(string id)
		{
			await using var lease = await AggregateWriteLock.AcquireAsync("orders", id);
			active++;
			Interlocked.Exchange(ref maxActive, Math.Max(Volatile.Read(ref maxActive), Volatile.Read(ref active)));
			if (id == streamId1)
				await gate.Task;

			Interlocked.Decrement(ref active);
		}

		// Act
		var task1 = Work(streamId1);
		await Task.Delay(20);

		var task2 = Work(streamId2);
		await Task.Delay(20);

		gate.SetResult();

		await Task.WhenAll(task1, task2);

		// Assert — both held the lock at the same time because they are different streams
		await Assert.That(maxActive).IsEqualTo(2);
	}

	[Test]
	public async Task AcquireAsync_GivenReleaseThenReacquire_AllowsSecondWriter()
	{
		// Arrange
		var streamId = UniqueId();
		var active = 0;

		await using (await AggregateWriteLock.AcquireAsync("orders", streamId))
		{
			active++;
		}

		await using (await AggregateWriteLock.AcquireAsync("orders", streamId))
		{
			active++;
		}

		// Act & Assert — no deadlock, both acquisitions succeeded sequentially
		await Assert.That(active).IsEqualTo(2);
	}

	[Test]
	public async Task AcquireAsync_GivenNullArguments_Throws()
	{
		// Act & Assert
		await Assert
			.That(async () => await AggregateWriteLock.AcquireAsync(null!, "order-1"))
			.Throws<ArgumentNullException>();
		await Assert
			.That(async () => await AggregateWriteLock.AcquireAsync("orders", null!))
			.Throws<ArgumentNullException>();
	}
}
