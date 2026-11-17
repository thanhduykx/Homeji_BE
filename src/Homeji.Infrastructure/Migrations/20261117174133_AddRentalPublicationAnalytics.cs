using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Homeji.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddRentalPublicationAnalytics : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "rental_publication_facts",
            schema: "homeji",
            columns: table => new
            {
                event_id = table.Column<Guid>(type: "uuid", nullable: false),
                rental_post_id = table.Column<Guid>(type: "uuid", nullable: false),
                occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_rental_publication_facts", x => x.event_id);
            });

        migrationBuilder.CreateIndex(
            name: "ix_rental_publication_facts_occurred",
            schema: "homeji",
            table: "rental_publication_facts",
            column: "occurred_at");

        migrationBuilder.Sql("ALTER TABLE homeji.rental_publication_facts ENABLE ROW LEVEL SECURITY;");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "rental_publication_facts",
            schema: "homeji");
    }
}
