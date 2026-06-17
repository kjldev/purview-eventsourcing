using FluentValidation;
using Purview.EventSourcing.Aggregates;
using Purview.EventSourcing.Services;

namespace Purview.EventSourcing.Services;

public sealed class FluentValidationAggregateValidatorTests
{
	[Test]
	public async Task ValidateAsync_UsesAsyncRules()
	{
		var asyncRuleInvoked = false;
		var aggregate = new TestAggregate { Name = "invalid" };
		var validator = new InlineValidator<TestAggregate>();
		validator
			.RuleFor(m => m.Name)
			.MustAsync(
				(_, _) =>
				{
					asyncRuleInvoked = true;
					return Task.FromResult(true);
				}
			);

		var adapter = new FluentValidationAggregateValidator<TestAggregate>(validator);

		var result = await adapter.ValidateAsync(aggregate);

		await Assert.That(asyncRuleInvoked).IsTrue();
		await Assert.That(result.IsValid).IsTrue();
	}

	sealed class TestAggregate : AggregateBase
	{
		public string Name { get; set; } = string.Empty;

		protected override void RegisterEvents() { }
	}
}
