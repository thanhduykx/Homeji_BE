using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861 // Migration runs once.

namespace Homeji.Infrastructure.Migrations;
    /// <inheritdoc />
    public partial class AddIndependentRoommateDiscovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "rental_post_id",
                schema: "homeji",
                table: "roommate_invitations",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateTable(
                name: "roommate_profiles",
                schema: "homeji",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Intent = table.Column<int>(type: "integer", nullable: false),
                    IsDiscoverable = table.Column<bool>(type: "boolean", nullable: false),
                    Introduction = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roommate_profiles", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_roommate_profiles_user_profiles_UserId",
                        column: x => x.UserId,
                        principalSchema: "homeji",
                        principalTable: "user_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_roommate_profiles_IsDiscoverable_Intent_UpdatedAt",
                schema: "homeji",
                table: "roommate_profiles",
                columns: new[] { "IsDiscoverable", "Intent", "UpdatedAt" });
            migrationBuilder.Sql("ALTER TABLE homeji.roommate_profiles ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("REVOKE ALL ON homeji.roommate_profiles FROM anon, authenticated;");
            migrationBuilder.Sql("CREATE UNIQUE INDEX ux_independent_roommate_connection ON homeji.roommate_invitations (LEAST(sender_id, receiver_id), GREATEST(sender_id, receiver_id)) WHERE rental_post_id IS NULL AND status IN (1, 2);");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DO $$ BEGIN IF EXISTS (SELECT 1 FROM homeji.roommate_invitations WHERE rental_post_id IS NULL) THEN RAISE EXCEPTION 'Cannot remove independent roommate support while invitations exist'; END IF; END $$;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS homeji.ux_independent_roommate_connection;");
            migrationBuilder.DropTable(
                name: "roommate_profiles",
                schema: "homeji");

            migrationBuilder.AlterColumn<Guid>(
                name: "rental_post_id",
                schema: "homeji",
                table: "roommate_invitations",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
