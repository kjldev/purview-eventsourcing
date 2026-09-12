using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Purview.EventSourcing.Admin.Client;

namespace Microsoft.AspNetCore.Builder;

public static class WebApplicationExtensions
{
	/// <summary>
	/// Maps the admin portal Razor Pages onto the application's route table.
	/// </summary>
	/// <param name = "app">The <see cref = "WebApplication"/> to map the pages onto.</param>
	/// <param name = "pathPrefix">The route prefix for the admin pages. Defaults to <c>/admin</c>.</param>
	/// <returns>The application for chaining.</returns>
	/// <exception cref = "ArgumentNullException"><paramref name = "app"/> is <see langword="null"/>.</exception>
	/// <exception cref = "ArgumentException"><paramref name = "pathPrefix"/> is <see langword="null"/> or whitespace.</exception>
	public static WebApplication MapPurviewEventSourcingAdminSite(this WebApplication app, string pathPrefix = "/admin")
	{
		ArgumentNullException.ThrowIfNull(app);
		ArgumentException.ThrowIfNullOrWhiteSpace(pathPrefix);
		app.MapRazorPages();
		return app;
	}
}
