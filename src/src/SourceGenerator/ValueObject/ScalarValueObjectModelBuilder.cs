using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Purview.EventSourcing.SourceGenerator.ValueObject;

static class ScalarValueObjectModelBuilder
{
	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Design",
		"CA1506:Avoid excessive class coupling",
		Justification = "Value object model construction couples many value types."
	)]
	public static GeneratorResult<ScalarValueObjectModel> Build(
		INamedTypeSymbol typeSymbol,
		TypeDeclarationSyntax syntax,
		CancellationToken cancellationToken
	)
	{
		cancellationToken.ThrowIfCancellationRequested();

		if (typeSymbol is null || syntax is null)
			return GeneratorResult<ScalarValueObjectModel>.Empty;

		var location = syntax.GetLocation();
		List<ReportableDiagnostic> diagnosticsList =
		[
			.. ValueObjectSymbolInspector.ValidateValueObjectType(typeSymbol, "Scalar", location),
		];

		var attributes = typeSymbol.GetAttributes();
		if (ValueObjectSymbolInspector.HasAttribute(attributes, ValueObjectSymbolInspector.ValueObjectAttributeName))
		{
			diagnosticsList.Add(
				ReportableDiagnostic.Create(
					DiagnosticLibrary.ConflictingValueObjectAttributes,
					isBlocking: true,
					location,
					typeSymbol.Name
				)
			);
			return GeneratorResult<ScalarValueObjectModel>.Create([.. diagnosticsList]);
		}

		var scalarOptions = ScalarAttributeData.FromAttributeData(attributes);
		var scalarProperty = typeSymbol
			.GetMembers(scalarOptions.PropertyName)
			.OfType<IPropertySymbol>()
			.FirstOrDefault(property => !property.IsStatic && property.GetMethod is not null);

		if (scalarProperty is null)
		{
			diagnosticsList.Add(
				ReportableDiagnostic.Create(
					DiagnosticLibrary.ScalarPropertyMissing,
					isBlocking: true,
					location,
					typeSymbol.Name,
					scalarOptions.PropertyName
				)
			);
			return GeneratorResult<ScalarValueObjectModel>.Create([.. diagnosticsList]);
		}

		var ctorExists = typeSymbol
			.Constructors.Where(static ctor => !ctor.IsStatic)
			.Any(ctor =>
				ctor.Parameters.Length == 1
				&& SymbolEqualityComparer.Default.Equals(ctor.Parameters[0].Type, scalarProperty.Type)
			);

		var typeModel = ValueObjectSymbolInspector.BuildTypeModel(typeSymbol);
		if (typeModel is null)
			return GeneratorResult<ScalarValueObjectModel>.Create([.. diagnosticsList]);

		if (typeSymbol.TypeKind == TypeKind.Struct && !typeSymbol.IsRecord)
		{
			diagnosticsList.Add(
				ReportableDiagnostic.Create(
					DiagnosticLibrary.ScalarShouldBeRecordStruct,
					isBlocking: false,
					location,
					typeSymbol.Name
				)
			);
		}

		var typeName = typeModel.Value.FullyQualifiedName;
		var scalarTypeName = ValueObjectSymbolInspector.ToTypeName(scalarProperty.Type);
		var scalarCanBeNull =
			scalarProperty.Type.IsReferenceType
			|| scalarProperty.Type.NullableAnnotation == NullableAnnotation.Annotated;
		var scalarIsReferenceType = scalarProperty.Type.IsReferenceType;
		var isReferenceType = typeSymbol.TypeKind == TypeKind.Class;
		var scalarPropertyName = scalarProperty.Name;
		var createExists = ValueObjectSymbolInspector.HasStaticFactory(typeSymbol, "Create", [scalarProperty.Type]);
		var hydrateExists = ValueObjectSymbolInspector.HasStaticFactory(typeSymbol, "Hydrate", [scalarProperty.Type]);
		var tryCreateExists = ValueObjectSymbolInspector.HasTryCreate(typeSymbol, scalarProperty.Type);
		var compareToSelfExists = ValueObjectSymbolInspector.HasInstanceMethod(typeSymbol, "CompareTo", [typeSymbol]);
		var compareToPrimitiveExists = ValueObjectSymbolInspector.HasInstanceMethod(
			typeSymbol,
			"CompareTo",
			[scalarProperty.Type]
		);
		var compareToObjectExists = ValueObjectSymbolInspector.HasCompareToObject(typeSymbol);
		var equalsSelfExists =
			typeSymbol.IsRecord || ValueObjectSymbolInspector.HasInstanceMethod(typeSymbol, "Equals", [typeSymbol]);
		var equalsPrimitiveExists = ValueObjectSymbolInspector.HasInstanceMethod(
			typeSymbol,
			"Equals",
			[scalarProperty.Type]
		);
		var equalsObjectExists = ValueObjectSymbolInspector.HasEqualsObject(typeSymbol);
		var getHashCodeExists = ValueObjectSymbolInspector.HasParameterlessMethod(typeSymbol, "GetHashCode");
		var sameTypeEqualityOperatorExists =
			typeSymbol.IsRecord
			|| ValueObjectSymbolInspector.HasBinaryOperator(typeSymbol, "op_Equality", [typeSymbol, typeSymbol]);
		var sameTypeInequalityOperatorExists =
			typeSymbol.IsRecord
			|| ValueObjectSymbolInspector.HasBinaryOperator(typeSymbol, "op_Inequality", [typeSymbol, typeSymbol]);
		var primitiveEqualityOperatorExists = ValueObjectSymbolInspector.HasBinaryOperator(
			typeSymbol,
			"op_Equality",
			[typeSymbol, scalarProperty.Type]
		);
		var primitiveInequalityOperatorExists = ValueObjectSymbolInspector.HasBinaryOperator(
			typeSymbol,
			"op_Inequality",
			[typeSymbol, scalarProperty.Type]
		);
		var reversePrimitiveEqualityOperatorExists = ValueObjectSymbolInspector.HasBinaryOperator(
			typeSymbol,
			"op_Equality",
			[scalarProperty.Type, typeSymbol]
		);
		var reversePrimitiveInequalityOperatorExists = ValueObjectSymbolInspector.HasBinaryOperator(
			typeSymbol,
			"op_Inequality",
			[scalarProperty.Type, typeSymbol]
		);
		var enumPropertiesEnabled =
			scalarOptions.GenerateEnumProperties && scalarProperty.Type.TypeKind == TypeKind.Enum;
		var enumFieldNames = enumPropertiesEnabled ? BuildEnumFieldNames(typeSymbol, scalarProperty.Type) : [];
		var toStringExists = ValueObjectSymbolInspector.HasParameterlessMethod(typeSymbol, "ToString");
		var hasJsonConverterAttribute = ValueObjectSymbolInspector.HasAttribute(
			typeSymbol,
			ValueObjectSymbolInspector.JsonConverterAttributeName
		);
		var declareOnNormalize = ValueObjectSymbolInspector.ShouldEmitScalarHookDeclaration(
			typeSymbol,
			"OnNormalize",
			1,
			includeRef: true
		);
		var declareOnValidate = ValueObjectSymbolInspector.ShouldEmitScalarHookDeclaration(
			typeSymbol,
			"OnValidate",
			1,
			includeRef: false
		);

		if (scalarOptions.DeserializationMode == ValueObjectSymbolInspector.StrictModeName && !createExists)
		{
			diagnosticsList.Add(
				ReportableDiagnostic.Create(
					DiagnosticLibrary.StrictDeserializationRequiresCreate,
					isBlocking: false,
					typeSymbol.Locations.FirstOrDefault(),
					typeSymbol.Name
				)
			);
		}

		var hintName = ValueObjectSymbolInspector.BuildHintName(typeSymbol, "ScalarValueObject");

		ScalarValueObjectModel model = new(
			typeModel.Value,
			scalarOptions,
			ctorExists,
			hintName,
			typeName,
			scalarTypeName,
			scalarPropertyName,
			scalarCanBeNull,
			scalarIsReferenceType,
			isReferenceType,
			typeSymbol.IsRecord,
			typeSymbol.IsReadOnly,
			typeSymbol.DeclaredAccessibility.ToTypeDeclarationAccessibility(),
			TypeReference.Create(scalarProperty.Type),
			createExists,
			hydrateExists,
			tryCreateExists,
			compareToSelfExists,
			compareToPrimitiveExists,
			compareToObjectExists,
			equalsSelfExists,
			equalsPrimitiveExists,
			equalsObjectExists,
			getHashCodeExists,
			sameTypeEqualityOperatorExists,
			sameTypeInequalityOperatorExists,
			primitiveEqualityOperatorExists,
			primitiveInequalityOperatorExists,
			reversePrimitiveEqualityOperatorExists,
			reversePrimitiveInequalityOperatorExists,
			enumPropertiesEnabled,
			enumFieldNames,
			toStringExists,
			hasJsonConverterAttribute,
			declareOnNormalize,
			declareOnValidate,
			ValueObjectSymbolInspector.ImplementsSelfEquatable(typeSymbol),
			ValueObjectSymbolInspector.HasMemberWithName(typeSymbol, "Empty"),
			ValueObjectSymbolInspector.GetEmptyValueExpression(scalarProperty.Type),
			ValueObjectSymbolInspector.HasConversionOperator(typeSymbol, scalarProperty.Type, fromPrimitive: true),
			ValueObjectSymbolInspector.HasConversionOperator(typeSymbol, scalarProperty.Type, fromPrimitive: false),
			ValueObjectSymbolInspector.HasContextualCreateOverload(typeSymbol, scalarProperty.Type),
			SymbolEqualityComparer.Default.Equals(scalarProperty.Type, typeSymbol),
			BuildExistingRelationalOperators(typeSymbol, typeName, typeName),
			BuildExistingRelationalOperators(typeSymbol, typeName, scalarTypeName)
		);

		return GeneratorResult<ScalarValueObjectModel>.Create(model, diagnosticsList.ToImmutableArray());
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

	static EquatableArray<string> BuildEnumFieldNames(INamedTypeSymbol typeSymbol, ITypeSymbol enumType)
	{
		var builder = ImmutableArray.CreateBuilder<string>();
		foreach (var enumField in ValueObjectSymbolInspector.GetEnumFields(enumType))
		{
			if (ValueObjectSymbolInspector.HasMemberWithName(typeSymbol, enumField.Name))
				continue;

			builder.Add(enumField.Name);
		}

		return builder.ToImmutable();
	}
}
