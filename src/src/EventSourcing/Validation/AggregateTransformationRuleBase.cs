//namespace Purview.EventSourcing.Validation;

///// <summary>
///// Base class for transformation rules.
///// </summary>
//public abstract class AggregateTransformationRuleBase<T> : IAggregateTransformationRule<T>
//{
//    public T? Transform(T? value) => TransformCore(value);

//    /// <summary>
//    /// Transforms the value. Override this method in derived classes.
//    /// </summary>
//    /// <param name="value">The value to transform</param>
//    /// <returns>Transformed value</returns>
//    protected abstract T? TransformCore(T? value);

//    object? IAggregateTransformationRule.Transform(object? value) => Transform((T?)value);
//}
