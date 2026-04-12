using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookLoggerApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGenreSpecificRatingCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AtmosphaereRating",
                table: "Books",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmotionaleTiefeRating",
                table: "Books",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HumorRating",
                table: "Books",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InformationsgehaltRating",
                table: "Books",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SpannungRating",
                table: "Books",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AtmosphaereRating",
                table: "Books");

            migrationBuilder.DropColumn(
                name: "EmotionaleTiefeRating",
                table: "Books");

            migrationBuilder.DropColumn(
                name: "HumorRating",
                table: "Books");

            migrationBuilder.DropColumn(
                name: "InformationsgehaltRating",
                table: "Books");

            migrationBuilder.DropColumn(
                name: "SpannungRating",
                table: "Books");
        }
    }
}
