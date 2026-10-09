using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Homeji.Infrastructure.Migrations;
/// <inheritdoc />
public partial class AddRentalSourceExpiry : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "source_checked_at",
            schema: "homeji",
            table: "rental_source_listings",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "source_expires_at",
            schema: "homeji",
            table: "rental_source_listings",
            type: "timestamp with time zone",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "source_checked_at",
            schema: "homeji",
            table: "rental_source_listings");

        migrationBuilder.DropColumn(
            name: "source_expires_at",
            schema: "homeji",
            table: "rental_source_listings");
    }
}
