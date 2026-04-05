using Purview.EventSourcing.Aggregates.Events;

namespace Purview.EventSourcing.Aggregates.Events.Upcasting;

public sealed class EventUpcasterRegistryTests
{
	#region Test event types

	sealed class LegacyEvent : EventBase
	{
		public string OldField { get; set; } = default!;

		protected override void BuildEventHash(ref HashCode hash)
			=> hash.Add(OldField);
	}

	sealed class CurrentEvent : EventBase
	{
		public string NewField { get; set; } = default!;

		protected override void BuildEventHash(ref HashCode hash)
			=> hash.Add(NewField);
	}

	sealed class IntermediateEvent : EventBase
	{
		public string MidField { get; set; } = default!;

		protected override void BuildEventHash(ref HashCode hash)
			=> hash.Add(MidField);
	}

	sealed class LegacyToCurrentUpcaster : IEventUpcaster<LegacyEvent, CurrentEvent>
	{
		public CurrentEvent Upcast(LegacyEvent source)
			=> new() { Details = source.Details, NewField = source.OldField + "_upgraded" };
	}

	sealed class LegacyToIntermediateUpcaster : IEventUpcaster<LegacyEvent, IntermediateEvent>
	{
		public IntermediateEvent Upcast(LegacyEvent source)
			=> new() { Details = source.Details, MidField = source.OldField + "_mid" };
	}

	sealed class IntermediateToCurrentUpcaster : IEventUpcaster<IntermediateEvent, CurrentEvent>
	{
		public CurrentEvent Upcast(IntermediateEvent source)
			=> new() { Details = source.Details, NewField = source.MidField + "_final" };
	}

	#endregion

	[Test]
	public async Task CanUpcast_GivenNoUpcasterRegistered_ReturnsFalse(CancellationToken cancellationToken)
	{
		// Arrange
		var registry = new EventUpcasterRegistry([]);
		var legacyEvent = new LegacyEvent { OldField = "value" };

		// Act
		var result = registry.CanUpcast(legacyEvent);

		// Assert
		await Assert.That(result).IsFalse();
	}

	[Test]
	public async Task CanUpcast_GivenUpcasterRegistered_ReturnsTrue(CancellationToken cancellationToken)
	{
		// Arrange
		var descriptor = new EventUpcasterDescriptor<LegacyEvent, CurrentEvent>(new LegacyToCurrentUpcaster());
		var registry = new EventUpcasterRegistry([descriptor]);
		var legacyEvent = new LegacyEvent { OldField = "value" };

		// Act
		var result = registry.CanUpcast(legacyEvent);

		// Assert
		await Assert.That(result).IsTrue();
	}

	[Test]
	public async Task Upcast_GivenNoUpcasterRegistered_ReturnsSameInstance(CancellationToken cancellationToken)
	{
		// Arrange
		var registry = new EventUpcasterRegistry([]);
		var currentEvent = new CurrentEvent { NewField = "value" };

		// Act
		var result = registry.Upcast(currentEvent);

		// Assert
		await Assert.That(result).IsEqualTo(currentEvent);
	}

	[Test]
	public async Task Upcast_GivenSingleUpcaster_ReturnsUpcastEvent(CancellationToken cancellationToken)
	{
		// Arrange
		var descriptor = new EventUpcasterDescriptor<LegacyEvent, CurrentEvent>(new LegacyToCurrentUpcaster());
		var registry = new EventUpcasterRegistry([descriptor]);
		var legacyEvent = new LegacyEvent { OldField = "hello" };

		// Act
		var result = registry.Upcast(legacyEvent);

		// Assert
		await Assert.That(result).IsNotNull();
		await Assert.That(result).IsTypeOf<CurrentEvent>();
		await Assert.That(((CurrentEvent)result).NewField).IsEqualTo("hello_upgraded");
	}

	[Test]
	public async Task Upcast_GivenChainedUpcasters_AppliesChainInOrder(CancellationToken cancellationToken)
	{
		// Arrange: LegacyEvent → IntermediateEvent → CurrentEvent
		var legacyToMid = new EventUpcasterDescriptor<LegacyEvent, IntermediateEvent>(new LegacyToIntermediateUpcaster());
		var midToCurrent = new EventUpcasterDescriptor<IntermediateEvent, CurrentEvent>(new IntermediateToCurrentUpcaster());
		var registry = new EventUpcasterRegistry([legacyToMid, midToCurrent]);

		var legacyEvent = new LegacyEvent { OldField = "v1" };

		// Act
		var result = registry.Upcast(legacyEvent);

		// Assert
		await Assert.That(result).IsTypeOf<CurrentEvent>();
		// LegacyEvent.OldField "v1" → IntermediateEvent.MidField "v1_mid" → CurrentEvent.NewField "v1_mid_final"
		await Assert.That(((CurrentEvent)result).NewField).IsEqualTo("v1_mid_final");
	}

	[Test]
	public async Task Upcast_GivenAlreadyCurrentEvent_ReturnsUnchanged(CancellationToken cancellationToken)
	{
		// Arrange: only LegacyEvent → CurrentEvent upcaster registered
		var descriptor = new EventUpcasterDescriptor<LegacyEvent, CurrentEvent>(new LegacyToCurrentUpcaster());
		var registry = new EventUpcasterRegistry([descriptor]);

		var currentEvent = new CurrentEvent { NewField = "already-current" };

		// Act
		var result = registry.Upcast(currentEvent);

		// Assert — CurrentEvent has no upcaster, returned unchanged
		await Assert.That(result).IsEqualTo(currentEvent);
		await Assert.That(((CurrentEvent)result).NewField).IsEqualTo("already-current");
	}

	[Test]
	public async Task CanUpcast_GivenNullEvent_ThrowsArgumentNullException(CancellationToken cancellationToken)
	{
		// Arrange
		var registry = new EventUpcasterRegistry([]);

		// Act & Assert
		await Assert.That(() => registry.CanUpcast(null!)).Throws<ArgumentNullException>();
	}

	[Test]
	public async Task Upcast_GivenNullEvent_ThrowsArgumentNullException(CancellationToken cancellationToken)
	{
		// Arrange
		var registry = new EventUpcasterRegistry([]);

		// Act & Assert
		await Assert.That(() => registry.Upcast(null!)).Throws<ArgumentNullException>();
	}

	[Test]
	public async Task Descriptor_GivenWrongSourceType_ThrowsInvalidOperationException(CancellationToken cancellationToken)
	{
		// Arrange
		var descriptor = new EventUpcasterDescriptor<LegacyEvent, CurrentEvent>(new LegacyToCurrentUpcaster());
		var wrongEvent = new CurrentEvent { NewField = "not-a-legacy-event" };

		// Act & Assert
		await Assert.That(() => descriptor.Upcast(wrongEvent)).Throws<InvalidOperationException>();
	}
}
