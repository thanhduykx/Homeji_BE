using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Homeji.Infrastructure.Migrations;

    /// <inheritdoc />
    public partial class AddRoomTransferRentalPosts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "original_lease_ends_on",
                schema: "homeji",
                table: "rental_posts",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "owner_consent_confirmed",
                schema: "homeji",
                table: "rental_posts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "owner_consent_contact",
                schema: "homeji",
                table: "rental_posts",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "owner_consent_verified_at",
                schema: "homeji",
                table: "rental_posts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "pass_fee",
                schema: "homeji",
                table: "rental_posts",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "transfer_kind",
                schema: "homeji",
                table: "rental_posts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "transfer_reason",
                schema: "homeji",
                table: "rental_posts",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "original_lease_ends_on",
                schema: "homeji",
                table: "rental_posts");

            migrationBuilder.DropColumn(
                name: "owner_consent_confirmed",
                schema: "homeji",
                table: "rental_posts");

            migrationBuilder.DropColumn(
                name: "owner_consent_contact",
                schema: "homeji",
                table: "rental_posts");

            migrationBuilder.DropColumn(
                name: "owner_consent_verified_at",
                schema: "homeji",
                table: "rental_posts");

            migrationBuilder.DropColumn(
                name: "pass_fee",
                schema: "homeji",
                table: "rental_posts");

            migrationBuilder.DropColumn(
                name: "transfer_kind",
                schema: "homeji",
                table: "rental_posts");

            migrationBuilder.DropColumn(
                name: "transfer_reason",
                schema: "homeji",
                table: "rental_posts");
        }
    }
