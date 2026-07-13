using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Homeji.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddRentalReviews : Migration
{
    private static readonly string[] PostUpdatedIndexColumns = ["rental_post_id", "updated_at"];
    private static readonly string[] PostReviewerIndexColumns = ["rental_post_id", "reviewer_id"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "rental_reviews",
            schema: "homeji",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                rental_post_id = table.Column<Guid>(type: "uuid", nullable: false),
                reviewer_id = table.Column<Guid>(type: "uuid", nullable: false),
                rating = table.Column<int>(type: "integer", nullable: false),
                comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_rental_reviews", x => x.id);
                table.CheckConstraint("ck_rental_reviews_rating", "rating >= 1 AND rating <= 5");
                table.ForeignKey(
                    name: "fk_rental_reviews_rental_posts",
                    column: x => x.rental_post_id,
                    principalSchema: "homeji",
                    principalTable: "rental_posts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_rental_reviews_user_profiles",
                    column: x => x.reviewer_id,
                    principalSchema: "homeji",
                    principalTable: "user_profiles",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_rental_reviews_post_updated",
            schema: "homeji",
            table: "rental_reviews",
            columns: PostUpdatedIndexColumns);

        migrationBuilder.CreateIndex(
            name: "ix_rental_reviews_reviewer_id",
            schema: "homeji",
            table: "rental_reviews",
            column: "reviewer_id");

        migrationBuilder.CreateIndex(
            name: "ux_rental_reviews_post_reviewer",
            schema: "homeji",
            table: "rental_reviews",
            columns: PostReviewerIndexColumns,
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "rental_reviews",
            schema: "homeji");
    }
}
