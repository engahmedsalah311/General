using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class order : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProductColourId",
                table: "OrderItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_ProductColourId",
                table: "OrderItems",
                column: "ProductColourId");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderItems_ProductColours_ProductColourId",
                table: "OrderItems",
                column: "ProductColourId",
                principalTable: "ProductColours",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrderItems_ProductColours_ProductColourId",
                table: "OrderItems");

            migrationBuilder.DropIndex(
                name: "IX_OrderItems_ProductColourId",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "ProductColourId",
                table: "OrderItems");
        }
    }
}
