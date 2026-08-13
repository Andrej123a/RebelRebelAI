using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rebel.Infrastructure.Migrations
{
    [Migration("20260721123000_AddEventCapacity")]
    public partial class AddEventCapacity : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxGuests",
                table: "Events",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxReservations",
                table: "Events",
                type: "integer",
                nullable: true);
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
