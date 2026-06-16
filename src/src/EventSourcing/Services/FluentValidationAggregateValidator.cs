using FluentValidation;
using FluentValidation.Results;
using Purview.EventSourcing.Aggregates;

namespace Purview.EventSourcing.Services;

public sealed class FluentValidationAggregateValidator<TAggregate>(IValidator<TAggregate> validator)
	: IAggregateValidator<TAggregate>
	where TAggregate : IAggregate
{
	readonly IValidator<TAggregate> _validator = validator;

	public ValidationResult Validate(TAggregate aggregate) => _validator.Validate(aggregate);

	public Task<ValidationResult> ValidateAsync(TAggregate aggregate, CancellationToken cancellationToken = default) =>
		_validator.ValidateAsync(aggregate, cancellationToken);
}
