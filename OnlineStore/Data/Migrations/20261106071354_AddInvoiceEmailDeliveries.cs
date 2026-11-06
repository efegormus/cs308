using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnlineStore.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceEmailDeliveries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InvoiceEmailDeliveries",
                columns: table => new
                {
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    RecipientSnapshot = table.Column<string>(type: "varchar(254)", maxLength: 254, nullable: false, collation: "utf8mb4_0900_ai_ci"),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", precision: 6, nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "datetime(6)", precision: 6, nullable: false),
                    LastAttemptAt = table.Column<DateTime>(type: "datetime(6)", precision: 6, nullable: true),
                    LeaseUntil = table.Column<DateTime>(type: "datetime(6)", precision: 6, nullable: true),
                    LeaseToken = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: true, collation: "utf8mb4_0900_ai_ci"),
                    SentAt = table.Column<DateTime>(type: "datetime(6)", precision: 6, nullable: true),
                    LastErrorCode = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: true, collation: "utf8mb4_0900_ai_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceEmailDeliveries", x => x.OrderId);
                    table.CheckConstraint("CK_InvoiceEmailDelivery_Attempts", "`AttemptCount` >= 0");
                    table.CheckConstraint("CK_InvoiceEmailDelivery_Lease", "(`LeaseUntil` IS NULL) = (`LeaseToken` IS NULL)");
                    table.CheckConstraint("CK_InvoiceEmailDelivery_Recipient", "CHAR_LENGTH(`RecipientSnapshot`) > 0");
                    table.ForeignKey(
                        name: "FK_InvoiceEmailDeliveries_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceEmailDeliveries_SentAt_NextAttemptAt_LeaseUntil",
                table: "InvoiceEmailDeliveries",
                columns: new[] { "SentAt", "NextAttemptAt", "LeaseUntil" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvoiceEmailDeliveries");
        }
    }
}
