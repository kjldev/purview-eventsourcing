using System.Diagnostics.CodeAnalysis;
using FluentValidation;
using FluentValidation.Results;

namespace Purview.EventSourcing.Aggregates;

/// <summary>
/// Represents the results of a save operation on a <typeparamref name="TAggregate"/>.
/// </summary>
/// <remarks>
/// A save operation could be due to <see cref="IEventStore{T}.SaveAsync(T, EventStoreOperationContext?, CancellationToken)"/>,
/// <see cref="IEventStore{T}.DeleteAsync(T, EventStoreOperationContext?, CancellationToken)" /> or
/// the <see cref="IEventStore{T}.RestoreAsync(T, EventStoreOperationContext?, CancellationToken)"/> operations.
/// </remarks>
/// <typeparam name="TAggregate">The <see cref="IAggregate"/> where a save operations was attempted.</typeparam>
public record class SaveResult<TAggregate>
	where TAggregate : IAggregate
{
	/// <summary>
	/// Converts a <see cref="SaveResult{TAggregate}"/> to a <see cref="bool"/>.
	/// </summary>
	/// <param name="aggregateSaveResult">The save result to convert.</param>
	/// <seealso cref="ToBoolean"/>
	public static implicit operator bool([NotNull] SaveResult<TAggregate> aggregateSaveResult)
		=> aggregateSaveResult.Saved && aggregateSaveResult.IsValid;

	/// <summary>
	/// Converts a <see cref="SaveResult{TAggregate}"/> to an <typeparamref name="TAggregate"/>.
	/// </summary>
	/// <param name="aggregateSaveResult">The save result to convert.</param>
	/// <seealso cref="ToAggregate"/>
	public static implicit operator TAggregate([NotNull] SaveResult<TAggregate> aggregateSaveResult)
		=> aggregateSaveResult.Aggregate;

	/// <summary>
	/// Constructs a new <see cref="SaveResult{TAggregate}"/> based on the saved and validation state
	/// from an <see cref="IEventStore{T}"/>.
	/// </summary>
	/// <param name="aggregate">The aggregate that was processed.</param>
	/// <param name="validationResult">The validation result.</param>
	/// <param name="saved">Indicates if the operations resulted in a save operation.</param>
	/// <param name="skipped">Indicates if, while the result of <paramref name="saved"/> maybe true,
	/// in-fact the results affected no changes.</param>
	public SaveResult(TAggregate aggregate, ValidationResult validationResult, bool saved, bool skipped)
	{
		Aggregate = aggregate;
		ValidationResult = validationResult;
		Saved = saved;
		Skipped = skipped;
	}

	/// <summary>
	/// The <typeparamref name="TAggregate">aggregate</typeparamref> where a save operation
	/// was attempted.
	/// </summary>
	public TAggregate Aggregate { get; }

	/// <summary>
	/// The result of the validation.
	/// </summary>
	public ValidationResult ValidationResult { get; }

	/// <summary>
	/// Indicates if the <see cref="ValidationResult"/>
	/// denotes failures or success.
	/// </summary>
	public bool IsValid => ValidationResult.IsValid;

	/// <summary>
	/// Indicates if the save operation was successful.
	/// </summary>
	/// <remarks>A value of false does not always mean failure, it may mean
	/// there was no events to save, or the events were already saved - in the case
	/// where idempotency markers are recorded and validated.</remarks>
	public bool Saved { get; }

	/// <summary>
	/// Indicates the result from a <see cref="IEventStore{T}.SaveAsync(T, EventStoreOperationContext?, CancellationToken)"/>
	/// operation was skipped.
	/// </summary>
	/// <remarks><para>
	/// This result is not featured in the ability to convert to a <see cref="bool"/> via <see cref="ToBoolean"/>
	/// or the implicit case.
	/// </para>
	/// <para>This maybe true when checking idempotency markers, or having not events to save.</para>
	/// </remarks>
	public bool Skipped { get; }

	/// <summary>
	/// Converts this <see cref="SaveResult{TAggregate}"/> to a <see cref="bool"/>
	/// to quickly indicate success or failure of the save operation.
	/// </summary>
	/// <returns>Returns true if <see cref="Saved"/> and <see cref="IsValid"/> is true,
	/// otherwise, returns false.</returns>
	public bool ToBoolean() => this;

	/// <summary>
	/// Converts this <see cref="SaveResult{TAggregate}"/> to an <typeparamref name="TAggregate"/>.
	/// </summary>
	/// <returns>Returns the <see cref="Aggregate"/> instance.</returns>
	public TAggregate ToAggregate()
	{
		EnsureValid();

		return this;
	}

	/// <summary>
	/// If <see cref="IsValid"/> is false, a <see cref="ValidationException"/> is thrown.
	/// </summary>
	public void EnsureValid()
	{
		if (!IsValid)
			throw new ValidationException(ValidationResult.Errors);
	}
}
