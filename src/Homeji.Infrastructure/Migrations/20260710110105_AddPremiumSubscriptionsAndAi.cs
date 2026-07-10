using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Homeji.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddPremiumSubscriptionsAndAi : Migration
{
    private static readonly string[] ActiveLookupColumns =
    {
        "user_id",
        "tier",
        "status",
        "started_at",
        "expires_at",
    };

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "package_code",
            schema: "homeji",
            table: "payment_transactions",
            type: "character varying(80)",
            maxLength: 80,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "purpose",
            schema: "homeji",
            table: "payment_transactions",
            type: "integer",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.CreateTable(
            name: "user_subscriptions",
            schema: "homeji",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                tier = table.Column<int>(type: "integer", nullable: false),
                status = table.Column<int>(type: "integer", nullable: false),
                package_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                package_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                payment_transaction_id = table.Column<Guid>(type: "uuid", nullable: true),
                started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_user_subscriptions", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "ix_user_subscriptions_active_lookup",
            schema: "homeji",
            table: "user_subscriptions",
            columns: ActiveLookupColumns);

        migrationBuilder.CreateIndex(
            name: "ix_user_subscriptions_user_id",
            schema: "homeji",
            table: "user_subscriptions",
            column: "user_id");

        migrationBuilder.CreateIndex(
            name: "ux_user_subscriptions_payment_transaction_id",
            schema: "homeji",
            table: "user_subscriptions",
            column: "payment_transaction_id",
            unique: true,
            filter: "payment_transaction_id IS NOT NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "user_subscriptions",
            schema: "homeji");

        migrationBuilder.DropColumn(
            name: "package_code",
            schema: "homeji",
            table: "payment_transactions");

        migrationBuilder.DropColumn(
            name: "purpose",
            schema: "homeji",
            table: "payment_transactions");
    }
}
