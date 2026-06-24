using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("EventSourcing.InMemory.IntegrationTests")]
[assembly: InternalsVisibleTo("EventSourcing.AzureStorage.IntegrationTests")]
[assembly: InternalsVisibleTo("EventSourcing.CosmosDb.IntegrationTests")]
[assembly: InternalsVisibleTo("EventSourcing.MongoDB.IntegrationTests")]
[assembly: InternalsVisibleTo("EventSourcing.SqlServer.IntegrationTests")]

[assembly: InternalsVisibleTo("EventSourcing.InMemory")]
[assembly: InternalsVisibleTo("EventSourcing.AzureStorage")]
[assembly: InternalsVisibleTo("EventSourcing.CosmosDb")]
[assembly: InternalsVisibleTo("EventSourcing.MongoDB")]
[assembly: InternalsVisibleTo("EventSourcing.SqlServer")]
