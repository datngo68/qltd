using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuanLyAnTrua.Migrations
{
    /// <inheritdoc />
    public partial class AddBankAccountToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccountHolderName",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankAccount",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankName",
                table: "Users",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccountHolderName",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "BankAccount",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "BankName",
                table: "Users");
        }
    }
}
