using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BriefappTodoList.Api.Migrations
{
    /// <inheritdoc />
    public partial class SubTasksContextFirstAndBranch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Branch",
                table: "WorkItems",
                type: "character varying(250)",
                maxLength: 250,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "ParentWorkItemId",
                table: "WorkItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tags",
                table: "WorkItems",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Constraints",
                table: "BacklogItems",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Tags",
                table: "BacklogItems",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "WikiRefs",
                table: "BacklogItems",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_ParentWorkItemId",
                table: "WorkItems",
                column: "ParentWorkItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkItems_WorkItems_ParentWorkItemId",
                table: "WorkItems",
                column: "ParentWorkItemId",
                principalTable: "WorkItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkItems_WorkItems_ParentWorkItemId",
                table: "WorkItems");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_ParentWorkItemId",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "Branch",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "ParentWorkItemId",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "Tags",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "Constraints",
                table: "BacklogItems");

            migrationBuilder.DropColumn(
                name: "Tags",
                table: "BacklogItems");

            migrationBuilder.DropColumn(
                name: "WikiRefs",
                table: "BacklogItems");
        }
    }
}
