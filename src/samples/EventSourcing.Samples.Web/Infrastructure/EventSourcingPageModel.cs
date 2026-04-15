using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Purview.EventSourcing.SqlServer.Exceptions;

namespace Purview.EventSourcing.Samples.Web.Infrastructure;

/// <summary>
/// Base page model that provides a helper for wrapping event-store saves with
/// typed concurrency-conflict handling. A <see cref="ConcurrencyException"/>
/// is surfaced as a user-friendly TempData error rather than an unhandled 500.
/// </summary>
public abstract class EventSourcingPageModel : PageModel
{
	const string ConflictMessage =
		"This record was modified by another user while you were editing it. "
		+ "The page has been refreshed — please review and resubmit your change.";

	/// <summary>
	/// Executes <paramref name="saveAction"/>, sets a success or conflict message,
	/// and returns <paramref name="result"/> in both cases.
	/// </summary>
	protected async Task<IActionResult> TrySaveAsync(Func<Task> saveAction, string successMessage, IActionResult result)
	{
		try
		{
			await saveAction();
			TempData["Success"] = successMessage;
		}
		catch (ConcurrencyException)
		{
			TempData["Error"] = ConflictMessage;
		}

		return result;
	}
}
