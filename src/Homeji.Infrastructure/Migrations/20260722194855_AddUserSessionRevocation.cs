using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Homeji.Infrastructure.Migrations;

    /// <inheritdoc />
    public partial class AddUserSessionRevocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE homeji.user_profiles ADD COLUMN IF NOT EXISTS sessions_revoked_before timestamp with time zone NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "sessions_revoked_before",
                schema: "homeji",
                table: "user_profiles");
        }
}
