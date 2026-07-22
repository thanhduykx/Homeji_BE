using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Homeji.Infrastructure.Context;

#nullable disable

namespace Homeji.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260723000100_RepairUserSessionRevocationColumn")]
public partial class RepairUserSessionRevocationColumn : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE homeji.user_profiles ADD COLUMN IF NOT EXISTS sessions_revoked_before timestamp with time zone NULL;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Intentionally left empty: the repair migration must never remove live session-revocation data.
    }
}
