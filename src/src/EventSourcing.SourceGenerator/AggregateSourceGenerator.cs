using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Purview.EventSourcing.SourceGenerator.Helpers;
using Purview.EventSourcing.SourceGenerator.Templates;

namespace Purview.EventSourcing.SourceGenerator;

[Generator]
public sealed class AggregateSourceGenerator : IIncrementalGenerator, ILogSupport
{
	const string GenerateAggregateAttributeName = "Purview.EventSourcing.Aggregates.GenerateAggregateAttribute";
	const string GenerateAggregateDefaultsAttributeName =
		"Purview.EventSourcing.Aggregates.GenerateAggregateDefaultsAttribute";
	const string GenerateAggregateEventAttributeName =
		"Purview.EventSourcing.Aggregates.GenerateAggregateEventAttribute";
	const string GenerateAggregateCollectionEventAttributeName =
		"Purview.EventSourcing.Aggregates.GenerateAggregateCollectionEventAttribute";
	const string AggregatePropertyAttributeMetadataName = "Purview.EventSourcing.Aggregates.AggregatePropertyAttribute";
	const string MetadataAttributeMetadataName = "Purview.EventSourcing.Aggregates.MetadataAttribute";
	const string ComputedAttributeMetadataName = "Purview.EventSourcing.Aggregates.ComputedAttribute";
	const string AggregateBaseMetadataName = "Purview.EventSourcing.Aggregates.AggregateBase";
	const string EventBaseMetadataName = "Purview.EventSourcing.Aggregates.Events.EventBase";
	const string IEventMetadataName = "Purview.EventSourcing.Aggregates.Events.IEvent";
	const string ScalarAttributeMetadataName = "Purview.EventSourcing.Serialization.ScalarAttribute";
	const string ValueObjectContextMetadataName = "Purview.EventSourcing.ValueObjects.ValueObjectContext`1";
	const string EventStoreListMetadataName = "Purview.EventSourcing.EventStoreList<T>";
	const string EventStoreSetMetadataName = "Purview.EventSourcing.EventStoreSet<T>";
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
				"AggregatePropertyAttribute.g.cs",
				EmbeddedResources.LoadTemplate("AggregatePropertyAttribute")
			);
			ctx.AddSource(
				"GenerateAggregateAttribute.g.cs",
				EmbeddedResources.LoadTemplate("GenerateAggregateAttribute")
			);
			ctx.AddSource(
				"GenerateAggregateCollectionEventAttribute.g.cs",
				EmbeddedResources.LoadTemplate("GenerateAggregateCollectionEventAttribute")
			);
			ctx.AddSource(
				"GenerateAggregateDefaultsAttribute.g.cs",
				EmbeddedResources.LoadTemplate("GenerateAggregateDefaultsAttribute")
			);
			ctx.AddSource(
				"GenerateAggregateDefaultBaseAttribute.g.cs",
				EmbeddedResources.LoadTemplate("GenerateAggregateDefaultBaseAttribute")
			);
			ctx.AddSource(
				"GenerateAggregateEventAttribute.g.cs",
				EmbeddedResources.LoadTemplate("GenerateAggregateEventAttribute")
			);
			ctx.AddSource("MetadataAttribute.g.cs", EmbeddedResources.LoadTemplate("MetadataAttribute"));
			ctx.AddSource("ComputedAttribute.g.cs", EmbeddedResources.LoadTemplate("ComputedAttribute"));
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

		var manualEventTypes = context.SyntaxProvider.CreateSyntaxProvider(
			predicate: static (node, _) =>
				node is TypeDeclarationSyntax typeDeclaration && typeDeclaration.BaseList is not null,
			transform: static (ctx, _) => GetEventTypeValidationResult(ctx)
		);

		var computedParameterInvocations = context.SyntaxProvider.CreateSyntaxProvider(
			predicate: static (node, _) => node is InvocationExpressionSyntax,
			transform: static (ctx, ct) => GetComputedParameterInvocationValidationResult(ctx, ct)
		);

		var nullableScalarNullComparisons = context.SyntaxProvider.CreateSyntaxProvider(
			predicate: static (node, _) =>
				node is BinaryExpressionSyntax binary
				&& (binary.IsKind(SyntaxKind.EqualsExpression) || binary.IsKind(SyntaxKind.NotEqualsExpression)),
			transform: static (ctx, ct) => GetNullableScalarNullComparisonValidationResult(ctx, ct)
		);

		context.RegisterSourceOutput(
			aggregateClasses.Combine(isDisabled),
			(spc, data) =>
			{
				var (result, disabled) = data;
				if (disabled)
					return;

				ReportDiagnostics(spc, result.Diagnostics, _logger);

				if (result.Info is null)
					return;

				var source = EmitHelper.GenerateAggregateSource(result.Info, _logger);
				spc.AddSource(result.Info.HintName, source);
			}
		);

		context.RegisterSourceOutput(
			standaloneEventMethods.Combine(isDisabled),
			(spc, result) =>
			{
				var (validationResult, disabled) = result;
				if (disabled)
					return;

				ReportDiagnostics(spc, validationResult.Diagnostics, _logger);
			}
		);

		context.RegisterSourceOutput(
			manualEventTypes.Combine(isDisabled),
			(spc, result) =>
			{
				var (validationResult, disabled) = result;
				if (disabled)
					return;

				ReportDiagnostics(spc, validationResult.Diagnostics, _logger);
			}
		);

		context.RegisterSourceOutput(
			computedParameterInvocations.Combine(isDisabled),
			(spc, result) =>
			{
				var (validationResult, disabled) = result;
				if (disabled)
					return;

				ReportDiagnostics(spc, validationResult.Diagnostics, _logger);
			}
		);

		context.RegisterSourceOutput(
			nullableScalarNullComparisons.Combine(isDisabled),
			(spc, result) =>
			{
				var (validationResult, disabled) = result;
				if (disabled)
					return;

				ReportDiagnostics(spc, validationResult.Diagnostics, _logger);
			}
		);
	}

	static EventMethodValidationResult GetComputedParameterInvocationValidationResult(
		GeneratorSyntaxContext context,
		CancellationToken ct
	)
	{
		ct.ThrowIfCancellationRequested();
		if (context.Node is not InvocationExpressionSyntax invocation)
			return new([]);

		if (context.SemanticModel.GetOperation(invocation, ct) is not IInvocationOperation invocationOperation)
			return new([]);

		var diagnostics = new List<Diagnostic>();
		foreach (var argument in invocationOperation.Arguments)
		{
			ct.ThrowIfCancellationRequested();
			if (argument.IsImplicit || argument.Parameter is null)
				continue;

			if (!HasComputedAttribute(argument.Parameter))
				continue;

			diagnostics.Add(
				Diagnostic.Create(
					GeneratorDiagnostics.ComputedParameterCannotBeSetByCaller,
					argument.Syntax.GetLocation(),
					invocationOperation.TargetMethod.Name,
					argument.Parameter.Name
				)
			);
		}

		return new([.. diagnostics]);
	}

	static EventMethodValidationResult GetNullableScalarNullComparisonValidationResult(
		GeneratorSyntaxContext context,
		CancellationToken ct
	)
	{
		ct.ThrowIfCancellationRequested();
		if (context.Node is not BinaryExpressionSyntax binaryExpression)
			return new([]);

		if (
			!binaryExpression.IsKind(SyntaxKind.EqualsExpression)
			&& !binaryExpression.IsKind(SyntaxKind.NotEqualsExpression)
		)
			return new([]);

		var leftIsNull = binaryExpression.Left.IsKind(SyntaxKind.NullLiteralExpression);
		var rightIsNull = binaryExpression.Right.IsKind(SyntaxKind.NullLiteralExpression);
		if (leftIsNull == rightIsNull)
			return new([]);

		var enclosingType = context.SemanticModel.GetEnclosingSymbol(binaryExpression.SpanStart, ct)?.ContainingType;
		if (enclosingType is null || !HasAttribute(enclosingType, GenerateAggregateAttributeName))
			return new([]);

		var checkedSide = leftIsNull ? binaryExpression.Right : binaryExpression.Left;
		if (
			context.SemanticModel.GetTypeInfo(checkedSide, ct).Type is not INamedTypeSymbol checkedType
			|| !checkedType.IsGenericType
			|| checkedType.OriginalDefinition.SpecialType != SpecialType.System_Nullable_T
		)
			return new([]);

		if (checkedType.TypeArguments[0] is not INamedTypeSymbol underlyingType)
			return new([]);

		var isScalarValueObject = underlyingType
			.GetAttributes()
			.Any(attribute => attribute.AttributeClass?.ToDisplayString() == ScalarAttributeMetadataName);
		if (!isScalarValueObject)
			return new([]);

		var recommendedComparison = binaryExpression.IsKind(SyntaxKind.EqualsExpression)
			? $"{checkedSide} is null"
			: $"{checkedSide} is not null";

		return new([
			Diagnostic.Create(
				GeneratorDiagnostics.NullableScalarEqualityNullComparisonShouldUsePatternMatching,
				binaryExpression.GetLocation(),
				binaryExpression.ToString(),
				recommendedComparison
			),
		]);
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
		var shouldDeclareAggregateBase = false;
		var compilation = ctx.SemanticModel.Compilation;
		var aggregateBaseSymbol = compilation.GetTypeByMetadataName(AggregateBaseMetadataName);
		var eventAttributeSymbol = compilation.GetTypeByMetadataName(GenerateAggregateEventAttributeName);
		var collectionEventAttributeSymbol = compilation.GetTypeByMetadataName(
			GenerateAggregateCollectionEventAttributeName
		);
		var valueObjectContextType = compilation.GetTypeByMetadataName(ValueObjectContextMetadataName);

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

		if (aggregateBaseSymbol is null)
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
		else if (!InheritsFromAggregateBase(classSymbol, aggregateBaseSymbol))
		{
			if (HasNoDeclaredBaseClass(classSymbol))
			{
				shouldDeclareAggregateBase = true;
			}
			else
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
		var aggregateEventSuffixOverride = GetAttributeStringNamedArgument(
			classSymbol.GetAttributes(),
			GenerateAggregateAttributeName,
			"EventSuffix"
		);
		var assemblyEventSuffix = GetAttributeStringNamedArgument(
			compilation.Assembly.GetAttributes(),
			GenerateAggregateDefaultsAttributeName,
			"EventSuffix"
		);

		var properties = new List<AggregateStatePropertyInfo>();
		var propertySymbolsByName = new Dictionary<string, IPropertySymbol>(StringComparer.Ordinal);
		var attributedMethods = new List<IMethodSymbol>();
		foreach (var member in classSymbol.GetMembers())
		{
			ct.ThrowIfCancellationRequested();

			if (member is IPropertySymbol propertySymbol)
			{
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

				if (IsCollectionLikeType(propertySymbol.Type) && !IsEventStoreCollectionType(propertySymbol.Type))
				{
					diagnostics.Add(
						Diagnostic.Create(
							GeneratorDiagnostics.AggregatePropertyCollectionTypeMustUseEventStoreCollections,
							propertySymbol.Locations.FirstOrDefault(),
							propertySymbol.Name,
							classSymbol.Name,
							propertySymbol.Type.ToDisplayString()
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
				continue;
			}

			if (
				member is IMethodSymbol methodSymbol
				&& (
					HasAttribute(methodSymbol, eventAttributeSymbol)
					|| HasAttribute(methodSymbol, collectionEventAttributeSymbol)
				)
			)
				attributedMethods.Add(methodSymbol);
		}

		var methods = new List<AggregateEventMethodInfo>();
		var invalidMethods = new List<InvalidAggregateEventMethodInfo>();
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
					valueObjectContextType,
					namespaceName,
					aggregateEventNamespaceOverride,
					aggregateEventSuffixOverride,
					assemblyEventSuffix,
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
					shouldDeclareAggregateBase,
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
			: HasAttribute(methodSymbol.ContainingType, GenerateAggregateAttributeName)
				? new EventMethodValidationResult([])
			: new EventMethodValidationResult([
				Diagnostic.Create(
					GeneratorDiagnostics.EventMethodRequiresAggregateAttribute,
					ctx.TargetNode.GetLocation(),
					methodSymbol.Name
				),
			]);
	}

	static EventTypeValidationResult GetEventTypeValidationResult(GeneratorSyntaxContext ctx)
	{
		if (ctx.Node is not TypeDeclarationSyntax typeDeclaration)
			return new EventTypeValidationResult([]);

		if (
			ctx.SemanticModel.GetDeclaredSymbol(typeDeclaration) is not INamedTypeSymbol typeSymbol
			|| typeSymbol.IsAbstract
		)
			return new EventTypeValidationResult([]);

		if (!IsEventType(typeSymbol))
			return new EventTypeValidationResult([]);

		var displayName = GetDisplayEventName(typeSymbol.Name);
		return EventVerbMap.IsPastTenseEventName(displayName)
			? new EventTypeValidationResult([])
			: new EventTypeValidationResult([
				Diagnostic.Create(
					GeneratorDiagnostics.EventNameShouldBePastTense,
					typeDeclaration.GetLocation(),
					displayName
				),
			]);
	}

	static bool TryCreateEventMethodInfo(
		INamedTypeSymbol classSymbol,
		IMethodSymbol methodSymbol,
		Dictionary<string, IPropertySymbol> propertySymbolsByName,
		Compilation compilation,
		INamedTypeSymbol? valueObjectContextType,
		string? aggregateNamespace,
		AttributeStringValue aggregateEventNamespaceOverride,
		AttributeStringValue aggregateEventSuffixOverride,
		AttributeStringValue assemblyEventSuffix,
		List<Diagnostic> diagnostics,
		CancellationToken ct,
		out AggregateEventMethodInfo methodInfo
	)
	{
		var eventSuffix = (
			aggregateEventSuffixOverride.IsPresent ? aggregateEventSuffixOverride.Value
			: assemblyEventSuffix.IsPresent ? assemblyEventSuffix.Value
			: "Event"
		)?.Trim();

		methodInfo = default!;
		var hasErrors = false;
		var methodLocation = methodSymbol.Locations.FirstOrDefault();
		var eventAttribute = methodSymbol
			.GetAttributes()
			.FirstOrDefault(attribute =>
				attribute.AttributeClass?.ToDisplayString() == GenerateAggregateEventAttributeName
			);
		var collectionEventAttribute = methodSymbol
			.GetAttributes()
			.FirstOrDefault(attribute =>
				attribute.AttributeClass?.ToDisplayString() == GenerateAggregateCollectionEventAttributeName
			);
		var isCollectionEvent = collectionEventAttribute is not null;
		var manualApply = false;

		if (eventAttribute is not null && collectionEventAttribute is not null)
		{
			diagnostics.Add(
				Diagnostic.Create(
					GeneratorDiagnostics.UnsupportedEventMethodSignature,
					methodLocation,
					methodSymbol.Name,
					"methods cannot combine [GenerateAggregateEvent] and [GenerateAggregateCollectionEvent]"
				)
			);
			return false;
		}

		if (!methodSymbol.IsPartialDefinition)
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

		if (methodSymbol.DeclaredAccessibility == Accessibility.Public && !EventVerbMap.IsVerbPhrase(methodSymbol.Name))
		{
			diagnostics.Add(
				Diagnostic.Create(
					GeneratorDiagnostics.AggregateMethodShouldBeVerbPhrase,
					methodLocation,
					methodSymbol.Name
				)
			);
		}

		if (methodSymbol.IsStatic)
			ReportUnsupportedSignature("static methods are not supported");

		if (methodSymbol.TypeParameters.Length > 0)
			ReportUnsupportedSignature("generic methods are not supported");

		if (!TryResolveReturnKind(methodSymbol, classSymbol, out var returnTypeName, out var returnKind))
		{
			ReportUnsupportedSignature("methods must return void, bool, or the containing aggregate type");
		}

		if (methodSymbol.PartialImplementationPart is not null)
			ReportUnsupportedSignature("methods must be partial declarations without a body");

		foreach (var parameter in methodSymbol.Parameters)
		{
			if (parameter.RefKind != RefKind.None)
				ReportUnsupportedSignature("ref, in, and out parameters are not supported");

			if (parameter.IsParams && (!isCollectionEvent || parameter.Type is not IArrayTypeSymbol))
				ReportUnsupportedSignature("params parameters are not supported");
		}

		if (isCollectionEvent)
		{
			if (methodSymbol.Parameters.Length != 1)
				ReportUnsupportedSignature("collection event methods must have exactly one parameter");
		}

		if (hasErrors)
			return false;

		var version = 1;
		var eventName = string.Empty;
		var hasExplicitEventName = false;
		string? eventNamespaceOverride = null;
		AttributeData? activeAttribute = collectionEventAttribute ?? eventAttribute;
		if (activeAttribute is not null)
		{
			foreach (var namedArgument in activeAttribute.NamedArguments)
			{
				if (namedArgument.Key == "Version" && namedArgument.Value.Value is int explicitVersion)
				{
					version = explicitVersion;
					continue;
				}

				if (namedArgument.Key == "EventName" && namedArgument.Value.Value is string explicitEventName)
				{
					eventName = explicitEventName.Trim();
					hasExplicitEventName = true;
					continue;
				}

				if (namedArgument.Key == "EventNamespace" && namedArgument.Value.Value is string explicitEventNamespace)
				{
					eventNamespaceOverride = explicitEventNamespace;
					continue;
				}

				if (namedArgument.Key == "Manual" && namedArgument.Value.Value is bool explicitManual)
					manualApply = explicitManual;
			}
		}

		var parameters = new List<EventPropertyInfo>();
		CollectionEventInfo? collectionEvent = null;
		if (isCollectionEvent)
		{
			if (
				!TryCreateCollectionEventInfo(
					methodSymbol,
					collectionEventAttribute!,
					propertySymbolsByName,
					diagnostics,
					out var collectionParameter,
					out collectionEvent
				)
			)
			{
				return false;
			}

			parameters.Add(collectionParameter);
		}
		else
		{
			foreach (var parameter in methodSymbol.Parameters)
			{
				ct.ThrowIfCancellationRequested();

				var aggregatePropertyName =
					GetAggregatePropertyNameOverride(parameter) ?? EventPropertyInfo.ToPropertyName(parameter.Name);
				var parameterLocation = parameter.Locations.FirstOrDefault() ?? methodLocation;
				var isComputedParameter = HasComputedAttribute(parameter);
				var parameterTypeName = parameter.Type.ToDisplayString(
					SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
						SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions
							| SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
					)
				);

				if (TryGetMetadataStoreSetting(parameter, out var storeMetadata))
				{
					if (isComputedParameter)
					{
						diagnostics.Add(
							Diagnostic.Create(
								GeneratorDiagnostics.EventParameterMustMapToWritableProperty,
								parameterLocation,
								parameter.Name,
								methodSymbol.Name,
								classSymbol.Name,
								"parameter cannot be marked with both [Metadata] and [Computed]"
							)
						);
						hasErrors = true;
						continue;
					}

					parameters.Add(
						new EventPropertyInfo(
							parameter.Name,
							parameterTypeName,
							parameterTypeName,
							aggregatePropertyName,
							false,
							storeMetadata,
							parameterTypeName,
							parameter.Type.SpecialType == SpecialType.System_String,
							EventParameterConversionKind.None,
							isComputed: false
						)
					);
					continue;
				}

				if (
					manualApply
					&& (
						!propertySymbolsByName.TryGetValue(aggregatePropertyName, out var manualMappedProperty)
						|| manualMappedProperty.SetMethod is null
						|| manualMappedProperty.SetMethod.IsInitOnly
					)
				)
				{
					parameters.Add(
						new EventPropertyInfo(
							parameter.Name,
							parameterTypeName,
							parameterTypeName,
							aggregatePropertyName,
							false,
							true,
							parameterTypeName,
							parameter.Type.SpecialType == SpecialType.System_String,
							EventParameterConversionKind.None,
							isComputed: isComputedParameter
						)
					);
					continue;
				}

				if (!propertySymbolsByName.TryGetValue(aggregatePropertyName, out var propertySymbol))
				{
					diagnostics.Add(
						Diagnostic.Create(
							GeneratorDiagnostics.EventParameterMustMapToWritableProperty,
							parameterLocation,
							parameter.Name,
							methodSymbol.Name,
							classSymbol.Name,
							$"property '{aggregatePropertyName}' does not exist"
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
							$"property '{aggregatePropertyName}' does not have a setter"
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
							$"property '{aggregatePropertyName}' is init-only"
						)
					);
					hasErrors = true;
					continue;
				}

				var propertyTypeName = propertySymbol.Type.ToDisplayString(
					SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
						SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions
							| SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
					)
				);

				var conversionKind = ResolveParameterConversionKind(
					compilation,
					classSymbol,
					parameter.Type,
					propertySymbol.Type,
					valueObjectContextType
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
							$"parameter type '{parameterTypeName}' cannot be mapped to property '{aggregatePropertyName}' of type '{propertyTypeName}' via implicit conversion or value-object Create(...)"
						)
					);
					hasErrors = true;
					continue;
				}

				// Warn when a non-nullable parameter maps to a nullable property via nullability widening.
				// The generator works around this automatically (see ResolveParameterConversionKind), but
				// the right long-term fix is to align the parameter's nullability with the property.
				if (
					conversionKind == EventParameterConversionKind.Implicit
					&& SymbolEqualityComparer.Default.Equals(parameter.Type, propertySymbol.Type)
					&& propertySymbol.Type.NullableAnnotation == NullableAnnotation.Annotated
					&& parameter.Type.NullableAnnotation != NullableAnnotation.Annotated
				)
				{
					diagnostics.Add(
						Diagnostic.Create(
							GeneratorDiagnostics.EventParameterNullabilityMismatch,
							parameterLocation,
							parameter.Name,
							methodSymbol.Name,
							aggregatePropertyName,
							propertyTypeName
						)
					);
				}

				parameters.Add(
					new EventPropertyInfo(
						parameter.Name,
						parameterTypeName,
						propertyTypeName,
						propertySymbol.Name,
						true,
						true,
						propertyTypeName,
						parameter.Type.SpecialType == SpecialType.System_String
							&& propertySymbol.Type.SpecialType == SpecialType.System_String,
						conversionKind.Value,
						isComputed: isComputedParameter
					)
				);
			}
		}

		if (hasErrors)
			return false;

		if (hasExplicitEventName)
		{
			if (!EventVerbMap.IsPastTenseEventName(eventName))
			{
				diagnostics.Add(
					Diagnostic.Create(
						GeneratorDiagnostics.EventNameOverrideShouldBePastTense,
						methodLocation,
						eventName,
						methodSymbol.Name
					)
				);
			}
		}
		else if (!EventVerbMap.TryCreateGeneratedEventName(methodSymbol.Name, classSymbol.Name, out eventName))
		{
			var suggestedMethodName = EventVerbMap.TrySuggestVerbPhrase(methodSymbol.Name, out var suggestedVerbPhrase)
				? suggestedVerbPhrase
				: $"Create{TrimAggregateSuffix(classSymbol.Name)}";

			diagnostics.Add(
				Diagnostic.Create(
					GeneratorDiagnostics.UnableToInferEventName,
					methodLocation,
					methodSymbol.Name,
					suggestedMethodName
				)
			);
			hasErrors = true;
		}
		else
		{
			if (
				isCollectionEvent
				&& collectionEvent is not null
				&& collectionEvent.ParameterShape == CollectionParameterShape.Array
			)
				eventName += "Array";

			eventName += eventSuffix;
		}

		if (hasErrors)
			return false;

		var eventNamespace = string.IsNullOrWhiteSpace(eventNamespaceOverride)
			? string.IsNullOrWhiteSpace(aggregateEventNamespaceOverride.Value)
				? CreateDefaultEventNamespace(aggregateNamespace, classSymbol.Name)
				: aggregateEventNamespaceOverride!.Value!.Trim()
			: eventNamespaceOverride!.Trim();

		methodInfo = new AggregateEventMethodInfo(
			methodSymbol.Name,
			eventName,
			eventNamespace,
			parameters,
			returnTypeName,
			returnKind,
			methodSymbol.DeclaredAccessibility,
			version,
			manualApply,
			collectionEvent
		);
		return true;
	}

	static bool TryCreateCollectionEventInfo(
		IMethodSymbol methodSymbol,
		AttributeData collectionEventAttribute,
		Dictionary<string, IPropertySymbol> propertySymbolsByName,
		List<Diagnostic> diagnostics,
		out EventPropertyInfo parameterInfo,
		out CollectionEventInfo? collectionEvent
	)
	{
		parameterInfo = default!;
		collectionEvent = null;

		var methodLocation = methodSymbol.Locations.FirstOrDefault();
		if (methodSymbol.Parameters.Length != 1)
			return false;

		if (
			collectionEventAttribute.ConstructorArguments.Length != 1
			|| collectionEventAttribute.ConstructorArguments[0].Value is not string rawPropertyName
			|| string.IsNullOrWhiteSpace(rawPropertyName)
		)
		{
			diagnostics.Add(
				Diagnostic.Create(
					GeneratorDiagnostics.UnsupportedEventMethodSignature,
					methodLocation,
					methodSymbol.Name,
					"collection property name must be provided via [GenerateAggregateCollectionEvent(nameof(CollectionProperty))]"
				)
			);
			return false;
		}

		var collectionPropertyName = rawPropertyName.Trim();
		if (!propertySymbolsByName.TryGetValue(collectionPropertyName, out var collectionProperty))
		{
			diagnostics.Add(
				Diagnostic.Create(
					GeneratorDiagnostics.EventParameterMustMapToWritableProperty,
					methodLocation,
					methodSymbol.Parameters[0].Name,
					methodSymbol.Name,
					methodSymbol.ContainingType.Name,
					$"collection property '{collectionPropertyName}' does not exist"
				)
			);
			return false;
		}

		if (!TryGetCollectionDetails(collectionProperty.Type, out var elementType, out var isSet))
		{
			diagnostics.Add(
				Diagnostic.Create(
					GeneratorDiagnostics.EventParameterMustMapToWritableProperty,
					methodLocation,
					methodSymbol.Parameters[0].Name,
					methodSymbol.Name,
					methodSymbol.ContainingType.Name,
					$"collection property '{collectionPropertyName}' must use Purview.EventSourcing.EventStoreList<T> or Purview.EventSourcing.EventStoreSet<T>"
				)
			);
			return false;
		}

		var parameter = methodSymbol.Parameters[0];
		var parameterType = parameter.Type;
		var parameterTypeName = parameterType.ToDisplayString(
			SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
				SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions
					| SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
			)
		);
		var elementTypeName = elementType.ToDisplayString(
			SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
				SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions
					| SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
			)
		);

		CollectionParameterShape parameterShape;
		string eventPropertyTypeName;
		if (SymbolEqualityComparer.Default.Equals(parameterType, elementType))
		{
			parameterShape = CollectionParameterShape.Single;
			eventPropertyTypeName = elementTypeName;
		}
		else if (
			parameterType is IArrayTypeSymbol arrayType
			&& SymbolEqualityComparer.Default.Equals(arrayType.ElementType, elementType)
		)
		{
			parameterShape = CollectionParameterShape.Array;
			eventPropertyTypeName = $"{elementTypeName}[]";
		}
		else if (TryGetIEnumerableElementType(parameterType, out var enumerableElementType))
		{
			if (!SymbolEqualityComparer.Default.Equals(enumerableElementType, elementType))
			{
				diagnostics.Add(
					Diagnostic.Create(
						GeneratorDiagnostics.UnsupportedEventMethodSignature,
						parameter.Locations.FirstOrDefault() ?? methodLocation,
						methodSymbol.Name,
						$"collection item type '{parameterTypeName}' does not match '{elementTypeName}'"
					)
				);
				return false;
			}

			parameterShape = CollectionParameterShape.Enumerable;
			eventPropertyTypeName = $"{elementTypeName}[]";
		}
		else
		{
			diagnostics.Add(
				Diagnostic.Create(
					GeneratorDiagnostics.UnsupportedEventMethodSignature,
					parameter.Locations.FirstOrDefault() ?? methodLocation,
					methodSymbol.Name,
					$"collection methods only support '{elementTypeName}', '{elementTypeName}[]', or IEnumerable<{elementTypeName}> parameters"
				)
			);
			return false;
		}

		parameterInfo = new EventPropertyInfo(
			parameter.Name,
			parameterTypeName,
			eventPropertyTypeName,
			collectionPropertyName,
			hasAggregateProperty: false,
			includeInEvent: true,
			equalityComparerTypeName: eventPropertyTypeName,
			useStringOrdinalComparison: false,
			parameterConversionKind: EventParameterConversionKind.None,
			isComputed: false,
			isParams: parameter.IsParams
		);

		if (
			!TryResolveCollectionMutationOperation(
				methodSymbol,
				collectionEventAttribute,
				diagnostics,
				methodLocation,
				out var mutationOperation
			)
		)
		{
			return false;
		}

		collectionEvent = new CollectionEventInfo(
			collectionPropertyName,
			elementTypeName,
			collectionProperty.Type.ToDisplayString(
				SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
					SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions
						| SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
				)
			),
			isSet,
			mutationOperation,
			parameterShape,
			methodSymbol.Name
		);

		return true;
	}

	static bool TryResolveCollectionMutationOperation(
		IMethodSymbol methodSymbol,
		AttributeData collectionEventAttribute,
		List<Diagnostic> diagnostics,
		Location? methodLocation,
		out CollectionMutationOperation operation
	)
	{
		operation = CollectionMutationOperation.Add;

		foreach (var namedArgument in collectionEventAttribute.NamedArguments)
		{
			if (namedArgument.Key != "Operation")
				continue;

			if (namedArgument.Value.Value is int operationValue)
			{
				if (operationValue == 1)
				{
					operation = CollectionMutationOperation.Add;
					return true;
				}

				if (operationValue == 2)
				{
					operation = CollectionMutationOperation.Remove;
					return true;
				}
			}

			return TryInferCollectionMutationOperation(methodSymbol, diagnostics, methodLocation, out operation);
		}

		return TryInferCollectionMutationOperation(methodSymbol, diagnostics, methodLocation, out operation);
	}

	static bool TryInferCollectionMutationOperation(
		IMethodSymbol methodSymbol,
		List<Diagnostic> diagnostics,
		Location? methodLocation,
		out CollectionMutationOperation operation
	)
	{
		operation = CollectionMutationOperation.Add;

		if (methodSymbol.Name.StartsWith("Add", StringComparison.Ordinal))
		{
			operation = CollectionMutationOperation.Add;
			return true;
		}

		if (
			methodSymbol.Name.StartsWith("Remove", StringComparison.Ordinal)
			|| methodSymbol.Name.StartsWith("Delete", StringComparison.Ordinal)
		)
		{
			operation = CollectionMutationOperation.Remove;
			return true;
		}

		diagnostics.Add(
			Diagnostic.Create(
				GeneratorDiagnostics.UnsupportedEventMethodSignature,
				methodLocation,
				methodSymbol.Name,
				"collection event methods must begin with 'Add', 'Remove', or 'Delete', or explicitly set Operation = CollectionEventOperation.Add/Remove"
			)
		);

		return false;
	}

	static EventParameterConversionKind? ResolveParameterConversionKind(
		Compilation compilation,
		INamedTypeSymbol aggregateType,
		ITypeSymbol parameterType,
		ITypeSymbol propertyType,
		INamedTypeSymbol? valueObjectContextType
	)
	{
		if (SymbolEqualityComparer.Default.Equals(parameterType, propertyType))
		{
			// Same underlying type. If nullability differs (non-nullable param → nullable property),
			// return Implicit so a typed local variable is generated and ref-hook calls don't produce
			// CS8600 ("Converting null literal or possible null value to non-nullable type").
			return
				parameterType.NullableAnnotation != propertyType.NullableAnnotation
				&& propertyType.NullableAnnotation == NullableAnnotation.Annotated
				? EventParameterConversionKind.Implicit
				: EventParameterConversionKind.None;
		}

		if (
			propertyType is INamedTypeSymbol namedPropertyType
			&& TryResolveValueObjectCreateConversion(
				aggregateType,
				namedPropertyType,
				parameterType,
				valueObjectContextType,
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
		INamedTypeSymbol aggregateType,
		INamedTypeSymbol propertyType,
		ITypeSymbol parameterType,
		INamedTypeSymbol? contextTypeDefinition,
		out EventParameterConversionKind conversionKind
	)
	{
		conversionKind = EventParameterConversionKind.None;

		var hasScalarAttribute = propertyType
			.GetAttributes()
			.Any(attribute => attribute.AttributeClass?.ToDisplayString() == ScalarAttributeMetadataName);
		var createMethods = propertyType.GetMembers("Create").OfType<IMethodSymbol>().ToArray();

		var hasContextualCreate = createMethods.Any(method =>
			IsContextualCreateMethod(method, propertyType, aggregateType, parameterType, contextTypeDefinition)
		);

		if (hasContextualCreate)
		{
			conversionKind = EventParameterConversionKind.ContextualCreate;
			return true;
		}

		var hasSimpleCreate = createMethods.Any(method => IsSimpleCreateMethod(method, propertyType, parameterType));

		if (hasSimpleCreate || hasScalarAttribute)
		{
			conversionKind = EventParameterConversionKind.Create;
			return true;
		}

		return false;
	}

	static bool IsSimpleCreateMethod(IMethodSymbol method, ITypeSymbol returnType, ITypeSymbol parameterType)
	{
		return method.IsStatic
			&& method.DeclaredAccessibility == Accessibility.Public
			&& method.Name == "Create"
			&& method.Parameters.Length == 1
			&& SymbolEqualityComparer.Default.Equals(method.ReturnType, returnType)
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
		return contextParameter.RefKind == RefKind.In
			&& contextTypeDefinition is not null
			&& contextParameter.Type is INamedTypeSymbol contextType
			&& SymbolEqualityComparer.Default.Equals(contextType.OriginalDefinition, contextTypeDefinition)
			&& contextType.TypeArguments.Length == 1
			&& SymbolEqualityComparer.Default.Equals(contextType.TypeArguments[0], aggregateType);
	}

	static AttributeStringValue GetAttributeStringNamedArgument(
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
					return new(value, true);
			}
		}

		return new AttributeStringValue(null, false);
	}

	static string? GetAggregatePropertyNameOverride(IParameterSymbol parameterSymbol)
	{
		foreach (var attribute in parameterSymbol.GetAttributes())
		{
			var attributeClass = attribute.AttributeClass;
			if (attributeClass is null || attributeClass.ToDisplayString() != AggregatePropertyAttributeMetadataName)
				continue;

			if (attribute.ConstructorArguments.Length == 1 && attribute.ConstructorArguments[0].Value is string value)
				return value.Trim();

			break;
		}

		return null;
	}

	static bool TryGetMetadataStoreSetting(IParameterSymbol parameterSymbol, out bool storeMetadata)
	{
		storeMetadata = true;

		foreach (var attribute in parameterSymbol.GetAttributes())
		{
			var attributeClass = attribute.AttributeClass;
			if (attributeClass is null || attributeClass.ToDisplayString() != MetadataAttributeMetadataName)
				continue;

			if (attribute.ConstructorArguments.Length == 1 && attribute.ConstructorArguments[0].Value is bool value)
				storeMetadata = value;

			return true;
		}

		return false;
	}

	static bool HasComputedAttribute(IParameterSymbol parameterSymbol)
	{
		foreach (var attribute in parameterSymbol.GetAttributes())
		{
			var attributeClass = attribute.AttributeClass;
			if (attributeClass is not null && attributeClass.ToDisplayString() == ComputedAttributeMetadataName)
				return true;
		}

		return false;
	}

	static bool IsEventType(INamedTypeSymbol typeSymbol)
	{
		for (var current = typeSymbol; current is not null; current = current.BaseType)
		{
			if (current.ToDisplayString() == EventBaseMetadataName)
				return true;
		}

		foreach (var implementedInterface in typeSymbol.AllInterfaces)
		{
			if (implementedInterface.ToDisplayString() == IEventMetadataName)
				return true;
		}

		return false;
	}

	static string GetDisplayEventName(string eventName) =>
		eventName.EndsWith("Event", StringComparison.Ordinal)
			? eventName.Substring(0, eventName.Length - "Event".Length)
			: eventName;

	static string TrimAggregateSuffix(string aggregateClassName) =>
		aggregateClassName.EndsWith("Aggregate", StringComparison.Ordinal)
			? aggregateClassName.Substring(0, aggregateClassName.Length - "Aggregate".Length)
			: aggregateClassName;

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

	static bool HasAttribute(ISymbol symbol, string metadataName) =>
		symbol.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == metadataName);

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

	static bool HasNoDeclaredBaseClass(INamedTypeSymbol classSymbol) =>
		classSymbol.BaseType is null || classSymbol.BaseType.SpecialType == SpecialType.System_Object;

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

	static bool IsCollectionLikeType(ITypeSymbol typeSymbol)
	{
		if (typeSymbol is IArrayTypeSymbol)
			return true;

		if (typeSymbol.SpecialType == SpecialType.System_String)
			return false;

		if (typeSymbol is not INamedTypeSymbol namedType)
			return false;

		if (
			namedType.IsGenericType
			&& namedType.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T
		)
			return true;

		foreach (var interfaceSymbol in namedType.AllInterfaces)
		{
			if (
				interfaceSymbol is INamedTypeSymbol namedInterface
				&& namedInterface.IsGenericType
				&& namedInterface.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T
			)
				return true;
		}

		return false;
	}

	static bool TryGetCollectionDetails(ITypeSymbol typeSymbol, out ITypeSymbol elementType, out bool isSet)
	{
		elementType = null!;
		isSet = false;

		if (typeSymbol is not INamedTypeSymbol namedType || !namedType.IsGenericType)
			return false;

		var definitionName = namedType.OriginalDefinition.ToDisplayString();
		if (definitionName == EventStoreListMetadataName)
		{
			elementType = namedType.TypeArguments[0];
			isSet = false;
			return true;
		}

		if (definitionName == EventStoreSetMetadataName)
		{
			elementType = namedType.TypeArguments[0];
			isSet = true;
			return true;
		}

		return false;
	}

	static bool TryGetIEnumerableElementType(ITypeSymbol typeSymbol, out ITypeSymbol elementType)
	{
		elementType = null!;

		if (
			typeSymbol is INamedTypeSymbol namedType
			&& namedType.IsGenericType
			&& namedType.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T
		)
		{
			elementType = namedType.TypeArguments[0];
			return true;
		}

		if (typeSymbol is not INamedTypeSymbol interfaceCarrier)
			return false;

		foreach (var interfaceSymbol in interfaceCarrier.AllInterfaces)
		{
			if (
				interfaceSymbol is INamedTypeSymbol enumerableInterface
				&& enumerableInterface.IsGenericType
				&& enumerableInterface.OriginalDefinition.SpecialType
					== SpecialType.System_Collections_Generic_IEnumerable_T
			)
			{
				elementType = enumerableInterface.TypeArguments[0];
				return true;
			}
		}

		return false;
	}

	static bool IsEventStoreCollectionType(ITypeSymbol typeSymbol) =>
		typeSymbol is INamedTypeSymbol namedType
		&& namedType.IsGenericType
		&& namedType.OriginalDefinition.ToDisplayString() is EventStoreListMetadataName or EventStoreSetMetadataName;

	void ILogSupport.SetLogOutput(Action<string, OutputType> action) => _logger = new GenerationLogger(action);
}
