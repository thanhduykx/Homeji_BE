using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Homeji.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddChatbotConversations : Migration
{
    private static readonly string[] ConversationUserUpdatedColumns =
    {
        "user_id",
        "updated_at",
    };

    private static readonly string[] MessageConversationCreatedColumns =
    {
        "conversation_id",
        "created_at",
    };

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "chat_conversations",
            schema: "homeji",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_chat_conversations", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "chat_messages",
            schema: "homeji",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                sender = table.Column<int>(type: "integer", nullable: false),
                content = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_chat_messages", x => x.id);
                table.ForeignKey(
                    name: "FK_chat_messages_chat_conversations_conversation_id",
                    column: x => x.conversation_id,
                    principalSchema: "homeji",
                    principalTable: "chat_conversations",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_chat_conversations_user_updated_at",
            schema: "homeji",
            table: "chat_conversations",
            columns: ConversationUserUpdatedColumns);

        migrationBuilder.CreateIndex(
            name: "ix_chat_messages_conversation_created_at",
            schema: "homeji",
            table: "chat_messages",
            columns: MessageConversationCreatedColumns);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "chat_messages",
            schema: "homeji");

        migrationBuilder.DropTable(
            name: "chat_conversations",
            schema: "homeji");
    }
}
