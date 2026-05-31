using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BriefappTodoList.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPromptCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BreakerTransitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BoxId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromState = table.Column<int>(type: "integer", nullable: false),
                    ToState = table.Column<int>(type: "integer", nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    TriggeredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BreakerTransitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChannelHealths",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChannelType = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    LastCheckAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FailoverTarget = table.Column<int>(type: "integer", nullable: true),
                    DownSince = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CheckCount = table.Column<int>(type: "integer", nullable: false),
                    FailureCount = table.Column<int>(type: "integer", nullable: false),
                    UptimePercent = table.Column<double>(type: "double precision", nullable: false),
                    DeliveryRate = table.Column<double>(type: "double precision", nullable: false),
                    AvgLatencyMs = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChannelHealths", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CircuitBreakers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BoxId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    FailureCount = table.Column<int>(type: "integer", nullable: false),
                    LastFailureAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TrippedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    HalfOpenCallCount = table.Column<int>(type: "integer", nullable: false),
                    LastTransitionAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FailureThreshold = table.Column<int>(type: "integer", nullable: false),
                    CooldownSeconds = table.Column<int>(type: "integer", nullable: false),
                    HalfOpenMaxCalls = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CircuitBreakers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FallbackAttemptLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BoxId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<string>(type: "text", nullable: false),
                    Strategy = table.Column<int>(type: "integer", nullable: false),
                    Success = table.Column<bool>(type: "boolean", nullable: false),
                    DurationMs = table.Column<int>(type: "integer", nullable: false),
                    FromModel = table.Column<string>(type: "text", nullable: true),
                    ToModel = table.Column<string>(type: "text", nullable: true),
                    Message = table.Column<string>(type: "text", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FallbackAttemptLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboundLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Channel = table.Column<int>(type: "integer", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    Delivered = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboundLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PromptCacheEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BoxId = table.Column<Guid>(type: "uuid", nullable: false),
                    SegmentType = table.Column<int>(type: "integer", nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    TokenCount = table.Column<int>(type: "integer", nullable: false),
                    HitCount = table.Column<long>(type: "bigint", nullable: false),
                    MissCount = table.Column<long>(type: "bigint", nullable: false),
                    TtlMinutes = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastUsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromptCacheEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    ChannelType = table.Column<int>(type: "integer", nullable: false),
                    BoxId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastActiveAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Preferences = table.Column<string>(type: "text", nullable: false),
                    HandoffState = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TokenBudgets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Scope = table.Column<int>(type: "integer", nullable: false),
                    ScopeId = table.Column<string>(type: "text", nullable: false),
                    BudgetTokens = table.Column<long>(type: "bigint", nullable: false),
                    UsedTokens = table.Column<long>(type: "bigint", nullable: false),
                    AlertThresholdPercent = table.Column<int>(type: "integer", nullable: false),
                    HardStopPercent = table.Column<int>(type: "integer", nullable: false),
                    Frozen = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TokenBudgets", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BreakerTransitions_BoxId_TriggeredAt",
                table: "BreakerTransitions",
                columns: new[] { "BoxId", "TriggeredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CircuitBreakers_BoxId",
                table: "CircuitBreakers",
                column: "BoxId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PromptCacheEntries_BoxId_SegmentType_ContentHash",
                table: "PromptCacheEntries",
                columns: new[] { "BoxId", "SegmentType", "ContentHash" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BreakerTransitions");

            migrationBuilder.DropTable(
                name: "ChannelHealths");

            migrationBuilder.DropTable(
                name: "CircuitBreakers");

            migrationBuilder.DropTable(
                name: "FallbackAttemptLogs");

            migrationBuilder.DropTable(
                name: "OutboundLogs");

            migrationBuilder.DropTable(
                name: "PromptCacheEntries");

            migrationBuilder.DropTable(
                name: "Sessions");

            migrationBuilder.DropTable(
                name: "TokenBudgets");
        }
    }
}
