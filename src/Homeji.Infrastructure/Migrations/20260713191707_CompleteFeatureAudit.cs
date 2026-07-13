using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Homeji.Infrastructure.Migrations;

/// <inheritdoc />
public partial class CompleteFeatureAudit : Migration
{
    private static readonly string[] ApplicantCreatedColumns = ["ApplicantId", "CreatedAt"];
    private static readonly string[] StatusCreatedColumns = ["Status", "CreatedAt"];
    private static readonly string[] UserOccurredColumns = ["UserId", "OccurredAt"];
    private static readonly string[] OwnerCreatedColumns = ["OwnerId", "CreatedAt"];
    private static readonly string[] PostRequesterStatusColumns = ["RentalPostId", "RequesterId", "Status"];
    private static readonly string[] RequesterCreatedColumns = ["RequesterId", "CreatedAt"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "landlord_verification_requests",
            schema: "homeji",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ApplicantId = table.Column<Guid>(type: "uuid", nullable: false),
                DocumentUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                ApplicantNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                Status = table.Column<int>(type: "integer", nullable: false),
                ReviewNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                ReviewedBy = table.Column<Guid>(type: "uuid", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_landlord_verification_requests", x => x.Id);
                table.ForeignKey(
                    name: "FK_landlord_verification_requests_user_profiles_ApplicantId",
                    column: x => x.ApplicantId,
                    principalSchema: "homeji",
                    principalTable: "user_profiles",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "user_activities",
            schema: "homeji",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                Action = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                ResourcePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                HttpMethod = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                ResponseStatusCode = table.Column<int>(type: "integer", nullable: false),
                OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_user_activities", x => x.Id);
                table.ForeignKey(
                    name: "FK_user_activities_user_profiles_UserId",
                    column: x => x.UserId,
                    principalSchema: "homeji",
                    principalTable: "user_profiles",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "viewing_appointments",
            schema: "homeji",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                RentalPostId = table.Column<Guid>(type: "uuid", nullable: false),
                RequesterId = table.Column<Guid>(type: "uuid", nullable: false),
                OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                ScheduledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                Status = table.Column<int>(type: "integer", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_viewing_appointments", x => x.Id);
                table.ForeignKey(
                    name: "FK_viewing_appointments_rental_posts_RentalPostId",
                    column: x => x.RentalPostId,
                    principalSchema: "homeji",
                    principalTable: "rental_posts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_landlord_verification_requests_ApplicantId_CreatedAt",
            schema: "homeji",
            table: "landlord_verification_requests",
            columns: ApplicantCreatedColumns);

        migrationBuilder.CreateIndex(
            name: "IX_landlord_verification_requests_Status_CreatedAt",
            schema: "homeji",
            table: "landlord_verification_requests",
            columns: StatusCreatedColumns);

        migrationBuilder.CreateIndex(
            name: "IX_user_activities_UserId_OccurredAt",
            schema: "homeji",
            table: "user_activities",
            columns: UserOccurredColumns);

        migrationBuilder.CreateIndex(
            name: "IX_viewing_appointments_OwnerId_CreatedAt",
            schema: "homeji",
            table: "viewing_appointments",
            columns: OwnerCreatedColumns);

        migrationBuilder.CreateIndex(
            name: "IX_viewing_appointments_RentalPostId_RequesterId_Status",
            schema: "homeji",
            table: "viewing_appointments",
            columns: PostRequesterStatusColumns);

        migrationBuilder.CreateIndex(
            name: "IX_viewing_appointments_RequesterId_CreatedAt",
            schema: "homeji",
            table: "viewing_appointments",
            columns: RequesterCreatedColumns);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "landlord_verification_requests",
            schema: "homeji");

        migrationBuilder.DropTable(
            name: "user_activities",
            schema: "homeji");

        migrationBuilder.DropTable(
            name: "viewing_appointments",
            schema: "homeji");
    }
}
