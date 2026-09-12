using Microsoft.Extensions.Options;
using Purview.EventSourcing.Samples.Options;

namespace Microsoft.AspNetCore.Builder;

static class WebApplicationExtensions
{
	extension(WebApplication app)
	{
		public WebApplication UseAdminAPI()
		{
			var sampleStoreOptions = app.Services.GetRequiredService<IOptions<SampleStoreOptions>>();
			if (sampleStoreOptions.Value.AdminAPIAvailable)
			{
				app.MapOpenApi();
				app.MapPurviewEventSourcingAdminAPI();
				app.MapPurviewEventSourcingAdminSite();
			}

			return app;
		}
	}
}
