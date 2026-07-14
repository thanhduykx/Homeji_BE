using System;
using Homeji.Infrastructure.Context;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Homeji.Infrastructure.Migrations;

/// <inheritdoc />
[Migration("20260714170000_AddConversationLastReadAt")]
[DbContext(typeof(ApplicationDbContext))]
public class AddConversationLastReadAt : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "ParticipantALastReadAt",
            schema: "homeji",
            table: "post_conversations",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "ParticipantBLastReadAt",
            schema: "homeji",
            table: "post_conversations",
            type: "timestamp with time zone",
            nullable: true);

        // Existing threads: treat history as read so only future incoming messages light up.
        migrationBuilder.Sql(
            """
            UPDATE homeji.post_conversations
            SET "ParticipantALastReadAt" = "UpdatedAt",
                "ParticipantBLastReadAt" = "UpdatedAt"
            WHERE "ParticipantALastReadAt" IS NULL
               OR "ParticipantBLastReadAt" IS NULL;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ParticipantALastReadAt",
            schema: "homeji",
            table: "post_conversations");

        migrationBuilder.DropColumn(
            name: "ParticipantBLastReadAt",
            schema: "homeji",
            table: "post_conversations");
    }
}
