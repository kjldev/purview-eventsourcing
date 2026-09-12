using Microsoft.AspNetCore.Mvc.RazorPages;
using Purview.EventSourcing.Admin.Client;
using Purview.EventSourcing.Admin.Site.Pages;

namespace Purview.EventSourcing.Samples.Admin.Site;

public sealed class AdminSiteEventsModelTests
{
	[Test]
	public async Task OnGetAsync_WithExistingAggregate_CallsAdminAPIClientAndReturnsPage(
		CancellationToken cancellationToken
	)
	{
		PagedResultOfEventEnvelopeResponse expected = new()
		{
			Items =
			[
				new EventEnvelopeResponse
				{
					AggregateType = "CustomerAggregate",
					AggregateId = "customer-1",
					Metadata = new EventMetadataResponse
					{
						Version = 1,
						TimestampUtc = DateTimeOffset.UtcNow,
						EventType = "CustomerRegistered",
						SchemaVersion = 1,
					},
					Payload = new JsonElement(),
				},
			],
			Page = 1,
			PageSize = 25,
			TotalCount = 1,
		};

		var capturedAggregateType = (string?)null;
		var capturedAggregateId = (string?)null;
		var capturedVersionFrom = (long?)null;
		var capturedVersionTo = (long?)null;
		var capturedPage = (int?)null;
		var capturedPageSize = (int?)null;

		FakeAdminAPIClient fakeClient = new(
			(aggregateType, aggregateId, versionFrom, versionTo, _, _, page, pageSize, _, _) =>
			{
				capturedAggregateType = aggregateType;
				capturedAggregateId = aggregateId;
				capturedVersionFrom = versionFrom;
				capturedVersionTo = versionTo;
				capturedPage = page;
				capturedPageSize = pageSize;
				return Task.FromResult(expected);
			}
		);

		EventsModel model = new(fakeClient) { AggregateType = "CustomerAggregate", AggregateId = "customer-1" };

		var result = await model.OnGetAsync(cancellationToken);

		await Assert.That(result).IsTypeOf<PageResult>();
		await Assert.That(model.EventRange).IsSameReferenceAs(expected);
		await Assert.That(capturedAggregateType).IsEqualTo("CustomerAggregate");
		await Assert.That(capturedAggregateId).IsEqualTo("customer-1");
		await Assert.That(capturedVersionFrom).IsNull();
		await Assert.That(capturedVersionTo).IsNull();
		await Assert.That(capturedPage).IsEqualTo(1);
		await Assert.That(capturedPageSize).IsEqualTo(25);
	}

	[Test]
	public async Task OnGetAsync_WithEmptyEventStream_ReturnsPageAndEmptyResult(CancellationToken cancellationToken)
	{
		PagedResultOfEventEnvelopeResponse expected = new()
		{
			Items = [],
			Page = 1,
			PageSize = 25,
			TotalCount = 0,
		};

		FakeAdminAPIClient fakeClient = new((_, _, _, _, _, _, _, _, _, _) => Task.FromResult(expected));

		EventsModel model = new(fakeClient) { AggregateType = "CustomerAggregate", AggregateId = "customer-1" };

		var result = await model.OnGetAsync(cancellationToken);

		await Assert.That(result).IsTypeOf<PageResult>();
		await Assert.That(model.EventRange).IsNotNull();
		await Assert.That(model.EventRange!.Items).IsEmpty();
	}

	static readonly HttpClient SharedHttpClient = new();

	sealed class FakeAdminAPIClient(
		Func<
			string,
			string,
			long?,
			long?,
			DateTimeOffset?,
			DateTimeOffset?,
			int?,
			int?,
			string?,
			CancellationToken,
			Task<PagedResultOfEventEnvelopeResponse>
		> handler
	) : AdminApiClient(string.Empty, SharedHttpClient)
	{
		public override Task<PagedResultOfEventEnvelopeResponse> GetAggregateEventRangeAsync(
			string aggregateType,
			string aggregateId,
			long? versionFrom = null,
			long? versionTo = null,
			DateTimeOffset? timeFromUtc = null,
			DateTimeOffset? timeToUtc = null,
			int? page = null,
			int? pageSize = null,
			string? sort = null,
			CancellationToken cancellationToken = default
		) =>
			handler(
				aggregateType,
				aggregateId,
				versionFrom,
				versionTo,
				timeFromUtc,
				timeToUtc,
				page,
				pageSize,
				sort,
				cancellationToken
			);
	}
}
