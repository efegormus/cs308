using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnlineStore.Data.Migrations
{
    /// <inheritdoc />
    public partial class AlignOrderLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_Status",
                table: "Orders");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Orders",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Processing",
                collation: "utf8mb4_bin",
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldMaxLength: 20,
                oldDefaultValue: "Pending",
                oldCollation: "utf8mb4_bin");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeliveredAt",
                table: "Orders",
                type: "datetime(6)",
                precision: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentStatus",
                table: "Orders",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Pending",
                collation: "utf8mb4_bin");

            migrationBuilder.AddColumn<DateTime>(
                name: "PurchasedAt",
                table: "Orders",
                type: "datetime(6)",
                precision: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitCost",
                table: "OrderItems",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            // Classify payment before renaming fulfillment states; no purchase dates/costs are guessed.
            migrationBuilder.Sql("""
                UPDATE `Orders`
                SET `PaymentStatus` = CASE `Status`
                    WHEN 'Pending' THEN 'Pending'
                    WHEN 'Cancelled' THEN 'Unknown'
                    ELSE 'Confirmed' END;
                UPDATE `Orders`
                SET `Status` = CASE `Status`
                    WHEN 'Pending' THEN 'Processing'
                    WHEN 'Paid' THEN 'Processing'
                    WHEN 'Shipped' THEN 'InTransit'
                    WHEN 'Completed' THEN 'Delivered'
                    ELSE `Status` END;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_FulfillmentPayment",
                table: "Orders",
                sql: "`Status` NOT IN ('InTransit', 'Delivered') OR (`Status` = 'InTransit' AND `PaymentStatus` = 'Confirmed') OR (`Status` = 'Delivered' AND `PaymentStatus` IN ('Confirmed', 'Refunded'))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_PaymentStatus",
                table: "Orders",
                sql: "`PaymentStatus` IN ('Pending', 'Confirmed', 'Failed', 'Refunded', 'Unknown')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_Status",
                table: "Orders",
                sql: "`Status` IN ('Processing', 'InTransit', 'Delivered', 'Cancelled')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OrderItems_UnitCost",
                table: "OrderItems",
                sql: "`UnitCost` IS NULL OR `UnitCost` >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_FulfillmentPayment",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_PaymentStatus",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_Status",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OrderItems_UnitCost",
                table: "OrderItems");

            migrationBuilder.Sql("""
                UPDATE `Orders`
                SET `Status` = CASE `Status`
                    WHEN 'Processing' THEN CASE WHEN `PaymentStatus` = 'Confirmed' THEN 'Paid' ELSE 'Pending' END
                    WHEN 'InTransit' THEN 'Shipped'
                    WHEN 'Delivered' THEN 'Completed'
                    ELSE `Status` END;
                """);

            migrationBuilder.DropColumn(
                name: "DeliveredAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PaymentStatus",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PurchasedAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "UnitCost",
                table: "OrderItems");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Orders",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Pending",
                collation: "utf8mb4_bin",
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldMaxLength: 20,
                oldDefaultValue: "Processing",
                oldCollation: "utf8mb4_bin");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_Status",
                table: "Orders",
                sql: "`Status` IN ('Pending', 'Paid', 'Shipped', 'Completed', 'Cancelled')");
        }
    }
}
