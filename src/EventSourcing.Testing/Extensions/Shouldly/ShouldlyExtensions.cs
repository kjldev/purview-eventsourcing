using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Shouldly;
#pragma warning restore IDE0130 // Namespace does not match folder structure

[DebuggerStepThrough]
[ShouldlyMethods]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class ShouldlyExtensions
{
	[MethodImpl(MethodImplOptions.NoInlining)]
	public static void ShouldHaveCount<T>([NotNull] this IEnumerable<T> enumerable, int length, string? customMessage = null)
		=> enumerable.Count().ShouldBe(length, customMessage);

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static void ShouldHaveCount<T>([NotNull] this T[] array, int length, string? customMessage = null)
		=> array.Length.ShouldBe(length, customMessage);
}
