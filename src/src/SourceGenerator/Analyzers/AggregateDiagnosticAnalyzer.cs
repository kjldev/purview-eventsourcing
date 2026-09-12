using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Purview.EventSourcing.SourceGenerator.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AggregateDiagnosticAnalyzer : DiagnosticAnalyzer
{
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
		[
			DiagnosticLibrary.AggregateBaseReferenceMissing,
			DiagnosticLibrary.AggregateMustBePartial,
			DiagnosticLibrary.AggregateMustInheritAggregateBase,
			DiagnosticLibrary.NestedAggregatesAreNotSupported,
			DiagnosticLibrary.GenericAggregatesAreNotSupported,
			DiagnosticLibrary.ManualRegisterEventsIsNotSupported,
			DiagnosticLibrary.AggregatePropertySetterShouldBePrivate,
			DiagnosticLibrary.AggregatePropertyCollectionTypeMustUseEventStoreCollections,
			DiagnosticLibrary.ScalarComplexValueMayNotTranslateInSQLSnapshots,
			DiagnosticLibrary.EventMethodMustBePartial,
			DiagnosticLibrary.UnsupportedEventMethodSignature,
			DiagnosticLibrary.EventSchemaVersionMustBePositive,
			DiagnosticLibrary.DuplicateGeneratedEventName,
			DiagnosticLibrary.DuplicateEventSchemaVersionOnAggregate,
			DiagnosticLibrary.EventParameterMustMapToWritableProperty,
			DiagnosticLibrary.EventParameterNullabilityMismatch,
			DiagnosticLibrary.AggregateMethodShouldBeVerbPhrase,
			DiagnosticLibrary.EventNameOverrideShouldBePastTense,
			DiagnosticLibrary.UnableToInferEventName,
		];

	public override void Initialize(AnalysisContext context)
	{
		if (context is null)
			throw new ArgumentNullException(nameof(context));

		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();

		context.RegisterSymbolAction(ValidateAggregate, SymbolKind.NamedType);
	}

	static void ValidateAggregate(SymbolAnalysisContext context)
	{
		var typeSymbol = (INamedTypeSymbol)context.Symbol;

		if (!TypeHelpers.HasAttribute(typeSymbol, TypeLibrary.Purview.EventSourcing.Aggregates.AggregateAttribute))
			return;

		if (
			typeSymbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(context.CancellationToken)
			is not ClassDeclarationSyntax syntax
		)
			return;

		var result = AggregateInfoBuilder.Build(typeSymbol, syntax, context.Compilation, context.CancellationToken);

		foreach (var diagnostic in result.Diagnostics)
			context.ReportDiagnostic(diagnostic.ToDiagnostic());

		if (
			context.Compilation.GetTypeByMetadataName(
				TypeLibrary.Purview.EventSourcing.Aggregates.AggregateBase.MetadataFullName
			)
			is null
		)
		{
			context.ReportDiagnostic(
				Diagnostic.Create(
					DiagnosticLibrary.AggregateBaseReferenceMissing,
					typeSymbol.Locations.FirstOrDefault()
				)
			);
		}
	}
}
