using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookLoggerApp.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixPlantImagePaths : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "PlantSpecies",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000002"));

            migrationBuilder.UpdateData(
                table: "Genres",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "Icon",
                value: "📖");

            migrationBuilder.UpdateData(
                table: "Genres",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "Icon",
                value: "📚");

            migrationBuilder.UpdateData(
                table: "Genres",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "Icon",
                value: "🧙");

            migrationBuilder.UpdateData(
                table: "Genres",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "Icon",
                value: "🚀");

            migrationBuilder.UpdateData(
                table: "Genres",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"),
                column: "Icon",
                value: "🔍");

            migrationBuilder.UpdateData(
                table: "Genres",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000006"),
                column: "Icon",
                value: "💕");

            migrationBuilder.UpdateData(
                table: "Genres",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000007"),
                column: "Icon",
                value: "👤");

            migrationBuilder.UpdateData(
                table: "Genres",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000008"),
                column: "Icon",
                value: "📜");

            migrationBuilder.UpdateData(
                table: "PlantSpecies",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000001"),
                columns: new[] { "BaseCost", "GrowthRate", "ImagePath", "MaxLevel", "UnlockLevel", "WaterIntervalDays" },
                values: new object[] { 500, 1.2, "images/plants/starter_sprout.svg", 10, 5, 3 });

            migrationBuilder.UpdateData(
                table: "PlantSpecies",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000003"),
                column: "ImagePath",
                value: "images/plants/reading_cactus.svg");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Genres",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "Icon",
                value: "??");

            migrationBuilder.UpdateData(
                table: "Genres",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "Icon",
                value: "??");

            migrationBuilder.UpdateData(
                table: "Genres",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "Icon",
                value: "??");

            migrationBuilder.UpdateData(
                table: "Genres",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "Icon",
                value: "??");

            migrationBuilder.UpdateData(
                table: "Genres",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"),
                column: "Icon",
                value: "??");

            migrationBuilder.UpdateData(
                table: "Genres",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000006"),
                column: "Icon",
                value: "??");

            migrationBuilder.UpdateData(
                table: "Genres",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000007"),
                column: "Icon",
                value: "??");

            migrationBuilder.UpdateData(
                table: "Genres",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000008"),
                column: "Icon",
                value: "??");

            migrationBuilder.UpdateData(
                table: "PlantSpecies",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000001"),
                columns: new[] { "BaseCost", "GrowthRate", "ImagePath", "MaxLevel", "UnlockLevel", "WaterIntervalDays" },
                values: new object[] { 0, 1.0, "/images/plants/starter_sprout.svg", 5, 1, 2 });

            migrationBuilder.UpdateData(
                table: "PlantSpecies",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000003"),
                column: "ImagePath",
                value: "/images/plants/reading_cactus.svg");

            migrationBuilder.InsertData(
                table: "PlantSpecies",
                columns: new[] { "Id", "BaseCost", "Description", "GrowthRate", "ImagePath", "IsAvailable", "MaxLevel", "Name", "UnlockLevel", "WaterIntervalDays" },
                values: new object[] { new Guid("10000000-0000-0000-0000-000000000002"), 500, "A lush fern that grows with every page.", 1.2, "/images/plants/bookworm_fern.svg", true, 10, "Bookworm Fern", 5, 3 });
        }
    }
}
