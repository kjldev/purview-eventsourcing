using System.Reflection;

namespace Purview.EventSourcing.SqlServer;

public sealed class SqlServerSnapshotClientTests
{
	[Test]
	public async Task Constructor_GivenDefaultOptions_EnsureTableSqlUsesApplicationLock()
	{
		// Arrange & Act
		var client = new SqlServerClient(
			new SqlServerClientOptions
			{
				ConnectionString = "Server=.;Database=Test;Trusted_Connection=True;",
				SchemaName = "dbo",
				TableName = "Snapshots",
				AutoCreateTable = false,
			}
		);

		var ensureTableSql = GetEnsureTableSql(client);

		// Assert
		await Assert.That(ensureTableSql).Contains("sp_getapplock");
		await Assert.That(ensureTableSql).Contains("@LockOwner = 'Transaction'");
		await Assert.That(ensureTableSql).Contains("BEGIN TRANSACTION");
		await Assert.That(ensureTableSql).Contains("ROLLBACK TRANSACTION");
	}

	static string GetEnsureTableSql(SqlServerClient client) =>
		typeof(SqlServerClient)
			.GetField("_ensureTableSql", BindingFlags.Instance | BindingFlags.NonPublic)
			?.GetValue(client) as string
		?? throw new InvalidOperationException("Could not read _ensureTableSql.");
}
