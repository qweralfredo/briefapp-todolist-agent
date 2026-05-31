using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BriefappTodoList.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAllowList : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AllowListEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    AppName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CallbackUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Scopes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AllowListEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AllowListEntries_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AllowListEntries_ProjectId_AppName",
                table: "AllowListEntries",
                columns: new[] { "ProjectId", "AppName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AllowListEntries");
        }
    }
}
