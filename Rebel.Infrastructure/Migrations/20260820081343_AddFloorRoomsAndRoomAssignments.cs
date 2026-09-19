using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace _03.Rebel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFloorRoomsAndRoomAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FloorRoomId",
                table: "PubTables",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FloorRooms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Shape = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PositionX = table.Column<int>(type: "integer", nullable: false),
                    PositionY = table.Column<int>(type: "integer", nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: false),
                    Height = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FloorRooms", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PubTables_FloorRoomId",
                table: "PubTables",
                column: "FloorRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_FloorRooms_Name",
                table: "FloorRooms",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PubTables_FloorRooms_FloorRoomId",
                table: "PubTables",
                column: "FloorRoomId",
                principalTable: "FloorRooms",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PubTables_FloorRooms_FloorRoomId",
                table: "PubTables");

            migrationBuilder.DropTable(
                name: "FloorRooms");

            migrationBuilder.DropIndex(
                name: "IX_PubTables_FloorRoomId",
                table: "PubTables");

            migrationBuilder.DropColumn(
                name: "FloorRoomId",
                table: "PubTables");
        }
    }
}
