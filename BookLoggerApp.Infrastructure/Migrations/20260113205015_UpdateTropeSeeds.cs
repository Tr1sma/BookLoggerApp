using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace BookLoggerApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateTropeSeeds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "PlantSpecies",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000008"),
                column: "UnlockLevel",
                value: 33);

            migrationBuilder.InsertData(
                table: "Tropes",
                columns: new[] { "Id", "GenreId", "Name" },
                values: new object[,]
                {
                    { new Guid("459f13f2-50a9-33b5-ecc9-9be1ac33e38b"), new Guid("00000000-0000-0000-0000-000000000006"), "Age Gap" },
                    { new Guid("aabac8c9-9716-8d1f-f85a-605d6802b65b"), new Guid("00000000-0000-0000-0000-000000000006"), "Forbidden Love" },
                    { new Guid("bcf86d7a-5a84-7610-bfee-55b48021c895"), new Guid("00000000-0000-0000-0000-000000000006"), "Sport Romance" },
                    { new Guid("ff194c52-0e9e-0247-d43b-68eb1803a73d"), new Guid("00000000-0000-0000-0000-000000000009"), "Enemies to Lovers" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Tropes",
                keyColumn: "Id",
                keyValue: new Guid("459f13f2-50a9-33b5-ecc9-9be1ac33e38b"));

            migrationBuilder.DeleteData(
                table: "Tropes",
                keyColumn: "Id",
                keyValue: new Guid("aabac8c9-9716-8d1f-f85a-605d6802b65b"));

            migrationBuilder.DeleteData(
                table: "Tropes",
                keyColumn: "Id",
                keyValue: new Guid("bcf86d7a-5a84-7610-bfee-55b48021c895"));

            migrationBuilder.DeleteData(
                table: "Tropes",
                keyColumn: "Id",
                keyValue: new Guid("ff194c52-0e9e-0247-d43b-68eb1803a73d"));

            migrationBuilder.UpdateData(
                table: "PlantSpecies",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000008"),
                column: "UnlockLevel",
                value: 32);
        }
    }
}
