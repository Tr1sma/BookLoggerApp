using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookLoggerApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBookwormFern : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "PlantSpecies",
                columns: new[] { "Id", "BaseCost", "Description", "GrowthRate", "ImagePath", "IsAvailable", "MaxLevel", "Name", "UnlockLevel", "WaterIntervalDays" },
                values: new object[] { new Guid("10000000-0000-0000-0000-000000000002"), 750, "A lush fern for dedicated readers.", 1.0, "images/plants/bookworm_fern.svg", true, 12, "Bookworm Fern", 5, 4 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "PlantSpecies",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000002"));
        }
    }
}
