using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgenticTodoList.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddFallbackAndBudget : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── FallbackAttemptLogs ───────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "FallbackAttemptLogs",
                columns: table => new
                {
                    Id         = table.Column<Guid>(nullable: false),
                    BoxId      = table.Column<Guid>(nullable: false),
                    TaskId     = table.Column<string>(maxLength: 200, nullable: false),
                    Strategy   = table.Column<int>(nullable: false),
                    Success    = table.Column<bool>(nullable: false),
                    DurationMs = table.Column<int>(nullable: false),
                    FromModel  = table.Column<string>(maxLength: 100, nullable: true),
                    ToModel    = table.Column<string>(maxLength: 100, nullable: true),
                    Message    = table.Column<string>(maxLength: 1000, nullable: false),
                    Timestamp  = table.Column<DateTimeOffset>(nullable: false),
                },
                constraints: table => table.PrimaryKey("PK_FallbackAttemptLogs", x => x.Id));

            migrationBuilder.CreateIndex(
                name: "IX_FallbackAttemptLogs_BoxId_Timestamp",
                table: "FallbackAttemptLogs",
                columns: ["BoxId", "Timestamp"]);

            // ── TokenBudgets ──────────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "TokenBudgets",
                columns: table => new
                {
                    Id                    = table.Column<Guid>(nullable: false),
                    Scope                 = table.Column<int>(nullable: false),
                    ScopeId              = table.Column<string>(maxLength: 200, nullable: false),
                    BudgetTokens          = table.Column<long>(nullable: false),
                    UsedTokens            = table.Column<long>(nullable: false),
                    AlertThresholdPercent = table.Column<int>(nullable: false, defaultValue: 80),
                    HardStopPercent       = table.Column<int>(nullable: false, defaultValue: 100),
                    Frozen               = table.Column<bool>(nullable: false, defaultValue: false),
                    CreatedAt            = table.Column<DateTimeOffset>(nullable: false),
                    UpdatedAt            = table.Column<DateTimeOffset>(nullable: false),
                },
                constraints: table => table.PrimaryKey("PK_TokenBudgets", x => x.Id));

            migrationBuilder.CreateIndex(
                name: "IX_TokenBudgets_Scope_ScopeId",
                table: "TokenBudgets",
                columns: ["Scope", "ScopeId"],
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable("FallbackAttemptLogs");
            migrationBuilder.DropTable("TokenBudgets");
        }
    }
}
