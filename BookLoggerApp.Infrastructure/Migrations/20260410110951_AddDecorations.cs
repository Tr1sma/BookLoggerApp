using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace BookLoggerApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDecorations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserDecorations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ShopItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PurchasedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserDecorations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserDecorations_ShopItems_ShopItemId",
                        column: x => x.ShopItemId,
                        principalTable: "ShopItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DecorationShelves",
                columns: table => new
                {
                    DecorationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ShelfId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DecorationShelves", x => new { x.DecorationId, x.ShelfId });
                    table.ForeignKey(
                        name: "FK_DecorationShelves_Shelves_ShelfId",
                        column: x => x.ShelfId,
                        principalTable: "Shelves",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DecorationShelves_UserDecorations_DecorationId",
                        column: x => x.DecorationId,
                        principalTable: "UserDecorations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "ShopItems",
                columns: new[] { "Id", "Cost", "Description", "ImagePath", "IsAvailable", "ItemType", "Name", "PlantSpeciesId", "UnlockLevel" },
                values: new object[,]
                {
                    { new Guid("20000000-0000-0000-0000-000000000001"), 100, "A warm flickering candle — the perfect reading companion.", "images/decorations/candle.svg", true, 2, "Reading Candle", null, 1 },
                    { new Guid("20000000-0000-0000-0000-000000000002"), 120, "A ceramic mug with a book-spine pattern. Always full.", "images/decorations/mug.svg", true, 2, "Cosy Book Mug", null, 1 },
                    { new Guid("20000000-0000-0000-0000-000000000003"), 150, "A hand-carved oak bookend. Your books deserve a proper brace.", "images/decorations/bookend_wood.svg", true, 2, "Wooden Bookend", null, 1 },
                    { new Guid("20000000-0000-0000-0000-000000000004"), 175, "Measure your reading sessions in style.", "images/decorations/hourglass.svg", true, 2, "Brass Hourglass", null, 5 },
                    { new Guid("20000000-0000-0000-0000-000000000005"), 180, "Round brass-rimmed glasses. Any shelf gains +10 distinguished.", "images/decorations/spectacles.svg", true, 2, "Scholar's Spectacles", null, 5 },
                    { new Guid("20000000-0000-0000-0000-000000000006"), 200, "A glass inkwell with a raven feather quill. Timeless.", "images/decorations/inkwell.svg", true, 2, "Inkwell & Quill", null, 10 },
                    { new Guid("20000000-0000-0000-0000-000000000007"), 220, "A ceramic owl. Wise observer of your reading habits.", "images/decorations/owl_figurine.svg", true, 2, "Owl Figurine", null, 10 },
                    { new Guid("20000000-0000-0000-0000-000000000008"), 260, "A vintage brass globe. Explore worlds between the shelves.", "images/decorations/globe.svg", true, 2, "Library Globe", null, 15 },
                    { new Guid("20000000-0000-0000-0000-000000000009"), 280, "Carved marble bookends — heavy, elegant, permanent.", "images/decorations/bookend_marble.svg", true, 2, "Marble Bookend", null, 15 },
                    { new Guid("20000000-0000-0000-0000-000000000010"), 320, "A miniature brass telescope. For reading between the stars.", "images/decorations/telescope.svg", true, 2, "Brass Telescope", null, 20 },
                    { new Guid("20000000-0000-0000-0000-000000000011"), 340, "An enchanted desk lamp that never runs out of oil.", "images/decorations/magic_lamp.svg", true, 2, "Magic Reading Lamp", null, 20 },
                    { new Guid("20000000-0000-0000-0000-000000000012"), 360, "A tiny dragon perched on your shelf. He's read everything.", "images/decorations/dragon_figurine.svg", true, 2, "Dragon Figurine", null, 25 },
                    { new Guid("20000000-0000-0000-0000-000000000013"), 380, "A glowing green flask of unknown purpose. Do not drink.", "images/decorations/alchemy_flask.svg", true, 2, "Alchemy Flask", null, 25 },
                    { new Guid("20000000-0000-0000-0000-000000000014"), 400, "A sealed scroll of immense wisdom. Or a grocery list. Who knows.", "images/decorations/ancient_scroll.svg", true, 2, "Ancient Scroll", null, 25 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_DecorationShelves_ShelfId",
                table: "DecorationShelves",
                column: "ShelfId");

            migrationBuilder.CreateIndex(
                name: "IX_UserDecorations_ShopItemId",
                table: "UserDecorations",
                column: "ShopItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DecorationShelves");

            migrationBuilder.DropTable(
                name: "UserDecorations");

            migrationBuilder.DeleteData(
                table: "ShopItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "ShopItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "ShopItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                table: "ShopItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000004"));

            migrationBuilder.DeleteData(
                table: "ShopItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000005"));

            migrationBuilder.DeleteData(
                table: "ShopItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000006"));

            migrationBuilder.DeleteData(
                table: "ShopItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000007"));

            migrationBuilder.DeleteData(
                table: "ShopItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000008"));

            migrationBuilder.DeleteData(
                table: "ShopItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000009"));

            migrationBuilder.DeleteData(
                table: "ShopItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000010"));

            migrationBuilder.DeleteData(
                table: "ShopItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000011"));

            migrationBuilder.DeleteData(
                table: "ShopItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000012"));

            migrationBuilder.DeleteData(
                table: "ShopItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000013"));

            migrationBuilder.DeleteData(
                table: "ShopItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000014"));
        }
    }
}
