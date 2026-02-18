using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookLoggerApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixIsbnAndLanguageMaxLength : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Language",
                table: "Books",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldMaxLength: 10,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ISBN",
                table: "Books",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldMaxLength: 13,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Language",
                table: "Books",
                maxLength: 10,
                nullable: true,
                oldClrType: typeof(string),
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ISBN",
                table: "Books",
                maxLength: 13,
                nullable: true,
                oldClrType: typeof(string),
                oldMaxLength: 20,
                oldNullable: true);
        }
    }
}
