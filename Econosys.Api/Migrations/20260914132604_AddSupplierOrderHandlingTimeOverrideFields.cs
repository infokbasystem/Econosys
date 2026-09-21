using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Econosys.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierOrderHandlingTimeOverrideFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "OverrideHandlingTimeDays",
                table: "SupplierOrder",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ForceHandlingTimeCountAs",
                table: "SupplierOrder",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OverrideHandlingTimeDays",
                table: "SupplierOrder");

            migrationBuilder.DropColumn(
                name: "ForceHandlingTimeCountAs",
                table: "SupplierOrder");
        }
    }
}
