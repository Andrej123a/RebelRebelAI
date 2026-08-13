using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace _03.Rebel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBeerRecommendationProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AcidityLevel",
                table: "Products",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AlcoholByVolume",
                table: "Products",
                type: "numeric(4,1)",
                precision: 4,
                scale: 1,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BeerStyle",
                table: "Products",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BitternessLevel",
                table: "Products",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BodyLevel",
                table: "Products",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FlavorNotes",
                table: "Products",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginCountry",
                table: "Products",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PairingTags",
                table: "Products",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SweetnessLevel",
                table: "Products",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcidityLevel",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "AlcoholByVolume",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "BeerStyle",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "BitternessLevel",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "BodyLevel",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "FlavorNotes",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "OriginCountry",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "PairingTags",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "SweetnessLevel",
                table: "Products");
        }
    }
}
