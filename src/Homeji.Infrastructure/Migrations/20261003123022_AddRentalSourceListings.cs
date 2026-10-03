using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Homeji.Infrastructure.Migrations;
/// <inheritdoc />
public partial class AddRentalSourceListings : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "is_synthetic",
            schema: "homeji",
            table: "rental_posts",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "is_synthetic",
            schema: "homeji",
            table: "marketplace_posts",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.CreateTable(
            name: "rental_source_listings",
            schema: "homeji",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                source = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                source_id = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                source_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                district = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                area = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                image_urls = table.Column<string[]>(type: "jsonb", nullable: false),
                source_updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                collected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_rental_source_listings", x => x.id);
                table.CheckConstraint("ck_rental_source_listings_district", "district IN ('quan-9', 'thu-duc')");
                table.CheckConstraint("ck_rental_source_listings_images", "jsonb_typeof(image_urls) = 'array' AND jsonb_array_length(image_urls) BETWEEN 1 AND 10");
                table.CheckConstraint("ck_rental_source_listings_price_area", "price > 0 AND price <= 100000000 AND area > 0 AND area <= 1000");
            });

        migrationBuilder.CreateIndex(
            name: "IX_rental_source_listings_district_collected_at_id",
            schema: "homeji",
            table: "rental_source_listings",
            columns: ["district", "collected_at", "id"]);

        migrationBuilder.CreateIndex(
            name: "IX_rental_source_listings_source_source_id",
            schema: "homeji",
            table: "rental_source_listings",
            columns: ["source", "source_id"],
            unique: true);

        // Preserve the origin before the separate display-text cleanup removes
        // seed markers. This must never make generated advertisements look verified.
        migrationBuilder.Sql("""
            UPDATE homeji.rental_posts SET is_synthetic = true
            WHERE title ~* '(PRIMARY_ACCOUNTS|\[Mẫu\]|demo)'
               OR description ~* '(HOMEJI_|DEMO_KEY|DỮ LIỆU MẪU HOMEJI)';
            UPDATE homeji.marketplace_posts SET is_synthetic = true
            WHERE title ~* '(PRIMARY_ACCOUNTS|\[Mẫu\]|demo)'
               OR description ~* '(HOMEJI_|DEMO_KEY|DỮ LIỆU MẪU HOMEJI)';
            ALTER TABLE homeji.rental_source_listings ENABLE ROW LEVEL SECURITY;
            REVOKE ALL ON homeji.rental_source_listings FROM PUBLIC, anon, authenticated;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "rental_source_listings",
            schema: "homeji");

        migrationBuilder.DropColumn(
            name: "is_synthetic",
            schema: "homeji",
            table: "rental_posts");

        migrationBuilder.DropColumn(
            name: "is_synthetic",
            schema: "homeji",
            table: "marketplace_posts");
    }
}
