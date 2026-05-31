using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BriefappTodoList.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAzureDevOpsMappings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AzureDevOpsMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BriefappWorkItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    AzureDevOpsWorkItemId = table.Column<int>(type: "integer", nullable: false),
                    AzureDevOpsUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    LastSyncedRev = table.Column<int>(type: "integer", nullable: false),
                    LastSyncedStatus = table.Column<int>(type: "integer", nullable: false),
                    LastSyncAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AzureDevOpsMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AzureDevOpsMappings_WorkItems_BriefappWorkItemId",
                        column: x => x.BriefappWorkItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AzureDevOpsMappings_BriefappWorkItemId",
                table: "AzureDevOpsMappings",
                column: "BriefappWorkItemId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AzureDevOpsMappings");
        }
    }
}
