using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace BookLoggerApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTropes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tropes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    GenreId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tropes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tropes_Genres_GenreId",
                        column: x => x.GenreId,
                        principalTable: "Genres",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BookTropes",
                columns: table => new
                {
                    BookId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TropeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookTropes", x => new { x.BookId, x.TropeId });
                    table.ForeignKey(
                        name: "FK_BookTropes_Books_BookId",
                        column: x => x.BookId,
                        principalTable: "Books",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BookTropes_Tropes_TropeId",
                        column: x => x.TropeId,
                        principalTable: "Tropes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Tropes",
                columns: new[] { "Id", "GenreId", "Name" },
                values: new object[,]
                {
                    { new Guid("06232a7b-a3cf-4cb6-e391-1c1416bb6393"), new Guid("00000000-0000-0000-0000-000000000003"), "The Chosen One" },
                    { new Guid("1b593009-47a6-0f35-9432-2b5547da1db8"), new Guid("00000000-0000-0000-0000-000000000009"), "Captive/Captor" },
                    { new Guid("1c63df2d-06cb-696e-4f4c-fa52e2184124"), new Guid("00000000-0000-0000-0000-000000000012"), "Plot Twist" },
                    { new Guid("1dc08b0f-f66a-1383-89d1-7c0ee86cf71b"), new Guid("00000000-0000-0000-0000-000000000004"), "Space Opera" },
                    { new Guid("208f5f1d-7ba3-db55-32cf-de635ae1268a"), new Guid("00000000-0000-0000-0000-000000000009"), "Morally Grey MC" },
                    { new Guid("225b2346-0e7f-380f-b2c6-b753885ad271"), new Guid("00000000-0000-0000-0000-000000000006"), "Slow Burn" },
                    { new Guid("25468a69-fe84-d76c-7e38-6f161136c3f4"), new Guid("00000000-0000-0000-0000-000000000003"), "Magic System" },
                    { new Guid("27ecfac0-c48a-9dfe-0784-24f0efedd5b7"), new Guid("00000000-0000-0000-0000-000000000004"), "Artificial Intelligence" },
                    { new Guid("2803e766-c646-4a2a-ec0d-52f02f76350e"), new Guid("00000000-0000-0000-0000-000000000012"), "Revenge" },
                    { new Guid("3e760546-f353-89c0-95b6-b175c95fe25b"), new Guid("00000000-0000-0000-0000-000000000005"), "Whodunit" },
                    { new Guid("44983da9-aac9-95e1-06ba-af767bfb87b3"), new Guid("00000000-0000-0000-0000-000000000005"), "Unreliable Narrator" },
                    { new Guid("49552cf2-6771-54a0-f1ad-5084b04f0458"), new Guid("00000000-0000-0000-0000-000000000004"), "Time Travel" },
                    { new Guid("4e6a5bd9-369a-6847-d3f5-3f3f984f08ba"), new Guid("00000000-0000-0000-0000-000000000003"), "Quest/Journey" },
                    { new Guid("5485fd65-af73-56a2-a85b-c3c1969a71dd"), new Guid("00000000-0000-0000-0000-000000000003"), "Prophecy" },
                    { new Guid("5b4c3447-46e5-5da3-3661-37d4a32e3cd1"), new Guid("00000000-0000-0000-0000-000000000009"), "Mafia" },
                    { new Guid("5dbf924c-bed7-4c27-e962-92031de0f005"), new Guid("00000000-0000-0000-0000-000000000003"), "Found Family" },
                    { new Guid("5ff7530c-5283-457d-2f14-ca2d5ed3c3c6"), new Guid("00000000-0000-0000-0000-000000000005"), "Red Herring" },
                    { new Guid("6cd9d130-dedb-5c43-f14a-1804bb24460e"), new Guid("00000000-0000-0000-0000-000000000003"), "Reluctant Hero" },
                    { new Guid("6eed7fd4-90aa-d6f1-5582-edf9bfdfdd13"), new Guid("00000000-0000-0000-0000-000000000009"), "Bully Romance" },
                    { new Guid("70491b04-a86a-7833-0051-f474154614c6"), new Guid("00000000-0000-0000-0000-000000000012"), "Spy/Espionage" },
                    { new Guid("754c9607-0f57-29ab-2a2e-14dc8ee54446"), new Guid("00000000-0000-0000-0000-000000000012"), "Serial Killer" },
                    { new Guid("7f2d4be6-3796-6003-6de7-2dae984815ca"), new Guid("00000000-0000-0000-0000-000000000004"), "Dystopia" },
                    { new Guid("997aff21-56c0-03b3-b009-443ada473520"), new Guid("00000000-0000-0000-0000-000000000006"), "Love Triangle" },
                    { new Guid("9d1d8d60-0f2b-d7ac-0b34-dc0c2588c438"), new Guid("00000000-0000-0000-0000-000000000006"), "Enemies to Lovers" },
                    { new Guid("a09ba3ec-57a1-ffee-185d-76e297cba9ac"), new Guid("00000000-0000-0000-0000-000000000012"), "Psychological" },
                    { new Guid("a5ab1333-7ced-651a-e6af-b7443ce7d9a2"), new Guid("00000000-0000-0000-0000-000000000004"), "Cyberpunk" },
                    { new Guid("a6c46a98-1a8d-5db4-8fb2-ad5ac57b7fd0"), new Guid("00000000-0000-0000-0000-000000000006"), "Friends to Lovers" },
                    { new Guid("a70c1aed-7321-998c-b263-e829a9a9553c"), new Guid("00000000-0000-0000-0000-000000000009"), "Obsessive Love" },
                    { new Guid("a911fd2e-3163-7903-8c95-e20c909490d5"), new Guid("00000000-0000-0000-0000-000000000005"), "Cozy Mystery" },
                    { new Guid("ac7f4770-da95-0564-8002-05c3f5725523"), new Guid("00000000-0000-0000-0000-000000000006"), "Second Chance" },
                    { new Guid("c576b382-257b-8b1a-f772-42747fa6e7f7"), new Guid("00000000-0000-0000-0000-000000000006"), "Grumpy x Sunshine" },
                    { new Guid("cd935690-3e5f-7a79-1af1-75ce4ee9acbf"), new Guid("00000000-0000-0000-0000-000000000006"), "Fake Dating" },
                    { new Guid("d534ca94-2dc7-b9c2-d889-5e8fccc24c53"), new Guid("00000000-0000-0000-0000-000000000005"), "Locked Room" },
                    { new Guid("db485dd7-0618-ef59-fa62-68596a71a0e7"), new Guid("00000000-0000-0000-0000-000000000004"), "Post-Apocalyptic" },
                    { new Guid("de64dbd3-f111-89d2-867b-3b947f4b0591"), new Guid("00000000-0000-0000-0000-000000000006"), "One Bed" },
                    { new Guid("e2dc894f-051b-7f30-7307-dd715f97ac41"), new Guid("00000000-0000-0000-0000-000000000005"), "Noir" },
                    { new Guid("f0eae802-8660-d175-9124-fc7e06ce35e2"), new Guid("00000000-0000-0000-0000-000000000003"), "Dark Lord" },
                    { new Guid("f2cab3ff-b942-e67d-705c-a0a023ee4c4b"), new Guid("00000000-0000-0000-0000-000000000004"), "First Contact" },
                    { new Guid("f38f69a8-8bee-cf86-f486-8607094b5d87"), new Guid("00000000-0000-0000-0000-000000000003"), "Magical Academy" },
                    { new Guid("f9c0966b-71b4-966b-6814-2aa3c01c75ff"), new Guid("00000000-0000-0000-0000-000000000009"), "Stalker" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_BookTropes_TropeId",
                table: "BookTropes",
                column: "TropeId");

            migrationBuilder.CreateIndex(
                name: "IX_Tropes_GenreId",
                table: "Tropes",
                column: "GenreId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookTropes");

            migrationBuilder.DropTable(
                name: "Tropes");
        }
    }
}
