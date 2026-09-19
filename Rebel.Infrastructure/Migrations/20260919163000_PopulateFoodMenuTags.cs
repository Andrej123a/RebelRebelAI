using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Rebel.Infrastructure.Data;

#nullable disable

namespace Rebel.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260919163000_PopulateFoodMenuTags")]
    public partial class PopulateFoodMenuTags : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE "Products" AS product
                SET
                    "IsSpicy" = TRIM(product."Name") IN (
                        'Smash Burger',
                        'Chicken Strips',
                        'Dirty Fries',
                        'Dirty Rebel Crisps',
                        'Buffalo Chicken Pizza',
                        'Pepperoni Pizza',
                        'Buffalo Wings',
                        'Hot Honey Wings'
                    ),
                    "IsVegan" = TRIM(product."Name") IN (
                        'Vegan Burger'
                    ),
                    "IsVegetarian" = TRIM(product."Name") IN (
                        'Vegan Burger',
                        'Cheese Sticks',
                        'French Fries',
                        'Dirty Fries',
                        'Dirty Rebel Crisps',
                        'Cheezy Pizza'
                    ),
                    "IsGlutenFree" = FALSE
                FROM "Categories" AS category
                WHERE category."Id" = product."CategoryId"
                    AND category."Type" = 0
                    AND NOT product."IsDeleted";
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE "Products" AS product
                SET
                    "IsSpicy" = FALSE,
                    "IsVegan" = FALSE,
                    "IsVegetarian" = FALSE
                FROM "Categories" AS category
                WHERE category."Id" = product."CategoryId"
                    AND category."Type" = 0
                    AND NOT product."IsDeleted";
                """);
        }
    }
}
