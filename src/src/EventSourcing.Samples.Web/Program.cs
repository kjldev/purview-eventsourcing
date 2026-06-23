using Azure.Storage.Blobs;
using Purview.EventSourcing;
using Purview.EventSourcing.Samples.Services;
using Purview.EventSourcing.Samples.Web.Services;
using Purview.EventSourcing.SqlServer.Events.Exceptions;

// No authentication in this sample — allow all operations without a principal identifier
EventStoreOperationContext.RequiresValidPrincipalIdentifierDefault = false;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Use Redis when available (e.g. via Aspire AppHost); fall back to in-memory for standalone dev runs
if (string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("redis")))
	builder.Services.AddDistributedMemoryCache();
else
	builder.AddRedisDistributedCache("redis");

// Register SQL Server event store (event stream + snapshots for querying)
builder.Services.AddSqlServerEventStore();
builder.Services.AddSqlServerSnapshotQueryableEventStore();

builder.Services.AddDomainServices();
builder.Services.AddScoped<IAggregateAuditService, AggregateAuditService>();

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
app.MapGroup("/api/audit")
	.MapGet(
		"/aggregates/{aggregateType}/{aggregateId}/events",
		async Task<IResult> (
			string aggregateType,
			string aggregateId,
			int? fromVersion,
			int? toVersion,
			DateTimeOffset? fromUtc,
			DateTimeOffset? toUtc,
			int? maxRecords,
			string? continuationToken,
			IAggregateAuditService auditService,
			CancellationToken cancellationToken
		) =>
		{
			if (!AggregateAuditService.IsSupportedAggregateType(aggregateType))
				return Results.BadRequest(
					new
					{
						Error = $"Unsupported aggregate type '{aggregateType}'.",
						SupportedAggregateTypes = AggregateAuditService.SupportedAggregateTypes,
					}
				);

			var request = new AggregateEventHistoryRequest
			{
				FromVersion = fromVersion,
				ToVersion = toVersion,
				FromUtc = fromUtc,
				ToUtc = toUtc,
				MaxRecords = maxRecords ?? ContinuationRequest.DefaultMaxRecords,
				ContinuationToken = continuationToken,
			};
			var response = await auditService.GetHistoryAsync(aggregateType, aggregateId, request, cancellationToken);

			return Results.Ok(response);
		}
	);
app.MapDefaultEndpoints().MapGet("/pingz", () => Results.Ok());

// Seed demo data on startup (no-op if data already exists).
await using (var scope = app.Services.CreateAsyncScope())
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

await app.RunAsync();

[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1515:Consider making public types internal")]
public partial class Program;
