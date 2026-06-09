//namespace Purview.EventSourcing.Validation;

//public sealed class DelegateTransformationRule<T>(Func<T?, T?> transformer)
//    : IAggregateTransformationRule<T>
//{
//    private readonly Func<T?, T?> _transformer =
//        transformer ?? throw new ArgumentNullException(nameof(transformer));

//    public T? Transform(T? value) => _transformer(value);

//    object? IAggregateTransformationRule.Transform(object? value) => Transform((T?)value);
//}
