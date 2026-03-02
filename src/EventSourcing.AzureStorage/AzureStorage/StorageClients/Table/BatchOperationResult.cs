using System.Collections;
using System.Diagnostics.CodeAnalysis;
using Azure;

namespace Purview.EventSourcing.AzureStorage.StorageClients.Table;

sealed class BatchOperationResult(Dictionary<int, Response[]> responses) : IEnumerable<Response>
{
	readonly Dictionary<int, Response[]> _responses = responses;

	public static implicit operator Response[]([NotNull] BatchOperationResult batch) =>
		[.. batch._responses.SelectMany(m => m.Value)];

	public int BatchCount => _responses.Count;

	public int Count => Responses.Length;

	public Response[] Responses => this;

	public IEnumerator<Response> GetEnumerator() => Responses.AsEnumerable().GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() => Responses.GetEnumerator();
}
