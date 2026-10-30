#pragma warning disable CA1861 // EF migration index definitions are executed only once.
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Homeji.Infrastructure.Migrations;
    /// <inheritdoc />
    public partial class AddAiListingReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_listing_reviews",
                schema: "homeji",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    content_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    snapshot = table.Column<string>(type: "jsonb", nullable: false),
                    source_updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    lease_id = table.Column<Guid>(type: "uuid", nullable: true),
                    lease_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    evidence = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    confidence = table.Column<double>(type: "double precision", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    previous_status = table.Column<int>(type: "integer", nullable: true),
                    blocked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_listing_reviews", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ai_moderation_schedule",
                schema: "homeji",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    AutoBlock = table.Column<bool>(type: "boolean", nullable: false),
                    BatchSize = table.Column<int>(type: "integer", nullable: false),
                    SpacingSeconds = table.Column<int>(type: "integer", nullable: false),
                    PauseMinutes = table.Column<int>(type: "integer", nullable: false),
                    DailyRequestLimit = table.Column<int>(type: "integer", nullable: false),
                    DailyTokenBudget = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    BackfillBefore = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NextAllowedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    BudgetDay = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RequestsToday = table.Column<int>(type: "integer", nullable: false),
                    TokensReservedToday = table.Column<int>(type: "integer", nullable: false),
                    InBatch = table.Column<int>(type: "integer", nullable: false),
                    PauseReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_moderation_schedule", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_listing_reviews_kind_subject_id_content_hash",
                schema: "homeji",
                table: "ai_listing_reviews",
                columns: new[] { "kind", "subject_id", "content_hash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_listing_reviews_kind_subject_id_source_updated_at",
                schema: "homeji",
                table: "ai_listing_reviews",
                columns: new[] { "kind", "subject_id", "source_updated_at" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_listing_reviews_state_next_attempt_at",
                schema: "homeji",
                table: "ai_listing_reviews",
                columns: new[] { "state", "next_attempt_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_listing_reviews",
                schema: "homeji");

            migrationBuilder.DropTable(
                name: "ai_moderation_schedule",
                schema: "homeji");
        }
    }
