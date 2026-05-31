using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BriefappTodoList.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSandboxMetricsAndNetworkPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NetworkPolicy",
                table: "Sandboxes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StartedAt",
                table: "Sandboxes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SandboxMetricSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SandboxId = table.Column<Guid>(type: "uuid", nullable: false),
                    CpuPercent = table.Column<double>(type: "double precision", nullable: false),
                    MemoryMb = table.Column<double>(type: "double precision", nullable: false),
                    MemoryPercent = table.Column<double>(type: "double precision", nullable: false),
                    NetworkRxBytes = table.Column<long>(type: "bigint", nullable: false),
                    NetworkTxBytes = table.Column<long>(type: "bigint", nullable: false),
                    DiskReadBytes = table.Column<long>(type: "bigint", nullable: false),
                    DiskWriteBytes = table.Column<long>(type: "bigint", nullable: false),
                    UptimeSeconds = table.Column<long>(type: "bigint", nullable: false),
                    CapturedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SandboxMetricSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SandboxMetricSnapshots_Sandboxes_SandboxId",
                        column: x => x.SandboxId,
                        principalTable: "Sandboxes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SandboxMetricSnapshots_SandboxId_CapturedAt",
                table: "SandboxMetricSnapshots",
                columns: new[] { "SandboxId", "CapturedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SandboxMetricSnapshots");

            migrationBuilder.DropColumn(
                name: "NetworkPolicy",
                table: "Sandboxes");

            migrationBuilder.DropColumn(
                name: "StartedAt",
                table: "Sandboxes");
        }
    }
}
