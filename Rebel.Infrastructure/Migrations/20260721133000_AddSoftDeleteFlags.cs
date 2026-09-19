using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rebel.Infrastructure.Migrations
{
    [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(global::Rebel.Infrastructure.Data.AppDbContext))]
    [Migration("20260721133000_AddSoftDeleteFlags")]
    public partial class AddSoftDeleteFlags : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            AddSoftDeleteColumns(migrationBuilder, "Categories");
            AddSoftDeleteColumns(migrationBuilder, "Events");
            AddSoftDeleteColumns(migrationBuilder, "Products");
            AddSoftDeleteColumns(migrationBuilder, "Reservations");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            DropSoftDeleteColumns(migrationBuilder, "Categories");
            DropSoftDeleteColumns(migrationBuilder, "Events");
            DropSoftDeleteColumns(migrationBuilder, "Products");
            DropSoftDeleteColumns(migrationBuilder, "Reservations");
        }

        private static void AddSoftDeleteColumns(
            MigrationBuilder migrationBuilder,
            string table)
        {
            migrationBuilder.Sql(
                $"""
                ALTER TABLE "{table}"
                ADD COLUMN IF NOT EXISTS "IsDeleted" boolean NOT NULL DEFAULT FALSE;

                ALTER TABLE "{table}"
                ADD COLUMN IF NOT EXISTS "DeletedAtUtc" timestamp with time zone;
                """);
        }

        private static void DropSoftDeleteColumns(
            MigrationBuilder migrationBuilder,
            string table)
        {
            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: table);

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: table);
        }
    }
}
