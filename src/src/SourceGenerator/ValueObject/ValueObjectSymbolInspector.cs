using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Purview.EventSourcing.SourceGenerator.ValueObject;

static class ValueObjectSymbolInspector
{
	public const string ScalarAttributeName = "Purview.EventSourcing.Serialization.ScalarAttribute";
	public const string ValueObjectAttributeName = "Purview.EventSourcing.Serialization.ValueObjectAttribute";
	public const string JsonConverterAttributeName = "System.Text.Json.Serialization.JsonConverterAttribute";
	public const string StrictModeName =
		"global::Purview.EventSourcing.Serialization.ValueObjectDeserializationMode.Strict";
	public const string HydrateModeName =
		"global::Purview.EventSourcing.Serialization.ValueObjectDeserializationMode.Hydrate";
	public const string LessThanOperatorName = "op_LessThan";
	public const string GreaterThanOperatorName = "op_GreaterThan";
	public const string LessThanOrEqualOperatorName = "op_LessThanOrEqual";
	public const string GreaterThanOrEqualOperatorName = "op_GreaterThanOrEqual";

	public static readonly string[] RelationalOperatorNames =
	[
		LessThanOperatorName,
		GreaterThanOperatorName,
		LessThanOrEqualOperatorName,
		GreaterThanOrEqualOperatorName,
	];

	public static List<ReportableDiagnostic> ValidateValueObjectType(
		INamedTypeSymbol typeSymbol,
		string attributeName,
		Location location
	)
	{
		List<ReportableDiagnostic> diagnostics = [];

		var isPartial = typeSymbol
			.DeclaringSyntaxReferences.Select(reference => reference.GetSyntax())
			.OfType<TypeDeclarationSyntax>()
			.Any(syntax => syntax.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.PartialKeyword)));
		if (!isPartial)
		{
			diagnostics.Add(
				ReportableDiagnostic.Create(
					DiagnosticLibrary.ValueObjectMustBePartial,
					isBlocking: true,
					location,
					typeSymbol.Name,
					attributeName
				)
			);
		}

		if (typeSymbol.ContainingType is not null)
		{
			diagnostics.Add(
				ReportableDiagnostic.Create(
					DiagnosticLibrary.NestedValueObjectsAreNotSupported,
					isBlocking: true,
					location,
					typeSymbol.Name,
					attributeName
				)
			);
		}

		if (typeSymbol.TypeParameters.Length > 0)
		{
			diagnostics.Add(
				ReportableDiagnostic.Create(
					DiagnosticLibrary.GenericValueObjectsAreNotSupported,
					isBlocking: true,
					location,
					typeSymbol.Name,
					attributeName
				)
			);
		}

		return diagnostics;
	}

	public static bool HasAttribute(INamedTypeSymbol typeSymbol, string metadataName) =>
		typeSymbol.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == metadataName);

	public static bool HasAttribute(ImmutableArray<AttributeData> attributes, string metadataName) =>
		attributes.Any(attribute => attribute.AttributeClass?.ToDisplayString() == metadataName);

	public static bool HasStaticFactory(INamedTypeSymbol typeSymbol, string name, ITypeSymbol[] parameterTypes)
	{
		return typeSymbol
			.GetMembers(name)
			.OfType<IMethodSymbol>()
			.Any(method =>
				method.IsStatic
				&& method.DeclaredAccessibility == Accessibility.Public
				&& method.Parameters.Length == parameterTypes.Length
				&& SymbolEqualityComparer.Default.Equals(method.ReturnType, typeSymbol)
				&& ParametersMatch(method.Parameters, parameterTypes)
			);
	}

	public static bool HasTryCreate(INamedTypeSymbol typeSymbol, ITypeSymbol scalarType)
	{
		return typeSymbol
			.GetMembers("TryCreate")
			.OfType<IMethodSymbol>()
			.Any(method =>
				method.IsStatic
				&& method.Parameters.Length == 2
				&& method.ReturnType.SpecialType == SpecialType.System_Boolean
				&& method.Parameters[1].RefKind == RefKind.Out
				&& SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, scalarType)
				&& SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, typeSymbol)
			);
	}

	public static bool HasInstanceMethod(
		INamedTypeSymbol typeSymbol,
		string name,
		IReadOnlyList<ITypeSymbol> parameterTypes
	)
	{
		return typeSymbol
			.GetMembers(name)
			.OfType<IMethodSymbol>()
			.Any(method =>
				!method.IsStatic
				&& method.Parameters.Length == parameterTypes.Count
				&& ParametersMatch(method.Parameters, parameterTypes)
			);
	}

	public static bool HasCompareToObject(INamedTypeSymbol typeSymbol) =>
		typeSymbol
			.GetMembers("CompareTo")
			.OfType<IMethodSymbol>()
			.Any(method =>
				!method.IsStatic
				&& method.Parameters.Length == 1
				&& method.Parameters[0].Type.SpecialType == SpecialType.System_Object
			);

	public static bool HasEqualsObject(INamedTypeSymbol typeSymbol) =>
		typeSymbol
			.GetMembers("Equals")
			.OfType<IMethodSymbol>()
			.Any(method =>
				!method.IsStatic
				&& method.Parameters.Length == 1
				&& method.Parameters[0].Type.SpecialType == SpecialType.System_Object
			);

	public static bool HasBinaryOperator(
		INamedTypeSymbol typeSymbol,
		string operatorMethodName,
		IReadOnlyList<ITypeSymbol> parameterTypes
	)
	{
		return typeSymbol
			.GetMembers(operatorMethodName)
			.OfType<IMethodSymbol>()
			.Any(method =>
				method.IsStatic
				&& method.Parameters.Length == parameterTypes.Count
				&& method.ReturnType.SpecialType == SpecialType.System_Boolean
				&& ParametersMatch(method.Parameters, parameterTypes)
			);
	}

	public static bool HasParameterlessMethod(INamedTypeSymbol typeSymbol, string name) =>
		typeSymbol
			.GetMembers(name)
			.OfType<IMethodSymbol>()
			.Any(method => !method.IsStatic && !method.IsImplicitlyDeclared && method.Parameters.Length == 0);

	public static bool ParametersMatch(ImmutableArray<IParameterSymbol> parameters, IReadOnlyList<ITypeSymbol> expected)
	{
		for (var i = 0; i < expected.Count; i++)
		{
			if (!SymbolEqualityComparer.Default.Equals(parameters[i].Type, expected[i]))
				return false;
		}

		return true;
	}

	public static bool HasConversionOperator(INamedTypeSymbol typeSymbol, ITypeSymbol primitiveType, bool fromPrimitive)
	{
		return typeSymbol
			.GetMembers()
			.OfType<IMethodSymbol>()
			.Any(method =>
				method.MethodKind == MethodKind.Conversion
				&& method.Name == "op_Implicit"
				&& (
					fromPrimitive
						? method.Parameters.Length == 1
							&& SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, primitiveType)
							&& SymbolEqualityComparer.Default.Equals(method.ReturnType, typeSymbol)
						: method.Parameters.Length == 1
							&& SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, typeSymbol)
							&& SymbolEqualityComparer.Default.Equals(method.ReturnType, primitiveType)
				)
			);
	}

	public static bool HasRelationalOperator(
		INamedTypeSymbol typeSymbol,
		string operatorMethodName,
		string leftTypeName,
		string rightTypeName
	)
	{
		return typeSymbol
			.GetMembers(operatorMethodName)
			.OfType<IMethodSymbol>()
			.Any(method =>
				method.IsStatic
				&& method.Parameters.Length == 2
				&& method.ReturnType.SpecialType == SpecialType.System_Boolean
				&& method
					.Parameters[0]
					.Type.ToDisplayString(
						SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
							SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions
								| SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
						)
					) == leftTypeName
				&& method
					.Parameters[1]
					.Type.ToDisplayString(
						SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
							SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions
								| SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
						)
					) == rightTypeName
			);
	}

	public static bool HasContextualCreateOverload(INamedTypeSymbol typeSymbol, ITypeSymbol primitiveType)
	{
		return typeSymbol
			.GetMembers("Create")
			.OfType<IMethodSymbol>()
			.Any(method =>
				method.IsStatic
				&& method.Parameters.Length == 2
				&& SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, primitiveType)
				&& method.Parameters[1].RefKind == RefKind.In
				&& method.Parameters[1].Type.Name == "ValueObjectContext"
			);
	}

	public static bool ShouldEmitScalarHookDeclaration(
		INamedTypeSymbol typeSymbol,
		string methodName,
		int parameterCount,
		bool includeRef
	)
	{
		var declarations = typeSymbol
			.DeclaringSyntaxReferences.Select(reference => reference.GetSyntax())
			.OfType<TypeDeclarationSyntax>()
			.SelectMany(declaration => declaration.Members.OfType<MethodDeclarationSyntax>())
			.Where(method =>
				method.Identifier.Text == methodName && method.ParameterList.Parameters.Count == parameterCount
			)
			.ToArray();

		var hasDefinition = declarations.Any(method => method.Body is null && method.ExpressionBody is null);
		if (hasDefinition)
			return false;

		if (!includeRef)
			return true;

		var hasRefImplementation = declarations.Any(method =>
			method.ParameterList.Parameters[0].Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.RefKeyword))
		);
		return hasRefImplementation || declarations.Length == 0;
	}

	public static bool ShouldEmitComplexHookDeclaration(
		INamedTypeSymbol typeSymbol,
		string methodName,
		int parameterCount,
		bool includeRef = false
	)
	{
		var declarations = GetComplexHookDeclarations(typeSymbol, methodName, parameterCount);

		var hasDefinition = declarations.Any(method =>
			(
				!includeRef
				|| method.ParameterList.Parameters.All(static parameter =>
					parameter.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.RefKeyword))
				)
			)
			&& method.Body is null
			&& method.ExpressionBody is null
		);
		if (hasDefinition)
			return false;

		if (!includeRef)
			return true;

		var hasRefImplementation = declarations.Any(method =>
			method.ParameterList.Parameters.All(static parameter =>
				parameter.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.RefKeyword))
			)
		);
		return hasRefImplementation || declarations.Length == 0;
	}

	public static bool IsComplexHookReadOnly(INamedTypeSymbol typeSymbol, string methodName, int parameterCount)
	{
		return GetComplexHookDeclarations(typeSymbol, methodName, parameterCount)
			.Any(method =>
				(method.Body is not null || method.ExpressionBody is not null)
				&& method.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.ReadOnlyKeyword))
			);
	}

	public static MethodDeclarationSyntax[] GetComplexHookDeclarations(
		INamedTypeSymbol typeSymbol,
		string methodName,
		int parameterCount
	)
	{
		return
		[
			.. typeSymbol
				.DeclaringSyntaxReferences.Select(reference => reference.GetSyntax())
				.OfType<TypeDeclarationSyntax>()
				.SelectMany(declaration => declaration.Members.OfType<MethodDeclarationSyntax>())
				.Where(method =>
					method.Identifier.Text == methodName && method.ParameterList.Parameters.Count == parameterCount
				),
		];
	}

	public static bool ConstructorMatches(IMethodSymbol constructor, IPropertySymbol[] properties)
	{
		if (constructor.Parameters.Length != properties.Length)
			return false;

		for (var i = 0; i < properties.Length; i++)
		{
			if (!SymbolEqualityComparer.Default.Equals(constructor.Parameters[i].Type, properties[i].Type))
				return false;
		}

		return true;
	}

	public static bool TryGetEfConstructorArguments(
		INamedTypeSymbol typeSymbol,
		IPropertySymbol[] properties,
		out string arguments
	)
	{
		if (properties.Length > 0)
		{
			arguments = string.Join(
				", ",
				properties.Select(static property => GetConstructorArgumentExpression(property.Type))
			);
			return true;
		}

		var parameterizedConstructors = typeSymbol
			.Constructors.Where(static ctor => !ctor.IsStatic && ctor.Parameters.Length > 0)
			.ToArray();

		if (parameterizedConstructors.Length == 1)
		{
			arguments = string.Join(
				", ",
				parameterizedConstructors[0]
					.Parameters.Select(static parameter => GetConstructorArgumentExpression(parameter.Type))
			);
			return true;
		}

		arguments = string.Empty;
		return false;
	}

	public static bool IsValueObjectPropertyCandidate(INamedTypeSymbol typeSymbol, IPropertySymbol property) =>
		!property.IsImplicitlyDeclared
		|| (
			typeSymbol.IsRecord
			&& typeSymbol
				.InstanceConstructors.Where(static ctor => !ctor.IsStatic)
				.SelectMany(static ctor => ctor.Parameters)
				.Any(parameter =>
					SymbolEqualityComparer.Default.Equals(parameter.Type, property.Type)
					&& string.Equals(parameter.Name, property.Name, StringComparison.OrdinalIgnoreCase)
				)
		);

	public static bool ImplementsSelfEquatable(INamedTypeSymbol typeSymbol) =>
		typeSymbol.AllInterfaces.Any(interfaceSymbol =>
			interfaceSymbol is INamedTypeSymbol namedTypeSymbol
			&& namedTypeSymbol.OriginalDefinition.Name == nameof(IEquatable<>)
			&& namedTypeSymbol.OriginalDefinition.ContainingNamespace.ToDisplayString() == "System"
			&& namedTypeSymbol.TypeArguments.Length == 1
			&& SymbolEqualityComparer.Default.Equals(namedTypeSymbol.TypeArguments[0], typeSymbol)
		);

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0072:Add missing cases")]
	public static GeneratedTypeModel? BuildTypeModel(INamedTypeSymbol typeSymbol)
	{
		if (typeSymbol.ContainingType is not null || typeSymbol.TypeParameters.Length > 0)
			return null;

		var access = typeSymbol.DeclaredAccessibility switch
		{
			Accessibility.Public => "public",
			Accessibility.Internal => "internal",
			Accessibility.Private => "private",
			Accessibility.Protected => "protected",
			Accessibility.ProtectedOrInternal => "protected internal",
			Accessibility.ProtectedAndInternal => "private protected",
			_ => "internal",
		};

		string declaration;
		if (typeSymbol.TypeKind == TypeKind.Struct)
		{
			var readonlyPrefix = typeSymbol.IsReadOnly ? "readonly " : string.Empty;
			declaration = typeSymbol.IsRecord
				? $"{access} {readonlyPrefix}partial record struct {typeSymbol.Name}"
				: $"{access} {readonlyPrefix}partial struct {typeSymbol.Name}";
		}
		else
		{
			declaration = typeSymbol.IsRecord
				? $"{access} partial record class {typeSymbol.Name}"
				: $"{access} partial class {typeSymbol.Name}";
		}

		var ns = typeSymbol.ContainingNamespace.IsGlobalNamespace
			? null
			: typeSymbol.ContainingNamespace.ToDisplayString();
		var fullyQualifiedName = typeSymbol.ToDisplayString(
			SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
				SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions
					| SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
			)
		);

		return new GeneratedTypeModel(typeSymbol.Name, ns, declaration, fullyQualifiedName);
	}

	public static string BuildHintName(INamedTypeSymbol typeSymbol, string suffix)
	{
		var fullName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
		var hash = ComputeStableHash(fullName);
		return $"{typeSymbol.Name}_{suffix}_{hash:X16}.g.cs";
	}

	public static ulong ComputeStableHash(string value)
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

	public static string ToTypeName(ITypeSymbol typeSymbol) =>
		typeSymbol.ToDisplayString(
			SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
				SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions
					| SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
			)
		);

	public static string ToCamelCase(string value) =>
		string.IsNullOrEmpty(value) ? value : char.ToLowerInvariant(value[0]) + value.Substring(1);

	public static string GetEmptyValueExpression(ITypeSymbol typeSymbol)
	{
		return typeSymbol.IsReferenceType
			? typeSymbol.NullableAnnotation == NullableAnnotation.Annotated
				? "null"
				: "null!"
			: typeSymbol is INamedTypeSymbol namedTypeSymbol
			&& namedTypeSymbol.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
				? "null"
				: TypeLibrary.System.Guid.Equals(typeSymbol) switch
				{
					true => $"{TypeLibrary.System.Guid}.Empty",
					false => "default",
				};
	}

	public static string GetConstructorArgumentExpression(ITypeSymbol typeSymbol)
	{
		if (typeSymbol.IsReferenceType)
			return $"({ToTypeName(typeSymbol)})null!";

		if (
			typeSymbol is INamedTypeSymbol namedTypeSymbol
			&& namedTypeSymbol.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
		)
		{
			return "null";
		}

		// For value types, we can use the default literal, but for Guid, we want to use Guid.Empty
		return TypeLibrary.System.Guid.Equals(typeSymbol) ? $"{TypeLibrary.System.Guid}.Empty" : "default";
	}

	public static IFieldSymbol[] GetEnumFields(ITypeSymbol enumTypeSymbol) =>
		[
			.. enumTypeSymbol
				.GetMembers()
				.OfType<IFieldSymbol>()
				.Where(field => field.HasConstantValue && field.DeclaredAccessibility == Accessibility.Public)
				.OrderBy(field => field.Locations.FirstOrDefault()?.SourceSpan.Start ?? int.MaxValue),
		];

	public static bool HasMemberWithName(INamedTypeSymbol typeSymbol, string name) =>
		typeSymbol.GetMembers(name).Any(member => !member.IsImplicitlyDeclared);
}
