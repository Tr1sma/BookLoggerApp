using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookLoggerApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPremiumSubscriptionSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsHiddenByEntitlement",
                table: "UserPlants",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsHiddenByEntitlement",
                table: "UserDecorations",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsFreeTier",
                table: "ShopItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsUltimateTier",
                table: "ShopItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsHiddenByEntitlement",
                table: "Shelves",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsFreeTier",
                table: "PlantSpecies",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPrestigeTier",
                table: "PlantSpecies",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "CurrentTier",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "EntitlementExpiresAt",
                table: "AppSettings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UserEntitlements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Tier = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    BillingPeriod = table.Column<int>(type: "INTEGER", nullable: true),
                    ProductId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    PurchaseToken = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    OrderId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    PurchasedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastVerifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AutoRenewing = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    InGracePeriod = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    IsInIntroductoryPrice = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    IsFamilyShared = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    LapseReason = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    LapsedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PromoCodeRedeemed = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    PromoExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserEntitlements", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "AppSettings",
                keyColumn: "Id",
                keyValue: new Guid("99999999-0000-0000-0000-000000000001"),
                column: "EntitlementExpiresAt",
                value: null);

            migrationBuilder.UpdateData(
                table: "PlantSpecies",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000001"),
                columns: new[] { "IsFreeTier", "IsPrestigeTier" },
                values: new object[] { true, false });

            migrationBuilder.UpdateData(
                table: "PlantSpecies",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000002"),
                columns: new[] { "IsFreeTier", "IsPrestigeTier" },
                values: new object[] { true, false });

            migrationBuilder.UpdateData(
                table: "PlantSpecies",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000003"),
                columns: new[] { "IsFreeTier", "IsPrestigeTier" },
                values: new object[] { true, false });

            migrationBuilder.UpdateData(
                table: "PlantSpecies",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000004"),
                columns: new[] { "IsFreeTier", "IsPrestigeTier" },
                values: new object[] { true, false });

            migrationBuilder.UpdateData(
                table: "PlantSpecies",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000005"),
                columns: new[] { "IsFreeTier", "IsPrestigeTier" },
                values: new object[] { false, false });

            migrationBuilder.UpdateData(
                table: "PlantSpecies",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000006"),
                columns: new[] { "IsFreeTier", "IsPrestigeTier" },
                values: new object[] { false, false });

            migrationBuilder.UpdateData(
                table: "PlantSpecies",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000007"),
                columns: new[] { "IsFreeTier", "IsPrestigeTier" },
                values: new object[] { false, false });

            migrationBuilder.UpdateData(
                table: "PlantSpecies",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000008"),
                columns: new[] { "IsFreeTier", "IsPrestigeTier" },
                values: new object[] { false, false });

            migrationBuilder.UpdateData(
                table: "PlantSpecies",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000009"),
                columns: new[] { "IsFreeTier", "IsPrestigeTier" },
                values: new object[] { false, true });

            migrationBuilder.UpdateData(
                table: "PlantSpecies",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-00000000000a"),
                columns: new[] { "IsFreeTier", "IsPrestigeTier" },
                values: new object[] { false, true });

            migrationBuilder.UpdateData(
                table: "ShopItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000001"),
                columns: new[] { "IsFreeTier", "IsUltimateTier" },
                values: new object[] { true, false });

            migrationBuilder.UpdateData(
                table: "ShopItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000002"),
                columns: new[] { "IsFreeTier", "IsUltimateTier" },
                values: new object[] { true, false });

            migrationBuilder.UpdateData(
                table: "ShopItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000003"),
                columns: new[] { "IsFreeTier", "IsUltimateTier" },
                values: new object[] { false, false });

            migrationBuilder.UpdateData(
                table: "ShopItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000004"),
                columns: new[] { "IsFreeTier", "IsUltimateTier" },
                values: new object[] { false, false });

            migrationBuilder.UpdateData(
                table: "ShopItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000005"),
                columns: new[] { "IsFreeTier", "IsUltimateTier" },
                values: new object[] { false, false });

            migrationBuilder.UpdateData(
                table: "ShopItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000006"),
                columns: new[] { "IsFreeTier", "IsUltimateTier" },
                values: new object[] { false, false });

            migrationBuilder.UpdateData(
                table: "ShopItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000007"),
                columns: new[] { "IsFreeTier", "IsUltimateTier" },
                values: new object[] { true, false });

            migrationBuilder.UpdateData(
                table: "ShopItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000008"),
                columns: new[] { "IsFreeTier", "IsUltimateTier" },
                values: new object[] { false, false });

            migrationBuilder.UpdateData(
                table: "ShopItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000009"),
                columns: new[] { "IsFreeTier", "IsUltimateTier" },
                values: new object[] { false, false });

            migrationBuilder.UpdateData(
                table: "ShopItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-00000000000f"),
                columns: new[] { "IsFreeTier", "IsUltimateTier" },
                values: new object[] { false, true });

            migrationBuilder.UpdateData(
                table: "ShopItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000010"),
                columns: new[] { "IsFreeTier", "IsUltimateTier" },
                values: new object[] { false, false });

            migrationBuilder.UpdateData(
                table: "ShopItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000011"),
                columns: new[] { "IsFreeTier", "IsUltimateTier" },
                values: new object[] { false, false });

            migrationBuilder.UpdateData(
                table: "ShopItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000012"),
                columns: new[] { "IsFreeTier", "IsUltimateTier" },
                values: new object[] { false, false });

            migrationBuilder.UpdateData(
                table: "ShopItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000013"),
                columns: new[] { "IsFreeTier", "IsUltimateTier" },
                values: new object[] { false, false });

            migrationBuilder.UpdateData(
                table: "ShopItems",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000014"),
                columns: new[] { "IsFreeTier", "IsUltimateTier" },
                values: new object[] { false, false });

            migrationBuilder.InsertData(
                table: "UserEntitlements",
                columns: new[] { "Id", "BillingPeriod", "CreatedAt", "ExpiresAt", "LapseReason", "LapsedAt", "LastVerifiedAt", "OrderId", "ProductId", "PromoCodeRedeemed", "PromoExpiresAt", "PurchaseToken", "PurchasedAt", "UpdatedAt" },
                values: new object[] { new Guid("99999999-0000-0000-0000-000000000002"), null, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, null, null, null, null, null, null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserEntitlements");

            migrationBuilder.DropColumn(
                name: "IsHiddenByEntitlement",
                table: "UserPlants");

            migrationBuilder.DropColumn(
                name: "IsHiddenByEntitlement",
                table: "UserDecorations");

            migrationBuilder.DropColumn(
                name: "IsFreeTier",
                table: "ShopItems");

            migrationBuilder.DropColumn(
                name: "IsUltimateTier",
                table: "ShopItems");

            migrationBuilder.DropColumn(
                name: "IsHiddenByEntitlement",
                table: "Shelves");

            migrationBuilder.DropColumn(
                name: "IsFreeTier",
                table: "PlantSpecies");

            migrationBuilder.DropColumn(
                name: "IsPrestigeTier",
                table: "PlantSpecies");

            migrationBuilder.DropColumn(
                name: "CurrentTier",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "EntitlementExpiresAt",
                table: "AppSettings");
        }
    }
}
