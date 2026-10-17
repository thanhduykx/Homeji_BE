using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Homeji.Infrastructure.Migrations;
/// <inheritdoc />
public partial class AddPasswordRecoveryOtp : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "password_recovery_challenges",
            schema: "homeji",
            columns: table => new
            {
                Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                NextSendAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Attempts = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_password_recovery_challenges", x => x.Email);
            });

        migrationBuilder.CreateIndex(
            name: "IX_password_recovery_challenges_ExpiresAt",
            schema: "homeji",
            table: "password_recovery_challenges",
            column: "ExpiresAt");
        migrationBuilder.Sql("ALTER TABLE homeji.password_recovery_challenges ENABLE ROW LEVEL SECURITY;");
        migrationBuilder.AddForeignKey(
            name: "fk_password_recovery_challenges_auth_user",
            schema: "homeji", table: "password_recovery_challenges", column: "UserId",
            principalSchema: "auth", principalTable: "users", principalColumn: "id",
            onDelete: ReferentialAction.Cascade);
        // Backfill only purchases evidenced by a matching wallet debit. Do not enqueue historical email.
        migrationBuilder.Sql("""
            INSERT INTO homeji.transaction_receipts
                ("Id", "UserId", "ReferenceId", "Event", "Amount", "ReferenceCode", "Description", "IssuedAt")
            SELECT md5(o."Id"::text || ':purchase')::uuid, o."BuyerId", o."Id", 6, o."AgreedPrice",
                replace(o."Id"::text, '-', ''), left(p.title, 220) || ' · Số lượng ' || o."Quantity" || ' · Thanh toán bằng số dư Homeji', o."CreatedAt"
            FROM homeji.marketplace_orders o JOIN homeji.marketplace_posts p ON p.id = o."MarketplacePostId"
            WHERE EXISTS (SELECT 1 FROM homeji.wallet_transactions w WHERE w."ReferenceId" = o."Id"
                AND w."WalletUserId" = o."BuyerId" AND w."Kind" = 2 AND w."Amount" = -o."AgreedPrice")
            ON CONFLICT ("ReferenceId", "Event") DO NOTHING;

            INSERT INTO homeji.transaction_receipts
                ("Id", "UserId", "ReferenceId", "Event", "Amount", "ReferenceCode", "Description", "IssuedAt")
            SELECT md5(o."Id"::text || ':refund')::uuid, o."BuyerId", o."Id", 7, o."AgreedPrice",
                replace(o."Id"::text, '-', ''), 'Hoàn tiền vào số dư Homeji · ' || left(p.title, 220) || ' · Số lượng ' || o."Quantity", o."RefundedAt"
            FROM homeji.marketplace_orders o JOIN homeji.marketplace_posts p ON p.id = o."MarketplacePostId"
            WHERE o."RefundedAt" IS NOT NULL AND EXISTS (SELECT 1 FROM homeji.transaction_receipts r
                WHERE r."ReferenceId" = o."Id" AND r."Event" = 6)
            ON CONFLICT ("ReferenceId", "Event") DO NOTHING;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "password_recovery_challenges",
            schema: "homeji");
    }
}
