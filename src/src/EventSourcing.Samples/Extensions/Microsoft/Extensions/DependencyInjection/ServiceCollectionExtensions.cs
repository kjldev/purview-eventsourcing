using System.ComponentModel;
using Purview.EventSourcing.Samples.Services;

namespace Microsoft.Extensions.DependencyInjection;

[EditorBrowsable(EditorBrowsableState.Never)]
public static class ServiceCollectionExtensions
{
	extension(IServiceCollection services)
	{
		public IServiceCollection AddDomainServices()
		{
			services.AddScoped<ISeedDataService, SeedDataService>();
			services.AddScoped<IOrderFulfillmentService, OrderFulfillmentService>();
			services.AddScoped<IStockTransferService, StockTransferService>();
			services.AddScoped<ICartCheckoutService, CartCheckoutService>();

			return services;
		}
	}
}
