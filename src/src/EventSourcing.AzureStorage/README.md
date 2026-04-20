# EventSourcing.AzureStorage

`EventSourcing.AzureStorage` adds Azure Table Storage and Blob Storage persistence to Purview EventSourcing.

## Install

```bash
dotnet add package EventSourcing.AzureStorage
```

## Register the provider

```csharp
builder.Services.AddAzureTableEventStore();
```

## What it provides

- Azure Table Storage event persistence
- Azure Blob Storage support for snapshots and large event payloads
- Configuration binding for the Azure Storage-backed event store

## Documentation

- Repository README: https://github.com/kjldev/purview-eventsourcing/blob/main/README.md
