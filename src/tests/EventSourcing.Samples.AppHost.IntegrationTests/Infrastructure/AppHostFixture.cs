using System.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TUnit.Aspire;

namespace Purview.EventSourcing.Samples.AppHost.Infrastructure;

public sealed class AppHostFixture : AspireFixture<Program>
{
    readonly string _databaseName = $"EventSourcingSampleTest_" + $"{Guid.NewGuid():N}"[..8];
    string? _databaseConnectionString;

    IServiceProvider? _serviceProvider;
    readonly ServiceCollection _services = [];

    protected override string[] Args => [$"--DatabaseName={_databaseName}", "--IsTestRun"];

    public IServiceProvider ServiceProvider
    {
        get
        {
            if (_serviceProvider is null)
                throw new InvalidOperationException("The fixture has not been initialised yet.");

            return _serviceProvider;
        }
    }

    public override async ValueTask DisposeAsync()
    {
        await DeleteDatabaseAsync();

        await base.DisposeAsync();
    }

    async Task DeleteDatabaseAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using SqlConnection conn = new(_databaseConnectionString);
            {
                await conn.OpenAsync(cancellationToken);
                // Terminate any open connections to the database before dropping it.
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText =
                        $@"
                        DECLARE @kill varchar(8000) = '';
                        SELECT @kill = @kill + 'KILL ' + CONVERT(varchar(5), session_id) + ';'
                        FROM sys.dm_exec_sessions
                        WHERE database_id  = DB_ID('{_databaseName}')
                        EXEC(@kill);
                    ";
                    await cmd.ExecuteNonQueryAsync(cancellationToken);
                }
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = $"DROP DATABASE IF EXISTS [{_databaseName}]";
                    await cmd.ExecuteNonQueryAsync(cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to delete database '{_databaseName}': {ex}");
        }
    }

    public IServiceCollection ServiceCollection => _services;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        _databaseConnectionString = BuildDatabaseConnectionString(
            await GetConnectionStringAsync("sql")
                ?? throw new InvalidOperationException(
                    "The AppHost did not expose a database connection string."
                )
        );

        await WaitForWebAppAsync(CancellationToken.None);

        _services.AddSqlServerSnapshotQueryableEventStore(registerAsIEventStore: true);

        _serviceProvider = _services.BuildServiceProvider(
            new ServiceProviderOptions() { ValidateOnBuild = true, ValidateScopes = true }
        );
    }

    public HttpClient CreateWebClient() => CreateHttpClient("web", "http");

    async Task WaitForWebAppAsync(CancellationToken cancellationToken)
    {
        using var client = CreateWebClient();
        client.Timeout = TimeSpan.FromSeconds(10);

        var timeoutAt = DateTimeOffset.UtcNow.AddMinutes(3);
        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            try
            {
                using var response = await client.GetAsync("/pingz", cancellationToken);
                Console.WriteLine("Pingz Response: " + response.StatusCode);

                if (response.IsSuccessStatusCode)
                    return;
            }
            catch (Exception) when (DateTimeOffset.UtcNow < timeoutAt)
            {
                // Resource may still be starting.
            }

            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        }

        throw new InvalidOperationException("The web app resource did not become ready in time.");
    }

    string BuildDatabaseConnectionString(string connectionString)
    {
        SqlConnectionStringBuilder builder = new(connectionString)
        {
            InitialCatalog = _databaseName,
        };

        return builder.ConnectionString;
    }

    public IQueryableEventStore QueryableEventStore()
    {
        if (_databaseConnectionString is null)
            throw new InvalidOperationException("The database connection string is not available.");

        return ServiceProvider.GetRequiredService<IQueryableEventStore>();
    }

    public IEventStore EventStore()
    {
        if (_databaseConnectionString is null)
            throw new InvalidOperationException("The database connection string is not available.");

        return ServiceProvider!.GetRequiredService<IEventStore>();
    }

    public IServiceCollection CloneServiceCollection(
        Action<IServiceCollection>? configuration = null
    )
    {
        ServiceCollection clone = [.. _services];

        configuration?.Invoke(clone);

        return clone;
    }

    public IServiceProvider CloneServiceProvider(Action<IServiceCollection>? configuration = null)
    {
        var services = CloneServiceCollection(configuration);

        return services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true }
        );
    }
}
