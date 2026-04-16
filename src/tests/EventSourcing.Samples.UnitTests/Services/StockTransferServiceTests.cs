using System.Linq.Expressions;
using FluentValidation.Results;
using Purview.EventSourcing.Aggregates;
using Purview.EventSourcing.Samples.Domain;

namespace Purview.EventSourcing.Samples.Services;

public sealed class StockTransferServiceTests
{
	static InventoryAggregate StockedItem(
		string id,
		string locationId,
		string locationName,
		int qty,
		string productId = "SKU-001"
	)
	{
		var item = new InventoryAggregate();
		item.Details.Id = id;
		item.Initialize(productId, "Widget", locationId, locationName, initialQuantity: qty);
		return item;
	}

	static LocationAggregate Location(string id, string name)
	{
		var location = new LocationAggregate();
		location.Details.Id = id;
		location.Initialize(id, name);
		return location;
	}

	static SaveResult<T> SuccessResult<T>(T aggregate)
		where T : class, IAggregate, new() => new(aggregate, new ValidationResult(), saved: true, skipped: false);

	static SaveResult<T> FailResult<T>(T aggregate)
		where T : class, IAggregate, new() =>
		new(
			aggregate,
			new ValidationResult([new ValidationFailure("field", "Save failed")]),
			saved: false,
			skipped: false
		);

	StockTransferService CreateService(IQueryableEventStore? store = null) =>
		new(store ?? Substitute.For<IQueryableEventStore>());

	[Test]
	public async Task TransferAsync_GivenSameSourceAndDestinationLocation_ReturnsFail(
		CancellationToken cancellationToken
	)
	{
		var source = StockedItem("inv-1", "LOC-001", "Warehouse North", 100);
		var store = Substitute.For<IQueryableEventStore>();
		store.GetAsync<InventoryAggregate>("inv-1", null, cancellationToken).Returns(source);

		var result = await CreateService(store).TransferAsync("inv-1", "LOC-001", 5, "test", cancellationToken);

		await Assert.That(result.Succeeded).IsFalse();
		await Assert.That(result.ErrorMessage).Contains("different");
	}

	[Test]
	public async Task TransferAsync_GivenNullSource_ReturnsFail(CancellationToken cancellationToken)
	{
		var store = Substitute.For<IQueryableEventStore>();
		store.GetAsync<InventoryAggregate>("missing", null, cancellationToken).Returns((InventoryAggregate?)null);

		var result = await CreateService(store).TransferAsync("missing", "inv-2", 5, "test", cancellationToken);

		await Assert.That(result.Succeeded).IsFalse();
		await Assert.That(result.ErrorMessage).Contains("Source");
	}

	[Test]
	public async Task TransferAsync_GivenNullDestination_ReturnsFail(CancellationToken cancellationToken)
	{
		var source = StockedItem("inv-1", "LOC-001", "Warehouse North", 100);
		var sourceLocation = Location("LOC-001", "Warehouse North");
		var store = Substitute.For<IQueryableEventStore>();
		store.GetAsync<InventoryAggregate>("inv-1", null, cancellationToken).Returns(source);
		store.GetAsync<LocationAggregate>("LOC-001", null, cancellationToken).Returns(sourceLocation);
		store.GetAsync<LocationAggregate>("missing", null, cancellationToken).Returns((LocationAggregate?)null);

		var result = await CreateService(store).TransferAsync("inv-1", "missing", 5, "test", cancellationToken);

		await Assert.That(result.Succeeded).IsFalse();
		await Assert.That(result.ErrorMessage).Contains("Destination");
	}

	[Test]
	public async Task TransferAsync_GivenMissingSourceLocationAggregate_ReturnsFail(CancellationToken cancellationToken)
	{
		var source = StockedItem("inv-1", "LOC-001", "Warehouse North", 100, productId: "SKU-001");
		var destinationLocation = Location("LOC-002", "Warehouse South");
		var store = Substitute.For<IQueryableEventStore>();
		store.GetAsync<InventoryAggregate>("inv-1", null, cancellationToken).Returns(source);
		store.GetAsync<LocationAggregate>("LOC-001", null, cancellationToken).Returns((LocationAggregate?)null);
		store.GetAsync<LocationAggregate>("LOC-002", null, cancellationToken).Returns(destinationLocation);

		var result = await CreateService(store).TransferAsync("inv-1", "LOC-002", 5, "test", cancellationToken);

		await Assert.That(result.Succeeded).IsFalse();
		await Assert.That(result.ErrorMessage).Contains("Source location");
	}

	[Test]
	public async Task TransferAsync_GivenInsufficientStock_ReturnsFail(CancellationToken cancellationToken)
	{
		var source = StockedItem("inv-1", "LOC-001", "Warehouse North", 3);
		var sourceLocation = Location("LOC-001", "Warehouse North");
		var destinationLocation = Location("LOC-002", "Warehouse South");
		var dest = StockedItem("inv-2", "LOC-002", "Warehouse South", 0);

		var store = Substitute.For<IQueryableEventStore>();
		store.GetAsync<InventoryAggregate>("inv-1", null, cancellationToken).Returns(source);
		store.GetAsync<LocationAggregate>("LOC-001", null, cancellationToken).Returns(sourceLocation);
		store.GetAsync<LocationAggregate>("LOC-002", null, cancellationToken).Returns(destinationLocation);
		store
			.FirstOrDefaultAsync<InventoryAggregate>(
				Arg.Any<Expression<Func<InventoryAggregate, bool>>>(),
				null,
				cancellationToken
			)
			.Returns(dest);

		var result = await CreateService(store).TransferAsync("inv-1", "LOC-002", 10, "test", cancellationToken);

		await Assert.That(result.Succeeded).IsFalse();
		await Assert.That(result.ErrorMessage).Contains("Insufficient");
	}

	[Test]
	public async Task TransferAsync_GivenValidData_ReturnsSuccess(CancellationToken cancellationToken)
	{
		var source = StockedItem("inv-1", "LOC-001", "Warehouse North", 100);
		var sourceLocation = Location("LOC-001", "Warehouse North");
		var destinationLocation = Location("LOC-002", "Warehouse South");
		var dest = StockedItem("inv-2", "LOC-002", "Warehouse South", 20);

		var store = Substitute.For<IQueryableEventStore>();
		store.GetAsync<InventoryAggregate>("inv-1", null, cancellationToken).Returns(source);
		store.GetAsync<LocationAggregate>("LOC-001", null, cancellationToken).Returns(sourceLocation);
		store.GetAsync<LocationAggregate>("LOC-002", null, cancellationToken).Returns(destinationLocation);
		store
			.FirstOrDefaultAsync<InventoryAggregate>(
				Arg.Any<Expression<Func<InventoryAggregate, bool>>>(),
				null,
				cancellationToken
			)
			.Returns(dest);
		store
			.SaveAsync<InventoryAggregate>(Arg.Any<InventoryAggregate>(), null, cancellationToken)
			.Returns(callInfo => SuccessResult((InventoryAggregate)callInfo[0]));

		var result = await CreateService(store).TransferAsync("inv-1", "LOC-002", 15, "rebalance", cancellationToken);

		await Assert.That(result.Succeeded).IsTrue();
		await Assert.That(result.Quantity).IsEqualTo(15);
	}

	[Test]
	public async Task TransferAsync_GivenValidData_AdjustsSourceAndDestinationCorrectly(
		CancellationToken cancellationToken
	)
	{
		var source = StockedItem("inv-1", "LOC-001", "Warehouse North", 100);
		var sourceLocation = Location("LOC-001", "Warehouse North");
		var destinationLocation = Location("LOC-002", "Warehouse South");
		var dest = StockedItem("inv-2", "LOC-002", "Warehouse South", 20);

		var store = Substitute.For<IQueryableEventStore>();
		store.GetAsync<InventoryAggregate>("inv-1", null, cancellationToken).Returns(source);
		store.GetAsync<LocationAggregate>("LOC-001", null, cancellationToken).Returns(sourceLocation);
		store.GetAsync<LocationAggregate>("LOC-002", null, cancellationToken).Returns(destinationLocation);
		store
			.FirstOrDefaultAsync<InventoryAggregate>(
				Arg.Any<Expression<Func<InventoryAggregate, bool>>>(),
				null,
				cancellationToken
			)
			.Returns(dest);
		store
			.SaveAsync<InventoryAggregate>(Arg.Any<InventoryAggregate>(), null, cancellationToken)
			.Returns(callInfo => SuccessResult((InventoryAggregate)callInfo[0]));

		await CreateService(store).TransferAsync("inv-1", "LOC-002", 15, "rebalance", cancellationToken);

		await Assert.That(source.QuantityOnHand).IsEqualTo(85);
		await Assert.That(dest.QuantityOnHand).IsEqualTo(35);
	}

	[Test]
	public async Task TransferAsync_WhenDestinationInventoryMissing_CreatesNewInventoryAtDestination(
		CancellationToken cancellationToken
	)
	{
		var source = StockedItem("inv-1", "LOC-001", "Warehouse North", 100);
		var sourceLocation = Location("LOC-001", "Warehouse North");
		var destinationLocation = Location("LOC-002", "Warehouse South");
		var createdDestination = new InventoryAggregate();
		createdDestination.Details.Id = "inv-2";

		var store = Substitute.For<IQueryableEventStore>();
		store.GetAsync<InventoryAggregate>("inv-1", null, cancellationToken).Returns(source);
		store.GetAsync<LocationAggregate>("LOC-001", null, cancellationToken).Returns(sourceLocation);
		store.GetAsync<LocationAggregate>("LOC-002", null, cancellationToken).Returns(destinationLocation);
		store
			.FirstOrDefaultAsync<InventoryAggregate>(
				Arg.Any<Expression<Func<InventoryAggregate, bool>>>(),
				null,
				cancellationToken
			)
			.Returns((InventoryAggregate?)null);
		store.CreateAsync<InventoryAggregate>(null, cancellationToken).Returns(createdDestination);
		store
			.SaveAsync<InventoryAggregate>(Arg.Any<InventoryAggregate>(), null, cancellationToken)
			.Returns(callInfo => SuccessResult((InventoryAggregate)callInfo[0]));

		var result = await CreateService(store).TransferAsync("inv-1", "LOC-002", 10, "test", cancellationToken);

		await Assert.That(result.Succeeded).IsTrue();
		await Assert.That(createdDestination.LocationId).IsEqualTo("LOC-002");
		await Assert.That(createdDestination.LocationName).IsEqualTo("Warehouse South");
		await Assert.That(createdDestination.ProductId).IsEqualTo(source.ProductId);
		await Assert.That(createdDestination.QuantityOnHand).IsEqualTo(10);
		await store.Received(1).CreateAsync<InventoryAggregate>(null, cancellationToken);
	}

	[Test]
	public async Task TransferAsync_WhenSourceSaveFails_ReturnsFail(CancellationToken cancellationToken)
	{
		var source = StockedItem("inv-1", "LOC-001", "Warehouse North", 100);
		var sourceLocation = Location("LOC-001", "Warehouse North");
		var destinationLocation = Location("LOC-002", "Warehouse South");
		var dest = StockedItem("inv-2", "LOC-002", "Warehouse South", 0);

		var store = Substitute.For<IQueryableEventStore>();
		store.GetAsync<InventoryAggregate>("inv-1", null, cancellationToken).Returns(source);
		store.GetAsync<LocationAggregate>("LOC-001", null, cancellationToken).Returns(sourceLocation);
		store.GetAsync<LocationAggregate>("LOC-002", null, cancellationToken).Returns(destinationLocation);
		store
			.FirstOrDefaultAsync<InventoryAggregate>(
				Arg.Any<Expression<Func<InventoryAggregate, bool>>>(),
				null,
				cancellationToken
			)
			.Returns(dest);
		store
			.SaveAsync<InventoryAggregate>(Arg.Any<InventoryAggregate>(), null, cancellationToken)
			.Returns(FailResult(source));

		var result = await CreateService(store).TransferAsync("inv-1", "LOC-002", 10, "test", cancellationToken);

		await Assert.That(result.Succeeded).IsFalse();
		// Destination should never be saved — compensation not needed
		await store.Received(1).SaveAsync<InventoryAggregate>(Arg.Any<InventoryAggregate>(), null, cancellationToken);
	}

	[Test]
	public async Task TransferAsync_WhenDestinationSaveFails_CompensatesSourceStock(CancellationToken cancellationToken)
	{
		var source = StockedItem("inv-1", "LOC-001", "Warehouse North", 100);
		var sourceLocation = Location("LOC-001", "Warehouse North");
		var destinationLocation = Location("LOC-002", "Warehouse South");
		var dest = StockedItem("inv-2", "LOC-002", "Warehouse South", 0);
		var callCount = 0;

		var store = Substitute.For<IQueryableEventStore>();
		store.GetAsync<InventoryAggregate>("inv-1", null, cancellationToken).Returns(source);
		store.GetAsync<LocationAggregate>("LOC-001", null, cancellationToken).Returns(sourceLocation);
		store.GetAsync<LocationAggregate>("LOC-002", null, cancellationToken).Returns(destinationLocation);
		store
			.FirstOrDefaultAsync<InventoryAggregate>(
				Arg.Any<Expression<Func<InventoryAggregate, bool>>>(),
				null,
				cancellationToken
			)
			.Returns(dest);
		store
			.SaveAsync<InventoryAggregate>(Arg.Any<InventoryAggregate>(), null, cancellationToken)
			.Returns(callInfo =>
			{
				callCount++;
				var agg = (InventoryAggregate)callInfo[0];
				// First call (source save) succeeds; second (dest) fails; third (compensation) succeeds.
				return callCount == 2 ? FailResult(agg) : SuccessResult(agg);
			});

		var result = await CreateService(store).TransferAsync("inv-1", "LOC-002", 10, "test", cancellationToken);

		await Assert.That(result.Succeeded).IsFalse();
		await Assert.That(result.ErrorMessage).Contains("restored");
		// 3 saves: source, destination (fail), compensation
		await store.Received(3).SaveAsync<InventoryAggregate>(Arg.Any<InventoryAggregate>(), null, cancellationToken);
		// Source should be back to its original quantity
		await Assert.That(source.QuantityOnHand).IsEqualTo(100);
	}
}
