using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rebel.Infrastructure.Migrations
{
    [Migration("20260722120000_AddReservationEmailStatus")]
    public partial class AddReservationEmailStatus : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "Reservations"
                ADD COLUMN IF NOT EXISTS "EmailStatus"
                    character varying(30) NOT NULL DEFAULT 'NotSent';

                ALTER TABLE "Reservations"
                ADD COLUMN IF NOT EXISTS "LastEmailSentAtUtc"
                    timestamp with time zone;

                ALTER TABLE "Reservations"
                ADD COLUMN IF NOT EXISTS "LastEmailError"
                    character varying(500);
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastEmailError",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "LastEmailSentAtUtc",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "EmailStatus",
                table: "Reservations");
        }
    }
}
