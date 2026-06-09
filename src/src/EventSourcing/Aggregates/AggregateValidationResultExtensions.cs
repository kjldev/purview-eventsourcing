//using FluentValidation.Results;

//namespace Purview.EventSourcing.Aggregates;

///// <summary>
///// Helpers for merging aggregate operation-state validation into save validation.
///// </summary>
//public static class AggregateValidationResultExtensions
//{
//    public static ValidationResult IncludeLastOperationFailures<TAggregate>(
//        this ValidationResult validationResult,
//        TAggregate aggregate
//    )
//        where TAggregate : class, IAggregate
//    {
//        ArgumentNullException.ThrowIfNull(validationResult);
//        ArgumentNullException.ThrowIfNull(aggregate);

//        if (aggregate is not AggregateBase { HasOperationFailures: true, LastOperation: { } lastOperation })
//            return validationResult;

//        if (lastOperation.ValidationResult.IsValid)
//            return validationResult;

//        var mergedFailures = validationResult.Errors.Concat(lastOperation.ValidationResult.Errors);
//        return new ValidationResult([.. mergedFailures]);
//    }
//}
