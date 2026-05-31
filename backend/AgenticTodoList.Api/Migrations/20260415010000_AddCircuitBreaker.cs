using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BriefappTodoList.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCircuitBreaker : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── CircuitBreakers ──────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "CircuitBreakers",
                columns: table => new
                {
                    Id                = table.Column<Guid>(type: "uuid", nullable: false),
                    BoxId             = table.Column<Guid>(type: "uuid", nullable: false),
                    State             = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    FailureCount      = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    LastFailureAt     = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TrippedAt         = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    HalfOpenCallCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    LastTransitionAt  = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FailureThreshold  = table.Column<int>(type: "integer", nullable: false, defaultValue: 3),
                    CooldownSeconds   = table.Column<int>(type: "integer", nullable: false, defaultValue: 300),
                    HalfOpenMaxCalls  = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CircuitBreakers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CircuitBreakers_BoxId",
                table: "CircuitBreakers",
                column: "BoxId",
                unique: true);

            // ── BreakerTransitions ───────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "BreakerTransitions",
                columns: table => new
                {
                    Id          = table.Column<Guid>(type: "uuid", nullable: false),
                    BoxId       = table.Column<Guid>(type: "uuid", nullable: false),
                    FromState   = table.Column<int>(type: "integer", nullable: false),
                    ToState     = table.Column<int>(type: "integer", nullable: false),
                    Category    = table.Column<int>(type: "integer", nullable: true),
                    Reason      = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    TriggeredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BreakerTransitions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BreakerTransitions_BoxId_TriggeredAt",
                table: "BreakerTransitions",
                columns: new[] { "BoxId", "TriggeredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "BreakerTransitions");
            migrationBuilder.DropTable(name: "CircuitBreakers");
        }
    }
}
