using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Purview.EventSourcing.SourceGenerator.ValueObject;

static class ComplexValueObjectModelBuilder
{
	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Design",
		"CA1506:Avoid excessive class coupling",
		Justification = "Value object model construction couples many value types."
	)]
	public static GeneratorResult<ComplexValueObjectModel> Build(
		INamedTypeSymbol typeSymbol,
		TypeDeclarationSyntax syntax,
		Compilation compilation,
		CancellationToken cancellationToken
	)
	{
		cancellationToken.ThrowIfCancellationRequested();

		if (typeSymbol is null || syntax is null)
			return GeneratorResult<ComplexValueObjectModel>.Empty;

		var location = syntax.GetLocation();
		List<ReportableDiagnostic> diagnosticsList =
		[
			.. ValueObjectSymbolInspector.ValidateValueObjectType(typeSymbol, "ValueObject", location),
		];

		var attributes = typeSymbol.GetAttributes();
		if (ValueObjectSymbolInspector.HasAttribute(attributes, ValueObjectSymbolInspector.ScalarAttributeName))
		{
			diagnosticsList.Add(
				ReportableDiagnostic.Create(
					DiagnosticLibrary.ConflictingValueObjectAttributes,
					isBlocking: true,
					location,
					typeSymbol.Name
				)
			);
			return GeneratorResult<ComplexValueObjectModel>.Create([.. diagnosticsList]);
		}

		var assemblyDefaults = ValueObjectDefaultsAttributeData.FromAttributeData(compilation.Assembly.GetAttributes());
		var valueObjectOptions = ValueObjectAttributeData.FromAttributeData(attributes);
		var effectiveGenerateConstructor =
			IsPropertyExplicitlySet(
				attributes,
				ValueObjectSymbolInspector.ValueObjectAttributeName,
				"GenerateConstructor"
			)
				? valueObjectOptions.GenerateConstructor
			: assemblyDefaults.Exists ? assemblyDefaults.GenerateConstructor
			: true;
		valueObjectOptions = valueObjectOptions with { GenerateConstructor = effectiveGenerateConstructor };

		var typeModel = ValueObjectSymbolInspector.BuildTypeModel(typeSymbol);
		if (typeModel is null)
			return GeneratorResult<ComplexValueObjectModel>.Create([.. diagnosticsList]);

		var properties = typeSymbol
			.GetMembers()
			.OfType<IPropertySymbol>()
			.Where(property =>
				!property.IsStatic
				&& !property.IsIndexer
				&& property.GetMethod is not null
				&& ValueObjectSymbolInspector.IsValueObjectPropertyCandidate(typeSymbol, property)
				&& SymbolEqualityComparer.Default.Equals(property.ContainingType, typeSymbol)
			)
			.OrderBy(property => property.Locations.FirstOrDefault()?.SourceSpan.Start ?? int.MaxValue)
			.ToArray();

		var ctorExists = typeSymbol
			.Constructors.Where(static ctor => !ctor.IsStatic)
			.Any(ctor => ValueObjectSymbolInspector.ConstructorMatches(ctor, properties));

		var propertyModels = ImmutableArray.CreateBuilder<ComplexPropertyModel>(properties.Length);
		foreach (var property in properties)
		{
			propertyModels.Add(
				new ComplexPropertyModel(
					property.Name,
					ValueObjectSymbolInspector.ToTypeName(property.Type),
					TypeReference.Create(property.Type)
				)
			);
		}

		var hydrateExists = ValueObjectSymbolInspector.HasStaticFactory(
			typeSymbol,
			"Hydrate",
			[.. properties.Select(property => property.Type)]
		);
		var compareToSelfExists = ValueObjectSymbolInspector.HasInstanceMethod(typeSymbol, "CompareTo", [typeSymbol]);
		var compareToObjectExists = ValueObjectSymbolInspector.HasCompareToObject(typeSymbol);
		var isReferenceType = typeSymbol.TypeKind == TypeKind.Class;
		var equalsSelfExists =
			typeSymbol.IsRecord || ValueObjectSymbolInspector.HasInstanceMethod(typeSymbol, "Equals", [typeSymbol]);
		var equalsObjectExists = ValueObjectSymbolInspector.HasEqualsObject(typeSymbol);
		var getHashCodeExists = ValueObjectSymbolInspector.HasParameterlessMethod(typeSymbol, "GetHashCode");
		var equalityOperatorExists =
			typeSymbol.IsRecord
			|| ValueObjectSymbolInspector.HasBinaryOperator(typeSymbol, "op_Equality", [typeSymbol, typeSymbol]);
		var inequalityOperatorExists =
			typeSymbol.IsRecord
			|| ValueObjectSymbolInspector.HasBinaryOperator(typeSymbol, "op_Inequality", [typeSymbol, typeSymbol]);
		var hasJsonConverterAttribute = ValueObjectSymbolInspector.HasAttribute(
			typeSymbol,
			ValueObjectSymbolInspector.JsonConverterAttributeName
		);
		var createExists = ValueObjectSymbolInspector.HasStaticFactory(
			typeSymbol,
			"Create",
			[.. properties.Select(property => property.Type)]
		);
		var declareOnNormalize = ValueObjectSymbolInspector.ShouldEmitComplexHookDeclaration(
			typeSymbol,
			"OnNormalize",
			properties.Length,
			includeRef: true
		);
		var declareOnValidate = ValueObjectSymbolInspector.ShouldEmitComplexHookDeclaration(
			typeSymbol,
			"OnValidate",
			properties.Length
		);
		var validateHookIsReadOnly =
			typeSymbol.TypeKind == TypeKind.Struct
			&& ValueObjectSymbolInspector.IsComplexHookReadOnly(typeSymbol, "OnValidate", properties.Length);

		var parameterlessCtorExists = typeSymbol
			.Constructors.Where(static ctor => !ctor.IsStatic)
			.Any(ctor => ctor.Parameters.Length == 0 && !ctor.IsImplicitlyDeclared);

		var hydrateFactoryName =
			valueObjectOptions.DeserializationMode == ValueObjectSymbolInspector.StrictModeName ? "Create" : "Hydrate";

		var efConstructorArguments = ValueObjectSymbolInspector.TryGetEfConstructorArguments(
			typeSymbol,
			properties,
			out var efCtorArgs
		)
			? efCtorArgs
			: null;

		var hintName = ValueObjectSymbolInspector.BuildHintName(typeSymbol, "ComplexValueObject");

		var emptyArguments = ImmutableArray.CreateBuilder<string>(properties.Length);
		foreach (var property in properties)
			emptyArguments.Add(ValueObjectSymbolInspector.GetEmptyValueExpression(property.Type));

		ComplexValueObjectModel model = new(
			typeModel.Value,
			propertyModels.ToImmutable(),
			valueObjectOptions,
			ctorExists,
			hintName,
			typeModel.Value.FullyQualifiedName,
			isReferenceType,
			typeSymbol.TypeKind == TypeKind.Struct,
			typeSymbol.IsRecord,
			typeSymbol.IsReadOnly,
			typeSymbol.DeclaredAccessibility.ToTypeDeclarationAccessibility(),
			ValueObjectSymbolInspector.ImplementsSelfEquatable(typeSymbol),
			hydrateExists,
			createExists,
			compareToSelfExists,
			compareToObjectExists,
			equalsSelfExists,
			equalsObjectExists,
			getHashCodeExists,
			equalityOperatorExists,
			inequalityOperatorExists,
			hasJsonConverterAttribute,
			declareOnNormalize,
			declareOnValidate,
			validateHookIsReadOnly,
			ValueObjectSymbolInspector.HasMemberWithName(typeSymbol, "Empty"),
			emptyArguments.ToImmutable(),
			parameterlessCtorExists,
			efConstructorArguments,
			hydrateFactoryName,
			BuildExistingRelationalOperators(
				typeSymbol,
				typeModel.Value.FullyQualifiedName,
				typeModel.Value.FullyQualifiedName
			)
		);

		return GeneratorResult<ComplexValueObjectModel>.Create(model, diagnosticsList.ToImmutableArray());
	}

	static EquatableArray<string> BuildExistingRelationalOperators(
		INamedTypeSymbol typeSymbol,
		string leftTypeName,
		string rightTypeName
	)
	{
		var builder = ImmutableArray.CreateBuilder<string>();
		foreach (var operatorName in ValueObjectSymbolInspector.RelationalOperatorNames)
		{
			if (ValueObjectSymbolInspector.HasRelationalOperator(typeSymbol, operatorName, leftTypeName, rightTypeName))
				builder.Add(operatorName);
		}

		return builder.ToImmutable();
	}

	static bool IsPropertyExplicitlySet(
		ImmutableArray<AttributeData> attributes,
		string attributeName,
		string propertyName
	)
	{
		var attribute = attributes.FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == attributeName);
		return attribute?.NamedArguments.Any(kvp => kvp.Key == propertyName) ?? false;
	}
}
