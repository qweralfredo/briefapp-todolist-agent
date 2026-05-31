using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BriefappTodoList.Api.Migrations
{
    /// <inheritdoc />
    public partial class DevLakeMetricsAndHumanEvaluation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add DevLake metrics fields to AgentRunLogs (SP-03 BL-05)
            migrationBuilder.AddColumn<decimal>(
                name: "CostUsd",
                table: "AgentRunLogs",
                type: "numeric(10,6)",
                precision: 10,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Environment",
                table: "AgentRunLogs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "production");

            migrationBuilder.AddColumn<string>(
                name: "ErrorMessage",
                table: "AgentRunLogs",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "LatencyMs",
                table: "AgentRunLogs",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "ModelName",
                table: "AgentRunLogs",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "Success",
                table: "AgentRunLogs",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "TokensInput",
                table: "AgentRunLogs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TokensOutput",
                table: "AgentRunLogs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Create HumanEvaluations table (SP-03 BL-06)
            migrationBuilder.CreateTable(
                name: "HumanEvaluations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewerId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AccuracyScore = table.Column<float>(type: "real", nullable: false),
                    RelevanceScore = table.Column<float>(type: "real", nullable: false),
                    CompletenessScore = table.Column<float>(type: "real", nullable: false),
                    SafetyScore = table.Column<float>(type: "real", nullable: false),
                    Score = table.Column<float>(type: "real", nullable: false),
                    FeedbackText = table.Column<string>(type: "text", nullable: false),
                    RequiresEscalation = table.Column<bool>(type: "boolean", nullable: false),
                    ReviewTimeSeconds = table.Column<long>(type: "bigint", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HumanEvaluations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HumanEvaluations_AgentRunLogs_AgentRunId",
                        column: x => x.AgentRunId,
                        principalTable: "AgentRunLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HumanEvaluations_AgentRunId_ReviewerId",
                table: "HumanEvaluations",
                columns: new[] { "AgentRunId", "ReviewerId" });

            migrationBuilder.CreateIndex(
                name: "IX_HumanEvaluations_SubmittedAt",
                table: "HumanEvaluations",
                column: "SubmittedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "HumanEvaluations");

            migrationBuilder.DropColumn(name: "CostUsd", table: "AgentRunLogs");
            migrationBuilder.DropColumn(name: "Environment", table: "AgentRunLogs");
            migrationBuilder.DropColumn(name: "ErrorMessage", table: "AgentRunLogs");
            migrationBuilder.DropColumn(name: "LatencyMs", table: "AgentRunLogs");
            migrationBuilder.DropColumn(name: "ModelName", table: "AgentRunLogs");
            migrationBuilder.DropColumn(name: "Success", table: "AgentRunLogs");
            migrationBuilder.DropColumn(name: "TokensInput", table: "AgentRunLogs");
            migrationBuilder.DropColumn(name: "TokensOutput", table: "AgentRunLogs");
        }
    }
}
