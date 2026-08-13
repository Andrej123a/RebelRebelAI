using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rebel.Infrastructure.Migrations
{
    [Migration("20260722110000_AddReservationCode")]
    public partial class AddReservationCode : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "Reservations"
                ADD COLUMN IF NOT EXISTS "ReservationCode"
                    character varying(16) NOT NULL DEFAULT '';

                UPDATE "Reservations"
                SET "ReservationCode" =
                    'RR-' ||
                    upper(
                        substring(
                            replace("Id"::text, '-', ''),
                            1,
                            6
                        )
                    )
                WHERE "ReservationCode" = '';

                CREATE UNIQUE INDEX IF NOT EXISTS "IX_Reservations_ReservationCode"
                    ON "Reservations" ("ReservationCode");
                """
            );
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reservations_ReservationCode",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "ReservationCode",
                table: "Reservations");
        }
    }
}
