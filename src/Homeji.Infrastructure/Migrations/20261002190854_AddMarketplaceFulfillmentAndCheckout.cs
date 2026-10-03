using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Homeji.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddMarketplaceFulfillmentAndCheckout : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "CheckoutId",
            schema: "homeji",
            table: "marketplace_orders",
            type: "uuid",
            nullable: false,
            defaultValue: Guid.Empty);

        migrationBuilder.AddColumn<string>(
            name: "DeliveryAddress",
            schema: "homeji",
            table: "marketplace_orders",
            type: "character varying(500)",
            maxLength: 500,
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "DeliveryLatitude",
            schema: "homeji",
            table: "marketplace_orders",
            type: "numeric(9,6)",
            precision: 9,
            scale: 6,
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "DeliveryLongitude",
            schema: "homeji",
            table: "marketplace_orders",
            type: "numeric(9,6)",
            precision: 9,
            scale: 6,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "FulfillmentType",
            schema: "homeji",
            table: "marketplace_orders",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<string>(
            name: "RecipientName",
            schema: "homeji",
            table: "marketplace_orders",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "RecipientPhone",
            schema: "homeji",
            table: "marketplace_orders",
            type: "character varying(16)",
            maxLength: 16,
            nullable: true);

        // Preserve the legacy checkout grouping when backfilling existing orders.
        migrationBuilder.Sql("""
            WITH grouped AS (
                SELECT "Id", first_value("Id") OVER (
                    PARTITION BY "BuyerId", "SellerId", "CreatedAt" ORDER BY "Id") AS checkout_id
                FROM homeji.marketplace_orders
            )
            UPDATE homeji.marketplace_orders AS orders
            SET "CheckoutId" = grouped.checkout_id
            FROM grouped WHERE orders."Id" = grouped."Id";
            """);

        migrationBuilder.CreateIndex(
            name: "IX_marketplace_orders_CheckoutId",
            schema: "homeji",
            table: "marketplace_orders",
            column: "CheckoutId");

        // Older application instances omit CheckoutId during a rolling deployment.
        // Assign a stable ID from their legacy group key; new instances supply
        // explicit IDs and bypass this compatibility branch.
        migrationBuilder.Sql("""
            CREATE FUNCTION homeji.assign_legacy_checkout_id() RETURNS trigger
            LANGUAGE plpgsql SET search_path = pg_catalog AS $function$
            BEGIN
                IF NEW."CheckoutId" = '00000000-0000-0000-0000-000000000000'::uuid THEN
                    NEW."CheckoutId" := md5(NEW."BuyerId"::text || ':' || NEW."SellerId"::text
                        || ':' || extract(epoch FROM NEW."CreatedAt")::text)::uuid;
                END IF;
                RETURN NEW;
            END;
            $function$;
            CREATE TRIGGER assign_legacy_checkout_id BEFORE INSERT ON homeji.marketplace_orders
            FOR EACH ROW EXECUTE FUNCTION homeji.assign_legacy_checkout_id();
            REVOKE ALL ON FUNCTION homeji.assign_legacy_checkout_id() FROM PUBLIC, anon, authenticated;
            """);

        migrationBuilder.Sql("""
            ALTER TABLE public."__EFMigrationsHistory" ENABLE ROW LEVEL SECURITY;
            REVOKE ALL ON TABLE public."__EFMigrationsHistory" FROM PUBLIC, anon, authenticated;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER assign_legacy_checkout_id ON homeji.marketplace_orders;
            DROP FUNCTION homeji.assign_legacy_checkout_id();
            """);
        // Keep migration metadata protected when rolling back business schema changes.
        migrationBuilder.DropIndex(
            name: "IX_marketplace_orders_CheckoutId",
            schema: "homeji",
            table: "marketplace_orders");

        migrationBuilder.DropColumn(
            name: "CheckoutId",
            schema: "homeji",
            table: "marketplace_orders");

        migrationBuilder.DropColumn(
            name: "DeliveryAddress",
            schema: "homeji",
            table: "marketplace_orders");

        migrationBuilder.DropColumn(
            name: "DeliveryLatitude",
            schema: "homeji",
            table: "marketplace_orders");

        migrationBuilder.DropColumn(
            name: "DeliveryLongitude",
            schema: "homeji",
            table: "marketplace_orders");

        migrationBuilder.DropColumn(
            name: "FulfillmentType",
            schema: "homeji",
            table: "marketplace_orders");

        migrationBuilder.DropColumn(
            name: "RecipientName",
            schema: "homeji",
            table: "marketplace_orders");

        migrationBuilder.DropColumn(
            name: "RecipientPhone",
            schema: "homeji",
            table: "marketplace_orders");
    }
}
