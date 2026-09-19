using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace _03.Rebel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFloorFixturesSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "FloorRooms"
                ADD COLUMN IF NOT EXISTS "Rotation" integer NOT NULL DEFAULT 0;

                CREATE TABLE IF NOT EXISTS "FloorFixtures" (
                    "Id" uuid NOT NULL,
                    "FloorRoomId" uuid,
                    "Kind" character varying(24) NOT NULL,
                    "Label" character varying(40) NOT NULL,
                    "PositionX" integer NOT NULL,
                    "PositionY" integer NOT NULL,
                    "Width" integer NOT NULL,
                    "Height" integer NOT NULL,
                    "Rotation" integer NOT NULL,
                    CONSTRAINT "PK_FloorFixtures" PRIMARY KEY ("Id")
                );

                ALTER TABLE "FloorFixtures"
                ADD COLUMN IF NOT EXISTS "FloorRoomId" uuid;

                CREATE INDEX IF NOT EXISTS "IX_FloorFixtures_FloorRoomId"
                    ON "FloorFixtures" ("FloorRoomId");

                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1
                        FROM pg_constraint
                        WHERE conname = 'FK_FloorFixtures_FloorRooms_FloorRoomId'
                    ) THEN
                        ALTER TABLE "FloorFixtures"
                        ADD CONSTRAINT "FK_FloorFixtures_FloorRooms_FloorRoomId"
                        FOREIGN KEY ("FloorRoomId") REFERENCES "FloorRooms" ("Id")
                        ON DELETE CASCADE;
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FloorFixtures");

            migrationBuilder.DropColumn(
                name: "Rotation",
                table: "FloorRooms");
        }
    }
}
