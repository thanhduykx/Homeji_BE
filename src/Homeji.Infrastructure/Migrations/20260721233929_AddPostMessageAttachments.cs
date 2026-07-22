using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Homeji.Infrastructure.Migrations;

    /// <inheritdoc />
    public partial class AddPostMessageAttachments : Migration
    {
        private static readonly string[] MessageCreatedColumns = ["MessageId", "CreatedAt"];
        private static readonly string[] UploaderCreatedColumns = ["UploaderId", "CreatedAt"];
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "post_message_attachments",
                schema: "homeji",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    UploaderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Context = table.Column<int>(type: "integer", nullable: false),
                    MimeType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Content = table.Column<byte[]>(type: "bytea", nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: false),
                    Height = table.Column<int>(type: "integer", nullable: false),
                    Bytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_post_message_attachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_post_message_attachments_post_messages_MessageId",
                        column: x => x.MessageId,
                        principalSchema: "homeji",
                        principalTable: "post_messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_post_message_attachments_MessageId_CreatedAt",
                schema: "homeji",
                table: "post_message_attachments",
                columns: MessageCreatedColumns);

            migrationBuilder.CreateIndex(
                name: "IX_post_message_attachments_UploaderId_CreatedAt",
                schema: "homeji",
                table: "post_message_attachments",
                columns: UploaderCreatedColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "post_message_attachments",
                schema: "homeji");
        }
    }
