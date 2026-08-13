using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Rebel.Infrastructure.Data;

#nullable disable

namespace _03.Rebel.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260811120000_AddContextAwareBeerFeedback")]
    public partial class AddContextAwareBeerFeedback : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var column in new[]
            {
                ("RequestedStyle", 40),
                ("FlavourTags", 160),
                ("RequestedOrigin", 40),
                ("StrengthPreference", 20),
                ("BitternessPreference", 20),
                ("SweetnessPreference", 20),
                ("FoodPairing", 40)
            })
            {
                migrationBuilder.AddColumn<string>(
                    name: column.Item1,
                    table: "BeerGuideFeedbacks",
                    type: $"character varying({column.Item2})",
                    maxLength: column.Item2,
                    nullable: true);
            }

            migrationBuilder.CreateTable(
                name: "BeerGuideResponseContexts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false),
                    RecommendedProductIds = table.Column<string>(
                        type: "character varying(240)",
                        maxLength: 240,
                        nullable: false),
                    RequestedStyle = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    FlavourTags = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    RequestedOrigin = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    StrengthPreference = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    BitternessPreference = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    SweetnessPreference = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    FoodPairing = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true)
                },
                constraints: table => table.PrimaryKey(
                    "PK_BeerGuideResponseContexts",
                    x => x.Id));

            migrationBuilder.CreateIndex(
                name: "IX_BeerGuideResponseContexts_CreatedAtUtc",
                table: "BeerGuideResponseContexts",
                column: "CreatedAtUtc");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "BeerGuideResponseContexts");

            foreach (var column in new[]
            {
                "RequestedStyle", "FlavourTags", "RequestedOrigin",
                "StrengthPreference", "BitternessPreference",
                "SweetnessPreference", "FoodPairing"
            })
            {
                migrationBuilder.DropColumn(
                    name: column,
                    table: "BeerGuideFeedbacks");
            }
        }
    }
}
