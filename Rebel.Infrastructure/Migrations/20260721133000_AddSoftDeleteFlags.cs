using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rebel.Infrastructure.Migrations
{
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
            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: table,
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                table: table,
                type: "timestamp with time zone",
                nullable: true);
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
