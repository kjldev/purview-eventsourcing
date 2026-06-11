using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Purview.EventSourcing.SourceGenerator.Helpers;
using Purview.EventSourcing.SourceGenerator.Templates;

namespace Purview.EventSourcing.SourceGenerator;

[Generator]
public sealed class AggregateSourceGenerator : IIncrementalGenerator, ILogSupport
{
	const string GenerateAggregateAttributeName = "Purview.EventSourcing.Aggregates.GenerateAggregateAttribute";
	const string GenerateAggregateEventAttributeName =
		"Purview.EventSourcing.Aggregates.GenerateAggregateEventAttribute";
	const string AggregateBaseMetadataName = "Purview.EventSourcing.Aggregates.AggregateBase";
	const string ScalarAttributeMetadataName = "Purview.EventSourcing.Serialization.ScalarAttribute";
	const string ValueObjectContextMetadataName = "Purview.EventSourcing.ValueObjects.ValueObjectContext`1";
	const int HintNameHashHexLength = 16;
	const string GeneratedSourceFileSuffix = ".g.cs";

	static readonly int HintNameSeparatorAndSuffixLength = 1 + HintNameHashHexLength + GeneratedSourceFileSuffix.Length;

	GenerationLogger? _logger;

	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		// Register the attribute templates as post-initialization output so they're
		// available to consuming projects without needing a reference to the core library.
		context.RegisterPostInitializationOutput(ctx =>
		{
			_logger?.Debug("Adding attribute definitions to compilation");

			ctx.AddEmbeddedAttributeDefinition();
			ctx.AddSource(
				"GenerateAggregateAttribute.g.cs",
				EmbeddedResources.LoadTemplate("GenerateAggregateAttribute")
			);
			ctx.AddSource(
				"GenerateAggregateEventAttribute.g.cs",
				EmbeddedResources.LoadTemplate("GenerateAggregateEventAttribute")
			);
			//ctx.AddSource(
			//    "AggregateValidationAttribute.g.cs",
			//    EmbeddedResources.LoadTemplate("AggregateValidationAttribute")
			//);
		});

		// Opt-out: set <DisableEventSourcingSourceGenerator>true</DisableEventSourcingSourceGenerator> to skip generation.
		var isDisabled = context.AnalyzerConfigOptionsProvider.Select(
			(opts, _) =>
			{
				opts.GlobalOptions.TryGetValue("build_property.DisableEventSourcingSourceGenerator", out var val);
				var isDisabled = string.Equals(val, "true", StringComparison.OrdinalIgnoreCase);

				if (isDisabled)
					_logger?.Debug("EventSourcingSourceGenerator is disabled via MSBuild property");

				return isDisabled;
			}
		);

		// Find all class declarations decorated with [GenerateAggregate]
		var aggregateClasses = context
			.SyntaxProvider.ForAttributeWithMetadataName(
				GenerateAggregateAttributeName,
				predicate: static (node, _) => node is ClassDeclarationSyntax,
				transform: static (ctx, ct) => GetAggregateGenerationResult(ctx, ct)
			)
			.Collect();

		var standaloneEventMethods = context
			.SyntaxProvider.ForAttributeWithMetadataName(
				GenerateAggregateEventAttributeName,
				predicate: static (node, _) => node is MethodDeclarationSyntax,
				transform: static (ctx, _) => GetStandaloneEventMethodValidationResult(ctx)
			)
			.Collect();

		context.RegisterSourceOutput(
			isDisabled.Combine(aggregateClasses),
			(spc, data) =>
			{
				var (isDisabled, aggregateResults) = data;
				if (isDisabled)
					return;

				foreach (var result in aggregateResults)
				{
					ReportDiagnostics(spc, result.Diagnostics, _logger);

					if (result.Info is null)
						return;

					var source = EmitHelper.GenerateAggregateSource(result.Info, _logger);
					spc.AddSource(result.Info.HintName, source);
				}
			}
		);

		context.RegisterSourceOutput(
			isDisabled.Combine(standaloneEventMethods),
			(spc, result) =>
			{
				var (isDisabled, validationResults) = result;
				if (isDisabled)
					return;

				foreach (var validationResult in validationResults)
					ReportDiagnostics(spc, validationResult.Diagnostics, _logger);
			}
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
		var aggregateEventNamespaceOverride = GetAttributeStringNamedArgument(
			classSymbol.GetAttributes(),
			GenerateAggregateAttributeName,
			"EventNamespace"
		);

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

			if (propertySymbol.SetMethod.DeclaredAccessibility is not Accessibility.Private)
			{
				diagnostics.Add(
					Diagnostic.Create(
						GeneratorDiagnostics.AggregatePropertySetterShouldBePrivate,
						propertySymbol.SetMethod.Locations.FirstOrDefault()
							?? propertySymbol.Locations.FirstOrDefault(),
						propertySymbol.Name,
						classSymbol.Name,
						propertySymbol.SetMethod.DeclaredAccessibility.ToString()
					)
				);
			}

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
		var invalidMethods = new List<InvalidAggregateEventMethodInfo>();
		var attributedMethods = classSymbol
			.GetMembers()
			.OfType<IMethodSymbol>()
			.Where(method => HasAttribute(method, eventAttributeSymbol))
			.ToArray();
		var methodsByEventType = new Dictionary<(string EventNamespace, string EventName), IMethodSymbol>();

		foreach (var methodSymbol in attributedMethods)
		{
			ct.ThrowIfCancellationRequested();

			var diagnosticsStart = diagnostics.Count;

			if (
				!TryCreateEventMethodInfo(
					classSymbol,
					methodSymbol,
					propertySymbolsByName,
					compilation,
					namespaceName,
					aggregateEventNamespaceOverride,
					diagnostics,
					ct,
					out var methodInfo
				)
			)
			{
				var diagnosticIds = diagnostics
					.Skip(diagnosticsStart)
					.Select(static diagnostic => diagnostic.Id)
					.Distinct(StringComparer.Ordinal)
					.OrderBy(static id => id, StringComparer.Ordinal)
					.ToArray();

				if (TryCreateInvalidMethodStub(methodSymbol, diagnosticIds, out var invalidMethod, ct))
					invalidMethods.Add(invalidMethod);

				continue;
			}

			var eventTypeKey = (methodInfo.EventNamespace, methodInfo.EventName);
			if (methodsByEventType.TryGetValue(eventTypeKey, out var conflictingMethod))
			{
				diagnostics.Add(
					Diagnostic.Create(
						GeneratorDiagnostics.DuplicateGeneratedEventName,
						methodSymbol.Locations.FirstOrDefault(),
						methodSymbol.Name,
						classSymbol.Name,
						$"{methodInfo.EventNamespace}.{methodInfo.EventName}"
					)
				);
				diagnostics.Add(
					Diagnostic.Create(
						GeneratorDiagnostics.DuplicateGeneratedEventName,
						conflictingMethod.Locations.FirstOrDefault(),
						conflictingMethod.Name,
						classSymbol.Name,
						$"{methodInfo.EventNamespace}.{methodInfo.EventName}"
					)
				);

				if (
					TryCreateInvalidMethodStub(
						methodSymbol,
						[GeneratorDiagnostics.DuplicateGeneratedEventName.Id],
						out var invalidMethod,
						ct
					)
				)
					invalidMethods.Add(invalidMethod);

				continue;
			}

			methodsByEventType[eventTypeKey] = methodSymbol;
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
					invalidMethods,
					CreateHintName(classSymbol)
				)
				: null,
			[.. diagnostics]
		);
	}

	static EventMethodValidationResult GetStandaloneEventMethodValidationResult(GeneratorAttributeSyntaxContext ctx)
	{
		return ctx.TargetSymbol is not IMethodSymbol methodSymbol ? new EventMethodValidationResult([])
			: ctx.SemanticModel.Compilation.GetTypeByMetadataName(GenerateAggregateAttributeName)
				is { } aggregateAttribute
			&& HasAttribute(methodSymbol.ContainingType, aggregateAttribute)
				? new EventMethodValidationResult([])
			: new EventMethodValidationResult([
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
		string? aggregateNamespace,
		string? aggregateEventNamespaceOverride,
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

		if (methodSymbol.IsStatic)
			ReportUnsupportedSignature("static methods are not supported");

		if (methodSymbol.TypeParameters.Length > 0)
			ReportUnsupportedSignature("generic methods are not supported");

		if (!TryResolveReturnKind(methodSymbol, classSymbol, out var returnTypeName, out var returnKind))
		{
			ReportUnsupportedSignature("methods must return void, bool, or the containing aggregate type");
		}

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
				var parameterTypeName = parameter.Type.ToDisplayString(
					SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
						SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions
							| SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
					)
				);
				parameters.Add(
					new EventPropertyInfo(
						parameter.Name,
						parameterTypeName,
						parameterTypeName,
						propertyName,
						false,
						parameterTypeName,
						parameter.Type.SpecialType == SpecialType.System_String,
						EventParameterConversionKind.None
					)
				);
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

			var conversionKind = ResolveParameterConversionKind(
				compilation,
				classSymbol,
				parameter.Type,
				propertySymbol.Type
			);
			if (conversionKind is null)
			{
				diagnostics.Add(
					Diagnostic.Create(
						GeneratorDiagnostics.EventParameterMustMapToWritableProperty,
						parameterLocation,
						parameter.Name,
						methodSymbol.Name,
						classSymbol.Name,
						$"parameter type '{parameter.Type.ToDisplayString()}' cannot be mapped to property '{propertyName}' of type '{propertySymbol.Type.ToDisplayString()}' via implicit conversion or value-object Create(...)"
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
					),
					propertySymbol.Type.ToDisplayString(
						SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
							SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions
								| SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
						)
					),
					propertySymbol.Name,
					true,
					propertySymbol.Type.ToDisplayString(
						SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
							SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions
								| SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
						)
					),
					parameter.Type.SpecialType == SpecialType.System_String
						&& propertySymbol.Type.SpecialType == SpecialType.System_String,
					conversionKind.Value
				)
			);
		}

		if (hasErrors)
			return false;

		var version = 1;
		var eventName = methodSymbol.Name + "Event";
		string? eventNamespaceOverride = null;
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
					continue;
				}

				if (namedArgument.Key == "EventName" && namedArgument.Value.Value is string explicitEventName)
				{
					eventName = explicitEventName;
					continue;
				}

				if (namedArgument.Key == "EventNamespace" && namedArgument.Value.Value is string explicitEventNamespace)
					eventNamespaceOverride = explicitEventNamespace;
			}

			break;
		}

		var eventNamespace = string.IsNullOrWhiteSpace(eventNamespaceOverride)
			? string.IsNullOrWhiteSpace(aggregateEventNamespaceOverride)
				? CreateDefaultEventNamespace(aggregateNamespace, classSymbol.Name)
				: aggregateEventNamespaceOverride!.Trim()
			: eventNamespaceOverride!.Trim();

		methodInfo = new AggregateEventMethodInfo(
			methodSymbol.Name,
			eventName,
			eventNamespace,
			parameters,
			returnTypeName,
			returnKind,
			methodSymbol.DeclaredAccessibility,
			version
		);
		return true;
	}

	static EventParameterConversionKind? ResolveParameterConversionKind(
		Compilation compilation,
		INamedTypeSymbol aggregateType,
		ITypeSymbol parameterType,
		ITypeSymbol propertyType
	)
	{
		if (SymbolEqualityComparer.Default.Equals(parameterType, propertyType))
			return EventParameterConversionKind.None;

		if (
			propertyType is INamedTypeSymbol namedPropertyType
			&& TryResolveValueObjectCreateConversion(
				compilation,
				aggregateType,
				namedPropertyType,
				parameterType,
				out var createConversionKind
			)
		)
		{
			return createConversionKind;
		}

		var conversion = compilation.ClassifyConversion(parameterType, propertyType);
		return conversion.Exists && conversion.IsImplicit ? EventParameterConversionKind.Implicit : null;
	}

	static bool TryResolveValueObjectCreateConversion(
		Compilation compilation,
		INamedTypeSymbol aggregateType,
		INamedTypeSymbol propertyType,
		ITypeSymbol parameterType,
		out EventParameterConversionKind conversionKind
	)
	{
		conversionKind = EventParameterConversionKind.None;

		var contextTypeDefinition = compilation.GetTypeByMetadataName(ValueObjectContextMetadataName);

		var hasScalarAttribute = propertyType
			.GetAttributes()
			.Any(attribute => attribute.AttributeClass?.ToDisplayString() == ScalarAttributeMetadataName);

		var hasContextualCreate = propertyType
			.GetMembers("Create")
			.OfType<IMethodSymbol>()
			.Any(method =>
				IsContextualCreateMethod(method, propertyType, aggregateType, parameterType, contextTypeDefinition)
			);

		if (hasContextualCreate)
		{
			conversionKind = EventParameterConversionKind.ContextualCreate;
			return true;
		}

		var hasSimpleCreate = propertyType
			.GetMembers("Create")
			.OfType<IMethodSymbol>()
			.Any(method => IsSimpleCreateMethod(method, propertyType, parameterType));

		if (hasSimpleCreate || hasScalarAttribute)
		{
			conversionKind = EventParameterConversionKind.Create;
			return true;
		}

		return false;
	}

	static bool IsSimpleCreateMethod(IMethodSymbol method, ITypeSymbol returnType, ITypeSymbol parameterType)
	{
		if (!method.IsStatic || method.DeclaredAccessibility != Accessibility.Public || method.Name != "Create")
			return false;

		if (method.Parameters.Length != 1)
			return false;

		return SymbolEqualityComparer.Default.Equals(method.ReturnType, returnType)
			&& SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, parameterType);
	}

	static bool IsContextualCreateMethod(
		IMethodSymbol method,
		ITypeSymbol returnType,
		INamedTypeSymbol aggregateType,
		ITypeSymbol parameterType,
		INamedTypeSymbol? contextTypeDefinition
	)
	{
		if (!method.IsStatic || method.DeclaredAccessibility != Accessibility.Public || method.Name != "Create")
			return false;

		if (method.Parameters.Length != 2)
			return false;

		if (!SymbolEqualityComparer.Default.Equals(method.ReturnType, returnType))
			return false;

		if (!SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, parameterType))
			return false;

		var contextParameter = method.Parameters[1];
		if (contextParameter.RefKind != RefKind.In)
			return false;

		if (
			contextTypeDefinition is null
			|| contextParameter.Type is not INamedTypeSymbol contextType
			|| !SymbolEqualityComparer.Default.Equals(contextType.OriginalDefinition, contextTypeDefinition)
			|| contextType.TypeArguments.Length != 1
		)
		{
			return false;
		}

		return SymbolEqualityComparer.Default.Equals(contextType.TypeArguments[0], aggregateType);
	}

	static string? GetAttributeStringNamedArgument(
		ImmutableArray<AttributeData> attributes,
		string attributeMetadataName,
		string argumentName
	)
	{
		foreach (var attribute in attributes)
		{
			var attributeClass = attribute.AttributeClass;
			if (attributeClass is null || attributeClass.ToDisplayString() != attributeMetadataName)
				continue;

			foreach (var namedArgument in attribute.NamedArguments)
			{
				if (namedArgument.Key == argumentName && namedArgument.Value.Value is string value)
					return value;
			}
		}

		return null;
	}

	static string CreateDefaultEventNamespace(string? aggregateNamespace, string aggregateClassName)
	{
		var aggregateNameWithoutSuffix = aggregateClassName;
		if (aggregateClassName.EndsWith("Aggregate", StringComparison.Ordinal))
		{
			aggregateNameWithoutSuffix = aggregateClassName.Substring(
				0,
				aggregateClassName.Length - "Aggregate".Length
			);
		}

		if (string.IsNullOrEmpty(aggregateNameWithoutSuffix))
			aggregateNameWithoutSuffix = aggregateClassName;

		return string.IsNullOrEmpty(aggregateNamespace)
			? aggregateNameWithoutSuffix
			: $"{aggregateNamespace}.{aggregateNameWithoutSuffix}Events";
	}

	static bool TryResolveReturnKind(
		IMethodSymbol methodSymbol,
		INamedTypeSymbol classSymbol,
		out string returnTypeName,
		out EventMethodReturnKind returnKind
	)
	{
		returnTypeName = "void";
		returnKind = EventMethodReturnKind.Void;

		if (methodSymbol.ReturnsVoid)
			return true;

		if (methodSymbol.ReturnType.SpecialType == SpecialType.System_Boolean)
		{
			returnTypeName = "bool";
			returnKind = EventMethodReturnKind.Bool;
			return true;
		}

		if (SymbolEqualityComparer.Default.Equals(methodSymbol.ReturnType, classSymbol))
		{
			returnTypeName = classSymbol.Name;
			returnKind = EventMethodReturnKind.Aggregate;
			return true;
		}

		return false;
	}

	static bool TryCreateInvalidMethodStub(
		IMethodSymbol methodSymbol,
		string[] diagnosticIds,
		out InvalidAggregateEventMethodInfo methodInfo,
		CancellationToken ct
	)
	{
		ct.ThrowIfCancellationRequested();
		methodInfo = null!;

		var declaration = methodSymbol
			.DeclaringSyntaxReferences.Select(reference => reference.GetSyntax(ct))
			.OfType<MethodDeclarationSyntax>()
			.FirstOrDefault(static syntax =>
				syntax.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.PartialKeyword))
				&& syntax.Body is null
				&& syntax.ExpressionBody is null
			);

		if (declaration is null)
			return false;

		var modifiers = string.Join(" ", declaration.Modifiers.Select(static modifier => modifier.Text));
		if (modifiers.Length > 0)
			modifiers += " ";

		var explicitInterfaceSpecifier = declaration.ExplicitInterfaceSpecifier?.ToString() ?? string.Empty;
		var typeParameterList = declaration.TypeParameterList?.ToString() ?? string.Empty;
		var constraints =
			declaration.ConstraintClauses.Count == 0
				? string.Empty
				: " " + string.Join(" ", declaration.ConstraintClauses.Select(static clause => clause.ToString()));

		methodInfo = new InvalidAggregateEventMethodInfo(
			$"{modifiers}{declaration.ReturnType} {explicitInterfaceSpecifier}{declaration.Identifier}{typeParameterList}{declaration.ParameterList}{constraints}",
			diagnosticIds
		);
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
		var shortName = classSymbol.Name;
		var builder = new System.Text.StringBuilder(shortName.Length + HintNameSeparatorAndSuffixLength);

		foreach (var character in shortName)
		{
			builder.Append(char.IsLetterOrDigit(character) ? character : '_');
		}

		builder.Append('_');
		builder.Append(
			ComputeStableHash(symbolName).ToString($"X{HintNameHashHexLength}", CultureInfo.InvariantCulture)
		);
		builder.Append(GeneratedSourceFileSuffix);
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

	static void ReportDiagnostics(
		SourceProductionContext context,
		IEnumerable<Diagnostic> diagnostics,
		GenerationLogger? logger
	)
	{
		foreach (var diagnostic in diagnostics)
		{
			context.ReportDiagnostic(diagnostic);

			logger?.Diagnostic(diagnostic.GetMessage(CultureInfo.InvariantCulture));
		}
	}

	void ILogSupport.SetLogOutput(Action<string, OutputType> action) => _logger = new GenerationLogger(action);
}
