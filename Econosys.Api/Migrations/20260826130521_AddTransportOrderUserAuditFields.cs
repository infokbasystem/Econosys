using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Econosys.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTransportOrderUserAuditFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CreatedByUserId",
                table: "TransportOrder",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedTimeStamp",
                table: "TransportOrder",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EditedByUserId",
                table: "TransportOrder",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EditedTimeStamp",
                table: "TransportOrder",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransportOrder_CreatedByUserId",
                table: "TransportOrder",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TransportOrder_EditedByUserId",
                table: "TransportOrder",
                column: "EditedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_TransportOrder_Shipper_ShipperId",
                table: "TransportOrder",
                column: "ShipperId",
                principalTable: "Shipper",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TransportOrder_User_CreatedByUserId",
                table: "TransportOrder",
                column: "CreatedByUserId",
                principalTable: "User",
                principalColumn: "lngUser_ID");

            migrationBuilder.AddForeignKey(
                name: "FK_TransportOrder_User_EditedByUserId",
                table: "TransportOrder",
                column: "EditedByUserId",
                principalTable: "User",
                principalColumn: "lngUser_ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TransportOrder_Shipper_ShipperId",
                table: "TransportOrder");

            migrationBuilder.DropForeignKey(
                name: "FK_TransportOrder_User_CreatedByUserId",
                table: "TransportOrder");

            migrationBuilder.DropForeignKey(
                name: "FK_TransportOrder_User_EditedByUserId",
                table: "TransportOrder");

            migrationBuilder.DropIndex(
                name: "IX_TransportOrder_CreatedByUserId",
                table: "TransportOrder");

            migrationBuilder.DropIndex(
                name: "IX_TransportOrder_EditedByUserId",
                table: "TransportOrder");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "TransportOrder");

            migrationBuilder.DropColumn(
                name: "CreatedTimeStamp",
                table: "TransportOrder");

            migrationBuilder.DropColumn(
                name: "EditedByUserId",
                table: "TransportOrder");

            migrationBuilder.DropColumn(
                name: "EditedTimeStamp",
                table: "TransportOrder");
        }
    }
}
