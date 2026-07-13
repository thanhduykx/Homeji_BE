using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Homeji.Infrastructure.Migrations;

/// <inheritdoc />
public partial class CompleteFullFeatureAudit : Migration
{
    private static readonly string[] UserActivityIndexColumns = ["UserId", "Type", "OccurredAt"];
    private static readonly string[] BuyerCreatedIndexColumns = ["BuyerId", "CreatedAt"];
    private static readonly string[] MarketplaceBuyerStatusIndexColumns = ["MarketplacePostId", "BuyerId", "Status"];
    private static readonly string[] SellerCreatedIndexColumns = ["SellerId", "CreatedAt"];
    private static readonly string[] ParticipantAUpdatedIndexColumns = ["ParticipantAId", "UpdatedAt"];
    private static readonly string[] ParticipantBUpdatedIndexColumns = ["ParticipantBId", "UpdatedAt"];
    private static readonly string[] ConversationUniqueIndexColumns = ["SubjectType", "SubjectId", "ParticipantAId", "ParticipantBId"];
    private static readonly string[] ConversationSentIndexColumns = ["ConversationId", "SentAt"];
    private static readonly string[] RequesterCreatedIndexColumns = ["RequesterId", "CreatedAt"];
    private static readonly string[] WantedSearchIndexColumns = ["Status", "PreferredArea", "MaxBudget"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "contact_address",
            schema: "homeji",
            table: "user_profiles",
            type: "character varying(500)",
            maxLength: 500,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "rental_need",
            schema: "homeji",
            table: "user_profiles",
            type: "character varying(1000)",
            maxLength: 1000,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Details",
            schema: "homeji",
            table: "user_activities",
            type: "character varying(1000)",
            maxLength: 1000,
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "RelatedEntityId",
            schema: "homeji",
            table: "user_activities",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "Type",
            schema: "homeji",
            table: "user_activities",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<int>(
            name: "accuracy_rating",
            schema: "homeji",
            table: "rental_reviews",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "amenities_rating",
            schema: "homeji",
            table: "rental_reviews",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "cleanliness_rating",
            schema: "homeji",
            table: "rental_reviews",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "landlord_rating",
            schema: "homeji",
            table: "rental_reviews",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "location_rating",
            schema: "homeji",
            table: "rental_reviews",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "security_rating",
            schema: "homeji",
            table: "rental_reviews",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "value_rating",
            schema: "homeji",
            table: "rental_reviews",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<DateOnly>(
            name: "available_from",
            schema: "homeji",
            table: "rental_posts",
            type: "date",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "available_slots",
            schema: "homeji",
            table: "rental_posts",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<decimal>(
            name: "electricity_price",
            schema: "homeji",
            table: "rental_posts",
            type: "numeric(18,2)",
            precision: 18,
            scale: 2,
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<string>(
            name: "house_rules",
            schema: "homeji",
            table: "rental_posts",
            type: "character varying(2000)",
            maxLength: 2000,
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "internet_price",
            schema: "homeji",
            table: "rental_posts",
            type: "numeric(18,2)",
            precision: 18,
            scale: 2,
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<int>(
            name: "max_occupants",
            schema: "homeji",
            table: "rental_posts",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<decimal>(
            name: "water_price",
            schema: "homeji",
            table: "rental_posts",
            type: "numeric(18,2)",
            precision: 18,
            scale: 2,
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.CreateTable(
            name: "marketplace_orders",
            schema: "homeji",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                MarketplacePostId = table.Column<Guid>(type: "uuid", nullable: false),
                BuyerId = table.Column<Guid>(type: "uuid", nullable: false),
                SellerId = table.Column<Guid>(type: "uuid", nullable: false),
                AgreedPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                PickupAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                PickupAddress = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                Status = table.Column<int>(type: "integer", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_marketplace_orders", x => x.Id);
                table.ForeignKey(
                    name: "FK_marketplace_orders_marketplace_posts_MarketplacePostId",
                    column: x => x.MarketplacePostId,
                    principalSchema: "homeji",
                    principalTable: "marketplace_posts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "post_conversations",
            schema: "homeji",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                SubjectType = table.Column<int>(type: "integer", nullable: false),
                SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                ParticipantAId = table.Column<Guid>(type: "uuid", nullable: false),
                ParticipantBId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_post_conversations", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "rental_wanted_posts",
            schema: "homeji",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                RequesterId = table.Column<Guid>(type: "uuid", nullable: false),
                Status = table.Column<int>(type: "integer", nullable: false),
                Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                PreferredArea = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                MaxBudget = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                OccupantCount = table.Column<int>(type: "integer", nullable: false),
                AmenityCodes = table.Column<string[]>(type: "text[]", nullable: false),
                DesiredMoveInDate = table.Column<DateOnly>(type: "date", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_rental_wanted_posts", x => x.Id);
                table.ForeignKey(
                    name: "FK_rental_wanted_posts_user_profiles_RequesterId",
                    column: x => x.RequesterId,
                    principalSchema: "homeji",
                    principalTable: "user_profiles",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "post_messages",
            schema: "homeji",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                SenderId = table.Column<Guid>(type: "uuid", nullable: false),
                Body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_post_messages", x => x.Id);
                table.ForeignKey(
                    name: "FK_post_messages_post_conversations_ConversationId",
                    column: x => x.ConversationId,
                    principalSchema: "homeji",
                    principalTable: "post_conversations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_user_activities_UserId_Type_OccurredAt",
            schema: "homeji",
            table: "user_activities",
            columns: UserActivityIndexColumns);

        migrationBuilder.CreateIndex(
            name: "IX_marketplace_orders_BuyerId_CreatedAt",
            schema: "homeji",
            table: "marketplace_orders",
            columns: BuyerCreatedIndexColumns);

        migrationBuilder.CreateIndex(
            name: "IX_marketplace_orders_MarketplacePostId_BuyerId_Status",
            schema: "homeji",
            table: "marketplace_orders",
            columns: MarketplaceBuyerStatusIndexColumns);

        migrationBuilder.CreateIndex(
            name: "IX_marketplace_orders_SellerId_CreatedAt",
            schema: "homeji",
            table: "marketplace_orders",
            columns: SellerCreatedIndexColumns);

        migrationBuilder.CreateIndex(
            name: "IX_post_conversations_ParticipantAId_UpdatedAt",
            schema: "homeji",
            table: "post_conversations",
            columns: ParticipantAUpdatedIndexColumns);

        migrationBuilder.CreateIndex(
            name: "IX_post_conversations_ParticipantBId_UpdatedAt",
            schema: "homeji",
            table: "post_conversations",
            columns: ParticipantBUpdatedIndexColumns);

        migrationBuilder.CreateIndex(
            name: "IX_post_conversations_SubjectType_SubjectId_ParticipantAId_Par~",
            schema: "homeji",
            table: "post_conversations",
            columns: ConversationUniqueIndexColumns,
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_post_messages_ConversationId_SentAt",
            schema: "homeji",
            table: "post_messages",
            columns: ConversationSentIndexColumns);

        migrationBuilder.CreateIndex(
            name: "IX_rental_wanted_posts_RequesterId_CreatedAt",
            schema: "homeji",
            table: "rental_wanted_posts",
            columns: RequesterCreatedIndexColumns);

        migrationBuilder.CreateIndex(
            name: "IX_rental_wanted_posts_Status_PreferredArea_MaxBudget",
            schema: "homeji",
            table: "rental_wanted_posts",
            columns: WantedSearchIndexColumns);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "marketplace_orders",
            schema: "homeji");

        migrationBuilder.DropTable(
            name: "post_messages",
            schema: "homeji");

        migrationBuilder.DropTable(
            name: "rental_wanted_posts",
            schema: "homeji");

        migrationBuilder.DropTable(
            name: "post_conversations",
            schema: "homeji");

        migrationBuilder.DropIndex(
            name: "IX_user_activities_UserId_Type_OccurredAt",
            schema: "homeji",
            table: "user_activities");

        migrationBuilder.DropColumn(
            name: "contact_address",
            schema: "homeji",
            table: "user_profiles");

        migrationBuilder.DropColumn(
            name: "rental_need",
            schema: "homeji",
            table: "user_profiles");

        migrationBuilder.DropColumn(
            name: "Details",
            schema: "homeji",
            table: "user_activities");

        migrationBuilder.DropColumn(
            name: "RelatedEntityId",
            schema: "homeji",
            table: "user_activities");

        migrationBuilder.DropColumn(
            name: "Type",
            schema: "homeji",
            table: "user_activities");

        migrationBuilder.DropColumn(
            name: "accuracy_rating",
            schema: "homeji",
            table: "rental_reviews");

        migrationBuilder.DropColumn(
            name: "amenities_rating",
            schema: "homeji",
            table: "rental_reviews");

        migrationBuilder.DropColumn(
            name: "cleanliness_rating",
            schema: "homeji",
            table: "rental_reviews");

        migrationBuilder.DropColumn(
            name: "landlord_rating",
            schema: "homeji",
            table: "rental_reviews");

        migrationBuilder.DropColumn(
            name: "location_rating",
            schema: "homeji",
            table: "rental_reviews");

        migrationBuilder.DropColumn(
            name: "security_rating",
            schema: "homeji",
            table: "rental_reviews");

        migrationBuilder.DropColumn(
            name: "value_rating",
            schema: "homeji",
            table: "rental_reviews");

        migrationBuilder.DropColumn(
            name: "available_from",
            schema: "homeji",
            table: "rental_posts");

        migrationBuilder.DropColumn(
            name: "available_slots",
            schema: "homeji",
            table: "rental_posts");

        migrationBuilder.DropColumn(
            name: "electricity_price",
            schema: "homeji",
            table: "rental_posts");

        migrationBuilder.DropColumn(
            name: "house_rules",
            schema: "homeji",
            table: "rental_posts");

        migrationBuilder.DropColumn(
            name: "internet_price",
            schema: "homeji",
            table: "rental_posts");

        migrationBuilder.DropColumn(
            name: "max_occupants",
            schema: "homeji",
            table: "rental_posts");

        migrationBuilder.DropColumn(
            name: "water_price",
            schema: "homeji",
            table: "rental_posts");
    }
}
