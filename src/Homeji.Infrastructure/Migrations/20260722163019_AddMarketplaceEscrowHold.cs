using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Homeji.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddMarketplaceEscrowHold : Migration
{
    private static readonly string[] FundsReleaseIndexColumns = ["Status", "DeliveredAt", "FundsReleasedAt"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "DeliveredAt",
            schema: "homeji",
            table: "marketplace_orders",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_marketplace_orders_Status_DeliveredAt_FundsReleasedAt",
            schema: "homeji",
            table: "marketplace_orders",
            columns: FundsReleaseIndexColumns);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_marketplace_orders_Status_DeliveredAt_FundsReleasedAt",
            schema: "homeji",
            table: "marketplace_orders");

        migrationBuilder.DropColumn(
            name: "DeliveredAt",
            schema: "homeji",
            table: "marketplace_orders");
    }
}
