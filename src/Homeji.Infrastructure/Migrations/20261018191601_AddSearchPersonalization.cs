using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Homeji.Infrastructure.Migrations;
    /// <inheritdoc />
    public partial class AddSearchPersonalization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "search_personalization",
                schema: "homeji",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    AiEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    EmailEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CheckedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Data = table.Column<string>(type: "jsonb", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_search_personalization", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_search_personalization_user_profiles_UserId",
                        column: x => x.UserId,
                        principalSchema: "homeji",
                        principalTable: "user_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_search_personalization_Enabled_CheckedAt",
                schema: "homeji",
                table: "search_personalization",
                columns: ["Enabled", "CheckedAt"]);
            // Backend-only data: no direct Data API access to behavior or consent.
            migrationBuilder.Sql("""
                ALTER TABLE homeji.search_personalization ENABLE ROW LEVEL SECURITY;
                REVOKE ALL ON homeji.search_personalization FROM anon, authenticated;
                GRANT ALL ON homeji.search_personalization TO service_role;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "search_personalization",
                schema: "homeji");
        }
    }
