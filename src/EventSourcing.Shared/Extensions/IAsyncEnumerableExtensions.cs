using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace System.Collections.Generic;

[EditorBrowsable(EditorBrowsableState.Never)]
[DebuggerStepThrough]
static class IAsyncEnumerableExtensions
{
	public static async IAsyncEnumerable<TResult> SelectAsync<TSource, TResult>([NotNull] this IAsyncEnumerable<TSource> source, [NotNull] Func<TSource, Task<TResult>> selector)
	{
		await foreach (var item in source)
		{
			var result = await selector(item);
			yield return result;
		}
	}

	public static async IAsyncEnumerable<TResult> SelectAsync<TSource, TResult>([NotNull] this IAsyncEnumerable<TSource> source, [NotNull] Func<TSource, TResult> selector)
	{
		await foreach (var item in source)
		{
			var result = selector(item);
			yield return result;
		}
	}

	public static async IAsyncEnumerable<TSource> SelectAsync<TSource>([NotNull] this IAsyncEnumerable<TSource> source, [NotNull] Action<TSource> action)
	{
		await foreach (var item in source)
		{
			action(item);
			yield return item;
		}
	}
}
