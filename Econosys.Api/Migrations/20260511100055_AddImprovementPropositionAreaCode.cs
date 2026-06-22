using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Econosys.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddImprovementPropositionAreaCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AreaCode",
                table: "ImprovementProposition",
                type: "varchar(30)",
                unicode: false,
                maxLength: 30,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AreaCode",
                table: "ImprovementProposition");
        }
    }
}
