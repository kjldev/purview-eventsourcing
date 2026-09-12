using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Purview.EventSourcing.EntityFrameworkCore.SourceGenerator.Heleprs;

namespace Purview.EventSourcing.EntityFrameworkCore.SourceGenerator;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EFSnapshotShapeAnalyzer : DiagnosticAnalyzer
{
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => DiagnosticLibrary.SupportedDiagnostics;

	public override void Initialize(AnalysisContext context)
	{
		if (context is null)
			throw new ArgumentNullException(nameof(context));

		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();
		context.RegisterCompilationAction(AnalyzeSnapshotShapes);
		context.RegisterSyntaxNodeAction(
			AnalyzeOpaqueQueryUse,
			Microsoft.CodeAnalysis.CSharp.SyntaxKind.SimpleMemberAccessExpression
		);
	}

	static void AnalyzeSnapshotShapes(CompilationAnalysisContext context)
	{
		HashSet<ITypeSymbol> visited = new(SymbolEqualityComparer.Default);
		foreach (var type in GetSourceTypes(context.Compilation.Assembly.GlobalNamespace))
		{
			if (TypeHelpers.HasAttribute(type, TypeLibrary.AggregateAttribute))
				AnalyzeType(type, context, visited);
		}
	}

	static void AnalyzeType(ITypeSymbol type, CompilationAnalysisContext context, HashSet<ITypeSymbol> visited)
	{
		type = UnwrapCollection(type);
		if (
			!visited.Add(type)
			|| !type.Locations.Any(static location => location.IsInSource)
			|| type.SpecialType != SpecialType.None
			|| type.TypeKind is TypeKind.Enum or TypeKind.TypeParameter
		)
		{
			return;
		}

		foreach (
			var property in type.GetMembers()
				.OfType<IPropertySymbol>()
				.Where(static property => !property.IsStatic && property.DeclaredAccessibility == Accessibility.Public)
		)
		{
			if (IsDictionaryLike(property.Type))
			{
				if (!HasOpaqueAttribute(property))
				{
					context.ReportDiagnostic(
						Diagnostic.Create(
							DiagnosticLibrary.UnsupportedDictionary,
							property.Locations.FirstOrDefault(),
							property.Name,
							property.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
						)
					);
				}

				continue;
			}

			if (!HasOpaqueAttribute(property))
				AnalyzeType(property.Type, context, visited);
		}
	}

	static ITypeSymbol UnwrapCollection(ITypeSymbol type)
	{
		if (type is IArrayTypeSymbol array)
			return array.ElementType;

		if (type is INamedTypeSymbol named)
		{
			var enumerable = named.AllInterfaces.FirstOrDefault(static item =>
				item.IsGenericType && TypeHelpers.IsCollectionLike(item.ConstructedFrom)
			);
			if (enumerable is not null)
				return enumerable.TypeArguments[0];
		}

		return type;
	}

	static IEnumerable<INamedTypeSymbol> GetSourceTypes(INamespaceSymbol namespaceSymbol)
	{
		foreach (var member in namespaceSymbol.GetMembers())
		{
			if (member is INamespaceSymbol childNamespace)
			{
				foreach (var type in GetSourceTypes(childNamespace))
					yield return type;
			}
			else if (member is INamedTypeSymbol type && type.Locations.Any(static location => location.IsInSource))
			{
				yield return type;
			}
		}
	}

	static void AnalyzeOpaqueQueryUse(SyntaxNodeAnalysisContext context)
	{
		var access = (MemberAccessExpressionSyntax)context.Node;
		if (
			context.SemanticModel.GetSymbolInfo(access, context.CancellationToken).Symbol
				is not IPropertySymbol property
			|| !HasOpaqueAttribute(property)
			|| !IsInsideSnapshotQuery(access, context.SemanticModel, context.CancellationToken)
		)
			return;

		context.ReportDiagnostic(
			Diagnostic.Create(DiagnosticLibrary.OpaqueQuery, access.Name.GetLocation(), property.Name)
		);
	}

	static bool IsInsideSnapshotQuery(SyntaxNode node, SemanticModel model, CancellationToken cancellationToken)
	{
		foreach (var invocation in node.Ancestors().OfType<InvocationExpressionSyntax>())
		{
			if (model.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol method)
				continue;

			if (method.Name is "QueryAsync" or "FirstOrDefaultAsync" or "SingleOrDefaultAsync")
				return true;
		}

		return false;
	}

	static bool HasOpaqueAttribute(IPropertySymbol property) =>
		TypeHelpers.HasAttribute(property, TypeLibrary.EFOpaqueAttribute);

	static bool IsDictionaryLike(ITypeSymbol type) => type is INamedTypeSymbol named ? IsDictionaryType(named) : false;

	static bool IsDictionaryType(INamedTypeSymbol type) =>
		type.IsGenericType
			? TypeHelpers.Is(
				type,
				TypeLibrary.DictionaryKV,
				TypeLibrary.IDictionaryKV,
				TypeLibrary.IReadOnlyDictionaryKV
			)
			: false;
}
