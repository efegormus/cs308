using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnlineStore.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceEmailStoppedState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InvoiceEmailDeliveries_SentAt_NextAttemptAt_LeaseUntil",
                table: "InvoiceEmailDeliveries");

            migrationBuilder.AddColumn<DateTime>(
                name: "StoppedAt",
                table: "InvoiceEmailDeliveries",
                type: "datetime(6)",
                precision: 6,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceEmailDeliveries_SentAt_StoppedAt_NextAttemptAt_LeaseU~",
                table: "InvoiceEmailDeliveries",
                columns: new[] { "SentAt", "StoppedAt", "NextAttemptAt", "LeaseUntil" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_InvoiceEmailDelivery_Stopped",
                table: "InvoiceEmailDeliveries",
                sql: "`StoppedAt` IS NULL OR (`SentAt` IS NULL AND `LeaseUntil` IS NULL AND `LastErrorCode` IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InvoiceEmailDeliveries_SentAt_StoppedAt_NextAttemptAt_LeaseU~",
                table: "InvoiceEmailDeliveries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InvoiceEmailDelivery_Stopped",
                table: "InvoiceEmailDeliveries");

            migrationBuilder.DropColumn(
                name: "StoppedAt",
                table: "InvoiceEmailDeliveries");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceEmailDeliveries_SentAt_NextAttemptAt_LeaseUntil",
                table: "InvoiceEmailDeliveries",
                columns: new[] { "SentAt", "NextAttemptAt", "LeaseUntil" });
        }
    }
}
