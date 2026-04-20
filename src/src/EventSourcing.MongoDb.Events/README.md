# EventSourcing.MongoDB.Events

`EventSourcing.MongoDB.Events` adds MongoDB event stream persistence to Purview EventSourcing.

## Install

```bash
dotnet add package EventSourcing.MongoDB.Events
```

## Register the provider

```csharp
builder.Services.AddMongoDBEventStore();
```

## What it provides

- MongoDB-backed event stream persistence
- Configuration binding for the MongoDB event store
- Telemetry registration for MongoDB event operations

## Documentation

- Repository README: https://github.com/kjldev/purview-eventsourcing/blob/main/README.md
