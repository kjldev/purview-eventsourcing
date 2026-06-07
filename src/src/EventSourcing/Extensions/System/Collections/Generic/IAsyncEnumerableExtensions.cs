using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace System.Collections.Generic;

[EditorBrowsable(EditorBrowsableState.Never)]
[DebuggerStepThrough]
static class IAsyncEnumerableExtensions
{
    extension<TSource>([NotNull] IAsyncEnumerable<TSource> source)
    {
        public async IAsyncEnumerable<TResult> SelectAsync<TResult>(
            [NotNull] Func<TSource, Task<TResult>> selector
        )
        {
            await foreach (var item in source)
            {
                var result = await selector(item);
                yield return result;
            }
        }

        public async IAsyncEnumerable<TResult> SelectAsync<TResult>(
            [NotNull] Func<TSource, TResult> selector
        )
        {
            await foreach (var item in source)
            {
                var result = selector(item);
                yield return result;
            }
        }

        public async IAsyncEnumerable<TSource> SelectAsync([NotNull] Action<TSource> action)
        {
            await foreach (var item in source)
            {
                action(item);
                yield return item;
            }
        }
    }
}
