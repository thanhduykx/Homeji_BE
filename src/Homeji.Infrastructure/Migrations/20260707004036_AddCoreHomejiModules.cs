using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable IDE0161, CA1861

namespace Homeji.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCoreHomejiModules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "avatar_path",
                schema: "homeji",
                table: "user_profiles",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "landlord_verification_status",
                schema: "homeji",
                table: "user_profiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "max_budget",
                schema: "homeji",
                table: "user_profiles",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "onboarding_completed",
                schema: "homeji",
                table: "user_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "pet_preference",
                schema: "homeji",
                table: "user_profiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "phone",
                schema: "homeji",
                table: "user_profiles",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "preferred_area",
                schema: "homeji",
                table: "user_profiles",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "role",
                schema: "homeji",
                table: "user_profiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "school",
                schema: "homeji",
                table: "user_profiles",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "sleep_habit",
                schema: "homeji",
                table: "user_profiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "smoking_preference",
                schema: "homeji",
                table: "user_profiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "bad_words",
                schema: "homeji",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    value = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bad_words", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                schema: "homeji",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    related_entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_read = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notifications", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rental_posts",
                schema: "homeji",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    deposit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    area = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    latitude = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: false),
                    longitude = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: false),
                    moderation_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    view_count = table.Column<int>(type: "integer", nullable: false),
                    save_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rental_posts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "reports",
                schema: "homeji",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reporter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_type = table.Column<int>(type: "integer", nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    resolution_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reports", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "roommate_invitations",
                schema: "homeji",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    rental_post_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sender_id = table.Column<Guid>(type: "uuid", nullable: false),
                    receiver_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_roommate_invitations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "saved_posts",
                schema: "homeji",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rental_post_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_saved_posts", x => new { x.user_id, x.rental_post_id });
                });

            migrationBuilder.CreateTable(
                name: "rental_post_amenities",
                schema: "homeji",
                columns: table => new
                {
                    rental_post_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rental_post_amenities", x => new { x.rental_post_id, x.code });
                    table.ForeignKey(
                        name: "FK_rental_post_amenities_rental_posts_rental_post_id",
                        column: x => x.rental_post_id,
                        principalSchema: "homeji",
                        principalTable: "rental_posts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rental_post_media",
                schema: "homeji",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    rental_post_id = table.Column<Guid>(type: "uuid", nullable: false),
                    media_type = table.Column<int>(type: "integer", nullable: false),
                    bucket = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    is_thumbnail = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rental_post_media", x => x.id);
                    table.ForeignKey(
                        name: "FK_rental_post_media_rental_posts_rental_post_id",
                        column: x => x.rental_post_id,
                        principalSchema: "homeji",
                        principalTable: "rental_posts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_bad_words_value",
                schema: "homeji",
                table: "bad_words",
                column: "value",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notifications_recipient_unread_created",
                schema: "homeji",
                table: "notifications",
                columns: new[] { "recipient_id", "is_read", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_rental_post_amenities_code",
                schema: "homeji",
                table: "rental_post_amenities",
                column: "code");

            migrationBuilder.CreateIndex(
                name: "ix_rental_post_media_post_id",
                schema: "homeji",
                table: "rental_post_media",
                column: "rental_post_id");

            migrationBuilder.CreateIndex(
                name: "ux_rental_post_media_path",
                schema: "homeji",
                table: "rental_post_media",
                column: "path",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_rental_posts_map_search",
                schema: "homeji",
                table: "rental_posts",
                columns: new[] { "status", "latitude", "longitude" });

            migrationBuilder.CreateIndex(
                name: "ix_rental_posts_owner_id",
                schema: "homeji",
                table: "rental_posts",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_rental_posts_price",
                schema: "homeji",
                table: "rental_posts",
                column: "price");

            migrationBuilder.CreateIndex(
                name: "ix_rental_posts_status",
                schema: "homeji",
                table: "rental_posts",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_reports_status",
                schema: "homeji",
                table: "reports",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_reports_target",
                schema: "homeji",
                table: "reports",
                columns: new[] { "target_type", "target_id" });

            migrationBuilder.CreateIndex(
                name: "ix_roommate_invitations_pending_lookup",
                schema: "homeji",
                table: "roommate_invitations",
                columns: new[] { "rental_post_id", "sender_id", "receiver_id", "status" },
                filter: "status = 1");

            migrationBuilder.CreateIndex(
                name: "ix_roommate_invitations_receiver_id",
                schema: "homeji",
                table: "roommate_invitations",
                column: "receiver_id");

            migrationBuilder.CreateIndex(
                name: "ix_roommate_invitations_sender_id",
                schema: "homeji",
                table: "roommate_invitations",
                column: "sender_id");

            migrationBuilder.CreateIndex(
                name: "ix_saved_posts_rental_post_id",
                schema: "homeji",
                table: "saved_posts",
                column: "rental_post_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bad_words",
                schema: "homeji");

            migrationBuilder.DropTable(
                name: "notifications",
                schema: "homeji");

            migrationBuilder.DropTable(
                name: "rental_post_amenities",
                schema: "homeji");

            migrationBuilder.DropTable(
                name: "rental_post_media",
                schema: "homeji");

            migrationBuilder.DropTable(
                name: "reports",
                schema: "homeji");

            migrationBuilder.DropTable(
                name: "roommate_invitations",
                schema: "homeji");

            migrationBuilder.DropTable(
                name: "saved_posts",
                schema: "homeji");

            migrationBuilder.DropTable(
                name: "rental_posts",
                schema: "homeji");

            migrationBuilder.DropColumn(
                name: "avatar_path",
                schema: "homeji",
                table: "user_profiles");

            migrationBuilder.DropColumn(
                name: "landlord_verification_status",
                schema: "homeji",
                table: "user_profiles");

            migrationBuilder.DropColumn(
                name: "max_budget",
                schema: "homeji",
                table: "user_profiles");

            migrationBuilder.DropColumn(
                name: "onboarding_completed",
                schema: "homeji",
                table: "user_profiles");

            migrationBuilder.DropColumn(
                name: "pet_preference",
                schema: "homeji",
                table: "user_profiles");

            migrationBuilder.DropColumn(
                name: "phone",
                schema: "homeji",
                table: "user_profiles");

            migrationBuilder.DropColumn(
                name: "preferred_area",
                schema: "homeji",
                table: "user_profiles");

            migrationBuilder.DropColumn(
                name: "role",
                schema: "homeji",
                table: "user_profiles");

            migrationBuilder.DropColumn(
                name: "school",
                schema: "homeji",
                table: "user_profiles");

            migrationBuilder.DropColumn(
                name: "sleep_habit",
                schema: "homeji",
                table: "user_profiles");

            migrationBuilder.DropColumn(
                name: "smoking_preference",
                schema: "homeji",
                table: "user_profiles");
        }
    }
}
