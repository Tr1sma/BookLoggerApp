using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookLoggerApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiCategoryRatings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CharactersRating",
                table: "Books",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OverallRating",
                table: "Books",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PacingRating",
                table: "Books",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PlotRating",
                table: "Books",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SpiceLevelRating",
                table: "Books",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WorldBuildingRating",
                table: "Books",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WritingStyleRating",
                table: "Books",
                type: "INTEGER",
                nullable: true);

            // Migrate existing Rating values to OverallRating
            migrationBuilder.Sql(
                @"UPDATE Books
                  SET OverallRating = Rating
                  WHERE Rating IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CharactersRating",
                table: "Books");

            migrationBuilder.DropColumn(
                name: "OverallRating",
                table: "Books");

            migrationBuilder.DropColumn(
                name: "PacingRating",
                table: "Books");

            migrationBuilder.DropColumn(
                name: "PlotRating",
                table: "Books");

            migrationBuilder.DropColumn(
                name: "SpiceLevelRating",
                table: "Books");

            migrationBuilder.DropColumn(
                name: "WorldBuildingRating",
                table: "Books");

            migrationBuilder.DropColumn(
                name: "WritingStyleRating",
                table: "Books");
        }
    }
}
