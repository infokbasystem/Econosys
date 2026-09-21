using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Econosys.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddJeevesApiSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "JeevesApiEndpoint",
                table: "Settings",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JeevesApiKeyProtected",
                table: "Settings",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JeevesTestApiEndpoint",
                table: "Settings",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JeevesTestApiKeyProtected",
                table: "Settings",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "JeevesApiEndpoint",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "JeevesApiKeyProtected",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "JeevesTestApiEndpoint",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "JeevesTestApiKeyProtected",
                table: "Settings");
        }
    }
}
