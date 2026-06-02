using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookLoggerApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionMoodTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "MoodTrackingEnabled",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "ReadingSessionMoods",
                columns: table => new
                {
                    ReadingSessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Mood = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadingSessionMoods", x => new { x.ReadingSessionId, x.Mood });
                    table.ForeignKey(
                        name: "FK_ReadingSessionMoods_ReadingSessions_ReadingSessionId",
                        column: x => x.ReadingSessionId,
                        principalTable: "ReadingSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "AppSettings",
                keyColumn: "Id",
                keyValue: new Guid("99999999-0000-0000-0000-000000000001"),
                column: "MoodTrackingEnabled",
                value: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReadingSessionMoods");

            migrationBuilder.DropColumn(
                name: "MoodTrackingEnabled",
                table: "AppSettings");
        }
    }
}
