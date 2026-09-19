using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rebel.Infrastructure.Migrations
{
    [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(global::Rebel.Infrastructure.Data.AppDbContext))]
    [Migration("20260721130000_AddProductMenuTags")]
    public partial class AddProductMenuTags : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "Products" ADD COLUMN IF NOT EXISTS "ContainsNuts" boolean NOT NULL DEFAULT FALSE;
                ALTER TABLE "Products" ADD COLUMN IF NOT EXISTS "IsGlutenFree" boolean NOT NULL DEFAULT FALSE;
                ALTER TABLE "Products" ADD COLUMN IF NOT EXISTS "IsLimited" boolean NOT NULL DEFAULT FALSE;
                ALTER TABLE "Products" ADD COLUMN IF NOT EXISTS "IsPopular" boolean NOT NULL DEFAULT FALSE;
                ALTER TABLE "Products" ADD COLUMN IF NOT EXISTS "IsPromo" boolean NOT NULL DEFAULT FALSE;
                ALTER TABLE "Products" ADD COLUMN IF NOT EXISTS "IsSpicy" boolean NOT NULL DEFAULT FALSE;
                ALTER TABLE "Products" ADD COLUMN IF NOT EXISTS "IsVegan" boolean NOT NULL DEFAULT FALSE;
                ALTER TABLE "Products" ADD COLUMN IF NOT EXISTS "IsVegetarian" boolean NOT NULL DEFAULT FALSE;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContainsNuts",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IsGlutenFree",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IsLimited",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IsPopular",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IsPromo",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IsSpicy",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IsVegan",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IsVegetarian",
                table: "Products");
        }
    }
}
