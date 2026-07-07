using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable IDE0161

namespace Homeji.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountAndPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "payment_transactions",
                schema: "homeji",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    method = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    order_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    request_id = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    payment_url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    deeplink = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    qr_code_url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    qr_code = table.Column<string>(type: "text", nullable: true),
                    qr_data_url = table.Column<string>(type: "text", nullable: true),
                    external_transaction_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    provider_message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    raw_provider_payload = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    paid_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payment_transactions", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_payment_transactions_request_id",
                schema: "homeji",
                table: "payment_transactions",
                column: "request_id");

            migrationBuilder.CreateIndex(
                name: "ix_payment_transactions_user_id",
                schema: "homeji",
                table: "payment_transactions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ux_payment_transactions_order_code",
                schema: "homeji",
                table: "payment_transactions",
                column: "order_code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payment_transactions",
                schema: "homeji");
        }
    }
}
