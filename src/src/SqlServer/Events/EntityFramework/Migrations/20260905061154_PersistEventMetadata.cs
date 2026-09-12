using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Purview.EventSourcing.SqlServer.Events.EntityFramework.Migrations
{
	/// <inheritdoc />
	public partial class PersistEventMetadata : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropIndex(
				name: "IX_EventStore_AggregateId_EntityType",
				schema: "dbo",
				table: "EventStore"
			);

			migrationBuilder.DropIndex(name: "IX_EventStore_EventRange", schema: "dbo", table: "EventStore");

			migrationBuilder.AddColumn<string>(
				name: "CausationId",
				schema: "dbo",
				table: "EventStore",
				type: "nvarchar(450)",
				maxLength: 450,
				nullable: true
			);

			migrationBuilder.AddColumn<string>(
				name: "CorrelationId",
				schema: "dbo",
				table: "EventStore",
				type: "nvarchar(450)",
				maxLength: 450,
				nullable: true
			);

			migrationBuilder.AddColumn<int>(
				name: "SchemaVersion",
				schema: "dbo",
				table: "EventStore",
				type: "int",
				nullable: false,
				defaultValue: 1
			);

			migrationBuilder.AddColumn<string>(
				name: "UserId",
				schema: "dbo",
				table: "EventStore",
				type: "nvarchar(450)",
				maxLength: 450,
				nullable: true
			);

			migrationBuilder
				.CreateIndex(
					name: "IX_EventStore_AggregateId_EntityType",
					schema: "dbo",
					table: "EventStore",
					columns: new[] { "AggregateId", "AggregateType", "EntityType" }
				)
				.Annotation("SqlServer:Include", new[] { "Version", "IsDeleted" });

			migrationBuilder
				.CreateIndex(
					name: "IX_EventStore_EventRange",
					schema: "dbo",
					table: "EventStore",
					columns: new[] { "AggregateId", "AggregateType", "Version" },
					filter: "[EntityType] = 1"
				)
				.Annotation(
					"SqlServer:Include",
					new[]
					{
						"Payload",
						"EventType",
						"IdempotencyId",
						"SchemaVersion",
						"CorrelationId",
						"CausationId",
						"UserId",
						"Timestamp",
					}
				);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropIndex(
				name: "IX_EventStore_AggregateId_EntityType",
				schema: "dbo",
				table: "EventStore"
			);

			migrationBuilder.DropIndex(name: "IX_EventStore_EventRange", schema: "dbo", table: "EventStore");

			migrationBuilder.DropColumn(name: "CausationId", schema: "dbo", table: "EventStore");

			migrationBuilder.DropColumn(name: "CorrelationId", schema: "dbo", table: "EventStore");

			migrationBuilder.DropColumn(name: "SchemaVersion", schema: "dbo", table: "EventStore");

			migrationBuilder.DropColumn(name: "UserId", schema: "dbo", table: "EventStore");

			migrationBuilder
				.CreateIndex(
					name: "IX_EventStore_AggregateId_EntityType",
					schema: "dbo",
					table: "EventStore",
					columns: new[] { "AggregateId", "EntityType" }
				)
				.Annotation(
					"SqlServer:Include",
					new[] { "Version", "IsDeleted", "AggregateType", "EventType", "IdempotencyId", "Timestamp" }
				);

			migrationBuilder
				.CreateIndex(
					name: "IX_EventStore_EventRange",
					schema: "dbo",
					table: "EventStore",
					columns: new[] { "AggregateId", "EntityType", "Version" },
					filter: "[EntityType] = 1"
				)
				.Annotation(
					"SqlServer:Include",
					new[] { "Payload", "EventType", "IdempotencyId", "IsDeleted", "AggregateType", "Timestamp" }
				);
		}
	}
}
