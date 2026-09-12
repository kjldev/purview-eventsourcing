using FluentValidation;
using Purview.EventSourcing.Aggregates;

namespace Purview.EventSourcing.Validation.FluentValidation;

public sealed class FluentValidationAggregateValidatorTests
{
	[Test]
	public async Task ValidateAsync_UsesAsyncRules(CancellationToken cancellationToken)
	{
		var asyncRuleInvoked = false;
		TestAggregate aggregate = new() { Name = "invalid" };
		InlineValidator<TestAggregate> validator = [];
		validator
			.RuleFor(m => m.Name)
			.MustAsync(
				(_, _) =>
				{
					asyncRuleInvoked = true;
					return Task.FromResult(true);
				}
			);

		FluentValidationAggregateValidator<TestAggregate> adapter = new(validator);

		var result = await adapter.ValidateAsync(aggregate, cancellationToken);

		await Assert.That(asyncRuleInvoked).IsTrue();
		await Assert.That(result.IsValid).IsTrue();
	}

	[Test]
	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Performance",
		"CA1849:Call async methods when in an async method"
	)]
	public async Task Validate_WhenFluentValidationFails_MapsToCoreValidationResult()
	{
		TestAggregate aggregate = new() { Name = "" };
		InlineValidator<TestAggregate> validator = [];
		validator.RuleFor(m => m.Name).NotEmpty();

		FluentValidationAggregateValidator<TestAggregate> adapter = new(validator);

		var result = adapter.Validate(aggregate);

		await Assert.That(result.IsValid).IsFalse();
		await Assert.That(result.Failures).Count().IsEqualTo(1);
		await Assert.That(result.Failures[0].PropertyName).IsEqualTo("Name");
	}

	sealed class TestAggregate : AggregateBase
	{
		public string Name { get; set; } = string.Empty;

		protected override void RegisterEvents() { }
	}
}
