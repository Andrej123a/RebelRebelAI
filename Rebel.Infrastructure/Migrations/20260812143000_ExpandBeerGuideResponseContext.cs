using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Rebel.Infrastructure.Data;

#nullable disable

namespace _03.Rebel.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260812143000_ExpandBeerGuideResponseContext")]
    public partial class ExpandBeerGuideResponseContext : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "RecommendedProductIds",
                table: "BeerGuideResponseContexts",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(240)",
                oldMaxLength: 240);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "RecommendedProductIds",
                table: "BeerGuideResponseContexts",
                type: "character varying(240)",
                maxLength: 240,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);
        }
    }
}
