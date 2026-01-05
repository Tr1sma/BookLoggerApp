using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookLoggerApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateShelfSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlantShelves",
                columns: table => new
                {
                    PlantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ShelfId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlantShelves", x => new { x.PlantId, x.ShelfId });
                    table.ForeignKey(
                        name: "FK_PlantShelves_Shelves_ShelfId",
                        column: x => x.ShelfId,
                        principalTable: "Shelves",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlantShelves_UserPlants_PlantId",
                        column: x => x.PlantId,
                        principalTable: "UserPlants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlantShelves_ShelfId",
                table: "PlantShelves",
                column: "ShelfId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlantShelves");
        }
    }
}
