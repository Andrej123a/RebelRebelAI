using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rebel.Infrastructure.Migrations
{
    [Migration("20260721130000_AddProductMenuTags")]
    public partial class AddProductMenuTags : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ContainsNuts",
                table: "Products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsGlutenFree",
                table: "Products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsLimited",
                table: "Products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPopular",
                table: "Products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPromo",
                table: "Products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSpicy",
                table: "Products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsVegan",
                table: "Products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsVegetarian",
                table: "Products",
                type: "boolean",
                nullable: false,
                defaultValue: false);
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
