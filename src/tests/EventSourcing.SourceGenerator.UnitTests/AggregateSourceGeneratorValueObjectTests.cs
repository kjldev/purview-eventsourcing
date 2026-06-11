using System.Reflection;
using Microsoft.CodeAnalysis;

namespace Purview.EventSourcing.SourceGenerator;

public sealed class AggregateSourceGeneratorValueObjectTests : SourceGeneratorTestBase<AggregateSourceGenerator>
{
	const string AggregateBaseStub = """
		#nullable enable
		namespace Purview.EventSourcing.Aggregates
		{
			public interface IAggregate
			{
			}

			public sealed class AggregateDetails
			{
				public string? Id { get; set; }
			}

			public abstract class AggregateBase : IAggregate
			{
				readonly System.Collections.Generic.Dictionary<System.Type, System.Delegate> _appliers = new();

				protected AggregateBase()
				{
					RegisterEvents();
				}

				public AggregateDetails Details { get; init; } = new();
				protected abstract void RegisterEvents();
				protected void Register<TEvent>(System.Action<TEvent> applier) where TEvent : class => _appliers[typeof(TEvent)] = applier;
				protected AggregateBase RecordAndApply<TEvent>(TEvent @event) where TEvent : class
				{
					((System.Action<TEvent>)_appliers[typeof(TEvent)])(@event);
					return this;
				}
			}
		}

		namespace Purview.EventSourcing.Aggregates.Events
		{
			public sealed class EventDetails
			{
				public string? CorrelationId { get; set; }
			}

			public abstract class EventBase
			{
				public EventDetails Details { get; init; } = new();
				public virtual int SchemaVersion => 1;
				protected abstract void BuildEventHash(ref System.HashCode hash);
			}
		}
		""";

	const string ValueObjectContextStub = """
		namespace Purview.EventSourcing.ValueObjects
		{
			public readonly record struct ValueObjectContext<TAggregate>(TAggregate Aggregate, string MemberName, string? EventName = null, string? CommandName = null);
		}

		namespace Purview.EventSourcing.Serialization
		{
			[System.AttributeUsage(System.AttributeTargets.Struct | System.AttributeTargets.Class)]
			public sealed class ScalarAttribute : System.Attribute
			{
				public ScalarAttribute(string propertyName = "Value")
				{
				}
			}
		}
		""";

	static string GetAggregateGeneratedSource(GeneratorDriverRunResult result)
	{
		var aggregateTree = result.GeneratedTrees.FirstOrDefault(t =>
			!t.FilePath.EndsWith("EmbeddedAttribute.cs", StringComparison.Ordinal)
			&& !t.FilePath.EndsWith("GenerateAggregateAttribute.g.cs", StringComparison.Ordinal)
			&& !t.FilePath.EndsWith("GenerateAggregateEventAttribute.g.cs", StringComparison.Ordinal)
			&& !t.FilePath.EndsWith("AggregateValidationAttribute.g.cs", StringComparison.Ordinal)
		);

		return aggregateTree?.GetText().ToString() ?? string.Empty;
	}

	[Test]
	public async Task Generate_UsesContextualValueObjectCreateAndHookMethods(CancellationToken cancellationToken)
	{
		var source =
			AggregateBaseStub
			+ ValueObjectContextStub
			+ """
				namespace Testing
				{

				public enum OrderStatusCode
				{
					Draft = 0,
					Confirmed = 1
				}

				[Purview.EventSourcing.Serialization.Scalar]
				public readonly partial record struct OrderStatus
				{
					public OrderStatusCode Value { get; }

					private OrderStatus(OrderStatusCode value) => Value = value;

					public static OrderStatus Create(OrderStatusCode value, in Purview.EventSourcing.ValueObjects.ValueObjectContext<OrderAggregate> context)
					{
						return new(value);
					}

					public static OrderStatus Hydrate(OrderStatusCode value) => new(value);
				}

				[Purview.EventSourcing.Aggregates.GenerateAggregate]
				public partial class OrderAggregate : Purview.EventSourcing.Aggregates.AggregateBase
				{
					public OrderStatus Status { get; private set; } = OrderStatus.Hydrate(OrderStatusCode.Draft);
					public int LineItems { get; private set; }

					[Purview.EventSourcing.Aggregates.GenerateAggregateEvent(EventName = "OrderConfirmed")]
					public partial void ConfirmOrder(OrderStatusCode status);

					public void AddLineItem() => LineItems++;

					partial void OnCreatingOrderConfirmed(ref OrderStatus status)
					{
						if (Status.Value != OrderStatusCode.Draft)
							throw new System.InvalidOperationException("Can only confirm draft orders.");

						if (LineItems == 0)
							throw new System.InvalidOperationException("Cannot confirm an order with no items.");
					}
				}
				}
				""";

		var (result, _) = await GenerateAsync(source, cancellationToken);
		var generatedSource = GetAggregateGeneratedSource(result);

		await Assert.That(generatedSource).Contains("OnCreatingOrderConfirmed(ref __statusValue);");
		await Assert.That(generatedSource).Contains("OnCreatedOrderConfirmed(@event);");
		await Assert
			.That(generatedSource)
			.Contains(
				"OrderStatus.Create(status, new global::Purview.EventSourcing.ValueObjects.ValueObjectContext<global::Testing.OrderAggregate>(this, MemberName: nameof(Status), EventName: nameof(global::Testing.OrderEvents.OrderConfirmed)))"
			);
		await Assert.That(generatedSource).Contains("OnStatusChanging(ref __statusValue);");
	}

	[Test]
	public async Task Generate_AllowsHookToRejectInvalidTransition(CancellationToken cancellationToken)
	{
		var source =
			AggregateBaseStub
			+ ValueObjectContextStub
			+ """
				namespace Testing
				{

				public enum OrderStatusCode
				{
					Draft = 0,
					Confirmed = 1
				}

				[Purview.EventSourcing.Serialization.Scalar]
				public readonly partial record struct OrderStatus
				{
					public OrderStatusCode Value { get; }

					private OrderStatus(OrderStatusCode value) => Value = value;

					public static OrderStatus Create(OrderStatusCode value, in Purview.EventSourcing.ValueObjects.ValueObjectContext<OrderAggregate> context)
					{
						return new(value);
					}

					public static OrderStatus Hydrate(OrderStatusCode value) => new(value);
				}

				[Purview.EventSourcing.Aggregates.GenerateAggregate]
				public partial class OrderAggregate : Purview.EventSourcing.Aggregates.AggregateBase
				{
					public OrderStatus Status { get; private set; } = OrderStatus.Hydrate(OrderStatusCode.Draft);
					public int LineItems { get; private set; }

					[Purview.EventSourcing.Aggregates.GenerateAggregateEvent(EventName = "OrderConfirmed")]
					public partial void ConfirmOrder(OrderStatusCode status);

					public void AddLineItem() => LineItems++;

					partial void OnCreatingOrderConfirmed(ref OrderStatus status)
					{
						if (LineItems == 0)
							throw new System.InvalidOperationException("Cannot confirm an order with no items.");
					}
				}
				}
				""";

		var assembly = await CompileToAssemblyAsync(source, cancellationToken);
		var aggregateType = assembly.GetType("Testing.OrderAggregate")!;
		var aggregate = Activator.CreateInstance(aggregateType)!;
		var orderStatusCodeType = assembly.GetType("Testing.OrderStatusCode")!;
		var confirmedValue = Enum.Parse(orderStatusCodeType, "Confirmed");
		var confirmMethod = aggregateType.GetMethod("ConfirmOrder")!;
		var addLineItemMethod = aggregateType.GetMethod("AddLineItem")!;

		var throwsWithoutLineItems = false;
		try
		{
			confirmMethod.Invoke(aggregate, [confirmedValue]);
		}
		catch (TargetInvocationException ex) when (ex.InnerException is InvalidOperationException)
		{
			throwsWithoutLineItems = true;
		}

		addLineItemMethod.Invoke(aggregate, null);
		confirmMethod.Invoke(aggregate, [confirmedValue]);

		var status = aggregateType.GetProperty("Status")!.GetValue(aggregate)!;
		var statusValue = status.GetType().GetProperty("Value")!.GetValue(status)!;

		await Assert.That(throwsWithoutLineItems).IsTrue();
		await Assert.That(statusValue.ToString()).IsEqualTo("Confirmed");
	}

	[Test]
	public async Task Generate_RunsPropertyHooksInRequiredOrderAndSkipsPreHookOnReplay(
		CancellationToken cancellationToken
	)
	{
		var source =
			AggregateBaseStub
			+ ValueObjectContextStub
			+ """
				namespace Testing
				{
				[Purview.EventSourcing.Serialization.Scalar]
				public readonly partial record struct EmailAddress
				{
					public string Value { get; }

					private EmailAddress(string value) => Value = value;

					public static int CreateCalls { get; private set; }
					public static int HydrateCalls { get; private set; }

					public static EmailAddress Create(string value)
					{
						CreateCalls++;
						return new(value);
					}

					public static EmailAddress Hydrate(string value)
					{
						HydrateCalls++;
						return new(value);
					}
				}

				[Purview.EventSourcing.Aggregates.GenerateAggregate]
				public partial class CustomerAggregate : Purview.EventSourcing.Aggregates.AggregateBase
				{
					public EmailAddress Email { get; private set; } = EmailAddress.Hydrate("init@example.com");
					public int ChangingCalls { get; private set; }
					public int ChangedCalls { get; private set; }
					public string Trace { get; private set; } = string.Empty;
					public string? PreviousEmailValue { get; private set; }
					public string? CurrentEmailValue { get; private set; }

					[Purview.EventSourcing.Aggregates.GenerateAggregateEvent(EventName = "CustomerRegistered")]
					public partial CustomerAggregate RegisterCustomer(string email);

					[Purview.EventSourcing.Aggregates.GenerateAggregateEvent(EventName = "CustomerEmailChanged")]
					public partial CustomerAggregate ChangeEmail(string email);

					partial void OnEmailChanging(ref EmailAddress email)
					{
						ChangingCalls++;
						Trace += "P";
						email = EmailAddress.Hydrate(email.Value + ".mutated");
					}

					partial void OnEmailChanged(EmailAddress previous, EmailAddress current)
					{
						ChangedCalls++;
						Trace += "C";
						PreviousEmailValue = previous.Value;
						CurrentEmailValue = current.Value;
					}

					partial void OnCreatingCustomerEmailChanged(ref EmailAddress email)
					{
						Trace += "E";
					}

					partial void OnAppliedCustomerEmailChanged(global::Testing.CustomerEvents.CustomerEmailChanged @event)
					{
						Trace += "A";
					}
				}
				}
				""";

		var assembly = await CompileToAssemblyAsync(source, cancellationToken);
		var aggregateType = assembly.GetType("Testing.CustomerAggregate")!;
		var emailType = assembly.GetType("Testing.EmailAddress")!;
		var eventType = assembly.GetType("Testing.CustomerEvents.CustomerEmailChanged")!;
		var aggregate = Activator.CreateInstance(aggregateType)!;

		aggregateType.GetMethod("RegisterCustomer")!.Invoke(aggregate, ["first@example.com"]);
		aggregateType.GetMethod("ChangeEmail")!.Invoke(aggregate, ["second@example.com"]);

		var traceAfterCommand = (string)aggregateType.GetProperty("Trace")!.GetValue(aggregate)!;
		var changingCalls = (int)aggregateType.GetProperty("ChangingCalls")!.GetValue(aggregate)!;
		var changedCalls = (int)aggregateType.GetProperty("ChangedCalls")!.GetValue(aggregate)!;
		var previousValue = (string?)aggregateType.GetProperty("PreviousEmailValue")!.GetValue(aggregate);
		var currentValue = (string?)aggregateType.GetProperty("CurrentEmailValue")!.GetValue(aggregate);
		var createCalls = (int)emailType.GetProperty("CreateCalls")!.GetValue(null)!;

		var hydrateMethod = emailType.GetMethod("Hydrate", BindingFlags.Public | BindingFlags.Static)!;
		var replayEvent = Activator.CreateInstance(eventType)!;
		eventType.GetProperty("Email")!.SetValue(replayEvent, hydrateMethod.Invoke(null, ["replay@example.com"]));

		var applyMethod = aggregateType.GetMethod(
			"Apply",
			BindingFlags.NonPublic | BindingFlags.Instance,
			null,
			[eventType],
			null
		)!;
		applyMethod.Invoke(aggregate, [replayEvent]);

		var changingCallsAfterReplay = (int)aggregateType.GetProperty("ChangingCalls")!.GetValue(aggregate)!;
		var changedCallsAfterReplay = (int)aggregateType.GetProperty("ChangedCalls")!.GetValue(aggregate)!;
		var createCallsAfterReplay = (int)emailType.GetProperty("CreateCalls")!.GetValue(null)!;
		var traceAfterReplay = (string)aggregateType.GetProperty("Trace")!.GetValue(aggregate)!;

		await Assert.That(traceAfterCommand).Contains("PECA");
		await Assert.That(changingCalls).IsEqualTo(2);
		await Assert.That(changedCalls).IsEqualTo(2);
		await Assert.That(previousValue).IsEqualTo("first@example.com.mutated");
		await Assert.That(currentValue).IsEqualTo("second@example.com.mutated");
		await Assert.That(createCalls).IsEqualTo(2);
		await Assert.That(changingCallsAfterReplay).IsEqualTo(2);
		await Assert.That(changedCallsAfterReplay).IsEqualTo(3);
		await Assert.That(createCallsAfterReplay).IsEqualTo(2);
		await Assert.That(traceAfterReplay.EndsWith("CA", StringComparison.Ordinal)).IsTrue();
	}
}
