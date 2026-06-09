//using System.ComponentModel.DataAnnotations;
//using System.Linq.Expressions;
//using System.Reflection;

//namespace Purview.EventSourcing.Validation;

//public class ValidationBuilder<T> : IValidationBuilder<T>
//{
//    readonly List<Func<T, ValidationResult?>> _validationRules = [];
//    readonly Dictionary<string, IAggregateTransformationRule> _transformations = [];

//    public IValidationBuilder<T> Required(Func<T, bool> predicate, string errorMessage)
//    {
//        ArgumentNullException.ThrowIfNull(predicate);
//        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);

//        _validationRules.Add(value =>
//            predicate(value) ? ValidationResult.Success : new ValidationResult(errorMessage)
//        );

//        return this;
//    }

//    public IValidationBuilder<T> RequiredProperty<TProperty>(
//        Expression<Func<T, TProperty>> propertySelector,
//        string errorMessage
//    )
//    {
//        ArgumentNullException.ThrowIfNull(propertySelector);
//        return Required(GetPropertyName(propertySelector), errorMessage);
//    }

//    public IValidationBuilder<T> Required(string propertyName, string errorMessage)
//    {
//        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
//        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);

//        var propertyInfo = GetPropertyInfo(propertyName);
//        _validationRules.Add(value =>
//        {
//            var propertyValue = propertyInfo.GetValue(value);
//            return
//                propertyValue is not null
//                && (propertyValue is not string text || !string.IsNullOrWhiteSpace(text))
//                ? ValidationResult.Success
//                : new ValidationResult(errorMessage);
//        });

//        return this;
//    }

//    public IValidationBuilder<T> Rule<TProperty>(
//        Expression<Func<T, TProperty>> propertySelector,
//        IAggregateValidationRule<TProperty> rule
//    )
//    {
//        ArgumentNullException.ThrowIfNull(propertySelector);
//        ArgumentNullException.ThrowIfNull(rule);

//        var accessor = propertySelector.Compile();
//        _validationRules.Add(value => rule.Validate(accessor(value)));
//        return this;
//    }

//    public IValidationBuilder<T> Rule<TProperty>(
//        Expression<Func<T, TProperty>> propertySelector,
//        ValidationAttribute validationAttribute
//    )
//    {
//        ArgumentNullException.ThrowIfNull(propertySelector);
//        ArgumentNullException.ThrowIfNull(validationAttribute);

//        var accessor = propertySelector.Compile();
//        var propertyName = GetPropertyName(propertySelector);
//        _validationRules.Add(value =>
//            validationAttribute.GetValidationResult(
//                accessor(value),
//                new ValidationContext((object)value!) { MemberName = propertyName }
//            )
//        );

//        return this;
//    }

//    public IValidationBuilder<T> Transform<TProperty>(
//        Expression<Func<T, TProperty>> propertySelector,
//        IAggregateTransformationRule<TProperty> rule
//    )
//    {
//        ArgumentNullException.ThrowIfNull(propertySelector);
//        ArgumentNullException.ThrowIfNull(rule);

//        var propertyName = GetPropertyName(propertySelector);
//        _transformations[propertyName] = rule;
//        return this;
//    }

//    public IValidationBuilder<T> Transform<TProperty>(
//        Expression<Func<T, TProperty>> propertySelector,
//        Func<TProperty?, TProperty?> transformer
//    )
//    {
//        ArgumentNullException.ThrowIfNull(propertySelector);
//        ArgumentNullException.ThrowIfNull(transformer);

//        var propertyName = GetPropertyName(propertySelector);
//        _transformations[propertyName] = new DelegateTransformationRule<TProperty>(transformer);
//        return this;
//    }

//    public IReadOnlyList<ValidationResult> Validate(T instance)
//    {
//        ArgumentNullException.ThrowIfNull(instance);

//        var failures = new List<ValidationResult>();
//        foreach (var rule in _validationRules)
//        {
//            var result = rule(instance);
//            if (result is not null)
//                failures.Add(result);
//        }

//        return failures;
//    }

//    public void ApplyTransformations(T instance)
//    {
//        ArgumentNullException.ThrowIfNull(instance);

//        foreach (var (propertyName, transformationRule) in _transformations)
//        {
//            var propertyInfo = GetPropertyInfo(propertyName);
//            if (!propertyInfo.CanWrite)
//                throw new InvalidOperationException(
//                    $"Property '{propertyName}' on type '{typeof(T).Name}' is not writable."
//                );

//            var transformedValue = transformationRule.Transform(propertyInfo.GetValue(instance));
//            propertyInfo.SetValue(instance, transformedValue);
//        }
//    }

//    internal IReadOnlyList<Func<T, ValidationResult?>> GetValidationRules() => _validationRules;

//    internal IReadOnlyDictionary<string, IAggregateTransformationRule> GetTransformations() =>
//        _transformations;

//    // Internal API for test access
//    internal (
//        IReadOnlyList<Func<T, ValidationResult?>> ValidationRules,
//        IReadOnlyDictionary<string, IAggregateTransformationRule> Transformations
//    ) GetRules() => (_validationRules, _transformations);

//    static string GetPropertyName<TProperty>(Expression<Func<T, TProperty>> propertySelector)
//    {
//        return GetPropertyInfo(propertySelector).Name;
//    }

//    static PropertyInfo GetPropertyInfo<TProperty>(Expression<Func<T, TProperty>> propertySelector)
//    {
//        ArgumentNullException.ThrowIfNull(propertySelector);

//        return propertySelector.Body switch
//        {
//            MemberExpression { Member: PropertyInfo propertyInfo } => propertyInfo,
//            UnaryExpression { Operand: MemberExpression { Member: PropertyInfo propertyInfo } } =>
//                propertyInfo,
//            _ => throw new ArgumentException(
//                "The property selector must target a property access expression.",
//                nameof(propertySelector)
//            ),
//        };
//    }

//    static PropertyInfo GetPropertyInfo(string propertyName)
//    {
//        return typeof(T).GetProperty(
//                propertyName,
//                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
//            )
//            ?? throw new ArgumentException(
//                $"Property '{propertyName}' was not found on type '{typeof(T).Name}'.",
//                nameof(propertyName)
//            );
//    }
//}
