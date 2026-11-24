using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable

namespace Econosys.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddQuotationHideExtraCosts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "bolHideExtraCostsOnPrint",
                table: "Quotation",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "bolHideExtraCostsOnPrint", table: "Quotation");
        }
    }
}
