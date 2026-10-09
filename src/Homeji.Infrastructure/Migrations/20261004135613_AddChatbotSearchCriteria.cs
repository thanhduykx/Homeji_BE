using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Homeji.Infrastructure.Migrations;
/// <inheritdoc />
public partial class AddChatbotSearchCriteria : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "search_criteria_json",
            schema: "homeji",
            table: "chat_conversations",
            type: "character varying(8000)",
            maxLength: 8000,
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "search_criteria_json",
            schema: "homeji",
            table: "chat_conversations");
    }
}
