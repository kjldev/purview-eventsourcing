namespace Purview.EventSourcing.SqlServer.Events;

/// <summary>
/// Unit tests for <see cref="SqlServerEventStoreClient"/> identifier safety
/// and for the per-aggregate-type table routing logic in <see cref="SqlServerEventStoreOptions"/>.
/// </summary>
public sealed class SqlServerEventStoreClientTests
{
	#region Identifier Validation Tests

	[Test]
	public async Task Constructor_GivenDefaultOptions_CreatesClientWithoutThrowing()
	{
		// Arrange & Act
		var client = new SqlServerEventStoreClient(
			new SqlServerEventStoreOptions
			{
				ConnectionString = "Server=.;Database=Test;Trusted_Connection=True;",
				SchemaName = "dbo",
				TableName = "EventStore",
				AutoCreateTable = false,
			}
		);

		// Assert
		await Assert.That(client).IsNotNull();
	}

	[Test]
	public async Task Constructor_GivenCustomSchemaAndTable_CreatesClientWithoutThrowing()
	{
		// Arrange & Act
		var client = new SqlServerEventStoreClient(
			new SqlServerEventStoreOptions
			{
				ConnectionString = "Server=.;Database=Test;Trusted_Connection=True;",
				SchemaName = "orders",
				TableName = "DomainEvents",
				AutoCreateTable = false,
			}
		);

		// Assert
		await Assert.That(client).IsNotNull();
	}

	[Test]
	public async Task Constructor_GivenIdentifierWithHyphen_CreatesClientWithoutThrowing()
	{
		// Arrange & Act — hyphens are valid in SQL identifiers when quoted
		var client = new SqlServerEventStoreClient(
			new SqlServerEventStoreOptions
			{
				ConnectionString = "Server=.;Database=Test;Trusted_Connection=True;",
				SchemaName = "my-schema",
				TableName = "my-table",
				AutoCreateTable = false,
			}
		);

		// Assert
		await Assert.That(client).IsNotNull();
	}

	[Test]
	public async Task Constructor_GivenEmptySchemaName_ThrowsArgumentException()
	{
		// Arrange & Act
		await Assert
			.That(() =>
			{
				var _ = new SqlServerEventStoreClient(
					new SqlServerEventStoreOptions
					{
						ConnectionString = "Server=.;Database=Test;Trusted_Connection=True;",
						SchemaName = "",
						TableName = "EventStore",
						AutoCreateTable = false,
					}
				);
			})
			.Throws<ArgumentException>();
	}

	[Test]
	public async Task Constructor_GivenWhitespaceTableName_ThrowsArgumentException()
	{
		// Arrange & Act
		await Assert
			.That(() =>
			{
				var _ = new SqlServerEventStoreClient(
					new SqlServerEventStoreOptions
					{
						ConnectionString = "Server=.;Database=Test;Trusted_Connection=True;",
						SchemaName = "dbo",
						TableName = "   ",
						AutoCreateTable = false,
					}
				);
			})
			.Throws<ArgumentException>();
	}

	[Test]
	public async Task Constructor_GivenIdentifierWithSemicolon_ThrowsArgumentException()
	{
		// Arrange & Act — semicolons are not valid identifier characters
		await Assert
			.That(() =>
			{
				var _ = new SqlServerEventStoreClient(
					new SqlServerEventStoreOptions
					{
						ConnectionString = "Server=.;Database=Test;Trusted_Connection=True;",
						SchemaName = "dbo; DROP TABLE EventStore --",
						TableName = "EventStore",
						AutoCreateTable = false,
					}
				);
			})
			.Throws<ArgumentException>();
	}

	[Test]
	public async Task Constructor_GivenIdentifierWithSingleQuote_ThrowsArgumentException()
	{
		// Arrange & Act
		await Assert
			.That(() =>
			{
				var _ = new SqlServerEventStoreClient(
					new SqlServerEventStoreOptions
					{
						ConnectionString = "Server=.;Database=Test;Trusted_Connection=True;",
						SchemaName = "dbo",
						TableName = "Event'Store",
						AutoCreateTable = false,
					}
				);
			})
			.Throws<ArgumentException>();
	}

	#endregion

	#region Per-Aggregate Table Override Tests

	[Test]
	public async Task SqlServerEventStoreOptions_GivenNoOverride_AggregateTableOverridesIsEmpty()
	{
		// Arrange & Act
		var options = new SqlServerEventStoreOptions
		{
			ConnectionString = "Server=.;Database=Test;Trusted_Connection=True;",
		};

		// Assert
		await Assert.That(options.AggregateTableOverrides).IsEmpty();
	}

	[Test]
	public async Task SqlServerEventStoreOptions_GivenOverride_OverrideIsStoredCaseInsensitively()
	{
		// Arrange
		var options = new SqlServerEventStoreOptions
		{
			ConnectionString = "Server=.;Database=Test;Trusted_Connection=True;",
			AggregateTableOverrides = new(StringComparer.OrdinalIgnoreCase)
			{
				["Order"] = new SqlServerAggregateTableOverride { SchemaName = "orders" },
			},
		};

		// Act & Assert — both casings should find the entry
		await Assert.That(options.AggregateTableOverrides.ContainsKey("Order")).IsTrue();
		await Assert.That(options.AggregateTableOverrides.ContainsKey("order")).IsTrue();
		await Assert.That(options.AggregateTableOverrides.ContainsKey("ORDER")).IsTrue();
	}

	[Test]
	public async Task SqlServerAggregateTableOverride_GivenOnlySchemaOverride_TableNameIsNull()
	{
		// Arrange & Act
		var ovr = new SqlServerAggregateTableOverride { SchemaName = "orders" };

		// Assert
		await Assert.That(ovr.SchemaName).IsEqualTo("orders");
		await Assert.That(ovr.TableName).IsNull();
	}

	[Test]
	public async Task SqlServerAggregateTableOverride_GivenOnlyTableOverride_SchemaNameIsNull()
	{
		// Arrange & Act
		var ovr = new SqlServerAggregateTableOverride { TableName = "OrderEvents" };

		// Assert
		await Assert.That(ovr.SchemaName).IsNull();
		await Assert.That(ovr.TableName).IsEqualTo("OrderEvents");
	}

	[Test]
	public async Task SqlServerAggregateTableOverride_GivenBothOverrides_BothAreStored()
	{
		// Arrange & Act
		var ovr = new SqlServerAggregateTableOverride { SchemaName = "orders", TableName = "Events" };

		// Assert
		await Assert.That(ovr.SchemaName).IsEqualTo("orders");
		await Assert.That(ovr.TableName).IsEqualTo("Events");
	}

	#endregion
}
