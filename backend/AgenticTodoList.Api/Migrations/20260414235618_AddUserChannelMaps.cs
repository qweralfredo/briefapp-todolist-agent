using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BriefappTodoList.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUserChannelMaps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Sandboxes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BoxId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<string>(type: "text", nullable: true),
                    ImageName = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ContainerId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CpuCores = table.Column<double>(type: "double precision", nullable: false),
                    MemoryMb = table.Column<int>(type: "integer", nullable: false),
                    NetworkMode = table.Column<int>(type: "integer", nullable: false),
                    TimeoutAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StoppedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sandboxes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserChannelMaps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BoxId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChannelType = table.Column<int>(type: "integer", nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RegisteredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserChannelMaps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserChannelMaps_Projects_BoxId",
                        column: x => x.BoxId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sandboxes_BoxId_Status",
                table: "Sandboxes",
                columns: new[] { "BoxId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_UserChannelMaps_BoxId",
                table: "UserChannelMaps",
                column: "BoxId");

            migrationBuilder.CreateIndex(
                name: "IX_UserChannelMaps_ChannelType_ExternalId",
                table: "UserChannelMaps",
                columns: new[] { "ChannelType", "ExternalId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Sandboxes");

            migrationBuilder.DropTable(
                name: "UserChannelMaps");
        }
    }
}
