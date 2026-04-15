using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace BookLoggerApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLateGameItemsAndAbilities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastStreakSaveAt",
                table: "UserPlants",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSingleton",
                table: "ShopItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SpecialAbilityKey",
                table: "ShopItems",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SpecialAbilityKey",
                table: "PlantSpecies",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "PlantSpecies",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000001"),
                column: "SpecialAbilityKey",
                value: null);

            migrationBuilder.UpdateData(
                table: "PlantSpecies",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000002"),
                column: "SpecialAbilityKey",
                value: null);

            migrationBuilder.UpdateData(
                table: "PlantSpecies",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000003"),
                column: "SpecialAbilityKey",
                value: null);

            migrationBuilder.UpdateData(
                table: "PlantSpecies",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000004"),
                column: "SpecialAbilityKey",
                value: null);

            migrationBuilder.UpdateData(
                table: "PlantSpecies",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000005"),
                column: "SpecialAbilityKey",
                value: null);

            migrationBuilder.UpdateData(
                table: "PlantSpecies",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000006"),
                column: "SpecialAbilityKey",
                value: null);

            migrationBuilder.UpdateData(
                table: "PlantSpecies",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000007"),
                column: "SpecialAbilityKey",
                value: null);

            migrationBuilder.UpdateData(
                table: "PlantSpecies",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000008"),
                column: "SpecialAbilityKey",
                value: null);

            migrationBuilder.InsertData(
                table: "PlantSpecies",
                columns: new[] { "Id", "BaseCost", "Description", "GrowthRate", "ImagePath", "IsAvailable", "MaxLevel", "Name", "SpecialAbilityKey", "UnlockLevel", "WaterIntervalDays", "XpBoostPercentage" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000009"), 20000, "Ein uralter Chronikbaum, dessen Blätter wie Pergament aus vergangenen Zeiten wirken. Wenn dein Lese-Streak zu brechen droht, hält er die Geschichte deiner Reise fest.", 0.40000000000000002, "images/plants/chronicle_tree.svg", true, 40, "Chronikbaum", "streak_guardian", 45, 21, 0.30m },
                    { new Guid("10000000-0000-0000-0000-00000000000a"), 80000, "Ein heiliger Bonsai, dessen goldene Blätter mit der Weisheit unzähliger Bücher glühen. Solange er wacht, stirbt in deinem Garten keine Pflanze — und er selbst erhebt sich immer wieder aus seiner Asche.", 0.25, "images/plants/eternal_phoenix_bonsai.svg", true, 50, "Ewiger Phönix-Bonsai", "eternal_phoenix", 57, 30, 0.50m }
                });

            migrationBuilder.UpdateData(
                table: "ShopItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000001"),
                columns: new[] { "IsSingleton", "SpecialAbilityKey" },
                values: new object[] { false, null });

            migrationBuilder.UpdateData(
                table: "ShopItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000002"),
                columns: new[] { "IsSingleton", "SpecialAbilityKey" },
                values: new object[] { false, null });

            migrationBuilder.UpdateData(
                table: "ShopItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000003"),
                columns: new[] { "IsSingleton", "SpecialAbilityKey" },
                values: new object[] { false, null });

            migrationBuilder.UpdateData(
                table: "ShopItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000004"),
                columns: new[] { "IsSingleton", "SpecialAbilityKey" },
                values: new object[] { false, null });

            migrationBuilder.UpdateData(
                table: "ShopItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000005"),
                columns: new[] { "IsSingleton", "SpecialAbilityKey" },
                values: new object[] { false, null });

            migrationBuilder.UpdateData(
                table: "ShopItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000006"),
                columns: new[] { "IsSingleton", "SpecialAbilityKey" },
                values: new object[] { false, null });

            migrationBuilder.UpdateData(
                table: "ShopItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000007"),
                columns: new[] { "IsSingleton", "SpecialAbilityKey" },
                values: new object[] { false, null });

            migrationBuilder.UpdateData(
                table: "ShopItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000008"),
                columns: new[] { "IsSingleton", "SpecialAbilityKey" },
                values: new object[] { false, null });

            migrationBuilder.UpdateData(
                table: "ShopItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000009"),
                columns: new[] { "IsSingleton", "SpecialAbilityKey" },
                values: new object[] { false, null });

            migrationBuilder.UpdateData(
                table: "ShopItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000010"),
                columns: new[] { "IsSingleton", "SpecialAbilityKey" },
                values: new object[] { false, null });

            migrationBuilder.UpdateData(
                table: "ShopItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000011"),
                columns: new[] { "IsSingleton", "SpecialAbilityKey" },
                values: new object[] { false, null });

            migrationBuilder.UpdateData(
                table: "ShopItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000012"),
                columns: new[] { "IsSingleton", "SpecialAbilityKey" },
                values: new object[] { false, null });

            migrationBuilder.UpdateData(
                table: "ShopItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000013"),
                columns: new[] { "IsSingleton", "SpecialAbilityKey" },
                values: new object[] { false, null });

            migrationBuilder.UpdateData(
                table: "ShopItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000014"),
                columns: new[] { "IsSingleton", "SpecialAbilityKey" },
                values: new object[] { false, null });

            migrationBuilder.InsertData(
                table: "ShopItems",
                columns: new[] { "Id", "Cost", "Description", "ImagePath", "IsAvailable", "IsSingleton", "ItemType", "Name", "PlantSpeciesId", "SlotWidth", "SpecialAbilityKey", "UnlockLevel" },
                values: new object[] { new Guid("20000000-0000-0000-0000-00000000000f"), 200000, "Das Herz der Bibliothek — ein pulsierendes Relikt in warmem Beige. Es ist die ultimative Belohnung für jede Leserin, die niemals aufgibt. Seine Magie durchdringt jeden Aspekt deiner Reise.", "images/decorations/heart_of_stories.svg", true, true, 2, "Herz der Geschichten", null, 2, "story_heart", 70 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "PlantSpecies",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000009"));

            migrationBuilder.DeleteData(
                table: "PlantSpecies",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-00000000000a"));

            migrationBuilder.DeleteData(
                table: "ShopItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-00000000000f"));

            migrationBuilder.DropColumn(
                name: "LastStreakSaveAt",
                table: "UserPlants");

            migrationBuilder.DropColumn(
                name: "IsSingleton",
                table: "ShopItems");

            migrationBuilder.DropColumn(
                name: "SpecialAbilityKey",
                table: "ShopItems");

            migrationBuilder.DropColumn(
                name: "SpecialAbilityKey",
                table: "PlantSpecies");
        }
    }
}
