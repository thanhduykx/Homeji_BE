using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Homeji.Infrastructure.Migrations;
/// <inheritdoc />
public partial class AddSignupVerificationChallenges : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "signup_verification_challenges",
            schema: "homeji",
            columns: table => new
            {
                Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CodeExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                NextSendAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Attempts = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_signup_verification_challenges", x => x.Email);
            });

        migrationBuilder.CreateIndex(
            name: "IX_signup_verification_challenges_ExpiresAt",
            schema: "homeji",
            table: "signup_verification_challenges",
            column: "ExpiresAt");

        migrationBuilder.Sql("ALTER TABLE homeji.signup_verification_challenges ENABLE ROW LEVEL SECURITY;");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "signup_verification_challenges",
            schema: "homeji");
    }
}
