using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BriefappTodoList.Api.Migrations
{
    /// <inheritdoc />
    public partial class WorkItemTokenTrackingAndFeedback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastIdeUsed",
                table: "WorkItems",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LastModelUsed",
                table: "WorkItems",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "TotalTokensSpent",
                table: "WorkItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "WorkItemFeedbacks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ModelUsed = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IdeUsed = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    TokensUsed = table.Column<int>(type: "integer", nullable: false),
                    Feedback = table.Column<string>(type: "text", nullable: false),
                    MetadataJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItemFeedbacks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkItemFeedbacks_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemFeedbacks_WorkItemId_CreatedAt",
                table: "WorkItemFeedbacks",
                columns: new[] { "WorkItemId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkItemFeedbacks");

            migrationBuilder.DropColumn(
                name: "LastIdeUsed",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "LastModelUsed",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "TotalTokensSpent",
                table: "WorkItems");
        }
    }
}
