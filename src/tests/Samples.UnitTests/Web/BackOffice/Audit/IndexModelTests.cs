using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Purview.EventSourcing.Samples.Web.Pages.BackOffice.Audit;
using Purview.EventSourcing.Samples.Web.Services;

namespace Purview.EventSourcing.Samples.Web.BackOffice.Audit;

public sealed class IndexModelTests
{
	[Test]
	public async Task OnGetAsync_GivenDateRangeWithoutAggregateId_LoadsRecentEvents(CancellationToken cancellationToken)
	{
		// Arrange
		var auditService = IAggregateAuditService.Mock();
		AggregateEventHistoryItem recentEvent = new()
		{
			AggregateId = "order-1",
			AggregateType = "OrderAggregate",
			AggregateVersion = 2,
			EventType = "OrderConfirmedEvent",
			When = DateTimeOffset.UtcNow,
			Payload = "{}",
			EventClrType = "OrderConfirmedEvent",
			CorrelationId = "corr-1",
			IdempotencyId = "idem-1",
			UserId = "user-1",
			CausationId = "cause-1",
		};
		auditService
			.GetLatestHistoryAsync("order", Any<AggregateEventHistoryRequest>(), Any<CancellationToken>())
			.Returns([recentEvent]);

		var model = CreateModel(auditService, cancellationToken);
		model.FromUtc = DateTimeOffset.UtcNow.AddDays(-1);

		// Act
		var result = await model.OnGetAsync();

		// Assert
		await Assert.That(result).IsTypeOf<PageResult>();
		await Assert.That(model.IsRecentMode).IsTrue();
		await Assert.That(model.Events).Count().IsEqualTo(1);
		await Assert.That(model.Events[0].EventType).IsEqualTo("OrderConfirmedEvent");
		auditService
			.GetLatestHistoryAsync("order", Any<AggregateEventHistoryRequest>(), Any<CancellationToken>())
			.WasCalled(Times.Once);
		auditService
			.GetLatestHistoryAsync("customer", Any<AggregateEventHistoryRequest>(), Any<CancellationToken>())
			.WasCalled(Times.Once);
		auditService
			.GetHistoryAsync(
				Any<string>(),
				Any<string>(),
				Any<AggregateEventHistoryRequest>(),
				Any<CancellationToken>()
			)
			.WasNeverCalled();
	}

	[Test]
	public async Task OnGetAsync_GivenNoAggregateIdAndNoDateRange_LoadsRecentEvents(CancellationToken cancellationToken)
	{
		// Arrange
		var auditService = IAggregateAuditService.Mock();
		auditService
			.GetLatestHistoryAsync("order", Any<AggregateEventHistoryRequest>(), Any<CancellationToken>())
			.Returns([]);
		var model = CreateModel(auditService, cancellationToken);

		// Act
		var result = await model.OnGetAsync();

		// Assert
		await Assert.That(result).IsTypeOf<PageResult>();
		await Assert.That(model.IsRecentMode).IsTrue();
		await Assert.That(model.Events).Count().IsEqualTo(0);
		auditService
			.GetHistoryAsync(
				Any<string>(),
				Any<string>(),
				Any<AggregateEventHistoryRequest>(),
				Any<CancellationToken>()
			)
			.WasNeverCalled();
		auditService
			.GetLatestHistoryAsync("order", Any<AggregateEventHistoryRequest>(), Any<CancellationToken>())
			.WasCalled(Times.Once);
		auditService
			.GetLatestHistoryAsync("customer", Any<AggregateEventHistoryRequest>(), Any<CancellationToken>())
			.WasCalled(Times.Once);
	}

	[Test]
	public async Task OnGetAsync_GivenDateRangeInQueryStringWithoutOffset_LoadsRecentEvents(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var auditService = IAggregateAuditService.Mock();
		auditService.GetLatestHistoryAsync("order", m => m.FromUtc.HasValue, Any<CancellationToken>()).Returns([]);

		var model = CreateModel(auditService, cancellationToken, "?fromUtc=2026-06-23T00:15");

		// Act
		await model.OnGetAsync();

		// Assert
		await Assert.That(model.IsRecentMode).IsTrue();
		auditService
			.GetLatestHistoryAsync("order", m => m.FromUtc.HasValue, Any<CancellationToken>())
			.WasCalled(Times.Once);
		auditService
			.GetLatestHistoryAsync("customer", m => m.FromUtc.HasValue, Any<CancellationToken>())
			.WasCalled(Times.Once);
	}

	[Test]
	public async Task OnGetAsync_GivenAggregateId_LoadsAggregateHistory(CancellationToken cancellationToken)
	{
		// Arrange
		var auditService = IAggregateAuditService.Mock();
		auditService
			.GetHistoryAsync("order", "agg-1", Any<AggregateEventHistoryRequest>(), Any<CancellationToken>())
			.Returns(
				new ContinuationResponse<AggregateEventHistoryItem>
				{
					Results =
					[
						new AggregateEventHistoryItem
						{
							AggregateId = "agg-1",
							AggregateType = "OrderAggregate",
							AggregateVersion = 1,
							EventType = "OrderCreatedEvent",
							When = DateTimeOffset.UtcNow,
							Payload = "{}",
							EventClrType = "OrderCreatedEvent",
						},
					],
				}
			);

		var model = CreateModel(auditService, cancellationToken);
		model.AggregateId = "  agg-1  ";

		// Act
		await model.OnGetAsync();

		// Assert
		await Assert.That(model.IsRecentMode).IsFalse();
		await Assert.That(model.AggregateId).IsEqualTo("agg-1");
		await Assert.That(model.Events).Count().IsEqualTo(1);
		auditService
			.GetHistoryAsync("order", "agg-1", Any<AggregateEventHistoryRequest>(), Any<CancellationToken>())
			.WasCalled(Times.Once);
		auditService
			.GetLatestHistoryAsync(Any<string>(), Any<AggregateEventHistoryRequest>(), Any<CancellationToken>())
			.WasNeverCalled();
	}

	static IndexModel CreateModel(
		IAggregateAuditService auditService,
		CancellationToken cancellationToken,
		string? queryString = null
	)
	{
		DefaultHttpContext httpContext = new() { RequestAborted = cancellationToken };
		if (!string.IsNullOrWhiteSpace(queryString))
			httpContext.Request.QueryString = new QueryString(queryString);

		IndexModel model = new(auditService) { PageContext = new PageContext { HttpContext = httpContext } };

		return model;
	}
}
