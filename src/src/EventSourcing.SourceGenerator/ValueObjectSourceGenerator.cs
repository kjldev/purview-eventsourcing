using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Purview.EventSourcing.SourceGenerator;

[Generator]
public sealed class ValueObjectSourceGenerator : IIncrementalGenerator
{
	const string ScalarAttributeName = "Purview.EventSourcing.Serialization.ScalarAttribute";
	const string ValueObjectAttributeName = "Purview.EventSourcing.Serialization.ValueObjectAttribute";
	const string JsonConverterAttributeName = "System.Text.Json.Serialization.JsonConverterAttribute";
	const string StrictModeName = "Purview.EventSourcing.Serialization.ValueObjectDeserializationMode.Strict";
	const string HydrateModeName = "Purview.EventSourcing.Serialization.ValueObjectDeserializationMode.Hydrate";
	const string LessThanOperatorName = "op_LessThan";
	const string GreaterThanOperatorName = "op_GreaterThan";
	const string LessThanOrEqualOperatorName = "op_LessThanOrEqual";
	const string GreaterThanOrEqualOperatorName = "op_GreaterThanOrEqual";

	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		var scalarCandidates = context
			.SyntaxProvider.ForAttributeWithMetadataName(
				ScalarAttributeName,
				predicate: static (node, _) => node is TypeDeclarationSyntax,
				transform: static (ctx, ct) => BuildScalarGenerationResult(ctx, ct)
			)
			;

		var complexCandidates = context
			.SyntaxProvider.ForAttributeWithMetadataName(
				ValueObjectAttributeName,
				predicate: static (node, _) => node is TypeDeclarationSyntax,
				transform: static (ctx, ct) => BuildComplexGenerationResult(ctx, ct)
			)
			;

		context.RegisterSourceOutput(scalarCandidates, EmitResult);
		context.RegisterSourceOutput(complexCandidates, EmitResult);
	}

	static void EmitResult(SourceProductionContext context, ValueObjectGenerationResult result)
	{
		foreach (var diagnostic in result.Diagnostics)
			context.ReportDiagnostic(diagnostic);

		if (result.Source is not null && result.HintName is not null)
			context.AddSource(result.HintName, result.Source);
	}

	static ValueObjectGenerationResult BuildScalarGenerationResult(
		GeneratorAttributeSyntaxContext context,
		CancellationToken cancellationToken
	)
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (context.TargetSymbol is not INamedTypeSymbol typeSymbol || context.TargetNode is not TypeDeclarationSyntax)
			return ValueObjectGenerationResult.Empty;

		var attributes = typeSymbol.GetAttributes();
		var diagnostics = ValidateValueObjectType(typeSymbol, "Scalar", context.TargetNode.GetLocation());
		if (HasAttribute(attributes, ValueObjectAttributeName))
		{
			diagnostics.Add(
				Diagnostic.Create(
					GeneratorDiagnostics.ConflictingValueObjectAttributes,
					context.TargetNode.GetLocation(),
					typeSymbol.Name
				)
			);
			return new ValueObjectGenerationResult(null, null, [.. diagnostics]);
		}

		var scalarOptions = ScalarOptions.From(attributes, ScalarAttributeName);
		var scalarProperty = typeSymbol
			.GetMembers(scalarOptions.PropertyName)
			.OfType<IPropertySymbol>()
			.FirstOrDefault(property => !property.IsStatic && property.GetMethod is not null);

		if (scalarProperty is null)
		{
			diagnostics.Add(
				Diagnostic.Create(
					GeneratorDiagnostics.ScalarPropertyMissing,
					context.TargetNode.GetLocation(),
					typeSymbol.Name,
					scalarOptions.PropertyName
				)
			);
			return new ValueObjectGenerationResult(null, null, [.. diagnostics]);
		}

		var ctorExists = typeSymbol
			.Constructors.Where(static ctor => !ctor.IsStatic)
			.Any(ctor =>
				ctor.Parameters.Length == 1
				&& SymbolEqualityComparer.Default.Equals(ctor.Parameters[0].Type, scalarProperty.Type)
			);
		if (!ctorExists)
		{
			diagnostics.Add(
				Diagnostic.Create(
					GeneratorDiagnostics.ScalarConstructorMissing,
					context.TargetNode.GetLocation(),
					typeSymbol.Name,
					scalarProperty.Type.ToDisplayString()
				)
			);
		}

		var typeModel = BuildTypeModel(typeSymbol);
		if (typeModel is null)
			return new ValueObjectGenerationResult(null, null, [.. diagnostics]);

		var source = GenerateScalarSource(
			typeSymbol,
			typeModel.Value,
			scalarProperty,
			scalarOptions,
			ctorExists,
			diagnostics
		);
		var hintName = BuildHintName(typeSymbol, "ScalarValueObject");
		return new ValueObjectGenerationResult(hintName, source, [.. diagnostics]);
	}

	static ValueObjectGenerationResult BuildComplexGenerationResult(
		GeneratorAttributeSyntaxContext context,
		CancellationToken cancellationToken
	)
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (context.TargetSymbol is not INamedTypeSymbol typeSymbol || context.TargetNode is not TypeDeclarationSyntax)
			return ValueObjectGenerationResult.Empty;

		var attributes = typeSymbol.GetAttributes();
		var diagnostics = ValidateValueObjectType(typeSymbol, "ValueObject", context.TargetNode.GetLocation());
		if (HasAttribute(attributes, ScalarAttributeName))
		{
			diagnostics.Add(
				Diagnostic.Create(
					GeneratorDiagnostics.ConflictingValueObjectAttributes,
					context.TargetNode.GetLocation(),
					typeSymbol.Name
				)
			);
			return new ValueObjectGenerationResult(null, null, [.. diagnostics]);
		}

		var valueObjectOptions = ValueObjectOptions.From(attributes, ValueObjectAttributeName);
		var typeModel = BuildTypeModel(typeSymbol);
		if (typeModel is null)
			return new ValueObjectGenerationResult(null, null, [.. diagnostics]);

		var properties = typeSymbol
			.GetMembers()
			.OfType<IPropertySymbol>()
			.Where(property =>
				!property.IsStatic
				&& !property.IsIndexer
				&& property.GetMethod is not null
				&& SymbolEqualityComparer.Default.Equals(property.ContainingType, typeSymbol)
			)
			.OrderBy(property => property.Locations.FirstOrDefault()?.SourceSpan.Start ?? int.MaxValue)
			.ToArray();

		var ctorExists = typeSymbol
			.Constructors.Where(static ctor => !ctor.IsStatic)
			.Any(ctor => ConstructorMatches(ctor, properties));
		if (!ctorExists)
		{
			diagnostics.Add(
				Diagnostic.Create(
					GeneratorDiagnostics.ComplexHydrateConstructorMissing,
					context.TargetNode.GetLocation(),
					typeSymbol.Name
				)
			);
		}

		if (
			valueObjectOptions.DeserializationMode == StrictModeName
			&& !HasStaticFactory(typeSymbol, "Create", [.. properties.Select(property => property.Type)])
		)
		{
			diagnostics.Add(
				Diagnostic.Create(
					GeneratorDiagnostics.StrictDeserializationRequiresCreate,
					context.TargetNode.GetLocation(),
					typeSymbol.Name
				)
			);
		}

		var source = GenerateComplexSource(typeSymbol, typeModel.Value, properties, valueObjectOptions, ctorExists);
		var hintName = BuildHintName(typeSymbol, "ComplexValueObject");
		return new ValueObjectGenerationResult(hintName, source, [.. diagnostics]);
	}

	static List<Diagnostic> ValidateValueObjectType(
		INamedTypeSymbol typeSymbol,
		string attributeName,
		Location location
	)
	{
		List<Diagnostic> diagnostics = [];

		var isPartial = typeSymbol
			.DeclaringSyntaxReferences.Select(reference => reference.GetSyntax())
			.OfType<TypeDeclarationSyntax>()
			.Any(syntax => syntax.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.PartialKeyword)));
		if (!isPartial)
		{
			diagnostics.Add(
				Diagnostic.Create(
					GeneratorDiagnostics.ValueObjectMustBePartial,
					location,
					typeSymbol.Name,
					attributeName
				)
			);
		}

		if (typeSymbol.ContainingType is not null)
		{
			diagnostics.Add(
				Diagnostic.Create(
					GeneratorDiagnostics.NestedValueObjectsAreNotSupported,
					location,
					typeSymbol.Name,
					attributeName
				)
			);
		}

		if (typeSymbol.TypeParameters.Length > 0)
		{
			diagnostics.Add(
				Diagnostic.Create(
					GeneratorDiagnostics.GenericValueObjectsAreNotSupported,
					location,
					typeSymbol.Name,
					attributeName
				)
			);
		}

		return diagnostics;
	}

	static string GenerateScalarSource(
		INamedTypeSymbol typeSymbol,
		GeneratedTypeModel typeModel,
		IPropertySymbol scalarProperty,
		ScalarOptions options,
		bool ctorExists,
		List<Diagnostic> diagnostics
	)
	{
		var typeName = typeModel.FullyQualifiedName;
		var scalarTypeName = ToTypeName(scalarProperty.Type);
		var compareParameterTypeName = scalarProperty.Type.IsReferenceType ? $"{scalarTypeName}?" : scalarTypeName;
		var scalarCanBeNull =
			scalarProperty.Type.IsReferenceType
			|| scalarProperty.Type.NullableAnnotation == NullableAnnotation.Annotated;
		var scalarPropertyName = scalarProperty.Name;
		var createExists = HasStaticFactory(typeSymbol, "Create", [scalarProperty.Type]);
		var hydrateExists = HasStaticFactory(typeSymbol, "Hydrate", [scalarProperty.Type]);
		var tryCreateExists = HasTryCreate(typeSymbol, scalarProperty.Type);
		var compareToSelfExists = HasInstanceMethod(typeSymbol, "CompareTo", [typeSymbol]);
		var compareToPrimitiveExists = HasInstanceMethod(typeSymbol, "CompareTo", [scalarProperty.Type]);
		var compareToObjectExists = HasCompareToObject(typeSymbol);
		var toStringExists = HasParameterlessMethod(typeSymbol, "ToString");
		var hasJsonConverterAttribute = HasAttribute(typeSymbol, JsonConverterAttributeName);
		var declareOnNormalize = ShouldEmitScalarHookDeclaration(typeSymbol, "OnNormalize", 1, includeRef: true);
		var declareOnValidate = ShouldEmitScalarHookDeclaration(typeSymbol, "OnValidate", 1, includeRef: false);

		if (
			options.DeserializationMode == StrictModeName
			&& !createExists
			&& !HasStaticFactory(typeSymbol, "Create", [scalarProperty.Type])
		)
		{
			diagnostics.Add(
				Diagnostic.Create(
					GeneratorDiagnostics.StrictDeserializationRequiresCreate,
					typeSymbol.Locations.FirstOrDefault(),
					typeSymbol.Name
				)
			);
		}

		StringBuilder sb = new();
		sb.AppendLine("// <auto-generated />");
		sb.AppendLine("#nullable enable");
		sb.AppendLine();

		if (typeModel.Namespace is not null)
		{
			sb.AppendLine($"namespace {typeModel.Namespace}");
			sb.AppendLine("{");
		}

		var indent = typeModel.Namespace is null ? string.Empty : "\t";
		if (options.GenerateJsonConverter && !hasJsonConverterAttribute)
			sb.AppendLine(
				$"{indent}[global::System.Text.Json.Serialization.JsonConverter(typeof({typeModel.Name}JsonConverter))]"
			);

		sb.AppendLine(
			$"{indent}{typeModel.DeclarationPrefix} : global::Purview.EventSourcing.ValueObjects.IScalarValueObject<{typeName}, {scalarTypeName}>, global::System.IComparable<{typeName}>, global::System.IComparable<{scalarTypeName}>, global::System.IComparable"
		);
		sb.AppendLine($"{indent}{{");

		if (declareOnNormalize)
			sb.AppendLine($"{indent}\tstatic partial void OnNormalize(ref {scalarTypeName} value);");
		if (declareOnValidate)
			sb.AppendLine($"{indent}\tstatic partial void OnValidate({scalarTypeName} value);");
		if (declareOnNormalize || declareOnValidate)
			sb.AppendLine();

		if (!createExists)
		{
			sb.AppendLine(
				$@"{indent}	public static {typeName} Create({scalarTypeName} value)
{indent}	{{
{indent}		OnNormalize(ref value);
{indent}		OnValidate(value);
{indent}		{CreateNewOrThrow(typeName, ctorExists, "value")}
{indent}	}}"
			);
			sb.AppendLine();
		}

		if (!hydrateExists)
		{
			sb.AppendLine(
				$@"{indent}	public static {typeName} Hydrate({scalarTypeName} value)
{indent}	{{
{indent}		{CreateNewOrThrow(typeName, ctorExists, "value")}
{indent}	}}"
			);
			sb.AppendLine();
		}

		if (!tryCreateExists)
		{
			sb.AppendLine(
				$@"{indent}	public static bool TryCreate({scalarTypeName} value, out {typeName} result)
{indent}	{{
{indent}		try
{indent}		{{
{indent}			result = Create(value);
{indent}			return true;
{indent}		}}
{indent}		catch (global::System.ArgumentException)
{indent}		{{
{indent}			result = default!;
{indent}			return false;
{indent}		}}
{indent}	}}"
			);
			sb.AppendLine();
		}

		if (options.GenerateComparable)
		{
			if (!compareToSelfExists)
			{
				sb.AppendLine(
					$"{indent}\tpublic int CompareTo({typeName} other) => CompareTo(other.{scalarPropertyName});"
				);
			}

			if (!compareToPrimitiveExists)
			{
				sb.AppendLine(
					$"{indent}\tpublic int CompareTo({compareParameterTypeName} other) => global::System.Collections.Generic.Comparer<{scalarTypeName}>.Default.Compare({scalarPropertyName}, other);"
				);
			}

			if (!compareToObjectExists)
			{
				sb.AppendLine(
					$@"{indent}	public int CompareTo(object? obj)
{indent}	{{
{indent}		if (obj is null)
{indent}			return 1;
{indent}		if (obj is {typeName} otherValueObject)
{indent}			return CompareTo(otherValueObject);
{indent}		if (obj is {scalarTypeName} primitive)
{indent}			return CompareTo(primitive);
{indent}		throw new global::System.ArgumentException($""Object must be of type {{nameof({typeModel.Name})}} or {scalarTypeName}."", nameof(obj));
{indent}	}}"
				);
			}

			if (options.GenerateComparisonOperators)
			{
				EmitRelationalOperators(sb, indent, typeSymbol, typeName, typeName, "CompareTo(right)");

				var scalarAndSelfAreSameType = SymbolEqualityComparer.Default.Equals(scalarProperty.Type, typeSymbol);
				if (!scalarAndSelfAreSameType)
				{
					EmitRelationalOperators(sb, indent, typeSymbol, typeName, scalarTypeName, "CompareTo(right)");
				}
			}

			sb.AppendLine();
		}

		if (options.GenerateImplicitToPrimitive && !HasConversionOperator(typeSymbol, scalarProperty.Type, false))
		{
			sb.AppendLine(
				$"{indent}\tpublic static implicit operator {scalarTypeName}({typeName} valueObject) => valueObject.{scalarPropertyName};"
			);
		}

		if (
			options.GenerateImplicitFromPrimitive
			&& !HasContextualCreateOverload(typeSymbol, scalarProperty.Type)
			&& !HasConversionOperator(typeSymbol, scalarProperty.Type, true)
		)
		{
			sb.AppendLine(
				$"{indent}\tpublic static implicit operator {typeName}({scalarTypeName} value) => Create(value);"
			);
		}

		if (!toStringExists)
		{
			sb.AppendLine();
			sb.AppendLine(
				$"{indent}\tpublic override string ToString() => {scalarPropertyName}.ToString() ?? string.Empty;"
			);
		}

		if (options.GenerateJsonConverter)
		{
			sb.AppendLine();
			sb.AppendLine(
				$@"{indent}	sealed class {typeModel.Name}JsonConverter : global::System.Text.Json.Serialization.JsonConverter<{typeName}>
{indent}	{{
{indent}		public override {typeName} Read(ref global::System.Text.Json.Utf8JsonReader reader, global::System.Type typeToConvert, global::System.Text.Json.JsonSerializerOptions options)
{indent}		{{
{indent}			var value = global::System.Text.Json.JsonSerializer.Deserialize<{scalarTypeName}>(ref reader, options);
{(scalarCanBeNull ? $"{indent}\t\t\tif (value is null)\n{indent}\t\t\t\tthrow new global::System.Text.Json.JsonException(\"{typeModel.Name} cannot be null.\");\n" : string.Empty)}{indent}			return {(options.DeserializationMode == StrictModeName ? "Create" : "Hydrate")}(value);
{indent}		}}

{indent}		public override void Write(global::System.Text.Json.Utf8JsonWriter writer, {typeName} value, global::System.Text.Json.JsonSerializerOptions options) =>
{indent}			global::System.Text.Json.JsonSerializer.Serialize(writer, value.{scalarPropertyName}, options);
{indent}	}}"
			);
		}

		sb.AppendLine($"{indent}}}");
		if (typeModel.Namespace is not null)
			sb.AppendLine("}");

		return sb.ToString();
	}

	static string GenerateComplexSource(
		INamedTypeSymbol typeSymbol,
		GeneratedTypeModel typeModel,
		IPropertySymbol[] properties,
		ValueObjectOptions options,
		bool ctorExists
	)
	{
		var typeName = typeModel.FullyQualifiedName;
		var propertyTypeNames = properties.Select(property => ToTypeName(property.Type)).ToArray();
		var propertyNames = properties.Select(property => property.Name).ToArray();
		var hydrateExists = HasStaticFactory(typeSymbol, "Hydrate", [.. properties.Select(property => property.Type)]);
		var compareToSelfExists = HasInstanceMethod(typeSymbol, "CompareTo", [typeSymbol]);
		var compareToObjectExists = HasCompareToObject(typeSymbol);
		var hasJsonConverterAttribute = HasAttribute(typeSymbol, JsonConverterAttributeName);
		var strictCreateExists = HasStaticFactory(
			typeSymbol,
			"Create",
			[.. properties.Select(property => property.Type)]
		);

		var hydrateFactoryName =
			options.DeserializationMode == StrictModeName && strictCreateExists ? "Create" : "Hydrate";

		StringBuilder sb = new();
		sb.AppendLine("// <auto-generated />");
		sb.AppendLine("#nullable enable");
		sb.AppendLine();

		if (typeModel.Namespace is not null)
		{
			sb.AppendLine($"namespace {typeModel.Namespace}");
			sb.AppendLine("{");
		}

		var indent = typeModel.Namespace is null ? string.Empty : "\t";
		if (options.GenerateJsonConverter && !hasJsonConverterAttribute)
			sb.AppendLine(
				$"{indent}[global::System.Text.Json.Serialization.JsonConverter(typeof({typeModel.Name}JsonConverter))]"
			);

		sb.AppendLine(
			$"{indent}{typeModel.DeclarationPrefix} : global::Purview.EventSourcing.ValueObjects.IValueObject<{typeName}>, global::System.IComparable<{typeName}>, global::System.IComparable"
		);
		sb.AppendLine($"{indent}{{");

		if (!hydrateExists)
		{
			var hydrateParams = string.Join(
				", ",
				propertyTypeNames.Zip(propertyNames, static (type, name) => $"{type} {ToCamelCase(name)}")
			);
			var hydrateArgs = string.Join(", ", propertyNames.Select(ToCamelCase));
			sb.AppendLine(
				$@"{indent}	public static {typeName} Hydrate({hydrateParams})
{indent}	{{
{indent}		{CreateNewOrThrow(typeName, ctorExists, hydrateArgs)}
{indent}	}}"
			);
			sb.AppendLine();
		}

		if (options.GenerateComparable)
		{
			if (!compareToSelfExists)
			{
				sb.AppendLine(
					$@"{indent}	public int CompareTo({typeName} other)
{indent}	{{"
				);
				if (typeSymbol.TypeKind == TypeKind.Class)
				{
					sb.AppendLine(
						$@"{indent}		if (other is null)
{indent}			return 1;"
					);
				}

				for (var i = 0; i < properties.Length; i++)
				{
					var prop = properties[i];
					var propTypeName = propertyTypeNames[i];
					sb.AppendLine(
						$@"{indent}		var compare{prop.Name} = global::System.Collections.Generic.Comparer<{propTypeName}>.Default.Compare({prop.Name}, other.{prop.Name});
{indent}		if (compare{prop.Name} != 0)
{indent}			return compare{prop.Name};"
					);
				}

				sb.AppendLine(
					$@"{indent}		return 0;
{indent}	}}"
				);
			}

			if (!compareToObjectExists)
			{
				sb.AppendLine(
					$@"{indent}	public int CompareTo(object? obj)
{indent}	{{
{indent}		if (obj is null)
{indent}			return 1;
{indent}		if (obj is {typeName} otherValueObject)
{indent}			return CompareTo(otherValueObject);
{indent}		throw new global::System.ArgumentException($""Object must be of type {{nameof({typeModel.Name})}}."", nameof(obj));
{indent}	}}"
				);
			}

			if (options.GenerateComparisonOperators)
			{
				EmitRelationalOperators(sb, indent, typeSymbol, typeName, typeName, "CompareTo(right)");
			}
		}

		if (options.GenerateJsonConverter)
		{
			var modelTypeName = $"{typeModel.Name}JsonModel";
			var toModelAssignments = string.Join(", ", propertyNames.Select(static name => $"{name} = value.{name}"));
			var hydrateArgs = string.Join(", ", propertyNames.Select(static name => $"model.{name}"));

			sb.AppendLine();
			sb.AppendLine(
				$@"{indent}	sealed class {typeModel.Name}JsonConverter : global::System.Text.Json.Serialization.JsonConverter<{typeName}>
{indent}	{{
{indent}		public override {typeName} Read(ref global::System.Text.Json.Utf8JsonReader reader, global::System.Type typeToConvert, global::System.Text.Json.JsonSerializerOptions options)
{indent}		{{
{indent}			var model = global::System.Text.Json.JsonSerializer.Deserialize<{modelTypeName}>(ref reader, options);
{indent}			if (model is null)
{indent}				throw new global::System.Text.Json.JsonException(""Unable to deserialize {typeModel.Name}."");
{indent}			return {hydrateFactoryName}({hydrateArgs});
{indent}		}}

{indent}		public override void Write(global::System.Text.Json.Utf8JsonWriter writer, {typeName} value, global::System.Text.Json.JsonSerializerOptions options)
{indent}		{{
{indent}			var model = new {modelTypeName} {{ {toModelAssignments} }};
{indent}			global::System.Text.Json.JsonSerializer.Serialize(writer, model, options);
{indent}		}}
{indent}	}}

{indent}	sealed class {modelTypeName}
{indent}	{{"
			);

			for (var i = 0; i < properties.Length; i++)
			{
				sb.AppendLine(
					$"{indent}\t\tpublic {propertyTypeNames[i]} {propertyNames[i]} {{ get; set; }} = default!;"
				);
			}

			sb.AppendLine($"{indent}\t}}");
		}

		sb.AppendLine($"{indent}}}");
		if (typeModel.Namespace is not null)
			sb.AppendLine("}");

		return sb.ToString();
	}

	static bool ConstructorMatches(IMethodSymbol constructor, IPropertySymbol[] properties)
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

	static string CreateNewOrThrow(string typeName, bool ctorExists, string ctorArgumentExpression) =>
		ctorExists
			? $"return new({ctorArgumentExpression});"
			: $@"throw new global::System.NotSupportedException(""Generated factory requires constructor for {typeName}."");";

	static bool HasAttribute(INamedTypeSymbol typeSymbol, string metadataName) =>
		typeSymbol.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == metadataName);

	static bool HasAttribute(ImmutableArray<AttributeData> attributes, string metadataName) =>
		attributes.Any(attribute => attribute.AttributeClass?.ToDisplayString() == metadataName);

	static bool HasStaticFactory(INamedTypeSymbol typeSymbol, string name, ITypeSymbol[] parameterTypes)
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

	static bool HasTryCreate(INamedTypeSymbol typeSymbol, ITypeSymbol scalarType)
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

	static bool HasInstanceMethod(INamedTypeSymbol typeSymbol, string name, IReadOnlyList<ITypeSymbol> parameterTypes)
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

	static bool HasCompareToObject(INamedTypeSymbol typeSymbol) =>
		typeSymbol
			.GetMembers("CompareTo")
			.OfType<IMethodSymbol>()
			.Any(method =>
				!method.IsStatic
				&& method.Parameters.Length == 1
				&& method.Parameters[0].Type.SpecialType == SpecialType.System_Object
			);

	static void EmitRelationalOperators(
		StringBuilder sb,
		string indent,
		INamedTypeSymbol declaringType,
		string leftTypeName,
		string rightTypeName,
		string compareExpression
	)
	{
		EmitRelationalOperator(
			sb,
			indent,
			declaringType,
			LessThanOperatorName,
			"<",
			leftTypeName,
			rightTypeName,
			compareExpression,
			"< 0"
		);
		EmitRelationalOperator(
			sb,
			indent,
			declaringType,
			GreaterThanOperatorName,
			">",
			leftTypeName,
			rightTypeName,
			compareExpression,
			"> 0"
		);
		EmitRelationalOperator(
			sb,
			indent,
			declaringType,
			LessThanOrEqualOperatorName,
			"<=",
			leftTypeName,
			rightTypeName,
			compareExpression,
			"<= 0"
		);
		EmitRelationalOperator(
			sb,
			indent,
			declaringType,
			GreaterThanOrEqualOperatorName,
			">=",
			leftTypeName,
			rightTypeName,
			compareExpression,
			">= 0"
		);
	}

	static void EmitRelationalOperator(
		StringBuilder sb,
		string indent,
		INamedTypeSymbol declaringType,
		string operatorMethodName,
		string operatorToken,
		string leftTypeName,
		string rightTypeName,
		string compareExpression,
		string comparisonSuffix
	)
	{
		if (HasRelationalOperator(declaringType, operatorMethodName, leftTypeName, rightTypeName))
			return;

		sb.AppendLine(
			$@"{indent}	public static bool operator {operatorToken}({leftTypeName} left, {rightTypeName} right)
{indent}	{{
{indent}		return left.{compareExpression} {comparisonSuffix};
{indent}	}}"
		);
	}

	static bool HasParameterlessMethod(INamedTypeSymbol typeSymbol, string name) =>
		typeSymbol
			.GetMembers(name)
			.OfType<IMethodSymbol>()
			.Any(method => !method.IsStatic && method.Parameters.Length == 0);

	static bool ParametersMatch(ImmutableArray<IParameterSymbol> parameters, IReadOnlyList<ITypeSymbol> expected)
	{
		for (var i = 0; i < expected.Count; i++)
		{
			if (!SymbolEqualityComparer.Default.Equals(parameters[i].Type, expected[i]))
				return false;
		}

		return true;
	}

	static bool HasConversionOperator(INamedTypeSymbol typeSymbol, ITypeSymbol primitiveType, bool fromPrimitive)
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

	static bool HasRelationalOperator(
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

	static bool HasContextualCreateOverload(INamedTypeSymbol typeSymbol, ITypeSymbol primitiveType)
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

	static bool ShouldEmitScalarHookDeclaration(
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

	static GeneratedTypeModel? BuildTypeModel(INamedTypeSymbol typeSymbol)
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

	static string BuildHintName(INamedTypeSymbol typeSymbol, string suffix)
	{
		var fullName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
		var hash = ComputeStableHash(fullName);
		return $"{typeSymbol.Name}_{suffix}_{hash:X16}.g.cs";
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

	static string ToTypeName(ITypeSymbol typeSymbol) =>
		typeSymbol.ToDisplayString(
			SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
				SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions
					| SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
			)
		);

	static string ToCamelCase(string value) =>
		string.IsNullOrEmpty(value) ? value : char.ToLowerInvariant(value[0]) + value.Substring(1);

	readonly struct ValueObjectGenerationResult(
		string? hintName,
		string? source,
		ImmutableArray<Diagnostic> diagnostics
	)
	{
		public string? HintName { get; } = hintName;

		public string? Source { get; } = source;

		public ImmutableArray<Diagnostic> Diagnostics { get; } = diagnostics;

		public static ValueObjectGenerationResult Empty { get; } = new(null, null, []);
	}

	readonly struct GeneratedTypeModel(
		string name,
		string? @namespace,
		string declarationPrefix,
		string fullyQualifiedName
	)
	{
		public string Name { get; } = name;

		public string? Namespace { get; } = @namespace;

		public string DeclarationPrefix { get; } = declarationPrefix;

		public string FullyQualifiedName { get; } = fullyQualifiedName;
	}

	readonly struct ScalarOptions(
		string propertyName,
		bool generateJsonConverter,
		bool generateComparable,
		bool generateComparisonOperators,
		bool generateImplicitFromPrimitive,
		bool generateImplicitToPrimitive,
		string deserializationMode
	)
	{
		public string PropertyName { get; } = propertyName;

		public bool GenerateJsonConverter { get; } = generateJsonConverter;

		public bool GenerateComparable { get; } = generateComparable;

		public bool GenerateComparisonOperators { get; } = generateComparisonOperators;

		public bool GenerateImplicitFromPrimitive { get; } = generateImplicitFromPrimitive;

		public bool GenerateImplicitToPrimitive { get; } = generateImplicitToPrimitive;

		public string DeserializationMode { get; } = deserializationMode;

		public static ScalarOptions From(INamedTypeSymbol typeSymbol, string metadataName) =>
			From(typeSymbol.GetAttributes(), metadataName);

		public static ScalarOptions From(ImmutableArray<AttributeData> attributes, string metadataName)
		{
			var attribute = attributes.First(data => data.AttributeClass?.ToDisplayString() == metadataName);

			var propertyName =
				attribute.ConstructorArguments.Length > 0 && attribute.ConstructorArguments[0].Value is string value
					? value
					: "Value";

			return new ScalarOptions(
				propertyName,
				GetNamedBool(attribute.NamedArguments, "GenerateJsonConverter", true),
				GetNamedBool(attribute.NamedArguments, "GenerateComparable", true),
				GetNamedBool(attribute.NamedArguments, "GenerateComparisonOperators", true),
				GetNamedBool(attribute.NamedArguments, "GenerateImplicitFromPrimitive", true),
				GetNamedBool(attribute.NamedArguments, "GenerateImplicitToPrimitive", true),
				GetDeserializationMode(attribute.NamedArguments, "DeserializationMode")
			);
		}
	}

	readonly struct ValueObjectOptions(
		bool generateJsonConverter,
		bool generateComparable,
		bool generateComparisonOperators,
		string deserializationMode
	)
	{
		public bool GenerateJsonConverter { get; } = generateJsonConverter;

		public bool GenerateComparable { get; } = generateComparable;

		public bool GenerateComparisonOperators { get; } = generateComparisonOperators;

		public string DeserializationMode { get; } = deserializationMode;

		public static ValueObjectOptions From(INamedTypeSymbol typeSymbol, string metadataName) =>
			From(typeSymbol.GetAttributes(), metadataName);

		public static ValueObjectOptions From(ImmutableArray<AttributeData> attributes, string metadataName)
		{
			var attribute = attributes.First(data => data.AttributeClass?.ToDisplayString() == metadataName);

			return new ValueObjectOptions(
				GetNamedBool(attribute.NamedArguments, "GenerateJsonConverter", true),
				GetNamedBool(attribute.NamedArguments, "GenerateComparable", true),
				GetNamedBool(attribute.NamedArguments, "GenerateComparisonOperators", true),
				GetDeserializationMode(attribute.NamedArguments, "DeserializationMode")
			);
		}
	}

	static bool GetNamedBool(
		ImmutableArray<KeyValuePair<string, TypedConstant>> namedArguments,
		string key,
		bool defaultValue
	)
	{
		foreach (var namedArgument in namedArguments)
		{
			if (namedArgument.Key == key && namedArgument.Value.Value is bool value)
				return value;
		}

		return defaultValue;
	}

	static string GetDeserializationMode(ImmutableArray<KeyValuePair<string, TypedConstant>> namedArguments, string key)
	{
		foreach (var namedArgument in namedArguments)
		{
			if (namedArgument.Key != key)
				continue;

			if (namedArgument.Value.Value is int mode)
				return mode == 1 ? StrictModeName : HydrateModeName;
		}

		return HydrateModeName;
	}
}
