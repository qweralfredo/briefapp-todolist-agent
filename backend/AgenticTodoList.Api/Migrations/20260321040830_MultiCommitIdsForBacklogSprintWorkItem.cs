using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BriefappTodoList.Api.Migrations
{
    /// <inheritdoc />
    public partial class MultiCommitIdsForBacklogSprintWorkItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<string>>(
                name: "CommitIds",
                table: "WorkItems",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.AddColumn<List<string>>(
                name: "CommitIds",
                table: "Sprints",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.AddColumn<List<string>>(
                name: "CommitIds",
                table: "BacklogItems",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CommitIds",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "CommitIds",
                table: "Sprints");

            migrationBuilder.DropColumn(
                name: "CommitIds",
                table: "BacklogItems");
        }
    }
}
