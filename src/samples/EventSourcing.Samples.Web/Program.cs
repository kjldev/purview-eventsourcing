using Purview.EventSourcing;
using Purview.EventSourcing.Samples.Services;

// No authentication in this sample — allow all operations without a principal identifier
EventStoreOperationContext.RequiresValidPrincipalIdentifierDefault = false;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddRedisDistributedCache("redis");

// Register SQL Server event store (event stream + snapshots for querying)
builder.Services.AddSqlServerEventStore();
builder.Services.AddSqlServerSnapshotQueryableEventStore();

builder.Services.AddScoped<ISeedDataService, SeedDataService>();
builder.Services.AddScoped<IOrderFulfillmentService, OrderFulfillmentService>();
builder.Services.AddScoped<IStockTransferService, StockTransferService>();

builder.Services.AddRazorPages();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
	app.UseExceptionHandler("/Error");

app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();
app.MapDefaultEndpoints();

// Seed demo data on startup (no-op if data already exists).
using (var scope = app.Services.CreateScope())
{
	var seeder = scope.ServiceProvider.GetRequiredService<ISeedDataService>();
	await seeder.SeedAsync();
}

app.Run();

// Required for WebApplicationFactory<Program> in integration tests
public partial class Program { }
