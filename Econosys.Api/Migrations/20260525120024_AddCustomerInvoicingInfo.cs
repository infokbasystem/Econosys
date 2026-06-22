using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Econosys.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerInvoicingInfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InvoicingInfo",
                table: "Customer",
                type: "varchar(2000)",
                unicode: false,
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InvoicingInfo",
                table: "Customer");
        }
    }
}
