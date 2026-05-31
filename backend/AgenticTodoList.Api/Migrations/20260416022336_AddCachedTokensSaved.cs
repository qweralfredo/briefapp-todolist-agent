using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BriefappTodoList.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCachedTokensSaved : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CachedTokensSaved",
                table: "TokenBudgets",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CachedTokensSaved",
                table: "TokenBudgets");
        }
    }
}
