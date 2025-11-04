using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookLoggerApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixStarterSproutUnlockLevel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "PlantSpecies",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000001"),
                column: "UnlockLevel",
                value: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "PlantSpecies",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000001"),
                column: "UnlockLevel",
                value: 5);
        }
    }
}
