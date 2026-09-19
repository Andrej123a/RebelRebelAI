using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace _03.Rebel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPubTableLayout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "LayoutHeight",
                table: "PubTables",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: false,
                defaultValue: 14m);

            migrationBuilder.AddColumn<decimal>(
                name: "LayoutWidth",
                table: "PubTables",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: false,
                defaultValue: 14m);

            migrationBuilder.AddColumn<decimal>(
                name: "LayoutX",
                table: "PubTables",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: false,
                defaultValue: 50m);

            migrationBuilder.AddColumn<decimal>(
                name: "LayoutY",
                table: "PubTables",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: false,
                defaultValue: 50m);

            migrationBuilder.AddColumn<int>(
                name: "Rotation",
                table: "PubTables",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Shape",
                table: "PubTables",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Square");

            migrationBuilder.AddColumn<string>(
                name: "TableType",
                table: "PubTables",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Classic");

            migrationBuilder.Sql(
                """
                WITH numbered AS (
                    SELECT
                        "Id",
                        ROW_NUMBER() OVER (
                            PARTITION BY COALESCE(NULLIF(TRIM("Area"), ''), 'Main floor')
                            ORDER BY "Label") - 1 AS index
                    FROM "PubTables"
                )
                UPDATE "PubTables" AS table_item
                SET
                    "LayoutX" = 8 + ((numbered.index % 5) * 18),
                    "LayoutY" = 12 + ((numbered.index / 5) * 20),
                    "LayoutWidth" = CASE
                        WHEN table_item."Capacity" >= 6 THEN 20
                        WHEN table_item."Capacity" <= 2 THEN 10
                        ELSE 14
                    END,
                    "LayoutHeight" = CASE
                        WHEN table_item."Capacity" >= 6 THEN 13
                        WHEN table_item."Capacity" <= 2 THEN 10
                        ELSE 14
                    END,
                    "TableType" = CASE
                        WHEN LOWER(COALESCE(table_item."Area", '')) LIKE '%bar%' THEN 'BarCounter'
                        WHEN LOWER(COALESCE(table_item."Area", '')) LIKE '%patio%' OR
                             LOWER(COALESCE(table_item."Area", '')) LIKE '%terrace%' THEN 'Outdoor'
                        ELSE 'Classic'
                    END,
                    "Shape" = CASE
                        WHEN LOWER(COALESCE(table_item."Area", '')) LIKE '%bar%' THEN 'Counter'
                        WHEN table_item."Capacity" >= 6 THEN 'Rectangle'
                        WHEN table_item."Capacity" <= 2 THEN 'Round'
                        ELSE 'Square'
                    END
                FROM numbered
                WHERE table_item."Id" = numbered."Id";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LayoutHeight",
                table: "PubTables");

            migrationBuilder.DropColumn(
                name: "LayoutWidth",
                table: "PubTables");

            migrationBuilder.DropColumn(
                name: "LayoutX",
                table: "PubTables");

            migrationBuilder.DropColumn(
                name: "LayoutY",
                table: "PubTables");

            migrationBuilder.DropColumn(
                name: "Rotation",
                table: "PubTables");

            migrationBuilder.DropColumn(
                name: "Shape",
                table: "PubTables");

            migrationBuilder.DropColumn(
                name: "TableType",
                table: "PubTables");
        }
    }
}
