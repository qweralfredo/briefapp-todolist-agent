using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgenticTodoList.Api.Migrations
{
    public partial class AddBox4SessionsAndChannelHealth : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Sessions ──────────────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "Sessions",
                columns: table => new
                {
                    Id           = table.Column<Guid>(nullable: false),
                    UserId       = table.Column<string>(maxLength: 200, nullable: false),
                    ChannelType  = table.Column<int>(nullable: false),
                    BoxId        = table.Column<Guid>(nullable: false),
                    LastActiveAt = table.Column<DateTimeOffset>(nullable: false),
                    Preferences  = table.Column<string>(nullable: false, defaultValue: "{}"),
                    HandoffState = table.Column<int>(nullable: false, defaultValue: 0),
                    CreatedAt    = table.Column<DateTimeOffset>(nullable: false),
                    ExpiresAt    = table.Column<DateTimeOffset>(nullable: false),
                },
                constraints: table => table.PrimaryKey("PK_Sessions", x => x.Id));

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_UserId_ChannelType_BoxId",
                table: "Sessions",
                columns: ["UserId", "ChannelType", "BoxId"]);

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_ExpiresAt",
                table: "Sessions",
                column: "ExpiresAt");

            // ── ChannelHealths ────────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "ChannelHealths",
                columns: table => new
                {
                    Id             = table.Column<Guid>(nullable: false),
                    ChannelType    = table.Column<int>(nullable: false),
                    Status         = table.Column<int>(nullable: false, defaultValue: 0),
                    LastCheckAt    = table.Column<DateTimeOffset>(nullable: false),
                    FailoverTarget = table.Column<int>(nullable: true),
                    DownSince      = table.Column<DateTimeOffset>(nullable: true),
                    CheckCount     = table.Column<int>(nullable: false, defaultValue: 0),
                    FailureCount   = table.Column<int>(nullable: false, defaultValue: 0),
                    UptimePercent  = table.Column<double>(nullable: false, defaultValue: 100.0),
                    DeliveryRate   = table.Column<double>(nullable: false, defaultValue: 100.0),
                    AvgLatencyMs   = table.Column<long>(nullable: false, defaultValue: 0L),
                },
                constraints: table => table.PrimaryKey("PK_ChannelHealths", x => x.Id));

            migrationBuilder.CreateIndex(
                name: "IX_ChannelHealths_ChannelType",
                table: "ChannelHealths",
                column: "ChannelType",
                unique: true);

            // ── OutboundLogs ───────────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "OutboundLogs",
                columns: table => new
                {
                    Id           = table.Column<Guid>(nullable: false),
                    UserId       = table.Column<string>(maxLength: 200, nullable: false),
                    Channel      = table.Column<int>(nullable: false),
                    Message      = table.Column<string>(maxLength: 4096, nullable: false),
                    Delivered    = table.Column<bool>(nullable: false),
                    ErrorMessage = table.Column<string>(maxLength: 500, nullable: true),
                    RetryCount   = table.Column<int>(nullable: false, defaultValue: 0),
                    SentAt       = table.Column<DateTimeOffset>(nullable: false),
                },
                constraints: table => table.PrimaryKey("PK_OutboundLogs", x => x.Id));

            migrationBuilder.CreateIndex(
                name: "IX_OutboundLogs_UserId_SentAt",
                table: "OutboundLogs",
                columns: ["UserId", "SentAt"]);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable("Sessions");
            migrationBuilder.DropTable("ChannelHealths");
            migrationBuilder.DropTable("OutboundLogs");
        }
    }
}
