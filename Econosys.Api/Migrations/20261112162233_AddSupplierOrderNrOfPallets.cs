using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Econosys.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierOrderNrOfPallets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NrOfPallets",
                table: "SupplierOrder",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NrOfPallets",
                table: "SupplierOrder");
        }
    }
}
