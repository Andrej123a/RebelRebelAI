using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Rebel.Infrastructure.Data;

#nullable disable

namespace _03.Rebel.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260811090000_FixRepeatedProductNames")]
    public partial class FixRepeatedProductNames : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE "Products"
                SET "Name" = 'Katsu Burger'
                WHERE regexp_replace(trim("Name"), '\s+', ' ', 'g')
                    ILIKE 'Katsu Burger Katsu Burger';

                UPDATE "Products"
                SET "Name" = 'Chicken Strips'
                WHERE regexp_replace(trim("Name"), '\s+', ' ', 'g')
                    ILIKE 'Chicken Strips Chicken Strips';
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data corrections are intentionally not reversed.
        }
    }
}
