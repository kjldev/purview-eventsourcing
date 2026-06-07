using Purview.EventSourcing.Aggregates;

namespace Purview.EventSourcing;

public static class TestHelpers
{
    public static string GenName(Guid? value = null)
    {
        var guidString = Convert.ToBase64String((value ?? Guid.NewGuid()).ToByteArray());
        return guidString
            .Replace("=", "", StringComparison.OrdinalIgnoreCase)
            .Replace("+", "", StringComparison.OrdinalIgnoreCase)
            .Replace("/", "", StringComparison.OrdinalIgnoreCase);
    }

    public static string GenAzureTableName(Guid? value = null, string? prefix = null)
    {
        prefix ??= "ENVTest";

        return $"{prefix}{GenName(value)}";
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Globalization",
        "CA1308:Normalize strings to uppercase"
    )]
    public static string GenAzureBlobContainerName(Guid? value = null, string? prefix = null)
    {
        prefix ??= "ENVTest-";
        return $"{prefix}{GenName(value)}".ToLowerInvariant();
    }

    public static string GenAzureCosmosDbContainerName(Guid? value = null, string? prefix = null)
    {
        prefix ??= "EnvTest_";

        return $"{prefix}{GenName(value)}";
    }

    public static string GenMongoDBCollectionName(Guid? value = null, string? prefix = null)
    {
        prefix ??= "EnvTest_";

        return $"{prefix}{GenName(value)}";
    }

    public static CancellationTokenSource CancellationTokenSource(
        bool throwOnCall = false,
        CancellationToken cancellationToken = default
    )
    {
        CancellationTokenSource cancellationTokenSource = new();
        if (throwOnCall)
            cancellationTokenSource.Cancel();

        cancellationToken.Register(cancellationTokenSource.Cancel);

        return cancellationTokenSource;
    }

    public static IServiceProvider ServiceProvider(params ServiceDefinition[] serviceDefinitions)
    {
        var serviceProvider = Substitute.For<IServiceProvider>();
        if (serviceDefinitions != null)
        {
            foreach (var serviceDef in serviceDefinitions)
            {
                serviceProvider
                    .GetService(Arg.Is(serviceDef.ServiceType))
                    .Returns(serviceDef.Instance);
            }
        }

        return serviceProvider;
    }

    public static TAggregate Aggregate<TAggregate>(
        object? aggregateId = null,
        Action<TAggregate>? creator = null,
        bool clearEvents = true,
        bool clearIsNew = false
    )
        where TAggregate : class, IAggregate, new()
    {
        TAggregate aggregate = new()
        {
            Details =
            {
                Id = aggregateId?.ToString() ?? typeof(TAggregate).Name + $"_{Guid.NewGuid()}",
            },
        };

        creator?.Invoke(aggregate);

        if (clearEvents)
            aggregate.ClearUnsavedEvents(int.MaxValue);

        if (clearIsNew)
            aggregate.Details.SavedVersion = 1;

        return aggregate;
    }
}
