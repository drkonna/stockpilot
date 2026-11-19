using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockPilot.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddProductStockReorderLevel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReorderLevel",
                table: "ProductStocks",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReorderLevel",
                table: "ProductStocks");
        }
    }
}
