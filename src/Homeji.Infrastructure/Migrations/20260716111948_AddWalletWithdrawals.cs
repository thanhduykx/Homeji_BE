using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Homeji.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddWalletWithdrawals : Migration
{
        private static readonly string[] StatusCreatedAtColumns = ["Status", "CreatedAt"];
        private static readonly string[] UserIdCreatedAtColumns = ["UserId", "CreatedAt"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "wallet_withdrawal_requests",
                schema: "homeji",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    BankName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    AccountNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    AccountHolder = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AdminNote = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ProcessedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wallet_withdrawal_requests", x => x.Id);
                    table.CheckConstraint("ck_wallet_withdrawal_requests_amount", "\"Amount\" > 0");
                    table.ForeignKey(
                        name: "FK_wallet_withdrawal_requests_user_profiles_ProcessedBy",
                        column: x => x.ProcessedBy,
                        principalSchema: "homeji",
                        principalTable: "user_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_wallet_withdrawal_requests_user_profiles_UserId",
                        column: x => x.UserId,
                        principalSchema: "homeji",
                        principalTable: "user_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_wallet_withdrawal_requests_ProcessedBy",
                schema: "homeji",
                table: "wallet_withdrawal_requests",
                column: "ProcessedBy");

            migrationBuilder.CreateIndex(
                name: "IX_wallet_withdrawal_requests_Status_CreatedAt",
                schema: "homeji",
                table: "wallet_withdrawal_requests",
                columns: StatusCreatedAtColumns);

            migrationBuilder.CreateIndex(
                name: "IX_wallet_withdrawal_requests_UserId_CreatedAt",
                schema: "homeji",
                table: "wallet_withdrawal_requests",
                columns: UserIdCreatedAtColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "wallet_withdrawal_requests",
                schema: "homeji");
        }
}
