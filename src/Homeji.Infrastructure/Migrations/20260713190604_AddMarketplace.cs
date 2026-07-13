using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Homeji.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddMarketplace : Migration
{
    private static readonly string[] MediaPostSortIndexColumns = ["marketplace_post_id", "sort_order"];
    private static readonly string[] MediaPostUrlIndexColumns = ["marketplace_post_id", "url"];
    private static readonly string[] MapSearchIndexColumns = ["status", "latitude", "longitude"];
    private static readonly string[] StatusUpdatedIndexColumns = ["status", "updated_at"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "marketplace_posts",
            schema: "homeji",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                seller_id = table.Column<Guid>(type: "uuid", nullable: false),
                status = table.Column<int>(type: "integer", nullable: false),
                title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                description = table.Column<string>(type: "character varying(3000)", maxLength: 3000, nullable: false),
                price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                condition = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                linked_rental_post_id = table.Column<Guid>(type: "uuid", nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_marketplace_posts", x => x.id);
                table.CheckConstraint("ck_marketplace_posts_price", "price > 0");
                table.ForeignKey(
                    name: "fk_marketplace_posts_linked_rental_posts",
                    column: x => x.linked_rental_post_id,
                    principalSchema: "homeji",
                    principalTable: "rental_posts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "fk_marketplace_posts_sellers",
                    column: x => x.seller_id,
                    principalSchema: "homeji",
                    principalTable: "user_profiles",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "marketplace_post_media",
            schema: "homeji",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                marketplace_post_id = table.Column<Guid>(type: "uuid", nullable: false),
                url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                sort_order = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_marketplace_post_media", x => x.id);
                table.ForeignKey(
                    name: "FK_marketplace_post_media_marketplace_posts_marketplace_post_id",
                    column: x => x.marketplace_post_id,
                    principalSchema: "homeji",
                    principalTable: "marketplace_posts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_marketplace_post_media_post_sort",
            schema: "homeji",
            table: "marketplace_post_media",
            columns: MediaPostSortIndexColumns);

        migrationBuilder.CreateIndex(
            name: "ux_marketplace_post_media_post_url",
            schema: "homeji",
            table: "marketplace_post_media",
            columns: MediaPostUrlIndexColumns,
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_marketplace_posts_linked_rental_post_id",
            schema: "homeji",
            table: "marketplace_posts",
            column: "linked_rental_post_id");

        migrationBuilder.CreateIndex(
            name: "ix_marketplace_posts_map_search",
            schema: "homeji",
            table: "marketplace_posts",
            columns: MapSearchIndexColumns);

        migrationBuilder.CreateIndex(
            name: "ix_marketplace_posts_seller_id",
            schema: "homeji",
            table: "marketplace_posts",
            column: "seller_id");

        migrationBuilder.CreateIndex(
            name: "ix_marketplace_posts_status_updated",
            schema: "homeji",
            table: "marketplace_posts",
            columns: StatusUpdatedIndexColumns);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "marketplace_post_media",
            schema: "homeji");

        migrationBuilder.DropTable(
            name: "marketplace_posts",
            schema: "homeji");
    }
}
