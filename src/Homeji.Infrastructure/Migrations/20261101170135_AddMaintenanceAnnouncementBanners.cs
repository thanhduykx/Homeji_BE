using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861 // EF-generated migration arguments.

namespace Homeji.Infrastructure.Migrations;
    /// <inheritdoc />
    public partial class AddMaintenanceAnnouncementBanners : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "maintenance_announcements",
                schema: "homeji",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ScheduledStartAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ScheduledEndAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_maintenance_announcements", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_announcements_CreatedAt_Id",
                schema: "homeji",
                table: "maintenance_announcements",
                columns: new[] { "CreatedAt", "Id" });
            migrationBuilder.Sql("ALTER TABLE homeji.maintenance_announcements ENABLE ROW LEVEL SECURITY; REVOKE ALL ON homeji.maintenance_announcements FROM anon, authenticated;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "maintenance_announcements",
                schema: "homeji");
        }
    }
