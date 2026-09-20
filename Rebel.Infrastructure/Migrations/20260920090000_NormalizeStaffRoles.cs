using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Rebel.Infrastructure.Data;

#nullable disable

namespace Rebel.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260920090000_NormalizeStaffRoles")]
public partial class NormalizeStaffRoles : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE "StaffMembers"
            SET "Role" = 'Front'
            WHERE "Role" IN ('Manager', 'Waiter', 'Bar');

            UPDATE "StaffShifts"
            SET "Role" = 'Front'
            WHERE "Role" IN ('Manager', 'Waiter', 'Bar');
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Legacy roles cannot be reconstructed after normalization.
    }
}
