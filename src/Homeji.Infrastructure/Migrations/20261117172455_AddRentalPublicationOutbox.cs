using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Homeji.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddRentalPublicationOutbox : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "rental_publications",
            schema: "homeji",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                rental_post_id = table.Column<Guid>(type: "uuid", nullable: false),
                occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                available_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                lease_id = table.Column<Guid>(type: "uuid", nullable: true),
                attempts = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_rental_publications", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "ix_rental_publications_available",
            schema: "homeji",
            table: "rental_publications",
            columns: ["available_at", "id"],
            filter: "published_at IS NULL");

        migrationBuilder.Sql("ALTER TABLE homeji.rental_publications ENABLE ROW LEVEL SECURITY;");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "rental_publications",
            schema: "homeji");
    }
}
