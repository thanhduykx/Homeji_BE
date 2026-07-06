using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Homeji.Infrastructure.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "homeji");

        migrationBuilder.CreateTable(
            name: "user_profiles",
            schema: "homeji",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                display_name = table.Column<string>(
                    type: "character varying(100)",
                    maxLength: 100,
                    nullable: false),
                created_at = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false),
                updated_at = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_user_profiles", profile => profile.id);
            });

        migrationBuilder.Sql(
            """
            ALTER TABLE homeji.user_profiles
                ADD CONSTRAINT fk_user_profiles_auth_users_id
                FOREIGN KEY (id) REFERENCES auth.users(id) ON DELETE CASCADE;

            ALTER TABLE homeji.user_profiles ENABLE ROW LEVEL SECURITY;

            REVOKE ALL ON SCHEMA homeji FROM PUBLIC, anon, authenticated;
            REVOKE ALL ON TABLE homeji.user_profiles FROM PUBLIC, anon, authenticated;

            COMMENT ON TABLE homeji.user_profiles IS
                'Application profile data keyed by the Supabase Auth user id.';
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "user_profiles",
            schema: "homeji");

        migrationBuilder.Sql("DROP SCHEMA IF EXISTS homeji;");
    }
}
