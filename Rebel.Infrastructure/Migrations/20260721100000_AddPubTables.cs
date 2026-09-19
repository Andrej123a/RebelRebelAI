using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace _03.Rebel.Infrastructure.Migrations
{
    /// <inheritdoc />
    [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(global::Rebel.Infrastructure.Data.AppDbContext))]
    [Migration("20260721100000_AddPubTables")]
    public partial class AddPubTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE TABLE IF NOT EXISTS "PubTables" (
                    "Id" uuid NOT NULL,
                    "Label" character varying(40) NOT NULL,
                    "Area" character varying(80),
                    "Capacity" integer NOT NULL,
                    "IsActive" boolean NOT NULL,
                    CONSTRAINT "PK_PubTables" PRIMARY KEY ("Id")
                );

                CREATE UNIQUE INDEX IF NOT EXISTS "IX_PubTables_Label"
                    ON "PubTables" ("Label");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PubTables");
        }
    }
}
