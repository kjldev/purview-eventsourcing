using System.Linq.Expressions;
using System.Reflection;
using Purview.EventSourcing.Aggregates;
using Purview.EventSourcing.Aggregates.Events;
using Purview.EventSourcing.Serialization;
using Purview.EventSourcing.SqlServer.Client;

namespace Purview.EventSourcing.SqlServer.Snapshots;

public sealed class SqlServerSnapshotClientTests
{
	[Test]
	public async Task Constructor_GivenDefaultOptions_DoesNotUseLegacyEnsureTableSql()
	{
		// Arrange & Act
		var client = new SqlServerClient(
			new SqlServerClientOptions("Server=.;Database=Test;Trusted_Connection=True;", false)
			{
				SchemaName = "dbo",
				TableName = "Snapshots",
				AutoCreateTable = false,
			}
		);

		var ensureTableSqlField = typeof(SqlServerClient).GetField(
			"_ensureTableSql",
			BindingFlags.Instance | BindingFlags.NonPublic
		);

		// Assert
		await Assert.That(client).IsNotNull();
		await Assert.That(ensureTableSqlField).IsNull();
	}

	[Test]
	public async Task GetByIdAsync_GivenUnsupportedPayloadShape_ThrowsEarly()
	{
		var client = new SqlServerClient(
			new SqlServerClientOptions("Server=.;Database=Test;Trusted_Connection=True;Encrypt=False;", false)
			{
				SchemaName = "dbo",
				TableName = $"Snapshots_{Guid.NewGuid():N}",
				AutoCreateTable = false,
			}
		);

		Task<UnsupportedPayloadAggregate?> Act() => client.GetByIdAsync<UnsupportedPayloadAggregate>("id");

		var ex = await Assert.That(Act).Throws<InvalidOperationException>();
		await Assert.That(ex).IsNotNull();
		await Assert.That(ex.Message).Contains(nameof(UnsupportedPayloadAggregate.UnsupportedMap));
	}

	[Test]
	public async Task RewriteAggregateTypePredicate_GivenScalarEqualsPrimitive_RewritesToPrimitiveComparison()
	{
		Expression<Func<ScalarHolder, bool>> whereClause = model => model.Email == "updated@test.com";
		var method = typeof(SqlServerClient).GetMethod(
			"RewriteAggregateTypePredicate",
			BindingFlags.Static | BindingFlags.NonPublic
		);
		await Assert.That(method).IsNotNull();

		var genericMethod = method!.MakeGenericMethod(typeof(ScalarHolder));
		var rewritten =
			(Expression<Func<ScalarHolder, bool>>)genericMethod.Invoke(null, [whereClause, nameof(ScalarHolder)])!;

		var binaryExpression = rewritten.Body as BinaryExpression;
		await Assert.That(binaryExpression).IsNotNull();
		await Assert.That(binaryExpression!.NodeType).IsEqualTo(ExpressionType.Equal);
		await Assert.That(binaryExpression.Left.Type).IsEqualTo(typeof(string));
		await Assert.That(binaryExpression.Right.Type).IsEqualTo(typeof(string));
	}

	[Scalar]
	readonly record struct ScalarEmail
	{
		public string Value { get; }

		public ScalarEmail(string value) => Value = value;

		public static bool operator ==(ScalarEmail left, string right) => left.Value == right;

		public static bool operator !=(ScalarEmail left, string right) => !(left == right);

		public static implicit operator string(ScalarEmail value) => value.Value;
	}

	sealed class ScalarHolder
	{
		public ScalarEmail Email { get; init; } = new("default@test.com");
	}

	sealed class UnsupportedPayloadAggregate : IAggregate
	{
		public string AggregateType => nameof(UnsupportedPayloadAggregate);

		public AggregateDetails Details { get; init; } = new();

		public Dictionary<string, string> UnsupportedMap { get; init; } = [];

		public IEnumerable<IEvent> GetUnsavedEvents() => [];

		public bool HasUnsavedEvents() => false;

		public IEnumerable<Type> GetRegisteredEventTypes() => [];

		public bool CanApplyEvent(IEvent aggregateEvent) => false;

		public void ClearUnsavedEvents(int? upToVersion = null) { }

		void IAggregate.ApplyEvent(IEvent @event) { }
	}
}
