using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookLoggerApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOnboardingFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasCompletedOnboarding",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "OnboardingCompletedAt",
                table: "AppSettings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "OnboardingAutoCompletedForExistingUser",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "OnboardingCurrentStep",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OnboardingFlowVersion",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OnboardingIntroStatus",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "OnboardingTutorialPlantId",
                table: "AppSettings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "OnboardingTutorialPlantNeedsWateringAssist",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "OnboardingMissionStates",
                columns: table => new
                {
                    MissionId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnboardingMissionStates", x => x.MissionId);
                });

            migrationBuilder.UpdateData(
                table: "AppSettings",
                keyColumn: "Id",
                keyValue: new Guid("99999999-0000-0000-0000-000000000001"),
                columns: new[]
                {
                    "OnboardingAutoCompletedForExistingUser",
                    "OnboardingCompletedAt",
                    "OnboardingCurrentStep",
                    "OnboardingFlowVersion",
                    "OnboardingIntroStatus",
                    "OnboardingTutorialPlantId",
                    "OnboardingTutorialPlantNeedsWateringAssist"
                },
                values: new object[]
                {
                    false,
                    null,
                    0,
                    1,
                    0,
                    null,
                    false
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OnboardingMissionStates");

            migrationBuilder.DropColumn(
                name: "HasCompletedOnboarding",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "OnboardingCompletedAt",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "OnboardingAutoCompletedForExistingUser",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "OnboardingCurrentStep",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "OnboardingFlowVersion",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "OnboardingIntroStatus",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "OnboardingTutorialPlantId",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "OnboardingTutorialPlantNeedsWateringAssist",
                table: "AppSettings");
        }
    }
}
