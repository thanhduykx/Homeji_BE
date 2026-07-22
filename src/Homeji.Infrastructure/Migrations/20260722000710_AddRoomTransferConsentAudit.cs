using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Homeji.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddRoomTransferConsentAudit : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "owner_consent_verification_note",
            schema: "homeji",
            table: "rental_posts",
            type: "character varying(500)",
            maxLength: 500,
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "owner_consent_verified_by",
            schema: "homeji",
            table: "rental_posts",
            type: "uuid",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "owner_consent_verification_note",
            schema: "homeji",
            table: "rental_posts");

        migrationBuilder.DropColumn(
            name: "owner_consent_verified_by",
            schema: "homeji",
            table: "rental_posts");
    }
}
