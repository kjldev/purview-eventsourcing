using FluentValidation;
using Purview.EventSourcing.Aggregates;

namespace Purview.EventSourcing.Services;

static class AggregateValidatorAdapter
{
	public static IAggregateValidator<TAggregate>? Adapt<TAggregate>(IValidator<TAggregate>? validator)
		where TAggregate : IAggregate
	{
		return validator is null
			? null
			: validator as IAggregateValidator<TAggregate>
				?? new FluentValidationAggregateValidator<TAggregate>(validator);
	}
}
