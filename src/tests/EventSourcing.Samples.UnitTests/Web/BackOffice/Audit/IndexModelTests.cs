using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
		var auditService = Substitute.For<IAggregateAuditService>();
		var recentEvent = new AggregateEventHistoryItem
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
			.GetLatestHistoryAsync("order", Arg.Any<AggregateEventHistoryRequest>(), Arg.Any<CancellationToken>())
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
		await auditService
			.Received(1)
			.GetLatestHistoryAsync("order", Arg.Any<AggregateEventHistoryRequest>(), Arg.Any<CancellationToken>());
		await auditService
			.Received(1)
			.GetLatestHistoryAsync("customer", Arg.Any<AggregateEventHistoryRequest>(), Arg.Any<CancellationToken>());
		await auditService.DidNotReceiveWithAnyArgs().GetHistoryAsync(default!, default!, default!, default);
	}

	[Test]
	public async Task OnGetAsync_GivenNoAggregateIdAndNoDateRange_LoadsRecentEvents(CancellationToken cancellationToken)
	{
		// Arrange
		var auditService = Substitute.For<IAggregateAuditService>();
		auditService
			.GetLatestHistoryAsync("order", Arg.Any<AggregateEventHistoryRequest>(), Arg.Any<CancellationToken>())
			.Returns([]);
		var model = CreateModel(auditService, cancellationToken);

		// Act
		var result = await model.OnGetAsync();

		// Assert
		await Assert.That(result).IsTypeOf<PageResult>();
		await Assert.That(model.IsRecentMode).IsTrue();
		await Assert.That(model.Events).Count().IsEqualTo(0);
		await auditService.DidNotReceiveWithAnyArgs().GetHistoryAsync(default!, default!, default!, default);
		await auditService
			.Received(1)
			.GetLatestHistoryAsync("order", Arg.Any<AggregateEventHistoryRequest>(), Arg.Any<CancellationToken>());
		await auditService
			.Received(1)
			.GetLatestHistoryAsync("customer", Arg.Any<AggregateEventHistoryRequest>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task OnGetAsync_GivenDateRangeInQueryStringWithoutOffset_LoadsRecentEvents(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var auditService = Substitute.For<IAggregateAuditService>();
		auditService
			.GetLatestHistoryAsync(
				"order",
				Arg.Is<AggregateEventHistoryRequest>(m => m.FromUtc.HasValue),
				Arg.Any<CancellationToken>()
			)
			.Returns([]);

		var model = CreateModel(auditService, cancellationToken, "?fromUtc=2026-06-23T00:15");

		// Act
		await model.OnGetAsync();

		// Assert
		await Assert.That(model.IsRecentMode).IsTrue();
		await auditService
			.Received(1)
			.GetLatestHistoryAsync(
				"order",
				Arg.Is<AggregateEventHistoryRequest>(m => m.FromUtc.HasValue),
				Arg.Any<CancellationToken>()
			);
		await auditService
			.Received(1)
			.GetLatestHistoryAsync(
				"customer",
				Arg.Is<AggregateEventHistoryRequest>(m => m.FromUtc.HasValue),
				Arg.Any<CancellationToken>()
			);
	}

	[Test]
	public async Task OnGetAsync_GivenAggregateId_LoadsAggregateHistory(CancellationToken cancellationToken)
	{
		// Arrange
		var auditService = Substitute.For<IAggregateAuditService>();
		auditService
			.GetHistoryAsync("order", "agg-1", Arg.Any<AggregateEventHistoryRequest>(), Arg.Any<CancellationToken>())
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
		await auditService
			.Received(1)
			.GetHistoryAsync("order", "agg-1", Arg.Any<AggregateEventHistoryRequest>(), Arg.Any<CancellationToken>());
		await auditService.DidNotReceiveWithAnyArgs().GetLatestHistoryAsync(default!, default!, default);
	}

	static IndexModel CreateModel(
		IAggregateAuditService auditService,
		CancellationToken cancellationToken,
		string? queryString = null
	)
	{
		var httpContext = new DefaultHttpContext { RequestAborted = cancellationToken };
		if (!string.IsNullOrWhiteSpace(queryString))
			httpContext.Request.QueryString = new QueryString(queryString);

		var model = new IndexModel(auditService);
		model.PageContext = new PageContext { HttpContext = httpContext };

		return model;
	}
}
