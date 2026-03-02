using TUnit.Core.Interfaces;

namespace Purview.EventSourcing.SnapshotOnly.SqlServer;

public class SqlServerSnapshotEventStoreFixture : IAsyncInitializer, IAsyncDisposable
{
	readonly Testcontainers.Azurite.AzuriteContainer _azuriteContainer;
	readonly Testcontainers.MsSql.MsSqlContainer _msSqlContainer;

	public SqlServerSnapshotEventStoreFixture()
	{
		_azuriteContainer = ContainerHelper.CreateAzurite();
		_msSqlContainer = ContainerHelper.CreateMsSql();
	}

	public SqlServerSnapshotTestContext CreateContext(int correlationIdsToGenerate = 1, string? tableName = null) =>
		new(
			_msSqlContainer.GetConnectionString(),
			_azuriteContainer.GetConnectionString(),
			correlationIdsToGenerate,
			tableName
		);

	public async Task InitializeAsync()
	{
		await _msSqlContainer.StartAsync();
		await _azuriteContainer.StartAsync();
	}

	public async ValueTask DisposeAsync()
	{
		GC.SuppressFinalize(this);

		await _msSqlContainer.DisposeAsync();
		await _azuriteContainer.DisposeAsync();
	}
}
