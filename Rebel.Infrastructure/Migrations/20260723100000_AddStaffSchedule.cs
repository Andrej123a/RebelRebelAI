using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Rebel.Infrastructure.Data;

#nullable disable

namespace Rebel.Infrastructure.Migrations
{
    [Migration("20260723100000_AddStaffSchedule")]
    [DbContext(typeof(AppDbContext))]
    public partial class AddStaffSchedule : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE TABLE IF NOT EXISTS "StaffMembers" (
                    "Id" uuid NOT NULL,
                    "FullName" character varying(80) NOT NULL,
                    "Role" character varying(20) NOT NULL,
                    "PhoneNumber" character varying(40) NULL,
                    "IsActive" boolean NOT NULL,
                    CONSTRAINT "PK_StaffMembers" PRIMARY KEY ("Id")
                );

                CREATE INDEX IF NOT EXISTS "IX_StaffMembers_IsActive_Role"
                    ON "StaffMembers" ("IsActive", "Role");

                CREATE TABLE IF NOT EXISTS "StaffShifts" (
                    "Id" uuid NOT NULL,
                    "StaffMemberId" uuid NOT NULL,
                    "Role" character varying(20) NOT NULL,
                    "ShiftDate" date NOT NULL,
                    "StartsAt" time without time zone NOT NULL,
                    "EndsAt" time without time zone NOT NULL,
                    "Note" character varying(160) NULL,
                    "CreatedAtUtc" timestamp with time zone NOT NULL,
                    CONSTRAINT "PK_StaffShifts" PRIMARY KEY ("Id"),
                    CONSTRAINT "FK_StaffShifts_StaffMembers_StaffMemberId"
                        FOREIGN KEY ("StaffMemberId")
                        REFERENCES "StaffMembers" ("Id")
                        ON DELETE CASCADE
                );

                CREATE INDEX IF NOT EXISTS "IX_StaffShifts_ShiftDate_Role"
                    ON "StaffShifts" ("ShiftDate", "Role");

                CREATE INDEX IF NOT EXISTS "IX_StaffShifts_StaffMemberId"
                    ON "StaffShifts" ("StaffMemberId");
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StaffShifts");

            migrationBuilder.DropTable(
                name: "StaffMembers");
        }
    }
}
