using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Homeji.Infrastructure.Migrations;
/// <inheritdoc />
public partial class AddRentalSourceContactPhones : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "contact_phones",
            schema: "homeji",
            table: "rental_source_listings",
            type: "jsonb",
            nullable: false,
            defaultValueSql: "'[]'::jsonb");

        migrationBuilder.Sql(
            "UPDATE homeji.rental_source_listings SET contact_phones = jsonb_build_array(contact_phone) WHERE contact_phone IS NOT NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "contact_phones",
            schema: "homeji",
            table: "rental_source_listings");
    }
}
