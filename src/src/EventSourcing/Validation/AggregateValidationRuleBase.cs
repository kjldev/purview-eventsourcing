//using System.ComponentModel.DataAnnotations;

//namespace Purview.EventSourcing.Validation;

///// <summary>
///// Base class for validation rules.
///// </summary>
//public abstract class AggregateValidationRuleBase<T> : IAggregateValidationRule<T>
//{
//    public ValidationResult Validate(T? value) => ValidateCore(value);

//    /// <summary>
//    /// Validates the value. Override this method in derived classes.
//    /// </summary>
//    /// <param name="value">The value to validate</param>
//    /// <returns>Validation result</returns>
//    protected abstract ValidationResult ValidateCore(T? value);

//    ValidationResult IAggregateValidationRule.Validate(object? value) => Validate((T?)value);
//}
