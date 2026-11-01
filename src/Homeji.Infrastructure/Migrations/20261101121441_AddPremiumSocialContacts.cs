using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Homeji.Infrastructure.Migrations;
    /// <inheritdoc />
    public partial class AddPremiumSocialContacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "facebook_url",
                schema: "homeji",
                table: "user_profiles",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "zalo_phone",
                schema: "homeji",
                table: "user_profiles",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "facebook_url",
                schema: "homeji",
                table: "user_profiles");

            migrationBuilder.DropColumn(
                name: "zalo_phone",
                schema: "homeji",
                table: "user_profiles");
        }
    }
