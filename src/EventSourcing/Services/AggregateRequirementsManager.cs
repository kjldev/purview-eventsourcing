using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Purview.EventSourcing.Aggregates;

namespace Purview.EventSourcing.Services;

sealed class AggregateRequiredServiceManager(IServiceProvider serviceProvider) : IAggregateRequirementsManager
{
	// This is static, but still allows the AggregateRequiredServiceManager to be registered as scoped
	// for the sake of the IServiceProvider.
	static readonly ConcurrentDictionary<Type, AggregateRequiredServiceManagerContext> _builders = new();

	public void Fulfil(IAggregate aggregate)
	{
		var context = _builders.GetOrAdd(aggregate.GetType(), t =>
		{
			AggregateRequiredServiceManagerContext builder = new(t);
			builder.Build();

			return builder;
		});

		context.Populate(aggregate, serviceProvider);
	}

	sealed class AggregateRequiredServiceManagerContext(Type aggregateType)
	{
		readonly List<Action<object[]>> _requiredServices = [];

		static readonly Lazy<MethodInfo> _populateMethod = new(() => typeof(AggregateRequiredServiceManagerContext).GetMethod(nameof(Populate), BindingFlags.Static | BindingFlags.NonPublic)!);

		bool _hasRequirements;

		public void Build()
		{
			var requiredServices = aggregateType.GetInterfaces().Where(m => m.IsGenericType && m.GetGenericTypeDefinition() == typeof(IRequirement<>));
			foreach (var requiredService in requiredServices)
			{
				var serviceType = requiredService.GetGenericArguments()[0];

				var genericMethod = _populateMethod.Value.MakeGenericMethod(serviceType);
				var parameters = genericMethod.GetParameters();
				var actionParams = Expression.Parameter(typeof(object[]), "params");
				var argExpressions = parameters.Select((param, i) =>
					Expression.Convert(
						Expression.ArrayIndex(actionParams, Expression.Constant(i)),
						param.ParameterType)
					).ToArray();

				var callExpression = Expression.Call(null, genericMethod, argExpressions);

				var lambda = Expression.Lambda<Action<object[]>>(callExpression, actionParams);
				var action = lambda.Compile();

				_requiredServices.Add(action);
			}

			_hasRequirements = _requiredServices.Count > 0;
		}

		public void Populate(IAggregate aggregate, IServiceProvider serviceProvider)
		{
			if (_hasRequirements)
			{
				foreach (var func in _requiredServices)
					func([aggregate, serviceProvider]);
			}
		}

		static void Populate<T>(IAggregate aggregate, IServiceProvider serviceProvider)
			where T : notnull
		{
			var required = (IRequirement<T>)aggregate;
			required.SetService(serviceProvider.GetRequiredService<T>());
		}
	}
}
