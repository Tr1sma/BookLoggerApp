using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookLoggerApp.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlantBookshelfFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BookshelfPosition",
                table: "UserPlants",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsInBookshelf",
                table: "UserPlants",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            // Update PlantSpecies image paths to SVG
            migrationBuilder.UpdateData(
                table: "PlantSpecies",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000001"),
                column: "ImagePath",
                value: "/images/plants/starter_sprout.svg");

            migrationBuilder.UpdateData(
                table: "PlantSpecies",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000002"),
                column: "ImagePath",
                value: "/images/plants/bookworm_fern.svg");

            migrationBuilder.UpdateData(
                table: "PlantSpecies",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000003"),
                column: "ImagePath",
                value: "/images/plants/reading_cactus.svg");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BookshelfPosition",
                table: "UserPlants");

            migrationBuilder.DropColumn(
                name: "IsInBookshelf",
                table: "UserPlants");

            // Revert PlantSpecies image paths to PNG
            migrationBuilder.UpdateData(
                table: "PlantSpecies",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000001"),
                column: "ImagePath",
                value: "/images/plants/starter_sprout.png");

            migrationBuilder.UpdateData(
                table: "PlantSpecies",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000002"),
                column: "ImagePath",
                value: "/images/plants/bookworm_fern.png");

            migrationBuilder.UpdateData(
                table: "PlantSpecies",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000003"),
                column: "ImagePath",
                value: "/images/plants/reading_cactus.png");
        }
    }
}
