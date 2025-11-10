using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuanLyAnTrua.Migrations
{
    /// <inheritdoc />
    public partial class AddTelegramUserIdToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Month",
                table: "SharedReports");

            migrationBuilder.DropColumn(
                name: "Year",
                table: "SharedReports");

            migrationBuilder.AddColumn<string>(
                name: "TelegramUserId",
                table: "Users",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TelegramUserId",
                table: "Users");

            migrationBuilder.AddColumn<int>(
                name: "Month",
                table: "SharedReports",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Year",
                table: "SharedReports",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }
    }
}
