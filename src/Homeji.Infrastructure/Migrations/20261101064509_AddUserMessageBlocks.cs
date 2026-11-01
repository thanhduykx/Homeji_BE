using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861 // EF-generated migration arguments.

namespace Homeji.Infrastructure.Migrations;
    /// <inheritdoc />
    public partial class AddUserMessageBlocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_message_blocks",
                schema: "homeji",
                columns: table => new
                {
                    BlockerId = table.Column<Guid>(type: "uuid", nullable: false),
                    BlockedId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_message_blocks", x => new { x.BlockerId, x.BlockedId });
                    table.CheckConstraint("CK_UserMessageBlock_DifferentUsers", "\"BlockerId\" <> \"BlockedId\"");
                    table.ForeignKey(
                        name: "FK_user_message_blocks_user_profiles_BlockedId",
                        column: x => x.BlockedId,
                        principalSchema: "homeji",
                        principalTable: "user_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_message_blocks_user_profiles_BlockerId",
                        column: x => x.BlockerId,
                        principalSchema: "homeji",
                        principalTable: "user_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_message_blocks_BlockedId_BlockerId",
                schema: "homeji",
                table: "user_message_blocks",
                columns: new[] { "BlockedId", "BlockerId" });
            // Only the authenticated application backend manages block relationships.
            migrationBuilder.Sql("ALTER TABLE homeji.user_message_blocks ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("REVOKE ALL ON homeji.user_message_blocks FROM anon, authenticated;");
            // Enforce the cutoff even when a send was already processing media when the block was saved.
            migrationBuilder.Sql("""
                CREATE FUNCTION homeji.reject_blocked_post_message() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE first_user uuid; second_user uuid;
                BEGIN
                    SELECT "ParticipantAId", "ParticipantBId" INTO first_user, second_user
                    FROM homeji.post_conversations WHERE "Id" = NEW."ConversationId";
                    PERFORM pg_advisory_xact_lock(hashtextextended(LEAST(first_user::text,second_user::text) || ':' || GREATEST(first_user::text,second_user::text), 724431));
                    IF EXISTS (SELECT 1 FROM homeji.user_message_blocks
                        WHERE ("BlockerId"=first_user AND "BlockedId"=second_user)
                           OR ("BlockerId"=second_user AND "BlockedId"=first_user)) THEN
                        RAISE EXCEPTION 'Messaging is blocked for this user pair' USING ERRCODE='23514', CONSTRAINT='user_message_block';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER reject_blocked_post_message BEFORE INSERT ON homeji.post_messages
                    FOR EACH ROW EXECUTE FUNCTION homeji.reject_blocked_post_message();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS reject_blocked_post_message ON homeji.post_messages; DROP FUNCTION IF EXISTS homeji.reject_blocked_post_message();");
            migrationBuilder.DropTable(
                name: "user_message_blocks",
                schema: "homeji");
        }
    }

