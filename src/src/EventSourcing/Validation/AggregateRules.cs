//using System.Linq.Expressions;
//using System.Reflection;
//using FluentValidation.Results;
//using Purview.EventSourcing.Aggregates;

//namespace Purview.EventSourcing.Validation;

///// <summary>
///// Defines transformation and validation rules for an aggregate.
///// </summary>
//public abstract class AggregateRules<TAggregate>
//    where TAggregate : class, IAggregate
//{
//    readonly Dictionary<string, PropertyRuleSet> _rulesByPropertyName = new(StringComparer.Ordinal);

//    protected void Transform<TProperty>(Expression<Func<TAggregate, TProperty>> transformExpression)
//    {
//        ArgumentNullException.ThrowIfNull(transformExpression);

//        var (propertyName, transform) = RuleExpressionParser.ParseTransform(transformExpression);
//        GetOrAddRuleSet(propertyName).Transformers.Add(transform);
//    }

//    protected void Transform<TProperty>(
//        Expression<Func<TAggregate, TProperty>> propertySelector,
//        Func<TProperty, TProperty> transformer
//    )
//    {
//        ArgumentNullException.ThrowIfNull(propertySelector);
//        ArgumentNullException.ThrowIfNull(transformer);

//        var propertyName = RuleExpressionParser.GetPropertyName(propertySelector);
//        GetOrAddRuleSet(propertyName).Transformers.Add(value => transformer((TProperty)value!));
//    }

//    protected void Validate(Expression<Func<TAggregate, bool>> validationExpression)
//    {
//        ArgumentNullException.ThrowIfNull(validationExpression);

//        var (propertyName, validate) = RuleExpressionParser.ParseValidation(validationExpression);
//        GetOrAddRuleSet(propertyName)
//            .Validators.Add(value =>
//                validate(value)
//                    ? null
//                    : new ValidationFailure(propertyName, $"Validation failed for '{propertyName}'.")
//            );
//    }

//    protected void Validate(Expression<Func<TAggregate, bool>> validationExpression, string errorMessage)
//    {
//        ArgumentNullException.ThrowIfNull(validationExpression);
//        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);

//        var (propertyName, validate) = RuleExpressionParser.ParseValidation(validationExpression);
//        GetOrAddRuleSet(propertyName)
//            .Validators.Add(value =>
//                validate(value) ? null : new ValidationFailure(propertyName, errorMessage)
//            );
//    }

//    protected void Validate<TProperty>(
//        Expression<Func<TAggregate, TProperty>> propertySelector,
//        Func<TProperty, bool> predicate,
//        string errorMessage
//    )
//    {
//        ArgumentNullException.ThrowIfNull(propertySelector);
//        ArgumentNullException.ThrowIfNull(predicate);
//        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);

//        var propertyName = RuleExpressionParser.GetPropertyName(propertySelector);
//        GetOrAddRuleSet(propertyName)
//            .Validators.Add(value =>
//                predicate((TProperty)value!)
//                    ? null
//                    : new ValidationFailure(propertyName, errorMessage)
//            );
//    }

//    public TProperty ApplyTransformations<TProperty>(string propertyName, TProperty value)
//    {
//        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

//        if (!_rulesByPropertyName.TryGetValue(propertyName, out var ruleSet))
//            return value;

//        object? current = value;
//        foreach (var transformer in ruleSet.Transformers)
//            current = transformer(current);

//        return current is null ? default! : (TProperty)current;
//    }

//    public void Validate<TProperty>(
//        string propertyName,
//        TProperty value,
//        ICollection<ValidationFailure> failures
//    )
//    {
//        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
//        ArgumentNullException.ThrowIfNull(failures);

//        if (!_rulesByPropertyName.TryGetValue(propertyName, out var ruleSet))
//            return;

//        foreach (var validator in ruleSet.Validators)
//        {
//            var failure = validator(value);
//            if (failure is not null)
//                failures.Add(failure);
//        }
//    }

//    public void TransformAggregate(TAggregate aggregate)
//    {
//        ArgumentNullException.ThrowIfNull(aggregate);

//        var aggregateType = aggregate.GetType();
//        foreach (var (propertyName, ruleSet) in _rulesByPropertyName)
//        {
//            var property = aggregateType.GetProperty(propertyName);
//            if (property is null || !property.CanRead || !property.CanWrite)
//                continue;

//            var currentValue = property.GetValue(aggregate);
//            object? transformedValue = currentValue;

//            foreach (var transformer in ruleSet.Transformers)
//                transformedValue = transformer(transformedValue);

//            property.SetValue(aggregate, transformedValue);
//        }
//    }

//    public void ValidateAggregate(TAggregate aggregate, ICollection<string> failures)
//    {
//        ArgumentNullException.ThrowIfNull(aggregate);
//        ArgumentNullException.ThrowIfNull(failures);

//        var aggregateType = aggregate.GetType();
//        foreach (var (propertyName, ruleSet) in _rulesByPropertyName)
//        {
//            var property = aggregateType.GetProperty(propertyName);
//            if (property is null || !property.CanRead)
//                continue;

//            var value = property.GetValue(aggregate);
//            foreach (var validator in ruleSet.Validators)
//            {
//                var failure = validator(value);
//                if (failure is not null)
//                    failures.Add(failure.ErrorMessage);
//            }
//        }
//    }

//    public AggregateOperationResult<TAggregate> Execute(
//        TAggregate aggregate,
//        IReadOnlyList<string> propertyNames,
//        IReadOnlyList<object?> values,
//        Func<object?[], AggregateOperationResult<TAggregate>> emitEvent
//    )
//    {
//        ArgumentNullException.ThrowIfNull(aggregate);
//        ArgumentNullException.ThrowIfNull(propertyNames);
//        ArgumentNullException.ThrowIfNull(values);
//        ArgumentNullException.ThrowIfNull(emitEvent);

//        if (propertyNames.Count != values.Count)
//            throw new ArgumentException(
//                "Property names and values must contain the same number of items.",
//                nameof(values)
//            );

//        var failures = new List<ValidationFailure>();
//        var transformedValues = new object?[values.Count];
//        var shouldEmitEvent = false;

//        var aggregateType = aggregate.GetType();
//        for (var i = 0; i < propertyNames.Count; i++)
//        {
//            var propertyName = propertyNames[i];
//            var property = aggregateType.GetProperty(
//                propertyName,
//                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
//            );

//            if (property is null || !property.CanRead)
//                throw new InvalidOperationException(
//                    $"Unable to evaluate rules for missing property '{propertyName}' on aggregate type '{aggregateType.FullName}'."
//                );

//            var transformedValue = ApplyTransformations(propertyName, values[i]);
//            transformedValues[i] = transformedValue;
//            Validate(propertyName, transformedValue, failures);

//            if (!shouldEmitEvent)
//                shouldEmitEvent = !Equals(property.GetValue(aggregate), transformedValue);
//        }

//        if (failures.Count > 0)
//            return AggregateOperationResult<TAggregate>.Failure(aggregate, failures);

//        if (!shouldEmitEvent)
//            return AggregateOperationResult<TAggregate>.Success(aggregate);

//        return emitEvent(transformedValues);
//    }

//    PropertyRuleSet GetOrAddRuleSet(string propertyName)
//    {
//        if (_rulesByPropertyName.TryGetValue(propertyName, out var ruleSet))
//            return ruleSet;

//        ruleSet = new PropertyRuleSet();
//        _rulesByPropertyName[propertyName] = ruleSet;
//        return ruleSet;
//    }

//    sealed class PropertyRuleSet
//    {
//        public List<Func<object?, object?>> Transformers { get; } = [];

//        public List<Func<object?, ValidationFailure?>> Validators { get; } = [];
//    }
//}

//static class RuleExpressionParser
//{
//    public static string GetPropertyName(LambdaExpression expression)
//    {
//        var propertyExpression = FindAggregatePropertyExpression(expression);
//        return propertyExpression.Member.Name;
//    }

//    public static (string propertyName, Func<object?, object?> transform) ParseTransform(
//        LambdaExpression expression
//    )
//    {
//        var propertyExpression = FindAggregatePropertyExpression(expression);
//        EnsureExpressionOnlyUsesPropertyChain(expression, propertyExpression);

//        var valueParameter = Expression.Parameter(typeof(object), "value");
//        var replacement = Expression.Convert(valueParameter, propertyExpression.Type);
//        var rewrittenBody = new ReplaceExpressionVisitor(propertyExpression, replacement).Visit(
//            expression.Body
//        )!;

//        var objectBody = Expression.Convert(rewrittenBody, typeof(object));
//        var lambda = Expression.Lambda<Func<object?, object?>>(objectBody, valueParameter);
//        return (propertyExpression.Member.Name, lambda.Compile());
//    }

//    public static (string propertyName, Func<object?, bool> validate) ParseValidation(
//        Expression<Func<object, bool>> expression
//    ) => throw new NotSupportedException();

//    public static (string propertyName, Func<object?, bool> validate) ParseValidation<TAggregate>(
//        Expression<Func<TAggregate, bool>> expression
//    )
//    {
//        var propertyExpression = FindAggregatePropertyExpression(expression);
//        EnsureExpressionOnlyUsesPropertyChain(expression, propertyExpression);

//        var valueParameter = Expression.Parameter(typeof(object), "value");
//        var replacement = Expression.Convert(valueParameter, propertyExpression.Type);
//        var rewrittenBody = new ReplaceExpressionVisitor(propertyExpression, replacement).Visit(
//            expression.Body
//        )!;

//        var boolBody = Expression.Convert(rewrittenBody, typeof(bool));
//        var lambda = Expression.Lambda<Func<object?, bool>>(boolBody, valueParameter);
//        return (propertyExpression.Member.Name, lambda.Compile());
//    }

//    static MemberExpression FindAggregatePropertyExpression(LambdaExpression expression)
//    {
//        var parameter = expression.Parameters[0];
//        var candidates = new List<MemberExpression>();

//        void Visit(Expression? node)
//        {
//            if (node is null)
//                return;

//            switch (node)
//            {
//                case MemberExpression memberExpression
//                    when memberExpression.Expression == parameter:
//                    candidates.Add(memberExpression);
//                    break;
//                case UnaryExpression unaryExpression:
//                    Visit(unaryExpression.Operand);
//                    break;
//                case MethodCallExpression methodCallExpression:
//                    Visit(methodCallExpression.Object);
//                    foreach (var argument in methodCallExpression.Arguments)
//                        Visit(argument);
//                    break;
//                case BinaryExpression binaryExpression:
//                    Visit(binaryExpression.Left);
//                    Visit(binaryExpression.Right);
//                    break;
//                case ConditionalExpression conditionalExpression:
//                    Visit(conditionalExpression.Test);
//                    Visit(conditionalExpression.IfTrue);
//                    Visit(conditionalExpression.IfFalse);
//                    break;
//                case InvocationExpression invocationExpression:
//                    Visit(invocationExpression.Expression);
//                    foreach (var argument in invocationExpression.Arguments)
//                        Visit(argument);
//                    break;
//                case MemberInitExpression memberInitExpression:
//                    Visit(memberInitExpression.NewExpression);
//                    foreach (var binding in memberInitExpression.Bindings.OfType<MemberAssignment>())
//                        Visit(binding.Expression);
//                    break;
//                case NewExpression newExpression:
//                    foreach (var argument in newExpression.Arguments)
//                        Visit(argument);
//                    break;
//            }
//        }

//        Visit(expression.Body);

//        if (candidates.Count == 0)
//            throw new InvalidOperationException(
//                $"Rule expression '{expression}' must reference one aggregate property."
//            );

//        var distinct = candidates
//            .Select(memberExpression => memberExpression.Member.Name)
//            .Distinct(StringComparer.Ordinal)
//            .ToArray();
//        if (distinct.Length != 1)
//            throw new InvalidOperationException(
//                $"Rule expression '{expression}' must reference exactly one aggregate property."
//            );

//        return candidates[0];
//    }

//    static void EnsureExpressionOnlyUsesPropertyChain(
//        LambdaExpression expression,
//        MemberExpression propertyExpression
//    )
//    {
//        var parameter = expression.Parameters[0];
//        var invalidReferenceFound = false;

//        void Visit(Expression? node)
//        {
//            if (node is null || invalidReferenceFound)
//                return;

//            if (node is ParameterExpression parameterExpression && parameterExpression == parameter)
//            {
//                invalidReferenceFound = true;
//                return;
//            }

//            switch (node)
//            {
//                case MemberExpression memberExpression:
//                    if (memberExpression != propertyExpression)
//                        Visit(memberExpression.Expression);
//                    return;
//                case UnaryExpression unaryExpression:
//                    Visit(unaryExpression.Operand);
//                    return;
//                case MethodCallExpression methodCallExpression:
//                    Visit(methodCallExpression.Object);
//                    foreach (var argument in methodCallExpression.Arguments)
//                        Visit(argument);
//                    return;
//                case BinaryExpression binaryExpression:
//                    Visit(binaryExpression.Left);
//                    Visit(binaryExpression.Right);
//                    return;
//                case ConditionalExpression conditionalExpression:
//                    Visit(conditionalExpression.Test);
//                    Visit(conditionalExpression.IfTrue);
//                    Visit(conditionalExpression.IfFalse);
//                    return;
//                case InvocationExpression invocationExpression:
//                    Visit(invocationExpression.Expression);
//                    foreach (var argument in invocationExpression.Arguments)
//                        Visit(argument);
//                    return;
//                case NewExpression newExpression:
//                    foreach (var argument in newExpression.Arguments)
//                        Visit(argument);
//                    return;
//            }
//        }

//        Visit(expression.Body);

//        if (invalidReferenceFound)
//            throw new InvalidOperationException(
//                $"Rule expression '{expression}' can only use the selected property chain."
//            );
//    }

//    sealed class ReplaceExpressionVisitor(Expression target, Expression replacement)
//        : ExpressionVisitor
//    {
//        protected override Expression VisitMember(MemberExpression node)
//        {
//            if (node == target)
//                return replacement;

//            return base.VisitMember(node);
//        }
//    }
//}
