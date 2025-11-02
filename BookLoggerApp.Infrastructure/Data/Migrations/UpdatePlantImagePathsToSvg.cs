using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookLoggerApp.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Migration to update plant image paths from .png to .svg
    /// </summary>
    public partial class UpdatePlantImagePathsToSvg : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
