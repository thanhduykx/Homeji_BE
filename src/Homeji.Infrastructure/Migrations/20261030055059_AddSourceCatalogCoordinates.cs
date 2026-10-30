using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Homeji.Infrastructure.Migrations;
/// <inheritdoc />
public partial class AddSourceCatalogCoordinates : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "latitude",
            schema: "homeji",
            table: "rental_source_listings",
            type: "numeric(10,7)",
            precision: 10,
            scale: 7,
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "longitude",
            schema: "homeji",
            table: "rental_source_listings",
            type: "numeric(10,7)",
            precision: 10,
            scale: 7,
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "latitude",
            schema: "homeji",
            table: "food_source_places",
            type: "numeric(10,7)",
            precision: 10,
            scale: 7,
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "longitude",
            schema: "homeji",
            table: "food_source_places",
            type: "numeric(10,7)",
            precision: 10,
            scale: 7,
            nullable: true);
        migrationBuilder.AddCheckConstraint("ck_rental_source_coordinates", "rental_source_listings", "(latitude IS NULL AND longitude IS NULL) OR (latitude IS NOT NULL AND longitude IS NOT NULL AND latitude BETWEEN -90 AND 90 AND longitude BETWEEN -180 AND 180)", "homeji");
        migrationBuilder.AddCheckConstraint("ck_food_source_coordinates", "food_source_places", "(latitude IS NULL AND longitude IS NULL) OR (latitude IS NOT NULL AND longitude IS NOT NULL AND latitude BETWEEN -90 AND 90 AND longitude BETWEEN -180 AND 180)", "homeji");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint("ck_rental_source_coordinates", "rental_source_listings", "homeji");
        migrationBuilder.DropCheckConstraint("ck_food_source_coordinates", "food_source_places", "homeji");
        migrationBuilder.DropColumn(
            name: "latitude",
            schema: "homeji",
            table: "rental_source_listings");

        migrationBuilder.DropColumn(
            name: "longitude",
            schema: "homeji",
            table: "rental_source_listings");

        migrationBuilder.DropColumn(
            name: "latitude",
            schema: "homeji",
            table: "food_source_places");

        migrationBuilder.DropColumn(
            name: "longitude",
            schema: "homeji",
            table: "food_source_places");
    }
}
