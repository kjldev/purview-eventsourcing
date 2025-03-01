using System.Diagnostics.CodeAnalysis;

namespace Purview.EventSourcing;

/// <summary>
/// The result of an exists operations on an aggregate. Is implicitly comparable to a <see cref="bool" />.
/// </summary>
/// <remarks>
/// <para>
/// When converting to a <see cref="bool"/>, when the <see cref="Status" /> is
/// <see cref="ExistsStatus.Exists"/> or <see cref="ExistsStatus.ExistsInDeletedState"/>, true is returned.
/// </para>
/// <para>Otherwise, false is returned.</para>
/// </remarks>
[System.Diagnostics.DebuggerStepThrough]
public readonly record struct ExistsState(ExistsStatus Status, int? Version)
{
	/// <summary>
	/// Converts this instance of a <see cref="ExistsState"/> to a <see cref="bool"/>.
	/// <param name="state">The value to convert.</param>
	/// <returns>Returns true if the <see cref="Status"/> equals <see cref="ExistsStatus.Exists"/>
	/// </summary>
	public static implicit operator bool([NotNull] ExistsState state)
		=> state.Status is
			ExistsStatus.Exists
			or ExistsStatus.ExistsInDeletedState;

	/// <summary>
	/// Represents a value that indicates the aggregate does not exist.
	/// </summary>
	public static ExistsState DoesNotExists => new(ExistsStatus.DoesNotExist, null);

	/// <summary>
	/// Represents a value that indicates the aggregate does exist.
	/// </summary>
	public static ExistsState Exists => new(ExistsStatus.Exists, null);

	/// <summary>
	/// Represents a value that indicates the aggregate exists, but is in a deleted state.
	/// </summary>
	public static ExistsState ExistsInDeletedState => new(ExistsStatus.ExistsInDeletedState, null);

	/// <summary>
	/// If <see cref="Status"/> is <see cref="ExistsStatus.Exists"/>
	/// or <see cref="ExistsStatus.ExistsInDeletedState"/>, then returns true.
	/// Otherwise, returns false.
	/// </summary>
	public bool DoesExist => this;
}
