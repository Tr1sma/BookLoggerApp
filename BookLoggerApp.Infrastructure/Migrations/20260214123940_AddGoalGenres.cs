using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookLoggerApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGoalGenres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GoalGenres",
                columns: table => new
                {
                    ReadingGoalId = table.Column<Guid>(type: "TEXT", nullable: false),
                    GenreId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoalGenres", x => new { x.ReadingGoalId, x.GenreId });
                    table.ForeignKey(
                        name: "FK_GoalGenres_Genres_GenreId",
                        column: x => x.GenreId,
                        principalTable: "Genres",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GoalGenres_ReadingGoals_ReadingGoalId",
                        column: x => x.ReadingGoalId,
                        principalTable: "ReadingGoals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GoalGenres_GenreId",
                table: "GoalGenres",
                column: "GenreId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GoalGenres");
        }
    }
}
