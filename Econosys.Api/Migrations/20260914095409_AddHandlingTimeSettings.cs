using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Econosys.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddHandlingTimeSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "intHandlingTimesGoalMaxNrOfDays",
                table: "Settings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "intHandlingTimesGoalNrOfDays",
                table: "Settings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "decHandlingTimesPercentHandledUnderGoalNrOfDays",
                table: "Settings",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "intHandlingTimesThresholdNrOfDays",
                table: "Settings",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "intHandlingTimesGoalMaxNrOfDays",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "intHandlingTimesGoalNrOfDays",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "decHandlingTimesPercentHandledUnderGoalNrOfDays",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "intHandlingTimesThresholdNrOfDays",
                table: "Settings");
        }
    }
}
