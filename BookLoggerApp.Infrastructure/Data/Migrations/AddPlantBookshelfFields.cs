using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookLoggerApp.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Migration to add bookshelf-related fields to UserPlant table
    /// </summary>
    public partial class AddPlantBookshelfFields : Migration
    {
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
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BookshelfPosition",
                table: "UserPlants");

            migrationBuilder.DropColumn(
                name: "IsInBookshelf",
                table: "UserPlants");
        }
    }
}
