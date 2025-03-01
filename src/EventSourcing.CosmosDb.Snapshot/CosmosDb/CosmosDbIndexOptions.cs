using System.ComponentModel.DataAnnotations;
using Microsoft.Azure.Cosmos;

namespace Purview.EventSourcing.CosmosDb;

public class CosmosDbIndexOptions
{
	public bool Automatic { get; set; } = true;

	[EnumDataType(typeof(IndexingMode))]
	public IndexingMode IndexingModel { get; set; } = IndexingMode.Consistent;

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "DTO")]
	public string[] IncludedPaths { get; set; } = [];

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "DTO")]
	public string[] ExcludedPaths { get; set; } = [];

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "DTO")]
	public SpatialPath[] SpatialIndices { get; set; } = [];

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "DTO")]
	public CompositePath[][] CompositeIndices { get; set; } = [];

	public override int GetHashCode()
	{
		HashCode hashCode = new();

		hashCode.Add(IndexingModel);
		if (IncludedPaths != null)
			hashCode.Add(IncludedPaths);

		if (ExcludedPaths != null)
			hashCode.Add(ExcludedPaths);

		if (SpatialIndices != null)
			hashCode.Add(SpatialIndices);

		if (CompositeIndices != null)
			hashCode.Add(CompositeIndices);

		return hashCode.ToHashCode();
	}
}
