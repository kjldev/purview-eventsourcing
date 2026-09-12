using System.ComponentModel.DataAnnotations;
using Purview.Aspire.ResourceKit;

namespace Purview.EventSourcing.Samples.AppHost.AppModel.Resources;

[ResourceDefinition<MongoDBServerResource>(Platform.MongoDB)]
sealed partial class MongoDBKit
{
	public IResourceBuilder<MongoDBDatabaseResource> Database { get; private set; }
	public IResourceBuilder<MongoDBDatabaseResource> SharedQueryDatabase { get; private set; }

	protected override IResourceBuilder<MongoDBServerResource> BuildResource(IDistributedApplicationBuilder builder)
	{
		var mongo = builder.AddMongoDB(Name);

		Database = mongo.AddDatabase(Options.DatabaseName);
		SharedQueryDatabase = mongo.AddDatabase(Options.SharedQueryDatabaseName);

		if (HostKit.Options.UseDataVolumes)
			mongo.WithDataVolume("eventsourcing-sample-mongo-data");

		return mongo;
	}

	partial class MongoDBKitOptions
	{
		[Required(AllowEmptyStrings = false)]
		public string DatabaseName { get; set; } = Platform.MongoDatabase;

		[Required(AllowEmptyStrings = false)]
		public string SharedQueryDatabaseName { get; set; } = Platform.MongoSharedQueryDatabase;
	}
}
