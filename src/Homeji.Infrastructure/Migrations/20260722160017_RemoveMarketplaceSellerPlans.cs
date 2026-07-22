using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Homeji.Infrastructure.Migrations;

/// <inheritdoc />
public partial class RemoveMarketplaceSellerPlans : Migration
{
    private static readonly string[] SellerSubscriptionIndexColumns = ["UserId", "ExpiresAt"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "marketplace_seller_subscriptions",
            schema: "homeji");

        migrationBuilder.Sql(
            """
            UPDATE homeji.wallet_transactions
            SET "Description" = 'Điều chỉnh dịch vụ trước đây'
            WHERE "Kind" = 6;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "marketplace_seller_subscriptions",
            schema: "homeji",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                CommissionRate = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                PackageCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                PackageName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_marketplace_seller_subscriptions", x => x.Id);
                table.CheckConstraint("ck_marketplace_seller_subscription_commission", "\"CommissionRate\" > 0 AND \"CommissionRate\" < 1");
                table.CheckConstraint("ck_marketplace_seller_subscription_price", "\"Price\" > 0");
                table.ForeignKey(
                    name: "FK_marketplace_seller_subscriptions_user_profiles_UserId",
                    column: x => x.UserId,
                    principalSchema: "homeji",
                    principalTable: "user_profiles",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_marketplace_seller_subscriptions_UserId_ExpiresAt",
            schema: "homeji",
            table: "marketplace_seller_subscriptions",
            columns: SellerSubscriptionIndexColumns);
    }
}
