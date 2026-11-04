using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnlineStore.Data.Migrations
{
    /// <inheritdoc />
    public partial class LinkCheckoutToSourceCart : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SourceCartId",
                table: "Orders",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_SourceCartId",
                table: "Orders",
                column: "SourceCartId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Carts_SourceCartId",
                table: "Orders",
                column: "SourceCartId",
                principalTable: "Carts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Carts_SourceCartId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_SourceCartId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "SourceCartId",
                table: "Orders");
        }
    }
}
