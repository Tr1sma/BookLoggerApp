using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace BookLoggerApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNewPlants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "PlantSpecies",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000002"),
                column: "UnlockLevel",
                value: 8);

            migrationBuilder.UpdateData(
                table: "PlantSpecies",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000003"),
                column: "UnlockLevel",
                value: 21);

            migrationBuilder.InsertData(
                table: "PlantSpecies",
                columns: new[] { "Id", "BaseCost", "Description", "GrowthRate", "ImagePath", "IsAvailable", "MaxLevel", "Name", "UnlockLevel", "WaterIntervalDays", "XpBoostPercentage" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000004"), 600, "A growing seedling nurtured by stories.", 1.1000000000000001, "images/plants/story_seedling.svg", true, 11, "Story Seedling", 3, 4, 0.06m },
                    { new Guid("10000000-0000-0000-0000-000000000005"), 850, "A beautiful lily that blooms with every chapter.", 0.90000000000000002, "images/plants/literary_lily.svg", true, 14, "Literary Lily", 14, 5, 0.09m },
                    { new Guid("10000000-0000-0000-0000-000000000006"), 1500, "A wise tree that stands the test of time.", 0.69999999999999996, "images/plants/wisdom_willow.svg", true, 18, "Wisdom Willow", 28, 8, 0.12m },
                    { new Guid("10000000-0000-0000-0000-000000000007"), 2500, "An ancient bonsai radiating knowledge.", 0.59999999999999998, "images/plants/ancient_bonsai.svg", true, 20, "Ancient Knowledge Bonsai", 31, 10, 0.15m },
                    { new Guid("10000000-0000-0000-0000-000000000008"), 5000, "A legendary tree with leaves like parchment.", 0.5, "images/plants/mystic_tome_tree.svg", true, 25, "Mystic Tome Tree", 32, 14, 0.20m }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "PlantSpecies",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000004"));

            migrationBuilder.DeleteData(
                table: "PlantSpecies",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000005"));

            migrationBuilder.DeleteData(
                table: "PlantSpecies",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000006"));

            migrationBuilder.DeleteData(
                table: "PlantSpecies",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000007"));

            migrationBuilder.DeleteData(
                table: "PlantSpecies",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000008"));

            migrationBuilder.UpdateData(
                table: "PlantSpecies",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000002"),
                column: "UnlockLevel",
                value: 10);

            migrationBuilder.UpdateData(
                table: "PlantSpecies",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000003"),
                column: "UnlockLevel",
                value: 20);
        }
    }
}
