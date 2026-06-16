//using System.Diagnostics.CodeAnalysis;
//using FluentValidation;
//using FluentValidation.Results;

//namespace Purview.EventSourcing.Aggregates;

///// <summary>
///// Represents the result of an aggregate operation, including validation errors.
///// </summary>
//public sealed record class AggregateOperationResult<TAggregate> : IAggregateOperationResult
//    where TAggregate : class, IAggregate
//{
//    public static AggregateOperationResult<TAggregate> Success([NotNull] TAggregate aggregate) =>
//        new(aggregate, new ValidationResult());

//    public static AggregateOperationResult<TAggregate> Failure(
//        [NotNull] TAggregate aggregate,
//        IEnumerable<ValidationFailure> failures
//    ) => new(aggregate, new ValidationResult([.. failures]));

//    public AggregateOperationResult([NotNull] TAggregate aggregate, ValidationResult validationResult)
//    {
//        Aggregate = aggregate;
//        ValidationResult = validationResult ?? throw new ArgumentNullException(nameof(validationResult));
//    }

//    public static implicit operator bool(
//        [NotNull] AggregateOperationResult<TAggregate> operationResult
//    ) => operationResult.IsSuccess;

//    public static implicit operator TAggregate(
//        [NotNull] AggregateOperationResult<TAggregate> operationResult
//    ) => operationResult.Aggregate;

//    public TAggregate Aggregate { get; }

//    IAggregate IAggregateOperationResult.Aggregate => Aggregate;

//    public ValidationResult ValidationResult { get; }

//    public bool IsSuccess => ValidationResult.IsValid;

//    public ValidationFailure? Error => ValidationResult.Errors.FirstOrDefault();

//    public IReadOnlyList<ValidationFailure> Errors => ValidationResult.Errors;

//    public void Guard()
//    {
//        if (!IsSuccess)
//            throw new ValidationException(ValidationResult.Errors);
//    }
//}
