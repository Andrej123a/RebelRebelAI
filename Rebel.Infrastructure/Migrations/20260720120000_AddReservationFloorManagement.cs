using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace _03.Rebel.Infrastructure.Migrations
{
    /// <inheritdoc />
    [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(global::Rebel.Infrastructure.Data.AppDbContext))]
    [Migration("20260720120000_AddReservationFloorManagement")]
    public partial class AddReservationFloorManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "Reservations"
                ADD COLUMN IF NOT EXISTS "InternalNote" character varying(500);

                ALTER TABLE "Reservations"
                ADD COLUMN IF NOT EXISTS "TableLabel" character varying(40);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InternalNote",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "TableLabel",
                table: "Reservations");
        }
    }
}
