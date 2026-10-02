using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861 // EF-generated migration arguments.

namespace Homeji.Infrastructure.Migrations;
    /// <inheritdoc />
    public partial class AddWebsiteTraffic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "website_page_views",
                schema: "homeji",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Page = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_website_page_views", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_website_page_views_OccurredAt",
                schema: "homeji",
                table: "website_page_views",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_website_page_views_SessionId_OccurredAt",
                schema: "homeji",
                table: "website_page_views",
                columns: new[] { "SessionId", "OccurredAt" });
            // Backend connects as table owner; browser database roles get no direct access.
            migrationBuilder.Sql("ALTER TABLE homeji.website_page_views ENABLE ROW LEVEL SECURITY;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "website_page_views",
                schema: "homeji");
        }
    }
