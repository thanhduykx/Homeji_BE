using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861 // EF-generated migration arguments.

namespace Homeji.Infrastructure.Migrations;
    /// <inheritdoc />
    public partial class AddPremiumPriceChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "premium_price_changes",
                schema: "homeji",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    AdminId = table.Column<Guid>(type: "uuid", nullable: false),
                    PackageCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PackageName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    PreviousPrice = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_premium_price_changes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_premium_price_changes_user_profiles_AdminId",
                        column: x => x.AdminId,
                        principalSchema: "homeji",
                        principalTable: "user_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_premium_price_changes_AdminId",
                schema: "homeji",
                table: "premium_price_changes",
                column: "AdminId");

            migrationBuilder.CreateIndex(
                name: "IX_premium_price_changes_PackageCode_CreatedAt",
                schema: "homeji",
                table: "premium_price_changes",
                columns: new[] { "PackageCode", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_premium_price_changes_RequestId",
                schema: "homeji",
                table: "premium_price_changes",
                column: "RequestId",
                unique: true);
            migrationBuilder.Sql("ALTER TABLE homeji.premium_price_changes ENABLE ROW LEVEL SECURITY; REVOKE ALL ON homeji.premium_price_changes FROM anon, authenticated;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "premium_price_changes",
                schema: "homeji");
        }
    }
