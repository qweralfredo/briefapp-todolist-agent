using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BriefappTodoList.Api.Migrations
{
    /// <inheritdoc />
    public partial class KnowledgeCategorizationAndDocumentation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "WikiPages",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "KnowledgeCheckpoints",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "DocumentationPages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    ContentMarkdown = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Tags = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentationPages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentationPages_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentationPages_ProjectId_Category_Title",
                table: "DocumentationPages",
                columns: new[] { "ProjectId", "Category", "Title" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentationPages");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "WikiPages");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "KnowledgeCheckpoints");
        }
    }
}

