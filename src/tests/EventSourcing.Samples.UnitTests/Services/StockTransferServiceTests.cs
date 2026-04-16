using System.Linq.Expressions;
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

	static TransactionResult SuccessfulTransaction(params IAggregate[] aggregates) =>
		new(
		[
			.. aggregates.Select(aggregate => new TransactionAggregateResult(aggregate, saved: true, skipped: false, error: null)),
		]
	);

	static TransactionResult FailedTransaction(IAggregate aggregate) =>
		new([new TransactionAggregateResult(aggregate, saved: false, skipped: false, error: new InvalidOperationException("Commit failed"))]);

	StockTransferService CreateService(
		IEventStoreTransactionFactory? transactionFactory = null,
		IQueryableEventStore? store = null
	) => new(transactionFactory ?? Substitute.For<IEventStoreTransactionFactory>(), store ?? Substitute.For<IQueryableEventStore>());

	[Test]
	public async Task TransferAsync_GivenSameSourceAndDestinationLocation_ReturnsFail(
		CancellationToken cancellationToken
	)
	{
		var source = StockedItem("inv-1", "LOC-001", "Warehouse North", 100);
		var store = Substitute.For<IQueryableEventStore>();
		store.GetAsync<InventoryAggregate>("inv-1", null, cancellationToken).Returns(source);

		var result = await CreateService(store: store)
			.TransferAsync("inv-1", "LOC-001", 5, "test", cancellationToken);

		await Assert.That(result.Succeeded).IsFalse();
		await Assert.That(result.ErrorMessage).Contains("different");
	}

	[Test]
	public async Task TransferAsync_GivenNullSource_ReturnsFail(CancellationToken cancellationToken)
	{
		var store = Substitute.For<IQueryableEventStore>();
		store.GetAsync<InventoryAggregate>("missing", null, cancellationToken).Returns((InventoryAggregate?)null);

		var result = await CreateService(store: store)
			.TransferAsync("missing", "inv-2", 5, "test", cancellationToken);

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

		var result = await CreateService(store: store)
			.TransferAsync("inv-1", "missing", 5, "test", cancellationToken);

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

		var result = await CreateService(store: store)
			.TransferAsync("inv-1", "LOC-002", 5, "test", cancellationToken);

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
		store.FirstOrDefaultAsync<InventoryAggregate>(Arg.Any<Expression<Func<InventoryAggregate, bool>>>(), null, cancellationToken)
			.Returns(dest);

		var result = await CreateService(store: store)
			.TransferAsync("inv-1", "LOC-002", 10, "test", cancellationToken);

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

		var transactionFactory = Substitute.For<IEventStoreTransactionFactory>();
		var transaction = Substitute.For<IEventStoreTransaction>();
		transactionFactory.Create(Arg.Any<string?>()).Returns(transaction);
		transaction.CommitAsync(cancellationToken).Returns(SuccessfulTransaction(source, dest));

		var store = Substitute.For<IQueryableEventStore>();
		store.GetAsync<InventoryAggregate>("inv-1", null, cancellationToken).Returns(source);
		store.GetAsync<LocationAggregate>("LOC-001", null, cancellationToken).Returns(sourceLocation);
		store.GetAsync<LocationAggregate>("LOC-002", null, cancellationToken).Returns(destinationLocation);
		store.FirstOrDefaultAsync<InventoryAggregate>(Arg.Any<Expression<Func<InventoryAggregate, bool>>>(), null, cancellationToken)
			.Returns(dest);

		var result = await CreateService(transactionFactory, store)
			.TransferAsync("inv-1", "LOC-002", 15, "rebalance", cancellationToken);

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

		var transactionFactory = Substitute.For<IEventStoreTransactionFactory>();
		var transaction = Substitute.For<IEventStoreTransaction>();
		transactionFactory.Create(Arg.Any<string?>()).Returns(transaction);
		transaction.CommitAsync(cancellationToken).Returns(SuccessfulTransaction(source, dest));

		var store = Substitute.For<IQueryableEventStore>();
		store.GetAsync<InventoryAggregate>("inv-1", null, cancellationToken).Returns(source);
		store.GetAsync<LocationAggregate>("LOC-001", null, cancellationToken).Returns(sourceLocation);
		store.GetAsync<LocationAggregate>("LOC-002", null, cancellationToken).Returns(destinationLocation);
		store.FirstOrDefaultAsync<InventoryAggregate>(Arg.Any<Expression<Func<InventoryAggregate, bool>>>(), null, cancellationToken)
			.Returns(dest);

		await CreateService(transactionFactory, store)
			.TransferAsync("inv-1", "LOC-002", 15, "rebalance", cancellationToken);

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

		var transactionFactory = Substitute.For<IEventStoreTransactionFactory>();
		var transaction = Substitute.For<IEventStoreTransaction>();
		transactionFactory.Create(Arg.Any<string?>()).Returns(transaction);
		transaction.CommitAsync(cancellationToken).Returns(SuccessfulTransaction(source, createdDestination));

		var store = Substitute.For<IQueryableEventStore>();
		store.GetAsync<InventoryAggregate>("inv-1", null, cancellationToken).Returns(source);
		store.GetAsync<LocationAggregate>("LOC-001", null, cancellationToken).Returns(sourceLocation);
		store.GetAsync<LocationAggregate>("LOC-002", null, cancellationToken).Returns(destinationLocation);
		store.FirstOrDefaultAsync<InventoryAggregate>(Arg.Any<Expression<Func<InventoryAggregate, bool>>>(), null, cancellationToken)
			.Returns((InventoryAggregate?)null);
		store.CreateAsync<InventoryAggregate>(null, cancellationToken).Returns(createdDestination);

		var result = await CreateService(transactionFactory, store)
			.TransferAsync("inv-1", "LOC-002", 10, "test", cancellationToken);

		await Assert.That(result.Succeeded).IsTrue();
		await Assert.That(createdDestination.LocationId).IsEqualTo("LOC-002");
		await Assert.That(createdDestination.LocationName).IsEqualTo("Warehouse South");
		await Assert.That(createdDestination.ProductId).IsEqualTo(source.ProductId);
		await Assert.That(createdDestination.QuantityOnHand).IsEqualTo(10);
		await store.Received(1).CreateAsync<InventoryAggregate>(null, cancellationToken);
	}

	[Test]
	public async Task TransferAsync_WhenTransactionCommitFails_ReturnsFail(CancellationToken cancellationToken)
	{
		var source = StockedItem("inv-1", "LOC-001", "Warehouse North", 100);
		var sourceLocation = Location("LOC-001", "Warehouse North");
		var destinationLocation = Location("LOC-002", "Warehouse South");
		var dest = StockedItem("inv-2", "LOC-002", "Warehouse South", 0);

		var transactionFactory = Substitute.For<IEventStoreTransactionFactory>();
		var transaction = Substitute.For<IEventStoreTransaction>();
		transactionFactory.Create(Arg.Any<string?>()).Returns(transaction);
		transaction.CommitAsync(cancellationToken).Returns(FailedTransaction(source));

		var store = Substitute.For<IQueryableEventStore>();
		store.GetAsync<InventoryAggregate>("inv-1", null, cancellationToken).Returns(source);
		store.GetAsync<LocationAggregate>("LOC-001", null, cancellationToken).Returns(sourceLocation);
		store.GetAsync<LocationAggregate>("LOC-002", null, cancellationToken).Returns(destinationLocation);
		store.FirstOrDefaultAsync<InventoryAggregate>(Arg.Any<Expression<Func<InventoryAggregate, bool>>>(), null, cancellationToken)
			.Returns(dest);

		var result = await CreateService(transactionFactory, store)
			.TransferAsync("inv-1", "LOC-002", 10, "test", cancellationToken);

		await Assert.That(result.Succeeded).IsFalse();
		await Assert.That(result.ErrorMessage).Contains("Nothing was saved");
	}

	[Test]
	public async Task TransferAsync_WhenTransactionCommitFails_DoesNotApplyCompensation(CancellationToken cancellationToken)
	{
		var source = StockedItem("inv-1", "LOC-001", "Warehouse North", 100);
		var sourceLocation = Location("LOC-001", "Warehouse North");
		var destinationLocation = Location("LOC-002", "Warehouse South");
		var dest = StockedItem("inv-2", "LOC-002", "Warehouse South", 0);

		var transactionFactory = Substitute.For<IEventStoreTransactionFactory>();
		var transaction = Substitute.For<IEventStoreTransaction>();
		transactionFactory.Create(Arg.Any<string?>()).Returns(transaction);
		transaction.CommitAsync(cancellationToken).Returns(FailedTransaction(dest));

		var store = Substitute.For<IQueryableEventStore>();
		store.GetAsync<InventoryAggregate>("inv-1", null, cancellationToken).Returns(source);
		store.GetAsync<LocationAggregate>("LOC-001", null, cancellationToken).Returns(sourceLocation);
		store.GetAsync<LocationAggregate>("LOC-002", null, cancellationToken).Returns(destinationLocation);
		store.FirstOrDefaultAsync<InventoryAggregate>(Arg.Any<Expression<Func<InventoryAggregate, bool>>>(), null, cancellationToken)
			.Returns(dest);

		var result = await CreateService(transactionFactory, store)
			.TransferAsync("inv-1", "LOC-002", 10, "test", cancellationToken);

		await Assert.That(result.Succeeded).IsFalse();
		await Assert.That(result.ErrorMessage).Contains("Nothing was saved");
		await Assert.That(source.QuantityOnHand).IsEqualTo(90);
		await Assert.That(dest.QuantityOnHand).IsEqualTo(10);
	}
}
