using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace _03.Rebel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFoodRecommendationProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "HeatLevel",
                table: "Products",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RichnessLevel",
                table: "Products",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SaltinessLevel",
                table: "Products",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "Products" AS product
                SET
                    "HeatLevel" = COALESCE(product."HeatLevel", profile.heat),
                    "SaltinessLevel" = COALESCE(product."SaltinessLevel", profile.saltiness),
                    "RichnessLevel" = COALESCE(product."RichnessLevel", profile.richness),
                    "SweetnessLevel" = COALESCE(product."SweetnessLevel", profile.sweetness),
                    "AcidityLevel" = COALESCE(product."AcidityLevel", profile.acidity),
                    "FlavorNotes" = COALESCE(NULLIF(product."FlavorNotes", ''), profile.flavours),
                    "PairingTags" = COALESCE(NULLIF(product."PairingTags", ''), profile.pairings)
                FROM (VALUES
                    ('Smash Burger', 3, 4, 5, 1, 3, 'beef, cheddar, pickles, gochujang mayo, crispy fries', 'IPA, pale ale, pilsner, amber ale'),
                    ('Rebel Burger', 1, 4, 5, 2, 3, 'beef, cheddar, fried onion, mustard, pickles, fries', 'pale ale, pilsner, amber ale, stout'),
                    ('Vegan Burger', 1, 3, 3, 2, 4, 'savory vegan patty, pickled red onion, mustard, pickles, fries', 'pilsner, wheat beer, pale ale, sour beer'),
                    ('Katsu Burger', 2, 4, 5, 2, 3, 'beef, cheddar, pickles, curry mayo, brioche, fries', 'IPA, pale ale, amber ale, pilsner'),
                    ('Cheese Sticks', 1, 4, 4, 1, 2, 'crispy breading, melted edamer, tomato sauce', 'pilsner, wheat beer, sour beer, pale ale'),
                    ('Corn Dog', 1, 3, 4, 3, 2, 'sausage, crisp batter, honey mustard, ketchup', 'lager, pilsner, wheat beer, amber ale'),
                    ('Chicken Strips', 3, 3, 4, 1, 2, 'crispy chicken, gochujang mayo', 'IPA, pilsner, pale ale, wheat beer'),
                    ('Dirty Rebel Crisps', 3, 5, 4, 1, 2, 'crispy potato, gochujang mayo, parmesan, black pepper', 'IPA, pilsner, pale ale'),
                    ('Dirty Fries', 4, 4, 4, 1, 4, 'crispy fries, sriracha mayo, pickles, pickled red onion', 'IPA, sour beer, pilsner'),
                    ('French Fries', 1, 4, 2, 1, 2, 'crispy potato, salt, ketchup', 'pilsner, lager, pale ale'),
                    ('Cheezy Pizza', 1, 5, 5, 2, 4, 'mixed cheese, gorgonzola, tomato, savory crust', 'IPA, sour beer, pilsner'),
                    ('Buffalo Chicken Pizza', 4, 4, 5, 1, 4, 'chicken, buffalo sauce, ranch, melted cheese', 'IPA, pilsner, pale ale'),
                    ('Pepperoni Pizza', 3, 5, 5, 3, 3, 'pepperoni, melted cheese, tomato, hot honey', 'IPA, amber ale, lager'),
                    ('The Fat Sausage', 1, 4, 4, 2, 2, 'savory sausage, honey mustard', 'lager, pilsner, amber ale'),
                    ('The Thin Sausage', 1, 4, 4, 2, 2, 'savory sausage, honey mustard', 'lager, pilsner, amber ale'),
                    ('Chicken Wings', 1, 3, 3, 1, 1, 'roasted chicken, ranch sauce', 'pale ale, pilsner, wheat beer'),
                    ('Hot Honey Wings', 4, 3, 4, 4, 2, 'chicken, hot honey, ranch sauce', 'IPA, pilsner, sour beer'),
                    ('Buffalo Wings', 5, 4, 4, 1, 4, 'chicken, buffalo sauce, ranch sauce', 'IPA, pilsner, pale ale')
                ) AS profile(name, heat, saltiness, richness, sweetness, acidity, flavours, pairings)
                WHERE LOWER(TRIM(product."Name")) = LOWER(profile.name)
                  AND NOT product."IsDeleted";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HeatLevel",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "RichnessLevel",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "SaltinessLevel",
                table: "Products");
        }
    }
}
