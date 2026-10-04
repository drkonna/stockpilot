using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StockPilot.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddStoreAndUserStoreLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StoreId",
                table: "Users",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Stores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    IsCentral = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stores", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_StoreId",
                table: "Users",
                column: "StoreId");

            migrationBuilder.Sql(@"
                INSERT INTO ""Stores"" (""Name"", ""Code"", ""IsCentral"")
                VALUES ('Κεντρικό Κατάστημα', 'MAIN', true);
            ");

            migrationBuilder.Sql(@"
                UPDATE ""Users""
                SET ""StoreId"" = (SELECT ""Id"" FROM ""Stores"" WHERE ""IsCentral"" = true LIMIT 1)
                WHERE ""Role"" <> 'Admin';
            ");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_StoreId_Required_Unless_Admin",
                table: "Users",
                sql: "\"Role\" = 'Admin' OR \"StoreId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Stores_IsCentral",
                table: "Stores",
                column: "IsCentral",
                unique: true,
                filter: "\"IsCentral\" = true");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Stores_StoreId",
                table: "Users",
                column: "StoreId",
                principalTable: "Stores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Stores_StoreId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "Stores");

            migrationBuilder.DropIndex(
                name: "IX_Users_StoreId",
                table: "Users");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_StoreId_Required_Unless_Admin",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "StoreId",
                table: "Users");
        }
    }
}
