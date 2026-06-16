//using FluentValidation.Results;

//namespace Purview.EventSourcing.Aggregates;

///// <summary>
///// Represents the result of an aggregate command operation.
///// </summary>
//public interface IAggregateOperationResult
//{
//    /// <summary>
//    /// Gets the aggregate targeted by the operation.
//    /// </summary>
//    IAggregate Aggregate { get; }

//    /// <summary>
//    /// Gets validation details for the operation.
//    /// </summary>
//    ValidationResult ValidationResult { get; }

//    /// <summary>
//    /// Gets a value indicating whether the operation succeeded.
//    /// </summary>
//    bool IsSuccess { get; }

//    /// <summary>
//    /// Throws if the operation failed validation.
//    /// </summary>
//    void Guard();
//}
