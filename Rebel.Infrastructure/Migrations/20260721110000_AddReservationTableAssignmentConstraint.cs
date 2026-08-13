using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace _03.Rebel.Infrastructure.Migrations
{
    /// <inheritdoc />
    [Migration("20260721110000_AddReservationTableAssignmentConstraint")]
    public partial class AddReservationTableAssignmentConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                WITH duplicate_assignments AS (
                    SELECT
                        "Id",
                        ROW_NUMBER() OVER (
                            PARTITION BY
                                "ReservationDate",
                                "ReservationTime",
                                "TableLabel"
                            ORDER BY "CreatedAtUtc", "Id"
                        ) AS row_number
                    FROM "Reservations"
                    WHERE "TableLabel" IS NOT NULL
                        AND "Status" IN ('Approved', 'Arrived')
                )
                UPDATE "Reservations"
                SET "TableLabel" = NULL
                WHERE "Id" IN (
                    SELECT "Id"
                    FROM duplicate_assignments
                    WHERE row_number > 1
                );
                """
            );

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_ReservationDate_ReservationTime_TableLabel",
                table: "Reservations",
                columns: new[] { "ReservationDate", "ReservationTime", "TableLabel" },
                unique: true,
                filter: "\"TableLabel\" IS NOT NULL AND \"Status\" IN ('Approved', 'Arrived')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reservations_ReservationDate_ReservationTime_TableLabel",
                table: "Reservations");
        }
    }
}
