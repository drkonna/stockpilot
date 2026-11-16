using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockPilot.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddProductLowerSkuIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""CREATE INDEX "IX_Products_LowerSku" ON "Products" (lower("Sku"));""");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP INDEX "IX_Products_LowerSku";""");
        }
    }
}
