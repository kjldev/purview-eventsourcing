using System.Diagnostics.CodeAnalysis;

namespace Purview.EventSourcing;

/// <summary>
/// Provides continuation support for queries/ list results.
/// </summary>
/// <typeparam name="T">The generic type to return.</typeparam>
/// <remarks>An instance of <typeparamref name="T"/> can never be null.</remarks>
public class ContinuationResponse<T>
	where T : notnull
{
	/// <summary>
	/// Implicit conversion to <see cref="ContinuationRequest"/>.
	/// </summary>
	/// <param name="response"></param>
	public static implicit operator ContinuationRequest([NotNull] ContinuationResponse<T> response) =>
		response.ToRequest();

	/// <summary>
	/// If there are more records, use this token to provide access to the next set.
	/// </summary>
	public string? ContinuationToken { get; init; }

	/// <summary>
	/// The requested records, if any.
	/// </summary>
	[SuppressMessage("Performance", "CA1819:Properties should not return arrays")]
	public T[] Results { get; set; } = [];

	/// <summary>
	/// Gets the total count of the query, if available. Set when <see cref="ContinuationRequest.IncludeTotalCount"/> is <see langword="true"/>.
	/// </summary>
	public long? TotalCount { get; set; }

	/// <summary>
	/// The requested record count, in this page of data.
	/// </summary>
	/// <remarks>Usually from <see cref="ContinuationRequest.MaxRecords"/>.</remarks>
	public int RequestedCount { get; init; }

	/// <summary>
	/// The count of results returned.
	/// </summary>
	public int ResultCount => Results?.Length ?? 0;

	/// <summary>
	/// True if <see cref="ResultCount"/> is greater than zero. Otherwise, false.
	/// </summary>
	public bool HasRecords => ResultCount > 0;

	/// <summary>
	/// Indicates if more records are available by using the continuation token.
	/// </summary>
	public bool HasMoreRecords => !string.IsNullOrWhiteSpace(ContinuationToken);

	/// <summary>
	/// Converts through the use of a converter func, provided by the user, to
	/// change the <see cref="Results"/> type.
	/// </summary>
	/// <typeparam name="TTo">The new result model.</typeparam>
	/// <param name="convert">The <see cref="Func{T, TTo}"/> used to convert models.</param>
	/// <returns></returns>
	public ContinuationResponse<TTo> Convert<TTo>(Func<T, TTo?> convert)
		where TTo : class => CreateResponse<ContinuationResponse<TTo>, TTo>(convert);

	/// <summary>
	/// Creates a <see cref="ContinuationRequest"/> from the current response.
	/// </summary>
	/// <returns></returns>
	public ContinuationRequest ToRequest() => CreateRequest<ContinuationRequest>();

	protected TRequest CreateRequest<TRequest>()
		where TRequest : ContinuationRequest, new()
	{
		return new() { ContinuationToken = ContinuationToken, MaxRecords = RequestedCount };
	}

	protected TRequest CreateResponse<TRequest, TTo>(Func<T, TTo?> convert)
		where TRequest : ContinuationResponse<TTo>, new()
		where TTo : class
	{
		return new()
		{
			ContinuationToken = ContinuationToken,
			RequestedCount = RequestedCount,
			Results = [.. Results.Select(convert).Where(m => m != null).Cast<TTo>()],
		};
	}
}
