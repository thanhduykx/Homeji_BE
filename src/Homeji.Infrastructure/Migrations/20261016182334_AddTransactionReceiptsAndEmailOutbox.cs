using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861 // One-time migration index definitions.

namespace Homeji.Infrastructure.Migrations;
/// <inheritdoc />
public partial class AddTransactionReceiptsAndEmailOutbox : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "transaction_receipts",
            schema: "homeji",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                ReferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                Event = table.Column<int>(type: "integer", nullable: false),
                Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                ReferenceCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                IssuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_transaction_receipts", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "receipt_email_deliveries",
            schema: "homeji",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                Attempts = table.Column<int>(type: "integer", nullable: false),
                NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                LeaseUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                LeaseId = table.Column<Guid>(type: "uuid", nullable: true),
                SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                LastErrorCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_receipt_email_deliveries", x => x.Id);
                table.ForeignKey(
                    name: "FK_receipt_email_deliveries_transaction_receipts_ReceiptId",
                    column: x => x.ReceiptId,
                    principalSchema: "homeji",
                    principalTable: "transaction_receipts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_receipt_email_deliveries_ReceiptId",
            schema: "homeji",
            table: "receipt_email_deliveries",
            column: "ReceiptId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_receipt_email_deliveries_SentAt_NextAttemptAt",
            schema: "homeji",
            table: "receipt_email_deliveries",
            columns: new[] { "SentAt", "NextAttemptAt" });

        migrationBuilder.CreateIndex(
            name: "IX_transaction_receipts_ReferenceId_Event",
            schema: "homeji",
            table: "transaction_receipts",
            columns: new[] { "ReferenceId", "Event" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_transaction_receipts_UserId_IssuedAt_Id",
            schema: "homeji",
            table: "transaction_receipts",
            columns: new[] { "UserId", "IssuedAt", "Id" });

        migrationBuilder.Sql("ALTER TABLE homeji.transaction_receipts ENABLE ROW LEVEL SECURITY;");
        migrationBuilder.Sql("ALTER TABLE homeji.receipt_email_deliveries ENABLE ROW LEVEL SECURITY;");
        // Historical receipts do not enqueue email or claim that an old email was sent.
        migrationBuilder.Sql("""
            INSERT INTO homeji.transaction_receipts
                ("Id", "UserId", "ReferenceId", "Event", "Amount", "ReferenceCode", "Description", "IssuedAt")
            SELECT md5(id::text || ':paid')::uuid, user_id, id,
                CASE WHEN purpose = 3 THEN 1 ELSE 2 END, amount, order_code, description,
                COALESCE(paid_at, updated_at)
            FROM homeji.payment_transactions WHERE status = 2
            ON CONFLICT ("ReferenceId", "Event") DO NOTHING;

            INSERT INTO homeji.transaction_receipts
                ("Id", "UserId", "ReferenceId", "Event", "Amount", "ReferenceCode", "Description", "IssuedAt")
            SELECT md5("Id"::text || ':requested')::uuid, "UserId", "Id", 3, "Amount",
                replace("Id"::text, '-', ''), 'Ngân hàng ' || "BankName" || ' ••••' || right("AccountNumber", 4), "CreatedAt"
            FROM homeji.wallet_withdrawal_requests
            ON CONFLICT ("ReferenceId", "Event") DO NOTHING;

            INSERT INTO homeji.transaction_receipts
                ("Id", "UserId", "ReferenceId", "Event", "Amount", "ReferenceCode", "Description", "IssuedAt")
            SELECT md5("Id"::text || ':processed')::uuid, "UserId", "Id", CASE WHEN "Status" = 2 THEN 4 ELSE 5 END,
                "Amount", replace("Id"::text, '-', ''), 'Ngân hàng ' || "BankName" || ' ••••' || right("AccountNumber", 4),
                COALESCE("ProcessedAt", "CreatedAt")
            FROM homeji.wallet_withdrawal_requests WHERE "Status" IN (2, 3)
            ON CONFLICT ("ReferenceId", "Event") DO NOTHING;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "receipt_email_deliveries",
            schema: "homeji");

        migrationBuilder.DropTable(
            name: "transaction_receipts",
            schema: "homeji");
    }
}
