using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Homeji.Infrastructure.Migrations;
    /// <inheritdoc />
    public partial class AddFoodSourcePlaces : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "food_source_places",
                schema: "homeji",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    source_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    maps_search_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    verification_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    collected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_food_source_places", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_food_source_places_name_address",
                schema: "homeji",
                table: "food_source_places",
                columns: ["name", "address"],
                unique: true);
            migrationBuilder.Sql("""
                ALTER TABLE homeji.food_source_places ENABLE ROW LEVEL SECURITY;
                REVOKE ALL ON homeji.food_source_places FROM PUBLIC;
                DO $roles$ BEGIN
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'anon') THEN
                        REVOKE ALL ON homeji.food_source_places FROM anon;
                    END IF;
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'authenticated') THEN
                        REVOKE ALL ON homeji.food_source_places FROM authenticated;
                    END IF;
                END $roles$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "food_source_places",
                schema: "homeji");
        }
    }
