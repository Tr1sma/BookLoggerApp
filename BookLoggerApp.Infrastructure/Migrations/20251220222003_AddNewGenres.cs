using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace BookLoggerApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNewGenres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Genres",
                columns: new[] { "Id", "ColorHex", "Description", "Icon", "Name" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000009"), "#880e4f", null, "🖤", "Dark Romance" },
                    { new Guid("00000000-0000-0000-0000-000000000010"), "#34495e", null, "🔦", "Krimi" },
                    { new Guid("00000000-0000-0000-0000-000000000011"), "#f1c40f", null, "🎭", "Comedy" },
                    { new Guid("00000000-0000-0000-0000-000000000012"), "#c0392b", null, "😱", "Thriller" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Genres",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000009"));

            migrationBuilder.DeleteData(
                table: "Genres",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000010"));

            migrationBuilder.DeleteData(
                table: "Genres",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000011"));

            migrationBuilder.DeleteData(
                table: "Genres",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000012"));
        }
    }
}
