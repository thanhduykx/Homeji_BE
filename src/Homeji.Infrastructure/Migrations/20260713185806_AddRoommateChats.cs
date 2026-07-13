using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Homeji.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddRoommateChats : Migration
{
    private static readonly string[] FirstUpdatedIndexColumns = ["first_participant_id", "updated_at"];
    private static readonly string[] SecondUpdatedIndexColumns = ["second_participant_id", "updated_at"];
    private static readonly string[] ConversationSentIndexColumns = ["conversation_id", "sent_at"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "roommate_conversations",
            schema: "homeji",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                invitation_id = table.Column<Guid>(type: "uuid", nullable: false),
                rental_post_id = table.Column<Guid>(type: "uuid", nullable: false),
                first_participant_id = table.Column<Guid>(type: "uuid", nullable: false),
                second_participant_id = table.Column<Guid>(type: "uuid", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_roommate_conversations", x => x.id);
                table.CheckConstraint("ck_roommate_conversations_distinct_participants", "first_participant_id <> second_participant_id");
                table.ForeignKey(
                    name: "fk_roommate_conversations_first_participant",
                    column: x => x.first_participant_id,
                    principalSchema: "homeji",
                    principalTable: "user_profiles",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_roommate_conversations_invitations",
                    column: x => x.invitation_id,
                    principalSchema: "homeji",
                    principalTable: "roommate_invitations",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_roommate_conversations_rental_posts",
                    column: x => x.rental_post_id,
                    principalSchema: "homeji",
                    principalTable: "rental_posts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_roommate_conversations_second_participant",
                    column: x => x.second_participant_id,
                    principalSchema: "homeji",
                    principalTable: "user_profiles",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "roommate_messages",
            schema: "homeji",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                sender_id = table.Column<Guid>(type: "uuid", nullable: false),
                body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_roommate_messages", x => x.id);
                table.ForeignKey(
                    name: "fk_roommate_messages_conversations",
                    column: x => x.conversation_id,
                    principalSchema: "homeji",
                    principalTable: "roommate_conversations",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_roommate_messages_senders",
                    column: x => x.sender_id,
                    principalSchema: "homeji",
                    principalTable: "user_profiles",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_roommate_conversations_first_updated",
            schema: "homeji",
            table: "roommate_conversations",
            columns: FirstUpdatedIndexColumns);

        migrationBuilder.CreateIndex(
            name: "ix_roommate_conversations_rental_post_id",
            schema: "homeji",
            table: "roommate_conversations",
            column: "rental_post_id");

        migrationBuilder.CreateIndex(
            name: "ix_roommate_conversations_second_updated",
            schema: "homeji",
            table: "roommate_conversations",
            columns: SecondUpdatedIndexColumns);

        migrationBuilder.CreateIndex(
            name: "ux_roommate_conversations_invitation_id",
            schema: "homeji",
            table: "roommate_conversations",
            column: "invitation_id",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_roommate_messages_conversation_sent",
            schema: "homeji",
            table: "roommate_messages",
            columns: ConversationSentIndexColumns);

        migrationBuilder.CreateIndex(
            name: "ix_roommate_messages_sender_id",
            schema: "homeji",
            table: "roommate_messages",
            column: "sender_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "roommate_messages",
            schema: "homeji");

        migrationBuilder.DropTable(
            name: "roommate_conversations",
            schema: "homeji");
    }
}
