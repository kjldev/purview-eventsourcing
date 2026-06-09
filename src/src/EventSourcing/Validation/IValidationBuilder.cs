//using System.ComponentModel.DataAnnotations;
//using System.Linq.Expressions;

//namespace Purview.EventSourcing.Validation;

//public interface IValidationBuilder<T>
//{
//    IValidationBuilder<T> Required(Func<T, bool> predicate, string errorMessage);

//    IValidationBuilder<T> RequiredProperty<TProperty>(
//        Expression<Func<T, TProperty>> propertySelector,
//        string errorMessage
//    );

//    IValidationBuilder<T> Required(string propertyName, string errorMessage);

//    IValidationBuilder<T> Rule<TProperty>(
//        Expression<Func<T, TProperty>> propertySelector,
//        IAggregateValidationRule<TProperty> rule
//    );

//    IValidationBuilder<T> Rule<TProperty>(
//        Expression<Func<T, TProperty>> propertySelector,
//        ValidationAttribute validationAttribute
//    );

//    IValidationBuilder<T> Transform<TProperty>(
//        Expression<Func<T, TProperty>> propertySelector,
//        IAggregateTransformationRule<TProperty> rule
//    );

//    IValidationBuilder<T> Transform<TProperty>(
//        Expression<Func<T, TProperty>> propertySelector,
//        Func<TProperty?, TProperty?> transformer
//    );

//    IReadOnlyList<ValidationResult> Validate(T instance);

//    void ApplyTransformations(T instance);
//}
