using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuanLyAnTrua.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "MonthlyPayments",
                type: "TEXT",
                nullable: false,
                defaultValue: "Confirmed");
            
            // Cập nhật tất cả các bản ghi hiện có thành "Confirmed"
            migrationBuilder.Sql("UPDATE MonthlyPayments SET Status = 'Confirmed' WHERE Status IS NULL OR Status = ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "MonthlyPayments");
        }
    }
}
