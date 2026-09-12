namespace Purview.EventSourcing.Aggregates.Events.Upcasting;

public sealed class EventUpcasterRegistryTests
{
	#region Test event types

	sealed class LegacyEvent : EventBase
	{
		public string OldField { get; set; } = default!;

		protected override void BuildEventHash(ref HashCode hash) => hash.Add(OldField);
	}

	sealed class CurrentEvent : EventBase
	{
		public string NewField { get; set; } = default!;

		protected override void BuildEventHash(ref HashCode hash) => hash.Add(NewField);
	}

	sealed class IntermediateEvent : EventBase
	{
		public string MidField { get; set; } = default!;

		protected override void BuildEventHash(ref HashCode hash) => hash.Add(MidField);
	}

	sealed class V3Event : EventBase
	{
		public string V3Field { get; set; } = default!;

		public override int SchemaVersion => 3;

		protected override void BuildEventHash(ref HashCode hash) => hash.Add(V3Field);
	}

	sealed class LegacyToCurrentUpcaster : IEventUpcaster<LegacyEvent, CurrentEvent>
	{
		public CurrentEvent Upcast(LegacyEvent source) =>
			new() { Details = source.Details, NewField = source.OldField + "_upgraded" };
	}

	sealed class CurrentEventToLegacyUpcaster : IEventUpcaster<CurrentEvent, LegacyEvent>
	{
		public LegacyEvent Upcast(CurrentEvent source) =>
			new() { Details = source.Details, OldField = source.NewField + "_downgraded" };
	}

	sealed class InPlaceUpcaster : IEventUpcaster<LegacyEvent, LegacyEvent>
	{
		public LegacyEvent Upcast(LegacyEvent source) =>
			new() { Details = source.Details, OldField = source.OldField + "_inplace" };
	}

	sealed class LegacyToIntermediateUpcaster : IEventUpcaster<LegacyEvent, IntermediateEvent>
	{
		public IntermediateEvent Upcast(LegacyEvent source) =>
			new() { Details = source.Details, MidField = source.OldField + "_mid" };
	}

	sealed class IntermediateToCurrentUpcaster : IEventUpcaster<IntermediateEvent, CurrentEvent>
	{
		public CurrentEvent Upcast(IntermediateEvent source) =>
			new() { Details = source.Details, NewField = source.MidField + "_final" };
	}

	sealed class IntermediateToV3Upcaster : IEventUpcaster<IntermediateEvent, V3Event>
	{
		public V3Event Upcast(IntermediateEvent source) =>
			new() { Details = source.Details, V3Field = source.MidField + "_v3" };
	}

	#endregion

	[Test]
	public async Task CanUpcast_GivenNoUpcasterRegistered_ReturnsFalse()
	{
		// Arrange
		EventUpcasterRegistry registry = new([]);
		LegacyEvent legacyEvent = new() { OldField = "value" };

		// Act
		var result = registry.CanUpcast(legacyEvent);

		// Assert
		await Assert.That(result).IsFalse();
	}

	[Test]
	public async Task CanUpcast_GivenUpcasterRegistered_ReturnsTrue()
	{
		// Arrange
		EventUpcasterDescriptor<LegacyEvent, CurrentEvent> descriptor = new(new LegacyToCurrentUpcaster());
		EventUpcasterRegistry registry = new([descriptor]);
		LegacyEvent legacyEvent = new() { OldField = "value" };

		// Act
		var result = registry.CanUpcast(legacyEvent);

		// Assert
		await Assert.That(result).IsTrue();
	}

	[Test]
	public async Task Upcast_GivenNoUpcasterRegistered_ReturnsSameInstance()
	{
		// Arrange
		EventUpcasterRegistry registry = new([]);
		CurrentEvent currentEvent = new() { NewField = "value" };

		// Act
		var result = registry.Upcast(currentEvent);

		// Assert
		await Assert.That(result).IsEqualTo(currentEvent);
	}

	[Test]
	public async Task Upcast_GivenSingleUpcaster_ReturnsUpcastEvent()
	{
		// Arrange
		EventUpcasterDescriptor<LegacyEvent, CurrentEvent> descriptor = new(new LegacyToCurrentUpcaster());
		EventUpcasterRegistry registry = new([descriptor]);
		LegacyEvent legacyEvent = new() { OldField = "hello" };

		// Act
		var result = registry.Upcast(legacyEvent);

		// Assert
		await Assert.That(result).IsNotNull();
		await Assert.That(result).IsTypeOf<CurrentEvent>();
		await Assert.That(((CurrentEvent)result).NewField).IsEqualTo("hello_upgraded");
	}

	[Test]
	public async Task Upcast_GivenChainedUpcasters_AppliesChainInOrder()
	{
		// Arrange: LegacyEvent → IntermediateEvent → CurrentEvent
		EventUpcasterDescriptor<LegacyEvent, IntermediateEvent> legacyToMid = new(new LegacyToIntermediateUpcaster());
		EventUpcasterDescriptor<IntermediateEvent, CurrentEvent> midToCurrent = new(
			new IntermediateToCurrentUpcaster()
		);
		EventUpcasterRegistry registry = new([legacyToMid, midToCurrent]);

		LegacyEvent legacyEvent = new() { OldField = "v1" };

		// Act
		var result = registry.Upcast(legacyEvent);

		// Assert
		await Assert.That(result).IsTypeOf<CurrentEvent>();
		// LegacyEvent.OldField "v1" → IntermediateEvent.MidField "v1_mid" → CurrentEvent.NewField "v1_mid_final"
		await Assert.That(((CurrentEvent)result).NewField).IsEqualTo("v1_mid_final");
	}

	[Test]
	public async Task Upcast_GivenAlreadyCurrentEvent_ReturnsUnchanged()
	{
		// Arrange: only LegacyEvent → CurrentEvent upcaster registered
		EventUpcasterDescriptor<LegacyEvent, CurrentEvent> descriptor = new(new LegacyToCurrentUpcaster());
		EventUpcasterRegistry registry = new([descriptor]);

		CurrentEvent currentEvent = new() { NewField = "already-current" };

		// Act
		var result = registry.Upcast(currentEvent);

		// Assert — CurrentEvent has no upcaster, returned unchanged
		await Assert.That(result).IsEqualTo(currentEvent);
		await Assert.That(((CurrentEvent)result).NewField).IsEqualTo("already-current");
	}

	[Test]
	public async Task CanUpcast_GivenNullEvent_ThrowsArgumentNullException()
	{
		// Arrange
		EventUpcasterRegistry registry = new([]);

		// Act & Assert
		await Assert.That(() => registry.CanUpcast(null!)).Throws<ArgumentNullException>();
	}

	[Test]
	public async Task Upcast_GivenNullEvent_ThrowsArgumentNullException()
	{
		// Arrange
		EventUpcasterRegistry registry = new([]);

		// Act & Assert
		await Assert.That(() => registry.Upcast(null!)).Throws<ArgumentNullException>();
	}

	[Test]
	public async Task Descriptor_GivenWrongSourceType_ThrowsInvalidOperationException()
	{
		// Arrange
		EventUpcasterDescriptor<LegacyEvent, CurrentEvent> descriptor = new(new LegacyToCurrentUpcaster());
		CurrentEvent wrongEvent = new() { NewField = "not-a-legacy-event" };

		// Act & Assert
		await Assert.That(() => descriptor.Upcast(wrongEvent)).Throws<InvalidOperationException>();
	}

	[Test]
	public async Task Upcast_PreservesEventDetailsMetadata()
	{
		// Arrange
		var now = DateTime.UtcNow;
		EventUpcasterDescriptor<LegacyEvent, CurrentEvent> descriptor = new(new LegacyToCurrentUpcaster());
		EventUpcasterRegistry registry = new([descriptor]);

		LegacyEvent legacyEvent = new()
		{
			OldField = "test",
			Details =
			{
				IdempotencyId = "idempotency-123",
				When = now,
				UserId = "user-456",
				CorrelationId = "correlation-789",
				AggregateVersion = 42,
			},
		};

		// Act
		var result = registry.Upcast(legacyEvent);

		// Assert
		await Assert.That(result).IsTypeOf<CurrentEvent>();
		var upcastEvent = (CurrentEvent)result;
		await Assert.That(upcastEvent.Details.IdempotencyId).IsEqualTo("idempotency-123");
		await Assert.That(upcastEvent.Details.When).IsEqualTo(now);
		await Assert.That(upcastEvent.Details.UserId).IsEqualTo("user-456");
		await Assert.That(upcastEvent.Details.CorrelationId).IsEqualTo("correlation-789");
		await Assert.That(upcastEvent.Details.AggregateVersion).IsEqualTo(42);
	}

	[Test]
	public async Task Upcast_GivenThreeHopChain_AppliesAllStepsInOrder()
	{
		// Arrange: LegacyEvent → IntermediateEvent → V3Event (three hops)
		EventUpcasterDescriptor<LegacyEvent, IntermediateEvent> legacyToMid = new(new LegacyToIntermediateUpcaster());
		EventUpcasterDescriptor<IntermediateEvent, V3Event> midToV3 = new(new IntermediateToV3Upcaster());
		EventUpcasterRegistry registry = new([legacyToMid, midToV3]);

		LegacyEvent legacyEvent = new() { OldField = "source" };

		// Act
		var result = registry.Upcast(legacyEvent);

		// Assert
		await Assert.That(result).IsTypeOf<V3Event>();
		// LegacyEvent.OldField "source" → IntermediateEvent.MidField "source_mid" → V3Event.V3Field "source_mid_v3"
		await Assert.That(((V3Event)result).V3Field).IsEqualTo("source_mid_v3");
		await Assert.That(((V3Event)result).SchemaVersion).IsEqualTo(3);
	}

	[Test]
	public async Task Upcast_GivenSameTypeUpcaster_AppliesOnceAndStops()
	{
		// Arrange
		EventUpcasterDescriptor<LegacyEvent, LegacyEvent> descriptor = new(new InPlaceUpcaster());
		EventUpcasterRegistry registry = new([descriptor]);
		LegacyEvent legacyEvent = new() { OldField = "value" };

		// Act
		var result = registry.Upcast(legacyEvent);

		// Assert — applied exactly once, not treated as a cycle
		await Assert.That(result).IsTypeOf<LegacyEvent>();
		await Assert.That(((LegacyEvent)result).OldField).IsEqualTo("value_inplace");
	}

	[Test]
	public async Task Constructor_GivenCircularChain_ThrowsAtConstruction()
	{
		// Arrange
		EventUpcasterDescriptor<LegacyEvent, CurrentEvent> legacyToCurrent = new(new LegacyToCurrentUpcaster());
		EventUpcasterDescriptor<CurrentEvent, LegacyEvent> currentToLegacy = new(new CurrentEventToLegacyUpcaster());

		// Act & Assert
		await Assert
			.That(() => new EventUpcasterRegistry([legacyToCurrent, currentToLegacy]))
			.Throws<InvalidOperationException>();
	}

	[Test]
	public async Task Constructor_GivenSelfLoop_DoesNotThrow()
	{
		// Arrange
		EventUpcasterDescriptor<LegacyEvent, LegacyEvent> descriptor = new(new InPlaceUpcaster());

		// Act & Assert
		await Assert.That(() => new EventUpcasterRegistry([descriptor])).ThrowsNothing();
	}

	[Test]
	public async Task CanUpcast_GivenThreeHopChain_ReturnsTrue()
	{
		// Arrange: chain LegacyEvent → IntermediateEvent → V3Event
		EventUpcasterDescriptor<LegacyEvent, IntermediateEvent> legacyToMid = new(new LegacyToIntermediateUpcaster());
		EventUpcasterDescriptor<IntermediateEvent, V3Event> midToV3 = new(new IntermediateToV3Upcaster());
		EventUpcasterRegistry registry = new([legacyToMid, midToV3]);

		LegacyEvent legacyEvent = new() { OldField = "test" };

		// Act
		var result = registry.CanUpcast(legacyEvent);

		// Assert
		await Assert.That(result).IsTrue();
	}
}
