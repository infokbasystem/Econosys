using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Econosys.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviationWorkdaysOverride : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OverrideHandlingWorkdays",
                table: "Deviation",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HandlingWorkdaysOverrideReason",
                table: "Deviation",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OverrideHandlingWorkdays",
                table: "Deviation");

            migrationBuilder.DropColumn(
                name: "HandlingWorkdaysOverrideReason",
                table: "Deviation");
        }
    }
}
