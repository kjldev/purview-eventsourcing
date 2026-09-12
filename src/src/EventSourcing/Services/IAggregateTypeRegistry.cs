using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Purview.EventSourcing.Aggregates;

namespace Purview.EventSourcing.Services;

/// <summary>
/// Resolves a persisted aggregate type name to its CLR aggregate type. Used by Admin tooling to
/// inspect runtime behavior (for example unknown-event visibility) without requiring the application
/// to expose its aggregate types explicitly.
/// </summary>
public interface IAggregateTypeRegistry
{
	/// <summary>
	/// Attempts to resolve <paramref name="aggregateTypeName"/> to the CLR aggregate type.
	/// </summary>
	bool TryResolve(string aggregateTypeName, [NotNullWhen(true)] out Type? aggregateType);
}

/// <summary>
/// Default <see cref="IAggregateTypeRegistry"/> that scans loaded assemblies for concrete
/// <see cref="IAggregate"/> types and registers them under their persisted aggregate type names.
/// </summary>
public sealed class AssemblyAggregateTypeRegistry(IAggregateEventNameMapper eventNameMapper) : IAggregateTypeRegistry
{
	readonly Dictionary<string, Type> _aggregateTypes = Build(eventNameMapper);

	/// <inheritdoc/>
	public bool TryResolve(string aggregateTypeName, [NotNullWhen(true)] out Type? aggregateType)
	{
		ArgumentNullException.ThrowIfNull(aggregateTypeName);
		return _aggregateTypes.TryGetValue(aggregateTypeName, out aggregateType);
	}

	static Dictionary<string, Type> Build(IAggregateEventNameMapper eventNameMapper)
	{
		Dictionary<string, Type> result = new(StringComparer.Ordinal);
		foreach (var type in FindAggregateTypes())
		{
			var aggregateName = Initialize(eventNameMapper, type);
			if (aggregateName is not null)
				result.TryAdd(aggregateName, type);
		}

		return result;
	}

	static IEnumerable<Type> FindAggregateTypes()
	{
		foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
		{
			foreach (var type in SafeGetTypes(assembly))
			{
				if (
					type.IsClass
					&& !type.IsAbstract
					&& !type.IsGenericTypeDefinition
					&& type.GetConstructor(Type.EmptyTypes)?.IsPublic == true
					&& typeof(IAggregate).IsAssignableFrom(type)
				)
					yield return type;
			}
		}
	}

	static Type[] SafeGetTypes(Assembly assembly)
	{
		try
		{
			return assembly.GetTypes();
		}
		catch (ReflectionTypeLoadException ex)
		{
			return ex.Types.Where(static type => type is not null).ToArray()!;
		}
	}

	static string? Initialize(IAggregateEventNameMapper eventNameMapper, Type aggregateType)
	{
		var initialize = typeof(IAggregateEventNameMapper).GetMethod(
			nameof(IAggregateEventNameMapper.InitializeAggregate)
		);
		if (initialize is null)
			return null;

		var generic = initialize.MakeGenericMethod(aggregateType);
		try
		{
			return generic.Invoke(eventNameMapper, parameters: null) as string;
		}
		catch (TargetInvocationException)
		{
			// An aggregate that cannot be initialized is simply not resolvable.
			return null;
		}
	}
}
