namespace Purview.EventSourcing.AzureStorage;

static class TableEventStoreConstants
{
	public const string StreamVersionRowKey = "version";
	public const string IdempotencyCheckRowKeyPrefix = "i_";
	public const string SnapshotFilename = "snapshot.json";
}
