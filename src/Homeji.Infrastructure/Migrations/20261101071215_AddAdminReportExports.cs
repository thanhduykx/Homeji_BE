using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861 // EF-generated migration arguments.

namespace Homeji.Infrastructure.Migrations;
    /// <inheritdoc />
    public partial class AddAdminReportExports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "admin_report_exports",
                schema: "homeji",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedById = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodDays = table.Column<int>(type: "integer", nullable: false),
                    Recipient = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    FileName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Content = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EmailStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    EmailAcceptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LeaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    LeaseUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastErrorCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_report_exports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_admin_report_exports_user_profiles_RequestedById",
                        column: x => x.RequestedById,
                        principalSchema: "homeji",
                        principalTable: "user_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_admin_report_exports_CreatedAt",
                schema: "homeji",
                table: "admin_report_exports",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_admin_report_exports_EmailStatus_NextAttemptAt",
                schema: "homeji",
                table: "admin_report_exports",
                columns: new[] { "EmailStatus", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_admin_report_exports_RequestedById_RequestId",
                schema: "homeji",
                table: "admin_report_exports",
                columns: new[] { "RequestedById", "RequestId" },
                unique: true);
            migrationBuilder.Sql("ALTER TABLE homeji.admin_report_exports ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("REVOKE ALL ON homeji.admin_report_exports FROM anon, authenticated;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "admin_report_exports",
                schema: "homeji");
        }
    }

