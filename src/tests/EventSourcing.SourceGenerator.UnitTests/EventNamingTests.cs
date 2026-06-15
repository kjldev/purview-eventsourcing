namespace Purview.EventSourcing.SourceGenerator;

public sealed class EventNamingTests
{
	[Test]
	public async Task EventVerbMap_ReturnsExpectedPastTense()
	{
		var cases = new (string Verb, string PastTense)[]
		{
			("Create", "Created"),
			("Register", "Registered"),
			("Change", "Changed"),
			("Set", "Set"),
			("Withdraw", "Withdrawn"),
			("Submit", "Submitted"),
			("Cancel", "Canceled"),
			("Split", "Split"),
			("Reactivate", "Reactivated"),
		};

		foreach (var (verb, pastTense) in cases)
		{
			await Assert.That(EventVerbMap.TryGetPastTense(verb, out var actualPastTense)).IsTrue();
			await Assert.That(actualPastTense).IsEqualTo(pastTense);
		}
	}

	[Test]
	public async Task EventVerbMap_InfersExpectedGeneratedEventNames()
	{
		var cases = new (string MethodName, string AggregateName, string ExpectedEventName)[]
		{
			("ChangeName", "CustomerAggregate", "NameChanged"),
			("ChangeAge", "CustomerAggregate", "AgeChanged"),
			("RegisterCustomer", "CustomerAggregate", "CustomerRegistered"),
			("CreateCustomer", "CustomerAggregate", "CustomerCreated"),
			("Deactivate", "CustomerAggregate", "CustomerDeactivated"),
			("Reactivate", "CustomerAggregate", "CustomerReactivated"),
			("ApproveQuestion", "CustomerAggregate", "QuestionApproved"),
			("WithdrawConsent", "CustomerAggregate", "ConsentWithdrawn"),
			("SubmitApplication", "CustomerAggregate", "ApplicationSubmitted"),
		};

		foreach (var (methodName, aggregateName, expectedEventName) in cases)
		{
			await Assert
				.That(EventVerbMap.TryCreateGeneratedEventName(methodName, aggregateName, out var eventName))
				.IsTrue();
			await Assert.That(eventName).IsEqualTo(expectedEventName);
		}
	}

	[Test]
	public async Task EventVerbMap_ValidatesPastTenseEventNames()
	{
		var validNames = new[]
		{
			"NameChanged",
			"CustomerRegistered",
			"CustomerCreated",
			"QuestionApproved",
			"ConsentWithdrawn",
			"CustomerDeactivated",
			"CustomerSet",
		};

		var invalidNames = new[]
		{
			"ChangeName",
			"RegisterCustomer",
			"CreateCustomer",
			"ApproveQuestion",
			"WithdrawConsent",
			"NewCustomer",
		};

		foreach (var name in validNames)
			await Assert.That(EventVerbMap.IsPastTenseEventName(name)).IsTrue();

		foreach (var name in invalidNames)
			await Assert.That(EventVerbMap.IsPastTenseEventName(name)).IsFalse();
	}
}
