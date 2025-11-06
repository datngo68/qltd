using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuanLyAnTrua.Migrations
{
    /// <inheritdoc />
    public partial class AddCreditorIdToMonthlyPayment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CreditorId",
                table: "MonthlyPayments",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyPayments_CreditorId",
                table: "MonthlyPayments",
                column: "CreditorId");

            migrationBuilder.AddForeignKey(
                name: "FK_MonthlyPayments_Users_CreditorId",
                table: "MonthlyPayments",
                column: "CreditorId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MonthlyPayments_Users_CreditorId",
                table: "MonthlyPayments");

            migrationBuilder.DropIndex(
                name: "IX_MonthlyPayments_CreditorId",
                table: "MonthlyPayments");

            migrationBuilder.DropColumn(
                name: "CreditorId",
                table: "MonthlyPayments");
        }
    }
}
