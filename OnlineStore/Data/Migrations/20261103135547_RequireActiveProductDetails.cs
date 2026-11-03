using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnlineStore.Data.Migrations
{
    /// <inheritdoc />
    public partial class RequireActiveProductDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Preserve incomplete legacy rows and order references, but keep them out of the live catalog.
            // Completing the real details is required before reactivation; no fictional values are filled in.
            migrationBuilder.Sql("""
                UPDATE `Products` SET `IsActive` = 0
                WHERE `IsActive` <> 0 AND NOT (
                    CHAR_LENGTH(TRIM(`Sku`)) > 0 AND CHAR_LENGTH(TRIM(`Name`)) > 0
                    AND CHAR_LENGTH(TRIM(`Model`)) > 0 AND CHAR_LENGTH(TRIM(`SerialNumber`)) > 0
                    AND `Description` IS NOT NULL AND CHAR_LENGTH(TRIM(`Description`)) > 0
                    AND CHAR_LENGTH(TRIM(`WarrantyStatus`)) > 0
                    AND CHAR_LENGTH(TRIM(`DistributorInformation`)) > 0);
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_ActiveDetails",
                table: "Products",
                sql: "`IsActive` = 0 OR (CHAR_LENGTH(TRIM(`Sku`)) > 0 AND CHAR_LENGTH(TRIM(`Name`)) > 0 AND CHAR_LENGTH(TRIM(`Model`)) > 0 AND CHAR_LENGTH(TRIM(`SerialNumber`)) > 0 AND `Description` IS NOT NULL AND CHAR_LENGTH(TRIM(`Description`)) > 0 AND CHAR_LENGTH(TRIM(`WarrantyStatus`)) > 0 AND CHAR_LENGTH(TRIM(`DistributorInformation`)) > 0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_ActiveDetails",
                table: "Products");
        }
    }
}
