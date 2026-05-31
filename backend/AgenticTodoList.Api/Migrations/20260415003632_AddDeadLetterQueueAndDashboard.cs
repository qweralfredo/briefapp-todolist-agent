using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BriefappTodoList.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDeadLetterQueueAndDashboard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "DurationMs",
                table: "TaskMessages",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DlqEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BoxId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalTopic = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    TaskPayload = table.Column<string>(type: "text", nullable: false),
                    OriginalTaskId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    FailureReason = table.Column<string>(type: "text", nullable: false),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    FirstFailedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastFailedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DlqEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DlqEntries_BoxId",
                table: "DlqEntries",
                column: "BoxId");

            migrationBuilder.CreateIndex(
                name: "IX_DlqEntries_Status_FirstFailedAt",
                table: "DlqEntries",
                columns: new[] { "Status", "FirstFailedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DlqEntries");

            migrationBuilder.DropColumn(
                name: "DurationMs",
                table: "TaskMessages");
        }
    }
}
