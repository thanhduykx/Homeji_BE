using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Homeji.Infrastructure.Migrations;

public partial class AddChatSearchIntent : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "search_intent_json", schema: "homeji", table: "chat_conversations",
            type: "character varying(8000)", maxLength: 8000, nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "search_intent_json", schema: "homeji", table: "chat_conversations");
    }
}
