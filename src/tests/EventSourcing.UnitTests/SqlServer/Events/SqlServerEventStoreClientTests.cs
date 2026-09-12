namespace Purview.EventSourcing.SqlServer.Events;

/// <summary>
/// Unit tests for <see cref="SqlServerEventStoreClient"/> identifier safety
/// and for the per-aggregate-type table routing logic in <see cref="SqlServerEventStoreOptions"/>.
/// </summary>
public sealed class SqlServerEventStoreClientTests
{
	[Test]
	public async Task Constructor_GivenDefaultOptions_CreatesClientWithoutThrowing()
	{
		// Arrange & Act
		SqlServerEventStoreClient client = new(
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
		SqlServerEventStoreClient client = new(
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
		SqlServerEventStoreClient client = new(
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
				SqlServerEventStoreClient _ = new(
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
				SqlServerEventStoreClient _ = new(
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
				SqlServerEventStoreClient _ = new(
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
				SqlServerEventStoreClient _ = new(
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

	[Test]
	public async Task SqlServerEventStoreOptions_GivenNoOverride_AggregateTableOverridesIsEmpty()
	{
		// Arrange & Act
		SqlServerEventStoreOptions options = new()
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
		SqlServerEventStoreOptions options = new()
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
		SqlServerAggregateTableOverride ovr = new() { SchemaName = "orders" };

		// Assert
		await Assert.That(ovr.SchemaName).IsEqualTo("orders");
		await Assert.That(ovr.TableName).IsNull();
	}

	[Test]
	public async Task SqlServerAggregateTableOverride_GivenOnlyTableOverride_SchemaNameIsNull()
	{
		// Arrange & Act
		SqlServerAggregateTableOverride ovr = new() { TableName = "OrderEvents" };

		// Assert
		await Assert.That(ovr.SchemaName).IsNull();
		await Assert.That(ovr.TableName).IsEqualTo("OrderEvents");
	}

	[Test]
	public async Task SqlServerAggregateTableOverride_GivenBothOverrides_BothAreStored()
	{
		// Arrange & Act
		SqlServerAggregateTableOverride ovr = new() { SchemaName = "orders", TableName = "Events" };

		// Assert
		await Assert.That(ovr.SchemaName).IsEqualTo("orders");
		await Assert.That(ovr.TableName).IsEqualTo("Events");
	}

	[Test]
	public async Task SqlServerEventStoreOptions_GivenDefaults_JsonIndexOptionsIsInitialized()
	{
		SqlServerEventStoreOptions options = new()
		{
			ConnectionString = "Server=.;Database=Test;Trusted_Connection=True;",
		};

		await Assert.That(options.JsonIndexOptions).IsNotNull();
		await Assert.That(options.JsonIndexOptions.Indexes).IsEmpty();
	}

	[Test]
	public async Task Constructor_GivenUnsupportedJsonIndexIncludeColumn_ThrowsArgumentException()
	{
		await Assert
			.That(() =>
			{
				SqlServerEventStoreClient _ = new(
					new SqlServerEventStoreOptions
					{
						ConnectionString = "Server=.;Database=Test;Trusted_Connection=True;",
						SchemaName = "dbo",
						TableName = "EventStore",
						AutoCreateTable = false,
						JsonIndexOptions = new SqlServerJsonIndexOptions
						{
							Enabled = true,
							Indexes =
							[
								new SqlServerJsonIndexDefinition
								{
									JsonPath = "$.Value",
									IncludeColumns = ["NotAColumn"],
								},
							],
						},
					}
				);
			})
			.Throws<ArgumentException>();
	}

	[Test]
	public async Task Constructor_GivenJsonIndexWithInvalidPath_ThrowsArgumentException()
	{
		await Assert
			.That(() =>
			{
				SqlServerEventStoreClient _ = new(
					new SqlServerEventStoreOptions
					{
						ConnectionString = "Server=.;Database=Test;Trusted_Connection=True;",
						SchemaName = "dbo",
						TableName = "EventStore",
						AutoCreateTable = false,
						JsonIndexOptions = new SqlServerJsonIndexOptions
						{
							Enabled = true,
							Indexes = [new SqlServerJsonIndexDefinition { JsonPath = "StringProperty" }],
						},
					}
				);
			})
			.Throws<ArgumentException>();
	}

	[Test]
	public async Task Constructor_GivenJsonIndexWithDuplicateIndexNames_ThrowsArgumentException()
	{
		await Assert
			.That(() =>
			{
				SqlServerEventStoreClient _ = new(
					new SqlServerEventStoreOptions
					{
						ConnectionString = "Server=.;Database=Test;Trusted_Connection=True;",
						SchemaName = "dbo",
						TableName = "EventStore",
						AutoCreateTable = false,
						JsonIndexOptions = new SqlServerJsonIndexOptions
						{
							Enabled = true,
							Indexes =
							[
								new SqlServerJsonIndexDefinition { JsonPath = "$.Value", IndexName = "IX_Duplicate" },
								new SqlServerJsonIndexDefinition
								{
									JsonPath = "$.OtherValue",
									IndexName = "IX_Duplicate",
								},
							],
						},
					}
				);
			})
			.Throws<ArgumentException>();
	}

	[Test]
	public async Task Constructor_GivenJsonIndexWithUnsafeFilter_ThrowsArgumentException()
	{
		await Assert
			.That(() =>
			{
				SqlServerEventStoreClient _ = new(
					new SqlServerEventStoreOptions
					{
						ConnectionString = "Server=.;Database=Test;Trusted_Connection=True;",
						SchemaName = "dbo",
						TableName = "EventStore",
						AutoCreateTable = false,
						JsonIndexOptions = new SqlServerJsonIndexOptions
						{
							Enabled = true,
							Indexes =
							[
								new SqlServerJsonIndexDefinition
								{
									JsonPath = "$.Value",
									Filter = "[EntityType] = 1; DROP TABLE dbo.EventStore",
								},
							],
						},
					}
				);
			})
			.Throws<ArgumentException>();
	}
}
