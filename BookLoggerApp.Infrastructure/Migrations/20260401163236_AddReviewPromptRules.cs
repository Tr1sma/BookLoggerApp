using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookLoggerApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewPromptRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastReviewPromptDate",
                table: "AppSettings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ReviewPromptDisabled",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ReviewPromptMonthCount",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "AppSettings",
                keyColumn: "Id",
                keyValue: new Guid("99999999-0000-0000-0000-000000000001"),
                columns: new[] { "LastReviewPromptDate", "ReviewPromptDisabled", "ReviewPromptMonthCount" },
                values: new object[] { null, false, 0 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastReviewPromptDate",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "ReviewPromptDisabled",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "ReviewPromptMonthCount",
                table: "AppSettings");
        }
    }
}
