using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Purview.EventSourcing.SourceGenerator.Helpers;
using Purview.EventSourcing.SourceGenerator.Templates;

namespace Purview.EventSourcing.SourceGenerator;

[Generator]
public sealed class AggregateSourceGenerator : IIncrementalGenerator
{
	const string GenerateAggregateAttributeName = "Purview.EventSourcing.Aggregates.GenerateAggregateAttribute";
	const string GenerateAggregateEventAttributeName =
		"Purview.EventSourcing.Aggregates.GenerateAggregateEventAttribute";
	const string AggregateBaseMetadataName = "Purview.EventSourcing.Aggregates.AggregateBase";

	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		// Register the attribute templates as post-initialization output so they're
		// available to consuming projects without needing a reference to the core library.
		context.RegisterPostInitializationOutput(ctx =>
		{
			var resources = EmbeddedResources.Instance;
			ctx.AddSource("GenerateAggregateAttribute.g.cs", resources.LoadTemplate("GenerateAggregateAttribute"));
			ctx.AddSource(
				"GenerateAggregateEventAttribute.g.cs",
				resources.LoadTemplate("GenerateAggregateEventAttribute")
			);
		});

		// Find all class declarations decorated with [GenerateAggregate]
		var aggregateClasses = context.SyntaxProvider.ForAttributeWithMetadataName(
			GenerateAggregateAttributeName,
			predicate: static (node, _) => node is ClassDeclarationSyntax,
			transform: static (ctx, ct) => GetAggregateGenerationResult(ctx, ct)
		);

		var standaloneEventMethods = context.SyntaxProvider.ForAttributeWithMetadataName(
			GenerateAggregateEventAttributeName,
			predicate: static (node, _) => node is MethodDeclarationSyntax,
			transform: static (ctx, _) => GetStandaloneEventMethodValidationResult(ctx)
		);

		context.RegisterSourceOutput(
			aggregateClasses,
			static (spc, result) =>
			{
				ReportDiagnostics(spc, result.Diagnostics);

				if (result.Info is null)
					return;

				var source = EmitHelper.GenerateAggregateSource(result.Info);
				spc.AddSource(result.Info.HintName, source);
			}
		);

		context.RegisterSourceOutput(
			standaloneEventMethods,
			static (spc, result) => ReportDiagnostics(spc, result.Diagnostics)
		);
	}

	static AggregateGenerationResult GetAggregateGenerationResult(
		GeneratorAttributeSyntaxContext ctx,
		CancellationToken ct
	)
	{
		ct.ThrowIfCancellationRequested();

		if (ctx.TargetSymbol is not INamedTypeSymbol classSymbol)
			return new AggregateGenerationResult(null, []);

		if (ctx.TargetNode is not ClassDeclarationSyntax syntax)
			return new AggregateGenerationResult(null, []);

		var diagnostics = new List<Diagnostic>();
		var canGenerate = true;
		var compilation = ctx.SemanticModel.Compilation;
		var aggregateBaseSymbol = compilation.GetTypeByMetadataName(AggregateBaseMetadataName);
		var eventAttributeSymbol = compilation.GetTypeByMetadataName(GenerateAggregateEventAttributeName);

		var isPartial = false;
		foreach (var modifier in syntax.Modifiers)
		{
			if (modifier.IsKind(SyntaxKind.PartialKeyword))
			{
				isPartial = true;
				break;
			}
		}

		if (!isPartial)
		{
			diagnostics.Add(
				Diagnostic.Create(
					GeneratorDiagnostics.AggregateMustBePartial,
					syntax.Identifier.GetLocation(),
					classSymbol.Name
				)
			);
			canGenerate = false;
		}

		if (classSymbol.ContainingType is not null)
		{
			diagnostics.Add(
				Diagnostic.Create(
					GeneratorDiagnostics.NestedAggregatesAreNotSupported,
					syntax.Identifier.GetLocation(),
					classSymbol.Name
				)
			);
			canGenerate = false;
		}

		if (classSymbol.TypeParameters.Length > 0)
		{
			diagnostics.Add(
				Diagnostic.Create(
					GeneratorDiagnostics.GenericAggregatesAreNotSupported,
					syntax.Identifier.GetLocation(),
					classSymbol.Name
				)
			);
			canGenerate = false;
		}

		if (aggregateBaseSymbol is null || !InheritsFromAggregateBase(classSymbol, aggregateBaseSymbol))
		{
			diagnostics.Add(
				Diagnostic.Create(
					GeneratorDiagnostics.AggregateMustInheritAggregateBase,
					syntax.Identifier.GetLocation(),
					classSymbol.Name
				)
			);
			canGenerate = false;
		}

		var registerEventsMethod = classSymbol
			.GetMembers("RegisterEvents")
			.OfType<IMethodSymbol>()
			.FirstOrDefault(method =>
				method.Parameters.Length == 0
				&& method.MethodKind == MethodKind.Ordinary
				&& !method.IsImplicitlyDeclared
			);
		if (registerEventsMethod is not null)
		{
			diagnostics.Add(
				Diagnostic.Create(
					GeneratorDiagnostics.ManualRegisterEventsIsNotSupported,
					registerEventsMethod.Locations.FirstOrDefault() ?? syntax.Identifier.GetLocation(),
					classSymbol.Name
				)
			);
			canGenerate = false;
		}

		var namespaceName = classSymbol.ContainingNamespace.IsGlobalNamespace
			? null
			: classSymbol.ContainingNamespace.ToDisplayString();

		var properties = new List<AggregateStatePropertyInfo>();
		var propertySymbolsByName = new Dictionary<string, IPropertySymbol>(StringComparer.Ordinal);
		foreach (var member in classSymbol.GetMembers())
		{
			ct.ThrowIfCancellationRequested();

			if (member is not IPropertySymbol propertySymbol)
				continue;

			if (propertySymbol.IsStatic || propertySymbol.IsIndexer || propertySymbol.IsImplicitlyDeclared)
				continue;

			propertySymbolsByName[propertySymbol.Name] = propertySymbol;

			if (propertySymbol.SetMethod is null)
				continue;

			properties.Add(
				new AggregateStatePropertyInfo(
					propertySymbol.Name,
					propertySymbol.Type.ToDisplayString(
						SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
							SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions
								| SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
						)
					)
				)
			);
		}

		var methods = new List<AggregateEventMethodInfo>();
		var attributedMethods = classSymbol
			.GetMembers()
			.OfType<IMethodSymbol>()
			.Where(method => HasAttribute(method, eventAttributeSymbol))
			.ToArray();
		var duplicatedEventMethodNames = attributedMethods
			.GroupBy(static method => method.Name, StringComparer.Ordinal)
			.Where(static group => group.Count() > 1)
			.ToDictionary(static group => group.Key, StringComparer.Ordinal);

		foreach (var duplicatedMethodName in duplicatedEventMethodNames.Keys)
		{
			foreach (var methodSymbol in attributedMethods.Where(method => method.Name == duplicatedMethodName))
			{
				diagnostics.Add(
					Diagnostic.Create(
						GeneratorDiagnostics.DuplicateGeneratedEventName,
						methodSymbol.Locations.FirstOrDefault(),
						methodSymbol.Name,
						classSymbol.Name,
						methodSymbol.Name + "Event"
					)
				);
			}
		}

		foreach (var methodSymbol in attributedMethods)
		{
			ct.ThrowIfCancellationRequested();

			if (duplicatedEventMethodNames.ContainsKey(methodSymbol.Name))
				continue;

			if (
				!TryCreateEventMethodInfo(
					classSymbol,
					methodSymbol,
					propertySymbolsByName,
					compilation,
					diagnostics,
					ct,
					out var methodInfo
				)
			)
			{
				continue;
			}

			methods.Add(methodInfo);
		}

		return new AggregateGenerationResult(
			canGenerate
				? new AggregateInfo(
					namespaceName,
					classSymbol.Name,
					classSymbol.DeclaredAccessibility,
					properties,
					methods,
					CreateHintName(classSymbol)
				)
				: null,
			[.. diagnostics]
		);
	}

	static EventMethodValidationResult GetStandaloneEventMethodValidationResult(GeneratorAttributeSyntaxContext ctx)
	{
		if (ctx.TargetSymbol is not IMethodSymbol methodSymbol)
			return new EventMethodValidationResult([]);

		if (
			ctx.SemanticModel.Compilation.GetTypeByMetadataName(GenerateAggregateAttributeName)
				is { } aggregateAttribute
			&& HasAttribute(methodSymbol.ContainingType, aggregateAttribute)
		)
		{
			return new EventMethodValidationResult([]);
		}

		return new EventMethodValidationResult([
			Diagnostic.Create(
				GeneratorDiagnostics.EventMethodRequiresAggregateAttribute,
				ctx.TargetNode.GetLocation(),
				methodSymbol.Name
			),
		]);
	}

	static bool TryCreateEventMethodInfo(
		INamedTypeSymbol classSymbol,
		IMethodSymbol methodSymbol,
		Dictionary<string, IPropertySymbol> propertySymbolsByName,
		Compilation compilation,
		List<Diagnostic> diagnostics,
		CancellationToken ct,
		out AggregateEventMethodInfo methodInfo
	)
	{
		methodInfo = default!;
		var hasErrors = false;
		var methodLocation = methodSymbol.Locations.FirstOrDefault();
		var methodDeclarations = methodSymbol
			.DeclaringSyntaxReferences.Select(reference => reference.GetSyntax(ct))
			.OfType<MethodDeclarationSyntax>()
			.ToArray();

		var isPartial = methodDeclarations.Any(declaration =>
			declaration.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.PartialKeyword))
		);
		if (!isPartial)
		{
			diagnostics.Add(
				Diagnostic.Create(GeneratorDiagnostics.EventMethodMustBePartial, methodLocation, methodSymbol.Name)
			);
			return false;
		}

		void ReportUnsupportedSignature(string reason)
		{
			diagnostics.Add(
				Diagnostic.Create(
					GeneratorDiagnostics.UnsupportedEventMethodSignature,
					methodLocation,
					methodSymbol.Name,
					reason
				)
			);
			hasErrors = true;
		}

		if (methodSymbol.DeclaredAccessibility != Accessibility.Public)
			ReportUnsupportedSignature("methods must be public");

		if (methodSymbol.IsStatic)
			ReportUnsupportedSignature("static methods are not supported");

		if (methodSymbol.TypeParameters.Length > 0)
			ReportUnsupportedSignature("generic methods are not supported");

		if (!methodSymbol.ReturnsVoid)
			ReportUnsupportedSignature("methods must return void");

		if (
			methodDeclarations.Any(declaration =>
				declaration.Body is not null || declaration.ExpressionBody is not null
			)
		)
			ReportUnsupportedSignature("methods must be partial declarations without a body");

		foreach (var parameter in methodSymbol.Parameters)
		{
			if (parameter.RefKind != RefKind.None)
				ReportUnsupportedSignature("ref, in, and out parameters are not supported");

			if (parameter.IsParams)
				ReportUnsupportedSignature("params parameters are not supported");
		}

		if (hasErrors)
			return false;

		var parameters = new List<EventPropertyInfo>();
		foreach (var parameter in methodSymbol.Parameters)
		{
			var propertyName = EventPropertyInfo.ToPropertyName(parameter.Name);
			var parameterLocation = parameter.Locations.FirstOrDefault() ?? methodLocation;

			if (!propertySymbolsByName.TryGetValue(propertyName, out var propertySymbol))
			{
				diagnostics.Add(
					Diagnostic.Create(
						GeneratorDiagnostics.EventParameterMustMapToWritableProperty,
						parameterLocation,
						parameter.Name,
						methodSymbol.Name,
						classSymbol.Name,
						$"no matching property named '{propertyName}' was found"
					)
				);
				hasErrors = true;
				continue;
			}

			if (propertySymbol.SetMethod is null)
			{
				diagnostics.Add(
					Diagnostic.Create(
						GeneratorDiagnostics.EventParameterMustMapToWritableProperty,
						parameterLocation,
						parameter.Name,
						methodSymbol.Name,
						classSymbol.Name,
						$"property '{propertyName}' does not have a setter"
					)
				);
				hasErrors = true;
				continue;
			}

			if (propertySymbol.SetMethod.IsInitOnly)
			{
				diagnostics.Add(
					Diagnostic.Create(
						GeneratorDiagnostics.EventParameterMustMapToWritableProperty,
						parameterLocation,
						parameter.Name,
						methodSymbol.Name,
						classSymbol.Name,
						$"property '{propertyName}' is init-only"
					)
				);
				hasErrors = true;
				continue;
			}

			var conversion = compilation.ClassifyConversion(parameter.Type, propertySymbol.Type);
			if (!conversion.Exists || !conversion.IsImplicit)
			{
				diagnostics.Add(
					Diagnostic.Create(
						GeneratorDiagnostics.EventParameterMustMapToWritableProperty,
						parameterLocation,
						parameter.Name,
						methodSymbol.Name,
						classSymbol.Name,
						$"parameter type '{parameter.Type.ToDisplayString()}' is not implicitly assignable to property '{propertyName}' of type '{propertySymbol.Type.ToDisplayString()}'"
					)
				);
				hasErrors = true;
				continue;
			}

			parameters.Add(
				new EventPropertyInfo(
					parameter.Name,
					parameter.Type.ToDisplayString(
						SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
							SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions
								| SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
						)
					)
				)
			);
		}

		if (hasErrors)
			return false;

		var version = 1;
		foreach (var attribute in methodSymbol.GetAttributes())
		{
			var attributeClass = attribute.AttributeClass;
			if (attributeClass is null || attributeClass.ToDisplayString() != GenerateAggregateEventAttributeName)
				continue;

			foreach (var namedArgument in attribute.NamedArguments)
			{
				if (namedArgument.Key == "Version" && namedArgument.Value.Value is int explicitVersion)
				{
					version = explicitVersion;
					break;
				}
			}

			break;
		}

		methodInfo = new AggregateEventMethodInfo(methodSymbol.Name, parameters, version);
		return true;
	}

	static bool HasAttribute(ISymbol symbol, INamedTypeSymbol? attributeSymbol)
	{
		if (attributeSymbol is null)
			return false;

		foreach (var attribute in symbol.GetAttributes())
		{
			if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeSymbol))
				return true;
		}

		return false;
	}

	static bool InheritsFromAggregateBase(INamedTypeSymbol classSymbol, INamedTypeSymbol aggregateBaseSymbol)
	{
		var current = classSymbol.BaseType;
		while (current is not null)
		{
			if (SymbolEqualityComparer.Default.Equals(current, aggregateBaseSymbol))
				return true;
			current = current.BaseType;
		}

		return false;
	}

	static string CreateHintName(INamedTypeSymbol classSymbol)
	{
		var symbolName = classSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
		var builder = new System.Text.StringBuilder(symbolName.Length);

		foreach (var character in symbolName)
		{
			builder.Append(char.IsLetterOrDigit(character) ? character : '_');
		}

		builder.Append('_');
		builder.Append(
			ComputeStableHash(symbolName).ToString("X16", System.Globalization.CultureInfo.InvariantCulture)
		);
		builder.Append(".g.cs");
		return builder.ToString();
	}

	static ulong ComputeStableHash(string value)
	{
		const ulong offsetBasis = 14695981039346656037;
		const ulong prime = 1099511628211;

		var hash = offsetBasis;
		foreach (var character in value)
		{
			hash ^= character;
			hash *= prime;
		}

		return hash;
	}

	static void ReportDiagnostics(SourceProductionContext context, IEnumerable<Diagnostic> diagnostics)
	{
		foreach (var diagnostic in diagnostics)
		{
			context.ReportDiagnostic(diagnostic);
		}
	}
}
