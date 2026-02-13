using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookLoggerApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWishlistFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WishlistInfos",
                columns: table => new
                {
                    BookId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    RecommendedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    WishlistNotes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    DateAddedToWishlist = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WishlistInfos", x => x.BookId);
                    table.ForeignKey(
                        name: "FK_WishlistInfos_Books_BookId",
                        column: x => x.BookId,
                        principalTable: "Books",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WishlistInfos_DateAddedToWishlist",
                table: "WishlistInfos",
                column: "DateAddedToWishlist");

            migrationBuilder.CreateIndex(
                name: "IX_WishlistInfos_Priority",
                table: "WishlistInfos",
                column: "Priority");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WishlistInfos");
        }
    }
}
