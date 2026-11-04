using Homeji.Infrastructure.Context;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Homeji.Infrastructure.Migrations;

/// <summary>The function body qualifies every table, so an empty search_path cannot change what it resolves.</summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20261104120000_PinBlockedMessageTriggerSearchPath")]
public partial class PinBlockedMessageTriggerSearchPath : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("ALTER FUNCTION homeji.reject_blocked_post_message() SET search_path = '';");

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("ALTER FUNCTION homeji.reject_blocked_post_message() RESET search_path;");
}
