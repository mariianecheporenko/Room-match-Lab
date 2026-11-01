using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Room_match_Lab.Migrations;

public partial class AddProfilePasswordHash : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "PasswordHash",
            table: "Profiles",
            type: "character varying(512)",
            maxLength: 512,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "PasswordHash", table: "Profiles");
    }
}
