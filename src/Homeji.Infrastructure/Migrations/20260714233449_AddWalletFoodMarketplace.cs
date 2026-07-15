using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Homeji.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddWalletFoodMarketplace : Migration
{
        private static readonly string[] SellerSubscriptionIndexColumns = ["UserId", "ExpiresAt"];
        private static readonly string[] WalletTransactionHistoryIndexColumns = ["WalletUserId", "CreatedAt"];
        private static readonly string[] WalletTransactionReferenceIndexColumns = ["WalletUserId", "Kind", "ReferenceId"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "available_quantity",
                schema: "homeji",
                table: "marketplace_posts",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "listing_type",
                schema: "homeji",
                table: "marketplace_posts",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "preparation_minutes",
                schema: "homeji",
                table: "marketplace_posts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "reserved_quantity",
                schema: "homeji",
                table: "marketplace_posts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "unit",
                schema: "homeji",
                table: "marketplace_posts",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "sản phẩm");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FundsReleasedAt",
                schema: "homeji",
                table: "marketplace_orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PlatformFeeAmount",
                schema: "homeji",
                table: "marketplace_orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PlatformFeeRate",
                schema: "homeji",
                table: "marketplace_orders",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "Quantity",
                schema: "homeji",
                table: "marketplace_orders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RefundedAt",
                schema: "homeji",
                table: "marketplace_orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SellerNetAmount",
                schema: "homeji",
                table: "marketplace_orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitPrice",
                schema: "homeji",
                table: "marketplace_orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.Sql(
                """
                UPDATE homeji.marketplace_orders
                SET "UnitPrice" = "AgreedPrice",
                    "Quantity" = 1,
                    "PlatformFeeRate" = 0.10,
                    "PlatformFeeAmount" = ROUND("AgreedPrice" * 0.10, 0),
                    "SellerNetAmount" = "AgreedPrice" - ROUND("AgreedPrice" * 0.10, 0),
                    "RefundedAt" = CASE WHEN "Status" IN (3, 4) THEN "UpdatedAt" ELSE NULL END,
                    "FundsReleasedAt" = CASE WHEN "Status" = 5 THEN "UpdatedAt" ELSE NULL END,
                    "Status" = CASE WHEN "Status" IN (1, 2) THEN 4 ELSE "Status" END;
                """);

            migrationBuilder.CreateTable(
                name: "marketplace_seller_subscriptions",
                schema: "homeji",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PackageCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    PackageName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CommissionRate = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "wallet_accounts",
                schema: "homeji",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Balance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalDeposited = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalSpent = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalEarned = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wallet_accounts", x => x.UserId);
                    table.CheckConstraint("ck_wallet_accounts_balance", "\"Balance\" >= 0");
                    table.CheckConstraint("ck_wallet_accounts_totals", "\"TotalDeposited\" >= 0 AND \"TotalSpent\" >= 0 AND \"TotalEarned\" >= 0");
                    table.ForeignKey(
                        name: "FK_wallet_accounts_user_profiles_UserId",
                        column: x => x.UserId,
                        principalSchema: "homeji",
                        principalTable: "user_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "wallet_transactions",
                schema: "homeji",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WalletUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    BalanceAfter = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ReferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wallet_transactions", x => x.Id);
                    table.CheckConstraint("ck_wallet_transactions_amount", "\"Amount\" <> 0");
                    table.ForeignKey(
                        name: "FK_wallet_transactions_wallet_accounts_WalletUserId",
                        column: x => x.WalletUserId,
                        principalSchema: "homeji",
                        principalTable: "wallet_accounts",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_marketplace_posts_stock",
                schema: "homeji",
                table: "marketplace_posts",
                sql: "available_quantity >= 0 AND reserved_quantity >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_marketplace_seller_subscriptions_UserId_ExpiresAt",
                schema: "homeji",
                table: "marketplace_seller_subscriptions",
                columns: SellerSubscriptionIndexColumns);

            migrationBuilder.CreateIndex(
                name: "IX_wallet_transactions_WalletUserId_CreatedAt",
                schema: "homeji",
                table: "wallet_transactions",
                columns: WalletTransactionHistoryIndexColumns);

            migrationBuilder.CreateIndex(
                name: "IX_wallet_transactions_WalletUserId_Kind_ReferenceId",
                schema: "homeji",
                table: "wallet_transactions",
                columns: WalletTransactionReferenceIndexColumns,
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "marketplace_seller_subscriptions",
                schema: "homeji");

            migrationBuilder.DropTable(
                name: "wallet_transactions",
                schema: "homeji");

            migrationBuilder.DropTable(
                name: "wallet_accounts",
                schema: "homeji");

            migrationBuilder.DropCheckConstraint(
                name: "ck_marketplace_posts_stock",
                schema: "homeji",
                table: "marketplace_posts");

            migrationBuilder.DropColumn(
                name: "available_quantity",
                schema: "homeji",
                table: "marketplace_posts");

            migrationBuilder.DropColumn(
                name: "listing_type",
                schema: "homeji",
                table: "marketplace_posts");

            migrationBuilder.DropColumn(
                name: "preparation_minutes",
                schema: "homeji",
                table: "marketplace_posts");

            migrationBuilder.DropColumn(
                name: "reserved_quantity",
                schema: "homeji",
                table: "marketplace_posts");

            migrationBuilder.DropColumn(
                name: "unit",
                schema: "homeji",
                table: "marketplace_posts");

            migrationBuilder.DropColumn(
                name: "FundsReleasedAt",
                schema: "homeji",
                table: "marketplace_orders");

            migrationBuilder.DropColumn(
                name: "PlatformFeeAmount",
                schema: "homeji",
                table: "marketplace_orders");

            migrationBuilder.DropColumn(
                name: "PlatformFeeRate",
                schema: "homeji",
                table: "marketplace_orders");

            migrationBuilder.DropColumn(
                name: "Quantity",
                schema: "homeji",
                table: "marketplace_orders");

            migrationBuilder.DropColumn(
                name: "RefundedAt",
                schema: "homeji",
                table: "marketplace_orders");

            migrationBuilder.DropColumn(
                name: "SellerNetAmount",
                schema: "homeji",
                table: "marketplace_orders");

            migrationBuilder.DropColumn(
                name: "UnitPrice",
                schema: "homeji",
                table: "marketplace_orders");
        }
}
