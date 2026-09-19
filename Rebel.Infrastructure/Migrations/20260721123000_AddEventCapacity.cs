using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rebel.Infrastructure.Migrations
{
    [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(global::Rebel.Infrastructure.Data.AppDbContext))]
    [Migration("20260721123000_AddEventCapacity")]
    public partial class AddEventCapacity : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "Events"
                ADD COLUMN IF NOT EXISTS "MaxGuests" integer;

                ALTER TABLE "Events"
                ADD COLUMN IF NOT EXISTS "MaxReservations" integer;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxGuests",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "MaxReservations",
                table: "Events");
        }
    }
}
