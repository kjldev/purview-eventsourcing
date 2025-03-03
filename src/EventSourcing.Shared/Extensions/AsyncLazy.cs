using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace System;

/// <summary>
/// Async version of <see cref="Lazy{T}"/>.
/// </summary>
/// <typeparam name="T">The type of the value to lazily initialize.</typeparam>
[DebuggerStepThrough]
sealed class AsyncLazy<T> : Lazy<Task<T>>
{
	// Async...
	/// <summary>
	/// Creates a new instance of <see cref="AsyncLazy{T}"/> with a factory to get it's inner value.
	/// </summary>
	/// <param name="taskFactory">The factory used to retrieve it's value.</param>
	[SuppressMessage("Reliability", "CA2008:Do not create tasks without passing a TaskScheduler", Justification = "")]
	public AsyncLazy(Func<Task<T>> taskFactory)
		: base(() => Task.Factory.StartNew(() => taskFactory()).Unwrap()) { }

	/// <summary>
	/// Creates a new instance of <see cref="AsyncLazy{T}"/> with a factory to get it's inner value.
	/// </summary>
	/// <param name="taskFactory">The factory used to retrieve it's value.</param>
	[SuppressMessage("Reliability", "CA2008:Do not create tasks without passing a TaskScheduler", Justification = "")]
	public AsyncLazy(Func<CancellationToken, Task<T>> taskFactory)
		: base(() => Task.Factory.StartNew(() => taskFactory(default)).Unwrap()) { }

	/// <summary>
	/// Creates a new instance of <see cref="AsyncLazy{T}"/> with a factory to get it's inner value.
	/// </summary>
	/// <param name="valueFactory">The factory used to retrieve it's value.</param>
	// Non-async - this is used for when you need to convert from a Task based approach, to a non-task based approach.
	public AsyncLazy(Func<T> valueFactory)
		: base(() => Task.FromResult(valueFactory())) { }

	/// <summary>
	/// Creates a new instance of <see cref="AsyncLazy{T}"/> with a factory to get it's inner value.
	/// </summary>
	/// <param name="value">The value to return.</param>
	public AsyncLazy(T value)
		: base(() => Task.FromResult(value)) { }

	/// <inheritdoc/>
	public TaskAwaiter<T> GetAwaiter()
		=> Value.GetAwaiter();
}
