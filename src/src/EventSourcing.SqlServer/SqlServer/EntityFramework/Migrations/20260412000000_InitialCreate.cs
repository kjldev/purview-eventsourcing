using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Purview.EventSourcing.SqlServer.EntityFramework.Migrations
{
	/// <inheritdoc />
	public partial class InitialCreate : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.EnsureSchema(name: "dbo");

			migrationBuilder.CreateTable(
				name: "Snapshots",
				schema: "dbo",
				columns: table => new
				{
					Id = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
					AggregateType = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
					Payload = table.Column<string>(type: "json", nullable: false),
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_Snapshots", x => x.Id);
				}
			);

			migrationBuilder
				.CreateIndex(
					name: "IX_Snapshots_AggregateType",
					schema: "dbo",
					table: "Snapshots",
					column: "AggregateType"
				)
				.Annotation("SqlServer:Include", new[] { "Payload" });
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(name: "Snapshots", schema: "dbo");
		}
	}
}
