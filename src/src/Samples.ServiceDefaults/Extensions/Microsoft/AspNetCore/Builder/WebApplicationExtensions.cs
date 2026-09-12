using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace Microsoft.AspNetCore.Builder;

public static class WebApplicationExtensions
{
	public static WebApplication MapDefaultEndpoints([NotNull] this WebApplication app)
	{
		if (app.Environment.IsDevelopment())
		{
			app.MapHealthChecks("/health");
			app.MapHealthChecks("/alive", new HealthCheckOptions { Predicate = r => r.Tags.Contains("live") });
		}

		return app;
	}
}
