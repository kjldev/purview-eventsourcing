//using System.ComponentModel.DataAnnotations;

//namespace Purview.EventSourcing.Validation;

///// <summary>
///// Interface for defining validation rules for aggregate properties.
///// </summary>
//public interface IAggregateValidationRule
//{
//    /// <summary>
//    /// Validates a property value against the rule.
//    /// </summary>
//    /// <param name="value">The value to validate</param>
//    /// <returns>Validation result</returns>
//    ValidationResult Validate(object? value);
//}

///// <summary>
///// Generic interface for defining validation rules for aggregate properties.
///// </summary>
///// <typeparam name="T">The type of the property being validated</typeparam>
//public interface IAggregateValidationRule<T> : IAggregateValidationRule
//{
//    /// <summary>
//    /// Validates a property value against the rule.
//    /// </summary>
//    /// <param name="value">The value to validate</param>
//    /// <returns>Validation result</returns>
//    ValidationResult Validate(T? value);
//}

///// <summary>
///// Interface for defining transformation rules for aggregate properties.
///// </summary>
//public interface IAggregateTransformationRule
//{
//    /// <summary>
//    /// Transforms a property value.
//    /// </summary>
//    /// <param name="value">The value to transform</param>
//    /// <returns>The transformed value</returns>
//    object? Transform(object? value);
//}

///// <summary>
///// Generic interface for defining transformation rules for aggregate properties.
///// </summary>
///// <typeparam name="T">The type of the property being transformed</typeparam>
//public interface IAggregateTransformationRule<T> : IAggregateTransformationRule
//{
//    /// <summary>
//    /// Transforms a property value.
//    /// </summary>
//    /// <param name="value">The value to transform</param>
//    /// <returns>The transformed value</returns>
//    T? Transform(T? value);
//}
