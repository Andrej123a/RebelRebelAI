using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Rebel.Infrastructure.Data;

#nullable disable

namespace _03.Rebel.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260811100000_AddBeerGuideFeedback")]
    public partial class AddBeerGuideFeedback : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BeerGuideFeedbacks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ResponseId = table.Column<Guid>(type: "uuid", nullable: false),
                    AnonymousSessionHash = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: false),
                    IsPositive = table.Column<bool>(type: "boolean", nullable: false),
                    Reason = table.Column<string>(
                        type: "character varying(30)",
                        maxLength: 30,
                        nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BeerGuideFeedbacks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BeerGuideFeedbacks_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BeerGuideFeedbacks_ProductId_CreatedAtUtc",
                table: "BeerGuideFeedbacks",
                columns: new[] { "ProductId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_BeerGuideFeedbacks_ResponseId_AnonymousSessionHash_ProductId",
                table: "BeerGuideFeedbacks",
                columns: new[] { "ResponseId", "AnonymousSessionHash", "ProductId" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "BeerGuideFeedbacks");
        }
    }
}
