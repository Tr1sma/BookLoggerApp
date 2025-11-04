using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace BookLoggerApp.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Theme = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Language = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    NotificationsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    ReadingRemindersEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    ReminderTime = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    AutoBackupEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastBackupDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TelemetryEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    UserLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalXp = table.Column<int>(type: "INTEGER", nullable: false),
                    Coins = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Books",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Author = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    ISBN = table.Column<string>(type: "TEXT", maxLength: 13, nullable: true),
                    Publisher = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    PublicationYear = table.Column<int>(type: "INTEGER", nullable: true),
                    Language = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    PageCount = table.Column<int>(type: "INTEGER", nullable: true),
                    CurrentPage = table.Column<int>(type: "INTEGER", nullable: false),
                    CoverImagePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Rating = table.Column<int>(type: "INTEGER", nullable: true),
                    DateAdded = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DateStarted = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DateCompleted = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Books", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Genres",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Icon = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    ColorHex = table.Column<string>(type: "TEXT", maxLength: 7, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Genres", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlantSpecies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    ImagePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    MaxLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    WaterIntervalDays = table.Column<int>(type: "INTEGER", nullable: false),
                    GrowthRate = table.Column<double>(type: "REAL", nullable: false),
                    BaseCost = table.Column<int>(type: "INTEGER", nullable: false),
                    IsAvailable = table.Column<bool>(type: "INTEGER", nullable: false),
                    UnlockLevel = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlantSpecies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReadingGoals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Target = table.Column<int>(type: "INTEGER", nullable: false),
                    Current = table.Column<int>(type: "INTEGER", nullable: false),
                    StartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsCompleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadingGoals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Annotations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BookId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 5000, nullable: false),
                    PageNumber = table.Column<int>(type: "INTEGER", nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ColorHex = table.Column<string>(type: "TEXT", maxLength: 7, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Annotations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Annotations_Books_BookId",
                        column: x => x.BookId,
                        principalTable: "Books",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Quotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BookId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Text = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    PageNumber = table.Column<int>(type: "INTEGER", nullable: true),
                    Context = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsFavorite = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Quotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Quotes_Books_BookId",
                        column: x => x.BookId,
                        principalTable: "Books",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReadingSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BookId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Minutes = table.Column<int>(type: "INTEGER", nullable: false),
                    PagesRead = table.Column<int>(type: "INTEGER", nullable: true),
                    StartPage = table.Column<int>(type: "INTEGER", nullable: true),
                    EndPage = table.Column<int>(type: "INTEGER", nullable: true),
                    XpEarned = table.Column<int>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadingSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReadingSessions_Books_BookId",
                        column: x => x.BookId,
                        principalTable: "Books",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BookGenres",
                columns: table => new
                {
                    BookId = table.Column<Guid>(type: "TEXT", nullable: false),
                    GenreId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookGenres", x => new { x.BookId, x.GenreId });
                    table.ForeignKey(
                        name: "FK_BookGenres_Books_BookId",
                        column: x => x.BookId,
                        principalTable: "Books",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BookGenres_Genres_GenreId",
                        column: x => x.GenreId,
                        principalTable: "Genres",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ShopItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ItemType = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Cost = table.Column<int>(type: "INTEGER", nullable: false),
                    ImagePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    IsAvailable = table.Column<bool>(type: "INTEGER", nullable: false),
                    UnlockLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    PlantSpeciesId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShopItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShopItems_PlantSpecies_PlantSpeciesId",
                        column: x => x.PlantSpeciesId,
                        principalTable: "PlantSpecies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "UserPlants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SpeciesId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CurrentLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    Experience = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    LastWatered = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PlantedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPlants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserPlants_PlantSpecies_SpeciesId",
                        column: x => x.SpeciesId,
                        principalTable: "PlantSpecies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "AppSettings",
                columns: new[] { "Id", "AutoBackupEnabled", "Coins", "CreatedAt", "Language", "LastBackupDate", "NotificationsEnabled", "ReadingRemindersEnabled", "ReminderTime", "TelemetryEnabled", "Theme", "TotalXp", "UpdatedAt", "UserLevel" },
                values: new object[] { new Guid("99999999-0000-0000-0000-000000000001"), false, 100, new DateTime(2025, 10, 30, 17, 9, 7, 340, DateTimeKind.Utc).AddTicks(8489), "en", null, false, false, null, false, "Light", 0, null, 1 });

            migrationBuilder.InsertData(
                table: "Genres",
                columns: new[] { "Id", "ColorHex", "Description", "Icon", "Name" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000001"), "#3498db", null, "📖", "Fiction" },
                    { new Guid("00000000-0000-0000-0000-000000000002"), "#2ecc71", null, "📚", "Non-Fiction" },
                    { new Guid("00000000-0000-0000-0000-000000000003"), "#9b59b6", null, "🧙", "Fantasy" },
                    { new Guid("00000000-0000-0000-0000-000000000004"), "#1abc9c", null, "🚀", "Science Fiction" },
                    { new Guid("00000000-0000-0000-0000-000000000005"), "#e74c3c", null, "🔍", "Mystery" },
                    { new Guid("00000000-0000-0000-0000-000000000006"), "#e91e63", null, "💕", "Romance" },
                    { new Guid("00000000-0000-0000-0000-000000000007"), "#f39c12", null, "👤", "Biography" },
                    { new Guid("00000000-0000-0000-0000-000000000008"), "#95a5a6", null, "📜", "History" }
                });

            migrationBuilder.InsertData(
                table: "PlantSpecies",
                columns: new[] { "Id", "BaseCost", "Description", "GrowthRate", "ImagePath", "IsAvailable", "MaxLevel", "Name", "UnlockLevel", "WaterIntervalDays" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000001"), 0, "A simple plant for beginners. Grows quickly!", 1.0, "/images/plants/starter_sprout.png", true, 5, "Starter Sprout", 1, 2 },
                    { new Guid("10000000-0000-0000-0000-000000000002"), 500, "A lush fern that grows with every page.", 1.2, "/images/plants/bookworm_fern.png", true, 10, "Bookworm Fern", 5, 3 },
                    { new Guid("10000000-0000-0000-0000-000000000003"), 1000, "Low maintenance, high rewards.", 0.80000000000000004, "/images/plants/reading_cactus.png", true, 15, "Reading Cactus", 10, 7 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Annotations_BookId",
                table: "Annotations",
                column: "BookId");

            migrationBuilder.CreateIndex(
                name: "IX_Annotations_PageNumber",
                table: "Annotations",
                column: "PageNumber");

            migrationBuilder.CreateIndex(
                name: "IX_BookGenres_GenreId",
                table: "BookGenres",
                column: "GenreId");

            migrationBuilder.CreateIndex(
                name: "IX_Books_DateAdded",
                table: "Books",
                column: "DateAdded");

            migrationBuilder.CreateIndex(
                name: "IX_Books_ISBN",
                table: "Books",
                column: "ISBN");

            migrationBuilder.CreateIndex(
                name: "IX_Books_Status",
                table: "Books",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Books_Title",
                table: "Books",
                column: "Title");

            migrationBuilder.CreateIndex(
                name: "IX_Genres_Name",
                table: "Genres",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlantSpecies_Name",
                table: "PlantSpecies",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_PlantSpecies_UnlockLevel",
                table: "PlantSpecies",
                column: "UnlockLevel");

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_BookId",
                table: "Quotes",
                column: "BookId");

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_IsFavorite",
                table: "Quotes",
                column: "IsFavorite");

            migrationBuilder.CreateIndex(
                name: "IX_ReadingGoals_EndDate",
                table: "ReadingGoals",
                column: "EndDate");

            migrationBuilder.CreateIndex(
                name: "IX_ReadingGoals_IsCompleted",
                table: "ReadingGoals",
                column: "IsCompleted");

            migrationBuilder.CreateIndex(
                name: "IX_ReadingGoals_StartDate",
                table: "ReadingGoals",
                column: "StartDate");

            migrationBuilder.CreateIndex(
                name: "IX_ReadingSessions_BookId",
                table: "ReadingSessions",
                column: "BookId");

            migrationBuilder.CreateIndex(
                name: "IX_ReadingSessions_StartedAt",
                table: "ReadingSessions",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ShopItems_IsAvailable",
                table: "ShopItems",
                column: "IsAvailable");

            migrationBuilder.CreateIndex(
                name: "IX_ShopItems_ItemType",
                table: "ShopItems",
                column: "ItemType");

            migrationBuilder.CreateIndex(
                name: "IX_ShopItems_PlantSpeciesId",
                table: "ShopItems",
                column: "PlantSpeciesId");

            migrationBuilder.CreateIndex(
                name: "IX_ShopItems_UnlockLevel",
                table: "ShopItems",
                column: "UnlockLevel");

            migrationBuilder.CreateIndex(
                name: "IX_UserPlants_IsActive",
                table: "UserPlants",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_UserPlants_SpeciesId",
                table: "UserPlants",
                column: "SpeciesId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Annotations");

            migrationBuilder.DropTable(
                name: "AppSettings");

            migrationBuilder.DropTable(
                name: "BookGenres");

            migrationBuilder.DropTable(
                name: "Quotes");

            migrationBuilder.DropTable(
                name: "ReadingGoals");

            migrationBuilder.DropTable(
                name: "ReadingSessions");

            migrationBuilder.DropTable(
                name: "ShopItems");

            migrationBuilder.DropTable(
                name: "UserPlants");

            migrationBuilder.DropTable(
                name: "Genres");

            migrationBuilder.DropTable(
                name: "Books");

            migrationBuilder.DropTable(
                name: "PlantSpecies");
        }
    }
}
