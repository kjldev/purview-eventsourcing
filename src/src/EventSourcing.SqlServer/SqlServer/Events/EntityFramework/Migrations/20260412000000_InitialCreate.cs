using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Purview.EventSourcing.SqlServer.Events.EntityFramework.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
	/// <inheritdoc />
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.EnsureSchema(name: "dbo");

		migrationBuilder.CreateTable(
			name: "EventStore",
			schema: "dbo",
			columns: table => new
			{
				Id = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
				EntityType = table.Column<int>(type: "int", nullable: false),
				AggregateId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
				AggregateType = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
				Version = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
				IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
				Payload = table.Column<string>(type: "json", nullable: true),
				EventType = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
				IdempotencyId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
				Timestamp = table.Column<DateTimeOffset>(
					type: "datetimeoffset",
					nullable: false,
					defaultValueSql: "SYSUTCDATETIME()"
				),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_EventStore", x => x.Id);
			}
		);

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
				name: "IX_EventStore_AggregateType_EntityType",
				schema: "dbo",
				table: "EventStore",
				columns: new[] { "AggregateType", "EntityType", "IsDeleted" }
			)
			.Annotation("SqlServer:Include", new[] { "AggregateId" });

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

	/// <inheritdoc />
	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropTable(name: "EventStore", schema: "dbo");
	}
}
