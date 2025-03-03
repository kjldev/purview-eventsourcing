using FluentValidation;
using Purview.EventSourcing.Aggregates;

namespace Purview.EventSourcing.Services;

/// <summary>
/// A default validator for <see cref="IAggregate"/>'s based on
/// standard data annotations.
/// </summary>
public sealed class DefaultAggregateValidator<TAggregate> : AbstractValidator<TAggregate>
	where TAggregate : IAggregate
{
	/// <summary>
	/// A statically cached instance based on the use of standard data annotations.
	/// </summary>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1000:Do not declare static members on generic types")]
	public static IValidator<TAggregate> Instance { get; } = new DefaultAggregateValidator<TAggregate>();

	DefaultAggregateValidator()
	{
		RuleFor(m => m)
			.Custom((aggregate, context) =>
			{
				System.ComponentModel.DataAnnotations.ValidationContext daContext = new(aggregate);
				List<System.ComponentModel.DataAnnotations.ValidationResult> failures = [];

				if (!System.ComponentModel.DataAnnotations.Validator.TryValidateObject(aggregate, daContext, failures, true))
				{
					foreach (var failure in failures)
						context.AddFailure(failure.MemberNames.FirstOrDefault(), failure.ErrorMessage);
				}
			});
	}
}
