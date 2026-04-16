using Azure.Storage.Blobs;
using Purview.EventSourcing;
using Purview.EventSourcing.Samples.Services;
using Purview.EventSourcing.Samples.Web.Services;
using Purview.EventSourcing.SqlServer.Exceptions;

// No authentication in this sample — allow all operations without a principal identifier
EventStoreOperationContext.RequiresValidPrincipalIdentifierDefault = false;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Use Redis when available (e.g. via Aspire AppHost); fall back to in-memory for standalone dev runs
if (!string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("redis")))
	builder.AddRedisDistributedCache("redis");
else
	builder.Services.AddDistributedMemoryCache();

// Register SQL Server event store (event stream + snapshots for querying)
builder.Services.AddSqlServerEventStore();
builder.Services.AddSqlServerSnapshotQueryableEventStore();

builder.Services.AddScoped<ISeedDataService, SeedDataService>();
builder.Services.AddScoped<IOrderFulfillmentService, OrderFulfillmentService>();
builder.Services.AddScoped<IStockTransferService, StockTransferService>();
builder.Services.AddScoped<ICartCheckoutService, CartCheckoutService>();

// Register product image service — uses Azure Blob Storage when configured, no-op otherwise
var blobConnectionString = builder.Configuration.GetConnectionString("blob-storage");
if (!string.IsNullOrWhiteSpace(blobConnectionString))
{
	builder.Services.AddSingleton(new BlobServiceClient(blobConnectionString));
	builder.Services.AddSingleton<IProductImageService, ProductImageService>();
}
else
{
	builder.Services.AddSingleton<IProductImageService, NullProductImageService>();
}

builder.Services.AddRazorPages();
builder.Services.AddSession(options =>
{
	options.IdleTimeout = TimeSpan.FromMinutes(30);
	options.Cookie.HttpOnly = true;
	options.Cookie.IsEssential = true;
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
	app.UseExceptionHandler("/Error");

app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.MapRazorPages();
app.MapDefaultEndpoints();

// Seed demo data on startup (no-op if data already exists).
using (var scope = app.Services.CreateScope())
{
	var seeder = scope.ServiceProvider.GetRequiredService<ISeedDataService>();
	for (var attempt = 0; ; attempt++)
	{
		try
		{
			await seeder.SeedAsync();
			break;
		}
		catch (ConcurrencyException) when (attempt < 2)
		{
			// Another app instance may be seeding the demo store at the same time.
			await Task.Delay(TimeSpan.FromMilliseconds(100 * (attempt + 1)));
		}
	}
}

app.Run();

// Required for WebApplicationFactory<Program> in integration tests
//public partial class Program { }
