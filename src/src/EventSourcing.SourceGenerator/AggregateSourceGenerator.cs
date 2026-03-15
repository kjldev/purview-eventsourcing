using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Purview.EventSourcing.SourceGenerator.Helpers;
using Purview.EventSourcing.SourceGenerator.Templates;

namespace Purview.EventSourcing.SourceGenerator;

[Generator]
public sealed class AggregateSourceGenerator : IIncrementalGenerator
{
	const string GenerateAggregateAttributeName = "Purview.EventSourcing.Aggregates.GenerateAggregateAttribute";
	const string GenerateAggregateEventAttributeName = "Purview.EventSourcing.Aggregates.GenerateAggregateEventAttribute";

	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		// Register the attribute templates as post-initialization output so they're
		// available to consuming projects without needing a reference to the core library.
		context.RegisterPostInitializationOutput(ctx =>
		{
			var resources = EmbeddedResources.Instance;
			ctx.AddSource("GenerateAggregateAttribute.g.cs", resources.LoadTemplate("GenerateAggregateAttribute"));
			ctx.AddSource("GenerateAggregateEventAttribute.g.cs", resources.LoadTemplate("GenerateAggregateEventAttribute"));
		});

		// Find all class declarations decorated with [GenerateAggregate]
		var aggregateClasses = context.SyntaxProvider.ForAttributeWithMetadataName(
			GenerateAggregateAttributeName,
			predicate: static (node, _) => node is ClassDeclarationSyntax,
			transform: static (ctx, ct) => GetAggregateInfo(ctx, ct)
		).Where(static info => info is not null);

		// Combine with compilation and generate
		context.RegisterSourceOutput(aggregateClasses, static (spc, info) =>
		{
			if (info is null)
				return;

			var source = EmitHelper.GenerateAggregateSource(info);
			spc.AddSource($"{info.ClassName}.g.cs", source);
		});
	}

	static AggregateInfo? GetAggregateInfo(GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
	{
		ct.ThrowIfCancellationRequested();

		if (ctx.TargetSymbol is not INamedTypeSymbol classSymbol)
			return null;

		// Verify it's a partial class
		var syntax = ctx.TargetNode as ClassDeclarationSyntax;
		if (syntax is null)
			return null;

		var isPartial = false;
		foreach (var modifier in syntax.Modifiers)
		{
			if (modifier.Text == "partial")
			{
				isPartial = true;
				break;
			}
		}

		if (!isPartial)
			return null;

		// Verify it inherits from AggregateBase (directly or transitively)
		if (!InheritsFromAggregateBase(classSymbol))
			return null;

		var namespaceName = classSymbol.ContainingNamespace.IsGlobalNamespace
			? null
			: classSymbol.ContainingNamespace.ToDisplayString();

		var methods = new List<AggregateEventMethodInfo>();

		foreach (var member in classSymbol.GetMembers())
		{
			ct.ThrowIfCancellationRequested();

			if (member is not IMethodSymbol methodSymbol)
				continue;

			// Check for [GenerateAggregateEvent]
			var hasAttribute = false;
			foreach (var attr in methodSymbol.GetAttributes())
			{
				var attrClass = attr.AttributeClass;
				if (attrClass is not null && attrClass.ToDisplayString() == GenerateAggregateEventAttributeName)
				{
					hasAttribute = true;
					break;
				}
			}

			if (!hasAttribute)
				continue;

			// Must be partial
			var methodSyntaxRefs = methodSymbol.DeclaringSyntaxReferences;
			var isMethodPartial = false;
			foreach (var syntaxRef in methodSyntaxRefs)
			{
				if (syntaxRef.GetSyntax(ct) is MethodDeclarationSyntax methodDecl)
				{
					foreach (var mod in methodDecl.Modifiers)
					{
						if (mod.Text == "partial")
						{
							isMethodPartial = true;
							break;
						}
					}
				}

				if (isMethodPartial)
					break;
			}

			if (!isMethodPartial)
				continue;

			var parameters = new List<EventPropertyInfo>();
			foreach (var param in methodSymbol.Parameters)
			{
				parameters.Add(new EventPropertyInfo(
					param.Name,
					param.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
				));
			}

			// Read Version from the [GenerateAggregateEvent] attribute (default 1)
			var version = 1;
			foreach (var attr in methodSymbol.GetAttributes())
			{
				var attrClass = attr.AttributeClass;
				if (attrClass is null || attrClass.ToDisplayString() != GenerateAggregateEventAttributeName)
					continue;

				foreach (var namedArg in attr.NamedArguments)
				{
					if (namedArg.Key == "Version" && namedArg.Value.Value is int v)
					{
						version = v;
						break;
					}
				}

				break;
			}

			methods.Add(new AggregateEventMethodInfo(
				methodSymbol.Name,
				parameters,
				version
			));
		}

		return new AggregateInfo(
			namespaceName,
			classSymbol.Name,
			classSymbol.DeclaredAccessibility,
			methods
		);
	}

	static bool InheritsFromAggregateBase(INamedTypeSymbol classSymbol)
	{
		var current = classSymbol.BaseType;
		while (current is not null)
		{
			if (current.Name == "AggregateBase")
				return true;
			current = current.BaseType;
		}

		return false;
	}
}
