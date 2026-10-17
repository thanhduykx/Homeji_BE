using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Homeji.Infrastructure.Migrations;

/// <inheritdoc />
public partial class LinkSignupChallengesToAuthUsers : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Supabase owns auth.users, so maintain this cross-schema relationship explicitly.
        migrationBuilder.Sql("""
            DELETE FROM homeji.signup_verification_challenges AS challenge
            WHERE NOT EXISTS (SELECT 1 FROM auth.users AS account WHERE account.id = challenge."UserId");
            """);
        migrationBuilder.AddForeignKey(
            name: "fk_signup_verification_challenges_auth_user",
            schema: "homeji", table: "signup_verification_challenges", column: "UserId",
            principalSchema: "auth", principalTable: "users", principalColumn: "id",
            onDelete: ReferentialAction.Cascade);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "fk_signup_verification_challenges_auth_user",
            schema: "homeji", table: "signup_verification_challenges");
    }
}
