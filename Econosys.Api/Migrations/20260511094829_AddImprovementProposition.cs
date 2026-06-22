using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Econosys.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddImprovementProposition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ImprovementProposition",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedByName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    CreatedTimestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Description = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: false),
                    ProposedMeasure = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: false),
                    Responsible = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    StatusCode = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Note = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: true),
                    FollowUp = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImprovementProposition", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImprovementProposition");
        }
    }
}
